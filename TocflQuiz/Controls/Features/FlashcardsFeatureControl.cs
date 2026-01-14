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
        private List<int> _view = new();
        private HashSet<int> _starred = new();
        private Dictionary<int, FlashcardProgressState> _progress = new();
        private readonly Stack<ProgressUndoEntry> _undo = new();
        private bool _showingSummary;

        private readonly FlashcardControl _card = new();
        private readonly Label _lblIndex = new();
        private readonly Button _btnPrev = new();
        private readonly Button _btnNext = new();

        private readonly ToggleSwitch _toggleProgress = new();
        private readonly FlowLayoutPanel _progressActions = new();
        private readonly Button _btnLearning = new();
        private readonly Button _btnKnown = new();
        private readonly Button _btnUndo = new();
        private readonly Button _btnReset = new();

        private readonly Button _btnPlay = new();
        private readonly Button _btnShuffle = new();
        private readonly Button _btnSettings = new();
        private readonly Button _btnFullscreen = new();

        private readonly ToolTip _tt = new ToolTip();
        private readonly Panel _summaryPanel = new();
        private readonly Label _lblKnownCount = new();
        private readonly Label _lblLearningCount = new();
        private readonly Label _lblRemainingCount = new();
        private readonly Button _btnSummaryHome = new();

        public event Action? ExitToHomeRequested;

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
            _view = set.Items?.Select((_, idx) => idx).ToList() ?? new List<int>();

            LoadStarred();
            LoadProgress();
            _showingSummary = false;
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
                RowCount = 2,
                BackColor = Color.White,
                Padding = new Padding(60, 36, 60, 16)
            };
            center.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            center.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            center.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _card.Dock = DockStyle.Fill;
            _card.Margin = new Padding(0);

            center.Controls.Add(_card, 0, 0);

            _progressActions.AutoSize = true;
            _progressActions.WrapContents = false;
            _progressActions.FlowDirection = FlowDirection.LeftToRight;
            _progressActions.Anchor = AnchorStyles.None;
            _progressActions.Margin = new Padding(0, 16, 0, 0);
            _progressActions.Visible = false;

            StyleProgressButton(_btnLearning, "Đang học", Color.FromArgb(76, 146, 245), Color.FromArgb(227, 239, 255));
            StyleProgressButton(_btnKnown, "Đã biết", Color.FromArgb(76, 175, 80), Color.FromArgb(229, 247, 232));
            StyleProgressButton(_btnUndo, "Hoàn tác", Color.FromArgb(140, 140, 140), Color.FromArgb(245, 245, 245));
            StyleProgressButton(_btnReset, "Đặt lại tiến độ", Color.FromArgb(180, 120, 90), Color.FromArgb(252, 242, 236));

            _progressActions.Controls.Add(_btnLearning);
            _progressActions.Controls.Add(_btnKnown);
            _progressActions.Controls.Add(_btnUndo);
            _progressActions.Controls.Add(_btnReset);
            center.Controls.Add(_progressActions, 0, 1);

            stage.Controls.Add(center);

            _summaryPanel.Dock = DockStyle.Fill;
            _summaryPanel.BackColor = Color.White;
            _summaryPanel.Visible = false;

            var summaryWrap = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = Color.White,
                Padding = new Padding(60, 40, 60, 40)
            };
            summaryWrap.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            summaryWrap.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            summaryWrap.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            summaryWrap.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var lblSummaryTitle = new Label
            {
                Text = "Tổng kết",
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                ForeColor = Color.FromArgb(60, 60, 60),
                AutoSize = true,
                Anchor = AnchorStyles.None
            };

            var stats = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 3,
                RowCount = 2,
                BackColor = Color.White
            };
            stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));
            stats.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            stats.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var lblKnown = new Label
            {
                Text = "Đã biết",
                Font = new Font("Segoe UI", 11F, FontStyle.Regular),
                ForeColor = Color.FromArgb(90, 90, 90),
                AutoSize = true,
                Anchor = AnchorStyles.None
            };
            var lblLearning = new Label
            {
                Text = "Đang học",
                Font = new Font("Segoe UI", 11F, FontStyle.Regular),
                ForeColor = Color.FromArgb(90, 90, 90),
                AutoSize = true,
                Anchor = AnchorStyles.None
            };
            var lblRemaining = new Label
            {
                Text = "Còn lại",
                Font = new Font("Segoe UI", 11F, FontStyle.Regular),
                ForeColor = Color.FromArgb(90, 90, 90),
                AutoSize = true,
                Anchor = AnchorStyles.None
            };

            ConfigureSummaryCountLabel(_lblKnownCount);
            ConfigureSummaryCountLabel(_lblLearningCount);
            ConfigureSummaryCountLabel(_lblRemainingCount);

            stats.Controls.Add(lblKnown, 0, 0);
            stats.Controls.Add(lblLearning, 1, 0);
            stats.Controls.Add(lblRemaining, 2, 0);
            stats.Controls.Add(_lblKnownCount, 0, 1);
            stats.Controls.Add(_lblLearningCount, 1, 1);
            stats.Controls.Add(_lblRemainingCount, 2, 1);

            _btnSummaryHome.Text = "Quay về trang chủ";
            _btnSummaryHome.Width = 240;
            _btnSummaryHome.Height = 46;
            _btnSummaryHome.FlatStyle = FlatStyle.Flat;
            _btnSummaryHome.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            _btnSummaryHome.BackColor = Color.FromArgb(62, 92, 255);
            _btnSummaryHome.ForeColor = Color.White;
            _btnSummaryHome.FlatAppearance.BorderSize = 0;
            _btnSummaryHome.Cursor = Cursors.Hand;

            var summaryBtnRow = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Height = 70 };
            summaryBtnRow.Controls.Add(_btnSummaryHome);
            summaryBtnRow.Layout += (_, __) =>
            {
                _btnSummaryHome.Location = new Point(
                    (summaryBtnRow.ClientSize.Width - _btnSummaryHome.Width) / 2,
                    Math.Max(0, (summaryBtnRow.ClientSize.Height - _btnSummaryHome.Height) / 2)
                );
            };

            summaryWrap.Controls.Add(lblSummaryTitle, 0, 0);
            summaryWrap.Controls.Add(stats, 0, 1);
            summaryWrap.Controls.Add(new Panel { Height = 20, Dock = DockStyle.Fill }, 0, 2);
            summaryWrap.Controls.Add(summaryBtnRow, 0, 3);

            _summaryPanel.Controls.Add(summaryWrap);
            _summaryPanel.Layout += (_, __) =>
            {
                summaryWrap.Location = new Point(
                    Math.Max(0, (_summaryPanel.ClientSize.Width - summaryWrap.Width) / 2),
                    Math.Max(0, (_summaryPanel.ClientSize.Height - summaryWrap.Height) / 2)
                );
            };

            stage.Controls.Add(_summaryPanel);

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
            _btnLearning.Click += (_, __) => MarkProgress(FlashcardProgressState.Learning);
            _btnKnown.Click += (_, __) => MarkProgress(FlashcardProgressState.Known);
            _btnUndo.Click += (_, __) => UndoProgress();
            _btnReset.Click += (_, __) => ResetProgress();
            _btnSummaryHome.Click += (_, __) => ExitToHomeRequested?.Invoke();

            // ✅ icon nằm trong card:
            _card.StarIconClicked += (_, __) => ToggleStar();
            _card.PencilIconClicked += (_, __) => { /* chưa làm */ };
            _card.SoundIconClicked += (_, __) => { /* chưa làm */ };

            // placeholders bottom
            _btnPlay.Click += (_, __) => { /* no logic */ };
            _btnShuffle.Click += (_, __) => { /* no logic */ };
            _btnSettings.Click += (_, __) => { /* no logic */ };
            _btnFullscreen.Click += (_, __) => { /* no logic */ };

            _toggleProgress.CheckedChanged += (_, __) =>
            {
                _progressActions.Visible = _toggleProgress.Checked && !_showingSummary;
            };

            // keyboard nav
            this.PreviewKeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right) e.IsInputKey = true;
            };
            this.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Left) Prev();
                if (e.KeyCode == Keys.Right) Next();
                if (e.KeyCode == Keys.Z && e.Control) UndoProgress();
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
            _progressActions.Enabled = enabled;
        }

        private void ShowCard()
        {
            if (_set?.Items == null || _set.Items.Count == 0)
            {
                HideSummary();
                _card.Starred = false;
                _card.SetCard("Chưa có thẻ", "Hãy tạo học phần trước.", "");
                _lblIndex.Text = "0 / 0";
                return;
            }

            if (_index < 0) _index = 0;
            if (_index >= _view.Count)
            {
                ShowSummary();
                return;
            }

            HideSummary();

            var cardIndex = GetCurrentItemIndex();
            if (cardIndex < 0 || cardIndex >= _set.Items.Count)
            {
                _index = 0;
                cardIndex = GetCurrentItemIndex();
            }

            var it = _set.Items[cardIndex];

            _card.Starred = _starred.Contains(cardIndex);

            var front = it.Term ?? "";
            var back = it.Definition ?? "";
            var sub = it.Pinyin ?? "";

            _card.SetCard(front, back, sub);

            _lblIndex.Text = $"{_index + 1} / {_view.Count}";

            _btnPrev.Enabled = _index > 0;
            _btnNext.Enabled = _index < _view.Count - 1;
            UpdateProgressButtons(cardIndex);
        }

        private void Prev()
        {
            if (_set?.Items == null) return;
            if (_showingSummary)
            {
                if (_view.Count == 0) return;
                _index = Math.Max(0, _view.Count - 1);
                ShowCard();
                return;
            }
            if (_index <= 0) return;
            _index--;
            ShowCard();
        }

        private void Next()
        {
            if (_set?.Items == null) return;
            if (_showingSummary) return;
            if (_index >= _view.Count - 1)
            {
                _index = _view.Count;
                ShowSummary();
                return;
            }
            _index++;
            ShowCard();
        }

        private void ToggleStar()
        {
            if (_set?.Items == null || _set.Items.Count == 0) return;

            var cardIndex = GetCurrentItemIndex();
            if (cardIndex < 0) return;

            if (_starred.Contains(cardIndex)) _starred.Remove(cardIndex);
            else _starred.Add(cardIndex);

            SaveStarred();
            ShowCard();
        }

        private string StarFilePath() => Path.Combine(GetSetDirectory(), "starred.json");

        private string ProgressFilePath() => Path.Combine(GetSetDirectory(), "flashcard_progress.json");

        private string GetSetDirectory()
        {
            return _set == null ? CardSetStorage.BaseDir : CardSetStorage.GetSetDirectory(_set);
        }

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
                var dir = GetSetDirectory();
                Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(
                    _starred.OrderBy(x => x).ToArray(),
                    new JsonSerializerOptions { WriteIndented = true }
                );
                File.WriteAllText(StarFilePath(), json);
            }
            catch { }
        }

        private void LoadProgress()
        {
            _progress = new Dictionary<int, FlashcardProgressState>();
            if (_set == null) return;

            try
            {
                var path = ProgressFilePath();
                if (!File.Exists(path)) return;

                var json = File.ReadAllText(path);
                var store = JsonSerializer.Deserialize<FlashcardProgressStore>(json);
                if (store?.States == null) return;

                var maxIndex = _set.Items?.Count ?? 0;
                _progress = store.States
                    .Where(kv => kv.Key >= 0 && kv.Key < maxIndex)
                    .ToDictionary(kv => kv.Key, kv => kv.Value);
            }
            catch
            {
                _progress = new Dictionary<int, FlashcardProgressState>();
            }

            UpdateSummaryCounts();
        }

        private void SaveProgress()
        {
            if (_set == null) return;

            try
            {
                var dir = GetSetDirectory();
                Directory.CreateDirectory(dir);

                var store = new FlashcardProgressStore
                {
                    States = _progress
                };
                var json = JsonSerializer.Serialize(
                    store,
                    new JsonSerializerOptions { WriteIndented = true }
                );
                File.WriteAllText(ProgressFilePath(), json);
            }
            catch { }
        }

        private void MarkProgress(FlashcardProgressState state)
        {
            if (_set?.Items == null || _set.Items.Count == 0) return;
            if (!_toggleProgress.Checked) return;

            var cardIndex = GetCurrentItemIndex();
            if (cardIndex < 0) return;

            var hadPrevious = _progress.TryGetValue(cardIndex, out var previous);
            _undo.Push(new ProgressUndoEntry
            {
                CardIndex = cardIndex,
                PreviousState = hadPrevious ? previous : (FlashcardProgressState?)null
            });

            if (state == FlashcardProgressState.None)
                _progress.Remove(cardIndex);
            else
                _progress[cardIndex] = state;

            SaveProgress();
            UpdateSummaryCounts();
            UpdateProgressButtons(cardIndex);
            Next();
        }

        private void UndoProgress()
        {
            if (_undo.Count == 0) return;

            var entry = _undo.Pop();
            if (entry.PreviousState.HasValue)
                _progress[entry.CardIndex] = entry.PreviousState.Value;
            else
                _progress.Remove(entry.CardIndex);

            SaveProgress();
            UpdateSummaryCounts();
            UpdateProgressButtons(GetCurrentItemIndex());
        }

        private void ResetProgress()
        {
            _progress.Clear();
            _undo.Clear();
            SaveProgress();
            UpdateSummaryCounts();
            UpdateProgressButtons(GetCurrentItemIndex());
        }

        private void UpdateSummaryCounts()
        {
            var total = _view.Count;
            if (total == 0)
            {
                _lblKnownCount.Text = "0";
                _lblLearningCount.Text = "0";
                _lblRemainingCount.Text = "0";
                return;
            }

            var viewSet = new HashSet<int>(_view);
            var known = _progress.Count(kv => kv.Value == FlashcardProgressState.Known && viewSet.Contains(kv.Key));
            var learning = _progress.Count(kv => kv.Value == FlashcardProgressState.Learning && viewSet.Contains(kv.Key));
            var remaining = Math.Max(0, total - known - learning);

            _lblKnownCount.Text = known.ToString();
            _lblLearningCount.Text = learning.ToString();
            _lblRemainingCount.Text = remaining.ToString();
        }

        private void ShowSummary()
        {
            _showingSummary = true;
            UpdateSummaryCounts();
            _summaryPanel.Visible = true;
            _card.Visible = false;
            _progressActions.Visible = false;
            _btnNext.Enabled = false;
            _btnPrev.Enabled = _view.Count > 0;
            _lblIndex.Text = $"{_view.Count} / {_view.Count}";
        }

        private void HideSummary()
        {
            if (!_showingSummary)
            {
                _summaryPanel.Visible = false;
                _card.Visible = true;
                _progressActions.Visible = _toggleProgress.Checked;
                return;
            }

            _showingSummary = false;
            _summaryPanel.Visible = false;
            _card.Visible = true;
            _progressActions.Visible = _toggleProgress.Checked;
        }

        private void UpdateProgressButtons(int cardIndex)
        {
            var state = _progress.TryGetValue(cardIndex, out var value)
                ? value
                : FlashcardProgressState.None;

            SetProgressButtonState(_btnLearning, state == FlashcardProgressState.Learning);
            SetProgressButtonState(_btnKnown, state == FlashcardProgressState.Known);
            _btnUndo.Enabled = _undo.Count > 0;
        }

        private int GetCurrentItemIndex()
        {
            if (_index < 0 || _index >= _view.Count) return -1;
            return _view[_index];
        }

        private static void ConfigureSummaryCountLabel(Label label)
        {
            label.AutoSize = true;
            label.Font = new Font("Segoe UI", 20F, FontStyle.Bold);
            label.ForeColor = Color.FromArgb(40, 40, 40);
            label.Text = "0";
            label.Anchor = AnchorStyles.None;
        }

        private void StyleProgressButton(Button b, string text, Color accent, Color activeBack)
        {
            b.Text = text;
            b.AutoSize = true;
            b.Height = 34;
            b.Margin = new Padding(6, 0, 6, 0);
            b.FlatStyle = FlatStyle.Flat;
            b.BackColor = Color.White;
            b.ForeColor = accent;
            b.Cursor = Cursors.Hand;
            b.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = accent;
            b.Tag = new ProgressButtonStyle { AccentColor = accent, ActiveBackColor = activeBack };
        }

        private static void SetProgressButtonState(Button b, bool active)
        {
            if (b.Tag is not ProgressButtonStyle style) return;

            b.BackColor = active ? style.ActiveBackColor : Color.White;
            b.ForeColor = style.AccentColor;
            b.FlatAppearance.BorderColor = style.AccentColor;
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

        private enum FlashcardProgressState
        {
            None = 0,
            Learning = 1,
            Known = 2
        }

        private sealed class FlashcardProgressStore
        {
            public Dictionary<int, FlashcardProgressState> States { get; set; } = new();
        }

        private sealed class ProgressUndoEntry
        {
            public int CardIndex { get; set; }
            public FlashcardProgressState? PreviousState { get; set; }
        }

        private sealed class ProgressButtonStyle
        {
            public Color AccentColor { get; set; }
            public Color ActiveBackColor { get; set; }
        }
    }
}
