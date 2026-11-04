namespace CitiesLauncher.Core;

/// <summary>Praat met GitHub: nieuwste release ophalen en de zip downloaden. Alle fouten worden teruggegeven, nooit gegooid, zodat het spel altijd kan starten.</summary>
public sealed class UpdateService(HttpClient http, string latestReleaseUrl)
{
    public const string DefaultLatestReleaseUrl = "https://api.github.com/repos/SvenvanDalen/CitiesGame/releases/latest";

    public async Task<(ReleaseInfo? Release, string? Error)> GetLatestAsync(CancellationToken cancellation)
    {
        try
        {
            using var response = await http.GetAsync(latestReleaseUrl, cancellation).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                // Onder meer 403/429 bij een bereikte rate limit en 404 zolang er nog geen release is.
                return (null, $"GitHub antwoordde met {(int)response.StatusCode}.");
            }

            var json = await response.Content.ReadAsStringAsync(cancellation).ConfigureAwait(false);
            return ReleaseInfo.TryParse(json, out var release)
                ? (release, null)
                : (null, "Onverwacht antwoord van GitHub.");
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return (null, $"Geen verbinding: {e.Message}");
        }
    }

    /// <summary>Downloadt de zip naar <paramref name="destination"/> en controleert grootte en hash. Geeft null bij succes, anders de foutmelding.</summary>
    public async Task<string?> DownloadAsync(ReleaseAsset asset, string destination, IProgress<(long Done, long Total)>? progress, CancellationToken cancellation)
    {
        try
        {
            using var response = await http.GetAsync(asset.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellation).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return $"Download mislukt: {(int)response.StatusCode}.";
            }

            var total = response.Content.Headers.ContentLength ?? asset.Size;
            await using (var source = await response.Content.ReadAsStreamAsync(cancellation).ConfigureAwait(false))
            await using (var target = File.Create(destination))
            {
                var buffer = new byte[81920];
                long done = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, cancellation).ConfigureAwait(false)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, read), cancellation).ConfigureAwait(false);
                    done += read;
                    progress?.Report((done, total));
                }
            }

            return DownloadVerifier.Verify(destination, asset.Size, asset.Sha256);
        }
        catch (Exception e) when (e is HttpRequestException or IOException or TaskCanceledException or OperationCanceledException)
        {
            return $"Download mislukt: {e.Message}";
        }
    }
}
