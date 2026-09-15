using System.Runtime.CompilerServices;

namespace Mdnet.Tests;

/// <summary>
/// Compares text against <c>Snapshots/&lt;Class&gt;.&lt;Test&gt;.verified.&lt;ext&gt;</c>. On mismatch writes a
/// <c>.received</c> file next to it; run with <c>MDNET_ACCEPT=1</c> to accept.
/// </summary>
internal static class Snapshot
{
    public static async Task Match(
        string actual,
        string extension,
        [CallerFilePath] string callerFile = "",
        [CallerMemberName] string test = ""
    )
    {
        actual = actual.Replace("\r\n", "\n", StringComparison.Ordinal);
        var directory = Path.Combine(Path.GetDirectoryName(callerFile)!, "Snapshots");
        var name = $"{Path.GetFileNameWithoutExtension(callerFile)}.{test}";
        var verified = Path.Combine(directory, $"{name}.verified.{extension}");
        var received = Path.Combine(directory, $"{name}.received.{extension}");
        Directory.CreateDirectory(directory);

        var expected = File.Exists(verified) ? (await File.ReadAllTextAsync(verified)).Replace("\r\n", "\n", StringComparison.Ordinal) : null;
        if (expected == actual)
        {
            File.Delete(received);
            return;
        }

        if (Environment.GetEnvironmentVariable("MDNET_ACCEPT") == "1")
        {
            await File.WriteAllTextAsync(verified, actual);
            File.Delete(received);
            return;
        }

        await File.WriteAllTextAsync(received, actual);
        throw new InvalidOperationException(
            expected is null
                ? $"New snapshot {received}. Review it and accept with MDNET_ACCEPT=1."
                : $"Snapshot mismatch: diff {verified} {received}. Accept with MDNET_ACCEPT=1."
        );
    }
}
