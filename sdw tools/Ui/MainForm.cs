using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using SdwEditor.Formats;

namespace SdwEditor.Ui
{
    internal sealed class MainForm : Form
    {
        private readonly TreeView _levels = new TreeView();
        private readonly TabControl _tabs = new TabControl();
        private readonly DavEditorControl _dav = new DavEditorControl();
        private readonly WarEditorControl _war = new WarEditorControl();
        private readonly MltEditorControl _mlt = new MltEditorControl();
        private readonly SndEditorControl _snd = new SndEditorControl();
        private readonly Label _status = new Label();
        private readonly Label _title = new Label();
        private readonly Label _side = new Label();
        private readonly Label _langLabel = new Label();
        private readonly Button _open = new Button();
        private readonly ComboBox _uiLang = new ComboBox();
        private LevelSet _set;
        private string _levelsRoot = @"E:\SDW (F)\Levels";
        private bool _suppressLang;

        public MainForm()
        {
            Width = 1440;
            Height = 900;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.Ui;
            MinimumSize = new Size(960, 600);

            Panel header = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Color.FromArgb(12, 12, 14) };
            _title.Text = "SDW  ·  WORKSHOP";
            _title.Font = Theme.Title;
            _title.ForeColor = Theme.Accent;
            _title.AutoSize = true;
            _title.Location = new Point(16, 14);

            _open.Width = 150;
            _open.Height = 30;
            _open.Left = 360;
            _open.Top = 12;
            Theme.StyleButton(_open);
            _open.Click += delegate { PickRoot(); };

            Panel langPanel = new Panel { Dock = DockStyle.Right, Width = 220, BackColor = Color.FromArgb(12, 12, 14) };
            _langLabel.AutoSize = true;
            _langLabel.ForeColor = Theme.Muted;
            _langLabel.Location = new Point(8, 18);
            _uiLang.DropDownStyle = ComboBoxStyle.DropDownList;
            _uiLang.Width = 118;
            _uiLang.Left = 90;
            _uiLang.Top = 14;
            _uiLang.FlatStyle = FlatStyle.Flat;
            _uiLang.BackColor = Theme.PanelAlt;
            _uiLang.ForeColor = Theme.Text;
            _uiLang.Items.Add("Русский");
            _uiLang.Items.Add("English");
            _uiLang.SelectedIndexChanged += UiLangChanged;
            langPanel.Controls.Add(_langLabel);
            langPanel.Controls.Add(_uiLang);

            header.Controls.Add(_title);
            header.Controls.Add(_open);
            header.Controls.Add(langPanel);

            _status.Dock = DockStyle.Bottom;
            _status.Height = 24;
            _status.BackColor = Color.FromArgb(12, 12, 14);
            _status.ForeColor = Theme.Muted;
            _status.TextAlign = ContentAlignment.MiddleLeft;

            SplitContainer split = new SplitContainer { Dock = DockStyle.Fill, BackColor = Theme.Line };
            Theme.LayoutSplit(split, 0.14f);
            Theme.StyleTree(_levels);
            _levels.Dock = DockStyle.Fill;
            _levels.AfterSelect += LevelsOnAfterSelect;
            _side.Dock = DockStyle.Top;
            _side.Height = 28;
            _side.ForeColor = Theme.Accent;
            _side.BackColor = Theme.Panel;
            _side.TextAlign = ContentAlignment.MiddleLeft;
            Panel left = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Panel };
            left.Controls.Add(_levels);
            left.Controls.Add(_side);
            split.Panel1.Controls.Add(left);

            _tabs.Dock = DockStyle.Fill;
            _tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
            _tabs.SizeMode = TabSizeMode.Fixed;
            _tabs.ItemSize = new Size(118, 32);
            _tabs.Padding = new Point(12, 4);
            _tabs.DrawItem += TabsOnDrawItem;
            AddTab(_dav);
            AddTab(_war);
            AddTab(_mlt);
            AddTab(_snd);
            split.Panel2.Controls.Add(_tabs);

            Controls.Add(split);
            Controls.Add(_status);
            Controls.Add(header);

            _suppressLang = true;
            _uiLang.SelectedIndex = Loc.Id;
            _suppressLang = false;
            ApplyLanguage();
            Loc.Changed += delegate { ApplyLanguage(); };

