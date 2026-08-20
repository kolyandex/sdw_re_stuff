using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace SdwEditor.Formats
{
    internal enum WarKind
    {
        Unknown,
        StaticMesh,
        AnimatedMesh,
        Skybox,
        Scenaric,
        CollisionMap,
        CollisionPolys,
        Export,
        Config,
        ScreenMap,
        CinExp,
        DebugAni
    }

    internal struct WarVec
    {
        public float X, Y, Z;
        public WarVec(float x, float y, float z) { X = x; Y = y; Z = z; }
    }

    internal struct WarTri
    {
        public WarVec A, B, C;
        public Color Ca, Cb, Cc;
        public float Ua, Va, Ub, Vb, Uc, Vc;
        public int[] TexArgb;
        public int TexW, TexH;
        public bool Textured;
        public bool Additive;
        public int Ia, Ib, Ic;
    }

    internal sealed class WarResource
    {
        public int Index;
        public uint Raw;
        public int Pointer;
        public byte Flags;
        public byte ModelType;
        public bool IsActor;
        public WarKind Kind;
        public int VertexCount;
        public int ChunkCount;
        public int BoneCount;
        public int ClassId = -1;
        public int ModelRef = -1;
        public WarVec Position;
        public short RotPitch, RotYaw, RotRoll;
        public List<WarTri> Tris = new List<WarTri>();
        public string Label;
        public WarBone[] Bones;
        public int[] VertBone;
        public float[] Lx, Ly, Lz;
        public List<WarAnimClip> Anims;
    }

    internal sealed class WarFile
    {
        public string Path;
        public string Version;
        public Color Fog = Color.FromArgb(248, 176, 96);
        public readonly List<WarResource> Resources = new List<WarResource>();
        private byte[] _data;

        public static WarFile Load(string path, DavFile dav)
        {
            WarFile f = new WarFile();
            f.Path = path;
            f._data = File.ReadAllBytes(path);
            byte[] d = f._data;
            if (d.Length < 16)
            {
                throw new InvalidDataException(Loc.T("err_war_short"));
            }
            f.Version = System.Text.Encoding.ASCII.GetString(d, 4, 4);
            f.Fog = Color.FromArgb(d[8], d[9], d[10]);
            int n = BitConverter.ToInt32(d, 12);
            for (int i = 0; i < n; i++)
            {
                uint rec = BitConverter.ToUInt32(d, 16 + i * 4);
                WarResource r = new WarResource();
                r.Index = i;
                r.Raw = rec;
                r.Pointer = (int)(rec & 0xFFFFFF);
                r.Flags = (byte)(rec >> 24);
                r.ModelType = (byte)(r.Flags & 0xBF);
                r.IsActor = (r.Flags & 0x40) != 0;
                r.Kind = KindOf(r.Flags, r.ModelType);
                f.ParseBody(r, dav);
                r.Label = f.MakeLabel(r);
                f.Resources.Add(r);
            }
            return f;
        }

        private static WarKind KindOf(byte flags, byte modelType)
        {
            if (flags == 5) return WarKind.Scenaric;
            if (flags == 0x80) return WarKind.CollisionMap;
            if (flags == 0x81) return WarKind.CollisionPolys;
            if (flags == 0x82) return WarKind.Export;
            if (flags == 0x83) return WarKind.Config;
            if (flags == 0x84) return WarKind.ScreenMap;
            if (flags == 0x85) return WarKind.CinExp;
            if (flags == 0x86) return WarKind.DebugAni;
            if (modelType == 4) return WarKind.AnimatedMesh;
            if (modelType == 38) return WarKind.Skybox;
            if (modelType == 3 || modelType == 11) return WarKind.StaticMesh;
            return WarKind.Unknown;
        }

        public void Relabel()
        {
            for (int i = 0; i < Resources.Count; i++)
            {
                Resources[i].Label = MakeLabel(Resources[i]);
            }
        }

        private string MakeLabel(WarResource r)
        {
            switch (r.Kind)
            {
                case WarKind.Scenaric:
                    return string.Format("#{0}  {1}  → mesh {2}", r.Index, Catalog.ClassName(r.ClassId), r.ModelRef);
                case WarKind.AnimatedMesh:
                    return Loc.F("label_skel", r.Index, r.VertexCount, r.BoneCount, r.Anims == null ? 0 : r.Anims.Count);
                case WarKind.StaticMesh:
                    return Loc.F("label_mesh", r.Index, r.IsActor ? Loc.T("label_actor") : Loc.T("label_geom"), r.VertexCount);
                case WarKind.Skybox:
                    return Loc.F("label_sky", r.Index, r.VertexCount);
                case WarKind.CollisionMap:
                    return Loc.F("label_collmap", r.Index);
                case WarKind.CollisionPolys:
                    return Loc.F("label_collpoly", r.Index);
                case WarKind.Export:
                    return "#" + r.Index + "  EXPORT";
                default:
                    return string.Format("#{0}  {1}  flags=0x{2:X2}", r.Index, r.Kind, r.Flags);
            }
        }

        private void ParseBody(WarResource r, DavFile dav)
        {
            byte[] d = _data;
            int p = r.Pointer;
            if (p < 0 || p + 4 > d.Length)
            {
                return;
            }

            if (r.Kind == WarKind.Scenaric && p + 18 <= d.Length)
            {
                r.ModelRef = BitConverter.ToUInt16(d, p);
                r.Position = MapVert(
                    BitConverter.ToInt16(d, p + 4),
                    BitConverter.ToInt16(d, p + 6),
                    BitConverter.ToInt16(d, p + 8));
                r.ClassId = BitConverter.ToUInt16(d, p + 10);
                r.RotPitch = BitConverter.ToInt16(d, p + 12);
                r.RotYaw = BitConverter.ToInt16(d, p + 14);
                r.RotRoll = BitConverter.ToInt16(d, p + 16);
                return;
            }

            if (r.Kind == WarKind.CollisionPolys)
            {
                ParseCollision(r, p);
                return;
            }

            if (r.Kind == WarKind.StaticMesh || r.Kind == WarKind.AnimatedMesh || r.Kind == WarKind.Skybox)
            {
                ParseModel(r, dav);
            }
        }

        private void ParseModel(WarResource r, DavFile dav)
        {
            byte[] d = _data;
            int p = r.Pointer;
            if (p + 16 > d.Length) return;
            int vertPtr = BitConverter.ToInt32(d, p);
            int meshPtr = BitConverter.ToInt32(d, p + 4);
            r.VertexCount = BitConverter.ToUInt16(d, p + 8);
            r.ChunkCount = BitConverter.ToUInt16(d, p + 10);
            short[] rx = new short[r.VertexCount];
            short[] ry = new short[r.VertexCount];
            short[] rz = new short[r.VertexCount];
            for (int i = 0; i < r.VertexCount; i++)
            {
                int vp = vertPtr + i * 8;
                if (vp + 6 > d.Length) break;
                rx[i] = BitConverter.ToInt16(d, vp);
                ry[i] = BitConverter.ToInt16(d, vp + 2);
                rz[i] = BitConverter.ToInt16(d, vp + 4);
            }

            float[] wx = null, wy = null, wz = null;
            ushort[] boneVertCount = null;
            if (r.Kind == WarKind.AnimatedMesh && p + 28 <= d.Length)
            {
                int skel = BitConverter.ToInt32(d, p + 20);
                int boneCount = BitConverter.ToUInt16(d, p + 24);
                if (skel > 0 && boneCount > 0 && boneCount < 512 && skel + boneCount * 10 <= d.Length)
                {
                    r.BoneCount = boneCount;
                    r.Bones = new WarBone[boneCount];
                    r.VertBone = new int[r.VertexCount];
                    r.Lx = new float[r.VertexCount];
                    r.Ly = new float[r.VertexCount];
                    r.Lz = new float[r.VertexCount];
                    int[] parent = new int[boneCount];
                    short[] bx = new short[boneCount];
                    short[] by = new short[boneCount];
                    short[] bz = new short[boneCount];
                    boneVertCount = new ushort[boneCount];
                    int bo = skel;
                    for (int b = 0; b < boneCount; b++)
                    {
                        parent[b] = BitConverter.ToUInt16(d, bo);
                        bx[b] = BitConverter.ToInt16(d, bo + 2);
                        by[b] = BitConverter.ToInt16(d, bo + 4);
                        bz[b] = BitConverter.ToInt16(d, bo + 6);
                        boneVertCount[b] = BitConverter.ToUInt16(d, bo + 8);
                        r.Bones[b].Parent = parent[b];
                        r.Bones[b].Tx = bx[b] / 100f;
                        r.Bones[b].Ty = -by[b] / 100f;
                        r.Bones[b].Tz = -bz[b] / 100f;
                        bo += 10;
                    }
                    wx = new float[boneCount];
                    wy = new float[boneCount];
                    wz = new float[boneCount];
                    bool[] ready = new bool[boneCount];
                    int left = boneCount;
                    for (int guard = 0; guard < boneCount + 2 && left > 0; guard++)
                    {
                        for (int b = 0; b < boneCount; b++)
                        {
                            if (ready[b]) continue;
                            int par = parent[b];
                            if (par == 0xFFFF || par < 0 || par >= boneCount)
                            {
                                wx[b] = bx[b];
                                wy[b] = by[b];
                                wz[b] = bz[b];
                                ready[b] = true;
                                left--;
                            }
                            else if (ready[par])
                            {
                                wx[b] = wx[par] + bx[b];
                                wy[b] = wy[par] + by[b];
                                wz[b] = wz[par] + bz[b];
                                ready[b] = true;
                                left--;
                            }
                        }
                    }
                }
            }

            WarVec[] verts = new WarVec[r.VertexCount];
            int vi = 0;
            if (wx != null && boneVertCount != null)
            {
                for (int b = 0; b < boneVertCount.Length; b++)
                {
                    int n = boneVertCount[b];
                    for (int k = 0; k < n && vi < r.VertexCount; k++, vi++)
                    {
                        r.VertBone[vi] = b;
                        r.Lx[vi] = rx[vi] / 100f;
                        r.Ly[vi] = -ry[vi] / 100f;
                        r.Lz[vi] = -rz[vi] / 100f;
                        WarVec bv = MapVert(rx[vi] + wx[b], ry[vi] + wy[b], rz[vi] + wz[b]);
                        verts[vi] = new WarVec(bv.X * 0.125f, bv.Y * 0.125f, bv.Z * 0.125f);
                    }
                }
                ParseAnims(r, BitConverter.ToInt32(d, p + 16));
            }
            while (vi < r.VertexCount)
            {
                if (r.Lx != null)
                {
                    r.VertBone[vi] = 0;
                    r.Lx[vi] = rx[vi] / 100f;
                    r.Ly[vi] = -ry[vi] / 100f;
                    r.Lz[vi] = -rz[vi] / 100f;
                }
                verts[vi] = MapVert(rx[vi], ry[vi], rz[vi]);
                vi++;
            }

            int o = meshPtr;
            for (int c = 0; c < r.ChunkCount && o + 4 <= d.Length; c++)
            {
                short type = BitConverter.ToInt16(d, o); o += 2;
                int count = BitConverter.ToUInt16(d, o); o += 2;
                for (int i = 0; i < count; i++)
                {
                    int next;
                    if (!ReadPoly(d, o, type, verts, dav, r.Tris, out next))
                    {
                        return;
                    }
                    o = next;
                }
            }
        }

        private static WarVec MapVert(float x, float y, float z)
        {
            return new WarVec(x / 100f, z / 100f, -y / 100f);
        }

        private void ParseAnims(WarResource r, int animList)
        {
            byte[] d = _data;
            if (r.Bones == null || animList <= 0 || animList + 4 > d.Length)
            {
                return;
            }
            int count = BitConverter.ToInt32(d, animList);
            if (count <= 0 || count > 512)
            {
                return;
            }
            r.Anims = new List<WarAnimClip>();
            int n = r.Bones.Length;
            for (int i = 0; i < count; i++)
            {
                int rec = animList + 4 + i * 4;
                if (rec + 4 > d.Length) break;
                int off = BitConverter.ToInt32(d, rec);
                if (off <= 0 || off + 10 > d.Length) continue;
                WarAnimClip clip = ReadClip(d, off, r, n);
                if (clip != null)
                {
                    r.Anims.Add(clip);
                }
            }
        }

        private static WarAnimClip ReadClip(byte[] d, int off, WarResource r, int boneCount)
        {
            int nameLen = 0;
            while (nameLen < 8 && d[off + nameLen] != 0) nameLen++;
            string name = System.Text.Encoding.ASCII.GetString(d, off, nameLen);
            int frames = BitConverter.ToUInt16(d, off + 8);
            if (frames <= 0 || frames > 4096)
            {
                return null;
            }
            WarBonePose[] last = new WarBonePose[boneCount];
            for (int b = 0; b < boneCount; b++)
            {
                last[b] = WarPose.RestBone(r.Bones[b]);
            }
            WarBonePose[][] posed = new WarBonePose[frames][];
            float[] times = new float[frames];
            float t = 0f;
            int o = off + 10;
            for (int f = 0; f < frames; f++)
            {
                if (o + 8 > d.Length) return null;
                int dur = BitConverter.ToUInt16(d, o);
                int ntr = BitConverter.ToUInt16(d, o + 2);
                int words = BitConverter.ToUInt16(d, o + 4);
                o += 8;
                int blob = o;
                int end = o + words * 2;
                if (end > d.Length) return null;
                for (int k = 0; k < ntr && o + 2 <= end; k++)
                {
                    ushort flags = BitConverter.ToUInt16(d, o); o += 2;
                    int bone = flags & 0x3F;
                    bool is8 = (flags & 0x8000) != 0;
                    if (bone >= boneCount)
                    {
                        SkipChannels(d, ref o, flags, is8, blob, end);
                        continue;
                    }
                    WarBonePose p = WarPose.RestBone(r.Bones[bone]);
                    ReadChannels(d, ref o, flags, is8, end, ref p);
                    if (is8 && ((o - blob) & 1) != 0 && o < end) o++;
                    WarPose.BakeQuat(ref p);
                    last[bone] = p;
                }
                o = end;
                times[f] = t / 1000f;
                t += dur <= 0 ? 1 : dur;
                posed[f] = (WarBonePose[])last.Clone();
            }
            WarAnimClip clip = new WarAnimClip();
            clip.Name = string.IsNullOrEmpty(name) ? "clip" : name;
            clip.Duration = t / 1000f;
            clip.Times = times;
            clip.Frames = posed;
            return clip;
        }

        private static void ReadChannels(byte[] d, ref int o, ushort flags, bool is8, int end, ref WarBonePose p)
        {
            if ((flags & 0x0040) != 0) p.Rx += ReadRot(d, ref o, is8, end);
            if ((flags & 0x0080) != 0) p.Ry -= ReadRot(d, ref o, is8, end);
            if ((flags & 0x0100) != 0) p.Rz -= ReadRot(d, ref o, is8, end);
            if ((flags & 0x0200) != 0) p.Tx += ReadPos(d, ref o, is8, end);
            if ((flags & 0x0400) != 0) p.Ty -= ReadPos(d, ref o, is8, end);
            if ((flags & 0x0800) != 0) p.Tz -= ReadPos(d, ref o, is8, end);
            if ((flags & 0x1000) != 0) p.Sx = ReadScl(d, ref o, is8, end);
            if ((flags & 0x2000) != 0) p.Sy = ReadScl(d, ref o, is8, end);
            if ((flags & 0x4000) != 0) p.Sz = ReadScl(d, ref o, is8, end);
        }

        private static void SkipChannels(byte[] d, ref int o, ushort flags, bool is8, int blob, int end)
        {
            WarBonePose dummy = new WarBonePose();
            ReadChannels(d, ref o, flags, is8, end, ref dummy);
            if (is8 && ((o - blob) & 1) != 0 && o < end) o++;
        }

        private static float ReadRot(byte[] d, ref int o, bool is8, int end)
        {
            float rads;
            if (is8)
            {
                if (o >= end) return 0f;
                sbyte v = (sbyte)d[o]; o++;
                rads = -(v * (float)Math.PI) / 64f;
            }
            else
            {
                if (o + 2 > end) return 0f;
                short v = BitConverter.ToInt16(d, o); o += 2;
                rads = -(v * (float)Math.PI) / 2048f;
            }
            return rads;
        }

        private static float ReadPos(byte[] d, ref int o, bool is8, int end)
        {
            if (is8)
            {
                if (o >= end) return 0f;
                sbyte v = (sbyte)d[o]; o++;
                return v / 100f;
            }
            if (o + 2 > end) return 0f;
            short v16 = BitConverter.ToInt16(d, o); o += 2;
            return v16 / 100f;
        }

        private static float ReadScl(byte[] d, ref int o, bool is8, int end)
        {
            if (is8)
            {
                if (o >= end) return 1f;
                byte v = d[o]; o++;
                return v / 128f;
            }
            if (o + 2 > end) return 1f;
            ushort v16 = BitConverter.ToUInt16(d, o); o += 2;
            return v16 / 1024f;
        }

        private int PointerEnd(int start)
        {
            byte[] d = _data;
            if (d.Length < 16) return d.Length;
            int n = BitConverter.ToInt32(d, 12);
            int end = d.Length;
            for (int i = 0; i < n; i++)
            {
                int p = (int)(BitConverter.ToUInt32(d, 16 + i * 4) & 0xFFFFFF);
                if (p > start && p < end)
                {
                    end = p;
                }
            }
            return end;
        }

        private void ParseCollision(WarResource r, int start)
        {
            byte[] d = _data;
            int end = PointerEnd(start);
            int count = (end - start) / 40;
            if (count < 0) count = 0;
            if (count > 20000) count = 20000;
            Color col = Color.FromArgb(180, 80, 200, 255);
            for (int i = 0; i < count; i++)
            {
                int o = start + i * 40;
                if (o + 40 > d.Length) break;
                WarVec a = MapVert(
                    BitConverter.ToInt16(d, o + 20),
                    BitConverter.ToInt16(d, o + 22),
                    BitConverter.ToInt16(d, o + 24));
                WarVec b = MapVert(
                    BitConverter.ToInt16(d, o + 26),
                    BitConverter.ToInt16(d, o + 28),
                    BitConverter.ToInt16(d, o + 30));
                WarVec c = MapVert(
                    BitConverter.ToInt16(d, o + 32),
                    BitConverter.ToInt16(d, o + 34),
                    BitConverter.ToInt16(d, o + 36));
                r.Tris.Add(new WarTri { A = a, B = b, C = c, Ca = col, Cb = col, Cc = col });
            }
            r.VertexCount = count * 3;
            r.ChunkCount = count;
        }

        private static Color Rgb(byte[] d, int o)
        {
            return Color.FromArgb(255, d[o], d[o + 1], d[o + 2]);
        }

        private static WarVec V(WarVec[] verts, int i)
        {
            if (i < 0 || i >= verts.Length) return new WarVec();
            return verts[i];
        }

        private static bool ReadPoly(byte[] d, int o, short type, WarVec[] verts, DavFile dav, List<WarTri> dst, out int next)
        {
            next = o;
            try
            {
                switch (type)
                {
                    case 0:
                        PushTri(dst, verts, d[o], d[o + 1], d[o + 2], Rgb(d, o + 4), Rgb(d, o + 4), Rgb(d, o + 4), null, null, 0, 0);
                        next = o + 8; return true;
                    case 1:
                        PushQuad(dst, verts, d[o], d[o + 1], d[o + 2], d[o + 3], Rgb(d, o + 4), Rgb(d, o + 4), Rgb(d, o + 4), Rgb(d, o + 4), null, null, 0, 0);
                        next = o + 8; return true;
                    case 2:
                        PushTri(dst, verts, d[o], d[o + 1], d[o + 2], Rgb(d, o + 4), Rgb(d, o + 8), Rgb(d, o + 12), null, null, 0, 0);
                        next = o + 16; return true;
                    case 3:
                        PushQuad(dst, verts, d[o], d[o + 1], d[o + 2], d[o + 3], Rgb(d, o + 4), Rgb(d, o + 8), Rgb(d, o + 12), Rgb(d, o + 16), null, null, 0, 0);
                        next = o + 20; return true;
                    case 4:
                    case 20:
                        PushTriTex(dst, verts, dav, d, o, false);
                        next = o + 24; return true;
                    case 5:
                        PushQuadTex(dst, verts, dav, d, o, false);
                        next = o + 24; return true;
                    case 6:
                    case 0x15:
                        PushTriTex(dst, verts, dav, d, o, true);
                        next = o + 40; return true;
                    case 7:
                        PushQuadTex(dst, verts, dav, d, o, true);
                        next = o + 48; return true;
                    case 8:
                    case 9:
                    case 10:
                    case 11:
                        PushQuadMat(dst, verts, dav, d[o], d[o + 1], d[o + 2], d[o + 3], BitConverter.ToUInt32(d, o + 4), SquareUv(type));
                        next = o + 48; return true;
                    case 12:
                    case 13:
                    case 14:
                    case 15:
                        PushQuadMat(dst, verts, dav, d[o], d[o + 1], d[o + 2], d[o + 3], BitConverter.ToUInt32(d, o + 4), SquareUv(type));
                        next = o + 24; return true;
                    case 0x12:
                    case 0x16:
                        PushTri(dst, verts, d[o], d[o + 1], d[o + 2], Rgb(d, o + 4), Rgb(d, o + 8), Rgb(d, o + 12), null, null, 0, 0);
                        next = o + 20; return true;
                    case 0x13:
                        PushQuad(dst, verts, d[o], d[o + 1], d[o + 2], d[o + 3], Rgb(d, o + 4), Rgb(d, o + 8), Rgb(d, o + 12), Rgb(d, o + 16), null, null, 0, 0);
                        next = o + 24; return true;
                    default:
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }

        private static float[] SquareUv(short type)
        {
            float s = (type == 8 || type == 9 || type == 12 || type == 13) ? 64f : 32f;
            if (type == 8 || type == 10 || type == 12 || type == 14)
            {
                return new float[] { 0, 0, 0, s, s, 0, s, s };
            }
            return new float[] { 0, 0, s, 0, 0, s, s, s };
        }

        private static void PushTri(List<WarTri> dst, WarVec[] verts, int i0, int i1, int i2, Color c, float[] uv, int[] argb, int tw, int th)
        {
            PushTri(dst, verts, i0, i1, i2, c, c, c, uv, argb, tw, th, false);
        }

        private static void PushTri(List<WarTri> dst, WarVec[] verts, int i0, int i1, int i2, Color c0, Color c1, Color c2, float[] uv, int[] argb, int tw, int th)
        {
            PushTri(dst, verts, i0, i1, i2, c0, c1, c2, uv, argb, tw, th, false);
        }

        private static void PushTri(List<WarTri> dst, WarVec[] verts, int i0, int i1, int i2, Color c0, Color c1, Color c2, float[] uv, int[] argb, int tw, int th, bool additive)
        {
            WarTri t = new WarTri
            {
                A = V(verts, i0),
                B = V(verts, i1),
                C = V(verts, i2),
                Ca = c0, Cb = c1, Cc = c2,
                TexArgb = argb,
                TexW = tw,
                TexH = th,
                Textured = argb != null,
                Additive = additive
            };
            if (uv != null && uv.Length >= 6)
            {
                t.Ua = uv[0]; t.Va = uv[1]; t.Ub = uv[2]; t.Vb = uv[3]; t.Uc = uv[4]; t.Vc = uv[5];
            }
            t.Ia = i0; t.Ib = i1; t.Ic = i2;
            dst.Add(t);
        }

        private static void PushQuad(List<WarTri> dst, WarVec[] verts, int i0, int i1, int i2, int i3, Color c, float[] uv, int[] argb, int tw, int th)
        {
            PushQuad(dst, verts, i0, i1, i2, i3, c, c, c, c, uv, argb, tw, th, false);
        }

        private static void PushQuad(List<WarTri> dst, WarVec[] verts, int i0, int i1, int i2, int i3, Color c0, Color c1, Color c2, Color c3, float[] uv, int[] argb, int tw, int th)
        {
            PushQuad(dst, verts, i0, i1, i2, i3, c0, c1, c2, c3, uv, argb, tw, th, false);
        }

        private static void PushQuad(List<WarTri> dst, WarVec[] verts, int i0, int i1, int i2, int i3, Color c0, Color c1, Color c2, Color c3, float[] uv, int[] argb, int tw, int th, bool additive)
        {
            if (uv == null)
            {
                PushTri(dst, verts, i0, i2, i1, c0, c2, c1, null, argb, tw, th, additive);
                PushTri(dst, verts, i1, i2, i3, c1, c2, c3, null, argb, tw, th, additive);
                return;
            }
            PushTri(dst, verts, i0, i2, i1, c0, c2, c1, new float[] { uv[0], uv[1], uv[4], uv[5], uv[2], uv[3] }, argb, tw, th, additive);
            PushTri(dst, verts, i1, i2, i3, c1, c2, c3, new float[] { uv[2], uv[3], uv[4], uv[5], uv[6], uv[7] }, argb, tw, th, additive);
        }

        private static void PushTriTex(List<WarTri> dst, WarVec[] verts, DavFile dav, byte[] d, int o, bool blended)
        {
            uint mat = BitConverter.ToUInt32(d, o + 12);
            int[] argb; int tw, th; bool additive;
            float[] uv = MapUv(dav, (int)mat, new float[] { d[o + 4], d[o + 5], d[o + 6], d[o + 7], d[o + 8], d[o + 9] }, out argb, out tw, out th, out additive);
            Color c0, c1, c2;
            if (blended)
            {
                c0 = Rgb(d, o + 16);
                c1 = Rgb(d, o + 20);
                c2 = Rgb(d, o + 24);
            }
            else
            {
                c0 = c1 = c2 = Rgb(d, o + 16);
            }
            PushTri(dst, verts, d[o], d[o + 2], d[o + 1], c0, c2, c1,
                new float[] { uv[0], uv[1], uv[4], uv[5], uv[2], uv[3] }, argb, tw, th, additive);
        }

        private static void PushQuadTex(List<WarTri> dst, WarVec[] verts, DavFile dav, byte[] d, int o, bool blended)
        {
            uint mat = BitConverter.ToUInt32(d, o + 12);
            int[] argb; int tw, th; bool additive;
            float[] uv = MapUv(dav, (int)mat, new float[] { d[o + 4], d[o + 5], d[o + 6], d[o + 7], d[o + 8], d[o + 9], d[o + 10], d[o + 11] }, out argb, out tw, out th, out additive);
            Color c0, c1, c2, c3;
            if (blended)
            {
                c0 = Rgb(d, o + 16);
                c1 = Rgb(d, o + 20);
                c2 = Rgb(d, o + 24);
                c3 = Rgb(d, o + 28);
            }
            else
            {
                c0 = c1 = c2 = c3 = Rgb(d, o + 16);
            }
            PushQuad(dst, verts, d[o], d[o + 1], d[o + 2], d[o + 3], c0, c1, c2, c3, uv, argb, tw, th, additive);
        }

        private static void PushQuadMat(List<WarTri> dst, WarVec[] verts, DavFile dav, int i0, int i1, int i2, int i3, uint mat, float[] rawUv)
        {
            int[] argb; int tw, th; bool additive;
            float[] uv = MapUv(dav, (int)mat, rawUv, out argb, out tw, out th, out additive);
            PushQuad(dst, verts, i0, i1, i2, i3, Color.FromArgb(255, 128, 128, 128), Color.FromArgb(255, 128, 128, 128), Color.FromArgb(255, 128, 128, 128), Color.FromArgb(255, 128, 128, 128), uv, argb, tw, th, additive);
        }

        private static float[] MapUv(DavFile dav, int material, float[] uv, out int[] argb, out int tw, out int th, out bool additive)
        {
            argb = null; tw = 0; th = 0; additive = false;
            if (dav == null)
            {
                return NormalizeUv(uv, 256f);
            }
            DavTexture t = dav.TextureByMaterial(material);
            if (t == null || t.Argb == null || t.Width < 1 || t.Height < 1)
            {
                return NormalizeUv(uv, 256f);
            }
            argb = t.Argb;
            tw = t.Width;
            th = t.Height;
            additive = t.Additive;
            float[] mapped = new float[uv.Length];
            for (int i = 0; i < uv.Length; i += 2)
            {
                mapped[i] = MapTexel(uv[i], tw);
                mapped[i + 1] = MapTexel(uv[i + 1], th);
            }
            return mapped;
        }

        private static float MapTexel(float coord, int size)
        {
            if (size < 1) size = 1;
            float pix = coord > 70f ? coord * size / 256f : coord;
            pix += 0.5f;
            if (pix < 0.5f) pix = 0.5f;
            float last = size - 0.5f;
            if (pix > last) pix = last;
            return pix / size;
        }

        private static float[] NormalizeUv(float[] uv, float scale)
        {
            float[] n = new float[uv.Length];
            for (int i = 0; i < uv.Length; i++) n[i] = uv[i] / scale;
            return n;
        }

        public List<WarTri> MeshFor(WarResource r)
        {
            if (r.Kind == WarKind.Scenaric && r.ModelRef >= 0 && r.ModelRef < Resources.Count)
            {
                WarResource model = Resources[r.ModelRef];
                List<WarTri> src = model.Tris;
                if (model.Kind == WarKind.AnimatedMesh && model.Anims != null && model.Anims.Count > 0 && src != null)
                {
                    src = new List<WarTri>(src);
                    WarPose.Apply(model, src, WarPose.DefaultClip(model), 0f);
                }
                return PlaceInstance(src, r);
            }
            return r.Tris;
        }

        public static bool IsWorldObject(WarResource r)
        {
            if (r == null || r.Kind != WarKind.Scenaric) return false;
            string name = Catalog.ClassName(r.ClassId);
            if (name.IndexOf("MANAGER", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (name.IndexOf("RESTRICTION", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (name.IndexOf("MAPLOCATION", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            return true;
        }

        public bool IsAnimatedWorld(WarResource r, out WarResource model)
        {
            model = null;
            if (!IsWorldObject(r) || r.ModelRef < 0 || r.ModelRef >= Resources.Count) return false;
            model = Resources[r.ModelRef];
            if (model == null || model.Kind != WarKind.AnimatedMesh) return false;
            if (model.Anims == null || model.Anims.Count == 0) return false;
            if (model.Bones == null || model.Tris == null || model.Tris.Count == 0) return false;
            return true;
        }

        internal static List<WarTri> PlaceInstance(List<WarTri> src, WarResource inst)
        {
            if (src == null || src.Count == 0) return new List<WarTri>();
            float k = (float)Math.PI / 2048f;
            float qx, qy, qz, qw;
            Xform3.EulerToQuat(inst.RotPitch * k, -inst.RotYaw * k, -inst.RotRoll * k, out qx, out qy, out qz, out qw);
            Xform3 xf = Xform3.Rt(inst.Position.X, inst.Position.Z, -inst.Position.Y, qx, qy, qz, qw);
            List<WarTri> dst = new List<WarTri>(src.Count);
            for (int i = 0; i < src.Count; i++)
            {
                WarTri t = src[i];
                t.A = XformDisplay(t.A, xf);
                t.B = XformDisplay(t.B, xf);
                t.C = XformDisplay(t.C, xf);
                dst.Add(t);
            }
            return dst;
        }

        private static WarVec XformDisplay(WarVec p, Xform3 xf)
        {
            float gx, gy, gz;
            xf.Apply(p.X, p.Z, -p.Y, out gx, out gy, out gz);
            return new WarVec(gx, -gz, gy);
        }
    }
}
