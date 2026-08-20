using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SdwEditor.Formats
{
    internal sealed class SndEntry
    {
        public int Index;
        public int SoundId;
        public uint Flags;
        public int WavOffset;
        public int WavLength;
        public byte[] WavBytes;
    }

    internal sealed class SndFile
    {
        private static readonly byte[] Riff = Encoding.ASCII.GetBytes("RIFF");
        private static readonly byte[] Wave = Encoding.ASCII.GetBytes("WAVE");
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("vdx7");

        public string Path;
        public byte[] Data;
        public readonly List<SndEntry> Entries = new List<SndEntry>();

        public static SndFile Load(string path)
        {
            SndFile f = new SndFile();
            f.Path = path;
            f.Data = File.ReadAllBytes(path);
            f.Parse();
            return f;
        }

        private void Parse()
        {
            Entries.Clear();
            byte[] data = Data;
            int i = 0;
            int index = 0;
            while (i <= data.Length - 12)
            {
                if (!Match(data, i, Riff) || !Match(data, i + 8, Wave))
                {
                    i++;
                    continue;
                }

                uint payload = BitConverter.ToUInt32(data, i + 4);
                long total = 8L + payload;
                if (total < 12 || i + total > data.Length)
                {
                    i++;
                    continue;
                }

                int soundId = -1;
                uint flags = 0;
                if (i >= 12 && BitConverter.ToUInt32(data, i - 8) == total)
                {
                    soundId = (int)BitConverter.ToUInt32(data, i - 12);
                    flags = BitConverter.ToUInt32(data, i - 4);
                }

                byte[] wav = new byte[(int)total];
                Buffer.BlockCopy(data, i, wav, 0, wav.Length);
                Entries.Add(new SndEntry
                {
                    Index = index++,
                    SoundId = soundId,
                    Flags = flags,
                    WavOffset = i,
                    WavLength = wav.Length,
                    WavBytes = wav
                });
                i += (int)total;
            }
        }

        public void ReplaceWav(int index, byte[] wav)
        {
            if (wav == null || wav.Length < 12 || !Match(wav, 0, Riff))
            {
                throw new InvalidDataException(Loc.T("err_wav"));
            }
            Entries[index].WavBytes = wav;
            Entries[index].WavLength = wav.Length;
        }

        public void Save(string path)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                BinaryWriter w = new BinaryWriter(ms);
                w.Write(0);
                w.Write(Magic);
                w.Write((uint)Entries.Count);
                foreach (SndEntry e in Entries)
                {
                    w.Write((uint)Math.Max(0, e.SoundId));
                    w.Write((uint)e.WavBytes.Length);
                    w.Write(e.Flags);
                    w.Write(e.WavBytes);
                }
                byte[] data = ms.ToArray();
                Crc32Sdw.Patch(data);
                File.WriteAllBytes(path, data);
                Path = path;
                Data = data;
                Parse();
            }
        }

        public void ExportAll(string dir)
        {
            Directory.CreateDirectory(dir);
            string baseName = System.IO.Path.GetFileNameWithoutExtension(Path);
            foreach (SndEntry e in Entries)
            {
                string name = string.Format("{0}_{1:000}_id{2:000}.wav", baseName, e.Index, e.SoundId);
                File.WriteAllBytes(System.IO.Path.Combine(dir, name), e.WavBytes);
            }
        }

        private static bool Match(byte[] data, int offset, byte[] sig)
        {
            if (offset < 0 || offset + sig.Length > data.Length)
            {
                return false;
            }
            for (int i = 0; i < sig.Length; i++)
            {
                if (data[offset + i] != sig[i])
                {
                    return false;
                }
            }
            return true;
        }
    }
}
