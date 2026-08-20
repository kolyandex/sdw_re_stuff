using System;
using System.IO;

namespace SdwEditor.Formats
{
    internal sealed class LevelSet : IDisposable
    {
        public string Folder;
        public string Name;
        public DavFile Dav;
        public WarFile War;
        public MltFile Mlt;
        public SndFile Snd;

        public static LevelSet Load(string folder)
        {
            LevelSet set = new LevelSet();
            set.Folder = folder;
            set.Name = Path.GetFileName(folder);
            string stem = FindStem(folder);
            string parent = Directory.GetParent(folder) != null ? Directory.GetParent(folder).FullName : folder;
            Catalog.Load(parent);
            Catalog.Load(folder);
            string dav = Find(folder, stem, ".dav");
            string war = Find(folder, stem, ".war");
            string mlt = Find(folder, stem, ".mlt");
            string snd = Find(folder, stem, ".snd");
            if (dav != null) set.Dav = DavFile.Load(dav);
            if (war != null) set.War = WarFile.Load(war, set.Dav);
            if (mlt != null) set.Mlt = MltFile.Load(mlt);
            if (snd != null) set.Snd = SndFile.Load(snd);
            return set;
        }

        private static string FindStem(string folder)
        {
            foreach (string f in Directory.GetFiles(folder))
            {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                if (ext == ".dav" || ext == ".war" || ext == ".mlt" || ext == ".snd")
                {
                    return Path.GetFileNameWithoutExtension(f);
                }
            }
            return Path.GetFileName(folder);
        }

        private static string Find(string folder, string stem, string ext)
        {
            string[] files = Directory.GetFiles(folder, "*" + ext);
            foreach (string f in files)
            {
                if (string.Equals(Path.GetFileNameWithoutExtension(f), stem, StringComparison.OrdinalIgnoreCase))
                {
                    return f;
                }
            }
            return files.Length > 0 ? files[0] : null;
        }

        public void Dispose()
        {
            if (Dav != null) Dav.Dispose();
        }
    }
}
