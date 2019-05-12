using System;
using System.IO;

using SevenZip;
using SevenZip.Compression.LZMA;

namespace GFTextMerge.AssetBundles
{
    public static class Helper
    {
        private static int dictionary = 0x80000;
        private static int posStateBits = 2;
        private static int litContextBits = 3;
        private static int litPosBits = 0;
        private static int algorithm = 2;
        private static int numFastBytes = 32;
        private static bool eos = false;
        private static CoderPropID[] propIDs = new CoderPropID[]
        {
            CoderPropID.DictionarySize,
            CoderPropID.PosStateBits,
            CoderPropID.LitContextBits,
            CoderPropID.LitPosBits,
            CoderPropID.Algorithm,
            CoderPropID.NumFastBytes,
            CoderPropID.MatchFinder,
            CoderPropID.EndMarker
        };
        private static object[] properties = new object[]
        {
            dictionary,
            posStateBits,
            litContextBits,
            litPosBits,
            algorithm,
            numFastBytes,
            "bt4",
            eos
        };

        public static void Compress(Stream inStream, Stream outStream)
        {
            Encoder encoder = new Encoder();
            encoder.SetCoderProperties(propIDs, properties);
            encoder.WriteCoderProperties(outStream);
            //BinaryWriter binaryWriter = new BinaryWriter(outStream, System.Text.Encoding.UTF8);
            encoder.Code(inStream, outStream, -1, -1, null);
        }

        public static void Decompress(Stream inStream, Stream outStream)
        {
            byte[] numArray = new byte[5];
            if (inStream.Read(numArray, 0, 5) != 5)
                throw new Exception("Input stream is too short.");
            Decoder decoder = new Decoder();
            decoder.SetDecoderProperties(numArray);
            using (BinaryReader binaryReader = new BinaryReader(inStream, System.Text.Encoding.UTF8))
            {
                long outSize = binaryReader.ReadInt64();
                long inSize = binaryReader.ReadInt64();
                decoder.Code(inStream, outStream, inSize, outSize, null);
            }
        }

        public static void Decompress(Stream inStream, Stream outStream, int compressedSize, int decompressedSize)
        {
            byte[] numArray = new byte[5];
            if (inStream.Read(numArray, 0, 5) != 5)
                throw new Exception("Input stream is too short.");
            Decoder decoder = new Decoder();
            decoder.SetDecoderProperties(numArray);
            //BinaryReader binaryReader = new BinaryReader(inStream, System.Text.Encoding.UTF8);
            decoder.Code(inStream, outStream, compressedSize, decompressedSize, null);
        }
    }
}
