namespace CitiesLauncher.Core;

/// <summary>De volledige updatestroom zonder het spel te starten; geeft aan of er bijgewerkt is. Gooit nooit: fouten worden gelogd en het spel start gewoon.</summary>
public sealed class LauncherRunner(string baseDirectory, string launcherFileName, UpdateService service, Action<string> log)
{
    public const string GameFolder = "Game";
    public const string VersionFileName = "version.txt";

    public string GameDirectory => Path.Combine(baseDirectory, GameFolder);

    public GameVersion? ReadLocalVersion()
    {
        var path = Path.Combine(GameDirectory, VersionFileName);
        return File.Exists(path) && GameVersion.TryParse(File.ReadAllText(path), out var version) ? version : null;
    }

    public async Task<bool> UpdateIfNeededAsync(CancellationToken cancellation)
    {
        var installer = new Installer(baseDirectory, launcherFileName);
        installer.FinishPendingLauncherUpdate();

        var local = ReadLocalVersion();
        var (release, error) = await service.GetLatestAsync(cancellation).ConfigureAwait(false);
        if (error is not null)
        {
            log($"Updatecontrole overgeslagen: {error}");
        }

        var decision = UpdateDecision.Decide(local, release);
        log(decision.Reason);
        if (decision.Action != UpdateAction.Update || decision.Release is null)
        {
            return false;
        }

        var work = Path.Combine(Path.GetTempPath(), "CitiesLauncher-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(work);
            var zip = Path.Combine(work, decision.Release.Asset.Name);
            var lastPercent = -1;
            var progress = new Progress<(long Done, long Total)>(p =>
            {
                var percent = p.Total > 0 ? (int)(p.Done * 100 / p.Total) : 0;
                if (percent / 10 != lastPercent / 10)
                {
                    lastPercent = percent;
                    log($"Downloaden: {percent}%");
                }
            });

            var downloadError = await service.DownloadAsync(decision.Release.Asset, zip, progress, cancellation).ConfigureAwait(false);
            if (downloadError is not null)
            {
                log(downloadError);
                return false;
            }

            var staging = Path.Combine(work, "staging");
            var files = SafeZip.Extract(zip, staging);
            if (!files.Contains(GameFolder + "/" + VersionFileName))
            {
                log("De download bevat geen Game/version.txt; update afgebroken.");
                return false;
            }

            installer.Install(staging, files);
            log($"Bijgewerkt naar {decision.Release.Version}.");
            return true;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or OperationCanceledException)
        {
            log($"Bijwerken mislukt, het huidige spel blijft staan: {e.Message}");
            return false;
        }
        finally
        {
            try
            {
                Directory.Delete(work, recursive: true);
            }
            catch (IOException)
            {
                // Tijdelijke map; de OS-opruiming doet de rest.
            }
        }
    }
}
