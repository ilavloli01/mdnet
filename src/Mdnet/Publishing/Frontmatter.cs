using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Mdnet.Publishing;

/// <summary>Frontmatter keys of a package <c>index.md</c>.</summary>
public static class PackageFields
{
    public const string Version = "version";
    public const string Title = "title";
    public const string Description = "description";
    public const string Authors = "authors";
    public const string Company = "company";
    public const string Copyright = "copyright";
    public const string Tags = "tags";
    public const string License = "license";
    public const string Project = "project";
    public const string Repository = "repository";
    public const string Frameworks = "frameworks";
    public const string Types = "types";
}

/// <summary>
/// Minimal YAML frontmatter: one <c>key: value</c> per line, values JSON-quoted only when plain YAML would misread them.
/// </summary>
public static partial class Frontmatter
{
    public static string Write(IEnumerable<KeyValuePair<string, string?>> values)
    {
        var sb = new StringBuilder();
        foreach (var (key, value) in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                sb.Append(key).Append(": ").Append(Quote(value)).Append('\n');
            }
        }

        return sb.Length == 0 ? "" : $"---\n{sb}---\n";
    }

    /// <summary>Frontmatter of a Markdown document, or an empty map.</summary>
    public static IReadOnlyDictionary<string, string> Parse(string markdown)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lines = markdown.ReplaceLineEndings("\n").Split('\n');
        if (lines.Length == 0 || lines[0].TrimEnd() != "---")
        {
            return result;
        }

        foreach (var line in lines.Skip(1))
        {
            if (line.TrimEnd() == "---")
            {
                return result;
            }

            var separator = line.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            var value = line[(separator + 1)..].Trim();
            if (value.StartsWith('"'))
            {
                try
                {
                    using var json = JsonDocument.Parse(value);
                    value = json.RootElement.GetString() ?? "";
                }
                catch (JsonException)
                {
                    continue;
                }
            }

            result[line[..separator].Trim()] = value;
        }

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private static string Quote(string value) =>
        PlainValue().IsMatch(value) && !value.Contains(": ", StringComparison.Ordinal) && !value.Contains(" #", StringComparison.Ordinal)
            ? value
            : $"\"{JsonEncodedText.Encode(value, JavaScriptEncoder.UnsafeRelaxedJsonEscaping)}\"";

    /// <summary>Starts with a character that has no YAML meaning, has no line breaks and no trailing space.</summary>
    [GeneratedRegex(@"^[\p{L}\p{N}(./_$][^\r\n]*(?<!\s|:)$")]
    private static partial Regex PlainValue();
}
