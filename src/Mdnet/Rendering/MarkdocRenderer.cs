using Mdnet.Loading;

namespace Mdnet.Rendering;

/// <summary>Runs the bundled Markdoc + React renderer (<c>renderer/render.mjs</c>) with bun or node.</summary>
public static class MarkdocRenderer
{
    public static async Task<int> RenderAsync(string markdownDir, string htmlDir, string runtime, Log log, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(markdownDir))
        {
            throw new DirectoryNotFoundException($"Markdown docs not found: {markdownDir}");
        }

        var script = FindRenderer()
            ?? throw new InvalidOperationException("Renderer bundle (renderer/render.mjs) not found. Set MDNET_RENDERER or build the renderer (cd renderer && bun run build).");
        var executable = FindRuntime(runtime)
            ?? throw new InvalidOperationException(
                runtime == "auto"
                    ? "HTML rendering needs bun or node on PATH. Install one, or pass --format md."
                    : $"{runtime} was not found on PATH."
            );

        log.Verbose($"Rendering with {executable} {script}");
        var result = await ProcessRunner.RunAsync(executable, [script, markdownDir, htmlDir], cancellationToken: cancellationToken);
        if (result.Output.Length > 0)
        {
            log.Verbose(result.Output.TrimEnd());
        }

        if (!result.Success)
        {
            log.Error("Renderer failed:\n" + (result.Error.Length > 0 ? result.Error : result.Output).TrimEnd());
        }

        return result.ExitCode;
    }

    private static string? FindRenderer()
    {
        if (Environment.GetEnvironmentVariable("MDNET_RENDERER") is { Length: > 0 } configured)
        {
            return File.Exists(configured) ? configured : throw new FileNotFoundException($"MDNET_RENDERER does not exist: {configured}");
        }

        var bundled = Path.Combine(AppContext.BaseDirectory, "renderer", "render.mjs");
        if (File.Exists(bundled))
        {
            return bundled;
        }

        // Development: walk up to the repository's renderer/dist.
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "renderer", "dist", "render.mjs");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? FindRuntime(string runtime) =>
        runtime switch
        {
            "bun" => ProcessRunner.FindOnPath("bun"),
            "node" => ProcessRunner.FindOnPath("node"),
            _ => ProcessRunner.FindOnPath("bun") ?? ProcessRunner.FindOnPath("node"),
        };
}
