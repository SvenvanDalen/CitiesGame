using System.Diagnostics;
using CitiesLauncher.Core;

// Dunne startpunt: de logica zit in CitiesLauncher.Core. Controleert op een nieuwere release en start daarna altijd het spel.
const string launcherFileName = "CitiesLauncher.exe";
const string gameFileName = "Cities.exe";

var baseDirectory = AppContext.BaseDirectory;
using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
http.DefaultRequestHeaders.UserAgent.ParseAdd("CitiesLauncher/1.0");
http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

var runner = new LauncherRunner(baseDirectory, launcherFileName,
    new UpdateService(http, UpdateService.DefaultLatestReleaseUrl), Console.WriteLine);

try
{
    using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(15));
    await runner.UpdateIfNeededAsync(cts.Token);
}
catch (Exception e)
{
    // Wat er ook misgaat: het spel moet kunnen starten.
    Console.WriteLine($"Updatecontrole mislukt: {e.Message}");
}

var gamePath = Path.Combine(runner.GameDirectory, gameFileName);
if (!File.Exists(gamePath))
{
    Console.WriteLine($"Het spel is niet gevonden: {gamePath}");
    return 1;
}

Process.Start(new ProcessStartInfo(gamePath) { WorkingDirectory = runner.GameDirectory, UseShellExecute = true });
return 0;
