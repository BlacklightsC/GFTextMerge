using System.IO;

using LZ4;

namespace GFTextMerge.AssetBundles
{
    public class ArchiveBlockInfo
    {
        public int uncompressedSize;
        public int compressedSize;
        public short flags;

        public ArchiveBlockInfo(int usize, int csize, short flags)
        {
            uncompressedSize = usize;
            compressedSize = csize;
            this.flags = flags;
        }

        public bool IsCompressed() => GetCompressionType() != CompressionType.NONE;

        public CompressionType GetCompressionType() => (CompressionType)(flags & 63);

        public byte[] Decompress(BinaryReader buf)
        {
            byte[] input = buf.ReadBytes(compressedSize);
            if (!IsCompressed()) return input;
            CompressionType compressionType = GetCompressionType();
            if (compressionType == CompressionType.LZ4 || compressionType == CompressionType.LZ4HC)
                return LZ4Codec.Decode(input, 0, input.Length, uncompressedSize);
            return null;
        }
    }
}