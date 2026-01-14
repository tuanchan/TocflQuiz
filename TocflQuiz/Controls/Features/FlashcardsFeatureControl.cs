using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Speech.Synthesis;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NAudio.Wave;
using TocflQuiz.Controls;
using TocflQuiz.Models;
using TocflQuiz.Services;

namespace TocflQuiz.Controls.Features
{
    public sealed partial class FlashcardsFeatureControl : UserControl
    {
        private CardSet? _set;
        private int _index;
        private readonly List<int> _order = new();
        private readonly List<int> _view = new();
        private readonly Dictionary<int, ProgressState> _progress = new();
        private readonly Stack<ProgressAction> _undo = new();
        private readonly Random _rng = new();
        private bool _progressTrackingEnabled;
        private bool _onlyStarred;
        private bool _shuffleEnabled;
        private FrontMode _frontMode = FrontMode.Term;
        private bool _ttsEnabled;
        private bool _autoSpeakEnabled;
        private bool _syncingProgressToggle;
        private bool _isDarkMode;

        private readonly FlashcardControl _card = new();
        private readonly Label _lblIndex = new();
        private readonly Button _btnPrev = new();
        private readonly Button _btnNext = new();

        private readonly ToggleSwitch _toggleProgress = new();
        private readonly Label _lblProgress = new();

        private readonly Button _btnPlay = new();
        private readonly Button _btnShuffle = new();
        private readonly Button _btnSettings = new();
        private readonly Button _btnFullscreen = new();

        private readonly SettingsOverlayPanel _settingsOverlay = new();
        private readonly RoundedPanel _settingsPanel = new();
        private readonly Button _btnCloseSettings = new();
        private readonly ToggleSwitch _toggleProgressSettings = new();
        private readonly ToggleSwitch _toggleOnlyStarred = new();
        private readonly ToggleSwitch _toggleTts = new();
        private readonly ToggleSwitch _toggleAutoSpeak = new();
        private readonly ComboBox _cmbFront = new();
        private readonly Label _lblReset = new();

        private readonly Dictionary<string, byte[]> _ttsCache = new(StringComparer.Ordinal);
        private CancellationTokenSource? _ttsCts;
        private WaveOutEvent? _ttsOutput;
        private WaveStream? _ttsStream;
        private readonly object _ttsLock = new();

        private readonly OverlayPanel _progressOverlay = new();
        private readonly RoundedPanel _progressDialog = new();
        private readonly Button _btnProgressPrimary = new();
        private readonly Button _btnProgressSecondary = new();
        private readonly Button _btnProgressClose = new();
        private readonly Label _lblProgressTitle = new();
        private readonly Label _lblProgressKnown = new();
        private readonly Label _lblProgressLearning = new();
        private readonly Label _lblProgressRemaining = new();

        private Panel? _root;
        private Panel? _stage;
        private Panel? _bottom;
        private Panel? _leftPanel;
        private Panel? _centerPanel;
        private Panel? _rightPanel;

        private readonly ToolTip _tt = new ToolTip();

        public event Action? ExitRequested;

        public FlashcardsFeatureControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9F);

            BuildUi();
            Wire();

