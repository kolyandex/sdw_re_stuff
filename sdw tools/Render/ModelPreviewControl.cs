using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using SdwEditor.Formats;

namespace SdwEditor.Render
{
    internal sealed class ModelPreviewControl : Control
    {
        private List<WarTri> _tris = new List<WarTri>();
        private List<WarTri> _sky = new List<WarTri>();
        private float _skyCx, _skyCy, _skyCz;
        private float _yaw = 0.7f;
        private float _pitch = -0.35f;
        private float _dist = 8f;
        private float _eyeX;
        private float _eyeY;
        private float _eyeZ;
        private Point _last;
        private bool _lookDrag;
        private string _hint = "";
        private readonly Timer _animTimer = new Timer();
        private readonly Timer _camTimer = new Timer();
        private WarResource _anim;
        private int _clip;
        private float _time;
        private bool _paused;
        private string _baseHint = "";
        private List<WarTri> _staticGeom;
        private List<LevelPoseGroup> _levelGroups;
        private int _levelActorCount;

        private sealed class LevelPoseGroup
        {
            public WarResource Model;
            public int Clip;
            public List<WarTri> Local;
            public List<WarLevelActor> Actors;
        }
        private GlContext _gl;
        private struct GpuTex
        {
            public int Id;
            public float Su, Sv;
            public bool Blend;
        }

        private readonly Dictionary<int[], GpuTex> _tex = new Dictionary<int[], GpuTex>();
        private GpuTex _white;
        private string _glInfo = "";
        private string _glVendor = "";
        private string _glRenderer = "";
        private string _glVersion = "";
        private int _maxTexSize;
        private readonly Panel _bar = new Panel();
        private readonly Label _hud = new Label();
        private readonly CheckBox _statsToggle = new CheckBox();
        private readonly Label _statsHud = new Label();
        private readonly Timer _statsTimer = new Timer();
        private readonly StringBuilder _statsBuf = new StringBuilder(512);
        private bool _showStats;
        private long _lastPaintTicks;
        private float _fps;
        private float _frameMs;
        private float _drawMs;
        private float _swapMs;
        private float _poseMs;
        private int _statTris;
        private int _statCalls;
        private int _statBinds;
        private int _glErr;
        private bool _statsQueued;

        public ModelPreviewControl()
        {
            SetStyle(ControlStyles.Opaque | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.Selectable, true);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, false);
            BackColor = Color.FromArgb(12, 12, 16);
            TabStop = true;
            _animTimer.Interval = 16;
            _animTimer.Tick += AnimTick;
            _camTimer.Interval = 16;
            _camTimer.Tick += CamTick;
            _camTimer.Start();
            _statsTimer.Interval = 16;
            _statsTimer.Tick += StatsTick;
            _bar.Dock = DockStyle.Top;
            _bar.Height = 22;
            _bar.BackColor = Color.FromArgb(12, 12, 16);
            _statsToggle.Dock = DockStyle.Right;
            _statsToggle.Width = 72;
            _statsToggle.AutoSize = false;
            _statsToggle.TabStop = false;
            _statsToggle.FlatStyle = FlatStyle.Flat;
            _statsToggle.ForeColor = Theme.Muted;
            _statsToggle.BackColor = Color.FromArgb(12, 12, 16);
            _statsToggle.CheckedChanged += StatsToggleChanged;
            _hud.Dock = DockStyle.Fill;
            _hud.BackColor = Color.FromArgb(12, 12, 16);
            _hud.ForeColor = Theme.Accent;
            _hud.Font = Theme.Ui;
            _hud.TextAlign = ContentAlignment.MiddleLeft;
            _hud.Padding = new Padding(8, 0, 0, 0);
            _bar.Controls.Add(_hud);
            _bar.Controls.Add(_statsToggle);
            _statsHud.Visible = false;
            _statsHud.AutoSize = true;
            _statsHud.Font = Theme.MonoSmall;
            _statsHud.ForeColor = Color.FromArgb(176, 210, 150);
            _statsHud.BackColor = Color.FromArgb(16, 18, 14);
            _statsHud.Padding = new Padding(6, 4, 8, 5);
            Controls.Add(_statsHud);
            Controls.Add(_bar);
            ApplyLanguage();
        }

        public void ApplyLanguage()
        {
            _statsToggle.Text = Loc.T("stats");
            if (_anim != null || LevelAnimPlaying())
            {
                _hint = AnimHint();
            }
            else if (_tris.Count == 0 && _sky.Count == 0)
            {
                _hint = Loc.T("pick_object");
            }
            else
            {
                _hint = (_baseHint ?? "") + TexStats(_tris, _sky);
            }
            UpdateHud();
            if (_showStats) PushStats();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x20;
                cp.Style |= 0x02000000 | 0x04000000;
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            InitGl();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            DropGl();
            base.OnHandleDestroyed(e);
        }

