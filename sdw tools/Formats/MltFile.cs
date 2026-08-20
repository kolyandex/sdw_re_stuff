using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SdwEditor.Formats
{
    internal sealed class MltFile
    {
        public string Path;
        public string Version;
        public int LanguageCount;
        public int SectionCount;
        public readonly List<List<List<string>>> Languages = new List<List<List<string>>>();
        private static readonly Encoding Cp1251 = Encoding.GetEncoding(1251);

        public static MltFile Load(string path)
        {
            MltFile f = new MltFile();
            f.Path = path;
            byte[] data = File.ReadAllBytes(path);
            f.Version = Encoding.ASCII.GetString(data, 4, 4);
            f.LanguageCount = BitConverter.ToUInt16(data, 8);
            f.SectionCount = BitConverter.ToUInt16(data, 10);
            int o = 16;
            for (int lang = 0; lang < f.LanguageCount; lang++)
            {
                List<List<string>> sections = new List<List<string>>();
                for (int s = 0; s < f.SectionCount; s++)
                {
                    if (o >= data.Length)
                    {
                        break;
                    }
                    int n = data[o++];
                    List<string> strs = new List<string>();
                    for (int i = 0; i < n; i++)
                    {
                        int end = o;
                        while (end < data.Length && data[end] != 0)
                        {
                            end++;
                        }
                        strs.Add(Cp1251.GetString(data, o, end - o));
                        o = Math.Min(end + 1, data.Length);
                    }
                    sections.Add(strs);
                }
                f.Languages.Add(sections);
            }
            return f;
        }

        public void Save(string path)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                BinaryWriter w = new BinaryWriter(ms);
                w.Write(0);
                byte[] ver = Encoding.ASCII.GetBytes((Version ?? "v1.2").PadRight(4).Substring(0, 4));
                w.Write(ver);
                w.Write((ushort)Languages.Count);
                w.Write((ushort)SectionCount);
                w.Write(0);
                foreach (List<List<string>> lang in Languages)
                {
                    for (int s = 0; s < SectionCount; s++)
                    {
                        List<string> strs = s < lang.Count ? lang[s] : new List<string>();
                        w.Write((byte)Math.Min(255, strs.Count));
                        int count = Math.Min(255, strs.Count);
                        for (int i = 0; i < count; i++)
                        {
                            byte[] raw = Cp1251.GetBytes(strs[i] ?? "");
                            w.Write(raw);
                            w.Write((byte)0);
                        }
                    }
                }
                byte[] data = ms.ToArray();
                Crc32Sdw.Patch(data);
                File.WriteAllBytes(path, data);
                Path = path;
            }
        }
    }
}
