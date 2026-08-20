using System;
using System.IO;
using System.Media;
using System.Windows.Forms;
using SdwEditor.Formats;
using SdwEditor.Render;

namespace SdwEditor.Ui
{
    internal sealed class SndEditorControl : UserControl
    {
        private readonly ListView _list = new ListView();
        private readonly WaveformControl _wave = new WaveformControl();
        private readonly Label _info = new Label();
        private readonly Button _play = new Button();
        private readonly Button _stop = new Button();
        private readonly Button _exp = new Button();
        private readonly Button _expAll = new Button();
        private readonly Button _repl = new Button();
        private SndFile _snd;
        private SoundPlayer _player;

        public SndEditorControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Theme.Panel;
            SplitContainer split = new SplitContainer { Dock = DockStyle.Fill, BackColor = Theme.Line };
            Theme.LayoutSplit(split, 0.24f);
            Controls.Add(split);
            Theme.StyleList(_list);
            _list.Dock = DockStyle.Fill;
            _list.Columns.Add(" ", 340);
            _list.SelectedIndexChanged += delegate { ShowSelected(); };
            _list.DoubleClick += delegate { Play(); };
            split.Panel1.Controls.Add(_list);

            Panel right = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Panel };
            FlowLayoutPanel bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, BackColor = Theme.Panel };
            StyleBtn(_play);
            StyleBtn(_stop);
            StyleBtn(_exp);
            StyleBtn(_expAll);
            StyleBtn(_repl);
            _play.Click += delegate { Play(); };
            _stop.Click += delegate { Stop(); };
            _exp.Click += delegate { ExportOne(); };
            _expAll.Click += delegate { ExportAll(); };
            _repl.Click += delegate { Replace(); };
            bar.Controls.AddRange(new Control[] { _play, _stop, _exp, _expAll, _repl });
            _wave.Dock = DockStyle.Fill;
            _info.Dock = DockStyle.Bottom;
            _info.Height = 28;
            _info.ForeColor = Theme.Muted;
            right.Controls.Add(_wave);
            right.Controls.Add(_info);
            right.Controls.Add(bar);
            split.Panel2.Controls.Add(right);
            ApplyLanguage();
        }

        private static void StyleBtn(Button b)
        {
            b.AutoSize = true;
            b.Height = 28;
            b.Margin = new Padding(6, 6, 0, 0);
            Theme.StyleButton(b);
        }

        public void ApplyLanguage()
        {
            if (_list.Columns.Count > 0) _list.Columns[0].Text = Loc.T("col_sound");
            _play.Text = Loc.T("play");
            _stop.Text = Loc.T("stop");
            _exp.Text = Loc.T("export_wav");
            _expAll.Text = Loc.T("export_all");
            _repl.Text = Loc.T("replace_wav");
            object sel = Selected();
            Bind(_snd);
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
            _wave.Invalidate();
        }

        public void Bind(SndFile snd)
        {
            Stop();
            _snd = snd;
            _list.Items.Clear();
            _wave.SetWav(null);
            if (snd == null) return;
            foreach (SndEntry e in snd.Entries)
            {
                ListViewItem it = new ListViewItem(Loc.F("snd_item", e.Index, e.SoundId, e.WavLength, e.Flags));
                it.Tag = e;
                _list.Items.Add(it);
            }
            if (_list.Items.Count > 0) _list.Items[0].Selected = true;
        }

        private SndEntry Selected()
        {
            if (_list.SelectedItems.Count == 0) return null;
            return _list.SelectedItems[0].Tag as SndEntry;
        }

        private void ShowSelected()
        {
            SndEntry e = Selected();
            if (e == null) return;
            _wave.SetWav(e.WavBytes);
            _info.Text = Loc.F("snd_info", e.SoundId);
        }

        private void Play()
        {
            SndEntry e = Selected();
            if (e == null) return;
            Stop();
            _player = new SoundPlayer(new MemoryStream(e.WavBytes));
            _player.Play();
        }

        private void Stop()
        {
            if (_player != null)
            {
                _player.Stop();
                _player.Dispose();
                _player = null;
            }
        }

        private void ExportOne()
        {
            SndEntry e = Selected();
            if (e == null) return;
            using (SaveFileDialog dlg = new SaveFileDialog())
            {
                dlg.Filter = "WAV|*.wav";
                dlg.FileName = "id" + e.SoundId + ".wav";
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    File.WriteAllBytes(dlg.FileName, e.WavBytes);
                }
            }
        }

        private void ExportAll()
        {
            if (_snd == null) return;
            using (FolderBrowserDialog dlg = new FolderBrowserDialog())
            {
                dlg.SelectedPath = @"E:\SDW (F)\Cursor";
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _snd.ExportAll(dlg.SelectedPath);
                }
            }
        }

        private void Replace()
        {
            SndEntry e = Selected();
            if (e == null || _snd == null) return;
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Filter = "WAV|*.wav";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    _snd.ReplaceWav(e.Index, File.ReadAllBytes(dlg.FileName));
                    _snd.Save(_snd.Path);
                    Bind(_snd);
                    MessageBox.Show(this, Loc.T("snd_replaced"), Loc.T("app_title"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, Loc.T("error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Stop();
            base.Dispose(disposing);
        }
    }
}
