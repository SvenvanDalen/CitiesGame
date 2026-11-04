using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using CitiesLauncher.Core;
using NUnit.Framework;

namespace CitiesLauncher.Tests;

public class GameVersionTests
{
    [TestCase("0.12", "0.12", 0)]
    [TestCase("0.10", "0.9", 1)]
    [TestCase("0.9", "0.10", -1)]
    [TestCase("v1.0", "1.0.0", 0)]
    [TestCase("1.2.3", "1.2", 1)]
    [TestCase("0.5-beta+abc", "0.5", 0)]
    public void Compares_numerically(string a, string b, int expected)
    {
        Assert.That(GameVersion.TryParse(a, out var va), Is.True);
        Assert.That(GameVersion.TryParse(b, out var vb), Is.True);
        Assert.That(Math.Sign(va.CompareTo(vb)), Is.EqualTo(expected));
        Assert.That(va.Equals(vb), Is.EqualTo(expected == 0));
        Assert.That(va > vb, Is.EqualTo(expected > 0));
        Assert.That(va < vb, Is.EqualTo(expected < 0));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("abc")]
    [TestCase("1..2")]
    [TestCase("1.-2")]
    public void Rejects_invalid(string? text) => Assert.That(GameVersion.TryParse(text, out _), Is.False);

    [Test]
    public void Equal_versions_share_hash_and_print_normalized()
    {
        GameVersion.TryParse("1.0", out var a);
        GameVersion.TryParse("v1.0.0", out var b);
        Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        Assert.That(a.Equals((object)b), Is.True);
        GameVersion? none = null;
        Assert.That(b.Equals(none), Is.False);
        Assert.That(b.CompareTo(none), Is.EqualTo(1));
        Assert.That(a.ToString(), Is.EqualTo("1.0"));
    }
}

public class ReleaseInfoTests
{
    private const string Hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private static string Json(string tag = "v0.15", string extra = "", string assetName = "CitiesGame-0.15-win64.zip", string digest = "sha256:" + Hash) =>
        "{\"tag_name\":\"" + tag + "\",\"body\":\"Notities\"," + extra + "\"assets\":["
        + "{\"name\":\"other.txt\",\"browser_download_url\":\"https://x/o\",\"size\":1},"
        + "{\"name\":\"" + assetName + "\",\"browser_download_url\":\"https://x/a.zip\",\"size\":42,\"digest\":\"" + digest + "\"}]}";

    [Test]
    public void Parses_release_with_zip_asset()
    {
        Assert.That(ReleaseInfo.TryParse(Json(), out var release), Is.True);
        Assert.That(release.Version.ToString(), Is.EqualTo("0.15"));
        Assert.That(release.Notes, Is.EqualTo("Notities"));
        Assert.That(release.Asset, Is.EqualTo(new ReleaseAsset("CitiesGame-0.15-win64.zip", "https://x/a.zip", 42, Hash)));
    }

    [Test]
    public void Rejects_drafts_prereleases_and_missing_asset()
    {
        Assert.That(ReleaseInfo.TryParse(Json(extra: "\"draft\":true,"), out _), Is.False);
        Assert.That(ReleaseInfo.TryParse(Json(extra: "\"prerelease\":true,"), out _), Is.False);
        Assert.That(ReleaseInfo.TryParse(Json(assetName: "readme.txt"), out _), Is.False);
        Assert.That(ReleaseInfo.TryParse(Json(tag: "nightly"), out _), Is.False);
    }

    [TestCase("not json")]
    [TestCase("[]")]
    [TestCase("{}")]
    [TestCase("{\"tag_name\":\"v1\",\"assets\":5}")]
    public void Rejects_garbage(string json) => Assert.That(ReleaseInfo.TryParse(json, out _), Is.False);

    [Test]
    public void Digest_parsing()
    {
        Assert.That(ReleaseInfo.ParseDigest(null), Is.Null);
        Assert.That(ReleaseInfo.ParseDigest("md5:abc"), Is.Null);
        Assert.That(ReleaseInfo.ParseDigest("sha256:short"), Is.Null);
        Assert.That(ReleaseInfo.ParseDigest("SHA256:" + Hash.ToUpperInvariant()), Is.EqualTo(Hash));
    }

    [Test]
    public void Missing_size_and_digest_are_tolerated()
    {
        var json = "{\"tag_name\":\"v1\",\"assets\":[{\"name\":\"a-win64.zip\",\"browser_download_url\":\"u\"}]}";
        Assert.That(ReleaseInfo.TryParse(json, out var release), Is.True);
        Assert.That(release.Asset.Size, Is.EqualTo(-1));
        Assert.That(release.Asset.Sha256, Is.Null);
    }
}

