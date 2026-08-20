using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace SdwEditor.Formats
{
    internal sealed class DavTexture
    {
        public int Index;
        public int X, Y, Width, Height, PageIndex;
        public string ExportName;
        public Bitmap Bitmap;
        public int[] Argb;
        public bool Additive;
    }

    internal sealed class DavPage
    {
        public int Index;
        public int Width, Height;
        public bool Argb5551;
        public bool Additive;
        public Bitmap Bitmap;
        public int[] Argb;
    }

    internal sealed class DavFile : IDisposable
    {
        public string Path;
        public readonly List<DavTexture> Textures = new List<DavTexture>();
        public readonly List<DavPage> Pages = new List<DavPage>();
        private readonly Dictionary<int, string> _exportNames = new Dictionary<int, string>();

        public static DavFile Load(string path)
        {
            DavFile f = new DavFile();
            f.Path = path;
            byte[] d = File.ReadAllBytes(path);
            if (d.Length < 24 || d[0] != (byte)'V' || d[1] != (byte)'D' || d[2] != (byte)'X' || d[3] != (byte)'7')
            {
                throw new InvalidDataException(Loc.T("err_dav"));
            }

            int hdr = BitConverter.ToInt32(d, 20);
            int matN = BitConverter.ToUInt16(d, hdr);
            int texN = BitConverter.ToUInt16(d, hdr + 2);
            int pageN = BitConverter.ToUInt16(d, hdr + 4);
            int matP = BitConverter.ToInt32(d, hdr + 6);
            int texP = BitConverter.ToInt32(d, hdr + 10);
            int pageP = BitConverter.ToInt32(d, hdr + 14);
            int expP = BitConverter.ToInt32(d, hdr + 18);

            ushort[] materials = new ushort[matN];
            for (int i = 0; i < matN; i++)
            {
                materials[i] = BitConverter.ToUInt16(d, matP + i * 2);
            }

            f.ParseExports(d, expP, texP);
            int o = pageP;
            for (int p = 0; p < pageN; p++)
            {
                short x1 = BitConverter.ToInt16(d, o); o += 2;
                short x2 = BitConverter.ToInt16(d, o); o += 2;
                short y1 = BitConverter.ToInt16(d, o); o += 2;
                short y2 = BitConverter.ToInt16(d, o); o += 2;
                ushort flags = BitConverter.ToUInt16(d, o); o += 2;
                int w = Math.Abs(x1 - x2);
                int h = Math.Abs(y1 - y2);
                bool is5551 = (flags & 0x1C) == 4;
                bool additive = (flags & 0x1C) == 0x10;
                Bitmap bmp = DecodePage(d, o, w, h, is5551);
                o += w * h * 2;
                f.Pages.Add(new DavPage { Index = p, Width = w, Height = h, Argb5551 = is5551, Additive = additive, Bitmap = bmp, Argb = LockArgb(bmp) });
            }

            for (int i = 0; i < texN; i++)
            {
                int tp = texP + i * 10;
                DavTexture t = new DavTexture
                {
                    Index = i,
                    X = BitConverter.ToInt16(d, tp),
                    Width = BitConverter.ToInt16(d, tp + 2),
                    Y = BitConverter.ToInt16(d, tp + 4),
                    Height = BitConverter.ToInt16(d, tp + 6),
                    PageIndex = BitConverter.ToUInt16(d, tp + 8)
                };
                string n;
                t.ExportName = f._exportNames.TryGetValue(i, out n) ? n : Catalog.ResourceName(i);
                if (t.PageIndex >= 0 && t.PageIndex < f.Pages.Count)
                {
                    DavPage page = f.Pages[t.PageIndex];
                    t.Additive = page.Additive;
                    Crop(page, t.X, t.Y, t.Width, t.Height, out t.Bitmap, out t.Argb);
                }
                f.Textures.Add(t);
            }

            f._materials = materials;
            return f;
        }

        private ushort[] _materials = new ushort[0];

        public DavPage PageOf(DavTexture t)
        {
            if (t == null || t.PageIndex < 0 || t.PageIndex >= Pages.Count)
            {
                return null;
            }
            return Pages[t.PageIndex];
        }

        public DavTexture TextureByMaterial(int materialIndex)
        {
            if (materialIndex < 0 || materialIndex >= _materials.Length)
            {
                return null;
            }
            int tex = _materials[materialIndex];
            if (tex < 0 || tex >= Textures.Count)
            {
                return null;
            }
            return Textures[tex];
        }

        private void ParseExports(byte[] d, int expP, int texP)
        {
            if (expP < 0 || expP + 4 > d.Length)
            {
                return;
            }
            int n = BitConverter.ToInt32(d, expP);
            int o = expP + 4;
            for (int i = 0; i < n && o + 4 <= d.Length; i++)
            {
                ushort id = BitConverter.ToUInt16(d, o); o += 2;
                ushort size = BitConverter.ToUInt16(d, o); o += 2;
                string baseName = Catalog.ResourceName(id);
                for (int j = 0; j < size && o + 4 <= d.Length; j++)
                {
                    uint ptr = BitConverter.ToUInt32(d, o); o += 4;
                    int texIdx;
                    if (ptr >= texP)
                    {
                        texIdx = (int)((ptr - texP) / 10);
                    }
                    else if (ptr + 2 <= d.Length)
                    {
                        texIdx = BitConverter.ToUInt16(d, (int)ptr);
                    }
                    else
                    {
                        continue;
                    }
                    string name = j == 0 ? baseName : (baseName + "_" + j);
                    if (!_exportNames.ContainsKey(texIdx))
                    {
                        _exportNames[texIdx] = name;
                    }
                }
            }
        }

        private static Bitmap DecodePage(byte[] d, int offset, int w, int h, bool is5551)
        {
            Bitmap bmp = new Bitmap(Math.Max(1, w), Math.Max(1, h), PixelFormat.Format32bppArgb);
            BitmapData bd = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            int[] px = new int[bmp.Width * bmp.Height];
            int src = offset;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    ushort v = (src + 1 < d.Length) ? BitConverter.ToUInt16(d, src) : (ushort)0;
                    src += 2;
                    px[y * bmp.Width + x] = ToArgb(v, is5551);
                }
            }
            Marshal.Copy(px, 0, bd.Scan0, px.Length);
            bmp.UnlockBits(bd);
            return bmp;
        }

        private static int[] LockArgb(Bitmap bmp)
        {
            BitmapData bd = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            int[] px = new int[bmp.Width * bmp.Height];
            Marshal.Copy(bd.Scan0, px, 0, px.Length);
            bmp.UnlockBits(bd);
            return px;
        }

        private static int ToArgb(ushort val, bool is5551)
        {
            int r, g, b, a;
            if (is5551)
            {
                r = (val & 0x7C00) >> 10;
                g = (val & 0x03E0) >> 5;
                b = val & 0x001F;
                a = ((val & 0x8000) >> 15) ^ 1;
                r = (r << 3) | (r >> 2);
                g = (g << 3) | (g >> 2);
                b = (b << 3) | (b >> 2);
                a = a > 0 ? 255 : 0;
            }
            else
            {
                r = (val & 0x0F00) >> 8;
                g = (val & 0x00F0) >> 4;
                b = val & 0x000F;
                a = ((val & 0xF000) >> 12) ^ 0xF;
                r = (r << 4) | r;
                g = (g << 4) | g;
                b = (b << 4) | b;
                a = (a << 4) | a;
            }
            return (a << 24) | (r << 16) | (g << 8) | b;
        }

        private static void Crop(DavPage page, int x, int y, int w, int h, out Bitmap bmp, out int[] argb)
        {
            w = Math.Max(1, w);
            h = Math.Max(1, h);
            argb = new int[w * h];
            int[] src = page.Argb;
            int sw = page.Width;
            int sh = page.Height;
            if (src != null && sw > 0 && sh > 0)
            {
                for (int yy = 0; yy < h; yy++)
                {
                    int sy = y + yy;
                    if (sy < 0 || sy >= sh) continue;
                    int srcRow = sy * sw;
                    int dstRow = yy * w;
                    for (int xx = 0; xx < w; xx++)
                    {
                        int sx = x + xx;
                        if (sx < 0 || sx >= sw) continue;
                        argb[dstRow + xx] = src[srcRow + sx];
                    }
                }
            }
            bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            BitmapData bd = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            Marshal.Copy(argb, 0, bd.Scan0, argb.Length);
            bmp.UnlockBits(bd);
        }

        public void Dispose()
        {
            foreach (DavPage p in Pages)
            {
                if (p.Bitmap != null) p.Bitmap.Dispose();
            }
            foreach (DavTexture t in Textures)
            {
                if (t.Bitmap != null) t.Bitmap.Dispose();
            }
        }
    }
}
