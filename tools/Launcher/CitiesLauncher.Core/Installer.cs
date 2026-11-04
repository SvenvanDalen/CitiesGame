namespace CitiesLauncher.Core;

/// <summary>
/// Zet de uitgepakte bestanden over de installatiemap. Per bestand wordt eerst een tijdelijke kopie naast het doel gezet en
/// dan hernoemd (atomair op dezelfde schijf); bij een fout worden alle eerder vervangen bestanden teruggezet.
/// Bestanden die niet in de zip stonden blijven staan, behalve die uit het manifest van de vorige installatie.
/// De launcher zelf wordt hernoemd in plaats van overschreven, want een draaiende exe is op Windows wel te hernoemen.
/// </summary>
public sealed class Installer(string installDirectory, string launcherFileName)
{
    public const string ManifestFileName = "installed-files.txt";
    public const string PendingSuffix = ".new";
    public const string BackupSuffix = ".old";

    private readonly string _root = Path.GetFullPath(installDirectory);

    /// <summary>Vervangt de spelbestanden door <paramref name="files"/> uit <paramref name="stagingDirectory"/>.</summary>
    public void Install(string stagingDirectory, IReadOnlyList<string> files)
    {
        var applied = new List<(string Target, string? Backup)>();
        try
        {
            foreach (var relative in files)
            {
                var source = Path.Combine(stagingDirectory, relative);
                var target = ResolveTarget(relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                var backup = Replace(source, target, IsLauncher(relative));
                applied.Add((target, backup));
            }
        }
        catch
        {
            Rollback(applied);
            throw;
        }

        // Alles staat er: backups opruimen (behalve die van de launcher, die nog draait), verouderde bestanden weghalen, manifest vernieuwen.
        foreach (var (target, backup) in applied)
        {
            if (!target.EndsWith(launcherFileName, StringComparison.OrdinalIgnoreCase))
            {
                TryDelete(backup);
            }
        }

        RemoveStale(files);
        File.WriteAllLines(Path.Combine(_root, ManifestFileName), files);
    }

    /// <summary>Bij het starten: een eerder uitgestelde nieuwe launcher alsnog activeren en achtergebleven .old-bestanden opruimen.</summary>
    public void FinishPendingLauncherUpdate()
    {
        var launcher = Path.Combine(_root, launcherFileName);
        var pending = launcher + PendingSuffix;
        if (File.Exists(pending))
        {
            try
            {
                Replace(pending, launcher, moveAside: true);
            }
            catch (IOException)
            {
                // Nog in gebruik: volgende start opnieuw proberen.
            }
        }

        TryDelete(launcher + BackupSuffix);
    }

    private bool IsLauncher(string relative) =>
        string.Equals(relative, launcherFileName, StringComparison.OrdinalIgnoreCase);

    private string ResolveTarget(string relative)
    {
        var target = Path.GetFullPath(Path.Combine(_root, relative));
        if (!target.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Onveilig pad: {relative}");
        }

        return target;
    }

    /// <summary>Zet source op target. Geeft het pad van de backup van het oude bestand terug (of null als er geen was).</summary>
    private static string? Replace(string source, string target, bool moveAside)
    {
        if (moveAside)
        {
            var aside = target + BackupSuffix;
            string? kept = null;
            if (File.Exists(target))
            {
                TryDelete(aside);
                File.Move(target, aside);
                kept = aside;
            }

            File.Move(source, target);
            return kept;
        }

        var temp = target + PendingSuffix + ".tmp";
        File.Copy(source, temp, overwrite: true);
        string? backup = null;
        if (File.Exists(target))
        {
            backup = target + BackupSuffix;
            File.Copy(target, backup, overwrite: true);
        }

        File.Move(temp, target, overwrite: true);
        return backup;
    }

    private static void Rollback(List<(string Target, string? Backup)> applied)
    {
        for (var i = applied.Count - 1; i >= 0; i--)
        {
            var (target, backup) = applied[i];
            try
            {
                if (backup is not null)
                {
                    File.Move(backup, target, overwrite: true);
                }
                else
                {
                    File.Delete(target);
                }
            }
            catch (IOException)
            {
                // Best effort; de volgende start probeert opnieuw bij te werken.
            }
        }
    }

    private void RemoveStale(IReadOnlyList<string> newFiles)
    {
        var manifest = Path.Combine(_root, ManifestFileName);
        if (!File.Exists(manifest))
        {
            return;
        }

        var keep = new HashSet<string>(newFiles, StringComparer.OrdinalIgnoreCase);
        foreach (var old in File.ReadAllLines(manifest))
        {
            if (string.IsNullOrWhiteSpace(old) || keep.Contains(old) || IsLauncher(old))
            {
                continue;
            }

            try
            {
                TryDelete(ResolveTarget(old));
            }
            catch (InvalidDataException)
            {
                // Een rommelig manifest verwijdert nooit iets buiten de installatiemap.
            }
        }
    }

    private static void TryDelete(string? path)
    {
        if (path is null)
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Geen ramp: opgeruimd bij de volgende start.
        }
        catch (UnauthorizedAccessException)
        {
            // Idem.
        }
    }
}