public class UpdateDecisionTests
{
    private static ReleaseInfo Release(string version)
    {
        GameVersion.TryParse(version, out var v);
        return new ReleaseInfo("v" + version, v, "", new ReleaseAsset("a-win64.zip", "u", 1, null));
    }

    [Test]
    public void No_release_means_no_update() =>
        Assert.That(UpdateDecision.Decide(null, null).Action, Is.EqualTo(UpdateAction.None));

    [Test]
    public void Unknown_local_version_updates()
    {
        Assert.That(UpdateDecision.Decide(null, Release("1")).Action, Is.EqualTo(UpdateAction.Update));
    }

    [TestCase("0.9", "0.10", UpdateAction.Update)]
    [TestCase("0.10", "0.10", UpdateAction.None)]
    [TestCase("0.11", "0.10", UpdateAction.None)]
    public void Only_newer_release_updates(string local, string latest, UpdateAction expected)
    {
        GameVersion.TryParse(local, out var l);
        var decision = UpdateDecision.Decide(l, Release(latest));
        Assert.That(decision.Action, Is.EqualTo(expected));
        Assert.That(decision.Release is not null, Is.EqualTo(expected == UpdateAction.Update));
    }
}

public abstract class TempDirTest
{
    protected string Dir = "";

    [SetUp]
    public void CreateDir()
    {
        Dir = Path.Combine(Path.GetTempPath(), "cl-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Dir);
    }

    [TearDown]
    public void DeleteDir() => Directory.Delete(Dir, recursive: true);

    protected string MakeZip(params (string Name, string Content)[] entries)
    {
        var path = Path.Combine(Dir, Guid.NewGuid().ToString("N") + ".zip");
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (name, content) in entries)
        {
            using var writer = new StreamWriter(archive.CreateEntry(name).Open(), new UTF8Encoding(false));
            writer.Write(content);
        }

        return path;
    }
}

public class SafeZipTests : TempDirTest
{
    [Test]
    public void Extracts_files_with_relative_paths()
    {
        var zip = MakeZip(("a.txt", "A"), ("Game/b.txt", "B"));
        var files = SafeZip.Extract(zip, Path.Combine(Dir, "out"));
        Assert.That(files, Is.EquivalentTo(new[] { "a.txt", "Game/b.txt" }));
        Assert.That(File.ReadAllText(Path.Combine(Dir, "out", "Game", "b.txt")), Is.EqualTo("B"));
    }

    [Test]
    public void Rejects_zip_slip()
    {
        var zip = MakeZip(("../evil.txt", "x"));
        Assert.Throws<InvalidDataException>(() => SafeZip.Extract(zip, Path.Combine(Dir, "out")));
        Assert.That(File.Exists(Path.Combine(Dir, "evil.txt")), Is.False);
    }

    [Test]
    public void Creates_directory_entries()
    {
        var path = Path.Combine(Dir, "d.zip");
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            archive.CreateEntry("empty/");
        }

        var files = SafeZip.Extract(path, Path.Combine(Dir, "out"));
        Assert.That(files, Is.Empty);
        Assert.That(Directory.Exists(Path.Combine(Dir, "out", "empty")), Is.True);
    }
}

public class DownloadVerifierTests : TempDirTest
{
    [Test]
    public void Checks_size_and_hash()
    {
        var path = Path.Combine(Dir, "f.bin");
        File.WriteAllText(path, "hello");
        var hash = Convert.ToHexString(SHA256.HashData("hello"u8.ToArray()));

        Assert.That(DownloadVerifier.Verify(path, 5, hash), Is.Null);
        Assert.That(DownloadVerifier.Verify(path, -1, null), Is.Null);
        Assert.That(DownloadVerifier.Verify(path, 6, hash), Does.Contain("grootte"));
        Assert.That(DownloadVerifier.Verify(path, 5, new string('0', 64)), Does.Contain("SHA-256"));
    }
}

public class InstallerTests : TempDirTest
{
    private string Install => Path.Combine(Dir, "install");

