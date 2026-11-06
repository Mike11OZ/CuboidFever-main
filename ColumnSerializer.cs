using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace CuboidFever;

// Turns one Column's blocks into the bytes of a column file, and back.
//
// File layout (little-endian):
//   4 bytes  magic "CFCL"
//   1 byte   format version
//   4 bytes  column X          } checked against the column the caller expected, so a file that was
//   4 bytes  column Z          } renamed or copied to the wrong place is caught instead of loaded
//   4 bytes  length of the uncompressed payload
//   4 bytes  checksum of the uncompressed payload (FNV-1a)
//   rest     the payload, deflate-compressed
//
// The payload is the column's chunks from bottom to top, each written by Chunk.WriteBlocks.
//
// Only blocks are stored. Light is recomputed after loading (Column.ComputeSkyLight, then LightMap.Stitch).
// Block IDs go through a translation table (see BlockPalette) so a save never depends on this build's IDs.
public static class ColumnSerializer
{
    public const byte FormatVersion = 1;

    private static readonly byte[] Magic = { (byte)'C', (byte)'F', (byte)'C', (byte)'L' };
    private const int HeaderSize = 4 + 1 + 4 + 4 + 4 + 4;

    // Smallest and largest possible payloads: every chunk fully uniform (2 bytes), and every chunk with 16 full layers
    private const int MinPayload = World.ChunksY * 2;
    private const int MaxPayload = World.ChunksY * (1 + Chunk.Size * (1 + Chunk.Size * Chunk.Size));

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    // Serializes the column's blocks. 'toSaved' translates runtime block IDs to saved IDs (BlockPalette.ToSavedTable).
    // Reads the column's blocks as they are right now, so call it on the main thread, or when nothing is editing
    // the column; the compression inside is the slow part and is the same either way.
    public static byte[] Write(Column column, ReadOnlySpan<byte> toSaved)
    {
        if (toSaved.Length != 256)
            throw new ArgumentException("The block ID table must have 256 entries.", nameof(toSaved));

        byte[] payload;
        using (var payloadStream = new MemoryStream())
        {
            using (var writer = new BinaryWriter(payloadStream, Utf8, leaveOpen: true))
            {
                foreach (var chunk in column.Chunks)
                    chunk.WriteBlocks(writer, toSaved);
            }
            payload = payloadStream.ToArray();
        }

        using var output = new MemoryStream(HeaderSize + payload.Length / 4 + 64);

        using (var header = new BinaryWriter(output, Utf8, leaveOpen: true))
        {
            header.Write(Magic);
            header.Write(FormatVersion);
            header.Write(column.CX);
            header.Write(column.CZ);
            header.Write(payload.Length);
            header.Write(Checksum(payload));
        }

        using (var deflate = new DeflateStream(output, CompressionLevel.Fastest, leaveOpen: true))
            deflate.Write(payload, 0, payload.Length);

        return output.ToArray();
    }

    // Rebuilds a column from the bytes of a column file. 'toRuntime' translates saved block IDs to this build's
    // IDs (BlockPalette.ToRuntimeTable). The column is returned with its blocks only: the caller still has to
    // call ComputeSkyLight on it. Throws InvalidDataException if the data is damaged, truncated, belongs to a
    // different column, or was written by a newer version of the game.
    public static Column Read(byte[] data, ReadOnlySpan<byte> toRuntime, int expectedCX, int expectedCZ)
    {
        if (toRuntime.Length != 256)
            throw new ArgumentException("The block ID table must have 256 entries.", nameof(toRuntime));

        if (data == null || data.Length < HeaderSize)
            throw new InvalidDataException("Column file is truncated.");

        byte[] payload;
        using (var input = new MemoryStream(data))
        {
            int cx, cz, payloadLength;
            uint checksum;

            using (var header = new BinaryReader(input, Utf8, leaveOpen: true))
            {
                if (!header.ReadBytes(Magic.Length).AsSpan().SequenceEqual(Magic))
                    throw new InvalidDataException("Not a column file (bad magic number).");

                byte version = header.ReadByte();
                if (version > FormatVersion)
                    throw new InvalidDataException(
                        $"Column file was saved by a newer version of the game (format {version}, " +
                        $"this build reads up to {FormatVersion}).");
                if (version == 0)
                    throw new InvalidDataException("Column file has an invalid format version.");

                cx = header.ReadInt32();
                cz = header.ReadInt32();
                payloadLength = header.ReadInt32();
                checksum = header.ReadUInt32();
            }

            if (cx != expectedCX || cz != expectedCZ)
                throw new InvalidDataException(
                    $"Column file is for column ({cx}, {cz}) but ({expectedCX}, {expectedCZ}) was expected.");

            if (payloadLength < MinPayload || payloadLength > MaxPayload)
                throw new InvalidDataException($"Column file has an impossible payload size ({payloadLength}).");

            payload = new byte[payloadLength];

            try
            {
                using var deflate = new DeflateStream(input, CompressionMode.Decompress, leaveOpen: true);
                deflate.ReadExactly(payload);
            }
            catch (EndOfStreamException)
            {
                throw new InvalidDataException("Column file is truncated.");
            }

            if (Checksum(payload) != checksum)
                throw new InvalidDataException("Column file failed its checksum (the data is damaged).");
        }

        var column = new Column(expectedCX, expectedCZ);

        try
        {
            using var payloadStream = new MemoryStream(payload);
            using var reader = new BinaryReader(payloadStream, Utf8);

            for (int cy = 0; cy < World.ChunksY; cy++)
            {
                var chunk = new Chunk(expectedCX, cy, expectedCZ);
                chunk.ReadBlocks(reader, toRuntime);
                column.Chunks[cy] = chunk;
            }

            if (payloadStream.Position != payload.Length)
                throw new InvalidDataException("Column file has unexpected extra data.");
        }
        catch (EndOfStreamException)
        {
            throw new InvalidDataException("Column file is truncated.");
        }

        return column;
    }

    // FNV-1a: not cryptographic, just enough to notice a damaged file
    private static uint Checksum(byte[] data)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (byte b in data)
            {
                hash ^= b;
                hash *= 16777619;
            }
            return hash;
        }
    }
}
