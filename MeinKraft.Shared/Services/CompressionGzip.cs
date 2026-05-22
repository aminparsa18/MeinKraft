using System.IO.Compression;
/// <summary>
/// GZip-based implementation of <see cref="ICompression"/>.
///
/// Changes vs. previous version
/// ─────────────────────────────
/// 1. COMPRESSION LEVEL — defaults to <see cref="CompressionLevel.Fastest"/>.
///    The previous code used no explicit level, which defaults to
///    <see cref="CompressionLevel.Optimal"/> — the slowest mode, maximising
///    compression ratio at the expense of CPU time.  For real-time chunk
///    streaming the trade-off is wrong: chunks are written once and read
///    infrequently, so CPU time during generation matters far more than
///    saving a few hundred bytes per chunk.
///    <see cref="CompressionLevel"/> is exposed as a property so callers
///    can override it for offline batch operations where ratio matters.
///
/// 2. PRE-SIZED OUTPUT STREAMS — both Compress and Decompress now pass an
///    initial capacity to their MemoryStream, avoiding repeated internal
///    array resizes on typical chunk data:
///      Compress:   pre-sized to the input length (compressed output is always
///                  smaller than or equal to input for block data)
///      Decompress: pre-sized to input × 4 (block data compresses roughly 3–5×;
///                  × 4 avoids resizes in the common case without over-allocating)
///
/// 3. CopyTo REPLACES MANUAL BUFFER LOOP — the hand-written 4096-byte read
///    loop in Decompress is replaced with GZipStream.CopyTo(outputStream).
///    The BCL implementation buffers internally and the JIT can optimise it
///    more aggressively than user-land byte-shuffling code.
///
/// 4. FLATTENED USING DECLARATIONS — the nested using-inside-using in Decompress
///    is replaced with two top-level using declarations, removing one
///    level of indentation with no behaviour change.
/// </summary>
public class CompressionGzip : ICompression
{
    /// <summary>
    /// Controls the compression speed/ratio trade-off.
    /// Defaults to <see cref="CompressionLevel.Fastest"/> for real-time use.
    /// Set to <see cref="CompressionLevel.SmallestSize"/> for offline batch
    /// operations where storage size is the priority.
    /// </summary>
    public CompressionLevel Level { get; set; } = CompressionLevel.Fastest;

    /// <inheritdoc/>
    public byte[] Compress(ReadOnlySpan<byte> data)
    {
        // Pre-size to input length — compressed output is always ≤ input for
        // structured block data, so this avoids all internal resize steps.
        MemoryStream output = new(data.Length);

        // GZipStream must be disposed before ToArray() so the final GZIP block
        // (checksum + end-of-stream marker) is flushed into output.
        using (GZipStream gzip = new(output, Level, leaveOpen: true))
        {
            gzip.Write(data);
        }

        return output.ToArray();
    }

    /// <inheritdoc/>
    public byte[] Decompress(byte[] data)
    {
        using MemoryStream inStream = new(data, writable: false);
        using GZipStream gzip = new(inStream, CompressionMode.Decompress);

        // Pre-size to 4× compressed length — block data typically compresses
        // 3–5×, so this avoids resizes in the common case.
        MemoryStream output = new(data.Length * 4);
        gzip.CopyTo(output);
        return output.ToArray();
    }
}