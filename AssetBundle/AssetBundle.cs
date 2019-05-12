using System;
using System.IO;
using System.Xml;
using System.Linq;
using System.Collections.Generic;

using LZ4;

namespace GFTextMerge.AssetBundles
{
    public class AssetBundle
    {
        public static readonly string SIGNATURE_RAW = "UnityRaw";
        public static readonly string SIGNATURE_WEB = "UnityWeb";
        public static readonly string SIGNATURE_FS = "UnityFS";
        public CompressionType compression = CompressionType.LZMA;
        private List<ArchiveBlockInfo> blocks = new List<ArchiveBlockInfo>();
        private List<AssetProp> nodes = new List<AssetProp>();
        private List<string> names = new List<string>();
        private List<int> statuses = new List<int>();
        private int minOffset = int.MaxValue;
        public static AssetBundle instance;
        private string signature;
        private int formatVersion;
        private string unityVersion;
        private string generatorVersion;
        private long fileSize;
        private uint ciblockSize;
        private uint uiblockSize;
        private uint flags;
        private byte[] guid;
        private long binaryDataOffset;
        private BinaryReader reader;
        private byte[] bytes;
        private string name;
        private string path;
        private long originalFileSize;

        public void Load(BinaryReader file, string name)
        {
            instance = this;
            signature = Utils.ReadStringToNull(file);
            formatVersion = Utils.readreversedInt32(file.ReadBytes(4));
            unityVersion = Utils.ReadStringToNull(file);
            generatorVersion = Utils.ReadStringToNull(file);
            reader = file;
            this.name = name;
            path = $"{name}_dump/";
            originalFileSize = file.BaseStream.Length;
            if (IsUnityFS) LoadUnityFS(file);
            else
            {
                if (!IsUnityWeb)
                    throw new Exception($"Not supported bundle type: {signature}");
                LoadUnityFS(file);
            }
        }

        private void LoadUnityFS(BinaryReader buf)
        {
            fileSize = Utils.readreversedInt64(buf.ReadBytes(8));
            ciblockSize = (uint)Utils.readreversedInt32(buf.ReadBytes(4));
            uiblockSize = (uint)Utils.readreversedInt32(buf.ReadBytes(4));
            flags = (uint)Utils.readreversedInt32(buf.ReadBytes(4));
            CompressionType compression = (CompressionType)((int)flags & 0x3F);
            Console.WriteLine($"file_size:{fileSize}");
            Console.WriteLine($"ciblock_size:{ciblockSize}");
            Console.WriteLine($"uiblock_size:{uiblockSize}");
            Console.WriteLine($"flags:{flags}");
            Console.WriteLine($"compression:{compression}");
            uint num1 = flags & 0x80;
            if (num1 > 0)
            {
                binaryDataOffset = buf.BaseStream.Position;
                buf.BaseStream.Seek(-ciblockSize, SeekOrigin.End);
            }
            if (IsUnityWeb)
            {
                int count = Utils.padding((int)buf.BaseStream.Position, 4);
                if (count > 0)
                    buf.ReadBytes(count);
            }
            byte[] buffer = readCompressedData(buf, compression);
            if (num1 > 0) buf.BaseStream.Seek(binaryDataOffset, SeekOrigin.Begin);
            binaryDataOffset = buf.BaseStream.Position;
            Console.WriteLine($"binary_data_offset:{binaryDataOffset}");
            using (MemoryStream input = new MemoryStream(buffer))
            using (BinaryReader reader = new BinaryReader(input))
            {
                guid = reader.ReadBytes(0x10);
                int num2 = Utils.readreversedInt32(reader.ReadBytes(4));
                Console.WriteLine($"num_blocks:{num2}");
                for (int index = 0; index < num2; ++index)
                    blocks.Add(new ArchiveBlockInfo(Utils.readreversedInt32(reader.ReadBytes(4)), Utils.readreversedInt32(reader.ReadBytes(4)), Utils.readreversedInt16(reader.ReadBytes(2))));
                Console.WriteLine("***********************************************");
                int num3 = Utils.readreversedInt32(reader.ReadBytes(4));
                Console.WriteLine($"num_nodes:{num3}");
                for (int index = 0; index < num3; ++index)
                {
                    long ofs = Utils.readreversedInt64(reader.ReadBytes(8));
                    long size = Utils.readreversedInt64(reader.ReadBytes(8));
                    int status = Utils.readreversedInt32(reader.ReadBytes(4));
                    string name = Utils.ReadStringToNull(reader);
                    names.Add(name);
                    statuses.Add(status);
                    nodes.Add(new AssetProp(ofs, size, status, name));
                    if (minOffset > ofs) minOffset = (int)ofs;
                }
                Console.WriteLine("***********************************************");
                foreach (AssetProp node in nodes)
                {
                    Console.WriteLine($"ofs:{node.ofs}");
                    Console.WriteLine($"size:{node.size}");
                    Console.WriteLine($"status:{node.status}");
                    Console.WriteLine($"name:{node.name}");
                }
                Console.WriteLine("***********************************************");
            }
        }

