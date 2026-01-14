using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TocflQuiz.Controls;
using TocflQuiz.Models;
using TocflQuiz.Services;
using WinRtSpeechSynthesizer = Windows.Media.SpeechSynthesis.SpeechSynthesizer;

namespace TocflQuiz.Controls.Features
{
    public sealed partial class FlashcardsFeatureControl : UserControl
    {
        private CardSet? _set;
        private int _index;
        private List<int> _studyOrder = new();
        private readonly Dictionary<int, ProgressState> _progressStates = new();
        private readonly Stack<ProgressAction> _progressHistory = new();
        private readonly Random _rng = new();

        private bool _shuffleEnabled;
        private bool _filterStarredOnly;
        private bool _progressTrackingEnabled;
        private bool _speechEnabled;
        private FrontFaceMode _frontFaceMode = FrontFaceMode.Term;

        private readonly FlashcardControl _card = new();
        private readonly Label _lblIndex = new();
        private readonly Button _btnPrev = new();
        private readonly Button _btnNext = new();

        private readonly ToggleSwitch _toggleProgress = new();
        private readonly ToggleSwitch _toggleProgressOptions = new();
        private readonly ToggleSwitch _toggleStarOnly = new();
        private readonly ToggleSwitch _toggleSpeech = new();

        private readonly Button _btnPlay = new();
        private readonly Button _btnShuffle = new();
        private readonly Button _btnSettings = new();
        private readonly Button _btnFullscreen = new();

        private readonly Panel _optionsOverlay = new();
        private readonly Button _btnOptionsClose = new();
        private readonly ComboBox _cmbFrontFace = new();
        private readonly Label _lblRestart = new();

        private readonly ToolTip _tt = new ToolTip();

        private readonly Dictionary<string, byte[]> _ttsCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly SemaphoreSlim _ttsLock = new(1, 1);
        private CancellationTokenSource? _ttsCts;
        private SoundPlayer? _soundPlayer;
        private MemoryStream? _soundStream;
        private WinRtSpeechSynthesizer? _winRtSynth;
        private bool _winRtSynthChecked;
        private bool _syncingToggles;

        private enum FrontFaceMode
        {
            Term,
            Definition,
            Pinyin
        }

        private enum ProgressState
        {
            None,
            Learning,
            Known
        }

        private sealed class ProgressAction
        {
            public int Position { get; init; }
            public int ItemIndex { get; init; }
            public ProgressState PreviousState { get; init; }
        }

        public FlashcardsFeatureControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9F);

            BuildUi();
            Wire();
            InitializeOptionState();

