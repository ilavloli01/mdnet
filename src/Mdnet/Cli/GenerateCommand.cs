using System.CommandLine;
using Mdnet.Loading;
using Mdnet.Markdown;
using Mdnet.Model;
using Mdnet.Rendering;
using Mdnet.Signatures;

namespace Mdnet.Cli;

internal static class GenerateCommand
{
    public static Command Create()
    {
        var path = new Argument<string>("path")
        {
            Description = "Solution (.sln/.slnx), project (.csproj) or directory.",
            DefaultValueFactory = _ => ".",
        };
        var packages = new Option<string[]>("--packages", "-p")
        {
            Description = "Package mode: document NuGet packages referenced by the projects whose id matches (e.g. \"Acme.*\"). Repeatable.",
            AllowMultipleArgumentsPerToken = true,
        };
        var output = new Option<string>("--output", "-o")
        {
            Description = "Output directory. Markdown goes to <output>/md, HTML to <output>/html.",
            DefaultValueFactory = _ => "docs",
        };
        var format = new Option<string[]>("--format", "-f")
        {
            Description = "Output formats: md, html.",
            DefaultValueFactory = _ => ["md", "html"],
            AllowMultipleArgumentsPerToken = true,
        };
        format.AcceptOnlyFromAmong("md", "html");
        var visibility = new Option<VisibilityLevel>("--visibility")
        {
            Description = "Lowest accessibility to document.",
            DefaultValueFactory = _ => VisibilityLevel.Protected,
        };
        var include = new Option<string[]>("--include")
        {
            Description = "Source mode: only projects whose name matches. Repeatable.",
            AllowMultipleArgumentsPerToken = true,
        };
        var exclude = new Option<string[]>("--exclude")
        {
            Description = "Source mode: skip projects whose name matches. Test projects are always skipped.",
            DefaultValueFactory = _ => ["*.Benchmarks", "*.Samples"],
            AllowMultipleArgumentsPerToken = true,
        };
        var runtime = RenderCommand.RuntimeOption();

        var command = new Command("generate", "Generate docs from source code or from referenced NuGet packages.")
        {
            path,
            packages,
            output,
            format,
            visibility,
            include,
            exclude,
            runtime,
        }.WithLogging();

        command.SetAction(
            (result, cancellationToken) =>
            {
                var log = CommonOptions.CreateLog(result);
                return CommonOptions.RunAsync(
                    log,
                    async () =>
                    {
                        var target = result.GetValue(path)!;
                        var patterns = new Glob(result.GetValue(packages) ?? []);
                        var level = new Visibility(result.GetValue(visibility));
                        IReadOnlyList<DocPackage> docs = patterns.IsEmpty
                            ? await new SourceLoader(log).LoadAsync(
                                target,
                                new Glob(result.GetValue(include) ?? []),
                                new Glob(result.GetValue(exclude) ?? []),
                                level,
                                cancellationToken
                            )
                            : await new PackageLoader(log).LoadAsync(target, patterns, level, cancellationToken);
                        if (docs.Count == 0)
                        {
                            log.Error("Nothing to document.");
                            return 1;
                        }

                        var outputDir = Path.GetFullPath(result.GetValue(output)!);
                        var markdownDir = Path.Combine(outputDir, "md");
                        new MarkdownWriter().Write(docs, markdownDir);
                        log.Info($"Markdown: {markdownDir} ({docs.Sum(d => d.Namespaces.Sum(n => n.Types.Count))} types in {docs.Count} package(s))");

                        if ((result.GetValue(format) ?? []).Contains("html"))
                        {
                            var htmlDir = Path.Combine(outputDir, "html");
                            var exit = await MarkdocRenderer.RenderAsync(markdownDir, htmlDir, result.GetValue(runtime)!, log, cancellationToken);
                            if (exit != 0)
                            {
                                return exit;
                            }

                            log.Info($"HTML: {htmlDir}");
                        }

                        return 0;
                    }
                );
            }
        );
        return command;
    }
}
