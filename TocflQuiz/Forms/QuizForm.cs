using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;
using NAudio.Wave;
using TocflQuiz.Models;
using TocflQuiz.Services;

namespace TocflQuiz.Forms
{
    // =========================================================
    // QuizForm.cs (FULL) - UI/UX + NAV FILE (A: đặt TRÊN nav câu)
    // - Giữ nguyên logic chấm/ô n (Submit/Progress/SR) như cũ
    // - Thêm "⏮ Bài trước / Bài tiếp ⏭" để chuyển file trong cùng danh sách
    // - Prev/Next câu: chuyển trong 1 file (Câu 1/5)
    // - 2 dạng đặc biệt: KHÔNG auto-next, nhưng vẫn bấm Next/Prev câu bình thường
    // Mốc tìm nhanh: [NAV-FILE], [NAV-QUESTION], [SPECIAL], [AUDIO], [STATUS], [UI-BUILD]
    // =========================================================
    public partial class QuizForm : Form
    {
        private readonly QuestionGroup _group;
        private readonly Dictionary<string, ProgressRecord> _progressMap;
        private readonly ProgressStoreJson _store;
        private readonly SpacedRepetition _sr;

        // ===================== [NAV-FILE] Navigate between files =====================
        private readonly List<QuestionGroup> _allGroups;
        private int _groupIndex;
        // ============================================================================

        // ===================== [UI] Controls =====================
        private TabControl tabPdf = new();
        private WebView2 wvQuestion = new();
        private WebView2 wvScript = new();

        private Label lblTitle = new();
        private Label lblIndex = new();
        private Label lblStatus = new();

        private FlowLayoutPanel pnlChoices = new();
        private Button btnPrev = new();
        private Button btnNext = new();
        private Button btnSubmit = new();

        // [NAV-FILE] buttons
        private Button btnPrevFile = new();
        private Button btnNextFile = new();

        private Button btnOpenPdf = new();
        private Button btnOpenScript = new();

        // ===================== [AUDIO] Controls =====================
        private GroupBox grpAudio = new();
        private Button btnPlayPause = new();
        private TrackBar trkAudio = new();
        private Label lblAudioTime = new();
        private System.Windows.Forms.Timer tmrAudio = new System.Windows.Forms.Timer();
        private System.Windows.Forms.Timer tmrStatus = new System.Windows.Forms.Timer();

        // Resize helper controls
        private Panel _audioRow1 = new();
        private FlowLayoutPanel _audioRow3 = new();

        private WaveOutEvent? _waveOut;
        private AudioFileReader? _audioReader;
        private bool _isDragging = false;

        // ===================== [STATE] Answer =====================
        private int _idx = 0;
        private List<string?> _userAnswers = new();

        // =========================================================
        // [CTOR-OVERLOAD] giữ tương thích: call cũ vẫn chạy
        // =========================================================
        public QuizForm(
            QuestionGroup group,
            Dictionary<string, ProgressRecord> progressMap,
            ProgressStoreJson store,
            SpacedRepetition sr)
            : this(group, progressMap, store, sr, new List<QuestionGroup> { group }, 0)
        { }

        // =========================================================
        // [CTOR] call mới có danh sách để Next/Prev "Bài"
        // =========================================================
        public QuizForm(
            QuestionGroup group,
            Dictionary<string, ProgressRecord> progressMap,
            ProgressStoreJson store,
            SpacedRepetition sr,
            List<QuestionGroup> allGroups,
            int groupIndex)
        {
            InitializeComponent();



            MinimumSize = new System.Drawing.Size(1000, 700);

            _group = group ?? throw new ArgumentNullException(nameof(group));
            _progressMap = progressMap ?? new Dictionary<string, ProgressRecord>(StringComparer.OrdinalIgnoreCase);
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _sr = sr ?? throw new ArgumentNullException(nameof(sr));

            // ===================== [NAV-FILE] init =====================
            _allGroups = allGroups ?? new List<QuestionGroup> { group };
            if (_allGroups.Count == 0) _allGroups.Add(group);
            _groupIndex = Math.Max(0, Math.Min(groupIndex, _allGroups.Count - 1));
            // =========================================================

            Text = $"📝 Quiz - {_group.FileId}";
            Width = 1800;
            Height = 1000;
            StartPosition = FormStartPosition.CenterParent;
            Font = new System.Drawing.Font("Segoe UI", 9F);

            _userAnswers = Enumerable.Repeat<string?>(null, Math.Max(1, _group.CorrectAnswers.Count)).ToList();

            BuildUi();     // [UI-BUILD]
            LoadPdf();     // [PDF]
            LoadAudio();   // [AUDIO]
            LoadIndex(0);  // [NAV-QUESTION]

            FormClosing += (_, __) =>
            {
                tmrStatus.Stop();
                CleanupAudio();
            };

            // [STATUS] realtime countdown (1s)
            tmrStatus.Interval = 1000;
            tmrStatus.Tick += (_, __) => RefreshStatusLabelOnly();
            tmrStatus.Start();

            // [NAV-FILE] enable/disable buttons (UI only)
            UpdateFileNavButtons();
        }