            SetEnabledUI(false);
            Disposed += (_, __) => CancelTts();
        }

        public void LoadSet(CardSet set)
        {
            _set = set;
            _index = 0;
            _progress.Clear();
            _undo.Clear();
            HideProgressOverlay();

            LoadLegacyStarred();
            RebuildView(keepCurrent: false);
            SetEnabledUI(_view.Count > 0);
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
            _lblProgress.AutoSize = true;
            _lblProgress.Text = "Theo dõi tiến độ";
            _lblProgress.Font = new Font("Segoe UI", 10F, FontStyle.Regular);
            _lblProgress.ForeColor = Color.FromArgb(60, 60, 60);
            _lblProgress.Location = new Point(0, 18);

            _toggleProgress.Location = new Point(_lblProgress.Right + 12, 16);
            _toggleProgress.Checked = false;

            left.Controls.Add(_lblProgress);
            left.Controls.Add(_toggleProgress);
            left.Layout += (_, __) => { _toggleProgress.Location = new Point(_lblProgress.Width + 12, 14); };

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
            StyleIconButton(_btnSettings, "⚙", "Cài đặt");
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
            BuildSettingsPanel();
            BuildProgressOverlay();
            Controls.Add(_settingsOverlay);
            Controls.Add(_progressOverlay);
            _settingsOverlay.BringToFront();

            _root = root;
            _stage = stage;
            _bottom = bottom;
            _leftPanel = left;
            _centerPanel = centerNav;
            _rightPanel = right;
        }

        private void BuildSettingsPanel()
        {
            _settingsOverlay.Visible = false;
            _settingsOverlay.Controls.Clear();

            _settingsPanel.Controls.Clear();
            _settingsPanel.Width = 420;
            _settingsPanel.Height = 420;
            _settingsPanel.BackColor = Color.FromArgb(12, 12, 36);
            _settingsPanel.Padding = new Padding(24, 18, 24, 18);
            _settingsPanel.Radius = 16;
            _settingsPanel.BorderThickness = 0;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                ColumnCount = 1,
                RowCount = 1,
                BackColor = Color.Transparent,
                Padding = new Padding(0)
            };

            var header = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Color.Transparent };
            var lblTitle = new Label
            {
                AutoSize = true,
                Text = "Tùy chọn",
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(0, 6)
            };

            _btnCloseSettings.Text = "✕";
            _btnCloseSettings.Width = 32;
            _btnCloseSettings.Height = 32;
            _btnCloseSettings.FlatStyle = FlatStyle.Flat;
            _btnCloseSettings.FlatAppearance.BorderSize = 0;
            _btnCloseSettings.BackColor = Color.FromArgb(28, 28, 58);
            _btnCloseSettings.ForeColor = Color.White;
            _btnCloseSettings.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            _btnCloseSettings.Cursor = Cursors.Hand;

            header.Controls.Add(lblTitle);
            header.Controls.Add(_btnCloseSettings);
            header.Layout += (_, __) =>
            {
                _btnCloseSettings.Location = new Point(header.Width - _btnCloseSettings.Width - 4, 4);
            };

            layout.Controls.Add(header, 0, layout.RowCount++);
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            layout.Controls.Add(BuildSettingsDivider(), 0, layout.RowCount++);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 12));

            layout.Controls.Add(BuildToggleRow(
                "Theo dõi tiến độ",
                "Sắp xếp các thẻ ghi nhớ của bạn để theo dõi những gì bạn đã biết và những gì đang học. " +
                "Tắt tính năng theo dõi tiến độ nếu bạn muốn nhanh chóng ôn lại các thẻ ghi nhớ.",
                _toggleProgressSettings), 0, layout.RowCount++);
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            layout.Controls.Add(BuildToggleRow(
                "Chỉ học thuật ngữ có gắn sao",
                "",
                _toggleOnlyStarred), 0, layout.RowCount++);
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            layout.Controls.Add(BuildComboRow("Mặt trước", _cmbFront), 0, layout.RowCount++);
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            layout.Controls.Add(BuildSettingsDivider(), 0, layout.RowCount++);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 12));

            layout.Controls.Add(BuildToggleRow(
                "Chuyển văn bản thành lời nói",
                "",
                _toggleTts), 0, layout.RowCount++);
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            layout.Controls.Add(BuildToggleRow(
                "Tự động phát âm thanh",
                "",
                _toggleAutoSpeak), 0, layout.RowCount++);
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            layout.Controls.Add(BuildResetRow(), 0, layout.RowCount++);
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            layout.Controls.Add(BuildFooterRow(), 0, layout.RowCount++);
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _settingsPanel.Controls.Add(layout);
            _settingsOverlay.Controls.Add(_settingsPanel);
            _settingsOverlay.Layout += (_, __) =>
            {
                _settingsPanel.Location = new Point(
                    Math.Max(0, (_settingsOverlay.ClientSize.Width - _settingsPanel.Width) / 2),
                    Math.Max(0, (_settingsOverlay.ClientSize.Height - _settingsPanel.Height) / 2)
                );
            };

            _cmbFront.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbFront.FlatStyle = FlatStyle.Flat;
            _cmbFront.Font = new Font("Segoe UI", 10F, FontStyle.Regular);
            _cmbFront.BackColor = Color.FromArgb(36, 40, 74);
            _cmbFront.ForeColor = Color.White;
            _cmbFront.Width = 220;
            _cmbFront.Items.Clear();
            _cmbFront.Items.Add("Tiếng Trung (Phồn thể)");
            _cmbFront.Items.Add("Tiếng Việt");
            _cmbFront.SelectedIndex = _frontMode == FrontMode.Term ? 0 : 1;

            _toggleProgressSettings.Checked = _progressTrackingEnabled;
            _toggleOnlyStarred.Checked = _onlyStarred;
            _toggleTts.Checked = _ttsEnabled;
            _toggleAutoSpeak.Checked = _autoSpeakEnabled;
        }

        private static Panel BuildSettingsDivider()
        {
            return new Panel
            {
                Dock = DockStyle.Top,
                Height = 1,
                BackColor = Color.FromArgb(35, 35, 70),
                Margin = new Padding(0, 10, 0, 10)
            };
        }

        private static Panel BuildToggleRow(string title, string description, ToggleSwitch toggle)
        {
            var row = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 12, 0, 12)
            };

            var lblTitle = new Label
            {
                AutoSize = true,
                Text = title,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(0, 0)
            };

            toggle.Location = new Point(0, 0);

            row.Controls.Add(lblTitle);
            row.Controls.Add(toggle);

            if (!string.IsNullOrWhiteSpace(description))
            {
                var lblDesc = new Label
                {
                    AutoSize = false,
                    Text = description,
                    Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                    ForeColor = Color.FromArgb(180, 180, 210),
                    Width = 420,
                    Height = 54,
                    Location = new Point(0, 28)
                };
                row.Controls.Add(lblDesc);
                row.Height = 86;
            }

            row.Layout += (_, __) =>
            {
                toggle.Location = new Point(row.Width - toggle.Width, 0);
            };

            return row;
        }

        private static Panel BuildComboRow(string title, ComboBox combo)
        {
            var row = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 14, 0, 14)
            };

            var lblTitle = new Label
            {
                AutoSize = true,
                Text = title,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(0, 8)
            };

            row.Controls.Add(lblTitle);
            row.Controls.Add(combo);

            row.Layout += (_, __) =>
            {
                combo.Location = new Point(row.Width - combo.Width, 2);
            };

            return row;
        }

        private Panel BuildResetRow()
        {
            var row = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 14, 0, 14)
            };

            _lblReset.Text = "Khởi động lại Thẻ ghi nhớ";
            _lblReset.AutoSize = true;
            _lblReset.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            _lblReset.ForeColor = Color.FromArgb(235, 85, 85);
            _lblReset.Cursor = Cursors.Hand;

            row.Controls.Add(_lblReset);
            return row;
        }

        private static Panel BuildFooterRow()
        {
            var row = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 8, 0, 8)
            };

            var lbl = new Label
            {
                AutoSize = true,
                Text = "Chính sách quyền riêng tư",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(150, 150, 190)
            };

            row.Controls.Add(lbl);
            return row;
        }

        private void BuildProgressOverlay()
        {
            _progressOverlay.Dock = DockStyle.Fill;
            _progressOverlay.Visible = false;

            _progressDialog.Width = 760;
            _progressDialog.Height = 360;
            _progressDialog.Radius = 18;
            _progressDialog.BackColor = Color.White;
            _progressDialog.BorderColor = Color.FromArgb(235, 235, 235);
            _progressDialog.BorderThickness = 1;
            _progressDialog.Padding = new Padding(24);

            _lblProgressTitle.Text = "Tiến độ của bạn";
            _lblProgressTitle.Font = new Font("Segoe UI", 18F, FontStyle.Bold);
            _lblProgressTitle.ForeColor = Color.FromArgb(35, 35, 35);
            _lblProgressTitle.AutoSize = true;

            _lblProgressKnown.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            _lblProgressLearning.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            _lblProgressRemaining.Font = new Font("Segoe UI", 11F, FontStyle.Bold);

            _lblProgressKnown.ForeColor = Color.FromArgb(20, 140, 110);
            _lblProgressLearning.ForeColor = Color.FromArgb(210, 120, 60);
            _lblProgressRemaining.ForeColor = Color.FromArgb(80, 90, 120);

            _btnProgressClose.Text = "×";
            _btnProgressClose.Width = 40;
            _btnProgressClose.Height = 40;
            _btnProgressClose.FlatStyle = FlatStyle.Flat;
            _btnProgressClose.FlatAppearance.BorderSize = 0;
            _btnProgressClose.BackColor = Color.White;
            _btnProgressClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(245, 246, 250);
            _btnProgressClose.FlatAppearance.MouseDownBackColor = Color.FromArgb(238, 240, 246);
            _btnProgressClose.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            _btnProgressClose.ForeColor = Color.FromArgb(90, 90, 90);
            _btnProgressClose.Cursor = Cursors.Hand;
            _btnProgressClose.TabStop = false;
            _btnProgressClose.Click += (_, __) => HideProgressOverlay();

            StylePrimaryActionButton(_btnProgressPrimary, "Quay về trang chủ");
            StyleGhostActionButton(_btnProgressSecondary, "Đặt lại Thẻ ghi nhớ");

            _btnProgressPrimary.Click += (_, __) =>
            {
                HideProgressOverlay();
                ExitRequested?.Invoke();
            };

            _btnProgressSecondary.Click += (_, __) =>
            {
                ResetProgress();
                HideProgressOverlay();
            };

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                BackColor = Color.White
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var header = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            header.Controls.Add(_lblProgressTitle);
            header.Controls.Add(_btnProgressClose);
            header.Layout += (_, __) =>
            {
                _lblProgressTitle.Location = new Point(0, 18);
                _btnProgressClose.Location = new Point(header.ClientSize.Width - _btnProgressClose.Width, 0);
            };

            var left = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            left.Controls.Add(_lblProgressKnown);
            left.Controls.Add(_lblProgressLearning);
            left.Controls.Add(_lblProgressRemaining);
            left.Layout += (_, __) =>
            {
                _lblProgressKnown.Location = new Point(0, 10);
                _lblProgressLearning.Location = new Point(0, 50);
                _lblProgressRemaining.Location = new Point(0, 90);
            };

            var right = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            var lblNext = new Label
            {
                Text = "Bước tiếp theo",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Color.FromArgb(35, 35, 35),
                AutoSize = true
            };
            right.Controls.Add(lblNext);
            right.Controls.Add(_btnProgressPrimary);
            right.Controls.Add(_btnProgressSecondary);
            right.Layout += (_, __) =>
            {
                lblNext.Location = new Point(0, 10);
                _btnProgressPrimary.Location = new Point(0, 50);
                _btnProgressSecondary.Location = new Point(0, 110);
            };

            root.Controls.Add(header, 0, 0);
            root.SetColumnSpan(header, 2);
            root.Controls.Add(left, 0, 1);
            root.Controls.Add(right, 1, 1);

            _progressDialog.Controls.Clear();
            _progressDialog.Controls.Add(root);

            _progressOverlay.Controls.Add(_progressDialog);
            Controls.Add(_progressOverlay);

            Layout += (_, __) => CenterProgressDialog();
            _progressOverlay.Layout += (_, __) => CenterProgressDialog();
        }

        private void Wire()
        {
            _btnPrev.Click += (_, __) =>
            {
                if (_progressTrackingEnabled) MarkProgress(ProgressState.Learning);
                else Prev();
            };
            _btnNext.Click += (_, __) =>
            {
                if (_progressTrackingEnabled) MarkProgress(ProgressState.Known);
                else Next();
            };

            // ✅ icon nằm trong card:
            _card.StarIconClicked += (_, __) => ToggleStar();
            _card.PencilIconClicked += (_, __) => EditCurrentCard();
            _card.SoundIconClicked += async (_, __) => await PlaySoundAsync();
            _card.Flipped += (_, __) => HandleCardFlipped();

            // placeholders bottom
            _btnPlay.Click += (_, __) =>
            {
                if (_progressTrackingEnabled) UndoProgress();
            };
            _btnShuffle.Click += (_, __) => ToggleShuffle();
            _btnSettings.Click += (_, __) => ToggleSettingsPanel();
            _btnFullscreen.Click += (_, __) => { /* no logic */ };

            _toggleProgress.CheckedChanged += (_, __) => SetProgressTracking(_toggleProgress.Checked);
            _toggleProgressSettings.CheckedChanged += (_, __) => SetProgressTracking(_toggleProgressSettings.Checked);
            _toggleOnlyStarred.CheckedChanged += (_, __) => SetOnlyStarred(_toggleOnlyStarred.Checked);
            _toggleTts.CheckedChanged += (_, __) => SetTtsEnabled(_toggleTts.Checked);
            _toggleAutoSpeak.CheckedChanged += (_, __) => _autoSpeakEnabled = _toggleAutoSpeak.Checked;
            _cmbFront.SelectedIndexChanged += (_, __) =>
            {
                _frontMode = _cmbFront.SelectedIndex == 1 ? FrontMode.Definition : FrontMode.Term;
                ShowCard();
            };
            _btnCloseSettings.Click += (_, __) => ToggleSettingsPanel();
            _lblReset.Click += (_, __) => ResetProgress();

            // keyboard nav
            this.PreviewKeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right ||
                    e.KeyCode == Keys.Space || e.KeyCode == Keys.J ||
                    e.KeyCode == Keys.L || e.KeyCode == Keys.N)
                    e.IsInputKey = true;
            };
            this.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Left)
                {
                    if (_progressTrackingEnabled) MarkProgress(ProgressState.Learning);
                    else Prev();
                }
                if (e.KeyCode == Keys.Right)
                {
                    if (_progressTrackingEnabled) MarkProgress(ProgressState.Known);
                    else Next();
                }
                if (e.KeyCode == Keys.Space)
                {
                    _card.ToggleFlip();
                    e.Handled = true;
                }
                if (e.KeyCode == Keys.J)
                {
                    if (_progressTrackingEnabled) MarkProgress(ProgressState.Learning);
                    else Prev();
                    e.Handled = true;
                }
                if (e.KeyCode == Keys.L)
                {
                    if (_progressTrackingEnabled) MarkProgress(ProgressState.Known);
                    else Next();
                    e.Handled = true;
                }
                if (e.KeyCode == Keys.N)
                {
                    _ = PlaySoundAsync();
                    e.Handled = true;
                }
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
            _btnPlay.Enabled = enabled;
            _btnShuffle.Enabled = enabled;
        }

        private void ShowCard()
        {
            if (_set?.Items == null || _set.Items.Count == 0 || _view.Count == 0)
            {
                _card.Starred = false;
                _card.SetCard("Chưa có thẻ", "Hãy tạo học phần trước.", "");
                _lblIndex.Text = "0 / 0";
                _btnPrev.Enabled = false;
                _btnNext.Enabled = false;
                return;
            }

            if (_index < 0) _index = 0;
            if (_index >= _view.Count) _index = _view.Count - 1;

            var itemIndex = _view[_index];
            var it = _set.Items[itemIndex];

            _card.Starred = it.IsStarred;

            var front = _frontMode == FrontMode.Term ? it.Term : it.Definition;
            var back = _frontMode == FrontMode.Term ? it.Definition : it.Term;
            var sub = it.Pinyin ?? "";

            _card.SetCard(front, back, sub);

            _lblIndex.Text = $"{_index + 1} / {_view.Count}";

            if (_progressTrackingEnabled)
            {
                _btnPrev.Enabled = _view.Count > 0;
                _btnNext.Enabled = _view.Count > 0;
            }
            else
            {
                _btnPrev.Enabled = _index > 0;
                _btnNext.Enabled = _index < _view.Count - 1;
            }

            AutoSpeakIfNeeded();
        }

        private void Prev()
        {
            if (_set?.Items == null || _view.Count == 0) return;
            if (_index <= 0) return;
            _index--;
            ShowCard();
        }

        private void Next()
        {
            if (_set?.Items == null || _view.Count == 0) return;
            if (_index >= _view.Count - 1)
            {
                ShowProgressOverlay();
                return;
            }
            _index++;
            ShowCard();
        }

        private void ToggleStar()
        {
            if (_set?.Items == null || _view.Count == 0) return;

            var itemIndex = _view[_index];
            var it = _set.Items[itemIndex];
            it.IsStarred = !it.IsStarred;

            PersistSet();
            RebuildView(keepCurrent: true);
            ShowCard();
        }

        private void RebuildView(bool keepCurrent)
        {
            if (_set?.Items == null)
            {
                _view.Clear();
                _order.Clear();
                return;
            }

            var currentItemIndex = _view.Count > 0 && _index >= 0 && _index < _view.Count
                ? _view[_index]
                : (int?)null;

            _order.Clear();
            _order.AddRange(Enumerable.Range(0, _set.Items.Count));

            if (_shuffleEnabled)
                Shuffle(_order);

            _view.Clear();
            if (_onlyStarred)
                _view.AddRange(_order.Where(i => _set.Items[i].IsStarred));
            else
                _view.AddRange(_order);

            if (_view.Count == 0)
            {
                _index = 0;
                return;
            }

            if (keepCurrent && currentItemIndex.HasValue)
            {
                var newIndex = _view.IndexOf(currentItemIndex.Value);
                _index = newIndex >= 0 ? newIndex : Math.Min(_index, _view.Count - 1);
            }
            else
            {
                _index = 0;
            }
        }

        private void Shuffle(List<int> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private void ToggleShuffle()
        {
            _shuffleEnabled = !_shuffleEnabled;
            _undo.Clear();
            RebuildView(keepCurrent: true);
            ShowCard();
        }

        private void SetOnlyStarred(bool enabled)
        {
            _onlyStarred = enabled;
            _undo.Clear();
            RebuildView(keepCurrent: true);
            SetEnabledUI(_view.Count > 0);
            ShowCard();
        }

        private void SetProgressTracking(bool enabled)
        {
            if (_syncingProgressToggle) return;
            _progressTrackingEnabled = enabled;

            _syncingProgressToggle = true;
            _toggleProgress.Checked = enabled;
            _toggleProgressSettings.Checked = enabled;
            _syncingProgressToggle = false;

            ApplyProgressUi();
            ShowCard();
        }

        private void ApplyProgressUi()
        {
            if (_progressTrackingEnabled)
            {
                _btnPrev.Text = "✗";
                _btnNext.Text = "✓";
                _btnPrev.ForeColor = Color.FromArgb(220, 120, 80);
                _btnNext.ForeColor = Color.FromArgb(60, 160, 90);
                _btnPlay.Text = "↶";
                _tt.SetToolTip(_btnPlay, "Hoàn tác");
            }
            else
            {
                _btnPrev.Text = "←";
                _btnNext.Text = "→";
                _btnPrev.ForeColor = Color.FromArgb(60, 60, 60);
                _btnNext.ForeColor = Color.FromArgb(60, 60, 60);
                _btnPlay.Text = "▶";
                _tt.SetToolTip(_btnPlay, "Tự động phát");
            }
        }

        private void MarkProgress(ProgressState state)
        {
            if (_set?.Items == null || _view.Count == 0) return;

            var itemIndex = _view[_index];
            _progress.TryGetValue(itemIndex, out var previousState);

            _progress[itemIndex] = state;
            _undo.Push(new ProgressAction(itemIndex, _index, previousState));

            if (_index < _view.Count - 1)
                _index++;
            else
                ShowProgressOverlay();

            ShowCard();
        }

        private void UndoProgress()
        {
            if (_undo.Count == 0) return;

            var action = _undo.Pop();
            if (action.PreviousState.HasValue)
                _progress[action.ItemIndex] = action.PreviousState.Value;
            else
                _progress.Remove(action.ItemIndex);

            _index = Math.Max(0, Math.Min(action.PreviousIndex, _view.Count - 1));
            ShowCard();
        }

        private void ResetProgress()
        {
            _progress.Clear();
            _undo.Clear();
            _index = 0;
            HideProgressOverlay();
            ShowCard();
        }

        private void ToggleSettingsPanel()
        {
            _settingsOverlay.Visible = !_settingsOverlay.Visible;
            if (_settingsOverlay.Visible)
                _settingsOverlay.BringToFront();
        }

        private void PersistSet()
        {
            if (_set == null) return;
            try
            {
                CardSetStorage.SaveSetJson(_set);
            }
            catch
            {
                // ignore save errors
            }
        }

        public void SetDarkMode(bool enabled)
        {
            _isDarkMode = enabled;

            var bg = enabled ? Color.FromArgb(30, 30, 40) : Color.White;
            var panel = enabled ? Color.FromArgb(30, 30, 40) : Color.White;
            var text = enabled ? Color.FromArgb(220, 220, 230) : Color.FromArgb(60, 60, 60);

            BackColor = bg;
            if (_root != null) _root.BackColor = bg;
            if (_stage != null) _stage.BackColor = panel;
            if (_bottom != null) _bottom.BackColor = panel;
            if (_leftPanel != null) _leftPanel.BackColor = panel;
            if (_centerPanel != null) _centerPanel.BackColor = panel;
            if (_rightPanel != null) _rightPanel.BackColor = panel;

            _lblIndex.ForeColor = text;
            _lblProgress.ForeColor = text;

            _btnPlay.BackColor = panel;
            _btnShuffle.BackColor = panel;
            _btnSettings.BackColor = panel;
            _btnFullscreen.BackColor = panel;

            if (!enabled)
            {
                _btnPlay.BackColor = Color.White;
                _btnShuffle.BackColor = Color.White;
                _btnSettings.BackColor = Color.White;
                _btnFullscreen.BackColor = Color.White;
            }
        }

        private void EditCurrentCard()
        {
            if (_set?.Items == null || _view.Count == 0) return;

            var itemIndex = _view[_index];
            var it = _set.Items[itemIndex];

            using var dlg = new Form
            {
                Text = "Chỉnh sửa thẻ",
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Color.White,
                Width = 520,
                Height = 420,
                Font = new Font("Segoe UI", 9.5F)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 7,
                Padding = new Padding(18)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));

            var lblTerm = new Label { Text = "Thuật ngữ", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
            var txtTerm = new TextBox { Dock = DockStyle.Fill, Text = it.Term ?? "" };

            var lblDef = new Label { Text = "Định nghĩa", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
            var txtDef = new TextBox { Dock = DockStyle.Fill, Multiline = true, Height = 80, Text = it.Definition ?? "" };

            var lblPinyin = new Label { Text = "Pinyin (tùy chọn)", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
            var txtPinyin = new TextBox { Dock = DockStyle.Fill, Text = it.Pinyin ?? "" };

            var btnSave = new Button { Text = "Lưu", Width = 120, Height = 36 };
            var btnCancel = new Button { Text = "Hủy", Width = 120, Height = 36 };

            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            actions.Controls.Add(btnSave);
            actions.Controls.Add(btnCancel);

            layout.Controls.Add(lblTerm, 0, 0);
            layout.Controls.Add(txtTerm, 0, 1);
            layout.Controls.Add(lblDef, 0, 2);
            layout.Controls.Add(txtDef, 0, 3);
            layout.Controls.Add(lblPinyin, 0, 4);
            layout.Controls.Add(txtPinyin, 0, 5);
            layout.Controls.Add(actions, 0, 6);

            dlg.Controls.Add(layout);

            btnCancel.Click += (_, __) => dlg.DialogResult = DialogResult.Cancel;
            btnSave.Click += (_, __) => dlg.DialogResult = DialogResult.OK;

            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            it.Term = txtTerm.Text.Trim();
            it.Definition = txtDef.Text.Trim();
            it.Pinyin = string.IsNullOrWhiteSpace(txtPinyin.Text) ? null : txtPinyin.Text.Trim();

            PersistSet();
            ShowCard();
        }

        private void SetTtsEnabled(bool enabled)
        {
            _ttsEnabled = enabled;
            if (!enabled)
                CancelTts();
        }

        private void AutoSpeakIfNeeded()
        {
            if (!_ttsEnabled || !_autoSpeakEnabled) return;
            if (_progressOverlay.Visible) return;
            if (_frontMode != FrontMode.Term) return;
            if (_card.IsFlipped) return;

            _ = PlaySoundAsync();
        }

        private void HandleCardFlipped()
        {
            if (_card.IsFlipped) return;
            AutoSpeakIfNeeded();
        }

        private async Task PlaySoundAsync()
        {
            if (!_ttsEnabled) return;

            var text = _card.IsFlipped ? _card.BackText : _card.FrontText;
            text = (text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(text)) return;

            CancelTts();
            var cts = new CancellationTokenSource();
            _ttsCts = cts;

            try
            {
                var audio = await GetOrCreateTtsAsync(text, cts.Token);
                if (cts.IsCancellationRequested) return;
                PlayAudio(audio);
            }
            catch
            {
                // ignore TTS errors
            }
        }

        private async Task<byte[]> GetOrCreateTtsAsync(string text, CancellationToken token)
        {
            lock (_ttsLock)
            {
                if (_ttsCache.TryGetValue(text, out var cached))
                    return cached;
            }

            var audio = await Task.Run(() => SynthesizeSpeech(text, token), token);
            lock (_ttsLock)
            {
                if (!_ttsCache.ContainsKey(text))
                    _ttsCache[text] = audio;
            }
            return audio;
        }

        private byte[] SynthesizeSpeech(string text, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            using var synth = new SpeechSynthesizer();
            var voiceName = PickXiaoXiaoVoice(synth);
            if (!string.IsNullOrWhiteSpace(voiceName))
                synth.SelectVoice(voiceName);

            using var ms = new MemoryStream();
            synth.SetOutputToWaveStream(ms);
            synth.Speak(text);
            return ms.ToArray();
        }

        private static string? PickXiaoXiaoVoice(SpeechSynthesizer synth)
        {
            var voices = synth.GetInstalledVoices()
                .Select(v => v.VoiceInfo)
                .ToList();

            var preferred = voices.FirstOrDefault(v =>
                v.Name.Contains("Xiaoxiao", StringComparison.OrdinalIgnoreCase) ||
                v.Name.Contains("zh-CN-Xiaoxiao", StringComparison.OrdinalIgnoreCase));
            if (preferred != null) return preferred.Name;

            var zhCn = voices.FirstOrDefault(v => v.Culture.Name.Equals("zh-CN", StringComparison.OrdinalIgnoreCase));
            if (zhCn != null) return zhCn.Name;

            var zh = voices.FirstOrDefault(v => v.Culture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase));
            return zh?.Name;
        }

        private void PlayAudio(byte[] audio)
        {
            CancelPlaybackOnly();

            var ms = new MemoryStream(audio);
            var reader = new WaveFileReader(ms);
            var output = new WaveOutEvent();

            _ttsStream = reader;
            _ttsOutput = output;

            output.Init(reader);
            output.PlaybackStopped += (_, __) =>
            {
                CancelPlaybackOnly();
            };
            output.Play();
        }

        private void CancelTts()
        {
            lock (_ttsLock)
            {
                _ttsCts?.Cancel();
                _ttsCts?.Dispose();
                _ttsCts = null;
                CancelPlaybackOnly();
            }
        }

        private void CancelPlaybackOnly()
        {
            var output = _ttsOutput;
            _ttsOutput = null;
            output?.Stop();
            output?.Dispose();

            var stream = _ttsStream;
            _ttsStream = null;
            stream?.Dispose();
        }

        private void ShowProgressOverlay()
        {
            if (_progressOverlay.Visible) return;

            int total = _view.Count;
            int known = _progress.Count(x => x.Value == ProgressState.Known);
            int learning = _progress.Count(x => x.Value == ProgressState.Learning);
            int remaining = Math.Max(0, total - known - learning);

            _lblProgressKnown.Text = $"Đã biết {known}";
            _lblProgressLearning.Text = $"Đang học {learning}";
            _lblProgressRemaining.Text = $"Còn lại {remaining}";

            _progressOverlay.Visible = true;
            _progressOverlay.BringToFront();
        }

        private void HideProgressOverlay()
        {
            _progressOverlay.Visible = false;
        }

        private void CenterProgressDialog()
        {
            if (!_progressOverlay.Visible) return;

            _progressDialog.Location = new Point(
                Math.Max(0, (ClientSize.Width - _progressDialog.Width) / 2),
                Math.Max(0, (ClientSize.Height - _progressDialog.Height) / 2)
            );
        }

        private void LoadLegacyStarred()
        {
            if (_set?.Items == null || _set.Items.Count == 0) return;

            if (_set.Items.Any(item => item.IsStarred)) return;

            try
            {
                var dir = CardSetStorage.GetSetDirectory(_set.Id);
                var path = Path.Combine(dir, "starred.json");
                if (!File.Exists(path)) return;

                var json = File.ReadAllText(path);
                var arr = JsonSerializer.Deserialize<int[]>(json) ?? Array.Empty<int>();
                foreach (var idx in arr.Where(i => i >= 0 && i < _set.Items.Count))
                    _set.Items[idx].IsStarred = true;

                PersistSet();
            }
            catch
            {
                // ignore legacy errors
            }
        }

        private enum FrontMode
        {
            Term,
            Definition
        }

        private enum ProgressState
        {
            Learning,
            Known
        }

        private readonly struct ProgressAction
        {
            public ProgressAction(int itemIndex, int previousIndex, ProgressState? previousState)
            {
                ItemIndex = itemIndex;
                PreviousIndex = previousIndex;
                PreviousState = previousState;
            }

            public int ItemIndex { get; }
            public int PreviousIndex { get; }
            public ProgressState? PreviousState { get; }
        }

        private sealed class SettingsOverlayPanel : Panel
        {
            public SettingsOverlayPanel()
            {
                Dock = DockStyle.Fill;
                BackColor = Color.Transparent;
                SetStyle(ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.UserPaint, true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                using var b = new SolidBrush(Color.FromArgb(160, 4, 4, 12));
                e.Graphics.FillRectangle(b, ClientRectangle);
                base.OnPaint(e);
            }
        }

        private sealed class OverlayPanel : Panel
        {
            public OverlayPanel()
            {
                Dock = DockStyle.Fill;
                BackColor = Color.Transparent;
                SetStyle(ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.UserPaint, true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                using var b = new SolidBrush(Color.FromArgb(160, 4, 4, 12));
                e.Graphics.FillRectangle(b, ClientRectangle);
                base.OnPaint(e);
            }
        }

        private sealed class RoundedPanel : Panel
        {
            public int Radius { get; set; } = 14;
            public Color BorderColor { get; set; } = Color.FromArgb(230, 230, 230);
            public int BorderThickness { get; set; } = 1;

            public RoundedPanel()
            {
                DoubleBuffered = true;
            }

            protected override void OnResize(EventArgs eventargs)
            {
                base.OnResize(eventargs);
                using var path = BuildRoundedPath(ClientRectangle, Radius);
                Region = new Region(path);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = BuildRoundedPath(ClientRectangle, Radius);
                using var brush = new SolidBrush(BackColor);
                e.Graphics.FillPath(brush, path);

                if (BorderThickness > 0)
                {
                    using var pen = new Pen(BorderColor, BorderThickness);
                    e.Graphics.DrawPath(pen, path);
                }

                base.OnPaint(e);
            }

            private static GraphicsPath BuildRoundedPath(Rectangle rect, int radius)
            {
                var path = new GraphicsPath();
                int d = radius * 2;
                path.AddArc(rect.X, rect.Y, d, d, 180, 90);
                path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
                path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
                path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                return path;
            }
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

        private static void StylePrimaryActionButton(Button b, string text)
        {
            b.Text = text;
            b.Width = 240;
            b.Height = 46;
            b.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
            b.BackColor = Color.FromArgb(76, 146, 245);
            b.ForeColor = Color.White;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.Cursor = Cursors.Hand;
        }

        private static void StyleGhostActionButton(Button b, string text)
        {
            b.Text = text;
            b.Width = 240;
            b.Height = 46;
            b.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
            b.BackColor = Color.FromArgb(235, 238, 245);
            b.ForeColor = Color.FromArgb(50, 60, 90);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.Cursor = Cursors.Hand;
        }
    }
}
