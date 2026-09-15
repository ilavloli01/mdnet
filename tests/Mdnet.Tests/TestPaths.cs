using System.Text;

namespace Mdnet.Tests;

internal static class TestPaths
{
    public static string RepoRoot { get; } = FindRepoRoot();

    public static string SampleProject => Path.Combine(RepoRoot, "samples", "Sample.Lib", "Sample.Lib.csproj");

    public static string TempDirectory(string name)
    {
        var dir = Path.Combine(Path.GetTempPath(), "mdnet-tests", name + "-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>All files under a folder as one snapshot text, so a single verified file shows the whole output.</summary>
    public static string Snapshot(string root)
    {
        var sb = new StringBuilder();
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            sb.Append("===== ").Append(relative).Append(" =====\n");
            sb.Append(File.ReadAllText(file).Replace("\r\n", "\n", StringComparison.Ordinal));
        }

        return sb.ToString();
    }

    private static string FindRepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "mdnet.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("mdnet.slnx not found above the test output directory.");
    }
}