    private string Stage(params (string Name, string Content)[] files)
    {
        var staging = Path.Combine(Dir, "staging-" + Guid.NewGuid().ToString("N"));
        foreach (var (name, content) in files)
        {
            var path = Path.Combine(staging, name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        return staging;
    }

    [Test]
    public void Replaces_files_keeps_unknown_files_and_removes_stale_manifest_entries()
    {
        Directory.CreateDirectory(Install);
        var installer = new Installer(Install, "CitiesLauncher.exe");
        installer.Install(Stage(("Game/a.dll", "1"), ("Game/old.dat", "1")), ["Game/a.dll", "Game/old.dat"]);
        File.WriteAllText(Path.Combine(Install, "Game", "user.cfg"), "mine");

        installer.Install(Stage(("Game/a.dll", "2"), ("Game/new.dat", "2")), ["Game/a.dll", "Game/new.dat"]);

        Assert.That(File.ReadAllText(Path.Combine(Install, "Game", "a.dll")), Is.EqualTo("2"));
        Assert.That(File.Exists(Path.Combine(Install, "Game", "old.dat")), Is.False);
        Assert.That(File.Exists(Path.Combine(Install, "Game", "new.dat")), Is.True);
        Assert.That(File.ReadAllText(Path.Combine(Install, "Game", "user.cfg")), Is.EqualTo("mine"));
        Assert.That(Directory.GetFiles(Path.Combine(Install, "Game"), "*.old"), Is.Empty);
    }

    [Test]
    public void Rolls_back_when_a_file_cannot_be_installed()
    {
        Directory.CreateDirectory(Path.Combine(Install, "Game"));
        File.WriteAllText(Path.Combine(Install, "Game", "a.dll"), "old");
        var installer = new Installer(Install, "CitiesLauncher.exe");
        var staging = Stage(("Game/a.dll", "new"));

        // Het tweede bestand ontbreekt in staging: de installatie faalt halverwege.
        Assert.Throws<FileNotFoundException>(() => installer.Install(staging, ["Game/a.dll", "Game/missing.dll"]));

        Assert.That(File.ReadAllText(Path.Combine(Install, "Game", "a.dll")), Is.EqualTo("old"));
        Assert.That(File.Exists(Path.Combine(Install, Installer.ManifestFileName)), Is.False);
    }

    [Test]
    public void Rejects_paths_outside_install_directory()
    {
        Directory.CreateDirectory(Install);
        var installer = new Installer(Install, "CitiesLauncher.exe");
        Assert.Throws<InvalidDataException>(() => installer.Install(Stage(("x.txt", "x")), ["../x.txt"]));
    }

    [Test]
    public void Launcher_is_moved_aside_not_overwritten_and_backup_is_cleaned_on_next_start()
    {
        Directory.CreateDirectory(Install);
        File.WriteAllText(Path.Combine(Install, "CitiesLauncher.exe"), "v1");
        var installer = new Installer(Install, "CitiesLauncher.exe");

        installer.Install(Stage(("CitiesLauncher.exe", "v2")), ["CitiesLauncher.exe"]);

        Assert.That(File.ReadAllText(Path.Combine(Install, "CitiesLauncher.exe")), Is.EqualTo("v2"));
        Assert.That(File.Exists(Path.Combine(Install, "CitiesLauncher.exe.old")), Is.True);
        installer.FinishPendingLauncherUpdate();
        Assert.That(File.Exists(Path.Combine(Install, "CitiesLauncher.exe.old")), Is.False);
    }

    [Test]
    public void Pending_launcher_is_activated_on_start()
    {
        Directory.CreateDirectory(Install);
        File.WriteAllText(Path.Combine(Install, "CitiesLauncher.exe"), "v1");
        File.WriteAllText(Path.Combine(Install, "CitiesLauncher.exe.new"), "v2");

        new Installer(Install, "CitiesLauncher.exe").FinishPendingLauncherUpdate();

        Assert.That(File.ReadAllText(Path.Combine(Install, "CitiesLauncher.exe")), Is.EqualTo("v2"));
        Assert.That(File.Exists(Path.Combine(Install, "CitiesLauncher.exe.new")), Is.False);
    }

    [Test]
    public void Malicious_manifest_never_deletes_outside_install_directory()
    {
        Directory.CreateDirectory(Install);
        var outside = Path.Combine(Dir, "outside.txt");
        File.WriteAllText(outside, "keep");
        File.WriteAllLines(Path.Combine(Install, Installer.ManifestFileName), ["../outside.txt", "", "CitiesLauncher.exe"]);

        new Installer(Install, "CitiesLauncher.exe").Install(Stage(("Game/a", "1")), ["Game/a"]);

        Assert.That(File.Exists(outside), Is.True);
    }
}

public class UpdateFlowTests : TempDirTest
{
    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }

    private static string ReleaseJson(string version, long size, string hash) =>
        "{\"tag_name\":\"v" + version + "\",\"assets\":[{\"name\":\"CitiesGame-" + version + "-win64.zip\","
        + "\"browser_download_url\":\"https://dl/zip\",\"size\":" + size + ",\"digest\":\"sha256:" + hash + "\"}]}";

    private (LauncherRunner Runner, List<string> Log) Runner(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var log = new List<string>();
        var service = new UpdateService(new HttpClient(new FakeHandler(respond)), "https://api/latest");
        return (new LauncherRunner(Path.Combine(Dir, "app"), "CitiesLauncher.exe", service, log.Add), log);
    }

    private void SetLocalVersion(string version)
    {
        var game = Path.Combine(Dir, "app", "Game");
        Directory.CreateDirectory(game);
        File.WriteAllText(Path.Combine(game, "version.txt"), version);
    }

    private static HttpResponseMessage Ok(string text) => new(HttpStatusCode.OK) { Content = new StringContent(text) };

    private static HttpResponseMessage Ok(byte[] bytes) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };

