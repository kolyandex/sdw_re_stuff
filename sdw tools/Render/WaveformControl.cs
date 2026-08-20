using System;
using System.Drawing;
using System.Windows.Forms;

namespace SdwEditor.Render
{
    internal sealed class WaveformControl : Control
    {
        private byte[] _wav;
        private short[] _pcm = new short[0];

        public WaveformControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
        }

        public void SetWav(byte[] wav)
        {
            _wav = wav;
            _pcm = Decode(wav);
            Invalidate();
        }

        private static short[] Decode(byte[] wav)
        {
            if (wav == null || wav.Length < 44) return new short[0];
            int data = -1;
            for (int i = 12; i < wav.Length - 8; i++)
            {
                if (wav[i] == (byte)'d' && wav[i + 1] == (byte)'a' && wav[i + 2] == (byte)'t' && wav[i + 3] == (byte)'a')
                {
                    data = i + 8;
                    break;
                }
            }
            if (data < 0) return new short[0];
            int n = (wav.Length - data) / 2;
            short[] pcm = new short[n];
            for (int i = 0; i < n; i++)
            {
                pcm[i] = BitConverter.ToInt16(wav, data + i * 2);
            }
            return pcm;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Bg);
            if (_pcm.Length < 2)
            {
                using (SolidBrush b = new SolidBrush(Theme.Muted))
                {
                    g.DrawString(Loc.T("no_wave"), Theme.Ui, b, 10, 8);
                }
                return;
            }
            int mid = Height / 2;
            using (Pen p = new Pen(Theme.Accent, 1))
            {
                int w = Math.Max(1, Width);
                float step = _pcm.Length / (float)w;
                for (int x = 0; x < w; x++)
                {
                    int i = Math.Min(_pcm.Length - 1, (int)(x * step));
                    int y = mid - _pcm[i] * (Height - 8) / 2 / 32768;
                    g.DrawLine(p, x, mid, x, y);
                }
            }
            using (Pen axis = new Pen(Theme.Line))
            {
                g.DrawLine(axis, 0, mid, Width, mid);
            }
        }
    }
}
