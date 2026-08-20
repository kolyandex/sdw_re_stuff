using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SdwEditor.Render
{
    internal static class Gl
    {
        public const int Texture2D = 0x0DE1;
        public const int Rgba = 0x1908;
        public const int UnsignedByte = 0x1401;
        public const int Nearest = 0x2600;
        public const int Linear = 0x2601;
        public const int NearestMipmapNearest = 0x2700;
        public const int Clamp = 0x2900;
        public const int ClampToEdge = 0x812F;
        public const int Repeat = 0x2901;
        public const int Modulate = 0x2100;
        public const int Triangles = 0x0004;
        public const int Lines = 0x0001;
        public const int ColorBufferBit = 0x4000;
        public const int DepthBufferBit = 0x0100;
        public const int DepthTest = 0x0B71;
        public const int Blend = 0x0BE2;
        public const int AlphaTest = 0x0BC0;
        public const int TextureEnv = 0x2300;
        public const int TextureEnvMode = 0x2200;
        public const int Greater = 0x0204;
        public const int Lequal = 0x0203;
        public const int SrcAlpha = 0x0302;
        public const int OneMinusSrcAlpha = 0x0303;
        public const int One = 1;
        public const int Zero = 0;
        public const int Projection = 0x1701;
        public const int Modelview = 0x1700;
        public const int UnpackAlignment = 0x0CF5;
        public const int TextureMinFilter = 0x2801;
        public const int TextureMagFilter = 0x2800;
        public const int TextureWrapS = 0x2802;
        public const int TextureWrapT = 0x2803;
        public const int PerspectiveCorrectionHint = 0x0C50;
        public const int Nicest = 0x1102;
        public const int PolygonOffsetFill = 0x8037;
        public const int Vendor = 0x1F00;
        public const int Renderer = 0x1F01;
        public const int Version = 0x1F02;
        public const int MaxTextureSize = 0x0D33;

        [DllImport("opengl32.dll", ExactSpelling = true)]
        public static extern void glViewport(int x, int y, int w, int h);
        [DllImport("opengl32.dll", ExactSpelling = true)]
        public static extern void glClearColor(float r, float g, float b, float a);
        [DllImport("opengl32.dll", ExactSpelling = true)]
        public static extern void glClear(int mask);
        [DllImport("opengl32.dll", ExactSpelling = true)]
        public static extern void glEnable(int cap);
        [DllImport("opengl32.dll", ExactSpelling = true)]
        public static extern void glDisable(int cap);
        [DllImport("opengl32.dll", ExactSpelling = true)]
        public static extern void glDepthFunc(int func);
        [DllImport("opengl32.dll", ExactSpelling = true)]
        public static extern void glDepthMask(byte flag);
        [DllImport("opengl32.dll", ExactSpelling = true)]
        public static extern void glBlendFunc(int s, int d);
        [DllImport("opengl32.dll", ExactSpelling = true)]
        public static extern void glAlphaFunc(int func, float r);
        [DllImport("opengl32.dll", ExactSpelling = true)]
        public static extern void glMatrixMode(int mode);
        [DllImport("opengl32.dll", ExactSpelling = true)]
        public static extern void glLoadIdentity();
        [DllImport("opengl32.dll", ExactSpelling = true)]
        public static extern void glHint(int t, int m);
        [DllImport("opengl32.dll", ExactSpelling = true)]
        public static extern void glPolygonOffset(float factor, float units);
        [DllImport("opengl32.dll", ExactSpelling = true)]
        public static extern void glBegin(int mode);
        [DllImport("opengl32.dll", ExactSpelling = true)]
        public static extern void glEnd();
        [DllImport("opengl32.dll", ExactSpelling = true)]
        public static extern void glVertex3f(float x, float y, float z);
        [DllImport("opengl32.dll", ExactSpelling = true)]
        public static extern void glColor4f(float r, float g, float b, float a);
        [DllImport("opengl32.dll", ExactSpelling = true)]
        public static extern void glTexCoord2f(float u, float v);
        [DllImport("opengl32.dll", ExactSpelling = true)]
        public static extern void glBindTexture(int target, int texture);
        [DllImport("opengl32.dll", ExactSpelling = true)]
        public static extern void glGenTextures(int n, IntPtr textures);
        [DllImport("opengl32.dll", ExactSpelling = true)]
        public static extern void glDeleteTextures(int n, IntPtr textures);
        [DllImport("opengl32.dll", ExactSpelling = true)]
        public static extern void glTexParameteri(int target, int pname, int param);
        [DllImport("opengl32.dll", ExactSpelling = true)]
        public static extern void glTexImage2D(int target, int level, int internalformat, int width, int height, int border, int format, int type, IntPtr pixels);
        [DllImport("opengl32.dll", ExactSpelling = true)]
        public static extern void glPixelStorei(int pname, int param);
        [DllImport("opengl32.dll", ExactSpelling = true)]
        public static extern void glTexEnvi(int target, int pname, int param);
        [DllImport("opengl32.dll", ExactSpelling = true)]
        public static extern int glGetError();
        [DllImport("opengl32.dll", ExactSpelling = true)]
        public static extern void glGetIntegerv(int pname, IntPtr parameters);
        [DllImport("opengl32.dll", ExactSpelling = true)]
        public static extern IntPtr glGetString(int name);
        [DllImport("glu32.dll", ExactSpelling = true)]
        public static extern void gluPerspective(double fovy, double aspect, double zNear, double zFar);
        [DllImport("glu32.dll", ExactSpelling = true)]
        public static extern void gluLookAt(double eyex, double eyey, double eyez, double cx, double cy, double cz, double ux, double uy, double uz);

        public static int GenTexture()
        {
            int[] ids = new int[1];
            GCHandle pin = GCHandle.Alloc(ids, GCHandleType.Pinned);
            try
            {
                glGenTextures(1, pin.AddrOfPinnedObject());
            }
            finally
            {
                pin.Free();
            }
            return ids[0];
        }

        public static void DeleteTextures(int[] ids)
        {
            if (ids == null || ids.Length == 0) return;
            GCHandle pin = GCHandle.Alloc(ids, GCHandleType.Pinned);
            try
            {
                glDeleteTextures(ids.Length, pin.AddrOfPinnedObject());
            }
            finally
            {
                pin.Free();
            }
        }

        public static string GetString(int name)
        {
            IntPtr p = glGetString(name);
            return p == IntPtr.Zero ? "" : Marshal.PtrToStringAnsi(p);
        }

        public static int GetInt(int name)
        {
            int[] v = new int[1];
            GCHandle pin = GCHandle.Alloc(v, GCHandleType.Pinned);
            try
            {
                glGetIntegerv(name, pin.AddrOfPinnedObject());
            }
            finally
            {
                pin.Free();
            }
            return v[0];
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PixelFormatDescriptor
    {
        public ushort nSize, nVersion;
        public uint dwFlags;
        public byte iPixelType, cColorBits, cRedBits, cRedShift, cGreenBits, cGreenShift, cBlueBits, cBlueShift, cAlphaBits, cAlphaShift;
        public byte cAccumBits, cAccumRedBits, cAccumGreenBits, cAccumBlueBits, cAccumAlphaBits;
        public byte cDepthBits, cStencilBits, cAuxBuffers, iLayerType, bReserved;
        public uint dwLayerMask, dwVisibleMask, dwDamageMask;
    }

    internal sealed class GlContext : IDisposable
    {
        private IntPtr _dc;
        private IntPtr _rc;
        private readonly Control _host;

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDc);
        [DllImport("gdi32.dll")]
        private static extern int ChoosePixelFormat(IntPtr hdc, ref PixelFormatDescriptor pfd);
        [DllImport("gdi32.dll")]
        private static extern int SetPixelFormat(IntPtr hdc, int format, ref PixelFormatDescriptor pfd);
        [DllImport("gdi32.dll")]
        private static extern int SwapBuffers(IntPtr hdc);
        [DllImport("opengl32.dll")]
        private static extern IntPtr wglCreateContext(IntPtr hdc);
        [DllImport("opengl32.dll")]
        private static extern int wglMakeCurrent(IntPtr hdc, IntPtr hglrc);
        [DllImport("opengl32.dll")]
        private static extern int wglDeleteContext(IntPtr hglrc);

        public GlContext(Control host)
        {
            _host = host;
            _dc = GetDC(host.Handle);
            PixelFormatDescriptor pfd = new PixelFormatDescriptor();
            pfd.nSize = 40;
            pfd.nVersion = 1;
            pfd.dwFlags = 0x25;
            pfd.iPixelType = 0;
            pfd.cColorBits = 24;
            pfd.cDepthBits = 24;
            pfd.iLayerType = 0;
            int fmt = ChoosePixelFormat(_dc, ref pfd);
            if (fmt == 0) throw new InvalidOperationException("ChoosePixelFormat");
            if (SetPixelFormat(_dc, fmt, ref pfd) == 0) throw new InvalidOperationException("SetPixelFormat");
            _rc = wglCreateContext(_dc);
            if (_rc == IntPtr.Zero) throw new InvalidOperationException("wglCreateContext");
            MakeCurrent();
        }

        public bool MakeCurrent()
        {
            return wglMakeCurrent(_dc, _rc) != 0;
        }

        public void Swap()
        {
            SwapBuffers(_dc);
        }

        public void Dispose()
        {
            wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
            if (_rc != IntPtr.Zero)
            {
                wglDeleteContext(_rc);
                _rc = IntPtr.Zero;
            }
            if (_dc != IntPtr.Zero)
            {
                ReleaseDC(_host.Handle, _dc);
                _dc = IntPtr.Zero;
            }
        }
    }
}
