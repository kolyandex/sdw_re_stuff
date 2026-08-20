using System;
using System.Collections.Generic;

namespace SdwEditor.Formats
{
    internal struct WarBone
    {
        public int Parent;
        public float Tx, Ty, Tz;
    }

    internal struct WarBonePose
    {
        public float Tx, Ty, Tz;
        public float Rx, Ry, Rz;
        public float Sx, Sy, Sz;
        public float Qx, Qy, Qz, Qw;
    }

    internal sealed class WarAnimClip
    {
        public string Name;
        public float Duration;
        public float[] Times;
        public WarBonePose[][] Frames;
    }

    internal sealed class WarLevelActor
    {
        public WarResource Model;
        public WarResource Inst;
        public int Clip;
    }

    internal struct Xform3
    {
        public float Xx, Xy, Xz, Ox;
        public float Yx, Yy, Yz, Oy;
        public float Zx, Zy, Zz, Oz;

        public static Xform3 Rt(float tx, float ty, float tz, float qx, float qy, float qz, float qw)
        {
            float xx = qx * qx, yy = qy * qy, zz = qz * qz;
            float xy = qx * qy, xz = qx * qz, yz = qy * qz;
            float wx = qw * qx, wy = qw * qy, wz = qw * qz;
            Xform3 m = new Xform3();
            m.Xx = 1f - 2f * (yy + zz); m.Xy = 2f * (xy - wz); m.Xz = 2f * (xz + wy); m.Ox = tx;
            m.Yx = 2f * (xy + wz); m.Yy = 1f - 2f * (xx + zz); m.Yz = 2f * (yz - wx); m.Oy = ty;
            m.Zx = 2f * (xz - wy); m.Zy = 2f * (yz + wx); m.Zz = 1f - 2f * (xx + yy); m.Oz = tz;
            return m;
        }

        public static void EulerToQuat(float rx, float ry, float rz, out float qx, out float qy, out float qz, out float qw)
        {
            float cp = (float)Math.Cos(rx * 0.5f), sp = (float)Math.Sin(rx * 0.5f);
            float cy = (float)Math.Cos(ry * 0.5f), syw = (float)Math.Sin(ry * 0.5f);
            float cr = (float)Math.Cos(rz * 0.5f), sr = (float)Math.Sin(rz * 0.5f);
            qx = sp * cy * cr + cp * syw * sr;
            qy = cp * syw * cr - sp * cy * sr;
            qz = cp * cy * sr - sp * syw * cr;
            qw = cp * cy * cr + sp * syw * sr;
        }

        public static Xform3 UniformScale(float s)
        {
            Xform3 m = new Xform3();
            m.Xx = s;
            m.Yy = s;
            m.Zz = s;
            return m;
        }

        public static Xform3 Mul(Xform3 a, Xform3 b)
        {
            Xform3 r = new Xform3();
            r.Xx = a.Xx * b.Xx + a.Xy * b.Yx + a.Xz * b.Zx;
            r.Xy = a.Xx * b.Xy + a.Xy * b.Yy + a.Xz * b.Zy;
            r.Xz = a.Xx * b.Xz + a.Xy * b.Yz + a.Xz * b.Zz;
            r.Ox = a.Xx * b.Ox + a.Xy * b.Oy + a.Xz * b.Oz + a.Ox;
            r.Yx = a.Yx * b.Xx + a.Yy * b.Yx + a.Yz * b.Zx;
            r.Yy = a.Yx * b.Xy + a.Yy * b.Yy + a.Yz * b.Zy;
            r.Yz = a.Yx * b.Xz + a.Yy * b.Yz + a.Yz * b.Zz;
            r.Oy = a.Yx * b.Ox + a.Yy * b.Oy + a.Yz * b.Oz + a.Oy;
            r.Zx = a.Zx * b.Xx + a.Zy * b.Yx + a.Zz * b.Zx;
            r.Zy = a.Zx * b.Xy + a.Zy * b.Yy + a.Zz * b.Zy;
            r.Zz = a.Zx * b.Xz + a.Zy * b.Yz + a.Zz * b.Zz;
            r.Oz = a.Zx * b.Ox + a.Zy * b.Oy + a.Zz * b.Oz + a.Oz;
            return r;
        }

        public void Apply(float x, float y, float z, out float ox, out float oy, out float oz)
        {
            ox = Xx * x + Xy * y + Xz * z + Ox;
            oy = Yx * x + Yy * y + Yz * z + Oy;
            oz = Zx * x + Zy * y + Zz * z + Oz;
        }
    }

    internal static class WarPose
    {
        public static int DefaultClip(WarResource r)
        {
            if (r == null || r.Anims == null || r.Anims.Count == 0) return 0;
            int standExact = -1;
            int standAny = -1;
            int moving = -1;
            int any = -1;
            for (int i = 0; i < r.Anims.Count; i++)
            {
                WarAnimClip c = r.Anims[i];
                if (c == null || c.Frames == null || c.Frames.Length == 0) continue;
                if (any < 0) any = i;
                if (moving < 0 && c.Frames.Length >= 2) moving = i;
                string n = c.Name;
                if (n == null || n.Length < 5) continue;
                if (n.StartsWith("stand", StringComparison.OrdinalIgnoreCase))
                {
                    if (standAny < 0) standAny = i;
                    if (n.Equals("stand", StringComparison.OrdinalIgnoreCase)) standExact = i;
                }
            }
            if (standExact >= 0) return standExact;
            if (standAny >= 0) return standAny;
            if (moving >= 0) return moving;
            if (any >= 0) return any;
            return 0;
        }

