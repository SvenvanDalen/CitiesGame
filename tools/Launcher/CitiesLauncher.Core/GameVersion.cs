using System.Globalization;

namespace CitiesLauncher.Core;

/// <summary>Versienummer als reeks getallen ("0.12" of "1.2.3"); vergelijkt numeriek, dus 0.10 is nieuwer dan 0.9.</summary>
public sealed class GameVersion : IComparable<GameVersion>, IEquatable<GameVersion>
{
    private readonly int[] _parts;

    private GameVersion(int[] parts) => _parts = parts;

    /// <summary>Parseert "0.12", "v0.12" of "1.2.3" (ook met -suffix of +metadata, die genegeerd wordt).</summary>
    public static bool TryParse(string? text, out GameVersion version)
    {
        version = null!;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var core = text.Trim();
        if (core.StartsWith('v') || core.StartsWith('V'))
        {
            core = core[1..];
        }

        var cut = core.IndexOfAny(['-', '+', ' ']);
        if (cut >= 0)
        {
            core = core[..cut];
        }

        var segments = core.Split('.');
        var parts = new int[segments.Length];
        for (var i = 0; i < segments.Length; i++)
        {
            if (!int.TryParse(segments[i], NumberStyles.None, CultureInfo.InvariantCulture, out parts[i]))
            {
                return false;
            }
        }

        version = new GameVersion(parts);
        return true;
    }

    public int CompareTo(GameVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        var length = Math.Max(_parts.Length, other._parts.Length);
        for (var i = 0; i < length; i++)
        {
            var a = i < _parts.Length ? _parts[i] : 0;
            var b = i < other._parts.Length ? other._parts[i] : 0;
            if (a != b)
            {
                return a.CompareTo(b);
            }
        }

        return 0;
    }

    public bool Equals(GameVersion? other) => other is not null && CompareTo(other) == 0;

    public override bool Equals(object? obj) => obj is GameVersion other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        var last = _parts.Length;
        while (last > 0 && _parts[last - 1] == 0)
        {
            last--;
        }

        for (var i = 0; i < last; i++)
        {
            hash.Add(_parts[i]);
        }

        return hash.ToHashCode();
    }

    public override string ToString() => string.Join('.', _parts);

    public static bool operator >(GameVersion a, GameVersion b) => a.CompareTo(b) > 0;

    public static bool operator <(GameVersion a, GameVersion b) => a.CompareTo(b) < 0;
}