        public void LoadFromXml(string name)
        {
            path = $"{Path.GetDirectoryName(name)}\\{this.name = Path.GetFileNameWithoutExtension(name)}_dump/";
            using (XmlReader reader = XmlReader.Create(name))
            {
                reader.ReadToDescendant("signature");
                signature = reader.ReadElementContentAsString();
                reader.ReadToFollowing("format_version");
                formatVersion = reader.ReadElementContentAsInt();
                reader.ReadToFollowing("unity_version");
                unityVersion = reader.ReadElementContentAsString();
                reader.ReadToFollowing("generator_version");
                generatorVersion = reader.ReadElementContentAsString();
                reader.ReadToFollowing("guid");
                guid = new byte[0x10];
                reader.ReadElementContentAsBinHex(guid, 0, 0x10);
                reader.ReadToFollowing("flags");
                flags = (uint)reader.ReadElementContentAsInt();
                reader.ReadToFollowing("files");
                names = ((IEnumerable<string>)reader.ReadElementContentAsString().Split(',')).ToList();
                reader.ReadToFollowing("statuses");
                statuses = new List<int>(Array.ConvertAll(reader.ReadElementContentAsString().Split(','), new Converter<string, int>(int.Parse)));
                reader.ReadToFollowing("original_size");
                originalFileSize = reader.ReadElementContentAsInt();
            }
            for (int index = 0; index < names.Count; ++index)
                nodes.Add(new AssetProp(0, 0, statuses[index], names[index]));
            flags &= 0xFFFFFF7F;
        }

        public void Create()
        {
            packDataToFile(compression);
            using (FileStream stream = File.Open($"{path}\\..\\{name}", FileMode.Create))
            {
                stream.SetLength(0);
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(Utils.string2bytes(signature));
                    writer.Write(Utils.reversedInt32bytes(formatVersion));
                    writer.Write(Utils.string2bytes(unityVersion));
                    writer.Write(Utils.string2bytes(generatorVersion));
                    byte[] array;
                    using (MemoryStream innerStream = new MemoryStream())
                    {
                        using (BinaryWriter innerWriter = new BinaryWriter(innerStream))
                        {
                            innerWriter.Write(guid);
                            innerWriter.Write(Utils.reversedInt32bytes(blocks.Count));
                            foreach (ArchiveBlockInfo block in blocks)
                            {
                                innerWriter.Write(Utils.reversedInt32bytes(block.uncompressedSize));
                                innerWriter.Write(Utils.reversedInt32bytes(block.compressedSize));
                                innerWriter.Write(Utils.reversedInt16bytes(block.flags));
                            }
                            innerWriter.Write(Utils.reversedInt32bytes(nodes.Count));
                            foreach (AssetProp node in nodes)
                            {
                                innerWriter.Write(Utils.reversedInt64bytes(node.ofs));
                                innerWriter.Write(Utils.reversedInt64bytes(node.size));
                                innerWriter.Write(Utils.reversedInt32bytes(node.status));
                                innerWriter.Write(Utils.string2bytes(node.name));
                            }
                        }
                        array = innerStream.ToArray();
                    }
                    byte[] buffer = LZ4Codec.Encode(array, 0, array.Length);
                    using (FileStream innerStream = File.Open($"{path}\\..\\{name}.comp", FileMode.Open))
                    using (BinaryReader reader = new BinaryReader(innerStream))
                    {
                        writer.Write(Utils.reversedInt64bytes(writer.BaseStream.Length + innerStream.Length + buffer.Length + 20));
                        writer.Write(Utils.reversedInt32bytes(buffer.Length));
                        writer.Write(Utils.reversedInt32bytes(array.Length));
                        writer.Write(Utils.reversedInt32bytes((int)flags | 3));
                        if (IsUnityWeb)
                        {
                            int length = Utils.padding((int)writer.BaseStream.Position, 4);
                            if (length > 0) writer.Write(new byte[length]);
                        }
                        writer.Write(buffer);
                        while (reader.BaseStream.Position != reader.BaseStream.Length)
                            writer.Write(reader.ReadBytes(0x20000));
                    }
                    if (stream.Length < originalFileSize)
                        stream.SetLength(originalFileSize);
                }
            }
            File.Delete($"{path}\\..\\{name}.comp");
        }

