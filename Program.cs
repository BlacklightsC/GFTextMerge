﻿using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Reflection;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Newtonsoft.Json;

namespace GFTextMerge
{
    using AssetBundles;
    using JsonObject;

    class Program
    {
        private static Settings Settings;
        private static DirectoryInfo TSourceDir, TDestDir, TResultDir;
        static void Main(string[] args)
        {
            string executedFileName = Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().Location);
            string executedFileNameSpace = string.Empty.PadLeft(executedFileName.Length);
            // 매개변수 부족할 경우 
            if (args.Length < 1) goto ParamException;
            switch (args[0].ToLower())
            {
                default: goto ParamException;
                case "merge":
                {
                    if (args.Length < 2)
                        Console.WriteLine($"Usage: {executedFileName} merge <Source> <Destination>");
                    else
                    {
                        Console.WriteLine("Initializing...");

                        // 기본값 설정 생성
                        if (!File.Exists("Settings.json")) File.WriteAllBytes("Settings.json", Properties.Resources.Settings);
                        if (Directory.Exists("mismatch")) Directory.Delete("mismatch", true); Thread.Sleep(200); Directory.CreateDirectory("mismatch");
                        if (Directory.Exists("result")) Directory.Delete("result", true); Thread.Sleep(200); TResultDir = Directory.CreateDirectory("result");
                        if (!Directory.Exists("overrides")) Directory.CreateDirectory("overrides");

                        Thread.Sleep(200);

                        Settings = JsonConvert.DeserializeObject<Settings>(File.ReadAllText("Settings.json"));

                        // 매개변수 받은 항목 초기화
                        TSourceDir = new DirectoryInfo(args[1]);
                        TDestDir = new DirectoryInfo(args[2]);

                        List<FileInfo> destFiles = new List<FileInfo>(TDestDir.GetFiles());
                        for (int i = destFiles.Count - 1; i >= 0; i--)
                        {
                            foreach (var content in Settings.Contents)
                                foreach (var file in content.Files)
                                    if (file == RemovePathID(destFiles[i].Name))
                                    {
                                        destFiles.RemoveAt(i);
                                        goto Continue;
                                    }
                            foreach (var locale in Settings.Locales)
                                foreach (var file in locale.Files)
                                    if (file == RemovePathID(destFiles[i].Name))
                                    {
                                        destFiles.RemoveAt(i);
                                        goto Continue;
                                    }
                                Continue:;
                        }
                        if (destFiles.Count > 0)
                        {
                            StringBuilder builder = new StringBuilder();
                            foreach (var item in destFiles)
                                builder.AppendLine(RemovePathID(item.Name));
                            if (Settings.MismatchLog)
                                File.AppendAllText($@".\mismatch\Unexcepted Files.txt", $"{builder.ToString()}\r\n");
                        }

                        // 통합 항목 병합
                        foreach (var content in Settings.Contents)
                        {
                            if (!content.Usage) continue;
                            if (content.Delete)
                            {
                                if (TDestDir.FullName != TResultDir.FullName) continue;
                                foreach (var assetName in content.Files)
                                {
                                    foreach (var asset in TSourceDir.GetFiles($"{assetName}-*", SearchOption.TopDirectoryOnly))
                                        asset.Delete();
                                    foreach (var asset in TDestDir.GetFiles($"{assetName}-*", SearchOption.TopDirectoryOnly))
                                        asset.Delete();
                                }
                            }
                            else if (content.Name == "RawCopy") CloneContents(content);
                            else if (content.Regex != null) ReplaceContents(content);
                        }

                        // 언어별 항목 병합
                        foreach (var locale in Settings.Locales)
                        {
                            if (!locale.Usage) continue;
                            switch (locale.Name)
                            {
                                case "AVG":
                                {
                                    DirectoryInfo overrideDir = new DirectoryInfo("overrides");
                                    FileInfo[] sources = TSourceDir.GetFiles($"{locale.BaseFileName}_{Settings.Source}-*", SearchOption.TopDirectoryOnly),
                                               dests = TDestDir.GetFiles($"{locale.BaseFileName}_{Settings.Destination}-*", SearchOption.TopDirectoryOnly),
                                               overrides = null;
                                    List<string> overridePaths = null;
                                    if (Settings.UseOverride && overrideDir.Exists)
                                    {
                                        overrides = overrideDir.GetFiles($"{locale.BaseFileName}*", SearchOption.AllDirectories);
                                        if (overrides.Length > 0)
                                        {
                                            overridePaths = new List<string>();
                                            foreach (var item in overrides)
                                                overridePaths.Add(item.FullName);
                                        }
                                    }

                                    if (sources.Length > 0)
                                        foreach (var dest in dests)
                                            ReplaceSingleContent(locale.Regex, sources[0].FullName, dest.FullName, Path.Combine(TResultDir.FullName, dest.Name), overridePaths?.Count > 0 ? overridePaths.ToArray() : null);

                                    if (Settings.RemoveDummy)
                                    {
                                        FileInfo[] datas = TDestDir.GetFiles($"{locale.BaseFileName}_*", SearchOption.TopDirectoryOnly);
                                        foreach (var data in datas)
                                        {
                                            if (data.Name.Contains(Settings.Destination)) continue;

                                            ClearSingleContent(locale.Regex, data.FullName, Path.Combine(TResultDir.FullName, data.Name));
                                        }
                                    }
                                }
                                break;

                                case "CFG":
                                {
                                    DirectoryInfo overrideDir = new DirectoryInfo("overrides");
                                    FileInfo[] sources = TSourceDir.GetFiles($"{locale.BaseFileName}_{Settings.Source}-*", SearchOption.TopDirectoryOnly);
                                    FileInfo[] dests = TDestDir.GetFiles($"{locale.BaseFileName}_{Settings.Destination}-*", SearchOption.TopDirectoryOnly);
                                    if (Settings.UseOverride && overrideDir.Exists)
                                    {
                                        FileInfo[] overrides = overrideDir.GetFiles($"{locale.BaseFileName}*", SearchOption.AllDirectories);
                                        if (overrides.Length > 0)
                                        {
                                            foreach (var item in dests)
                                            {
                                                Console.WriteLine($"Copying \"{Path.GetFileName(item.Name)}\"...");
                                                overrides[0].CopyTo(Path.Combine(TResultDir.FullName, item.Name), true);
                                            }
                                            continue;
                                        }
                                    }
                                    if (sources.Length > 0)
                                        foreach (var item in dests)
                                        {
                                            Console.WriteLine($"Copying \"{Path.GetFileName(item.Name)}\"...");
                                            sources[0].CopyTo(Path.Combine(TResultDir.FullName, item.Name), true);
                                        }

                                    if (Settings.RemoveDummy)
                                    {
                                        FileInfo[] CFGs = TDestDir.GetFiles($"{locale.BaseFileName}_*", SearchOption.TopDirectoryOnly);
                                        if (CFGs.Length > 0)
                                        {
                                            FileInfo SmallCFG = CFGs[0];
                                            for (int i = 0; i < CFGs.Length; i++)
                                            {
                                                if (SmallCFG.Length > CFGs[i].Length)
                                                    SmallCFG = CFGs[i];
                                                if (CFGs[i].Name.Contains(Settings.Source)
                                                 || CFGs[i].Name.Contains(Settings.Destination))
                                                    CFGs[i] = null;
                                            }
                                            for (int i = 0; i < CFGs.Length; i++)
                                                if (CFGs[i] != null && CFGs[i] != SmallCFG)
                                                {
                                                    Console.WriteLine($"Clearing \"{Path.GetFileName(CFGs[i].Name)}\"...");
                                                    SmallCFG.CopyTo(Path.Combine(TResultDir.FullName, CFGs[i].Name), true);
                                                }
                                        }
                                    }
                                }
                                break;

                                case "Data":
                                {
                                    DirectoryInfo overrideDir = new DirectoryInfo("overrides");
                                    FileInfo[] sources = TSourceDir.GetFiles(Settings.Source != "CN" ? $"{locale.BaseFileName}_{Settings.Source}-*"
                                                                                                     : $"{locale.BaseFileName}-*", SearchOption.TopDirectoryOnly),
                                               dests = TDestDir.GetFiles(Settings.Destination != "CN" ? $"{locale.BaseFileName}_{Settings.Destination}-*"
                                                                                                      : $"{locale.BaseFileName}-*", SearchOption.TopDirectoryOnly),
                                               overrides = null;
                                    if (Settings.UseOverride && overrideDir.Exists)
                                        overrides = overrideDir.GetFiles(Settings.Source != "CN" ? $"{locale.BaseFileName}_{Settings.Destination}*"
                                                                                                 : $"{locale.BaseFileName}*", SearchOption.AllDirectories);
                                    if (sources.Length > 0)
                                        foreach (var dest in dests)
                                            ReplaceSingleContent(locale.Regex
                                                               , sources[0].FullName
                                                               , dest.FullName
                                                               , Path.Combine(TResultDir.FullName, dest.Name)
                                                               , overrides?.Length > 0 ? new string[] { overrides[0].FullName } : null);

                                    if (Settings.RemoveDummy)
                                    {
                                        FileInfo[] datas = TDestDir.GetFiles($"{locale.BaseFileName}*", SearchOption.TopDirectoryOnly);
                                        foreach (var data in datas)
                                        {
                                            if (data.Name.Contains(Settings.Destination != "CN" ? $"{locale.BaseFileName}_{Settings.Destination}-"
                                                                                                : $"{locale.BaseFileName}-")) continue;

                                            ClearSingleContent(locale.Regex, data.FullName, Path.Combine(TResultDir.FullName, data.Name));
                                        }
                                    }
                                }
                                break;
                            }
                        }
                        Console.WriteLine("Operation Complete");
                    }
                    break;
                }
                case "pack":
                {
                    if (args.Length < 2)
                    {
                        Console.WriteLine($"Usage: {executedFileName} pack <Source>");
                        Console.WriteLine($"       {executedFileNameSpace} pack <Source>.xml <CompressionType>");
                        Console.WriteLine($"       {executedFileNameSpace}                    LZMA");
                        Console.WriteLine($"       {executedFileNameSpace}                    LZ4");
                        Console.WriteLine($"       {executedFileNameSpace}                    LZ4HC");
                        Console.WriteLine($"       {executedFileNameSpace}                    NONE");
                    }
                    else
                    {
                        string str = args[1];
                        AssetBundle assetBundle = new AssetBundle();
                        if (File.Exists(str))
                            if (Path.GetExtension(str).ToLower() == ".xml")
                            {
                                assetBundle.LoadFromXml(str);
                                if (args.Length > 1)
                                    switch (args[2].ToLower())
                                    {
                                        case "lz4":
                                            assetBundle.compression = CompressionType.LZ4;
                                            break;
                                        case "lz4hc":
                                            assetBundle.compression = CompressionType.LZ4HC;
                                            break;
                                        case "lzma":
                                            assetBundle.compression = CompressionType.LZMA;
                                            break;
                                        case "none":
                                            assetBundle.compression = CompressionType.NONE;
                                            break;
                                    }
                                assetBundle.Create();
                            }
                            else
                                try
                                {
                                    using (FileStream fileStream = File.Open(str, FileMode.Open))
                                    using (BinaryReader file = new BinaryReader(fileStream))
                                    {
                                        assetBundle.Load(file, str);
                                        assetBundle.dump();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine(ex.Message);
                                }
                    }
                    break;
                }
            }
            return;
            ParamException:
            Console.WriteLine($"Usage: {executedFileName} merge <Source> <Destination>");
            Console.WriteLine($"       {executedFileNameSpace} pack <Source>");
            Console.WriteLine($"       {executedFileNameSpace} pack <Source>.xml <CompressionType>");
            Console.WriteLine($"       {executedFileNameSpace}                    LZMA");
            Console.WriteLine($"       {executedFileNameSpace}                    LZ4");
            Console.WriteLine($"       {executedFileNameSpace}                    LZ4HC");
            Console.WriteLine($"       {executedFileNameSpace}                    NONE");
        }

