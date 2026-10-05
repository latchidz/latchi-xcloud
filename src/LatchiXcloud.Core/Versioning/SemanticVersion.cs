using System.Globalization;

namespace LatchiXcloud.Core.Versioning;

/// <summary>Minimal semver (v-prefixed tolerated) — enough to compare Better xCloud releases.</summary>
public sealed class SemanticVersion : IComparable<SemanticVersion>
{
    public int Major { get; }
    public int Minor { get; }
    public int Patch { get; }

    private SemanticVersion(int major, int minor, int patch)
    { Major = major; Minor = minor; Patch = patch; }

    public static bool TryParse(string? text, out SemanticVersion version)
    {
        version = new SemanticVersion(0, 0, 0);
        if (string.IsNullOrWhiteSpace(text)) return false;
        var t = text.Trim().TrimStart('v', 'V');
        var parts = t.Split('.');
        if (parts.Length is < 1 or > 4) return false;
        var nums = new int[3];
        for (var i = 0; i < 3; i++)
        {
            if (i < parts.Length)
            {
                var digits = new string(parts[i].TakeWhile(char.IsDigit).ToArray());
                if (digits.Length == 0 || !int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
                    return false;
                // tolerate pre-release suffix on the last part (e.g. 6.7.12-beta1 → treat as its own version)
                nums[i] = n;
            }
        }
        version = new SemanticVersion(nums[0], nums[1], nums[2]);
        return true;
    }

    public int CompareTo(SemanticVersion? other)
    {
        if (other is null) return 1;
        int c = Major.CompareTo(other.Major); if (c != 0) return c;
        c = Minor.CompareTo(other.Minor); if (c != 0) return c;
        return Patch.CompareTo(other.Patch);
    }

    public override string ToString() => $"{Major}.{Minor}.{Patch}";
    public static bool operator >(SemanticVersion a, SemanticVersion b) => a.CompareTo(b) > 0;
    public static bool operator <(SemanticVersion a, SemanticVersion b) => a.CompareTo(b) < 0;
}