        private void packDataToFile(CompressionType compression)
        {
            bool flag = false;
            mergeDataToFile();
            using (FileStream fileStream1 = File.Open($"{path}\\..\\{name}.comp", FileMode.Create))
            using (BinaryWriter binaryWriter = new BinaryWriter(fileStream1))
            using (FileStream fileStream2 = File.Open($"{path}\\..\\{name}.unc", FileMode.Open))
            using (BinaryReader binaryReader = new BinaryReader(fileStream2))
                switch (compression)
                {
                    case CompressionType.NONE:
                        flag = true;
                        break;
                    case CompressionType.LZMA:
                        blocks.Clear();
                        Helper.Compress(fileStream2, binaryWriter.BaseStream);
                        blocks.Add(new ArchiveBlockInfo((int)fileStream2.Length, (int)binaryWriter.BaseStream.Length, 0x41));
                        break;

                    case CompressionType.LZ4:
                    case CompressionType.LZ4HC:
                    {
                        blocks.Clear();
                        byte[] input = binaryReader.ReadBytes(0x20000);
                        while (input.Length != 0)
                        {
                            byte[] buffer = compression == CompressionType.LZ4 ? LZ4Codec.Encode(input, 0, input.Length)
                                                                               : LZ4Codec.EncodeHC(input, 0, input.Length);
                            blocks.Add(new ArchiveBlockInfo(input.Length, buffer.Length, 3));
                            input = binaryReader.ReadBytes(0x20000);
                            binaryWriter.Write(buffer);
                        }
                        break;
                    }
                }
            if (flag)
            {
                File.Delete($"{path}\\..\\{name}.comp");
                File.Move($"{path}\\..\\{name}.unc", $"{path}\\..\\{name}.comp");
            }
            else File.Delete($"{path}\\..\\{name}.unc");
        }

        private void packData(CompressionType compression)
        {
            mergeData();
            using (MemoryStream memoryBuffer = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(memoryBuffer))
            using (MemoryStream source = new MemoryStream(bytes))
            using (BinaryReader reader = new BinaryReader(source))
                switch (compression)
                {
                    case CompressionType.NONE:
                        bytes = source.ToArray();
                        break;
                    case CompressionType.LZMA:
                        blocks.Clear();
                        Helper.Compress(source, memoryBuffer);
                        blocks.Add(new ArchiveBlockInfo((int)source.Length, (int)memoryBuffer.Length, 0x41));
                        bytes = memoryBuffer.ToArray();
                        break;
                    case CompressionType.LZ4:
                    case CompressionType.LZ4HC:
                        blocks.Clear();
                        byte[] input = reader.ReadBytes(0x20000);
                        while (input.Length != 0)
                        {
                            byte[] buffer = LZ4Codec.Encode(input, 0, input.Length);
                            blocks.Add(new ArchiveBlockInfo(input.Length, buffer.Length, 3));
                            input = reader.ReadBytes(0x20000);
                            writer.Write(buffer);
                        }
                        bytes = memoryBuffer.ToArray();
                        break;
                }
        }