        // ===================== [SPECIAL] Define special categories =====================
        // 2 dạng đặc biệt: KHÔNG auto-next (nhưng vẫn bấm Next/Prev câu bình thường)
        // TODO: nếu 2 tên khác, đổi đúng tên Category hiển thị ở combobox.
        private bool IsSpecialMultiAnswerCategory()
        {
            var cat = (_group.Category ?? "").Trim();
            return cat.Equals("Paragraph Completion", StringComparison.OrdinalIgnoreCase)
                || cat.Equals("Sentence Comprehension", StringComparison.OrdinalIgnoreCase);
        }

        // Dạng thường -> auto-next (chọn xong nhảy câu tiếp)
        private bool AllowAutoNextForThisMode() => !IsSpecialMultiAnswerCategory();

        // ===================== [UI-BUILD] =====================
        private void BuildUi()
        {
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 750,
                BackColor = System.Drawing.Color.FromArgb(200, 200, 200),
                SplitterWidth = 6
            };

            // LEFT: PDF tabs
            tabPdf.Dock = DockStyle.Fill;
            tabPdf.Font = new System.Drawing.Font("Segoe UI", 9.5F);

            var tpQ = new TabPage("📄 PDF - Đề");
            wvQuestion.Dock = DockStyle.Fill;
            tpQ.Controls.Add(wvQuestion);

            var tpS = new TabPage("📜 PDF - Script");
            wvScript.Dock = DockStyle.Fill;
            tpS.Controls.Add(wvScript);

            tabPdf.TabPages.Clear();
            tabPdf.TabPages.Add(tpQ);
            tabPdf.TabPages.Add(tpS);

            split.Panel1.Controls.Add(tabPdf);

