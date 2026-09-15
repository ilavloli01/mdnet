using System.Text.RegularExpressions;

namespace Mdnet.Loading;

/// <summary>Case-insensitive <c>*</c>/<c>?</c> name patterns; several may be separated by <c>;</c> or <c>,</c>.</summary>
public sealed class Glob
{
    private readonly Regex[] _patterns;

    public Glob(IEnumerable<string> patterns)
    {
        _patterns = patterns
            .SelectMany(p => p.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(p => new Regex("^" + Regex.Escape(p).Replace(@"\*", ".*").Replace(@"\?", ".") + "$", RegexOptions.IgnoreCase))
            .ToArray();
    }

    public bool IsEmpty => _patterns.Length == 0;

    public bool IsMatch(string name) => _patterns.Any(p => p.IsMatch(name));
}