        private void InitGl()
        {
            DropGl();
            try
            {
                _gl = new GlContext(this);
                if (!_gl.MakeCurrent()) throw new InvalidOperationException("wglMakeCurrent");
                Gl.glPixelStorei(Gl.UnpackAlignment, 1);
                Gl.glClearColor(14f / 255f, 14f / 255f, 18f / 255f, 1f);
                Gl.glEnable(Gl.DepthTest);
                Gl.glDepthFunc(Gl.Lequal);
                Gl.glEnable(Gl.AlphaTest);
                Gl.glAlphaFunc(Gl.Greater, 8f / 255f);
                Gl.glEnable(Gl.Blend);
                Gl.glEnable(Gl.Texture2D);
                Gl.glTexEnvi(Gl.TextureEnv, Gl.TextureEnvMode, Gl.Modulate);
                Gl.glHint(Gl.PerspectiveCorrectionHint, Gl.Nicest);
                _white = UploadTexture(new int[] { -1 }, 1, 1);
                _glVendor = Gl.GetString(Gl.Vendor);
                _glRenderer = Gl.GetString(Gl.Renderer);
                _glVersion = Gl.GetString(Gl.Version);
                _maxTexSize = Gl.GetInt(Gl.MaxTextureSize);
                _glInfo = _glRenderer + " · " + _glVersion;
                UpdateHud();
            }
            catch
            {
                DropGl();
            }
        }

        private void DropGl()
        {
            if (_gl != null)
            {
                try
                {
                    _gl.MakeCurrent();
                    List<int> ids = new List<int>();
                    foreach (KeyValuePair<int[], GpuTex> kv in _tex)
                    {
                        ids.Add(kv.Value.Id);
                    }
                    if (_white.Id != 0) ids.Add(_white.Id);
                    Gl.DeleteTextures(ids.ToArray());
                }
                catch
                {
                }
                _tex.Clear();
                _white = new GpuTex();
                _glInfo = "";
                _glVendor = "";
                _glRenderer = "";
                _glVersion = "";
                _maxTexSize = 0;
                _gl.Dispose();
                _gl = null;
            }
        }

        public void SetMesh(List<WarTri> tris, string hint)
        {
            StopAnim();
            _tris = tris ?? new List<WarTri>();
            _sky = new List<WarTri>();
            SmoothVertColors(_tris);
            _hint = (hint ?? "") + TexStats(_tris, _sky);
            UpdateHud();
            FrameMesh();
            Invalidate();
        }

        public void SetLevel(List<WarTri> geom, List<WarTri> sky, List<WarLevelActor> actors, string hint)
        {
            StopAnim();
            _staticGeom = geom ?? new List<WarTri>();
            SmoothVertColors(_staticGeom);
            _sky = sky ?? new List<WarTri>();
            _baseHint = hint ?? "";
            _tris = new List<WarTri>(_staticGeom.Count);
            if (actors != null && actors.Count > 0)
            {
                BuildLevelGroups(actors);
                _time = 0f;
                _paused = false;
                PoseLevelActors();
                _animTimer.Start();
                _hint = AnimHint();
            }
            else
            {
                _tris.AddRange(_staticGeom);
                _hint = _baseHint + TexStats(_tris, _sky);
            }
            UpdateHud();
            CenterSky();
            FrameMesh();
            Invalidate();
        }

        private void BuildLevelGroups(List<WarLevelActor> actors)
        {
            _levelGroups = new List<LevelPoseGroup>();
            _levelActorCount = actors.Count;
            Dictionary<int, LevelPoseGroup> map = new Dictionary<int, LevelPoseGroup>();
            for (int i = 0; i < actors.Count; i++)
            {
                WarLevelActor a = actors[i];
                if (a == null || a.Model == null || a.Inst == null) continue;
                int key = a.Model.Index * 4096 + a.Clip;
                LevelPoseGroup g;
                if (!map.TryGetValue(key, out g))
                {
                    g = new LevelPoseGroup();
                    g.Model = a.Model;
                    g.Clip = a.Clip;
                    g.Local = CloneTris(a.Model.Tris);
                    SmoothVertColors(g.Local);
                    g.Actors = new List<WarLevelActor>();
                    map[key] = g;
                    _levelGroups.Add(g);
                }
                g.Actors.Add(a);
            }
        }

        private void PoseLevelActors()
        {
            if (_staticGeom == null) _staticGeom = new List<WarTri>();
            int extra = 0;
            if (_levelGroups != null)
            {
                for (int i = 0; i < _levelGroups.Count; i++)
                {
                    extra += _levelGroups[i].Local.Count * _levelGroups[i].Actors.Count;
                }
            }
            if (_tris == null)
            {
                _tris = new List<WarTri>(_staticGeom.Count + extra);
            }
            else
            {
                _tris.Clear();
            }
            _tris.AddRange(_staticGeom);
            if (_levelGroups == null) return;
            for (int i = 0; i < _levelGroups.Count; i++)
            {
                LevelPoseGroup g = _levelGroups[i];
                WarPose.Apply(g.Model, g.Local, g.Clip, _time);
                for (int a = 0; a < g.Actors.Count; a++)
                {
                    _tris.AddRange(WarFile.PlaceInstance(g.Local, g.Actors[a].Inst));
                }
            }
        }

