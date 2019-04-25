using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Newtonsoft.Json;

namespace GFTextMerge
{
    using JsonObject;

    class Program
    {
        private static Settings Settings;
        private static string TargetSourcePath;
        private static string TargetDestPath;
        private static string TargetResultPath;
        static void Main(string[] args)
        {
            // 기본값 설정 생성
            if (!File.Exists("Settings.json")) File.WriteAllBytes("Settings.json", Properties.Resources.Settings);
            if (File.Exists("mismatch.log")) File.Delete("mismatch.log");
            if (!Directory.Exists("overrides")) Directory.CreateDirectory("overrides");
            Settings = JsonConvert.DeserializeObject<Settings>(File.ReadAllText("Settings.json"));

            // 매개변수 부족할 경우 
            if (args.Length < 3)
            {
                Console.WriteLine($"Usage: {Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().Location)} <Source> <Destination> <Result>");
                return;
            }
            TargetSourcePath = args[0];
            TargetDestPath = args[1];
            TargetResultPath = args[2];
        }

        private static string RemovePathID(string filename)
            => Path.GetFileNameWithoutExtension(filename).Split('-')[0];

        private static bool CompareStreams(Stream a, Stream b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) throw new ArgumentNullException(a == null ? "a" : "b");
            if (a.Length < b.Length) return false;
            if (a.Length > b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a.ReadByte().CompareTo(b.ReadByte()) != 0)
                    return false;

            return true;
        }

        public bool CompareFile(string pSource, string pDest)
        {
            FileInfo sFile = new FileInfo(pSource),
                     dFile = new FileInfo(pDest);
            if (sFile.Exists && dFile.Exists 
             && sFile.Length == dFile.Length)
                using (FileStream sStream = sFile.OpenRead())
                using (FileStream dStream = dFile.OpenRead())
                    if (CompareStreams(sStream, dStream))
                    {
                        sStream.Close();
                        dStream.Close();
                        return true;
                    }
            return false;
        }

        public static void ReplaceContents(Content pContent, string pSourceDir, string pDestDir, string pResultDir)
        {
            if (pContent == null) throw new ArgumentNullException(nameof(pContent));

            DirectoryInfo sourceDir = new DirectoryInfo(pSourceDir),
                          destDir = new DirectoryInfo(pDestDir),
                          overrideDir = new DirectoryInfo("overrides");
            foreach (var item in pContent.Files)
            {
                FileInfo[] sources = sourceDir.GetFiles($"{item}-*", SearchOption.TopDirectoryOnly),
                           dests = destDir.GetFiles($"{item}-*", SearchOption.TopDirectoryOnly),
                           overrides = null;
                if (Settings.UseOverride && overrideDir.Exists)
                    overrides = overrideDir.GetFiles($"{item}-*", SearchOption.TopDirectoryOnly);

                if (sources.Length > 1 || dests.Length > 1)
                {
                    
                }
                else
                {

                }
            }
        }

        public static void ReplaceSingleContent(RegexPreset pRegex, string pSource, string pDest, string pResult, string pOverride = null)
        {
            List<string[]> sList = new List<string[]>(), 
                           dList = new List<string[]>(), 
                           oList = null;

            foreach (string item in File.ReadAllLines(pSource)) sList.Add(Regex.Split(item, pRegex.Search));
            foreach (string item in File.ReadAllLines(pDest)) dList.Add(Regex.Split(item, pRegex.Search));

            if (Settings.UseOverride && pOverride != null)
            {
                oList = new List<string[]>();
                foreach (string item in File.ReadAllLines(pOverride))
                    oList.Add(Regex.Split(item, pRegex.Search));
            }

            int lines = 1;
            StringBuilder builder = new StringBuilder();
            foreach (string[] dItem in dList)
            {
                if (dItem == null) continue;
                for (int i = 1; i < dItem.Length; i++)
                   if (pRegex.PrimaryKey.IndexOf(i) == -1
                     && string.IsNullOrWhiteSpace(dItem[i]))
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

                if (sItem == null)
                {
                    string result = string.Format(pRegex.Replace, dItem);
                    builder.Append(result);
                    if (Settings.MismatchLog)
                        File.AppendAllText("mismatch.log", $"{Path.GetFileNameWithoutExtension(pDest)}[{lines}] : {result}\r\n");
                }
                else builder.AppendFormat(pRegex.Replace, sItem);

                Continue: builder.AppendLine();
                lines++;
            }
            if (File.Exists(pResult)) File.Delete(pResult);
            File.WriteAllText(pResult, builder.ToString());
        }
    }
}
