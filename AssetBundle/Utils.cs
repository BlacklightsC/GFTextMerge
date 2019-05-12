using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;

namespace GFTextMerge.AssetBundles
{
    public static class Utils
    {
        public static readonly byte[] empty = new byte[52]
        {
            0x05, 0x00, 0x00, 0x00, 0x45, 0x6D, 0x70, 0x74,
            0x79, 0x00, 0x00, 0x00, 0x1E, 0x00, 0x00, 0x00,
            0x45, 0x6D, 0x70, 0x74, 0x79, 0x45, 0x6D, 0x70,
            0x74, 0x79, 0x45, 0x6D, 0x70, 0x74, 0x79, 0x45,
            0x6D, 0x70, 0x74, 0x79, 0x45, 0x6D, 0x70, 0x74,
            0x79, 0x45, 0x6D, 0x70, 0x74, 0x79, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00
        };

        public static short readreversedInt16(byte[] b)
        {
            Array.Reverse(b, 0, b.Length);
            return BitConverter.ToInt16(b, 0);
        }

        public static int readreversedInt32(byte[] b)
        {
            Array.Reverse(b, 0, b.Length);
            return BitConverter.ToInt32(b, 0);
        }

        public static long readreversedInt64(byte[] b)
        {
            Array.Reverse(b, 0, b.Length);
            return BitConverter.ToInt64(b, 0);
        }

        public static byte[] reversedInt16bytes(short i)
        {
            byte[] bytes = BitConverter.GetBytes(i);
            Array.Reverse(bytes, 0, bytes.Length);
            return bytes;
        }

        public static byte[] string2bytes(string str)
        {
            return Encoding.ASCII.GetBytes(str + "\0");
        }

        public static byte[] reversedInt32bytes(int i)
        {
            byte[] bytes = BitConverter.GetBytes(i);
            Array.Reverse(bytes, 0, bytes.Length);
            return bytes;
        }

        public static byte[] reversedInt64bytes(long i)
        {
            byte[] bytes = BitConverter.GetBytes(i);
            Array.Reverse(bytes, 0, bytes.Length);
            return bytes;
        }

        public static void readFile(byte[] bytes, FileStream fs)
        {
            int length = bytes.Length;
            int offset = 0;
            while (length > 0)
            {
                int num = fs.Read(bytes, offset, length);
                offset += num;
                length -= num;
            }
        }

        public static string ReadStringToNull(BinaryReader reader)
        {
            string buffer = string.Empty;
            byte num;
            while ((num = reader.ReadByte()) > 0)
                buffer += (char)num;
            return buffer;
        }

        public static int padding(int i, int l)
        {
            int num = i % l;
            if (num > 0)
                return l - num;
            return 0;
        }

        public static void WriteStreamWithPadding(BinaryWriter memWriter, byte[] b, int l)
        {
            int num = b.Length % l;
            memWriter.Write(b);
            if (num <= 0)
                return;
            memWriter.Write(new byte[l - num]);
        }

        private static byte[] StreamToByteArray(Stream ms)
        {
            byte[] buffer = new byte[ms.Length];
            ms.Seek(0, SeekOrigin.Begin);
            ms.Read(buffer, 0, buffer.Length);
            return buffer;
        }

        private static byte[] GetBytes(string str)
        {
            byte[] numArray = new byte[str.Length * 2];
            Buffer.BlockCopy(str.ToCharArray(), 0, numArray, 0, numArray.Length);
            return numArray;
        }

        private static string GetString(byte[] bytes)
        {
            char[] chArray = new char[bytes.Length / 2];
            Buffer.BlockCopy(bytes, 0, chArray, 0, bytes.Length);
            return new string(chArray);
        }

        public static string GetMd5Hash(string input)
        {
            byte[] hash = MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(input));
            StringBuilder stringBuilder = new StringBuilder();
            for (int index = 0; index < hash.Length; ++index)
                stringBuilder.Append(hash[index].ToString("x2"));
            return stringBuilder.ToString();
        }
    }
}
