using System.Text.RegularExpressions;

namespace LatchiXcloud.Core.Userscript;

/// <summary>Parsed ==UserScript== header — the single source of truth for injection rules.</summary>
public sealed class UserscriptMetadata
{
    public string Name { get; set; } = "";
    public string Namespace { get; set; } = "";
    public string Version { get; set; } = "";
    public string Description { get; set; } = "";
    public string License { get; set; } = "";
    public string RunAt { get; set; } = "document-idle";
    public string UpdateUrl { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public List<string> Matches { get; set; } = new();
    public List<string> Excludes { get; set; } = new();
    public List<string> Grants { get; set; } = new();

    public static UserscriptMetadata Parse(string source)
    {
        var m = Regex.Match(source, @"==UserScript==\s*(?<body>.*?)\s*==/UserScript==", RegexOptions.Singleline);
        if (!m.Success) throw new FormatException("No ==UserScript== metadata block found");
        var meta = new UserscriptMetadata();
        foreach (var rawLine in m.Groups["body"].Value.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.StartsWith("//", StringComparison.Ordinal)) line = line.TrimStart('/').Trim();
            var kv = Regex.Match(line, @"^@([\w.-]+)\s+(.*)$");
            if (!kv.Success) continue;
            var key = kv.Groups[1].Value;
            var value = kv.Groups[2].Value.Trim();
            switch (key)
            {
                case "name": meta.Name = value; break;
                case "namespace": meta.Namespace = value; break;
                case "version": meta.Version = value; break;
                case "description": meta.Description = value; break;
                case "license": meta.License = value; break;
                case "run-at": meta.RunAt = value; break;
                case "updateURL": meta.UpdateUrl = value; break;
                case "downloadURL": meta.DownloadUrl = value; break;
                case "match": meta.Matches.Add(value); break;
                case "exclude": meta.Excludes.Add(value); break;
                case "grant": meta.Grants.Add(value); break;
            }
        }
        return meta;
    }

    public bool IsGrantless => Grants.Count == 0 || Grants.All(g => g == "none");
}
