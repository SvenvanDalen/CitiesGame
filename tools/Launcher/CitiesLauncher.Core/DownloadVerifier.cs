using System.Security.Cryptography;

namespace CitiesLauncher.Core;

public static class DownloadVerifier
{
    /// <summary>Controleert grootte (als bekend, dus >= 0) en SHA-256 (als bekend). Geeft null bij succes, anders de foutmelding.</summary>
    public static string? Verify(string path, long expectedSize, string? expectedSha256)
    {
        var actualSize = new FileInfo(path).Length;
        if (expectedSize >= 0 && actualSize != expectedSize)
        {
            return $"Verkeerde grootte: {actualSize} bytes in plaats van {expectedSize}.";
        }

        if (expectedSha256 is null)
        {
            return null;
        }

        using var stream = File.OpenRead(path);
        var actual = Convert.ToHexString(SHA256.HashData(stream));
        return string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase)
            ? null
            : "De SHA-256-hash komt niet overeen.";
    }
}
