using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace CuboidFever;

// One file per saved column: <world folder>/columns/c.<cx>.<cz>.cfc
// Only columns the player has changed are ever written, so even a long-lived world has a modest file count.
public sealed class FileColumnStore : IColumnStore
{
    private const string Prefix = "c.";
    private const string Extension = ".cfc";

    private readonly string _directory;

    public FileColumnStore(string worldDirectory)
    {
        _directory = Path.Combine(worldDirectory, "columns");
    }

    private string PathFor(int cx, int cz) =>
        Path.Combine(_directory, string.Create(CultureInfo.InvariantCulture, $"{Prefix}{cx}.{cz}{Extension}"));

    public byte[] Read(int cx, int cz)
    {
        try
        {
            // FileShare.Delete lets a save replace the file while this read is still open (on Windows a
            // replace fails otherwise), so a save never has to wait for a load or the other way round.
            using var stream = new FileStream(
                PathFor(cx, cz), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

            var data = new byte[stream.Length];
            stream.ReadExactly(data);
            return data;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    public void Write(int cx, int cz, byte[] data) =>
        AtomicFile.WriteAllBytes(PathFor(cx, cz), data);

    public void Delete(int cx, int cz) => File.Delete(PathFor(cx, cz));

    public bool Exists(int cx, int cz) => File.Exists(PathFor(cx, cz));

    public IEnumerable<(int cx, int cz)> ListColumns()
    {
        if (!Directory.Exists(_directory))
            yield break;

        foreach (string file in Directory.EnumerateFiles(_directory, Prefix + "*" + Extension))
        {
            // "c.-3.7.cfc" -> "-3.7" -> (-3, 7). Directory patterns can match more loosely than they look
            // (on Windows "*.cfc" also finds "x.cfc.tmp", a leftover from an interrupted save), so check the
            // ending ourselves, and ignore anything that does not parse.
            string name = Path.GetFileName(file);
            if (!name.EndsWith(Extension, StringComparison.Ordinal)) continue;

            string middle = name.Substring(Prefix.Length, name.Length - Prefix.Length - Extension.Length);
            string[] parts = middle.Split('.');

            if (parts.Length == 2 &&
                int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int cx) &&
                int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int cz))
            {
                yield return (cx, cz);
            }
        }
    }
}