        private static string RemovePathID(string filename)
            => Path.GetFileNameWithoutExtension(filename).Split('-')[0];

        private static bool CompareStreams(Stream a, Stream b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Length  != b.Length ) return false;
            for (int i = 0; i < a.Length; i++)
                if (a.ReadByte().CompareTo(b.ReadByte()) != 0)
                    return false;

            return true;
        }

        public static bool CompareFile(string pSource, string pDest)
            => CompareFile(new FileInfo(pSource), new FileInfo(pDest));
        public static bool CompareFile(FileInfo pSource, FileInfo pDest)
        {
            if (pSource == null && pDest == null) return true;
            if (pSource == null || pDest == null) return false;
            if (pSource.Exists && pDest.Exists
             && pSource.Length == pDest.Length)
                using (FileStream sourceFile = pSource.OpenRead())
                using (FileStream destFile = pDest.OpenRead())
                    if (CompareStreams(sourceFile, destFile))
                    {
                        sourceFile.Close();
                        destFile.Close();
                        return true;
                    }
            return false;
        }

        public static void ClearContents(Content pContent)
        {
            foreach (var item in pContent.Files)
                foreach (var dest in TDestDir.GetFiles($"{item}-*", SearchOption.TopDirectoryOnly))
                {
                    if (dest == null) continue;
                    ClearSingleContent(pContent.Regex, dest.FullName, Path.Combine(TResultDir.FullName, dest.Name));
                }
        }

