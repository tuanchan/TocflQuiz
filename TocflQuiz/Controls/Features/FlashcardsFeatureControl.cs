using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Media;
using System.Text.Json;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;
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
        private int _lastCardIndex = -1;
        private List<int> _order = new();
        private List<int> _filteredOrder = new();
        private List<int> _shuffleOrder = new();
        private bool _shuffleEnabled;
        private bool _progressTracking;
        private bool _starredOnly;
        private bool _ttsEnabled = true;
        private bool _autoPronounce;
        private bool _isDarkMode;
        private bool _completionShown;
        private FrontSideOption _frontSide = FrontSideOption.Term;

        private readonly Dictionary<int, CardProgressState> _progressMap = new();
        private readonly Stack<ProgressAction> _undoStack = new();

        private readonly Dictionary<string, byte[]> _ttsCache = new(StringComparer.Ordinal);
        private CancellationTokenSource? _ttsCts;
        private SoundPlayer? _soundPlayer;
        private MemoryStream? _currentSoundStream;

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

        private readonly Panel _settingsPanel = new();
        private readonly ToggleSwitch _toggleProgressOpt = new();
        private readonly ToggleSwitch _toggleStarredOnly = new();
        private readonly ToggleSwitch _toggleTts = new();
        private readonly ToggleSwitch _toggleAutoPronounce = new();
        private readonly ComboBox _cmbFrontSide = new();
        private readonly Label _lblReset = new();
        private readonly Button _btnCloseSettings = new();

        private readonly OverlayPanel _completionOverlay = new();
        private readonly RoundedPanel _completionDialog = new();
        private readonly Label _completionTitle = new();
        private readonly Label _completionSubtitle = new();
        private readonly Label _completionKnown = new();
        private readonly Label _completionLearning = new();
        private readonly Label _completionRemaining = new();
        private readonly Button _completionClose = new();
        private readonly RoundedButton _completionReset = new();
        private readonly RoundedButton _completionDismiss = new();

        private TableLayoutPanel? _root;
        private Panel? _stage;
        private TableLayoutPanel? _center;
        private TableLayoutPanel? _bottom;
        private Panel? _leftPanel;
        private Panel? _centerNavPanel;
        private Panel? _rightPanel;
        private Label? _lblProgressText;

        private enum CardProgressState
        {
            None,
            Learning,
            Known
        }

        private enum FrontSideOption
        {
            Term,
            Definition,
            Pinyin
        }

        private static readonly Color LightBackground = Color.White;
        private static readonly Color LightTextPrimary = Color.FromArgb(60, 60, 60);
        private static readonly Color LightTextSecondary = Color.FromArgb(90, 90, 90);
        private static readonly Color LightBorder = Color.FromArgb(225, 225, 225);

        private static readonly Color DarkBackground = Color.FromArgb(40, 40, 50);
        private static readonly Color DarkPageBackground = Color.FromArgb(30, 30, 40);
        private static readonly Color DarkBorder = Color.FromArgb(60, 60, 70);
        private static readonly Color DarkTextPrimary = Color.FromArgb(220, 220, 230);
        private static readonly Color DarkTextSecondary = Color.FromArgb(160, 160, 170);

        private sealed class ProgressAction
        {
            public int CardIndex { get; set; }
            public CardProgressState PreviousState { get; set; }
        }

        public FlashcardsFeatureControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9F);

            BuildUi();
            Wire();
            ApplyTheme();

            SetEnabledUI(false);
        }

        public void LoadSet(CardSet set)
        {
            _set = set;
            _index = 0;
            _lastCardIndex = -1;
            _progressMap.Clear();
            _undoStack.Clear();
            _shuffleOrder.Clear();
            _completionShown = false;

            ApplyLegacyStarred();
            RebuildOrder(false);
            UpdateShuffleUi();
            UpdateProgressUi();
            SetEnabledUI(_set.Items != null && _set.Items.Count > 0);
        }

        public void SetDarkMode(bool isDark)
        {
            _isDarkMode = isDark;
            ApplyTheme();
        }

        private void BuildUi()
        {
            _root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = LightBackground,
                Padding = new Padding(0),
                Margin = new Padding(0),
                ColumnCount = 1,
                RowCount = 2
            };
            _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));

            // ===== Stage =====
            _stage = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = LightBackground
            };

            _center = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 1,
                BackColor = LightBackground,
                Padding = new Padding(60, 36, 60, 16)
            };
            _center.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _center.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _card.Dock = DockStyle.Fill;
            _card.Margin = new Padding(0);

            _center.Controls.Add(_card, 0, 0);
            _stage.Controls.Add(_center);

            // ===== Bottom bar =====
            _bottom = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = LightBackground,
                Padding = new Padding(18, 8, 18, 10),
                ColumnCount = 3,
                RowCount = 1
            };
            _bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            _bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            _bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));

            // left
            _leftPanel = new Panel { Dock = DockStyle.Fill, BackColor = LightBackground };
            _lblProgressText = new Label
            {
                AutoSize = true,
                Text = "Theo dõi tiến độ",
                Font = new Font("Segoe UI", 10F, FontStyle.Regular),
                ForeColor = LightTextPrimary,
                Location = new Point(0, 18)
            };
            _toggleProgress.Location = new Point(_lblProgressText.Right + 12, 16);
            _toggleProgress.Checked = _progressTracking;

            _leftPanel.Controls.Add(_lblProgressText);
            _leftPanel.Controls.Add(_toggleProgress);
            _leftPanel.Layout += (_, __) => { _toggleProgress.Location = new Point(_lblProgressText.Width + 12, 14); };

            // center nav
            _centerNavPanel = new Panel { Dock = DockStyle.Fill, BackColor = LightBackground };
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

            _centerNavPanel.Controls.Add(nav);
            _centerNavPanel.Layout += (_, __) =>
            {
                nav.Location = new Point(
                    (_centerNavPanel.ClientSize.Width - nav.Width) / 2,
                    (_centerNavPanel.ClientSize.Height - nav.Height) / 2
                );
            };

            // right actions
            _rightPanel = new Panel { Dock = DockStyle.Fill, BackColor = LightBackground };
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

            _rightPanel.Controls.Add(actions);
            _rightPanel.Layout += (_, __) => { actions.Location = new Point(_rightPanel.ClientSize.Width - actions.Width, 10); };

            _bottom.Controls.Add(_leftPanel, 0, 0);
            _bottom.Controls.Add(_centerNavPanel, 1, 0);
            _bottom.Controls.Add(_rightPanel, 2, 0);

            _root.Controls.Add(_stage, 0, 0);
            _root.Controls.Add(_bottom, 0, 1);

            Controls.Clear();
            Controls.Add(_root);

            BuildSettingsPanel(_root);
            BuildCompletionOverlay(_root);
            UpdateProgressUi();
            UpdateShuffleUi();
        }

        private void ApplyTheme()
        {
            var pageBg = _isDarkMode ? DarkPageBackground : LightBackground;
            var panelBg = pageBg;
            var buttonBg = _isDarkMode ? Color.FromArgb(45, 45, 58) : Color.FromArgb(248, 248, 248);
            var iconButtonBg = _isDarkMode ? DarkBackground : Color.White;
            var iconButtonText = _isDarkMode ? DarkTextSecondary : LightTextPrimary;

            BackColor = pageBg;
            if (_root != null) _root.BackColor = pageBg;
            if (_stage != null) _stage.BackColor = panelBg;
            if (_center != null) _center.BackColor = panelBg;
            if (_bottom != null) _bottom.BackColor = panelBg;
            if (_leftPanel != null) _leftPanel.BackColor = panelBg;
            if (_centerNavPanel != null) _centerNavPanel.BackColor = panelBg;
            if (_rightPanel != null) _rightPanel.BackColor = panelBg;

            if (_lblProgressText != null)
                _lblProgressText.ForeColor = _isDarkMode ? DarkTextSecondary : LightTextPrimary;

            _lblIndex.ForeColor = _isDarkMode ? DarkTextPrimary : Color.FromArgb(70, 70, 70);

            ApplyNavButtonTheme(_btnPrev, buttonBg);
            ApplyNavButtonTheme(_btnNext, buttonBg);

            _btnPlay.BackColor = iconButtonBg;
            _btnShuffle.BackColor = iconButtonBg;
            _btnSettings.BackColor = iconButtonBg;
            _btnFullscreen.BackColor = iconButtonBg;

            _btnPlay.ForeColor = iconButtonText;
            _btnSettings.ForeColor = iconButtonText;
            _btnFullscreen.ForeColor = iconButtonText;

            _toggleProgress.OnBackColor = Color.FromArgb(90, 120, 255);
            _toggleProgress.OffBackColor = _isDarkMode ? Color.FromArgb(70, 70, 80) : Color.FromArgb(210, 210, 210);
            _toggleProgress.KnobColor = Color.White;

            _toggleProgressOpt.OnBackColor = _toggleProgress.OnBackColor;
            _toggleProgressOpt.OffBackColor = _toggleProgress.OffBackColor;
            _toggleProgressOpt.KnobColor = _toggleProgress.KnobColor;

            _toggleStarredOnly.OnBackColor = _toggleProgress.OnBackColor;
            _toggleStarredOnly.OffBackColor = _toggleProgress.OffBackColor;
            _toggleStarredOnly.KnobColor = _toggleProgress.KnobColor;

            _toggleTts.OnBackColor = _toggleProgress.OnBackColor;
            _toggleTts.OffBackColor = _toggleProgress.OffBackColor;
            _toggleTts.KnobColor = _toggleProgress.KnobColor;

            _toggleAutoPronounce.OnBackColor = _toggleProgress.OnBackColor;
            _toggleAutoPronounce.OffBackColor = _toggleProgress.OffBackColor;
            _toggleAutoPronounce.KnobColor = _toggleProgress.KnobColor;

            _cmbFrontSide.BackColor = _isDarkMode ? Color.FromArgb(34, 34, 58) : Color.White;
            _cmbFrontSide.ForeColor = _isDarkMode ? Color.White : Color.FromArgb(40, 40, 40);

            _settingsPanel.BackColor = Color.FromArgb(20, 20, 40);
            _btnCloseSettings.ForeColor = Color.White;

            _card.SetDarkMode(_isDarkMode);

            ApplyCompletionTheme();

            UpdateProgressUi();
            UpdateShuffleUi();
        }

        private void ApplyNavButtonTheme(Button button, Color background)
        {
            button.BackColor = background;
            button.FlatAppearance.BorderColor = background;
        }

        private void Wire()
        {
            _btnPrev.Click += (_, __) => HandlePrevAction();
            _btnNext.Click += (_, __) => HandleNextAction();

            // ✅ icon nằm trong card:
            _card.StarIconClicked += (_, __) => ToggleStar();
            _card.PencilIconClicked += (_, __) => EditCurrentCard();
            _card.SoundIconClicked += async (_, __) => await PlaySoundAsync();
            _card.FlipRequested += async (_, __) => await HandleAutoPronounceFlipAsync();

            // placeholders bottom
            _btnPlay.Click += (_, __) => HandlePlayAction();
            _btnShuffle.Click += (_, __) => ToggleShuffle();
            _btnSettings.Click += (_, __) => ToggleSettingsPanel();
            _btnFullscreen.Click += (_, __) => { /* no logic */ };

            _toggleProgress.CheckedChanged += (_, __) => SetProgressTracking(_toggleProgress.Checked);
            _toggleProgressOpt.CheckedChanged += (_, __) => SetProgressTracking(_toggleProgressOpt.Checked);
            _toggleStarredOnly.CheckedChanged += (_, __) =>
            {
                _starredOnly = _toggleStarredOnly.Checked;
                RebuildOrder(true);
            };
            _toggleTts.CheckedChanged += (_, __) => _ttsEnabled = _toggleTts.Checked;
            _toggleAutoPronounce.CheckedChanged += (_, __) => _autoPronounce = _toggleAutoPronounce.Checked;
            _cmbFrontSide.SelectedIndexChanged += (_, __) =>
            {
                _frontSide = (FrontSideOption)_cmbFrontSide.SelectedIndex;
                ShowCard();
            };
            _lblReset.Click += (_, __) => ResetProgress();
            _btnCloseSettings.Click += (_, __) => _settingsPanel.Visible = false;

            // keyboard nav
            this.PreviewKeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Left ||
                    e.KeyCode == Keys.Right ||
                    e.KeyCode == Keys.Space ||
                    e.KeyCode == Keys.J ||
                    e.KeyCode == Keys.L ||
                    e.KeyCode == Keys.N)
                {
                    e.IsInputKey = true;
                }
            };
            this.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Left) Prev();
                if (e.KeyCode == Keys.Right) Next();
                if (e.KeyCode == Keys.Space)
                {
                    _card.ToggleFlip();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
                if (e.KeyCode == Keys.N) _ = PlaySoundAsync();
                if (e.KeyCode == Keys.J)
                {
                    if (_progressTracking)
                        HandlePrevAction();
                    else
                        Prev();
                }
                if (e.KeyCode == Keys.L)
                {
                    if (_progressTracking)
                        HandleNextAction();
                    else
                        Next();
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
            _btnSettings.Enabled = enabled;
            _btnFullscreen.Enabled = enabled;
            _toggleProgress.Enabled = enabled;
            _toggleProgressOpt.Enabled = enabled;
            _toggleStarredOnly.Enabled = enabled;
            _toggleTts.Enabled = enabled;
            _toggleAutoPronounce.Enabled = enabled;
            _cmbFrontSide.Enabled = enabled;
        }

        private void ShowCard()
        {
            if (_set?.Items == null || _set.Items.Count == 0 || _order.Count == 0)
            {
                _card.Starred = false;
                if (_set?.Items == null || _set.Items.Count == 0)
                    _card.SetCard("Chưa có thẻ", "Hãy tạo học phần trước.", "");
                else
                    _card.SetCard("Không có thẻ", "Không có thẻ phù hợp với bộ lọc hiện tại.", "");
                _lblIndex.Text = "0 / 0";
                _btnPrev.Enabled = false;
                _btnNext.Enabled = false;
                _completionShown = false;
                HideCompletionOverlay();
                return;
            }

            if (_index < 0) _index = 0;
            if (_index >= _order.Count) _index = _order.Count - 1;

            var itemIndex = _order[_index];
            var it = _set.Items[itemIndex];

            _card.Starred = it.IsStarred;

            var front = GetFrontText(it);
            var back = GetBackText(it);
            var sub = GetSubText(it);

            _card.SetCard(front, back, sub);
            var cardChanged = itemIndex != _lastCardIndex;
            _lastCardIndex = itemIndex;

            _lblIndex.Text = $"{_index + 1} / {_order.Count}";

            if (_progressTracking)
            {
                _btnPrev.Enabled = _order.Count > 0;
                _btnNext.Enabled = _order.Count > 0;
            }
            else
            {
                _btnPrev.Enabled = _index > 0;
                _btnNext.Enabled = _index < _order.Count - 1;
            }

            if (_autoPronounce && cardChanged)
            {
                _ = PlayChineseTermAsync();
            }

            CheckCompletion();
        }

        private void Prev()
        {
            if (_set?.Items == null || _order.Count == 0) return;
            if (_index <= 0) return;
            _index--;
            ShowCard();
        }

        private void Next()
        {
            if (_set?.Items == null || _order.Count == 0) return;
            if (_index >= _order.Count - 1) return;
            _index++;
            ShowCard();
        }

        private void ToggleStar()
        {
            if (_set?.Items == null || _set.Items.Count == 0 || _order.Count == 0) return;

            var itemIndex = _order[_index];
            var item = _set.Items[itemIndex];
            item.IsStarred = !item.IsStarred;

            CardSetStorage.SaveSetJson(_set);

            RebuildOrder(true);
        }

        private void HandlePrevAction()
        {
            if (_progressTracking)
            {
                MarkProgress(CardProgressState.Learning);
                return;
            }

            Prev();
        }

        private void HandleNextAction()
        {
            if (_progressTracking)
            {
                MarkProgress(CardProgressState.Known);
                return;
            }

            Next();
        }

        private void HandlePlayAction()
        {
            if (_progressTracking)
            {
                UndoProgress();
                return;
            }
        }

        private void ToggleShuffle()
        {
            _shuffleEnabled = !_shuffleEnabled;
            if (_shuffleEnabled)
            {
                BuildShuffleOrder();
                _index = 0;
                _lastCardIndex = -1;
                _completionShown = false;
                RebuildOrder(false);
            }
            else
            {
                _shuffleOrder.Clear();
                RebuildOrder(true);
            }
            UpdateShuffleUi();
        }

        private void SetProgressTracking(bool enabled)
        {
            _progressTracking = enabled;
            if (_toggleProgress.Checked != enabled) _toggleProgress.Checked = enabled;
            if (_toggleProgressOpt.Checked != enabled) _toggleProgressOpt.Checked = enabled;
            UpdateProgressUi();
        }

        private void UpdateProgressUi()
        {
            if (_progressTracking)
            {
                _btnPrev.Text = "✕";
                _btnNext.Text = "✓";
                _btnPlay.Text = "↶";
                _btnPrev.ForeColor = _isDarkMode ? Color.FromArgb(235, 120, 120) : Color.FromArgb(200, 74, 74);
                _btnNext.ForeColor = _isDarkMode ? Color.FromArgb(110, 210, 150) : Color.FromArgb(46, 140, 90);
                _btnPlay.ForeColor = _isDarkMode ? DarkTextSecondary : Color.FromArgb(60, 60, 60);
                _tt.SetToolTip(_btnPrev, "Đang học");
                _tt.SetToolTip(_btnNext, "Đã thuộc");
                _tt.SetToolTip(_btnPlay, "Hoàn tác");
            }
            else
            {
                _btnPrev.Text = "←";
                _btnNext.Text = "→";
                _btnPlay.Text = "▶";
                _btnPrev.ForeColor = _isDarkMode ? DarkTextSecondary : Color.FromArgb(60, 60, 60);
                _btnNext.ForeColor = _isDarkMode ? DarkTextSecondary : Color.FromArgb(60, 60, 60);
                _btnPlay.ForeColor = _isDarkMode ? DarkTextSecondary : Color.FromArgb(60, 60, 60);
                _tt.SetToolTip(_btnPrev, "Thẻ trước");
                _tt.SetToolTip(_btnNext, "Thẻ sau");
                _tt.SetToolTip(_btnPlay, "Tự động phát");
            }
        }

        private void UpdateShuffleUi()
        {
            var offColor = _isDarkMode ? DarkTextSecondary : Color.FromArgb(60, 60, 60);
            _btnShuffle.ForeColor = _shuffleEnabled ? Color.FromArgb(76, 146, 245) : offColor;
        }

        private void MarkProgress(CardProgressState state)
        {
            if (_set?.Items == null || _order.Count == 0) return;

            var itemIndex = _order[_index];
            var prevState = _progressMap.TryGetValue(itemIndex, out var existing) ? existing : CardProgressState.None;
            _progressMap[itemIndex] = state;
            _undoStack.Push(new ProgressAction
            {
                CardIndex = itemIndex,
                PreviousState = prevState
            });

            if (_index < _order.Count - 1) _index++;
            ShowCard();
        }

        private void UndoProgress()
        {
            if (_undoStack.Count == 0) return;

            var action = _undoStack.Pop();
            if (action.PreviousState == CardProgressState.None)
                _progressMap.Remove(action.CardIndex);
            else
                _progressMap[action.CardIndex] = action.PreviousState;

            var newPos = _order.IndexOf(action.CardIndex);
            if (newPos >= 0) _index = newPos;
            ShowCard();
        }

        private void ResetProgress()
        {
            _progressMap.Clear();
            _undoStack.Clear();
            _index = 0;
            _lastCardIndex = -1;
            _completionShown = false;
            ShowCard();
        }

        private void RebuildOrder(bool preserveCurrent)
        {
            if (_set?.Items == null)
            {
                _order = new List<int>();
                _filteredOrder = new List<int>();
                _shuffleOrder = new List<int>();
                ShowCard();
                return;
            }

            int? currentItem = preserveCurrent && _order.Count > 0 ? _order[_index] : null;

            if (_shuffleEnabled && _shuffleOrder.Count != _set.Items.Count)
            {
                BuildShuffleOrder();
            }

            var baseOrder = _shuffleEnabled && _shuffleOrder.Count == _set.Items.Count
                ? _shuffleOrder
                : Enumerable.Range(0, _set.Items.Count).ToList();

            _filteredOrder = baseOrder
                .Where(i => !_starredOnly || _set.Items[i].IsStarred)
                .ToList();

            _order = new List<int>(_filteredOrder);

            if (currentItem.HasValue)
            {
                var pos = _order.IndexOf(currentItem.Value);
                _index = pos >= 0 ? pos : 0;
            }
            else
            {
                _index = 0;
            }

            ShowCard();
        }

        private void BuildShuffleOrder()
        {
            if (_set?.Items == null) return;
            _shuffleOrder = Enumerable.Range(0, _set.Items.Count).ToList();
            Shuffle(_shuffleOrder);
        }

        private static void Shuffle(List<int> list)
        {
            var rng = new Random();
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private string GetFrontText(CardItem item)
        {
            return _frontSide switch
            {
                FrontSideOption.Definition => item.Definition ?? "",
                FrontSideOption.Pinyin => item.Pinyin ?? "",
                _ => item.Term ?? ""
            };
        }

        private string GetBackText(CardItem item)
        {
            return _frontSide switch
            {
                FrontSideOption.Definition => item.Term ?? "",
                FrontSideOption.Pinyin => item.Definition ?? "",
                _ => item.Definition ?? ""
            };
        }

        private string GetSubText(CardItem item)
        {
            return _frontSide == FrontSideOption.Pinyin ? (item.Term ?? "") : (item.Pinyin ?? "");
        }

        private void ApplyLegacyStarred()
        {
            if (_set?.Items == null || _set.Items.Count == 0) return;

            var path = LegacyStarFilePath();
            if (!File.Exists(path)) return;

            try
            {
                var json = File.ReadAllText(path);
                var arr = JsonSerializer.Deserialize<int[]>(json) ?? Array.Empty<int>();
                foreach (var idx in arr.Where(i => i >= 0 && i < _set.Items.Count))
                {
                    _set.Items[idx].IsStarred = true;
                }

                CardSetStorage.SaveSetJson(_set);
            }
            catch
            {
            }
        }

        private string GetSetDir()
        {
            var id = _set?.Id;
            if (string.IsNullOrWhiteSpace(id)) id = "unknown_set";
            var safe = MakeSafeFileName(id);
            return Path.Combine(CardSetStorage.BaseDir, safe);
        }

        private string LegacyStarFilePath() => Path.Combine(GetSetDir(), "starred.json");

        private static string MakeSafeFileName(string s)
        {
            s ??= "";
            foreach (var c in Path.GetInvalidFileNameChars())
                s = s.Replace(c, '_');
            return s.Trim();
        }

        private async Task PlaySoundAsync()
        {
            if (!_ttsEnabled) return;
            if (_set?.Items == null || _order.Count == 0) return;

            var itemIndex = _order[_index];
            var item = _set.Items[itemIndex];
            var text = _card.IsFlipped ? GetBackText(item) : GetFrontText(item);

            await PlayTextAsync(text);
        }

        private async Task PlayChineseTermAsync()
        {
            if (!_ttsEnabled) return;
            if (_set?.Items == null || _order.Count == 0) return;

            var itemIndex = _order[_index];
            var item = _set.Items[itemIndex];
            var text = item.Term ?? "";

            await PlayTextAsync(text);
        }

        private async Task PlayTextAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            _ttsCts?.Cancel();
            _ttsCts = new CancellationTokenSource();
            var token = _ttsCts.Token;

            _soundPlayer?.Stop();

            try
            {
                var audio = await GetOrCreateAudioAsync(text, token);
                if (token.IsCancellationRequested) return;

                BeginInvoke(new Action(() =>
                {
                    if (token.IsCancellationRequested) return;
                    _soundPlayer?.Stop();
                    _currentSoundStream?.Dispose();
                    _currentSoundStream = new MemoryStream(audio);
                    _soundPlayer = new SoundPlayer(_currentSoundStream);
                    _soundPlayer.Load();
                    _soundPlayer.Play();
                }));
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task HandleAutoPronounceFlipAsync()
        {
            if (!_autoPronounce) return;
            await PlayChineseTermAsync();
        }

        private Task<byte[]> GetOrCreateAudioAsync(string text, CancellationToken token)
        {
            if (_ttsCache.TryGetValue(text, out var cached))
                return Task.FromResult(cached);

            return Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                using var synth = new SpeechSynthesizer();
                SelectVoiceForTts(synth);
                using var ms = new MemoryStream();
                synth.SetOutputToWaveStream(ms);
                synth.Speak(text);
                var data = ms.ToArray();
                _ttsCache[text] = data;
                return data;
            }, token);
        }

        private static void SelectVoiceForTts(SpeechSynthesizer synth)
        {
            var voice = synth.GetInstalledVoices()
                .Select(v => v.VoiceInfo)
                .FirstOrDefault(v => v.Name.Contains("Xiaoxiao", StringComparison.OrdinalIgnoreCase));

            if (voice != null)
            {
                synth.SelectVoice(voice.Name);
                return;
            }

            try
            {
                synth.SelectVoiceByHints(VoiceGender.Female, VoiceAge.Adult, 0, new CultureInfo("zh-CN"));
            }
            catch
            {
            }
        }

        private void ToggleSettingsPanel()
        {
            if (!_settingsPanel.Visible)
            {
                _toggleProgressOpt.Checked = _progressTracking;
                _toggleStarredOnly.Checked = _starredOnly;
                _toggleTts.Checked = _ttsEnabled;
                _toggleAutoPronounce.Checked = _autoPronounce;
                _cmbFrontSide.SelectedIndex = (int)_frontSide;
            }

            _settingsPanel.Visible = !_settingsPanel.Visible;
            _settingsPanel.BringToFront();
        }

        private void BuildSettingsPanel(Control root)
        {
            _settingsPanel.Visible = false;
            _settingsPanel.Size = new Size(420, 520);
            _settingsPanel.BackColor = Color.FromArgb(20, 20, 40);
            _settingsPanel.Padding = new Padding(22);
            _settingsPanel.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            _settingsPanel.Layout += (_, __) =>
            {
                _settingsPanel.Location = new Point(
                    Math.Max(0, root.ClientSize.Width - _settingsPanel.Width - 24),
                    24
                );
            };
            root.Resize += (_, __) =>
            {
                _settingsPanel.Location = new Point(
                    Math.Max(0, root.ClientSize.Width - _settingsPanel.Width - 24),
                    24
                );
            };

            _btnCloseSettings.Text = "✕";
            _btnCloseSettings.FlatStyle = FlatStyle.Flat;
            _btnCloseSettings.FlatAppearance.BorderSize = 0;
            _btnCloseSettings.Size = new Size(34, 34);
            _btnCloseSettings.ForeColor = Color.White;
            _btnCloseSettings.BackColor = Color.Transparent;
            _btnCloseSettings.Cursor = Cursors.Hand;
            _btnCloseSettings.Font = new Font("Segoe UI", 11F, FontStyle.Bold);

            var lblTitle = new Label
            {
                Text = "Tùy chọn",
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(0, 0)
            };

            _btnCloseSettings.Location = new Point(_settingsPanel.Width - _btnCloseSettings.Width - 6, -2);

            var list = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Location = new Point(0, 50),
                Size = new Size(_settingsPanel.Width - 10, _settingsPanel.Height - 70),
                BackColor = Color.Transparent
            };

            list.ControlAdded += (_, __) =>
            {
                foreach (Control c in list.Controls)
                {
                    c.Margin = new Padding(0, 0, 0, 18);
                }
            };

            var rowProgress = BuildToggleRow(
                "Theo dõi tiến độ",
                "Sắp xếp các thẻ ghi nhớ của bạn để theo dõi những gì bạn đã biết và những gì đang học.",
                _toggleProgressOpt
            );

            var rowStarred = BuildToggleRow(
                "Chỉ học thuật ngữ có gắn sao",
                null,
                _toggleStarredOnly
            );

            var rowFront = BuildDropdownRow("Mặt trước", _cmbFrontSide);
            _cmbFrontSide.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbFrontSide.Items.Clear();
            _cmbFrontSide.Items.AddRange(new object[]
            {
                "Tiếng Trung (Phồn thể)",
                "Tiếng Việt",
                "Pinyin"
            });
            _cmbFrontSide.SelectedIndex = (int)_frontSide;

            var rowTts = BuildToggleRow(
                "Chuyển văn bản thành lời nói",
                null,
                _toggleTts
            );

            var rowAutoPronounce = BuildToggleRow(
                "Tự động phát âm",
                "Tự động đọc thuật ngữ tiếng Trung khi đổi thẻ hoặc lật thẻ.",
                _toggleAutoPronounce
            );

            var rowReset = BuildActionRow("Khởi động lại Thẻ ghi nhớ", _lblReset);

            list.Controls.Add(rowProgress);
            list.Controls.Add(rowStarred);
            list.Controls.Add(rowFront);
            list.Controls.Add(rowTts);
            list.Controls.Add(rowAutoPronounce);
            list.Controls.Add(rowReset);

            _toggleProgressOpt.Checked = _progressTracking;
            _toggleStarredOnly.Checked = _starredOnly;
            _toggleTts.Checked = _ttsEnabled;
            _toggleAutoPronounce.Checked = _autoPronounce;

            _settingsPanel.Controls.Add(lblTitle);
            _settingsPanel.Controls.Add(_btnCloseSettings);
            _settingsPanel.Controls.Add(list);

            root.Controls.Add(_settingsPanel);
            _settingsPanel.BringToFront();
        }

        private Control BuildToggleRow(string title, string? description, ToggleSwitch toggle)
        {
            var panel = new Panel
            {
                Width = _settingsPanel.Width - 30,
                Height = description == null ? 40 : 78,
                BackColor = Color.Transparent
            };

            var lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(0, 0)
            };

            toggle.Location = new Point(panel.Width - toggle.Width - 6, 4);

            panel.Controls.Add(lblTitle);
            panel.Controls.Add(toggle);

            if (!string.IsNullOrWhiteSpace(description))
            {
                var lblDesc = new Label
                {
                    Text = description,
                    Font = new Font("Segoe UI", 9F),
                    ForeColor = Color.FromArgb(180, 190, 210),
                    AutoSize = false,
                    Width = panel.Width - 20,
                    Height = 36,
                    Location = new Point(0, 24)
                };
                panel.Controls.Add(lblDesc);
            }

            panel.Layout += (_, __) =>
            {
                toggle.Location = new Point(panel.Width - toggle.Width - 6, 4);
            };

            return panel;
        }

        private Control BuildDropdownRow(string title, ComboBox combo)
        {
            var panel = new Panel
            {
                Width = _settingsPanel.Width - 30,
                Height = 44,
                BackColor = Color.Transparent
            };

            var lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(0, 10)
            };

            combo.Width = 200;
            combo.Height = 30;
            combo.Location = new Point(panel.Width - combo.Width - 6, 6);
            combo.BackColor = Color.FromArgb(34, 34, 58);
            combo.ForeColor = Color.White;
            combo.FlatStyle = FlatStyle.Flat;

            panel.Controls.Add(lblTitle);
            panel.Controls.Add(combo);

            panel.Layout += (_, __) =>
            {
                combo.Location = new Point(panel.Width - combo.Width - 6, 6);
            };

            return panel;
        }

        private Control BuildActionRow(string title, Label label)
        {
            var panel = new Panel
            {
                Width = _settingsPanel.Width - 30,
                Height = 36,
                BackColor = Color.Transparent
            };

            label.Text = title;
            label.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            label.ForeColor = Color.FromArgb(230, 86, 96);
            label.AutoSize = true;
            label.Cursor = Cursors.Hand;
            label.Location = new Point(0, 8);

            panel.Controls.Add(label);
            return panel;
        }

        private void BuildCompletionOverlay(Control root)
        {
            _completionOverlay.Dock = DockStyle.Fill;
            _completionOverlay.Visible = false;

            _completionDialog.Width = 760;
            _completionDialog.Height = 360;
            _completionDialog.Radius = 18;
            _completionDialog.BackColor = LightBackground;
            _completionDialog.BorderColor = LightBorder;
            _completionDialog.BorderThickness = 1;
            _completionDialog.Shadow = true;
            _completionDialog.Padding = new Padding(28);

            _completionTitle.Text = "Hoàn thành thẻ ghi nhớ";
            _completionTitle.Font = new Font("Segoe UI", 22F, FontStyle.Bold);
            _completionTitle.ForeColor = Color.FromArgb(35, 35, 35);
            _completionTitle.AutoSize = true;

            _completionSubtitle.Text = "(chưa chọn)";
            _completionSubtitle.Font = new Font("Segoe UI", 11.5F, FontStyle.Bold);
            _completionSubtitle.ForeColor = Color.FromArgb(90, 100, 150);
            _completionSubtitle.AutoSize = true;

            _completionClose.Text = "×";
            _completionClose.Width = 44;
            _completionClose.Height = 44;
            _completionClose.FlatStyle = FlatStyle.Flat;
            _completionClose.FlatAppearance.BorderSize = 0;
            _completionClose.BackColor = LightBackground;
            _completionClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(245, 246, 250);
            _completionClose.FlatAppearance.MouseDownBackColor = Color.FromArgb(238, 240, 246);
            _completionClose.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
            _completionClose.ForeColor = Color.FromArgb(90, 90, 90);
            _completionClose.Cursor = Cursors.Hand;
            _completionClose.TabStop = false;
            _completionClose.Click += (_, __) => HideCompletionOverlay();

            _completionKnown.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            _completionKnown.ForeColor = Color.FromArgb(16, 185, 129);
            _completionKnown.AutoSize = true;

            _completionLearning.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            _completionLearning.ForeColor = Color.FromArgb(234, 88, 12);
            _completionLearning.AutoSize = true;

            _completionRemaining.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            _completionRemaining.ForeColor = Color.FromArgb(90, 90, 90);
            _completionRemaining.AutoSize = true;

            StylePrimaryButton(_completionReset, "Đặt lại Thẻ ghi nhớ");
            StyleGhostButton(_completionDismiss, "Đóng");

            _completionReset.Click += (_, __) =>
            {
                HideCompletionOverlay();
                ResetProgress();
            };
            _completionDismiss.Click += (_, __) => HideCompletionOverlay();

            var rootLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = LightBackground
            };
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));

            var header = new Panel { Dock = DockStyle.Fill, BackColor = LightBackground };
            var leftHeader = new Panel { Dock = DockStyle.Fill, BackColor = LightBackground };
            leftHeader.Controls.Add(_completionSubtitle);
            leftHeader.Controls.Add(_completionTitle);
            _completionSubtitle.Location = new Point(0, 6);
            _completionTitle.Location = new Point(0, 38);

            var rightHeader = new Panel { Dock = DockStyle.Right, Width = 80, BackColor = LightBackground };
            rightHeader.Controls.Add(_completionClose);
            rightHeader.Layout += (_, __) =>
            {
                _completionClose.Location = new Point(rightHeader.ClientSize.Width - _completionClose.Width, 0);
            };

            header.Controls.Add(rightHeader);
            header.Controls.Add(leftHeader);

            var statsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = LightBackground,
                Padding = new Padding(8, 8, 8, 8)
            };
            statsPanel.Controls.Add(_completionKnown);
            statsPanel.Controls.Add(_completionLearning);
            statsPanel.Controls.Add(_completionRemaining);

            var btnRow = new Panel { Dock = DockStyle.Fill, BackColor = LightBackground };
            btnRow.Controls.Add(_completionReset);
            btnRow.Controls.Add(_completionDismiss);
            btnRow.Layout += (_, __) =>
            {
                int gap = 12;
                int totalW = _completionReset.Width + gap + _completionDismiss.Width;
                int x = (btnRow.ClientSize.Width - totalW) / 2;
                int y = 16;

                _completionReset.Location = new Point(x, y);
                _completionDismiss.Location = new Point(x + _completionReset.Width + gap, y);
            };

            rootLayout.Controls.Add(header, 0, 0);
            rootLayout.Controls.Add(statsPanel, 0, 1);
            rootLayout.Controls.Add(btnRow, 0, 2);

            _completionDialog.Controls.Clear();
            _completionDialog.Controls.Add(rootLayout);

            _completionOverlay.Controls.Add(_completionDialog);
            root.Controls.Add(_completionOverlay);

            Layout += (_, __) => CenterCompletionDialog();
            _completionOverlay.Layout += (_, __) => CenterCompletionDialog();
        }

        private void ShowCompletionOverlay()
        {
            int total = _order.Count;
            int known = _progressMap.Count(p => p.Value == CardProgressState.Known);
            int learning = _progressMap.Count(p => p.Value == CardProgressState.Learning);
            int remaining = Math.Max(0, total - known - learning);

            _completionSubtitle.Text = _set?.Title ?? "(chưa chọn)";
            _completionKnown.Text = $"Đã biết: {known}";
            _completionLearning.Text = $"Đang học: {learning}";
            _completionRemaining.Text = $"Còn lại: {remaining}";

            _completionOverlay.Visible = true;
            _completionOverlay.BringToFront();
            CenterCompletionDialog();
        }

        private void HideCompletionOverlay()
        {
            _completionOverlay.Visible = false;
        }

        private void CenterCompletionDialog()
        {
            if (!_completionOverlay.Visible) return;

            int x = Math.Max(0, (ClientSize.Width - _completionDialog.Width) / 2);
            int y = Math.Max(0, (ClientSize.Height - _completionDialog.Height) / 2);
            _completionDialog.Location = new Point(x, y);
        }

        private void CheckCompletion()
        {
            if (_order.Count == 0) return;
            if (_index < _order.Count - 1) return;
            if (_completionShown) return;

            _completionShown = true;
            ShowCompletionOverlay();
        }

        private void ApplyCompletionTheme()
        {
            var dialogBg = _isDarkMode ? DarkBackground : LightBackground;
            var dialogBorder = _isDarkMode ? DarkBorder : LightBorder;
            var titleColor = _isDarkMode ? DarkTextPrimary : Color.FromArgb(35, 35, 35);
            var subtitleColor = _isDarkMode ? DarkTextSecondary : Color.FromArgb(90, 100, 150);

            _completionDialog.BackColor = dialogBg;
            _completionDialog.BorderColor = dialogBorder;

            _completionClose.BackColor = dialogBg;
            _completionClose.ForeColor = _isDarkMode ? DarkTextSecondary : Color.FromArgb(90, 90, 90);
            _completionClose.FlatAppearance.MouseOverBackColor = _isDarkMode ? Color.FromArgb(60, 60, 70) : Color.FromArgb(245, 246, 250);
            _completionClose.FlatAppearance.MouseDownBackColor = _isDarkMode ? Color.FromArgb(70, 70, 80) : Color.FromArgb(238, 240, 246);

            _completionTitle.ForeColor = titleColor;
            _completionSubtitle.ForeColor = subtitleColor;

            _completionRemaining.ForeColor = _isDarkMode ? DarkTextSecondary : Color.FromArgb(90, 90, 90);

            if (_isDarkMode)
            {
                _completionDismiss.BackColor = Color.FromArgb(45, 45, 58);
                _completionDismiss.BorderColor = Color.FromArgb(90, 90, 110);
                _completionDismiss.ForeColor = Color.FromArgb(230, 230, 240);
            }
            else
            {
                _completionDismiss.BackColor = Color.FromArgb(245, 246, 250);
                _completionDismiss.BorderColor = Color.FromArgb(230, 232, 238);
                _completionDismiss.ForeColor = Color.FromArgb(60, 60, 60);
            }

            ApplyCompletionBackground(_completionDialog, dialogBg);

            _completionDialog.Invalidate();
        }

        private static void ApplyCompletionBackground(Control control, Color backColor)
        {
            if (control is Panel || control is TableLayoutPanel || control is FlowLayoutPanel)
            {
                control.BackColor = backColor;
            }

            foreach (Control child in control.Controls)
            {
                ApplyCompletionBackground(child, backColor);
            }
        }

        private void EditCurrentCard()
        {
            if (_set?.Items == null || _order.Count == 0) return;

            var itemIndex = _order[_index];
            var item = _set.Items[itemIndex];

            using var dlg = new EditCardDialog(item);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            item.Term = dlg.TermValue;
            item.Definition = dlg.DefinitionValue;
            item.Pinyin = dlg.PinyinValue;

            CardSetStorage.SaveSetJson(_set);
            ShowCard();
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
            b.FlatAppearance.BorderColor = Color.FromArgb(248, 248, 248);
        }

        private static void StylePrimaryButton(RoundedButton b, string text)
        {
            b.Text = text;
            b.Width = 220;
            b.Height = 48;
            b.Radius = 14;
            b.BorderThickness = 0;
            b.BackColor = Color.FromArgb(62, 92, 255);
            b.BorderColor = b.BackColor;
            b.ForeColor = Color.White;
            b.Font = new Font("Segoe UI", 11.5F, FontStyle.Bold);
            b.Cursor = Cursors.Hand;
        }

        private static void StyleGhostButton(RoundedButton b, string text)
        {
            b.Text = text;
            b.Width = 220;
            b.Height = 48;
            b.Radius = 14;
            b.BorderThickness = 1;
            b.BorderColor = Color.FromArgb(230, 232, 238);
            b.BackColor = Color.FromArgb(245, 246, 250);
            b.ForeColor = Color.FromArgb(50, 50, 50);
            b.Font = new Font("Segoe UI", 11.5F, FontStyle.Bold);
            b.Cursor = Cursors.Hand;
        }

        private sealed class OverlayPanel : Panel
        {
            public OverlayPanel()
            {
                Dock = DockStyle.Fill;
                SetStyle(ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.UserPaint, true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                using var b = new SolidBrush(Color.FromArgb(120, 0, 0, 0));
                e.Graphics.FillRectangle(b, ClientRectangle);
            }
        }

        private sealed class RoundedPanel : Panel
        {
            public int Radius { get; set; } = 16;
            public bool Shadow { get; set; } = false;
            public int BorderThickness { get; set; } = 1;
            public Color BorderColor { get; set; } = Color.FromArgb(230, 230, 230);

            private GraphicsPath? _regionPath;

            public RoundedPanel()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.ResizeRedraw |
                         ControlStyles.UserPaint, true);
            }

            protected override void OnSizeChanged(EventArgs e)
            {
                base.OnSizeChanged(e);
                UpdateRegion();
                Invalidate();
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing) _regionPath?.Dispose();
                base.Dispose(disposing);
            }

            private void UpdateRegion()
            {
                _regionPath?.Dispose();

                int r = Math.Max(1, Math.Min(Radius, Math.Min(Width, Height) / 2));
                var rect = new Rectangle(0, 0, Width, Height);

                _regionPath = RoundedRect(rect, r);
                Region = new Region(_regionPath);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(ResolveBackColor(this));

                var rect = new Rectangle(0, 0, Width - 1, Height - 1);
                int r = Math.Max(1, Math.Min(Radius, Math.Min(Width, Height) / 2));

                if (Shadow)
                {
                    var sh = new Rectangle(rect.X + 6, rect.Y + 8, rect.Width - 6, rect.Height - 8);
                    using var shPath = RoundedRect(sh, r);
                    using var shBrush = new SolidBrush(Color.FromArgb(25, 0, 0, 0));
                    g.FillPath(shBrush, shPath);
                }

                using var path = RoundedRect(rect, r);
                using var fill = new SolidBrush(BackColor);
                g.FillPath(fill, path);

                if (BorderThickness > 0)
                {
                    using var pen = new Pen(BorderColor, BorderThickness)
                    {
                        LineJoin = LineJoin.Round,
                        Alignment = PenAlignment.Inset
                    };
                    g.DrawPath(pen, path);
                }
            }

            private static GraphicsPath RoundedRect(Rectangle r, int radius)
            {
                var path = new GraphicsPath();
                int d = radius * 2;
                path.AddArc(r.X, r.Y, d, d, 180, 90);
                path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                return path;
            }
        }

        private sealed class RoundedButton : Button
        {
            public int Radius { get; set; } = 10;
            public int BorderThickness { get; set; } = 1;
            public Color BorderColor { get; set; } = Color.FromArgb(230, 230, 230);

            private GraphicsPath? _regionPath;

            public RoundedButton()
            {
                FlatStyle = FlatStyle.Flat;
                FlatAppearance.BorderSize = 0;

                SetStyle(ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.UserPaint |
                         ControlStyles.ResizeRedraw, true);
            }

            protected override void OnSizeChanged(EventArgs e)
            {
                base.OnSizeChanged(e);
                UpdateRegion();
                Invalidate();
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing) _regionPath?.Dispose();
                base.Dispose(disposing);
            }

            private void UpdateRegion()
            {
                _regionPath?.Dispose();

                int r = Math.Max(1, Math.Min(Radius, Math.Min(Width, Height) / 2));
                var rect = new Rectangle(0, 0, Width, Height);

                _regionPath = RoundedRect(rect, r);
                Region = new Region(_regionPath);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                g.Clear(ResolveBackColor(this));

                int inset = Math.Max(1, BorderThickness);
                var rect = new Rectangle(inset, inset, Width - 1 - inset * 2, Height - 1 - inset * 2);

                int r = Math.Max(1, Math.Min(Radius, Math.Min(Width, Height) / 2));

                using var path = RoundedRect(rect, r);
                using var fill = new SolidBrush(BackColor);

                g.FillPath(fill, path);

                if (BorderThickness > 0)
                {
                    using var pen = new Pen(BorderColor, BorderThickness)
                    {
                        LineJoin = LineJoin.Round,
                        Alignment = PenAlignment.Inset
                    };

                    g.DrawPath(pen, path);
                }

                TextRenderer.DrawText(
                    g,
                    Text,
                    Font,
                    rect,
                    ForeColor,
                    TextFormatFlags.HorizontalCenter |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.EndEllipsis);
            }

            private static GraphicsPath RoundedRect(Rectangle r, int radius)
            {
                var path = new GraphicsPath();
                int d = radius * 2;
                path.AddArc(r.X, r.Y, d, d, 180, 90);
                path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                return path;
            }
        }

        private static Color ResolveBackColor(Control c)
        {
            Control? p = c.Parent;
            while (p != null && p.BackColor.A < 255) p = p.Parent;
            return p?.BackColor ?? SystemColors.Control;
        }

        private sealed class EditCardDialog : Form
        {
            private readonly TextBox _txtTerm = new();
            private readonly TextBox _txtDefinition = new();
            private readonly TextBox _txtPinyin = new();

            public string TermValue => _txtTerm.Text.Trim();
            public string DefinitionValue => _txtDefinition.Text.Trim();
            public string PinyinValue => string.IsNullOrWhiteSpace(_txtPinyin.Text) ? null : _txtPinyin.Text.Trim();

            public EditCardDialog(CardItem item)
            {
                Text = "Chỉnh sửa thẻ";
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                Width = 420;
                Height = 320;
                BackColor = Color.FromArgb(20, 20, 40);
                Font = new Font("Segoe UI", 9.5F);

                var lblTerm = BuildLabel("Từ vựng", new Point(20, 20));
                _txtTerm.Location = new Point(20, 44);
                _txtTerm.Size = new Size(360, 28);
                StyleTextBox(_txtTerm);

                var lblDef = BuildLabel("Nghĩa", new Point(20, 82));
                _txtDefinition.Location = new Point(20, 106);
                _txtDefinition.Size = new Size(360, 28);
                StyleTextBox(_txtDefinition);

                var lblPinyin = BuildLabel("Pinyin", new Point(20, 144));
                _txtPinyin.Location = new Point(20, 168);
                _txtPinyin.Size = new Size(360, 28);
                StyleTextBox(_txtPinyin);

                var btnOk = new Button
                {
                    Text = "Lưu",
                    DialogResult = DialogResult.OK,
                    Width = 100,
                    Height = 34,
                    Location = new Point(280, 220),
                    BackColor = Color.FromArgb(76, 146, 245),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                btnOk.FlatAppearance.BorderSize = 0;

                var btnCancel = new Button
                {
                    Text = "Hủy",
                    DialogResult = DialogResult.Cancel,
                    Width = 100,
                    Height = 34,
                    Location = new Point(170, 220),
                    BackColor = Color.FromArgb(52, 52, 74),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                btnCancel.FlatAppearance.BorderSize = 0;

                Controls.Add(lblTerm);
                Controls.Add(_txtTerm);
                Controls.Add(lblDef);
                Controls.Add(_txtDefinition);
                Controls.Add(lblPinyin);
                Controls.Add(_txtPinyin);
                Controls.Add(btnCancel);
                Controls.Add(btnOk);

                AcceptButton = btnOk;
                CancelButton = btnCancel;

                _txtTerm.Text = item.Term ?? "";
                _txtDefinition.Text = item.Definition ?? "";
                _txtPinyin.Text = item.Pinyin ?? "";
            }

            private static Label BuildLabel(string text, Point location)
            {
                return new Label
                {
                    Text = text,
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                    AutoSize = true,
                    Location = location
                };
            }

            private static void StyleTextBox(TextBox box)
            {
                box.BackColor = Color.FromArgb(34, 34, 58);
                box.ForeColor = Color.White;
                box.BorderStyle = BorderStyle.FixedSingle;
            }
        }
    }
}
