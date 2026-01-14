using System;
using System.Collections.Generic;
using System.Drawing;
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
        private List<int> _order = new();
        private List<int> _filteredOrder = new();
        private bool _shuffleEnabled;
        private bool _progressTracking;
        private bool _starredOnly;
        private bool _ttsEnabled = true;
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
        private readonly ComboBox _cmbFrontSide = new();
        private readonly Label _lblReset = new();
        private readonly Button _btnCloseSettings = new();

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

            SetEnabledUI(false);
        }

        public void LoadSet(CardSet set)
        {
            _set = set;
            _index = 0;
            _progressMap.Clear();
            _undoStack.Clear();

            ApplyLegacyStarred();
            RebuildOrder(false);
            UpdateShuffleUi();
            UpdateProgressUi();
            SetEnabledUI(_set.Items != null && _set.Items.Count > 0);
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
            _toggleProgress.Checked = _progressTracking;

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

            BuildSettingsPanel(root);
            UpdateProgressUi();
            UpdateShuffleUi();
        }

        private void Wire()
        {
            _btnPrev.Click += (_, __) => HandlePrevAction();
            _btnNext.Click += (_, __) => HandleNextAction();

            // ✅ icon nằm trong card:
            _card.StarIconClicked += (_, __) => ToggleStar();
            _card.PencilIconClicked += (_, __) => EditCurrentCard();
            _card.SoundIconClicked += async (_, __) => await PlaySoundAsync();

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
            _btnPlay.Enabled = enabled;
            _btnShuffle.Enabled = enabled;
            _btnSettings.Enabled = enabled;
            _btnFullscreen.Enabled = enabled;
            _toggleProgress.Enabled = enabled;
            _toggleProgressOpt.Enabled = enabled;
            _toggleStarredOnly.Enabled = enabled;
            _toggleTts.Enabled = enabled;
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
            RebuildOrder(true);
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
                _btnPrev.ForeColor = Color.FromArgb(200, 74, 74);
                _btnNext.ForeColor = Color.FromArgb(46, 140, 90);
                _btnPlay.ForeColor = Color.FromArgb(60, 60, 60);
                _tt.SetToolTip(_btnPrev, "Đang học");
                _tt.SetToolTip(_btnNext, "Đã thuộc");
                _tt.SetToolTip(_btnPlay, "Hoàn tác");
            }
            else
            {
                _btnPrev.Text = "←";
                _btnNext.Text = "→";
                _btnPlay.Text = "▶";
                _btnPrev.ForeColor = Color.FromArgb(60, 60, 60);
                _btnNext.ForeColor = Color.FromArgb(60, 60, 60);
                _btnPlay.ForeColor = Color.FromArgb(60, 60, 60);
                _tt.SetToolTip(_btnPrev, "Thẻ trước");
                _tt.SetToolTip(_btnNext, "Thẻ sau");
                _tt.SetToolTip(_btnPlay, "Tự động phát");
            }
        }

        private void UpdateShuffleUi()
        {
            _btnShuffle.ForeColor = _shuffleEnabled ? Color.FromArgb(76, 146, 245) : Color.FromArgb(60, 60, 60);
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
            ShowCard();
        }

        private void RebuildOrder(bool preserveCurrent)
        {
            if (_set?.Items == null)
            {
                _order = new List<int>();
                _filteredOrder = new List<int>();
                ShowCard();
                return;
            }

            int? currentItem = preserveCurrent && _order.Count > 0 ? _order[_index] : null;

            _filteredOrder = Enumerable.Range(0, _set.Items.Count)
                .Where(i => !_starredOnly || _set.Items[i].IsStarred)
                .ToList();

            _order = new List<int>(_filteredOrder);
            if (_shuffleEnabled) Shuffle(_order);

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

            var rowReset = BuildActionRow("Khởi động lại Thẻ ghi nhớ", _lblReset);

            list.Controls.Add(rowProgress);
            list.Controls.Add(rowStarred);
            list.Controls.Add(rowFront);
            list.Controls.Add(rowTts);
            list.Controls.Add(rowReset);

            _toggleProgressOpt.Checked = _progressTracking;
            _toggleStarredOnly.Checked = _starredOnly;
            _toggleTts.Checked = _ttsEnabled;

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
            b.FlatAppearance.BorderColor = Color.FromArgb(225, 225, 225);
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