        public static void ClearSingleContent(RegexPreset pRegex, string pDest, string pResult)
        {
            if (pRegex == null) throw new ArgumentNullException(nameof(pRegex));

            Console.WriteLine($"Clearing \"{Path.GetFileName(pResult)}\"...");

            List<string[]> dList = new List<string[]>();

            foreach (string item in File.ReadAllLines(pDest)) dList.Add(Regex.Split(item, pRegex.Search));

            StringBuilder builder = new StringBuilder();
            foreach (string[] dItem in dList)
            {
                builder.AppendFormat(pRegex.Empty, dItem);
                builder.AppendLine();
            }
            if (File.Exists(pResult)) File.Delete(pResult);
            File.WriteAllText(pResult, builder.ToString());
        }

        public static void CloneContents(Content pContent)
        {
            if (pContent == null) throw new ArgumentNullException(nameof(pContent));

            DirectoryInfo overrideDir = new DirectoryInfo("overrides");
            foreach (var item in pContent.Files)
            {
                FileInfo[] sources = TSourceDir.GetFiles($"{item}-*", SearchOption.TopDirectoryOnly),
                             dests = TDestDir.GetFiles($"{item}-*", SearchOption.TopDirectoryOnly),
                         overrides = null;
                if (Settings.UseOverride && overrideDir.Exists)
                {
                    overrides = overrideDir.GetFiles($"{item}*", SearchOption.AllDirectories);
                    if (overrides.Length > 0)
                    {
                        foreach (var dest in dests)
                        {
                            if (dest == null) continue;
                            Console.WriteLine($"Copying \"{dest.Name}\"...");
                            overrides[0].CopyTo(Path.Combine(TResultDir.FullName, dest.Name), true);
                        }
                        continue;
                    }
                }

                if (sources.Length > 1 || dests.Length > 1)
                {
                    for (int s = 0; s < sources.Length; s++)
                        for (int d = 0; d < dests.Length; d++)
                            if (CompareFile(sources[s], dests[d]))
                                sources[s] = dests[d] = null;
                }

                foreach (var source in sources)
                {
                    if (source == null) continue;
                    foreach (var dest in dests)
                    {
                        if (dest == null) continue;
                        Console.WriteLine($"Copying \"{dest.Name}\"...");
                        source.CopyTo(Path.Combine(TResultDir.FullName, dest.Name), true);
                    }
                    break;
                }
            }
        }