        private void mergeDataToFile()
        {
            long _ofs = 0;
            List<AssetProp> assetPropList = new List<AssetProp>();
            blocks.Clear();
            using (FileStream fileStream1 = File.Open($"{path}\\..\\{name}.unc", FileMode.Create))
            using (BinaryWriter binaryWriter = new BinaryWriter(fileStream1))
                foreach (AssetProp node in nodes)
                    using (FileStream fileStream2 = File.Open(path + node.name, FileMode.Open))
                    using (BinaryReader binaryReader = new BinaryReader(fileStream2))
                    {
                        blocks.Add(new ArchiveBlockInfo((int)fileStream2.Length, (int)fileStream2.Length, 0x40));
                        assetPropList.Add(new AssetProp(_ofs, fileStream2.Length, node.status, node.name));
                        _ofs += fileStream2.Length;
                        if (fileStream2.Length > 0x1900000)
                        {
                            for (byte[] buffer = binaryReader.ReadBytes(0xA00000); buffer.Length > 0; buffer = binaryReader.ReadBytes(0xA00000))
                                binaryWriter.Write(buffer);
                        }
                        else
                        {
                            byte[] buffer = binaryReader.ReadBytes((int)fileStream2.Length);
                            binaryWriter.Write(buffer);
                        }
                    }
            nodes = assetPropList;
        }

