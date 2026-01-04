using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using TocflQuiz.Controls;
using TocflQuiz.Models;
using TocflQuiz.Services;

namespace TocflQuiz.Controls.Features
{
    public sealed partial class FlashcardsFeatureControl : UserControl
    {
        private CardSet? _set;
        private int _index;
        private HashSet<int> _starred = new();

        private readonly FlashcardControl _card = new();
        private readonly Label _lblIndex = new();
        private readonly Button _btnPrev = new();
        private readonly Button _btnNext = new();

        private readonly ToggleSwitch _toggleProgress = new();

        private readonly Button _btnPlay = new();
        private readonly Button _btnShuffle = new();
        private readonly Button _btnSettings = new();
        private readonly Button _btnFullscreen = new();

        private readonly ToolTip _tt = new ToolTip();

        public FlashcardsFeatureControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9F);

            BuildUi();
            Wire();

            SetEnabledUI(false);
        }

        public void LoadSet(CardSet set)
        {
            _set = set;
            _index = 0;

            LoadStarred();
            SetEnabledUI(_set.Items != null && _set.Items.Count > 0);
            ShowCard();
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(0),
                Margin = new Padding(0),
                ColumnCount = 1,
                RowCount = 2
            };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));

            // ===== Stage =====
            var stage = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };

            var center = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 1,
                BackColor = Color.White,
                Padding = new Padding(60, 36, 60, 16)
            };
            center.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            center.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _card.Dock = DockStyle.Fill;
            _card.Margin = new Padding(0);

            center.Controls.Add(_card, 0, 0);
            stage.Controls.Add(center);

            // ===== Bottom bar =====
            var bottom = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(18, 8, 18, 10),
                ColumnCount = 3,
                RowCount = 1
            };
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));

            // left
            var left = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            var lblProg = new Label
            {
                AutoSize = true,
                Text = "Theo dõi tiến độ",
                Font = new Font("Segoe UI", 10F, FontStyle.Regular),
                ForeColor = Color.FromArgb(60, 60, 60),
                Location = new Point(0, 18)
            };
            _toggleProgress.Location = new Point(lblProg.Right + 12, 16);
            _toggleProgress.Checked = false;

            left.Controls.Add(lblProg);
            left.Controls.Add(_toggleProgress);
            left.Layout += (_, __) => { _toggleProgress.Location = new Point(lblProg.Width + 12, 14); };

            // center nav
            var centerNav = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            var nav = new FlowLayoutPanel
            {
                AutoSize = true,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Color.Transparent
            };

            StyleRoundButton(_btnPrev, "←");
            StyleRoundButton(_btnNext, "→");

            _lblIndex.AutoSize = true;
            _lblIndex.Text = "0 / 0";
            _lblIndex.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            _lblIndex.ForeColor = Color.FromArgb(70, 70, 70);
            _lblIndex.Padding = new Padding(10, 14, 10, 0);

            nav.Controls.Add(_btnPrev);
            nav.Controls.Add(_lblIndex);
            nav.Controls.Add(_btnNext);

            centerNav.Controls.Add(nav);
            centerNav.Layout += (_, __) =>
            {
                nav.Location = new Point(
                    (centerNav.ClientSize.Width - nav.Width) / 2,
                    (centerNav.ClientSize.Height - nav.Height) / 2
                );
            };

            // right actions
            var right = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            var actions = new FlowLayoutPanel
            {
                AutoSize = true,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                Anchor = AnchorStyles.Right,
                BackColor = Color.Transparent
            };

            StyleIconButton(_btnPlay, "▶", "Tự động phát (sẽ làm sau)");
            StyleIconButton(_btnShuffle, "🔀", "Trộn thẻ (sẽ làm sau)");
            StyleIconButton(_btnSettings, "⚙", "Cài đặt (sẽ làm sau)");
            StyleIconButton(_btnFullscreen, "⛶", "Toàn màn hình (sẽ làm sau)");

            actions.Controls.Add(_btnPlay);
            actions.Controls.Add(_btnShuffle);
            actions.Controls.Add(_btnSettings);
            actions.Controls.Add(_btnFullscreen);

            right.Controls.Add(actions);
            right.Layout += (_, __) => { actions.Location = new Point(right.ClientSize.Width - actions.Width, 10); };

            bottom.Controls.Add(left, 0, 0);
            bottom.Controls.Add(centerNav, 1, 0);
            bottom.Controls.Add(right, 2, 0);

            root.Controls.Add(stage, 0, 0);
            root.Controls.Add(bottom, 0, 1);

            Controls.Clear();
            Controls.Add(root);
        }

        private void Wire()
        {
            _btnPrev.Click += (_, __) => Prev();
            _btnNext.Click += (_, __) => Next();

            // ✅ icon nằm trong card:
            _card.StarIconClicked += (_, __) => ToggleStar();
            _card.PencilIconClicked += (_, __) => { /* chưa làm */ };
            _card.SoundIconClicked += (_, __) => { /* chưa làm */ };

            // placeholders bottom
            _btnPlay.Click += (_, __) => { /* no logic */ };
            _btnShuffle.Click += (_, __) => { /* no logic */ };
            _btnSettings.Click += (_, __) => { /* no logic */ };
            _btnFullscreen.Click += (_, __) => { /* no logic */ };

            // keyboard nav
            this.PreviewKeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right) e.IsInputKey = true;
            };
            this.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Left) Prev();
                if (e.KeyCode == Keys.Right) Next();
            };

            this.Click += (_, __) => this.Focus();
            _card.Click += (_, __) => this.Focus();
            this.TabStop = true;
        }

        private void SetEnabledUI(bool enabled)
        {
            _card.Enabled = enabled;
            _btnPrev.Enabled = enabled;
            _btnNext.Enabled = enabled;
        }

        private void ShowCard()
        {
            if (_set?.Items == null || _set.Items.Count == 0)
            {
                _card.Starred = false;
                _card.SetCard("Chưa có thẻ", "Hãy tạo học phần trước.", "");
                _lblIndex.Text = "0 / 0";
                return;
            }

            if (_index < 0) _index = 0;
            if (_index >= _set.Items.Count) _index = _set.Items.Count - 1;

            var it = _set.Items[_index];

            _card.Starred = _starred.Contains(_index);

            var front = it.Term ?? "";
            var back = it.Definition ?? "";
            var sub = it.Pinyin ?? "";

            _card.SetCard(front, back, sub);

            _lblIndex.Text = $"{_index + 1} / {_set.Items.Count}";

            _btnPrev.Enabled = _index > 0;
            _btnNext.Enabled = _index < _set.Items.Count - 1;
        }

        private void Prev()
        {
            if (_set?.Items == null) return;
            if (_index <= 0) return;
            _index--;
            ShowCard();
        }

        private void Next()
        {
            if (_set?.Items == null) return;
            if (_index >= _set.Items.Count - 1) return;
            _index++;
            ShowCard();
        }

        private void ToggleStar()
        {
            if (_set?.Items == null || _set.Items.Count == 0) return;

            if (_starred.Contains(_index)) _starred.Remove(_index);
            else _starred.Add(_index);

            SaveStarred();
            ShowCard();
        }

        private string GetSetDir()
        {
            var id = _set?.Id;
            if (string.IsNullOrWhiteSpace(id)) id = "unknown_set";
            var safe = MakeSafeFileName(id);
            return Path.Combine(CardSetStorage.BaseDir, safe);
        }

        private string StarFilePath() => Path.Combine(GetSetDir(), "starred.json");

        private void LoadStarred()
        {
            _starred = new HashSet<int>();
            if (_set == null) return;

            try
            {
                var path = StarFilePath();
                if (!File.Exists(path)) return;

                var json = File.ReadAllText(path);
                var arr = JsonSerializer.Deserialize<int[]>(json) ?? Array.Empty<int>();
                _starred = new HashSet<int>(arr.Where(i => i >= 0));
            }
            catch
            {
                _starred = new HashSet<int>();
            }
        }

        private void SaveStarred()
        {
            if (_set == null) return;

            try
            {
                var dir = GetSetDir();
                Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(
                    _starred.OrderBy(x => x).ToArray(),
                    new JsonSerializerOptions { WriteIndented = true }
                );
                File.WriteAllText(StarFilePath(), json);
            }
            catch { }
        }

        private static string MakeSafeFileName(string s)
        {
            s ??= "";
            foreach (var c in Path.GetInvalidFileNameChars())
                s = s.Replace(c, '_');
            return s.Trim();
        }

        private void StyleIconButton(Button b, string text, string tooltip)
        {
            b.Text = text;
            b.Width = 38;
            b.Height = 34;
            b.Margin = new Padding(6, 0, 0, 0);
            b.FlatStyle = FlatStyle.Flat;
            b.BackColor = Color.White;
            b.ForeColor = Color.FromArgb(60, 60, 60);
            b.Cursor = Cursors.Hand;
            b.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            b.FlatAppearance.BorderSize = 0;
            _tt.SetToolTip(b, tooltip);
        }

        private static void StyleRoundButton(Button b, string text)
        {
            b.Text = text;
            b.Width = 52;
            b.Height = 52;
            b.Margin = new Padding(6, 0, 6, 0);
            b.FlatStyle = FlatStyle.Flat;
            b.BackColor = Color.FromArgb(248, 248, 248);
            b.ForeColor = Color.FromArgb(60, 60, 60);
            b.Cursor = Cursors.Hand;
            b.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = Color.FromArgb(225, 225, 225);
        }
    }
}