        public static void ReplaceContents(Content pContent)
        {
            if (pContent == null) throw new ArgumentNullException(nameof(pContent));

            DirectoryInfo overrideDir = new DirectoryInfo("overrides");
            foreach (var item in pContent.Files)
            {
                FileInfo[] sources = TSourceDir.GetFiles($"{item}-*", SearchOption.TopDirectoryOnly),
                             dests = TDestDir.GetFiles($"{item}-*", SearchOption.TopDirectoryOnly),
                         overrides = null;
                List<string> overridePaths = null;
                if (Settings.UseOverride && overrideDir.Exists)
                {
                    overrides = overrideDir.GetFiles($"{item}*", SearchOption.AllDirectories);
                    if(overrides.Length > 0)
                    {
                        overridePaths = new List<string>();
                        foreach (var file in overrides) overridePaths.Add(file.FullName);
                    }
                }

                if (sources.Length > 1 || dests.Length > 1)
                {
                    for (int s = 0; s < sources.Length; s++)
                        for (int d = 0; d < dests.Length; d++)
                            if (CompareFile(sources[s], dests[d]))
                                sources[s] = dests[d] = null;
                }
                foreach (var source in sources)
                {
                    if (source == null) continue;
                    foreach (var dest in dests)
                    {
                        if (dest == null) continue;
                        ReplaceSingleContent(pContent.Regex
                                           , source.FullName
                                           , dest.FullName
                                           , Path.Combine(TResultDir.FullName, dest.Name)
                                           , overridePaths?.Count > 0 ? overridePaths.ToArray() : null);
                    }
                    break;
                }
            }
        }