        private bool LevelAnimPlaying()
        {
            return _levelGroups != null && _levelGroups.Count > 0;
        }

        private static string TexStats(List<WarTri> a, List<WarTri> b)
        {
            int n = 0, tex = 0;
            CountTex(a, ref n, ref tex);
            CountTex(b, ref n, ref tex);
            return n == 0 ? "" : Loc.F("tex_stats", tex, n);
        }

        private static void CountTex(List<WarTri> list, ref int n, ref int tex)
        {
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                n++;
                if (list[i].TexArgb != null && list[i].TexW > 0) tex++;
            }
        }

        private void CenterSky()
        {
            _skyCx = 0f;
            _skyCy = 0f;
            _skyCz = 0f;
            int n = 0;
            for (int i = 0; i < _sky.Count; i++)
            {
                WarTri t = _sky[i];
                _skyCx += t.A.X + t.B.X + t.C.X;
                _skyCy += t.A.Y + t.B.Y + t.C.Y;
                _skyCz += t.A.Z + t.B.Z + t.C.Z;
                n += 3;
            }
            if (n > 0)
            {
                _skyCx /= n;
                _skyCy /= n;
                _skyCz /= n;
            }
        }

        public void PlayAnimated(WarResource r, string hint)
        {
            StopAnim();
            _baseHint = hint ?? "";
            if (r == null)
            {
                ClearPreview();
                return;
            }
            _tris = CloneTris(r.Tris);
            _sky = new List<WarTri>();
            _anim = r;
            _clip = WarPose.DefaultClip(r);
            _time = 0f;
            _paused = false;
            if (r.Anims != null && r.Anims.Count > 0 && r.Bones != null)
            {
                WarPose.Apply(r, _tris, _clip, 0f);
                _animTimer.Start();
            }
            _hint = AnimHint();
            UpdateHud();
            FrameMesh();
            Invalidate();
        }

        public void StopAnim()
        {
            _animTimer.Stop();
            _anim = null;
            _paused = false;
            _time = 0f;
            _staticGeom = null;
            _levelGroups = null;
            _levelActorCount = 0;
            _poseMs = 0f;
        }

        private static List<WarTri> CloneTris(List<WarTri> src)
        {
            if (src == null) return new List<WarTri>();
            List<WarTri> d = new List<WarTri>(src.Count);
            for (int i = 0; i < src.Count; i++)
            {
                d.Add(src[i]);
            }
            return d;
        }

        private void AnimTick(object sender, EventArgs e)
        {
            if (_paused) return;
            long t0 = Stopwatch.GetTimestamp();
            if (LevelAnimPlaying())
            {
                _time += _animTimer.Interval / 1000f;
                PoseLevelActors();
                _poseMs = MsSince(t0);
                _hint = AnimHint();
                UpdateHud();
                Invalidate();
                return;
            }
            if (_anim == null) return;
            _time += _animTimer.Interval / 1000f;
            WarPose.Apply(_anim, _tris, _clip, _time);
            _poseMs = MsSince(t0);
            _hint = AnimHint();
            UpdateHud();
            Invalidate();
        }

        private string AnimHint()
        {
            if (LevelAnimPlaying())
            {
                return Loc.F("anim_level", _baseHint, _levelActorCount, _paused ? Loc.T("paused") : Loc.T("playing"));
            }
            if (_anim == null || _anim.Anims == null || _anim.Anims.Count == 0)
            {
                return _baseHint;
            }
            if (_clip < 0 || _clip >= _anim.Anims.Count)
            {
                _clip = 0;
            }
            WarAnimClip c = _anim.Anims[_clip];
            return Loc.F("anim_clip", _baseHint, c.Name, _clip + 1, _anim.Anims.Count, _paused ? Loc.T("paused") : Loc.T("playing"));
        }

        private void UpdateHud()
        {
            _hud.Text = (_hint ?? "") + "   |   " + Loc.T("hud_help")
                + (string.IsNullOrEmpty(_glInfo) ? "" : "   |   " + _glInfo);
        }

        private void StepClip(int delta)
        {
            if (_anim == null || _anim.Anims == null || _anim.Anims.Count == 0) return;
            _clip = (_clip + delta) % _anim.Anims.Count;
            if (_clip < 0) _clip += _anim.Anims.Count;
            _time = 0f;
            WarPose.Apply(_anim, _tris, _clip, 0f);
            _hint = AnimHint();
            UpdateHud();
            Invalidate();
        }

        public void ClearPreview()
        {
            StopAnim();
            _tris = new List<WarTri>();
            _sky = new List<WarTri>();
            _hint = Loc.T("no_geom");
            UpdateHud();
            _eyeX = 0f;
            _eyeY = -8f;
            _eyeZ = 2f;
            _yaw = 0.7f;
            _pitch = -0.35f;
            Invalidate();
        }

        private void FrameMesh()
        {
            if (_tris.Count == 0)
            {
                _dist = 8f;
                return;
            }
            float minx = 1e9f, miny = 1e9f, minz = 1e9f, maxx = -1e9f, maxy = -1e9f, maxz = -1e9f;
            foreach (WarTri t in _tris)
            {
                Span(t.A, ref minx, ref miny, ref minz, ref maxx, ref maxy, ref maxz);
                Span(t.B, ref minx, ref miny, ref minz, ref maxx, ref maxy, ref maxz);
                Span(t.C, ref minx, ref miny, ref minz, ref maxx, ref maxy, ref maxz);
            }
            float dx = maxx - minx, dy = maxy - miny, dz = maxz - minz;
            _dist = Math.Max(2.5f, (float)Math.Sqrt(dx * dx + dy * dy + dz * dz) * 1.6f);
            float cx = (minx + maxx) * 0.5f;
            float cy = (miny + maxy) * 0.5f;
            float cz = (minz + maxz) * 0.5f;
            _yaw = 0.7f;
            _pitch = -0.35f;
            float fx, fy, fz, rx, ry;
            LookDir(out fx, out fy, out fz, out rx, out ry);
            _eyeX = cx - fx * _dist;
            _eyeY = cy - fy * _dist;
            _eyeZ = cz - fz * _dist;
        }

        private static void Span(WarVec v, ref float minx, ref float miny, ref float minz, ref float maxx, ref float maxy, ref float maxz)
        {
            if (v.X < minx) minx = v.X; if (v.X > maxx) maxx = v.X;
            if (v.Y < miny) miny = v.Y; if (v.Y > maxy) maxy = v.Y;
            if (v.Z < minz) minz = v.Z; if (v.Z > maxz) maxz = v.Z;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus();
            if (e.Button == MouseButtons.Middle)
            {
                _lookDrag = true;
                _last = e.Location;
                Capture = true;
                Cursor = Cursors.SizeAll;
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Middle)
            {
                _lookDrag = false;
                Capture = false;
                Cursor = Cursors.Default;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (!_lookDrag) return;
            _yaw += (e.X - _last.X) * 0.01f;
            _pitch -= (e.Y - _last.Y) * 0.01f;
            const float pi = (float)Math.PI;
            const float twoPi = pi * 2f;
            while (_yaw > pi) _yaw -= twoPi;
            while (_yaw < -pi) _yaw += twoPi;
            if (_pitch > 1.52f) _pitch = 1.52f;
            if (_pitch < -1.52f) _pitch = -1.52f;
            _last = e.Location;
            Invalidate();
        }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            if (key == Keys.Left || key == Keys.Right || key == Keys.Up || key == Keys.Down
                || key == Keys.Space || key == Keys.N || key == Keys.P
                || key == Keys.OemOpenBrackets || key == Keys.OemCloseBrackets)
            {
                return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (MoveCamera(keyData))
            {
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (MoveCamera(e.KeyData))
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }
            base.OnKeyDown(e);
        }

        public bool HandleCameraKey(Keys keyData)
        {
            return MoveCamera(keyData);
        }

        private bool MoveCamera(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            if (_anim != null || LevelAnimPlaying())
            {
                if (key == Keys.Space)
                {
                    _paused = !_paused;
                    _hint = AnimHint();
                    UpdateHud();
                    Invalidate();
                    return true;
                }
                if (_anim != null && (key == Keys.N || key == Keys.OemCloseBrackets))
                {
                    StepClip(1);
                    return true;
                }
                if (_anim != null && (key == Keys.P || key == Keys.OemOpenBrackets))
                {
                    StepClip(-1);
                    return true;
                }
            }
            switch (key)
            {
                case Keys.Up:
                case Keys.Down:
                case Keys.Left:
                case Keys.Right:
                    return true;
                default:
                    return false;
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            float fx, fy, fz, rx, ry;
            LookDir(out fx, out fy, out fz, out rx, out ry);
            float step = Math.Max(0.06f, _dist * 0.02f);
            if (e.Delta < 0) step = -step;
            _eyeX += fx * step;
            _eyeY += fy * step;
            _eyeZ += fz * step;
            Invalidate();
        }

        [DllImport("user32.dll")]
        private static extern int ValidateRect(IntPtr hWnd, IntPtr lpRect);

        protected override void WndProc(ref Message m)
        {
            if (_gl != null && (m.Msg == 0x000F || m.Msg == 0x0014))
            {
                if (m.Msg == 0x000F)
                {
                    PaintGl();
                    ValidateRect(Handle, IntPtr.Zero);
                }
                m.Result = (IntPtr)1;
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_gl == null)
            {
                e.Graphics.Clear(BackColor);
                return;
            }
            PaintGl();
        }

        private void PaintGl()
        {
            try
            {
                if (_gl == null || !_gl.MakeCurrent()) return;
                long now = Stopwatch.GetTimestamp();
                if (_lastPaintTicks != 0)
                {
                    float dt = MsSince(_lastPaintTicks);
                    if (dt > 0.05f)
                    {
                        _frameMs = dt;
                        float inst = 1000f / dt;
                        _fps = _fps < 0.1f ? inst : _fps * 0.85f + inst * 0.15f;
                    }
                }
                _lastPaintTicks = now;
                _statTris = 0;
                _statCalls = 0;
                _statBinds = 0;
                long t0 = Stopwatch.GetTimestamp();
                DrawScene();
                _drawMs = MsSince(t0);
                _glErr = Gl.glGetError();
                long t1 = Stopwatch.GetTimestamp();
                _gl.Swap();
                _swapMs = MsSince(t1);
                if (_showStats && IsHandleCreated && !_statsQueued)
                {
                    _statsQueued = true;
                    BeginInvoke(new MethodInvoker(delegate
                    {
                        _statsQueued = false;
                        if (_showStats) PushStats();
                    }));
                }
            }
            catch
            {
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            PlaceStats();
            Invalidate();
        }

        private void DrawScene()
        {
            int w = Math.Max(1, Width);
            int h = Math.Max(1, Height);
            Gl.glViewport(0, 0, w, h);
            Gl.glClear(Gl.ColorBufferBit | Gl.DepthBufferBit);

            float f = Math.Min(w, h) * 0.9f;
            double fovy = 2.0 * Math.Atan((h * 0.5) / f) * 180.0 / Math.PI;
            Gl.glMatrixMode(Gl.Projection);
            Gl.glLoadIdentity();
            Gl.gluPerspective(fovy, w / (double)h, 0.15, 4000.0);
            Gl.glMatrixMode(Gl.Modelview);
            Gl.glLoadIdentity();
            float fx, fy, fz, rx, ry;
            LookDir(out fx, out fy, out fz, out rx, out ry);
            float sp = (float)Math.Sin(_pitch);
            float cp = (float)Math.Cos(_pitch);
            float ux = (float)Math.Sin(_yaw) * sp;
            float uy = -(float)Math.Cos(_yaw) * sp;
            float uz = cp;
            Gl.gluLookAt(_eyeX, _eyeY, _eyeZ, _eyeX + fx, _eyeY + fy, _eyeZ + fz, ux, uy, uz);

            WarmTextures(_sky);
            WarmTextures(_tris);

            if (_sky.Count == 0)
            {
                DrawGrid();
            }

            Gl.glDepthMask(0);
            Gl.glDisable(Gl.Blend);
            DrawTris(_sky, true, false, true, fx, fy, fz);
            Gl.glDepthMask(1);

            Gl.glEnable(Gl.Blend);
            Gl.glBlendFunc(Gl.SrcAlpha, Gl.OneMinusSrcAlpha);
            DrawTris(_tris, false, false, false, fx, fy, fz);
            Gl.glEnable(Gl.PolygonOffsetFill);
            Gl.glPolygonOffset(-1.5f, -1.5f);
            DrawTris(_tris, false, false, true, fx, fy, fz);
            Gl.glDisable(Gl.PolygonOffsetFill);

            Gl.glEnable(Gl.Blend);
            Gl.glDepthMask(0);
            Gl.glBlendFunc(Gl.One, Gl.One);
            DrawTris(_tris, false, true, true, fx, fy, fz);
            Gl.glDepthMask(1);
            Gl.glBlendFunc(Gl.SrcAlpha, Gl.OneMinusSrcAlpha);
        }

        private void DrawGrid()
        {
            Gl.glDisable(Gl.Texture2D);
            Gl.glDisable(Gl.Blend);
            Gl.glColor4f(0.16f, 0.14f, 0.125f, 1f);
            Gl.glBegin(Gl.Lines);
            _statCalls++;
            const int n = 24;
            for (int i = -n; i <= n; i++)
            {
                Gl.glVertex3f(i, -n, 0f);
                Gl.glVertex3f(i, n, 0f);
                Gl.glVertex3f(-n, i, 0f);
                Gl.glVertex3f(n, i, 0f);
            }
            Gl.glEnd();
            Gl.glEnable(Gl.Texture2D);
        }

        private void DrawTris(List<WarTri> tris, bool sky, bool additivePass, bool texturedPass, float fx, float fy, float fz)
        {
            if (tris == null || tris.Count == 0) return;
            Gl.glEnable(Gl.Texture2D);
            float ox = sky ? (_eyeX - _skyCx) : 0f;
            float oy = sky ? (_eyeY - _skyCy) : 0f;
            float oz = sky ? (_eyeZ - _skyCz) : 0f;
            int bound = int.MinValue;
            float su = 1f, sv = 1f;
            bool open = false;
            for (int i = 0; i < tris.Count; i++)
            {
                WarTri t = tris[i];
                if (!sky && t.Additive != additivePass) continue;
                bool textured = t.TexArgb != null && t.TexW > 0;
                if (!sky && !additivePass && textured != texturedPass) continue;
                if (open && NeedsUpload(t))
                {
                    Gl.glEnd();
                    open = false;
                    bound = int.MinValue;
                }
                GpuTex gpu = TexOf(t);
                if (!open || gpu.Id != bound)
                {
                    if (open) Gl.glEnd();
                    if (!sky && !additivePass)
                    {
                        if (gpu.Blend) Gl.glEnable(Gl.Blend);
                        else Gl.glDisable(Gl.Blend);
                    }
                    Gl.glBindTexture(Gl.Texture2D, gpu.Id);
                    Gl.glBegin(Gl.Triangles);
                    _statCalls++;
                    _statBinds++;
                    bound = gpu.Id;
                    su = gpu.Su;
                    sv = gpu.Sv;
                    open = true;
                }
                float shade = 1f;
                Emit(t.A, t.Ca, t.Ua * su, t.Va * sv, textured, shade, ox, oy, oz);
                Emit(t.B, t.Cb, t.Ub * su, t.Vb * sv, textured, shade, ox, oy, oz);
                Emit(t.C, t.Cc, t.Uc * su, t.Vc * sv, textured, shade, ox, oy, oz);
                _statTris++;
            }
            if (open) Gl.glEnd();
        }

        private static void SmoothVertColors(List<WarTri> tris)
        {
            if (tris == null || tris.Count < 2) return;
            SmoothVertColors(tris, true);
            SmoothVertColors(tris, false);
        }

        private static void SmoothVertColors(List<WarTri> tris, bool textured)
        {
            Dictionary<long, int[]> acc = new Dictionary<long, int[]>();
            for (int i = 0; i < tris.Count; i++)
            {
                WarTri t = tris[i];
                if (t.Additive) continue;
                bool tex = t.TexArgb != null && t.TexW > 0;
                if (tex != textured) continue;
                AccColor(acc, t.A, t.Ca);
                AccColor(acc, t.B, t.Cb);
                AccColor(acc, t.C, t.Cc);
            }
            if (acc.Count == 0) return;
            for (int i = 0; i < tris.Count; i++)
            {
                WarTri t = tris[i];
                if (t.Additive) continue;
                bool tex = t.TexArgb != null && t.TexW > 0;
                if (tex != textured) continue;
                t.Ca = AvgColor(acc, t.A, t.Ca);
                t.Cb = AvgColor(acc, t.B, t.Cb);
                t.Cc = AvgColor(acc, t.C, t.Cc);
                tris[i] = t;
            }
        }

        private static long PosKey(WarVec v)
        {
            int x = (int)Math.Round(v.X * 50.0) + 500000;
            int y = (int)Math.Round(v.Y * 50.0) + 500000;
            int z = (int)Math.Round(v.Z * 50.0) + 500000;
            return ((long)x << 40) | ((long)y << 20) | (uint)z;
        }

        private static void AccColor(Dictionary<long, int[]> acc, WarVec p, Color c)
        {
            long k = PosKey(p);
            int[] a;
            if (!acc.TryGetValue(k, out a))
            {
                a = new int[4];
                acc[k] = a;
            }
            a[0] += c.R;
            a[1] += c.G;
            a[2] += c.B;
            a[3]++;
        }

        private static Color AvgColor(Dictionary<long, int[]> acc, WarVec p, Color fallback)
        {
            int[] a;
            if (!acc.TryGetValue(PosKey(p), out a) || a[3] < 1) return fallback;
            return Color.FromArgb(255, a[0] / a[3], a[1] / a[3], a[2] / a[3]);
        }

        private static void Emit(WarVec p, Color c, float u, float v, bool textured, float shade, float ox, float oy, float oz)
        {
            float s = textured ? 128f : 255f;
            Gl.glColor4f(c.R / s * shade, c.G / s * shade, c.B / s * shade, 1f);
            Gl.glTexCoord2f(u, v);
            Gl.glVertex3f(p.X + ox, p.Y + oy, p.Z + oz);
        }

        private bool NeedsUpload(WarTri t)
        {
            return t.TexArgb != null && t.TexW > 0 && t.TexH > 0 && !_tex.ContainsKey(t.TexArgb);
        }

        private void WarmTextures(List<WarTri> tris)
        {
            if (tris == null) return;
            for (int i = 0; i < tris.Count; i++)
            {
                TexOf(tris[i]);
            }
        }

        private GpuTex TexOf(WarTri t)
        {
            if (t.TexArgb == null || t.TexW < 1 || t.TexH < 1) return _white;
            GpuTex gpu;
            if (_tex.TryGetValue(t.TexArgb, out gpu)) return gpu;
            gpu = UploadTexture(t.TexArgb, t.TexW, t.TexH);
            _tex[t.TexArgb] = gpu;
            return gpu;
        }

        private static int NextPot(int n)
        {
            int p = 1;
            while (p < n) p <<= 1;
            if (p > 1024) p = 1024;
            return p;
        }

        private GpuTex UploadTexture(int[] argb, int w, int h)
        {
            if (w < 1) w = 1;
            if (h < 1) h = 1;
            if (w > 1024) w = 1024;
            if (h > 1024) h = 1024;
            int pw = NextPot(w);
            int ph = NextPot(h);
            byte[] rgba = new byte[pw * ph * 4];
            bool blend = false;
            if (argb != null)
            {
                for (int y = 0; y < h && y < ph; y++)
                {
                    int src = y * w;
                    int dst = y * pw * 4;
                    for (int x = 0; x < w && x < pw; x++)
                    {
                        if (src + x >= argb.Length) break;
                        int c = argb[src + x];
                        int o = dst + x * 4;
                        int a = (c >> 24) & 255;
                        if (a > 8 && a < 250) blend = true;
                        rgba[o] = (byte)((c >> 16) & 255);
                        rgba[o + 1] = (byte)((c >> 8) & 255);
                        rgba[o + 2] = (byte)(c & 255);
                        rgba[o + 3] = (byte)a;
                    }
                }
                if (w < pw)
                {
                    for (int y = 0; y < h && y < ph; y++)
                    {
                        int src = (y * pw + (w - 1)) * 4;
                        for (int x = w; x < pw; x++)
                        {
                            int dst = (y * pw + x) * 4;
                            rgba[dst] = rgba[src];
                            rgba[dst + 1] = rgba[src + 1];
                            rgba[dst + 2] = rgba[src + 2];
                            rgba[dst + 3] = rgba[src + 3];
                        }
                    }
                }
                if (h < ph)
                {
                    int last = (h - 1) * pw * 4;
                    for (int y = h; y < ph; y++)
                    {
                        Buffer.BlockCopy(rgba, last, rgba, y * pw * 4, pw * 4);
                    }
                }
            }
            int id = Gl.GenTexture();
            Gl.glBindTexture(Gl.Texture2D, id);
            Gl.glPixelStorei(Gl.UnpackAlignment, 1);
            Gl.glTexParameteri(Gl.Texture2D, Gl.TextureMinFilter, Gl.Nearest);
            Gl.glTexParameteri(Gl.Texture2D, Gl.TextureMagFilter, Gl.Nearest);
            int wrap = (w == pw && h == ph && w > 1) ? Gl.Repeat : Gl.ClampToEdge;
            Gl.glTexParameteri(Gl.Texture2D, Gl.TextureWrapS, wrap);
            Gl.glTexParameteri(Gl.Texture2D, Gl.TextureWrapT, wrap);
            IntPtr buf = Marshal.AllocHGlobal(rgba.Length);
            try
            {
                Marshal.Copy(rgba, 0, buf, rgba.Length);
                Gl.glTexImage2D(Gl.Texture2D, 0, 4, pw, ph, 0, Gl.Rgba, Gl.UnsignedByte, buf);
            }
            finally
            {
                Marshal.FreeHGlobal(buf);
            }
            GpuTex gpu = new GpuTex();
            gpu.Id = id;
            gpu.Su = w / (float)pw;
            gpu.Sv = h / (float)ph;
            gpu.Blend = blend;
            return gpu;
        }

        private void StatsToggleChanged(object sender, EventArgs e)
        {
            _showStats = _statsToggle.Checked;
            _statsHud.Visible = _showStats;
            if (_showStats)
            {
                _lastPaintTicks = 0;
                _fps = 0f;
                _statsTimer.Start();
                Invalidate();
            }
            else
            {
                _statsTimer.Stop();
                _statsHud.Text = "";
            }
        }

        private void StatsTick(object sender, EventArgs e)
        {
            if (_showStats) Invalidate();
        }

        private static float MsSince(long startTicks)
        {
            return (float)((Stopwatch.GetTimestamp() - startTicks) * 1000.0 / Stopwatch.Frequency);
        }

        private void PushStats()
        {
            int mesh = _tris == null ? 0 : _tris.Count;
            int sky = _sky == null ? 0 : _sky.Count;
            int tex = 0, untex = 0, add = 0;
            CountPasses(_tris, ref tex, ref untex, ref add);
            int groups = _levelGroups == null ? 0 : _levelGroups.Count;
            _statsBuf.Length = 0;
            _statsBuf.Append("FPS    ").Append(_fps.ToString("0.0")).AppendLine();
            _statsBuf.Append(Loc.T("stat_frame")).Append(_frameMs.ToString("0.00")).Append(" ms");
            _statsBuf.Append("   draw ").Append(_drawMs.ToString("0.00"));
            _statsBuf.Append("   pose ").Append(_poseMs.ToString("0.00"));
            _statsBuf.Append("   swap ").Append(_swapMs.ToString("0.00")).AppendLine();
            _statsBuf.Append("submit ").Append(_statTris);
            _statsBuf.Append(" tris   ").Append(_statCalls).Append(" begin");
            _statsBuf.Append("   ").Append(_statBinds).Append(" bind").AppendLine();
            _statsBuf.Append("mesh   ").Append(mesh);
            _statsBuf.Append("   sky ").Append(sky);
            _statsBuf.Append("   tex ").Append(tex);
            _statsBuf.Append("   raw ").Append(untex);
            _statsBuf.Append("   add ").Append(add).AppendLine();
            _statsBuf.Append("GPU    ").Append(_tex.Count).Append(" tex");
            _statsBuf.Append("   max2D ").Append(_maxTexSize).AppendLine();
            _statsBuf.Append(Loc.T("stat_actors")).Append(_levelActorCount);
            _statsBuf.Append("   ").Append(Loc.T("stat_groups")).Append(groups).AppendLine();
            _statsBuf.Append("view   ").Append(Width).Append("×").Append(Height);
            _statsBuf.Append("   eye ").Append(_eyeX.ToString("0.0")).Append("  ");
            _statsBuf.Append(_eyeY.ToString("0.0")).Append("  ").Append(_eyeZ.ToString("0.0")).AppendLine();
            _statsBuf.Append("GL     ").Append(_glRenderer).AppendLine();
            _statsBuf.Append("       ").Append(_glVendor);
            if (!string.IsNullOrEmpty(_glVersion))
            {
                _statsBuf.Append("  ·  ").Append(_glVersion);
            }
            _statsBuf.AppendLine();
            _statsBuf.Append("err    ").Append(GlErrName(_glErr));
            _statsBuf.Append("   GC ").Append((GC.GetTotalMemory(false) / (1024.0 * 1024.0)).ToString("0.0")).Append(" MB");
            string text = _statsBuf.ToString();
            if (_statsHud.Text != text)
            {
                _statsHud.Text = text;
            }
            PlaceStats();
        }

        private static void CountPasses(List<WarTri> tris, ref int tex, ref int untex, ref int add)
        {
            if (tris == null) return;
            for (int i = 0; i < tris.Count; i++)
            {
                WarTri t = tris[i];
                if (t.Additive) add++;
                else if (t.TexArgb != null && t.TexW > 0) tex++;
                else untex++;
            }
        }

        private static string GlErrName(int err)
        {
            if (err == 0) return "GL_NO_ERROR";
            if (err == 0x0500) return "INVALID_ENUM";
            if (err == 0x0501) return "INVALID_VALUE";
            if (err == 0x0502) return "INVALID_OPERATION";
            if (err == 0x0503) return "STACK_OVERFLOW";
            if (err == 0x0504) return "STACK_UNDERFLOW";
            if (err == 0x0505) return "OUT_OF_MEMORY";
            return "0x" + err.ToString("X");
        }

        private void PlaceStats()
        {
            if (!_statsHud.Visible) return;
            _statsHud.Left = 8;
            int top = Height - _statsHud.Height - 8;
            int min = _bar.Height + 4;
            if (top < min) top = min;
            _statsHud.Top = top;
            _statsHud.BringToFront();
        }

        private void LookDir(out float fx, out float fy, out float fz, out float rx, out float ry)
        {
            float cy = (float)Math.Cos(_yaw);
            float syw = (float)Math.Sin(_yaw);
            float cp = (float)Math.Cos(_pitch);
            float sp = (float)Math.Sin(_pitch);
            fx = -syw * cp;
            fy = cy * cp;
            fz = sp;
            rx = cy;
            ry = syw;
        }

        private void CamTick(object sender, EventArgs e)
        {
            Form form = FindForm();
            if (form == null || form != Form.ActiveForm || !form.ContainsFocus)
            {
                return;
            }
            bool up = Down(Keys.Up);
            bool dn = Down(Keys.Down);
            bool lf = Down(Keys.Left);
            bool rt = Down(Keys.Right);
            if (!up && !dn && !lf && !rt) return;
            float step = Math.Max(0.08f, Math.Min(1.6f, _dist * 0.016f));
            if ((Control.ModifierKeys & Keys.Shift) == Keys.Shift)
            {
                step *= 3f;
            }
            float hx = -((float)Math.Sin(_yaw));
            float hy = (float)Math.Cos(_yaw);
            float rx = (float)Math.Cos(_yaw);
            float ry = (float)Math.Sin(_yaw);
            if (up) { _eyeX += hx * step; _eyeY += hy * step; }
            if (dn) { _eyeX -= hx * step; _eyeY -= hy * step; }
            if (rt) { _eyeX += rx * step; _eyeY += ry * step; }
            if (lf) { _eyeX -= rx * step; _eyeY -= ry * step; }
            Invalidate();
        }

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        private static bool Down(Keys key)
        {
            return (GetKeyState((int)key) & 0x8000) != 0;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _camTimer.Stop();
                _camTimer.Dispose();
                _animTimer.Stop();
                _animTimer.Dispose();
                _statsTimer.Stop();
                _statsTimer.Dispose();
                DropGl();
            }
            base.Dispose(disposing);
        }
    }
}
