using System.Text.Json;
using System.Text.Json.Serialization;

namespace LatchiXcloud.Core.Services;

/// <summary>Single shared JSON options: tolerant reader, strict writer (camelCase on disk).</summary>
public static class Json
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static readonly JsonSerializerOptions Lenient = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T? Deserialize<T>(string? json)
    {
        if (string.IsNullOrEmpty(json)) return default;
        try { return JsonSerializer.Deserialize<T>(json, Lenient); }
        catch (JsonException) { return default; } // corrupt file → defaults, never a crash
    }
}
