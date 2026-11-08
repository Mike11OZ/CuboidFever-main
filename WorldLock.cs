using System;
using System.IO;

namespace CuboidFever;

// Thrown when a world is already open somewhere else, so a second copy of the game cannot open it too.
public sealed class WorldInUseException : IOException
{
    public WorldInUseException(string message, Exception inner = null) : base(message, inner) { }
}

// Stops two copies of the game from having the same world open at once, which would have them overwrite each
// other's saved columns.
//
// The lock is a file in the world folder that the game keeps open with sharing turned off: while one process
// holds it, any other attempt to open it fails. The operating system lets go of an open file when its process
// ends, even by a crash or the task manager, so a lock can never be left behind and need cleaning up by hand.
// The file itself stays in the folder (deleting it would open a gap where a third copy could slip in).
public sealed class WorldLock : IDisposable
{
    public const string FileName = "session.lock";

    private FileStream _stream;

    private WorldLock(FileStream stream) => _stream = stream;

    public static string PathIn(string worldFolder) => Path.Combine(worldFolder, FileName);

    // Takes the lock on a world folder, or throws WorldInUseException if something else already holds it.
    public static WorldLock Acquire(string worldFolder)
    {
        try
        {
            var stream = new FileStream(PathIn(worldFolder), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            return new WorldLock(stream);
        }
        catch (IOException e)
        {
            throw new WorldInUseException(
                "This world is already open in another copy of the game (or its files are locked by another program).", e);
        }
    }

    // Lets go of the lock. Safe to call more than once.
    public void Dispose()
    {
        _stream?.Dispose();
        _stream = null;
    }
}