        private void mergeData()
        {
            long _ofs = 0;
            List<AssetProp> assetPropList = new List<AssetProp>();
            blocks.Clear();
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream))
                    foreach (AssetProp node in nodes)
                        using (FileStream fileStream = File.Open(path + node.name, FileMode.Open))
                        using (BinaryReader binaryReader = new BinaryReader(fileStream))
                        {
                            blocks.Add(new ArchiveBlockInfo((int)fileStream.Length, (int)fileStream.Length, 0x40));
                            assetPropList.Add(new AssetProp(_ofs, fileStream.Length, node.status, node.name));
                            _ofs += fileStream.Length;
                            if (fileStream.Length > 0x1900000)
                            {
                                for (byte[] buffer = binaryReader.ReadBytes(0xA00000); buffer.Length > 0; buffer = binaryReader.ReadBytes(0xA00000))
                                    binaryWriter.Write(buffer);
                            }
                            else
                            {
                                byte[] buffer = binaryReader.ReadBytes((int)fileStream.Length);
                                binaryWriter.Write(buffer);
                            }
                        }
                bytes = memoryStream.ToArray();
            }
            nodes = assetPropList;
        }

        public void dump()
        {
            unpackDataToFile();
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            using (FileStream fileStream1 = File.Open($"{name}.unc", FileMode.Open))
            using (BinaryReader binaryReader = new BinaryReader(fileStream1))
                foreach (AssetProp node in nodes)
                {
                    string directoryName = Path.GetDirectoryName(path + node.name);
                    if (!Directory.Exists(directoryName)) Directory.CreateDirectory(directoryName);
                    using (FileStream fileStream2 = File.Open(path + node.name, FileMode.OpenOrCreate))
                    {
                        fileStream2.SetLength(0);
                        using (BinaryWriter binaryWriter = new BinaryWriter(fileStream2))
                        {
                            binaryReader.BaseStream.Position = node.ofs - minOffset;
                            if (node.size > 0x1900000)
                            {
                                long num = 0;
                                while (num < node.size)
                                {
                                    if (node.size - num < 0xA00000)
                                    {
                                        byte[] buffer = binaryReader.ReadBytes((int)(node.size - num));
                                        binaryWriter.Write(buffer);
                                        num += node.size - num;
                                    }
                                    else
                                    {
                                        byte[] buffer = binaryReader.ReadBytes(0xA00000);
                                        binaryWriter.Write(buffer);
                                        num += 0xA00000;
                                    }
                                }
                            }
                            else
                            {
                                byte[] buffer = binaryReader.ReadBytes((int)node.size);
                                binaryWriter.Write(buffer);
                            }
                        }
                    }
                }
            File.Delete($"{name}.unc");
            using (XmlWriter xmlWriter = XmlWriter.Create($"{name}.xml", new XmlWriterSettings{ Indent = true }))
            {
                xmlWriter.WriteStartDocument();
                xmlWriter.WriteStartElement(nameof(AssetBundle));
                xmlWriter.WriteElementString("signature", signature);
                xmlWriter.WriteElementString("format_version", formatVersion.ToString());
                xmlWriter.WriteElementString("unity_version", unityVersion);
                xmlWriter.WriteElementString("generator_version", generatorVersion);
                xmlWriter.WriteStartElement("guid");
                xmlWriter.WriteBinHex(guid, 0, guid.Length);
                xmlWriter.WriteEndElement();
                xmlWriter.WriteElementString("flags", flags.ToString());
                xmlWriter.WriteElementString("files", string.Join(",", names.ToArray()));
                xmlWriter.WriteElementString("statuses", string.Join(",", statuses.ToArray()));
                xmlWriter.WriteElementString("original_size", originalFileSize.ToString());
                xmlWriter.WriteEndDocument();
            }
        }

        private void unpackDataToFile()
        {
            int num = 0;
            reader.BaseStream.Seek(binaryDataOffset + minOffset, SeekOrigin.Begin);
            using (FileStream fileStream = File.Open($"{name}.unc", FileMode.Create))
            using (BinaryWriter binaryWriter = new BinaryWriter(fileStream))
                foreach (ArchiveBlockInfo block in blocks)
                {
                    ++num;
                    switch (block.GetCompressionType())
                    {
                        case CompressionType.NONE:
                            Console.WriteLine($"Reader position:{reader.BaseStream.Position};");
                            Console.WriteLine($"block[{num}]:size={block.compressedSize}; unc_size={block.uncompressedSize};");
                            if (block.uncompressedSize > 0x1900000)
                            {
                                for (byte[] buffer = reader.ReadBytes(0xA00000); buffer.Length > 0; buffer = reader.ReadBytes(0xA00000))
                                    binaryWriter.Write(buffer);
                                break;
                            }
                            byte[] buffer1 = reader.ReadBytes(block.uncompressedSize);
                            binaryWriter.Write(buffer1);
                            break;
                        case CompressionType.LZMA:
                            Helper.Decompress(reader.BaseStream, fileStream, block.compressedSize, block.uncompressedSize);
                            break;
                        case CompressionType.LZ4:
                        case CompressionType.LZ4HC:
                            using (MemoryStream memoryStream = new MemoryStream(reader.ReadBytes(block.compressedSize)))
                            {
                                Console.WriteLine($"mem_in size:{memoryStream.Length};");
                                Console.WriteLine($"block[{num}]:size={block.compressedSize}; unc_size={block.uncompressedSize};");
                                byte[] buffer2 = LZ4Codec.Decode(memoryStream.ToArray(), 0, (int)memoryStream.Length, block.uncompressedSize);
                                binaryWriter.Write(buffer2);
                                break;
                            }
                        default:
                            throw new Exception($"Unknown compression type: {block.GetCompressionType()}");
                    }
                }
        }

        private void unpackData()
        {
            reader.BaseStream.Seek(binaryDataOffset + minOffset, SeekOrigin.Begin);
            using (MemoryStream memoryStream1 = new MemoryStream())
            {
                using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream1))
                    foreach (ArchiveBlockInfo block in blocks)
                    {
                        byte[] buffer;
                        switch (block.GetCompressionType())
                        {
                            case CompressionType.NONE: buffer = reader.ReadBytes(block.compressedSize); break;
                            case CompressionType.LZMA:
                                buffer = new byte[block.uncompressedSize];
                                using (MemoryStream memoryStream2 = new MemoryStream(reader.ReadBytes(block.compressedSize)))
                                using (MemoryStream memoryStream3 = new MemoryStream(buffer))
                                {
                                    Helper.Decompress(memoryStream2, memoryStream3, block.compressedSize, block.uncompressedSize);
                                    break;
                                }
                            case CompressionType.LZ4:
                            case CompressionType.LZ4HC:
                                using (MemoryStream memoryStream2 = new MemoryStream(reader.ReadBytes(block.compressedSize)))
                                {
                                    buffer = LZ4Codec.Decode(memoryStream2.ToArray(), 0, (int)memoryStream2.Length, block.uncompressedSize);
                                    break;
                                }
                            default:
                                throw new Exception($"Unknown compression type: {block.GetCompressionType()}");
                        }
                        binaryWriter.Write(buffer);
                    }
                bytes = memoryStream1.ToArray();
            }
        }

        private bool IsUnityFS => signature == SIGNATURE_FS;

        private bool IsUnityWeb => signature == SIGNATURE_WEB;

        private bool IsCompressed => CompressionType != CompressionType.NONE;

        private CompressionType CompressionType => (CompressionType)((int)flags & 0x3F);

        private byte[] readCompressedData(BinaryReader buf, CompressionType compression)
        {
            byte[] input = buf.ReadBytes((int)ciblockSize);
            switch (compression)
            {
                case CompressionType.NONE: return input;
                case CompressionType.LZ4: 
                case CompressionType.LZ4HC: return LZ4Codec.Decode(input, 0, input.Length, (int)uiblockSize);
                default: return null;
            }
        }
    }
}