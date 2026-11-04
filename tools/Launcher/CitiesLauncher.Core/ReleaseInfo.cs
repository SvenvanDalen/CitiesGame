using System.Text.Json;

namespace CitiesLauncher.Core;

/// <summary>Het zip-bestand van een release met de gegevens om de download te controleren.</summary>
public sealed record ReleaseAsset(string Name, string DownloadUrl, long Size, string? Sha256);

/// <summary>De nieuwste release zoals GitHub die meldt.</summary>
public sealed record ReleaseInfo(string Tag, GameVersion Version, string Notes, ReleaseAsset Asset)
{
    /// <summary>Leest het antwoord van /releases/latest. Geeft false bij een onverwacht formaat, een draft/prerelease of zonder win64-zip.</summary>
    public static bool TryParse(string json, out ReleaseInfo release)
    {
        release = null!;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || GetString(root, "tag_name") is not { } tag
                || !GameVersion.TryParse(tag, out var version)
                || GetBool(root, "draft") || GetBool(root, "prerelease")
                || !root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var element in assets.EnumerateArray())
            {
                var name = GetString(element, "name");
                var url = GetString(element, "browser_download_url");
                if (name is null || url is null || !name.EndsWith("-win64.zip", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var size = element.TryGetProperty("size", out var s) && s.ValueKind == JsonValueKind.Number ? s.GetInt64() : -1;
                release = new ReleaseInfo(tag, version, GetString(root, "body") ?? string.Empty,
                    new ReleaseAsset(name, url, size, ParseDigest(GetString(element, "digest"))));
                return true;
            }

            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>"sha256:abc..." naar de hex-hash; andere algoritmes of leeg geven null.</summary>
    public static string? ParseDigest(string? digest)
    {
        const string prefix = "sha256:";
        if (digest is null || !digest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var hash = digest[prefix.Length..].Trim();
        return hash.Length == 64 ? hash.ToLowerInvariant() : null;
    }

    private static string? GetString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool GetBool(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
}
