using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using SdwEditor.Formats;
using SdwEditor.Render;

namespace SdwEditor.Ui
{
    internal sealed class DavEditorControl : UserControl
    {
        private readonly ListView _list = new ListView();
        private readonly TexturePreviewControl _preview = new TexturePreviewControl();
        private readonly Label _info = new Label();
        private readonly Button _export = new Button();
        private DavFile _dav;

        public DavEditorControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Theme.Panel;
            SplitContainer split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.BackColor = Theme.Line;
            Theme.LayoutSplit(split, 0.22f);
            Controls.Add(split);

            Panel left = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Panel };
            _export.Dock = DockStyle.Bottom;
            _export.Height = 32;
            Theme.StyleButton(_export);
            _export.Click += delegate { Export(); };
            Theme.StyleList(_list);
            _list.Dock = DockStyle.Fill;
            _list.ShowGroups = true;
            _list.Columns.Add(" ", 260);
            _list.SelectedIndexChanged += delegate { ShowSelected(); };
            left.Controls.Add(_list);
            left.Controls.Add(_export);
            split.Panel1.Controls.Add(left);

            _info.Dock = DockStyle.Bottom;
            _info.Height = 28;
            _info.ForeColor = Theme.Muted;
            _info.TextAlign = ContentAlignment.MiddleLeft;
            split.Panel2.Controls.Add(_preview);
            split.Panel2.Controls.Add(_info);
            _preview.Dock = DockStyle.Fill;
            ApplyLanguage();
        }

        public void ApplyLanguage()
        {
            _export.Text = Loc.T("export_png");
            if (_list.Columns.Count > 0) _list.Columns[0].Text = Loc.T("col_texture");
            object sel = _list.SelectedItems.Count > 0 ? _list.SelectedItems[0].Tag : null;
            Bind(_dav);
            if (sel != null)
            {
                for (int i = 0; i < _list.Items.Count; i++)
                {
                    if (_list.Items[i].Tag == sel)
                    {
                        _list.Items[i].Selected = true;
                        break;
                    }
                }
            }
            _preview.Invalidate();
        }

        public void Bind(DavFile dav)
        {
            _dav = dav;
            _list.Items.Clear();
            _list.Groups.Clear();
            if (dav == null) return;
            ListViewGroup pages = new ListViewGroup(Loc.T("grp_pages"));
            ListViewGroup texs = new ListViewGroup(Loc.T("grp_textures"));
            _list.Groups.Add(pages);
            _list.Groups.Add(texs);
            foreach (DavPage p in dav.Pages)
            {
                ListViewItem it = new ListViewItem(string.Format("page {0}  {1}×{2}  {3}", p.Index, p.Width, p.Height, p.Argb5551 ? "5551" : "4444"));
                it.Tag = p;
                it.Group = pages;
                _list.Items.Add(it);
            }
            foreach (DavTexture t in dav.Textures)
            {
                ListViewItem it = new ListViewItem(string.Format("#{0:000}  {1}  {2}×{3}", t.Index, t.ExportName, t.Width, t.Height));
                it.Tag = t;
                it.Group = texs;
                _list.Items.Add(it);
            }
            if (_list.Items.Count > 0)
            {
                _list.Items[0].Selected = true;
            }
        }

        private void ShowSelected()
        {
            if (_list.SelectedItems.Count == 0) return;
            object tag = _list.SelectedItems[0].Tag;
            DavPage page = tag as DavPage;
            DavTexture tex = tag as DavTexture;
            if (page != null)
            {
                _preview.Image = page.Bitmap;
                _info.Text = Loc.F("atlas_info", page.Index, page.Width, page.Height);
            }
            else if (tex != null)
            {
                _preview.Image = tex.Bitmap;
                _info.Text = "  " + tex.ExportName + " · page " + tex.PageIndex + " @ " + tex.X + "," + tex.Y;
            }
        }

        private void Export()
        {
            if (_preview.Image == null) return;
            using (SaveFileDialog dlg = new SaveFileDialog())
            {
                dlg.Filter = "PNG|*.png";
                dlg.FileName = "texture.png";
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _preview.Image.Save(dlg.FileName, ImageFormat.Png);
                }
            }
        }
    }
}