            Load += delegate { FillLevels(); };
            FormClosed += delegate { DisposeSet(); };
        }

        private void UiLangChanged(object sender, EventArgs e)
        {
            if (_suppressLang) return;
            Loc.Set(_uiLang.SelectedIndex);
        }

        private void ApplyLanguage()
        {
            Text = Loc.T("app_title");
            _open.Text = Loc.T("open_folder");
            _side.Text = Loc.T("levels_header");
            _langLabel.Text = Loc.T("ui_language");
            _langLabel.Left = 8;
            _uiLang.Left = _langLabel.Right + 8;
            if (_tabs.TabPages.Count >= 4)
            {
                _tabs.TabPages[0].Text = Loc.T("tab_dav");
                _tabs.TabPages[1].Text = Loc.T("tab_war");
                _tabs.TabPages[2].Text = Loc.T("tab_mlt");
                _tabs.TabPages[3].Text = Loc.T("tab_snd");
            }
            _tabs.Invalidate();
            _dav.ApplyLanguage();
            _war.ApplyLanguage();
            _mlt.ApplyLanguage();
            _snd.ApplyLanguage();
            RefreshStatus();
        }

        private void AddTab(Control editor)
        {
            TabPage page = new TabPage(" ");
            page.BackColor = Theme.Panel;
            editor.Dock = DockStyle.Fill;
            page.Controls.Add(editor);
            _tabs.TabPages.Add(page);
        }

        private void TabsOnDrawItem(object sender, DrawItemEventArgs e)
        {
            TabPage page = _tabs.TabPages[e.Index];
            bool sel = e.Index == _tabs.SelectedIndex;
            using (SolidBrush bg = new SolidBrush(sel ? Theme.AccentDim : Theme.Panel))
            {
                e.Graphics.FillRectangle(bg, e.Bounds);
            }
            TextRenderer.DrawText(e.Graphics, page.Text, sel ? Theme.UiBold : Theme.Ui, e.Bounds,
                sel ? Theme.Accent : Theme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private void PickRoot()
        {
            using (FolderBrowserDialog dlg = new FolderBrowserDialog())
            {
                dlg.SelectedPath = _levelsRoot;
                dlg.Description = Loc.T("folder_levels");
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _levelsRoot = dlg.SelectedPath;
                    FillLevels();
                }
            }
        }

        private void FillLevels()
        {
            _levels.Nodes.Clear();
            if (!Directory.Exists(_levelsRoot))
            {
                _status.Text = Loc.F("folder_missing", _levelsRoot);
                return;
            }
            foreach (string dir in Directory.GetDirectories(_levelsRoot))
            {
                if (Directory.GetFiles(dir, "*.dav").Length + Directory.GetFiles(dir, "*.war").Length
                    + Directory.GetFiles(dir, "*.mlt").Length + Directory.GetFiles(dir, "*.snd").Length == 0)
                {
                    continue;
                }
                _levels.Nodes.Add(new TreeNode(Path.GetFileName(dir)) { Tag = dir });
            }
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            if (_set != null)
            {
                _status.Text = Loc.F("status_loaded",
                    _set.Name,
                    _set.Dav == null ? 0 : _set.Dav.Textures.Count,
                    _set.War == null ? 0 : _set.War.Resources.Count,
                    _set.Mlt == null ? "-" : Loc.F("langs_short", _set.Mlt.LanguageCount),
                    _set.Snd == null ? 0 : _set.Snd.Entries.Count);
                return;
            }
            if (!Directory.Exists(_levelsRoot))
            {
                _status.Text = Loc.F("folder_missing", _levelsRoot);
                return;
            }
            if (_levels.Nodes.Count == 0)
            {
                _status.Text = Loc.T("status_pick");
                return;
            }
            _status.Text = Loc.F("levels_in", _levels.Nodes.Count, _levelsRoot);
        }

        private void LevelsOnAfterSelect(object sender, TreeViewEventArgs e)
        {
            string folder = e.Node == null ? null : e.Node.Tag as string;
            if (string.IsNullOrEmpty(folder)) return;
            Cursor = Cursors.WaitCursor;
            try
            {
                DisposeSet();
                _set = LevelSet.Load(folder);
                _dav.Bind(_set.Dav);
                _war.Bind(_set.War);
                _mlt.Bind(_set.Mlt);
                _snd.Bind(_set.Snd);
                _title.Text = "SDW  ·  " + _set.Name.ToUpperInvariant();
                RefreshStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Loc.T("open_level_fail"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void DisposeSet()
        {
            if (_set != null)
            {
                _set.Dispose();
                _set = null;
            }
        }
    }
}
