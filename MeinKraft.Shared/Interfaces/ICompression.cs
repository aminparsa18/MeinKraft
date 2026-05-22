public interface ICompression
{
    byte[] Compress(ReadOnlySpan<byte> data);
    byte[] Decompress(byte[] data);
}