        public static void ReplaceSingleContent(RegexPreset pRegex, string pSource, string pDest, string pResult, string[] pOverride = null)
        {
            if (pRegex == null) throw new ArgumentNullException(nameof(pRegex));

            Console.WriteLine($"Writing \"{Path.GetFileName(pResult)}\"...");

            List<string[]> sList = new List<string[]>(), 
                           dList = new List<string[]>(), 
                           oList = null;

            foreach (string item in File.ReadAllLines(pSource)) sList.Add(Regex.Split(item, pRegex.Search));
            foreach (string item in File.ReadAllLines(pDest)) dList.Add(Regex.Split(item, pRegex.Search));

            if (Settings.UseOverride && pOverride != null)
            {
                oList = new List<string[]>();
                foreach (string path in pOverride)
                    foreach (string item in File.ReadAllLines(path))
                        oList.Add(Regex.Split(item, pRegex.Search));
            }

            int lines = 1;
            StringBuilder builder = new StringBuilder();
            foreach (string[] dItem in dList)
            {
                if (dItem == null) continue;
                for (int i = 1; i < dItem.Length - 1; i++)
                    if (pRegex.PrimaryKey.IndexOf(i) == -1
                    && (string.IsNullOrWhiteSpace(dItem[i])
                    ||  dItem[i].Contains("(todo)")))
                    {
                        builder.AppendFormat(pRegex.Empty, dItem);
                        goto Continue;
                    }

                string[] sItem = sList.Find(temp =>
                {
                    if (temp.Length != dItem.Length) return false;
                    for (int i = 1; i < temp.Length; i++)
                    {
                        if (pRegex.PrimaryKey.IndexOf(i) != -1
                         && temp[i] != dItem[i]) return false;
                    }
                    return true;
                });

                if (oList != null)
                {
                    string[] oItem = oList.Find(temp =>
                    {
                        if (temp.Length != dItem.Length) return false;
                        for (int i = 1; i < temp.Length; i++)
                        {
                            if (pRegex.PrimaryKey.IndexOf(i) != -1
                             && temp[i] != dItem[i]) return false;
                        }
                        return true;
                    });
                    if (oItem != null) sItem = oItem;
                }

                if (sItem != null)
                {
                    for (int i = 1; i < sItem.Length - 1; i++)
                        if (pRegex.PrimaryKey.IndexOf(i) == -1
                        && (string.IsNullOrWhiteSpace(sItem[i])
                        || sItem[i].Contains("(todo)")))
                            goto SourceNotExist;
                    builder.AppendFormat(pRegex.Replace, sItem);
                    goto Continue;
                }
                SourceNotExist:
                string result = string.Format(pRegex.Replace, dItem);
                builder.Append(result);
                if (Settings.MismatchLog)
                    File.AppendAllText($@".\mismatch\{RemovePathID(pDest)}.txt", $"{result}\r\n");
                Continue: builder.AppendLine();
                lines++;
            }
            if (File.Exists(pResult)) File.Delete(pResult);
            File.WriteAllText(pResult, builder.ToString());
        }
    }
}