        public static void Apply(WarResource r, List<WarTri> tris, int clip, float time)
        {
            if (r == null || tris == null || r.Bones == null || r.Lx == null || r.VertBone == null)
            {
                return;
            }
            int n = r.Bones.Length;
            WarBonePose[] pose = Sample(r, clip, time);
            if (pose == null || pose.Length != n)
            {
                return;
            }
            Xform3[] world = new Xform3[n];
            bool[] ready = new bool[n];
            int left = n;
            for (int guard = 0; guard < n + 2 && left > 0; guard++)
            {
                for (int b = 0; b < n; b++)
                {
                    if (ready[b]) continue;
                    WarBonePose p = pose[b];
                    Xform3 local = Xform3.Rt(p.Tx, p.Ty, p.Tz, p.Qx, p.Qy, p.Qz, p.Qw);
                    int par = r.Bones[b].Parent;
                    if (par == 0xFFFF || par < 0 || par >= n)
                    {
                        world[b] = Xform3.Mul(local, Xform3.UniformScale(0.125f));
                        ready[b] = true;
                        left--;
                    }
                    else if (ready[par])
                    {
                        world[b] = Xform3.Mul(world[par], local);
                        ready[b] = true;
                        left--;
                    }
                }
            }

            int vc = r.Lx.Length;
            WarVec[] posed = new WarVec[vc];
            for (int i = 0; i < vc; i++)
            {
                int b = r.VertBone[i];
                if (b < 0 || b >= n)
                {
                    posed[i] = new WarVec(r.Lx[i], -r.Lz[i], r.Ly[i]);
                    continue;
                }
                float gx, gy, gz;
                float sx = pose[b].Sx, sy = pose[b].Sy, sz = pose[b].Sz;
                world[b].Apply(r.Lx[i] * sx, r.Ly[i] * sy, r.Lz[i] * sz, out gx, out gy, out gz);
                posed[i] = new WarVec(gx, -gz, gy);
            }

            for (int i = 0; i < tris.Count; i++)
            {
                WarTri t = tris[i];
                t.A = At(posed, t.Ia);
                t.B = At(posed, t.Ib);
                t.C = At(posed, t.Ic);
                tris[i] = t;
            }
        }

        private static WarVec At(WarVec[] v, int i)
        {
            if (i < 0 || i >= v.Length) return new WarVec();
            return v[i];
        }

        private static WarBonePose[] Sample(WarResource r, int clip, float time)
        {
            if (r.Anims == null || r.Anims.Count == 0)
            {
                return Rest(r);
            }
            if (clip < 0 || clip >= r.Anims.Count)
            {
                clip = 0;
            }
            WarAnimClip a = r.Anims[clip];
            if (a == null || a.Frames == null || a.Frames.Length == 0)
            {
                return Rest(r);
            }
            if (a.Frames.Length == 1 || a.Duration <= 1e-4f)
            {
                return a.Frames[0];
            }
            float t = time % a.Duration;
            if (t < 0f) t += a.Duration;
            int i = 0;
            while (i + 1 < a.Times.Length && a.Times[i + 1] <= t)
            {
                i++;
            }
            int j = i + 1;
            if (j >= a.Frames.Length)
            {
                j = 0;
            }
            float t0 = a.Times[i];
            float t1 = j == 0 ? a.Duration : a.Times[j];
            float span = t1 - t0;
            float u = span < 1e-4f ? 0f : (t - t0) / span;
            if (u < 0f) u = 0f;
            if (u > 1f) u = 1f;
            return Mix(a.Frames[i], a.Frames[j], u);
        }

        private static WarBonePose[] Rest(WarResource r)
        {
            WarBonePose[] p = new WarBonePose[r.Bones.Length];
            for (int i = 0; i < p.Length; i++)
            {
                p[i] = RestBone(r.Bones[i]);
            }
            return p;
        }

        public static WarBonePose RestBone(WarBone b)
        {
            WarBonePose p = new WarBonePose();
            p.Tx = b.Tx; p.Ty = b.Ty; p.Tz = b.Tz;
            p.Sx = 1f; p.Sy = 1f; p.Sz = 1f;
            p.Qw = 1f;
            return p;
        }

        public static void BakeQuat(ref WarBonePose p)
        {
            Xform3.EulerToQuat(p.Rx, p.Ry, p.Rz, out p.Qx, out p.Qy, out p.Qz, out p.Qw);
        }

        private static WarBonePose[] Mix(WarBonePose[] a, WarBonePose[] b, float u)
        {
            WarBonePose[] r = new WarBonePose[a.Length];
            float v = 1f - u;
            for (int i = 0; i < a.Length; i++)
            {
                WarBonePose p = new WarBonePose();
                p.Tx = a[i].Tx * v + b[i].Tx * u;
                p.Ty = a[i].Ty * v + b[i].Ty * u;
                p.Tz = a[i].Tz * v + b[i].Tz * u;
                p.Rx = LerpAng(a[i].Rx, b[i].Rx, u);
                p.Ry = LerpAng(a[i].Ry, b[i].Ry, u);
                p.Rz = LerpAng(a[i].Rz, b[i].Rz, u);
                p.Sx = a[i].Sx * v + b[i].Sx * u;
                p.Sy = a[i].Sy * v + b[i].Sy * u;
                p.Sz = a[i].Sz * v + b[i].Sz * u;
                WarPose.BakeQuat(ref p);
                r[i] = p;
            }
            return r;
        }

        private static float LerpAng(float a, float b, float u)
        {
            float d = b - a;
            float twopi = (float)(Math.PI * 2.0);
            while (d > (float)Math.PI) d -= twopi;
            while (d < -(float)Math.PI) d += twopi;
            return a + d * u;
        }
    }
}
