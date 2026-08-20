using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace SdwEditor
{
    internal static class Catalog
    {
        private static readonly Dictionary<int, string> Classes = new Dictionary<int, string>();
        private static readonly Dictionary<int, string> Resources = new Dictionary<int, string>();
        private static readonly Regex Define = new Regex(
            @"#define\s+(CLASSID_|DAV_IDI_|WAR_IDO_)(\w+)\s+(\d+)",
            RegexOptions.Compiled);

        public static void Load(string levelsRoot)
        {
            Classes.Clear();
            Resources.Clear();
            if (string.IsNullOrEmpty(levelsRoot) || !Directory.Exists(levelsRoot))
            {
                return;
            }

            string[] headers = Directory.GetFiles(levelsRoot, "*.h", SearchOption.AllDirectories);
            foreach (string header in headers)
            {
                ParseHeader(File.ReadAllText(header));
            }
        }

        private static void ParseHeader(string text)
        {
            foreach (Match m in Define.Matches(text))
            {
                int id = int.Parse(m.Groups[3].Value);
                string name = m.Groups[2].Value;
                if (m.Groups[1].Value == "CLASSID_")
                {
                    Classes[id] = name;
                }
                else
                {
                    Resources[id] = m.Groups[1].Value.TrimEnd('_') + " " + name;
                }
            }
        }

        public static string ClassName(int id)
        {
            string name;
            return Classes.TryGetValue(id, out name) ? name : ("CLASS_" + id);
        }

        public static string ResourceName(int id)
        {
            string name;
            return Resources.TryGetValue(id, out name) ? name : ("ID_" + id);
        }
    }
}
