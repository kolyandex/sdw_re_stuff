using System;
using System.Windows.Forms;
using SdwEditor.Formats;

namespace SdwEditor.Ui
{
    internal sealed class MltEditorControl : UserControl
    {
        private readonly ComboBox _lang = new ComboBox();
        private readonly ComboBox _section = new ComboBox();
        private readonly DataGridView _grid = new DataGridView();
        private readonly Label _lblLang = new Label();
        private readonly Label _lblSection = new Label();
        private readonly Button _save = new Button();
        private MltFile _mlt;
        private bool _suppress;

        public MltEditorControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Theme.Panel;
            Panel top = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Theme.Panel };
            _lblLang.AutoSize = true;
            _lblLang.Left = 8;
            _lblLang.Top = 11;
            _lblLang.ForeColor = Theme.Muted;
            _lang.Top = 8;
            _lang.Width = 80;
            _lang.DropDownStyle = ComboBoxStyle.DropDownList;
            _lblSection.AutoSize = true;
            _lblSection.Top = 11;
            _lblSection.ForeColor = Theme.Muted;
            _section.Top = 8;
            _section.Width = 90;
            _section.DropDownStyle = ComboBoxStyle.DropDownList;
            _save.Top = 6;
            _save.Width = 150;
            _save.Height = 28;
            Theme.StyleButton(_save);
            _save.Click += delegate { Save(); };
            _lang.SelectedIndexChanged += delegate { FillGrid(); };
            _section.SelectedIndexChanged += delegate { FillGrid(); };
            top.Controls.AddRange(new Control[] { _lblLang, _lang, _lblSection, _section, _save });
            Theme.StyleGrid(_grid);
            _grid.Dock = DockStyle.Fill;
            _grid.Columns.Add("idx", "#");
            _grid.Columns.Add("text", " ");
            _grid.Columns[0].Width = 50;
            _grid.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns[0].ReadOnly = true;
            _grid.CellEndEdit += GridOnCellEndEdit;
            Controls.Add(_grid);
            Controls.Add(top);
            ApplyLanguage();
        }

        public void ApplyLanguage()
        {
            _lblLang.Text = Loc.T("mlt_lang");
            _lblSection.Text = Loc.T("mlt_section");
            _save.Text = Loc.T("mlt_save");
            if (_grid.Columns.Count > 1) _grid.Columns[1].HeaderText = Loc.T("mlt_col");
            _lang.Left = _lblLang.Right + 8;
            _lblSection.Left = _lang.Right + 16;
            _section.Left = _lblSection.Right + 8;
            _save.Left = _section.Right + 16;
        }

        public void Bind(MltFile mlt)
        {
            _mlt = mlt;
            _lang.Items.Clear();
            _section.Items.Clear();
            _grid.Rows.Clear();
            if (mlt == null) return;
            for (int i = 0; i < mlt.LanguageCount; i++) _lang.Items.Add(i.ToString());
            for (int i = 0; i < mlt.SectionCount; i++) _section.Items.Add(i.ToString());
            if (_lang.Items.Count > 0) _lang.SelectedIndex = 0;
            if (_section.Items.Count > 0) _section.SelectedIndex = 0;
            FillGrid();
        }

        private void FillGrid()
        {
            _suppress = true;
            _grid.Rows.Clear();
            if (_mlt == null || _lang.SelectedIndex < 0 || _section.SelectedIndex < 0)
            {
                _suppress = false;
                return;
            }
            int li = _lang.SelectedIndex;
            int si = _section.SelectedIndex;
            if (li >= _mlt.Languages.Count || si >= _mlt.Languages[li].Count)
            {
                _suppress = false;
                return;
            }
            var strs = _mlt.Languages[li][si];
            for (int i = 0; i < strs.Count; i++)
            {
                _grid.Rows.Add(i, strs[i]);
            }
            _suppress = false;
        }

        private void GridOnCellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (_suppress || _mlt == null || e.RowIndex < 0) return;
            int li = _lang.SelectedIndex;
            int si = _section.SelectedIndex;
            object val = _grid.Rows[e.RowIndex].Cells[1].Value;
            _mlt.Languages[li][si][e.RowIndex] = val == null ? "" : val.ToString();
        }

        private void Save()
        {
            if (_mlt == null) return;
            try
            {
                _mlt.Save(_mlt.Path);
                MessageBox.Show(this, Loc.T("mlt_saved"), Loc.T("app_title"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Loc.T("mlt_save_err"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
