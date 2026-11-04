using System.IO.Compression;

namespace CitiesLauncher.Core;

public static class SafeZip
{
    /// <summary>
    /// Pakt de zip uit in een lege doelmap en weigert paden die buiten de map terechtkomen (zip-slip).
    /// Geeft de uitgepakte bestanden terug als relatieve paden met '/' als scheidingsteken.
    /// </summary>
    public static IReadOnlyList<string> Extract(string zipPath, string destination)
    {
        var root = Path.GetFullPath(destination);
        Directory.CreateDirectory(root);
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        var files = new List<string>();

        using var archive = ZipFile.OpenRead(zipPath);
        foreach (var entry in archive.Entries)
        {
            var target = Path.GetFullPath(Path.Combine(root, entry.FullName));
            if (!target.StartsWith(rootWithSeparator, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Onveilig pad in zip: {entry.FullName}");
            }

            if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
            {
                Directory.CreateDirectory(target);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: true);
            files.Add(Path.GetRelativePath(root, target).Replace('\\', '/'));
        }

        return files;
    }
}
