using System.Text;
using System.Text.RegularExpressions;

namespace Mdnet.Markdown;

/// <summary>
/// Embeds a project or namespace <c>README.md</c> into a generated page: removes a title that only repeats the
/// package name, demotes headings below the page structure and drops links that only work inside the repository.
/// </summary>
public static partial class ReadmeImporter
{
    /// <param name="markdown">README content.</param>
    /// <param name="minHeadingLevel">Level the README's shallowest heading is shifted to.</param>
    /// <param name="titles">Names the page already shows (package id, namespace); a leading H1 repeating one of them is dropped.</param>
    public static string? Import(string markdown, int minHeadingLevel, params string[] titles)
    {
        var lines = markdown.ReplaceLineEndings("\n").Split('\n').ToList();
        var fences = FenceMask(lines);

        var first = lines.FindIndex(l => l.Trim().Length > 0);
        if (first >= 0 && !fences[first] && Heading().Match(lines[first]) is { Success: true } title
            && title.Groups["hashes"].Length == 1 && titles.Any(t => Letters(t) == Letters(title.Groups["text"].Value)))
        {
            lines.RemoveAt(first);
            fences.RemoveAt(first);
        }

        var shallowest = lines.Where((l, i) => !fences[i]).Select(l => Heading().Match(l)).Where(m => m.Success).Select(m => m.Groups["hashes"].Length).DefaultIfEmpty(minHeadingLevel).Min();
        var shift = minHeadingLevel - shallowest;

        var sb = new StringBuilder();
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (!fences[i])
            {
                if (Heading().Match(line) is { Success: true } heading)
                {
                    line = new string('#', Math.Clamp(heading.Groups["hashes"].Length + shift, 1, 6)) + " " + heading.Groups["text"].Value;
                }

                line = RelativeImage().Replace(line, m => IsRelative(m.Groups["url"].Value) ? "" : m.Value);
                line = Link().Replace(line, m => IsRelative(m.Groups["url"].Value) ? m.Groups["label"].Value : m.Value);
                line = line.Replace("{%", "\\{%", StringComparison.Ordinal);
            }

            sb.Append(line).Append('\n');
        }

        var result = Regex.Replace(sb.ToString(), @"\n{3,}", "\n\n").Trim();
        return result.Length == 0 ? null : result;
    }

    /// <summary>First prose paragraph as plain text (headings, code, lists, quotes and tables skipped).</summary>
    public static string? FirstParagraph(string markdown)
    {
        var lines = markdown.ReplaceLineEndings("\n").Split('\n');
        var fences = FenceMask(lines);
        var paragraph = new List<string>();
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            var prose = !fences[i] && line.Length > 0 && !line.StartsWith('#') && !line.StartsWith('>') && !line.StartsWith('|')
                && !line.StartsWith("```", StringComparison.Ordinal) && !line.StartsWith("~~~", StringComparison.Ordinal)
                && !ListItem().IsMatch(line) && !line.StartsWith('<') && !line.StartsWith("![", StringComparison.Ordinal);
            if (prose)
            {
                paragraph.Add(line);
            }
            else if (paragraph.Count > 0)
            {
                break;
            }
        }

        if (paragraph.Count == 0)
        {
            return null;
        }

        var text = string.Join(" ", paragraph);
        text = Link().Replace(text, m => m.Groups["label"].Value);
        text = Regex.Replace(text, @"(\*\*|__|`)", "");
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    /// <summary>True for every line that is inside a fenced code block, including the fence lines.</summary>
    private static List<bool> FenceMask(IReadOnlyList<string> lines)
    {
        var mask = new List<bool>(lines.Count);
        string? fence = null;
        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();
            if (fence is null)
            {
                var open = Fence().Match(trimmed);
                if (open.Success)
                {
                    fence = open.Value;
                    mask.Add(true);
                    continue;
                }

                mask.Add(false);
            }
            else
            {
                mask.Add(true);
                if (trimmed.StartsWith(fence, StringComparison.Ordinal) && trimmed.Trim().All(c => c == fence[0]))
                {
                    fence = null;
                }
            }
        }

        return mask;
    }

    private static bool IsRelative(string url) =>
        !url.StartsWith('#') && !url.StartsWith("//", StringComparison.Ordinal) && !Scheme().IsMatch(url);

    private static string Letters(string text) => new string(text.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    [GeneratedRegex(@"^(?<hashes>#{1,6})\s+(?<text>.*?)\s*#*\s*$")]
    private static partial Regex Heading();

    [GeneratedRegex(@"^(`{3,}|~{3,})")]
    private static partial Regex Fence();

    [GeneratedRegex(@"(?<!!)\[(?<label>[^\]]*)\]\((?<url>[^)\s]+)(?:\s+""[^""]*"")?\)")]
    private static partial Regex Link();

    [GeneratedRegex(@"!\[[^\]]*\]\((?<url>[^)\s]+)(?:\s+""[^""]*"")?\)")]
    private static partial Regex RelativeImage();

    [GeneratedRegex(@"^([-*+]|\d+[.)])\s")]
    private static partial Regex ListItem();

    [GeneratedRegex(@"^[a-z][a-z0-9+.-]*:", RegexOptions.IgnoreCase)]
    private static partial Regex Scheme();
}
