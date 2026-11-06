using System.IO;
using System.Text;

namespace CuboidFever;

// Writes a file so that a crash or power cut part-way through can never leave a half-written file behind.
// The data goes to a temporary file next to the target first, is flushed to disk, and then replaces the
// target in one step. Readers see either the complete old file or the complete new one.
// Every file the save system writes goes through here.
public static class AtomicFile
{
    public static void WriteAllBytes(string path, byte[] data)
    {
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        string temp = path + ".tmp";

        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.Write(data, 0, data.Length);
            stream.Flush(true); // flush to the physical disk, not just the operating system's cache
        }

        File.Move(temp, path, overwrite: true);
    }

    public static void WriteAllText(string path, string text) =>
        WriteAllBytes(path, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(text));
}