            // RIGHT: header + audio + index + choices + [NAV-FILE] + [NAV-QUESTION] + submit
            var right = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 8,
                Padding = new Padding(16),
                BackColor = System.Drawing.Color.FromArgb(245, 245, 245)
            };

            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));   // 0 title
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 180));  // 1 audio
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));   // 2 index/status
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 300));  // 3 choices
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));   // 4 [NAV-FILE]  (A: đặt TRÊN nav câu)
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));   // 5 [NAV-QUESTION]
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));   // 6 submit
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // 7 filler

            // Title section
            var titlePanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = System.Drawing.Color.FromArgb(70, 130, 180),
                Padding = new Padding(12, 10, 12, 10),
                Margin = new Padding(0, 0, 0, 12)
            };
            lblTitle.Dock = DockStyle.Fill;
            lblTitle.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            lblTitle.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            lblTitle.ForeColor = System.Drawing.Color.White;
            lblTitle.Text = $"📚 {_group.Mode} / {_group.Category}\n📁 File: {_group.FileId}";
            titlePanel.Controls.Add(lblTitle);
            right.Controls.Add(titlePanel, 0, 0);

            // ========= [AUDIO] SECTION =========
            grpAudio.Text = "  🔊 Audio (Listening)  ";
            grpAudio.Dock = DockStyle.Fill;
            grpAudio.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold);
            grpAudio.ForeColor = System.Drawing.Color.FromArgb(50, 50, 50);
            grpAudio.Padding = new Padding(12, 8, 12, 12);
            grpAudio.Margin = new Padding(0, 0, 0, 12);
            grpAudio.BackColor = System.Drawing.Color.White;

            btnPlayPause.Text = "▶ Play";
            btnPlayPause.BackColor = System.Drawing.Color.FromArgb(60, 170, 100);
            btnPlayPause.ForeColor = System.Drawing.Color.White;
            btnPlayPause.FlatStyle = FlatStyle.Flat;
            btnPlayPause.FlatAppearance.BorderSize = 0;
            btnPlayPause.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            btnPlayPause.Cursor = Cursors.Hand;
            btnPlayPause.Click += (_, __) => TogglePlayPause();

            btnOpenPdf.Text = "📄 Mở PDF đề";
            btnOpenPdf.BackColor = System.Drawing.Color.FromArgb(100, 100, 100);
            btnOpenPdf.ForeColor = System.Drawing.Color.White;
            btnOpenPdf.FlatStyle = FlatStyle.Flat;
            btnOpenPdf.FlatAppearance.BorderSize = 0;
            btnOpenPdf.Font = new System.Drawing.Font("Segoe UI", 9F);
            btnOpenPdf.Cursor = Cursors.Hand;
            btnOpenPdf.Click += (_, __) => OpenExternal(_group.PdfQuestionPath);

            btnOpenScript.Text = "📜 Mở PDF script";
            btnOpenScript.BackColor = System.Drawing.Color.FromArgb(100, 100, 100);
            btnOpenScript.ForeColor = System.Drawing.Color.White;
            btnOpenScript.FlatStyle = FlatStyle.Flat;
            btnOpenScript.FlatAppearance.BorderSize = 0;
            btnOpenScript.Font = new System.Drawing.Font("Segoe UI", 9F);
            btnOpenScript.Cursor = Cursors.Hand;
            btnOpenScript.Click += (_, __) => OpenExternal(_group.PdfScriptPath);

            lblAudioTime.Font = new System.Drawing.Font("Consolas", 10F);
            lblAudioTime.ForeColor = System.Drawing.Color.FromArgb(70, 70, 70);

            trkAudio.Minimum = 0;
            trkAudio.Maximum = 1000;
            trkAudio.TickFrequency = 50;
            trkAudio.MouseDown += (_, __) => _isDragging = true;
            trkAudio.MouseUp += (_, __) => { _isDragging = false; SeekAudioFromTrack(); };
            trkAudio.KeyUp += (_, __) => SeekAudioFromTrack();

            var audioLayout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(8),
                AutoScroll = false
            };

            _audioRow1 = new Panel { Height = 36, Margin = new Padding(0) };
            _audioRow1.MinimumSize = new System.Drawing.Size(280, 36);

            btnPlayPause.SetBounds(0, 2, 120, 32);

            lblAudioTime.AutoSize = false;
            lblAudioTime.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            lblAudioTime.SetBounds(130, 2, 200, 32);
            lblAudioTime.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;

            _audioRow1.Controls.Add(btnPlayPause);
            _audioRow1.Controls.Add(lblAudioTime);

            trkAudio.Height = 42;
            trkAudio.Margin = new Padding(0, 6, 0, 0);
            trkAudio.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;

            _audioRow3 = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoSize = true,
                Margin = new Padding(0, 8, 0, 0)
            };
            btnOpenPdf.Width = 140;
            btnOpenPdf.Height = 34;
            btnOpenPdf.Margin = new Padding(0, 0, 8, 0);
            btnOpenScript.Width = 160;
            btnOpenScript.Height = 34;

            _audioRow3.Controls.Add(btnOpenPdf);
            _audioRow3.Controls.Add(btnOpenScript);

            audioLayout.Controls.Add(_audioRow1);
            audioLayout.Controls.Add(trkAudio);
            audioLayout.Controls.Add(_audioRow3);

            grpAudio.Controls.Clear();
            grpAudio.Controls.Add(audioLayout);

            grpAudio.Resize += (_, __) =>
            {
                var w = Math.Max(280, grpAudio.ClientSize.Width - 20);
                _audioRow1.Width = w;
                trkAudio.Width = w;
                lblAudioTime.Width = Math.Max(80, w - 130);
            };

            right.Controls.Add(grpAudio, 0, 1);
            // ===================== [AUDIO] END =====================

            // index + status
            var rowIndexPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = System.Drawing.Color.FromArgb(250, 250, 250),
                Padding = new Padding(12),
                Margin = new Padding(0, 0, 0, 12)
            };
            var rowIndex = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            rowIndex.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            rowIndex.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));

            lblIndex.Dock = DockStyle.Fill;
            lblIndex.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            lblIndex.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            lblIndex.ForeColor = System.Drawing.Color.FromArgb(50, 50, 50);

            lblStatus.Dock = DockStyle.Fill;
            lblStatus.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            lblStatus.Font = new System.Drawing.Font("Consolas", 9F);
            lblStatus.ForeColor = System.Drawing.Color.FromArgb(70, 70, 70);

            rowIndex.Controls.Add(lblIndex, 0, 0);
            rowIndex.Controls.Add(lblStatus, 1, 0);
            rowIndexPanel.Controls.Add(rowIndex);

            right.Controls.Add(rowIndexPanel, 0, 2);

            // choices
            var choicesGroup = new GroupBox
            {
                Text = "  ✏️ Chọn đáp án  ",
                Dock = DockStyle.Fill,
                Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold),
                ForeColor = System.Drawing.Color.FromArgb(50, 50, 50),
                Padding = new Padding(12, 8, 12, 8),
                Margin = new Padding(0, 0, 0, 12),
                BackColor = System.Drawing.Color.White
            };

            pnlChoices.Dock = DockStyle.Fill;
            pnlChoices.FlowDirection = FlowDirection.TopDown;
            pnlChoices.WrapContents = false;
            pnlChoices.AutoScroll = true;
            pnlChoices.BackColor = System.Drawing.Color.White;
            pnlChoices.Padding = new Padding(8);

            choicesGroup.Controls.Add(pnlChoices);
            right.Controls.Add(choicesGroup, 0, 3);

            // ===================== [NAV-FILE] (A: đặt TRÊN nav câu) =====================
            var fileNavPanel = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 12) };
            var fileNav = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };

            btnPrevFile.Text = "⏮ Bài trước";
            btnPrevFile.Width = 150;
            btnPrevFile.Height = 44;
            btnPrevFile.Font = new System.Drawing.Font("Segoe UI", 10F);
            btnPrevFile.BackColor = System.Drawing.Color.FromArgb(90, 90, 90);
            btnPrevFile.ForeColor = System.Drawing.Color.White;
            btnPrevFile.FlatStyle = FlatStyle.Flat;
            btnPrevFile.FlatAppearance.BorderSize = 0;
            btnPrevFile.Cursor = Cursors.Hand;
            btnPrevFile.Margin = new Padding(0, 0, 10, 0);
            btnPrevFile.Click += (_, __) => NavigateFile(-1);

            btnNextFile.Text = "Bài tiếp ⏭";
            btnNextFile.Width = 150;
            btnNextFile.Height = 44;
            btnNextFile.Font = new System.Drawing.Font("Segoe UI", 10F);
            btnNextFile.BackColor = System.Drawing.Color.FromArgb(60, 120, 200);
            btnNextFile.ForeColor = System.Drawing.Color.White;
            btnNextFile.FlatStyle = FlatStyle.Flat;
            btnNextFile.FlatAppearance.BorderSize = 0;
            btnNextFile.Cursor = Cursors.Hand;
            btnNextFile.Click += (_, __) => NavigateFile(+1);

            fileNav.Controls.Add(btnPrevFile);
            fileNav.Controls.Add(btnNextFile);
            fileNavPanel.Controls.Add(fileNav);

            right.Controls.Add(fileNavPanel, 0, 4);
            // ===================== [NAV-FILE] END =====================

            // ===================== [NAV-QUESTION] Prev/Next câu =====================
            var navPanel = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 12) };
            var nav = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };

            btnPrev.Text = "◀ Câu trước";
            btnPrev.Width = 150;
            btnPrev.Height = 44;
            btnPrev.Font = new System.Drawing.Font("Segoe UI", 10F);
            btnPrev.BackColor = System.Drawing.Color.FromArgb(120, 120, 120);
            btnPrev.ForeColor = System.Drawing.Color.White;
            btnPrev.FlatStyle = FlatStyle.Flat;
            btnPrev.FlatAppearance.BorderSize = 0;
            btnPrev.Cursor = Cursors.Hand;
            btnPrev.Margin = new Padding(0, 0, 10, 0);
            btnPrev.Click += (_, __) => LoadIndex(_idx - 1);

            btnNext.Text = "Câu tiếp ▶";
            btnNext.Width = 150;
            btnNext.Height = 44;
            btnNext.Font = new System.Drawing.Font("Segoe UI", 10F);
            btnNext.BackColor = System.Drawing.Color.FromArgb(70, 130, 180);
            btnNext.ForeColor = System.Drawing.Color.White;
            btnNext.FlatStyle = FlatStyle.Flat;
            btnNext.FlatAppearance.BorderSize = 0;
            btnNext.Cursor = Cursors.Hand;
            btnNext.Click += (_, __) => LoadIndex(_idx + 1);

            nav.Controls.Add(btnPrev);
            nav.Controls.Add(btnNext);
            navPanel.Controls.Add(nav);

            right.Controls.Add(navPanel, 0, 5);
            // ===================== [NAV-QUESTION] END =====================

            // submit
            btnSubmit.Text = "✅ Nộp bài / Lưu kết quả";
            btnSubmit.Dock = DockStyle.Fill;
            btnSubmit.Height = 48;
            btnSubmit.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold);
            btnSubmit.BackColor = System.Drawing.Color.FromArgb(220, 100, 50);
            btnSubmit.ForeColor = System.Drawing.Color.White;
            btnSubmit.FlatStyle = FlatStyle.Flat;
            btnSubmit.FlatAppearance.BorderSize = 0;
            btnSubmit.Cursor = Cursors.Hand;
            btnSubmit.Click += (_, __) => Submit();
            right.Controls.Add(btnSubmit, 0, 6);

            split.Panel2.Controls.Add(right);

            Controls.Add(split);

            // timer audio
            tmrAudio.Interval = 200;
            tmrAudio.Tick += (_, __) => UpdateAudioUi();
        }

        // ===================== [NAV-FILE] Core navigate file =====================
        private void UpdateFileNavButtons()
        {
            if (_allGroups == null || _allGroups.Count <= 1)
            {
                btnPrevFile.Enabled = false;
                btnNextFile.Enabled = false;
                return;
            }

            btnPrevFile.Enabled = _groupIndex > 0;
            btnNextFile.Enabled = _groupIndex < _allGroups.Count - 1;
        }

        private void NavigateFile(int delta)
        {
            if (_allGroups == null || _allGroups.Count == 0) return;

            var newIdx = _groupIndex + delta;
            if (newIdx < 0 || newIdx >= _allGroups.Count)
            {
                System.Media.SystemSounds.Beep.Play();
                return;
            }

            // (UI/UX) tuỳ chọn cảnh báo nếu chưa chọn đủ (không bắt buộc)
            // if (_userAnswers.Any(a => string.IsNullOrWhiteSpace(a)))
            // {
            //     var r = MessageBox.Show("Bạn chưa chọn đủ đáp án trong bài hiện tại. Vẫn chuyển bài?",
            //         "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            //     if (r != DialogResult.Yes) return;
            // }

            _groupIndex = newIdx;
            var nextGroup = _allGroups[_groupIndex];

            // Mở form mới (an toàn, không phải reset state phức tạp)
            var f = new QuizForm(nextGroup, _progressMap, _store, _sr, _allGroups, _groupIndex);
            f.Show();
            Close();
        }
        // ===================== [NAV-FILE] END =====================

        // ===================== [PDF] =====================
        private async void LoadPdf()
        {
            if (!string.IsNullOrWhiteSpace(_group.PdfQuestionPath) && System.IO.File.Exists(_group.PdfQuestionPath))
            {
                try
                {
                    await wvQuestion.EnsureCoreWebView2Async();
                    wvQuestion.Source = new Uri(_group.PdfQuestionPath);
                }
                catch { }
            }

            bool hasScript = !string.IsNullOrWhiteSpace(_group.PdfScriptPath) && System.IO.File.Exists(_group.PdfScriptPath);
            btnOpenScript.Enabled = hasScript;

            if (hasScript)
            {
                try
                {
                    await wvScript.EnsureCoreWebView2Async();
                    wvScript.Source = new Uri(_group.PdfScriptPath!);
                }
                catch { }
            }
            else
            {
                tabPdf.TabPages[1].Text = "📜 PDF - Script (không có)";
            }

            grpAudio.Enabled = !string.IsNullOrWhiteSpace(_group.Mp3Path) && System.IO.File.Exists(_group.Mp3Path!);
            btnOpenPdf.Enabled = !string.IsNullOrWhiteSpace(_group.PdfQuestionPath);
        }

        // ===================== [AUDIO] =====================
        private void LoadAudio()
        {
            CleanupAudio();

            if (string.IsNullOrWhiteSpace(_group.Mp3Path) || !System.IO.File.Exists(_group.Mp3Path))
            {
                btnPlayPause.Enabled = false;
                trkAudio.Enabled = false;
                lblAudioTime.Text = "--:-- / --:--";
                return;
            }

            try
            {
                _audioReader = new AudioFileReader(_group.Mp3Path);
                _waveOut = new WaveOutEvent();
                _waveOut.Init(_audioReader);
                _waveOut.PlaybackStopped += (_, __) => { btnPlayPause.Text = "▶ Play"; };

                btnPlayPause.Enabled = true;
                trkAudio.Enabled = true;
                tmrAudio.Start();
                UpdateAudioUi();
            }
            catch
            {
                btnPlayPause.Enabled = false;
                trkAudio.Enabled = false;
                lblAudioTime.Text = "--:-- / --:--";
            }
        }

        private void TogglePlayPause()
        {
            if (_waveOut == null) return;

            if (_waveOut.PlaybackState == PlaybackState.Playing)
            {
                _waveOut.Pause();
                btnPlayPause.Text = "▶ Play";
            }
            else
            {
                _waveOut.Play();
                btnPlayPause.Text = "⏸ Pause";
            }
        }

        private void SeekAudioFromTrack()
        {
            if (_audioReader == null) return;

            try
            {
                var total = _audioReader.TotalTime.TotalSeconds;
                if (total <= 0) return;

                var ratio = trkAudio.Value / 1000.0;
                var sec = total * ratio;
                _audioReader.CurrentTime = TimeSpan.FromSeconds(sec);
            }
            catch { }
        }

        private void UpdateAudioUi()
        {
            if (_audioReader == null) return;

            try
            {
                var cur = _audioReader.CurrentTime;
                var total = _audioReader.TotalTime;

                lblAudioTime.Text = $"{Fmt(cur)} / {Fmt(total)}";

                if (!_isDragging && total.TotalSeconds > 0)
                {
                    var ratio = cur.TotalSeconds / total.TotalSeconds;
                    var v = (int)Math.Max(0, Math.Min(1000, ratio * 1000));
                    trkAudio.Value = v;
                }
            }
            catch { }
        }

        private static string Fmt(TimeSpan t) => $"{(int)t.TotalMinutes:00}:{t.Seconds:00}";

        private void CleanupAudio()
        {
            try { tmrAudio.Stop(); } catch { }

            try
            {
                _waveOut?.Stop();
                _waveOut?.Dispose();
            }
            catch { }
            _waveOut = null;

            try
            {
                _audioReader?.Dispose();
            }
            catch { }
            _audioReader = null;
        }

        // ===================== [NAV-QUESTION] Load question index =====================
        private void LoadIndex(int newIndex)
        {
            var total = Math.Max(1, _group.CorrectAnswers.Count);
            if (newIndex < 0) newIndex = 0;
            if (newIndex > total - 1) newIndex = total - 1;

            _idx = newIndex;

            // (UI only) show answered count
            var answered = _userAnswers.Count(a => !string.IsNullOrWhiteSpace(a));
            lblIndex.Text = $"📝 Câu {_idx + 1} / {total} (đã chọn {answered}/{total})";

            btnPrev.Enabled = total > 1 && _idx > 0;
            btnNext.Enabled = total > 1 && _idx < total - 1;

            RefreshStatusLabelOnly();
            BuildChoicesUi();

            // restore chosen
            var chosen = _userAnswers[_idx];
            foreach (RadioButton rb in pnlChoices.Controls.OfType<RadioButton>())
            {
                rb.CheckedChanged -= Choice_CheckedChanged;
                rb.Checked = (chosen != null && rb.Tag?.ToString() == chosen);
                rb.CheckedChanged += Choice_CheckedChanged;
            }
        }

        private void BuildChoicesUi()
        {
            pnlChoices.Controls.Clear();

            int optionCount = _group.OptionCount <= 0 ? 4 : _group.OptionCount;
            char start = 'A';

            for (int i = 0; i < optionCount; i++)
            {
                char c = (char)(start + i);
                var rb = new RadioButton
                {
                    AutoSize = true,
                    Text = $"   {c}",
                    Tag = c.ToString(),
                    Font = new System.Drawing.Font("Segoe UI", 11F),
                    ForeColor = System.Drawing.Color.FromArgb(50, 50, 50),
                    Margin = new Padding(0, 8, 0, 8),
                    Padding = new Padding(4),
                    Cursor = Cursors.Hand
                };
                rb.CheckedChanged += Choice_CheckedChanged;
                pnlChoices.Controls.Add(rb);
            }
        }

        private void Choice_CheckedChanged(object? sender, EventArgs e)
        {
            if (sender is not RadioButton rb) return;
            if (!rb.Checked) return;

            var ans = rb.Tag?.ToString();
            _userAnswers[_idx] = ans;

            var total = Math.Max(1, _group.CorrectAnswers.Count);

            // [SPECIAL] chỉ auto-next cho dạng thường
            if (AllowAutoNextForThisMode() && total > 1 && _idx < total - 1)
            {
                LoadIndex(_idx + 1);
                return;
            }

            var answered = _userAnswers.Count(a => !string.IsNullOrWhiteSpace(a));
            lblIndex.Text = $"📝 Câu {_idx + 1} / {total} (đã chọn {answered}/{total})";
        }

        // ===================== [SUBMIT] =====================
        private void Submit()
        {
            int total = Math.Max(1, _group.CorrectAnswers.Count);

            if (_userAnswers.Any(a => string.IsNullOrWhiteSpace(a)))
            {
                var r = MessageBox.Show("Còn câu chưa chọn đáp án. Vẫn nộp?", "Xác nhận",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (r != DialogResult.Yes) return;
            }

            int correct = 0;
            for (int i = 0; i < total; i++)
            {
                var user = (_userAnswers[i] ?? "").Trim().ToUpperInvariant();
                var cor = (_group.CorrectAnswers.ElementAtOrDefault(i) ?? "").Trim().ToUpperInvariant();
                if (!string.IsNullOrWhiteSpace(user) && user == cor) correct++;
            }

            bool allCorrect = (correct == total);

            var pr = _store.GetOrCreate(_progressMap, _group.FileId);
            var now = DateTime.Now;

            if (pr.NextDue == DateTime.MinValue && allCorrect)
            {
                pr.LastAttempt = now;
                pr.LastCorrect = correct;
                pr.LastTotal = total;
                pr.Stage = 0;
                pr.NextDue = _sr.ComputeFirstDue(now);
            }
            else
            {
                _sr.ApplyResult(pr, now, allCorrect, correct, total);
            }

            _store.Save(_progressMap);

            var nd = pr.NextDue == DateTime.MinValue ? "--" : pr.NextDue.ToString("yyyy-MM-dd");
            MessageBox.Show(
                $"✅ Kết quả: {correct}/{total}\n" +
                $"Làm đúng hết: {(allCorrect ? "Có ✓" : "Không ✗")}\n" +
                $"Stage hiện tại: {pr.Stage}\n" +
                $"Ngày đến hạn: {nd}\n\n" +
                $"💡 Lưu ý: Nếu làm trước hạn và đúng hết → không nâng stage/không đổi NextDue",
                "Đã lưu kết quả",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            LoadIndex(_idx);
        }

        // ===================== [OPEN] =====================
        private static void OpenExternal(string? path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
                {
                    MessageBox.Show("File không tồn tại.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch
            {
                MessageBox.Show("Không mở được file bằng ứng dụng mặc định.", "Lỗi",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ===================== [STATUS] realtime label =====================
        private void RefreshStatusLabelOnly()
        {
            var today = DateTime.Now.Date;

            if (_progressMap.TryGetValue(_group.FileId, out var pr) && pr.IsDone)
            {
                var nd = pr.NextDue == DateTime.MinValue ? "--" : pr.NextDue.ToString("yyyy-MM-dd");
                var dueText = pr.IsDue(today) ? "DUE" : "NOT DUE";

                var countdown = "";
                if (pr.NextDue != DateTime.MinValue && pr.NextDue.Date > today)
                {
                    var target = pr.NextDue.Date;
                    var ts = target - DateTime.Now;
                    if (ts.TotalSeconds < 0) ts = TimeSpan.Zero;
                    countdown = $" | Còn: {FormatTs(ts)}";
                }

                lblStatus.Text = $"Stage: {pr.Stage} | NextDue: {nd} | {dueText}{countdown}";
            }
            else
            {
                lblStatus.Text = "NEW";
            }
        }

        private static string FormatTs(TimeSpan ts)
        {
            var days = (int)ts.TotalDays;
            return $"{days}d {ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}";
        }

        private void QuizForm_Load(object sender, EventArgs e)
        {
            StartPosition = FormStartPosition.CenterScreen;
        }
    }
}
