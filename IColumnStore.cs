using System.Collections.Generic;

namespace CuboidFever;

// Where a world's saved columns live. It deals only in the bytes ColumnSerializer produces, so it knows nothing
// about blocks or palettes, and the storage layout (one file per column today, region files later) can change
// without touching the rest of the save system.
//
// Reads happen on world worker threads and writes on a background writer, so implementations must allow a read
// of a column while it is being rewritten: the reader gets either the complete old data or the complete new data.
// Two writes to the same column at once are not supported; the writer queue handles each column one at a time.
public interface IColumnStore
{
    // The saved bytes for a column, or null if it has never been saved.
    byte[] Read(int cx, int cz);

    // Saves a column, replacing any earlier version.
    void Write(int cx, int cz, byte[] data);

    // Removes a column's saved data (it will regenerate from the seed). Does nothing if there is none.
    void Delete(int cx, int cz);

    bool Exists(int cx, int cz);

    // Every column that has saved data.
    IEnumerable<(int cx, int cz)> ListColumns();
}
