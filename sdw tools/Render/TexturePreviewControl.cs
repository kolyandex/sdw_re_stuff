using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SdwEditor.Render
{
    internal sealed class TexturePreviewControl : Control
    {
        private Image _image;
        private float _zoom = 2f;
        private PointF _pan;
        private Point _last;
        private bool _drag;

        public TexturePreviewControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
        }

        public Image Image
        {
            get { return _image; }
            set
            {
                _image = value;
                _zoom = 2f;
                _pan = PointF.Empty;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            _drag = true;
            _last = e.Location;
            Focus();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _drag = false;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (!_drag) return;
            _pan.X += e.X - _last.X;
            _pan.Y += e.Y - _last.Y;
            _last = e.Location;
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            _zoom *= e.Delta > 0 ? 1.25f : 0.8f;
            _zoom = Math.Max(0.25f, Math.Min(32f, _zoom));
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            DrawChecker(g);
            if (_image == null)
            {
                using (SolidBrush b = new SolidBrush(Theme.Muted))
                {
                    g.DrawString(Loc.T("no_image"), Theme.Ui, b, 12, 12);
                }
                return;
            }
            g.CompositingMode = CompositingMode.SourceOver;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            float iw = _image.Width * _zoom;
            float ih = _image.Height * _zoom;
            float x = (Width - iw) / 2f + _pan.X;
            float y = (Height - ih) / 2f + _pan.Y;
            g.DrawImage(_image, x, y, iw, ih);
            using (Pen p = new Pen(Theme.Accent, 1))
            {
                g.DrawRectangle(p, x, y, iw, ih);
            }
            using (SolidBrush b = new SolidBrush(Theme.Accent))
            {
                g.DrawString(string.Format("{0}×{1}  ×{2:0.0}", _image.Width, _image.Height, _zoom), Theme.Ui, b, 10, 8);
            }
        }

        private void DrawChecker(Graphics g)
        {
            int s = 8;
            using (SolidBrush a = new SolidBrush(Color.FromArgb(28, 28, 32)))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(40, 40, 46)))
            {
                for (int y = 0; y < Height; y += s)
                {
                    for (int x = 0; x < Width; x += s)
                    {
                        g.FillRectangle(((x / s + y / s) & 1) == 0 ? a : b, x, y, s, s);
                    }
                }
            }
        }
    }
}
