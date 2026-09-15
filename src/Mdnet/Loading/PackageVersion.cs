namespace Mdnet.Loading;

/// <summary>Orders NuGet versions: numeric parts first, then release above prerelease.</summary>
public sealed class PackageVersionComparer : IComparer<string>
{
    public static readonly PackageVersionComparer Instance = new();

    public int Compare(string? x, string? y)
    {
        if (x is null || y is null)
        {
            return string.Compare(x, y, StringComparison.Ordinal);
        }

        var (xNumbers, xLabel) = Split(x);
        var (yNumbers, yLabel) = Split(y);
        for (var i = 0; i < Math.Max(xNumbers.Length, yNumbers.Length); i++)
        {
            var cmp = xNumbers.ElementAtOrDefault(i).CompareTo(yNumbers.ElementAtOrDefault(i));
            if (cmp != 0)
            {
                return cmp;
            }
        }

        return (xLabel, yLabel) switch
        {
            (null, null) => 0,
            (null, _) => 1,
            (_, null) => -1,
            _ => string.Compare(xLabel, yLabel, StringComparison.OrdinalIgnoreCase),
        };
    }

    private static (long[] Numbers, string? Label) Split(string version)
    {
        version = version.Split('+')[0];
        var dash = version.IndexOf('-', StringComparison.Ordinal);
        var core = dash < 0 ? version : version[..dash];
        var numbers = core.Split('.').Select(p => long.TryParse(p, out var n) ? n : 0).ToArray();
        return (numbers, dash < 0 ? null : version[(dash + 1)..]);
    }
}