            SetEnabledUI(false);
        }

        public void LoadSet(CardSet set)
        {
            _set = set;
            _index = 0;

            _progressStates.Clear();
            _progressHistory.Clear();

            ApplyLegacyStarred();
            RebuildStudyOrder(keepCurrent: false);
            SetEnabledUI(_studyOrder.Count > 0);
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

            StyleIconButton(_btnPlay, "▶", "Tự động phát");
            StyleIconButton(_btnShuffle, "🔀", "Trộn thẻ");
            StyleIconButton(_btnSettings, "⚙", "Tùy chọn");
            StyleIconButton(_btnFullscreen, "⛶", "Toàn màn hình");

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
            BuildOptionsOverlay();
            Controls.Add(_optionsOverlay);
            _optionsOverlay.BringToFront();
        }

        private void BuildOptionsOverlay()
        {
            _optionsOverlay.Dock = DockStyle.Fill;
            _optionsOverlay.BackColor = Color.FromArgb(18, 18, 40);
            _optionsOverlay.Visible = false;

            var container = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(18, 18, 40),
                Padding = new Padding(28, 22, 28, 24)
            };

            var lblTitle = new Label
            {
                Text = "Tùy chọn",
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(0, 0)
            };

            _btnOptionsClose.Text = "✕";
            _btnOptionsClose.Width = 38;
            _btnOptionsClose.Height = 34;
            _btnOptionsClose.FlatStyle = FlatStyle.Flat;
            _btnOptionsClose.BackColor = Color.FromArgb(30, 30, 55);
            _btnOptionsClose.ForeColor = Color.White;
            _btnOptionsClose.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            _btnOptionsClose.FlatAppearance.BorderSize = 0;
            _btnOptionsClose.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            container.Controls.Add(lblTitle);
            container.Controls.Add(_btnOptionsClose);

            container.Layout += (_, __) =>
            {
                _btnOptionsClose.Location = new Point(container.ClientSize.Width - _btnOptionsClose.Width, 0);
            };

            var options = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 5,
                Location = new Point(0, 70),
                BackColor = Color.Transparent
            };
            options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68));
            options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));

            var lblProgress = BuildOptionTitle("Theo dõi tiến độ");
            var lblProgressDesc = BuildOptionDesc("Sắp xếp các thẻ ghi nhớ của bạn để theo dõi những gì bạn đã biết và những gì đang học. Tắt tính năng theo dõi tiến độ nếu bạn muốn nhanh chóng ôn lại các thẻ ghi nhớ.");
            var progressPanel = new Panel { AutoSize = true, BackColor = Color.Transparent };
            progressPanel.Controls.Add(lblProgress);
            progressPanel.Controls.Add(lblProgressDesc);
            lblProgressDesc.Location = new Point(0, lblProgress.Bottom + 6);

            _toggleProgressOptions.Anchor = AnchorStyles.Right;
            _toggleProgressOptions.Margin = new Padding(0, 8, 0, 0);
            options.Controls.Add(progressPanel, 0, 0);
            options.Controls.Add(_toggleProgressOptions, 1, 0);

            var lblStarOnly = BuildOptionTitle("Chỉ học thuật ngữ có gắn sao");
            _toggleStarOnly.Anchor = AnchorStyles.Right;
            _toggleStarOnly.Margin = new Padding(0, 8, 0, 0);
            options.Controls.Add(lblStarOnly, 0, 1);
            options.Controls.Add(_toggleStarOnly, 1, 1);

            var lblFront = BuildOptionTitle("Mặt trước");
            options.Controls.Add(lblFront, 0, 2);
            _cmbFrontFace.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbFrontFace.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            _cmbFrontFace.BackColor = Color.FromArgb(42, 48, 78);
            _cmbFrontFace.ForeColor = Color.White;
            _cmbFrontFace.FlatStyle = FlatStyle.Flat;
            _cmbFrontFace.Width = 220;
            _cmbFrontFace.Anchor = AnchorStyles.Right;
            _cmbFrontFace.Items.AddRange(new object[]
            {
                "Tiếng Trung (Phồn thể)",
                "Nghĩa tiếng Việt",
                "Pinyin"
            });
            _cmbFrontFace.SelectedIndex = 0;
            options.Controls.Add(_cmbFrontFace, 1, 2);

            var lblSpeech = BuildOptionTitle("Chuyển văn bản thành lời nói");
            _toggleSpeech.Anchor = AnchorStyles.Right;
            _toggleSpeech.Margin = new Padding(0, 8, 0, 0);
            options.Controls.Add(lblSpeech, 0, 3);
            options.Controls.Add(_toggleSpeech, 1, 3);

            _lblRestart.Text = "Khởi động lại Thẻ ghi nhớ";
            _lblRestart.AutoSize = true;
            _lblRestart.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            _lblRestart.ForeColor = Color.FromArgb(232, 74, 83);
            _lblRestart.Cursor = Cursors.Hand;
            options.Controls.Add(_lblRestart, 0, 4);

            container.Controls.Add(options);
            _optionsOverlay.Controls.Clear();
            _optionsOverlay.Controls.Add(container);
        }

        private static Label BuildOptionTitle(string text)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true
            };
        }

        private static Label BuildOptionDesc(string text)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.FromArgb(185, 190, 220),
                AutoSize = true,
                MaximumSize = new Size(520, 0)
            };
        }

        private void InitializeOptionState()
        {
            _syncingToggles = true;
            _toggleProgress.Checked = _progressTrackingEnabled;
            _toggleProgressOptions.Checked = _progressTrackingEnabled;
            _toggleStarOnly.Checked = _filterStarredOnly;
            _toggleSpeech.Checked = _speechEnabled;
            _cmbFrontFace.SelectedIndex = _frontFaceMode switch
            {
                FrontFaceMode.Definition => 1,
                FrontFaceMode.Pinyin => 2,
                _ => 0
            };
            _syncingToggles = false;

            UpdateActionButtons();
            _tt.SetToolTip(_btnShuffle, "Trộn thẻ");
        }

        private void Wire()
        {
            _btnPrev.Click += (_, __) => HandlePrevClick();
            _btnNext.Click += (_, __) => HandleNextClick();

            // ✅ icon nằm trong card:
            _card.StarIconClicked += (_, __) => ToggleStar();
            _card.PencilIconClicked += (_, __) => EditCurrentCard();
            _card.SoundIconClicked += async (_, __) => await PlayCurrentTextAsync();

            _btnPlay.Click += (_, __) =>
            {
                if (_progressTrackingEnabled) UndoProgress();
            };
            _btnShuffle.Click += (_, __) => ToggleShuffle();
            _btnSettings.Click += (_, __) => ToggleOptionsPanel();
            _btnFullscreen.Click += (_, __) => { /* no logic */ };

            // keyboard nav
            this.PreviewKeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right) e.IsInputKey = true;
            };
            this.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Left) HandlePrevClick();
                if (e.KeyCode == Keys.Right) HandleNextClick();
            };

            this.Click += (_, __) => this.Focus();
            _card.Click += (_, __) => this.Focus();
            this.TabStop = true;

            _toggleProgress.CheckedChanged += (_, __) => SetProgressTracking(_toggleProgress.Checked);
            _toggleProgressOptions.CheckedChanged += (_, __) => SetProgressTracking(_toggleProgressOptions.Checked);
            _toggleStarOnly.CheckedChanged += (_, __) => SetFilterStarOnly(_toggleStarOnly.Checked);
            _toggleSpeech.CheckedChanged += (_, __) => SetSpeechEnabled(_toggleSpeech.Checked);
            _cmbFrontFace.SelectedIndexChanged += (_, __) => UpdateFrontFaceMode();
            _btnOptionsClose.Click += (_, __) => ToggleOptionsPanel(false);
            _lblRestart.Click += (_, __) => ResetProgress();
        }

        private void SetEnabledUI(bool enabled)
        {
            _card.Enabled = enabled;
            _btnPrev.Enabled = enabled;
            _btnNext.Enabled = enabled;
            _btnPlay.Enabled = enabled;
            _btnShuffle.Enabled = enabled;
            _toggleProgress.Enabled = enabled;
            _toggleProgressOptions.Enabled = enabled;
            _toggleStarOnly.Enabled = enabled;
            _toggleSpeech.Enabled = enabled;
        }

        private void ShowCard()
        {
            if (_set?.Items == null || _studyOrder.Count == 0)
            {
                _card.Starred = false;
                _card.SetCard("Chưa có thẻ", "Hãy tạo học phần trước.", "");
                _lblIndex.Text = "0 / 0";
                UpdateNavButtons();
                return;
            }

            if (_index < 0) _index = 0;
            if (_index >= _studyOrder.Count) _index = _studyOrder.Count - 1;

            var itemIndex = _studyOrder[_index];
            var it = _set.Items[itemIndex];

            _card.Starred = it.IsStarred;

            var (front, back, sub) = ResolveCardText(it);
            _card.SetCard(front, back, sub);

            _lblIndex.Text = $"{_index + 1} / {_studyOrder.Count}";

            UpdateNavButtons();
        }

        private void Prev()
        {
            if (_set?.Items == null || _studyOrder.Count == 0) return;
            if (_index <= 0) return;
            _index--;
            ShowCard();
        }

        private void Next()
        {
            if (_set?.Items == null || _studyOrder.Count == 0) return;
            if (_index >= _studyOrder.Count - 1) return;
            _index++;
            ShowCard();
        }

        private void ToggleStar()
        {
            if (_set?.Items == null || _studyOrder.Count == 0) return;

            var itemIndex = _studyOrder[_index];
            var item = _set.Items[itemIndex];
            item.IsStarred = !item.IsStarred;

            PersistSet();
            if (_filterStarredOnly) RebuildStudyOrder(keepCurrent: true);
            ShowCard();
        }

        private void HandlePrevClick()
        {
            if (_progressTrackingEnabled)
            {
                RegisterProgress(ProgressState.Learning);
                return;
            }

            Prev();
        }

        private void HandleNextClick()
        {
            if (_progressTrackingEnabled)
            {
                RegisterProgress(ProgressState.Known);
                return;
            }

            Next();
        }

        private void RegisterProgress(ProgressState state)
        {
            if (_set?.Items == null || _studyOrder.Count == 0) return;

            var itemIndex = _studyOrder[_index];
            var previous = _progressStates.TryGetValue(itemIndex, out var prev) ? prev : ProgressState.None;
            _progressStates[itemIndex] = state;
            _progressHistory.Push(new ProgressAction
            {
                Position = _index,
                ItemIndex = itemIndex,
                PreviousState = previous
            });

            if (_index < _studyOrder.Count - 1)
                _index++;

            ShowCard();
        }

        private void UndoProgress()
        {
            if (_progressHistory.Count == 0) return;

            var action = _progressHistory.Pop();
            if (action.PreviousState == ProgressState.None)
                _progressStates.Remove(action.ItemIndex);
            else
                _progressStates[action.ItemIndex] = action.PreviousState;

            _index = Math.Min(action.Position, Math.Max(0, _studyOrder.Count - 1));
            ShowCard();
        }

        private void ResetProgress()
        {
            _progressStates.Clear();
            _progressHistory.Clear();
            _index = 0;
            ShowCard();
        }

        private void SetProgressTracking(bool enabled)
        {
            if (_syncingToggles) return;

            _progressTrackingEnabled = enabled;
            _syncingToggles = true;
            if (_toggleProgress.Checked != enabled) _toggleProgress.Checked = enabled;
            if (_toggleProgressOptions.Checked != enabled) _toggleProgressOptions.Checked = enabled;
            _syncingToggles = false;

            UpdateActionButtons();
            UpdateNavButtons();
        }

        private void SetFilterStarOnly(bool enabled)
        {
            if (_syncingToggles) return;

            _filterStarredOnly = enabled;
            _syncingToggles = true;
            if (_toggleStarOnly.Checked != enabled) _toggleStarOnly.Checked = enabled;
            _syncingToggles = false;

            RebuildStudyOrder(keepCurrent: true);
            ShowCard();
        }

        private void SetSpeechEnabled(bool enabled)
        {
            _speechEnabled = enabled;
            if (!_speechEnabled) StopTtsPlayback();
        }

        private void UpdateFrontFaceMode()
        {
            _frontFaceMode = _cmbFrontFace.SelectedIndex switch
            {
                1 => FrontFaceMode.Definition,
                2 => FrontFaceMode.Pinyin,
                _ => FrontFaceMode.Term
            };
            ShowCard();
        }

        private void ToggleShuffle()
        {
            _shuffleEnabled = !_shuffleEnabled;
            _tt.SetToolTip(_btnShuffle, _shuffleEnabled ? "Tắt trộn thẻ" : "Trộn thẻ");
            RebuildStudyOrder(keepCurrent: true);
            ShowCard();
        }

        private void ToggleOptionsPanel(bool? show = null)
        {
            _optionsOverlay.Visible = show ?? !_optionsOverlay.Visible;
            if (_optionsOverlay.Visible)
                _optionsOverlay.BringToFront();
        }

        private void RebuildStudyOrder(bool keepCurrent)
        {
            if (_set?.Items == null)
            {
                _studyOrder = new List<int>();
                _index = 0;
                return;
            }

            var currentItemIndex = keepCurrent && _studyOrder.Count > 0 && _index >= 0 && _index < _studyOrder.Count
                ? _studyOrder[_index]
                : -1;

            var order = Enumerable.Range(0, _set.Items.Count).ToList();
            if (_filterStarredOnly)
                order = order.Where(i => _set.Items[i].IsStarred).ToList();

            if (_shuffleEnabled)
                Shuffle(order);

            var changed = !_studyOrder.SequenceEqual(order);
            _studyOrder = order;

            if (currentItemIndex >= 0)
            {
                var newIndex = _studyOrder.IndexOf(currentItemIndex);
                _index = newIndex >= 0 ? newIndex : 0;
            }
            else
            {
                _index = 0;
            }

            if (changed)
                _progressHistory.Clear();
        }

        private void Shuffle(List<int> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private void UpdateNavButtons()
        {
            bool hasItems = _studyOrder.Count > 0;
            if (_progressTrackingEnabled)
            {
                _btnPrev.Enabled = hasItems;
                _btnNext.Enabled = hasItems;
                _btnPlay.Enabled = hasItems && _progressHistory.Count > 0;
            }
            else
            {
                _btnPrev.Enabled = hasItems && _index > 0;
                _btnNext.Enabled = hasItems && _index < _studyOrder.Count - 1;
                _btnPlay.Enabled = hasItems;
            }
        }

        private void UpdateActionButtons()
        {
            if (_progressTrackingEnabled)
            {
                _btnPrev.Text = "✕";
                _btnNext.Text = "✓";
                _btnPlay.Text = "↶";
                _tt.SetToolTip(_btnPrev, "Chưa thuộc");
                _tt.SetToolTip(_btnNext, "Đã thuộc");
                _tt.SetToolTip(_btnPlay, "Hoàn tác");
            }
            else
            {
                _btnPrev.Text = "←";
                _btnNext.Text = "→";
                _btnPlay.Text = "▶";
                _tt.SetToolTip(_btnPrev, "Trước");
                _tt.SetToolTip(_btnNext, "Sau");
                _tt.SetToolTip(_btnPlay, "Tự động phát");
            }
        }

        private (string front, string back, string sub) ResolveCardText(CardItem item)
        {
            var term = item.Term ?? "";
            var def = item.Definition ?? "";
            var pinyin = item.Pinyin ?? "";

            return _frontFaceMode switch
            {
                FrontFaceMode.Definition => (def, term, pinyin),
                FrontFaceMode.Pinyin => (string.IsNullOrWhiteSpace(pinyin) ? term : pinyin, term, def),
                _ => (term, def, pinyin)
            };
        }

        private void EditCurrentCard()
        {
            if (_set?.Items == null || _studyOrder.Count == 0) return;

            var itemIndex = _studyOrder[_index];
            var item = _set.Items[itemIndex];

            using var dialog = BuildEditDialog(item);
            if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;

            var termBox = (TextBox)dialog.Tag!;
            var defBox = (TextBox)termBox.Tag!;
            var pinyinBox = (TextBox)defBox.Tag!;

            item.Term = termBox.Text.Trim();
            item.Definition = defBox.Text.Trim();
            item.Pinyin = string.IsNullOrWhiteSpace(pinyinBox.Text) ? null : pinyinBox.Text.Trim();

            PersistSet();
            ShowCard();
        }

        private Form BuildEditDialog(CardItem item)
        {
            var dlg = new Form
            {
                Text = "Chỉnh sửa thẻ",
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false,
                MaximizeBox = false,
                ShowInTaskbar = false,
                BackColor = Color.FromArgb(24, 26, 46),
                ForeColor = Color.White,
                ClientSize = new Size(520, 300),
                Font = new Font("Segoe UI", 9.5F)
            };

            var lblTerm = new Label
            {
                Text = "Thuật ngữ",
                AutoSize = true,
                Location = new Point(24, 24),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };
            var txtTerm = new TextBox
            {
                Location = new Point(24, 48),
                Width = 470,
                Text = item.Term ?? "",
                BackColor = Color.FromArgb(40, 44, 72),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            var lblDef = new Label
            {
                Text = "Định nghĩa",
                AutoSize = true,
                Location = new Point(24, 92),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };
            var txtDef = new TextBox
            {
                Location = new Point(24, 116),
                Width = 470,
                Text = item.Definition ?? "",
                BackColor = Color.FromArgb(40, 44, 72),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            var lblPinyin = new Label
            {
                Text = "Pinyin (tuỳ chọn)",
                AutoSize = true,
                Location = new Point(24, 160),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };
            var txtPinyin = new TextBox
            {
                Location = new Point(24, 184),
                Width = 470,
                Text = item.Pinyin ?? "",
                BackColor = Color.FromArgb(40, 44, 72),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            var btnCancel = new Button
            {
                Text = "Hủy",
                DialogResult = DialogResult.Cancel,
                Width = 90,
                Height = 32,
                Location = new Point(308, 236),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(50, 54, 86),
                ForeColor = Color.White
            };
            btnCancel.FlatAppearance.BorderSize = 0;

            var btnSave = new Button
            {
                Text = "Lưu",
                DialogResult = DialogResult.OK,
                Width = 90,
                Height = 32,
                Location = new Point(404, 236),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(90, 120, 255),
                ForeColor = Color.White
            };
            btnSave.FlatAppearance.BorderSize = 0;

            dlg.Controls.Add(lblTerm);
            dlg.Controls.Add(txtTerm);
            dlg.Controls.Add(lblDef);
            dlg.Controls.Add(txtDef);
            dlg.Controls.Add(lblPinyin);
            dlg.Controls.Add(txtPinyin);
            dlg.Controls.Add(btnCancel);
            dlg.Controls.Add(btnSave);

            dlg.AcceptButton = btnSave;
            dlg.CancelButton = btnCancel;

            txtTerm.Tag = txtDef;
            txtDef.Tag = txtPinyin;
            dlg.Tag = txtTerm;

            return dlg;
        }

        private async Task PlayCurrentTextAsync()
        {
            if (!_speechEnabled) return;
            var text = _card.IsFlipped ? _card.BackText : _card.FrontText;
            text = text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(text)) return;

            StopTtsPlayback();
            _ttsCts = new CancellationTokenSource();
            var token = _ttsCts.Token;

            try
            {
                byte[]? audio = null;
                lock (_ttsCache)
                {
                    _ttsCache.TryGetValue(text, out audio);
                }

                if (audio == null || audio.Length == 0)
                {
                    await _ttsLock.WaitAsync(token);
                    try
                    {
                        audio = await SynthesizeAudioAsync(text, token);
                    }
                    finally
                    {
                        _ttsLock.Release();
                    }

                    if (audio.Length == 0) return;
                    lock (_ttsCache)
                    {
                        _ttsCache[text] = audio;
                    }
                }

                if (token.IsCancellationRequested) return;
                PlayAudio(audio);
            }
            catch (OperationCanceledException)
            {
                // ignored
            }
        }

        private void StopTtsPlayback()
        {
            _ttsCts?.Cancel();
            _ttsCts?.Dispose();
            _ttsCts = null;

            StopAudioPlayer();
        }

        private void PlayAudio(byte[] audio)
        {
            StopAudioPlayer();
            _soundStream = new MemoryStream(audio);
            _soundPlayer = new SoundPlayer(_soundStream);
            _soundPlayer.Play();
        }

        private void StopAudioPlayer()
        {
            try { _soundPlayer?.Stop(); } catch { }
            _soundPlayer?.Dispose();
            _soundPlayer = null;
            _soundStream?.Dispose();
            _soundStream = null;
        }

        private async Task<byte[]> SynthesizeAudioAsync(string text, CancellationToken token)
        {
            try
            {
                var synth = EnsureWinRtSynth();
                if (synth != null)
                {
                    var stream = await synth.SynthesizeTextToStreamAsync(text);
                    await using var audioStream = stream.AsStreamForRead();
                    using var ms = new MemoryStream();
                    await audioStream.CopyToAsync(ms, token);
                    return ms.ToArray();
                }
            }
            catch
            {
                // ignored, fallback below
            }

            return await SynthesizeWithSystemSpeechAsync(text, token);
        }

        private WinRtSpeechSynthesizer? EnsureWinRtSynth()
        {
            if (_winRtSynthChecked) return _winRtSynth;
            _winRtSynthChecked = true;

            try
            {
                var synth = new WinRtSpeechSynthesizer();
                var voice = WinRtSpeechSynthesizer.AllVoices.FirstOrDefault(v =>
                    v.DisplayName.Contains("Xiaoxiao", StringComparison.OrdinalIgnoreCase) ||
                    v.VoiceName.Contains("Xiaoxiao", StringComparison.OrdinalIgnoreCase));

                if (voice != null)
                {
                    synth.Voice = voice;
                    _winRtSynth = synth;
                }
            }
            catch
            {
                _winRtSynth = null;
            }

            return _winRtSynth;
        }

        private static Task<byte[]> SynthesizeWithSystemSpeechAsync(string text, CancellationToken token)
        {
            return Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                using var synth = new System.Speech.Synthesis.SpeechSynthesizer();
                var zhVoice = synth.GetInstalledVoices()
                    .FirstOrDefault(v => v.VoiceInfo.Culture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase));
                if (zhVoice != null)
                    synth.SelectVoice(zhVoice.VoiceInfo.Name);

                using var ms = new MemoryStream();
                synth.SetOutputToWaveStream(ms);
                synth.Speak(text);
                return ms.ToArray();
            }, token);
        }

        private void ApplyLegacyStarred()
        {
            if (_set?.Items == null) return;

            var setDir = CardSetStorage.GetSetDirectory(_set.Id);
            var starPath = Path.Combine(setDir, "starred.json");
            var markerPath = Path.Combine(setDir, "starred.migrated");
            if (!File.Exists(starPath) || File.Exists(markerPath)) return;

            if (_set.Items.Any(i => i.IsStarred))
            {
                TryWriteMarker(markerPath);
                return;
            }

            try
            {
                var json = File.ReadAllText(starPath);
                var arr = JsonSerializer.Deserialize<int[]>(json) ?? Array.Empty<int>();
                foreach (var idx in arr.Where(i => i >= 0 && i < _set.Items.Count))
                    _set.Items[idx].IsStarred = true;

                PersistSet();
                TryWriteMarker(markerPath);
            }
            catch
            {
                // ignore legacy errors
            }
        }

        private static void TryWriteMarker(string path)
        {
            try { File.WriteAllText(path, "migrated"); } catch { }
        }

        private void PersistSet()
        {
            if (_set == null) return;
            try
            {
                CardSetStorage.SaveSetJsonOnly(_set);
            }
            catch { }
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