    [Test]
    public async Task Downloads_and_installs_newer_release()
    {
        SetLocalVersion("0.1");
        var zip = File.ReadAllBytes(MakeZip(("Game/version.txt", "0.2"), ("Game/Cities.exe", "exe"), ("CitiesLauncher.exe", "l2")));
        var hash = Convert.ToHexString(SHA256.HashData(zip));
        var (runner, log) = Runner(r => r.RequestUri!.AbsoluteUri.EndsWith("latest")
            ? Ok(ReleaseJson("0.2", zip.Length, hash))
            : Ok(zip));

        Assert.That(await runner.UpdateIfNeededAsync(CancellationToken.None), Is.True);

        Assert.That(runner.ReadLocalVersion()!.ToString(), Is.EqualTo("0.2"));
        Assert.That(File.Exists(Path.Combine(Dir, "app", "Game", "Cities.exe")), Is.True);
        Assert.That(log, Has.Some.Contains("Bijgewerkt"));
    }

    [Test]
    public async Task Does_nothing_when_up_to_date()
    {
        SetLocalVersion("0.2");
        var (runner, _) = Runner(_ => Ok(ReleaseJson("0.2", 1, new string('a', 64))));
        Assert.That(await runner.UpdateIfNeededAsync(CancellationToken.None), Is.False);
    }

    [TestCase(HttpStatusCode.Forbidden)]
    [TestCase(HttpStatusCode.NotFound)]
    public async Task Rate_limit_or_missing_release_is_not_fatal(HttpStatusCode code)
    {
        SetLocalVersion("0.1");
        var (runner, log) = Runner(_ => new HttpResponseMessage(code));
        Assert.That(await runner.UpdateIfNeededAsync(CancellationToken.None), Is.False);
        Assert.That(log, Has.Some.Contains("overgeslagen"));
    }

    [Test]
    public async Task Offline_is_not_fatal()
    {
        SetLocalVersion("0.1");
        var (runner, log) = Runner(_ => throw new HttpRequestException("offline"));
        Assert.That(await runner.UpdateIfNeededAsync(CancellationToken.None), Is.False);
        Assert.That(log, Has.Some.Contains("offline"));
    }

    [Test]
    public async Task Garbage_response_is_not_fatal()
    {
        SetLocalVersion("0.1");
        var (runner, _) = Runner(_ => Ok("<html>"));
        Assert.That(await runner.UpdateIfNeededAsync(CancellationToken.None), Is.False);
    }

    [Test]
    public async Task Corrupt_download_keeps_current_game()
    {
        SetLocalVersion("0.1");
        var zip = File.ReadAllBytes(MakeZip(("Game/version.txt", "0.2")));
        var (runner, log) = Runner(r => r.RequestUri!.AbsoluteUri.EndsWith("latest")
            ? Ok(ReleaseJson("0.2", zip.Length, new string('0', 64)))
            : Ok(zip));

        Assert.That(await runner.UpdateIfNeededAsync(CancellationToken.None), Is.False);
        Assert.That(runner.ReadLocalVersion()!.ToString(), Is.EqualTo("0.1"));
        Assert.That(log, Has.Some.Contains("SHA-256"));
    }

    [Test]
    public async Task Failed_download_status_is_reported()
    {
        SetLocalVersion("0.1");
        var (runner, log) = Runner(r => r.RequestUri!.AbsoluteUri.EndsWith("latest")
            ? Ok(ReleaseJson("0.2", 1, new string('0', 64)))
            : new HttpResponseMessage(HttpStatusCode.InternalServerError));
        Assert.That(await runner.UpdateIfNeededAsync(CancellationToken.None), Is.False);
        Assert.That(log, Has.Some.Contains("500"));
    }

    [Test]
    public async Task Zip_without_version_file_is_rejected()
    {
        SetLocalVersion("0.1");
        var zip = File.ReadAllBytes(MakeZip(("Game/Cities.exe", "exe")));
        var hash = Convert.ToHexString(SHA256.HashData(zip));
        var (runner, log) = Runner(r => r.RequestUri!.AbsoluteUri.EndsWith("latest")
            ? Ok(ReleaseJson("0.2", zip.Length, hash))
            : Ok(zip));

        Assert.That(await runner.UpdateIfNeededAsync(CancellationToken.None), Is.False);
        Assert.That(File.Exists(Path.Combine(Dir, "app", "Game", "Cities.exe")), Is.False);
        Assert.That(log, Has.Some.Contains("version.txt"));
    }

    [Test]
    public void Local_version_is_null_without_file()
    {
        var (runner, _) = Runner(_ => Ok(""));
        Assert.That(runner.ReadLocalVersion(), Is.Null);
    }
}
