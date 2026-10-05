using System.Text.RegularExpressions;

namespace LatchiXcloud.Core.Userscript;

/// <summary>
/// Converts Tampermonkey-style match patterns to JavaScript regex sources.
/// Rule (Violentmonkey-compatible approximation): every '*' matches any run of
/// characters, everything else is literal. Anchored ^...$ on the full URL.
/// </summary>
public static class PatternConverter
{
    public static string ToJsRegexSource(string pattern)
    {
        var sb = new System.Text.StringBuilder("^");
        foreach (var ch in pattern)
        {
            if (ch == '*') sb.Append(".*");
            else sb.Append(Regex.Escape(ch.ToString()));
        }
        sb.Append('$');
        return sb.ToString();
    }

    /// <summary>True when the URL satisfies the @match/@exclude rules of a userscript.</summary>
    public static bool TestManaged(string url, UserscriptMetadata meta)
    {
        bool Included() => meta.Matches.Any(p => Regex.IsMatch(url, ToJsRegexSource(p)));
        bool Excluded() => meta.Excludes.Any(p => Regex.IsMatch(url, ToJsRegexSource(p)));
        return Included() && !Excluded();
    }
}
