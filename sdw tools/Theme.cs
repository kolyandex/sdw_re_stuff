using System;
using System.Drawing;
using System.Windows.Forms;

namespace SdwEditor
{
    internal static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(18, 18, 22);
        public static readonly Color Panel = Color.FromArgb(28, 28, 34);
        public static readonly Color PanelAlt = Color.FromArgb(36, 36, 44);
        public static readonly Color Line = Color.FromArgb(58, 52, 44);
        public static readonly Color Text = Color.FromArgb(232, 226, 214);
        public static readonly Color Muted = Color.FromArgb(150, 142, 128);
        public static readonly Color Accent = Color.FromArgb(248, 176, 96);
        public static readonly Color AccentDim = Color.FromArgb(120, 72, 28);
        public static readonly Color Danger = Color.FromArgb(220, 90, 70);
        public static readonly Color Fog = Color.FromArgb(248, 176, 96);

        public static readonly Font Ui = new Font("Segoe UI", 9f);
        public static readonly Font UiBold = new Font("Segoe UI", 9f, FontStyle.Bold);
        public static readonly Font Title = new Font("Segoe UI", 14f, FontStyle.Bold);
        public static readonly Font Mono = new Font("Consolas", 9f);
        public static readonly Font MonoSmall = new Font("Consolas", 7.5f);

        public static void Style(Control root)
        {
            root.BackColor = Panel;
            root.ForeColor = Text;
            root.Font = Ui;
            foreach (Control child in root.Controls)
            {
                Style(child);
            }
        }

        public static void StyleButton(Button b)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = Line;
            b.FlatAppearance.MouseOverBackColor = AccentDim;
            b.BackColor = PanelAlt;
            b.ForeColor = Text;
            b.Cursor = Cursors.Hand;
        }

        public static void StyleList(ListView lv)
        {
            lv.BackColor = Bg;
            lv.ForeColor = Text;
            lv.BorderStyle = BorderStyle.None;
            lv.FullRowSelect = true;
            lv.HideSelection = false;
            lv.View = View.Details;
            lv.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        }

        public static void StyleTree(TreeView tv)
        {
            tv.BackColor = Bg;
            tv.ForeColor = Text;
            tv.BorderStyle = BorderStyle.None;
            tv.HideSelection = false;
            tv.ShowLines = false;
            tv.ShowPlusMinus = true;
            tv.HotTracking = true;
        }

        public static void StyleGrid(DataGridView g)
        {
            g.BackgroundColor = Bg;
            g.BorderStyle = BorderStyle.None;
            g.EnableHeadersVisualStyles = false;
            g.ColumnHeadersDefaultCellStyle.BackColor = PanelAlt;
            g.ColumnHeadersDefaultCellStyle.ForeColor = Accent;
            g.ColumnHeadersDefaultCellStyle.SelectionBackColor = PanelAlt;
            g.DefaultCellStyle.BackColor = Bg;
            g.DefaultCellStyle.ForeColor = Text;
            g.DefaultCellStyle.SelectionBackColor = AccentDim;
            g.DefaultCellStyle.SelectionForeColor = Color.White;
            g.GridColor = Line;
            g.RowHeadersVisible = false;
            g.AllowUserToAddRows = false;
            g.AllowUserToResizeRows = false;
        }

        public static void EnableDoubleBuffer(Control c)
        {
            typeof(Control).GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(c, true, null);
        }

        /// <summary>
        /// Ставит долю Panel1 только когда сплиттер уже имеет нормальный размер.
        /// Не трогает MinSize — иначе WinForms кидает InvalidOperationException.
        /// </summary>
        public static void LayoutSplit(SplitContainer split, float panel1Ratio)
        {
            bool applied = false;
            EventHandler apply = delegate
            {
                if (applied || !split.IsHandleCreated)
                {
                    return;
                }

                int total = split.Orientation == Orientation.Vertical ? split.Width : split.Height;
                if (total < 300)
                {
                    return;
                }

                int min = split.Panel1MinSize;
                int max = total - split.Panel2MinSize - split.SplitterWidth - 2;
                if (max < min)
                {
                    return;
                }

                int distance = (int)(total * panel1Ratio);
                if (distance < min) distance = min;
                if (distance > max) distance = max;

                if (split.SplitterDistance == distance)
                {
                    applied = true;
                    return;
                }

                split.SplitterDistance = distance;
                applied = true;
            };

            split.HandleCreated += delegate { apply(null, EventArgs.Empty); };
            split.SizeChanged += delegate { apply(null, EventArgs.Empty); };
            split.SplitterMoved += delegate
            {
                if (applied)
                {
                    return;
                }
                if (split.Width > 300 && split.Height > 200)
                {
                    applied = true;
                }
            };
        }
    }
}
