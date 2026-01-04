using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;
using TocflQuiz.Models;
using TocflQuiz.Controls.Features.Quiz;

namespace TocflQuiz.Controls.Features
{
    public sealed partial class QuizFeatureControl : UserControl
    {
        // ===== result overlay =====
        private readonly OverlayPanel _resultOverlay = new();
        private readonly RoundedPanel _resultDlg = new();

        private readonly Label _resSetTitle = new();
        private readonly Label _resTitle = new();
        private readonly Label _resIcon = new();

        private readonly Label _resScore = new();
        private readonly Label _resPercent = new();
        private readonly Label _resTime = new();

        private readonly Button _resClose = new();
        private readonly RoundedButton _btnViewResult = new();
        private readonly RoundedButton _btnExit = new();

        // ===== state =====
        private CardSet? _set;
        public event Action? ExitToCourseListRequested;

        private bool _submitted;
        private DateTime _startedAt;
        private TimeSpan _elapsed;

        // ===== header (0/20 + set title) =====
        private readonly Label _lblProgress = new();
        private readonly Label _lblSetTitle = new();

        // ===== scroll host =====
        private readonly Panel _scroll = new();
        private readonly VerticalStackPanel _stack = new();

        // ===== overlay setup =====
        private readonly OverlayPanel _overlay = new();
        private readonly RoundedPanel _dlg = new();

        private readonly Button _btnClose = new();
        private readonly Label _dlgSetTitle = new();
        private readonly Label _dlgTitle = new();
        private readonly Label _dlgIcon = new();

        private readonly Label _lblMax = new();

        private readonly PillNumberBox _pillCount = new();
        private readonly PillComboBox _pillAnswer = new();
        private readonly ToggleSwitchLike _tgMulti = new();

        private readonly RoundedButton _btnStart = new();

        private QuizConfig _cfg = new();

        // ===== quiz state =====
        private readonly List<QuizQuestionCard> _cards = new();
        private SubmitSectionControl? _submit;
        private int _answered;
        private int _total;
        private int _correct;

        // ===== fonts for Traditional Chinese =====
        private static readonly string[] TcFontFamilies =
        {
            "DFKai-SB",
            "BiauKai",
            "KaiTi",
            "STKaiti",
            "Microsoft JhengHei UI",
            "Microsoft JhengHei",
            "PMingLiU",
            "MingLiU"
        };
        private static readonly string TcPrimaryFontName = PickInstalledFont(TcFontFamilies) ?? "Microsoft JhengHei";

        public QuizFeatureControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9F);

            BuildUi();
            Wire();

            ShowEmptyState();
            ShowSetup();
        }

        public void BindSelectedSet(CardSet? set)
        {
            _set = set;

            var title = set?.Title ?? "(chưa chọn)";
            _lblSetTitle.Text = title;
            _dlgSetTitle.Text = title;

            var max = set?.Items?.Count ?? 0;
            _lblMax.Text = $"Câu hỏi (tối đa {max})";

            _pillCount.Maximum = Math.Max(0, max);
            _pillCount.Minimum = max > 0 ? 1 : 0;
            _pillCount.Value = max > 0 ? Math.Min(20, max) : 0;

            ShowEmptyState();
            ShowSetup();
        }

        // ================= UI =================
        private void BuildUi()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var header = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };

            _lblProgress.AutoSize = false;
            _lblProgress.TextAlign = ContentAlignment.MiddleCenter;
            _lblProgress.Dock = DockStyle.Top;
            _lblProgress.Height = 30;
            _lblProgress.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            _lblProgress.ForeColor = Color.FromArgb(30, 30, 30);
            _lblProgress.Text = "0 / 0";

            _lblSetTitle.AutoSize = false;
            _lblSetTitle.TextAlign = ContentAlignment.TopCenter;
            _lblSetTitle.Dock = DockStyle.Top;
            _lblSetTitle.Height = 28;
            _lblSetTitle.Font = new Font("Segoe UI", 10.5F, FontStyle.Regular);
            _lblSetTitle.ForeColor = Color.FromArgb(90, 90, 90);
            _lblSetTitle.Text = "(chưa chọn)";

            header.Controls.Add(_lblSetTitle);
            header.Controls.Add(_lblProgress);

            _scroll.Dock = DockStyle.Fill;
            _scroll.AutoScroll = true;
            _scroll.BackColor = Color.FromArgb(245, 245, 245);

            _stack.Dock = DockStyle.Top;
            _stack.BackColor = _scroll.BackColor;
            _stack.Padding = new Padding(0, 18, 0, 30);
            _stack.Spacing = 18;

            _scroll.Controls.Add(_stack);

            root.Controls.Add(header, 0, 0);
            root.Controls.Add(_scroll, 0, 1);

            Controls.Clear();
            Controls.Add(root);

            BuildSetupOverlay();
            BuildResultOverlay(); // ✅ THIẾU trước đó

            _scroll.SizeChanged += (_, __) => ResizeCardsToFit();
        }

        private void ResizeCardsToFit()
        {
            int w = Math.Max(520, _scroll.ClientSize.Width - 80);
            w = Math.Min(760, w);

            foreach (var c in _cards) c.SetCardWidth(w);
            _submit?.SetCardWidth(w);

            _stack.PerformLayout();
        }

        private void BuildSetupOverlay()
        {
            _overlay.Dock = DockStyle.Fill;
            _overlay.Visible = false;

            _dlg.Width = 760;
            _dlg.Height = 520;
            _dlg.Radius = 18;
            _dlg.BackColor = Color.White;
            _dlg.BorderColor = Color.FromArgb(235, 235, 235);
            _dlg.BorderThickness = 1;
            _dlg.Shadow = true;
            _dlg.Padding = new Padding(28);

            _btnClose.Text = "×";
            _btnClose.Width = 44;
            _btnClose.Height = 44;
            _btnClose.FlatStyle = FlatStyle.Flat;
            _btnClose.FlatAppearance.BorderSize = 0;
            _btnClose.BackColor = Color.FromArgb(245, 245, 245);
            _btnClose.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
            _btnClose.ForeColor = Color.FromArgb(90, 90, 90);
            _btnClose.Cursor = Cursors.Hand;

            _dlgSetTitle.Text = "(chưa chọn)";
            _dlgSetTitle.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            _dlgSetTitle.ForeColor = Color.FromArgb(60, 60, 60);
            _dlgSetTitle.AutoSize = true;

            _dlgTitle.Text = "Thiết lập bài kiểm tra";
            _dlgTitle.Font = new Font("Segoe UI", 22F, FontStyle.Bold);
            _dlgTitle.ForeColor = Color.FromArgb(35, 35, 35);
            _dlgTitle.AutoSize = true;

            _dlgIcon.Text = "📄";
            _dlgIcon.Font = new Font("Segoe UI Emoji", 42F, FontStyle.Regular);
            _dlgIcon.AutoSize = true;

            _lblMax.Text = "Câu hỏi (tối đa 0)";
            _lblMax.Font = new Font("Segoe UI", 11.5F, FontStyle.Bold);
            _lblMax.ForeColor = Color.FromArgb(60, 60, 60);
            _lblMax.AutoSize = true;

            var lblAnswer = new Label
            {
                Text = "Trả lời bằng",
                Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(60, 60, 60),
                AutoSize = true
            };

            _pillCount.Width = 110;
            _pillCount.Height = 46;

            _pillAnswer.Width = 240;
            _pillAnswer.Height = 46;
            _pillAnswer.Items.AddRange(new object[]
            {
                "Tiếng Trung (Phồn thể)",
                "Tiếng Việt",
                "Cả hai"
            });
            _pillAnswer.SelectedIndex = 0;

            _tgMulti.Value = true;

            _btnStart.Text = "Bắt đầu làm kiểm tra";
            _btnStart.Height = 48;
            _btnStart.Width = 240;
            _btnStart.Radius = 14;
            _btnStart.BorderThickness = 0;
            _btnStart.BackColor = Color.FromArgb(62, 92, 255);
            _btnStart.BorderColor = _btnStart.BackColor;
            _btnStart.ForeColor = Color.White;
            _btnStart.Font = new Font("Segoe UI", 11.5F, FontStyle.Bold);
            _btnStart.Cursor = Cursors.Hand;

            var dlgRoot = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = Color.White
            };
            dlgRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 140));
            dlgRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 130));
            dlgRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            dlgRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));

            var header = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            var headerGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.White
            };
            headerGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
            headerGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));

            var leftHeader = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            leftHeader.Controls.Add(_dlgTitle);
            leftHeader.Controls.Add(_dlgSetTitle);
            _dlgSetTitle.Location = new Point(0, 8);
            _dlgTitle.Location = new Point(0, 40);

            var rightHeader = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            rightHeader.Controls.Add(_dlgIcon);
            rightHeader.Controls.Add(_btnClose);
            rightHeader.Layout += (_, __) =>
            {
                _btnClose.Location = new Point(rightHeader.ClientSize.Width - _btnClose.Width, 0);
                _dlgIcon.Location = new Point(rightHeader.ClientSize.Width - _dlgIcon.Width - 6, 46);
            };

            headerGrid.Controls.Add(leftHeader, 0, 0);
            headerGrid.Controls.Add(rightHeader, 1, 0);
            header.Controls.Add(headerGrid);

            var inputs = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                BackColor = Color.White
            };
            inputs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            inputs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            inputs.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            inputs.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));

            var rowCountLeft = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            rowCountLeft.Controls.Add(_lblMax);
            _lblMax.Location = new Point(0, 18);

            var rowCountRight = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            rowCountRight.Controls.Add(_pillCount);
            rowCountRight.Layout += (_, __) => _pillCount.Location = new Point(rowCountRight.ClientSize.Width - _pillCount.Width, 8);

            var rowAnswerLeft = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            rowAnswerLeft.Controls.Add(lblAnswer);
            lblAnswer.Location = new Point(0, 18);

            var rowAnswerRight = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            rowAnswerRight.Controls.Add(_pillAnswer);
            rowAnswerRight.Layout += (_, __) => _pillAnswer.Location = new Point(rowAnswerRight.ClientSize.Width - _pillAnswer.Width, 8);

            inputs.Controls.Add(rowCountLeft, 0, 0);
            inputs.Controls.Add(rowCountRight, 1, 0);
            inputs.Controls.Add(rowAnswerLeft, 0, 1);
            inputs.Controls.Add(rowAnswerRight, 1, 1);

            var sep = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Color.FromArgb(235, 235, 235) };

            var toggles = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.White,
                Padding = new Padding(0, 10, 0, 0)
            };
            toggles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 80));
            toggles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
            toggles.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));

            AddToggleRow(toggles, 0, "Trắc nghiệm (4 đáp án)", _tgMulti);

            var body = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            body.Controls.Add(toggles);
            body.Controls.Add(sep);

            var bottom = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            bottom.Controls.Add(_btnStart);
            bottom.Layout += (_, __) => _btnStart.Location = new Point(bottom.ClientSize.Width - _btnStart.Width, 12);

            dlgRoot.Controls.Add(header, 0, 0);
            dlgRoot.Controls.Add(inputs, 0, 1);
            dlgRoot.Controls.Add(body, 0, 2);
            dlgRoot.Controls.Add(bottom, 0, 3);

            _dlg.Controls.Clear();
            _dlg.Controls.Add(dlgRoot);

            _overlay.Controls.Add(_dlg);
            Controls.Add(_overlay);

            Layout += (_, __) => CenterDialog();
            _overlay.Layout += (_, __) => CenterDialog();
        }

        private void BuildResultOverlay()
        {
            _resultOverlay.Dock = DockStyle.Fill;
            _resultOverlay.Visible = false;

            _resultDlg.Width = 760;
            _resultDlg.Height = 420;
            _resultDlg.Radius = 18;
            _resultDlg.BackColor = Color.White;
            _resultDlg.BorderColor = Color.FromArgb(235, 235, 235);
            _resultDlg.BorderThickness = 1;
            _resultDlg.Shadow = true;
            _resultDlg.Padding = new Padding(28);

            _resIcon.Text = "✅";
            _resIcon.Font = new Font("Segoe UI Emoji", 36F, FontStyle.Regular);
            _resIcon.AutoSize = true;

            _resSetTitle.Text = "(ngày ?)";
            _resSetTitle.Font = new Font("Segoe UI", 11.5F, FontStyle.Bold);
            _resSetTitle.ForeColor = Color.FromArgb(90, 100, 150);
            _resSetTitle.AutoSize = true;

            _resTitle.Text = "Kết quả bài kiểm tra";
            _resTitle.Font = new Font("Segoe UI", 22F, FontStyle.Bold);
            _resTitle.ForeColor = Color.FromArgb(35, 35, 35);
            _resTitle.AutoSize = true;

            _resClose.Text = "×";
            _resClose.Width = 44;
            _resClose.Height = 44;
            _resClose.FlatStyle = FlatStyle.Flat;
            _resClose.FlatAppearance.BorderSize = 0;
            _resClose.BackColor = Color.FromArgb(245, 245, 245);
            _resClose.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
            _resClose.ForeColor = Color.FromArgb(90, 90, 90);
            _resClose.Cursor = Cursors.Hand;
            _resClose.Click += (_, __) => HideResultOverlay();

            _resScore.Text = "0/0";
            _resScore.Font = new Font("Segoe UI", 22F, FontStyle.Bold);
            _resScore.ForeColor = Color.FromArgb(35, 35, 35);
            _resScore.AutoSize = true;

            _resPercent.Text = "0%";
            _resPercent.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
            _resPercent.ForeColor = Color.FromArgb(90, 90, 90);
            _resPercent.AutoSize = true;

            _resTime.Text = "Thời gian: —";
            _resTime.Font = new Font("Segoe UI", 12F, FontStyle.Regular);
            _resTime.ForeColor = Color.FromArgb(110, 110, 110);
            _resTime.AutoSize = true;

            StylePrimaryButton(_btnViewResult, "Xem kết quả");
            StyleGhostButton(_btnExit, "Thoát");

            _btnViewResult.Click += (_, __) =>
            {
                HideResultOverlay();
                ApplyReviewToAllCards();
                ScrollToFirstWrongIfAny();

                // ✅ đổi nút cuối trang thành "Quay về trang chủ"
                if (_submit != null)
                {
                    _submit.SetMode(SubmitSectionControl.Mode.GoHome);
                    _submit.EnableSubmit(true); // để chắc chắn clickable
                }
            };


            _btnExit.Click += (_, __) =>
            {
                HideResultOverlay();
                ExitToCourseListRequested?.Invoke();
            };

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Color.White
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));

            // Header
            var header = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };

            var leftHeader = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            leftHeader.Controls.Add(_resSetTitle);
            leftHeader.Controls.Add(_resTitle);
            _resSetTitle.Location = new Point(0, 8);
            _resTitle.Location = new Point(0, 40);

            var rightHeader = new Panel { Dock = DockStyle.Right, Width = 140, BackColor = Color.White };
            rightHeader.Controls.Add(_resIcon);
            rightHeader.Controls.Add(_resClose);
            rightHeader.Layout += (_, __) =>
            {
                _resClose.Location = new Point(rightHeader.ClientSize.Width - _resClose.Width, 0);
                _resIcon.Location = new Point(rightHeader.ClientSize.Width - _resIcon.Width - 6, 50);
            };

            header.Controls.Add(rightHeader);
            header.Controls.Add(leftHeader);

            // Summary block
            var summaryWrap = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                Radius = 16,
                BackColor = Color.FromArgb(245, 246, 250),
                BorderColor = Color.FromArgb(230, 232, 238),
                BorderThickness = 1,
                Shadow = false,
                Padding = new Padding(22)
            };

            var summary = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                BackColor = summaryWrap.BackColor
            };
            summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            summary.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
            summary.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));

            var scoreHost = new Panel { Dock = DockStyle.Fill, BackColor = summaryWrap.BackColor };
            scoreHost.Controls.Add(_resScore);
            scoreHost.Layout += (_, __) => _resScore.Location = new Point(0, 10);

            var percentHost = new Panel { Dock = DockStyle.Fill, BackColor = summaryWrap.BackColor };
            percentHost.Controls.Add(_resPercent);
            percentHost.Layout += (_, __) =>
                _resPercent.Location = new Point(percentHost.ClientSize.Width - _resPercent.Width, 28);

            var timeHost = new Panel { Dock = DockStyle.Fill, BackColor = summaryWrap.BackColor };
            timeHost.Controls.Add(_resTime);
            timeHost.Layout += (_, __) => _resTime.Location = new Point(0, 8);

            summary.Controls.Add(scoreHost, 0, 0);
            summary.Controls.Add(percentHost, 1, 0);
            summary.Controls.Add(timeHost, 0, 1);
            summary.SetColumnSpan(timeHost, 2);

            summaryWrap.Controls.Add(summary);

            // Buttons
            var btnRow = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            btnRow.Controls.Add(_btnViewResult);
            btnRow.Controls.Add(_btnExit);
            btnRow.Layout += (_, __) =>
            {
                int gap = 12;
                int totalW = _btnViewResult.Width + gap + _btnExit.Width;
                int x = (btnRow.ClientSize.Width - totalW) / 2;
                int y = 18;

                _btnViewResult.Location = new Point(x, y);
                _btnExit.Location = new Point(x + _btnViewResult.Width + gap, y);
            };

            root.Controls.Add(header, 0, 0);
            root.Controls.Add(summaryWrap, 0, 1);
            root.Controls.Add(btnRow, 0, 2);

            _resultDlg.Controls.Clear();
            _resultDlg.Controls.Add(root);

            _resultOverlay.Controls.Add(_resultDlg);
            Controls.Add(_resultOverlay);

            Layout += (_, __) => CenterResultDialog();
            _resultOverlay.Layout += (_, __) => CenterResultDialog();
        }

        private static void AddToggleRow(TableLayoutPanel host, int row, string text, ToggleSwitchLike toggle)
        {
            var lbl = new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(60, 60, 60)
            };

            var right = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            right.Controls.Add(toggle);
            right.Layout += (_, __) =>
            {
                toggle.Location = new Point(right.ClientSize.Width - toggle.Width - 4, (right.ClientSize.Height - toggle.Height) / 2);
            };

            host.Controls.Add(lbl, 0, row);
            host.Controls.Add(right, 1, row);
        }

        private void CenterDialog()
        {
            if (!_overlay.Visible) return;

            int x = Math.Max(0, (ClientSize.Width - _dlg.Width) / 2);
            int y = Math.Max(0, (ClientSize.Height - _dlg.Height) / 2);
            _dlg.Location = new Point(x, y);
        }

        private void CenterResultDialog()
        {
            if (!_resultOverlay.Visible) return;

            int x = Math.Max(0, (ClientSize.Width - _resultDlg.Width) / 2);
            int y = Math.Max(0, (ClientSize.Height - _resultDlg.Height) / 2);
            _resultDlg.Location = new Point(x, y);
        }

        private void Wire()
        {
            _btnClose.Click += (_, __) => HideSetup();

            _btnStart.Click += (_, __) =>
            {
                if (_set?.Items == null || _set.Items.Count == 0)
                {
                    MessageBox.Show("Chưa có thẻ trong học phần. Hãy tạo học phần trước.", "Kiểm tra",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                _cfg = new QuizConfig
                {
                    Count = (int)_pillCount.Value,
                    AnswerMode = (AnswerMode)_pillAnswer.SelectedIndex,
                    EnableMultipleChoice = _tgMulti.Value
                };

                HideSetup();
                StartQuiz();
            };
        }

        private void ShowSetup()
        {
            _overlay.Visible = true;
            _overlay.BringToFront();
            CenterDialog();
        }

        private void HideSetup() => _overlay.Visible = false;

        private void ShowResultOverlay(int correct, int total, TimeSpan elapsed)
        {
            _resSetTitle.Text = _set?.Title ?? "(chưa chọn)";
            _resScore.Text = $"{correct}/{total}";

            int percent = total <= 0 ? 0 : (int)Math.Round((correct * 100.0) / total);
            _resPercent.Text = $"{percent}%";

            if (elapsed.TotalSeconds <= 0.5)
                _resTime.Text = "Thời gian: —";
            else
                _resTime.Text = $"Thời gian: {elapsed:mm\\:ss}";

            _resultOverlay.Visible = true;
            _resultOverlay.BringToFront();
            CenterResultDialog();
        }

        private void HideResultOverlay() => _resultOverlay.Visible = false;

        // ================= QUIZ =================
        private void ShowEmptyState()
        {
            _cards.Clear();
            _stack.Controls.Clear();
            _submit = null;

            _answered = 0;
            _correct = 0;
            _total = 0;
            UpdateHeader();

            var empty = new Label
            {
                AutoSize = false,
                Width = 700,
                Height = 160,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 11.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(110, 110, 110),
                Text = "Bấm \"Bắt đầu làm kiểm tra\" để tạo bài kiểm tra."
            };

            _stack.Controls.Add(empty);
            ResizeCardsToFit();
        }

        private void StartQuiz()
        {
            if (_set == null) return;

            if (!_cfg.EnableMultipleChoice)
            {
                MessageBox.Show("Hiện tại chỉ hỗ trợ Trắc nghiệm. Hãy bật Trắc nghiệm.", "Kiểm tra",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                ShowSetup();
                return;
            }

            var questions = QuizEngine.BuildQuestions(_set, _cfg);

            if (questions.Count < 1)
            {
                MessageBox.Show("Học phần cần ít nhất 4 thẻ để tạo trắc nghiệm 4 đáp án.", "Kiểm tra",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _stack.SuspendLayout();
            _cards.Clear();
            _stack.Controls.Clear();

            _answered = 0;
            _correct = 0;
            _total = questions.Count;

            _submitted = false;
            _startedAt = DateTime.Now;
            _elapsed = TimeSpan.Zero;

            UpdateHeader();

            for (int i = 0; i < questions.Count; i++)
            {
                var q = questions[i];

                var card = new QuizQuestionCard(q, _scroll.BackColor);
                card.SelectionChanged += (_, __) =>
                {
                    if (_submitted) return;

                    _answered = _cards.Count(c => c.IsAnswered);
                    UpdateHeader();

                    if (_answered >= _total) ShowSubmitSection();
                    else HideSubmitSection();

                    FocusNextQuestion(card.QuestionIndex);
                };

                _cards.Add(card);
                _stack.Controls.Add(card);
            }

            // ===== submit section =====
            _submit = new SubmitSectionControl();
            _submit.Visible = false;

            // reset về mode Submit mỗi lần bắt đầu quiz mới
            _submit.SetMode(SubmitSectionControl.Mode.Submit);
            _submit.EnableSubmit(false);

            _submit.SubmitClicked += (_, __) => SubmitQuiz();
            _submit.GoHomeClicked += (_, __) => ExitToCourseListRequested?.Invoke();

            _stack.Controls.Add(_submit);

            _stack.ResumeLayout();
            ResizeCardsToFit();

            if (_cards.Count > 0)
            {
                _scroll.ScrollControlIntoView(_cards[0]);
                _cards[0].FocusFirstChoice();
            }
        }


        private void SubmitQuiz()
        {
            if (_submitted) return;

            _answered = _cards.Count(c => c.IsAnswered);
            UpdateHeader();

            if (_answered < _total)
            {
                // chưa trả lời hết thì chỉ scroll xuống, không chấm
                ShowSubmitSection();
                _submit?.EnableSubmit(false);
                return;
            }

            _submitted = true;
            _elapsed = DateTime.Now - _startedAt;

            int correct = 0;
            foreach (var c in _cards)
            {
                if (c.IsCorrectNow()) correct++;
                c.Lock(); // chốt bài: không cho đổi nữa
            }

            _correct = correct;
            ShowResultOverlay(_correct, _total, _elapsed);
        }

        private void ShowSubmitSection()
        {
            if (_submit == null) return;

            _submit.Visible = true;
            _submit.EnableSubmit(_answered >= _total && !_submitted);
            ResizeCardsToFit();
            _scroll.ScrollControlIntoView(_submit);
        }

        private void HideSubmitSection()
        {
            if (_submit == null) return;
            _submit.Visible = false;
        }

        private void FocusNextQuestion(int currentIndex1Based)
        {
            for (int i = Math.Max(0, currentIndex1Based - 1); i < _cards.Count; i++)
            {
                if (!_cards[i].IsAnswered)
                {
                    _scroll.ScrollControlIntoView(_cards[i]);
                    _cards[i].FocusFirstChoice();
                    return;
                }
            }

            if (_submit != null && _submit.Visible)
                _scroll.ScrollControlIntoView(_submit);
        }

        private void UpdateHeader() => _lblProgress.Text = $"{_answered} / {_total}";

        private void ApplyReviewToAllCards()
        {
            var greenBg = Color.FromArgb(236, 253, 245);
            var greenBorder = Color.FromArgb(16, 185, 129);

            var redBg = Color.FromArgb(254, 242, 242);
            var redBorder = Color.FromArgb(239, 68, 68);

            foreach (var c in _cards)
                c.ApplyReview(greenBg, greenBorder, redBg, redBorder);

            _stack.PerformLayout();
        }

        private void ScrollToFirstWrongIfAny()
        {
            // scroll tới câu sai/không biết đầu tiên cho tiện xem
            foreach (var c in _cards)
            {
                if (!c.IsCorrectNow())
                {
                    _scroll.ScrollControlIntoView(c);
                    return;
                }
            }
            if (_cards.Count > 0) _scroll.ScrollControlIntoView(_cards[0]);
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

        // ================= Question Card UI =================
        private sealed class QuizQuestionCard : UserControl
        {
            private readonly QuizQuestion _q;
            private readonly Color _pageBg;

            public int QuestionIndex => _q.Index;

            public string? SelectedChoice { get; private set; }
            public bool DontKnow { get; private set; }
            public bool Locked { get; private set; }

            public bool IsAnswered => SelectedChoice != null || DontKnow;

            public event EventHandler? SelectionChanged;

            private readonly RoundedPanel _card = new();
            private readonly Label _lblSmall = new();
            private readonly Label _lblIndex = new();
            private readonly Label _lblQuestion = new();
            private readonly Label _lblHint = new();
            private readonly RoundedButton[] _btn = new RoundedButton[4];
            private readonly LinkLabel _lnkDontKnow = new();

            public QuizQuestionCard(QuizQuestion q, Color pageBg)
            {
                _q = q;
                _pageBg = pageBg;

                BackColor = _pageBg;
                AutoSize = true;
                AutoSizeMode = AutoSizeMode.GrowAndShrink;

                Build();
            }

            public void SetCardWidth(int w)
            {
                Width = w;
                MinimumSize = new Size(w, 0);
                MaximumSize = new Size(w, 0);

                _card.Width = w;
                _card.MinimumSize = new Size(w, 0);
                _card.MaximumSize = new Size(w, 0);

                Invalidate();
            }

            public void FocusFirstChoice()
            {
                if (_btn.Length > 0)
                    _btn[0].Focus();
            }

            private void Build()
            {
                _card.Radius = 16;
                _card.BackColor = Color.White;
                _card.BorderColor = Color.FromArgb(235, 235, 235);
                _card.BorderThickness = 1;
                _card.Shadow = true;
                _card.Padding = new Padding(22);
                _card.AutoSize = true;
                _card.AutoSizeMode = AutoSizeMode.GrowAndShrink;

                var topRow = new Panel { Dock = DockStyle.Top, Height = 34, BackColor = Color.White };

                _lblSmall.Text = _q.SmallLabel;
                _lblSmall.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
                _lblSmall.ForeColor = Color.FromArgb(90, 100, 150);
                _lblSmall.AutoSize = true;

                _lblIndex.Text = $"{_q.Index}/{_q.Total}";
                _lblIndex.Font = new Font("Segoe UI", 10F, FontStyle.Regular);
                _lblIndex.ForeColor = Color.FromArgb(140, 140, 140);
                _lblIndex.AutoSize = true;

                topRow.Controls.Add(_lblSmall);
                topRow.Controls.Add(_lblIndex);
                topRow.Layout += (_, __) =>
                {
                    _lblSmall.Location = new Point(0, 6);
                    _lblIndex.Location = new Point(topRow.ClientSize.Width - _lblIndex.Width, 6);
                };

                _lblQuestion.Text = _q.QuestionText;
                _lblQuestion.AutoSize = false;
                _lblQuestion.Dock = DockStyle.Top;
                _lblQuestion.Height = 86;
                _lblQuestion.TextAlign = ContentAlignment.MiddleLeft;
                _lblQuestion.ForeColor = Color.FromArgb(30, 30, 30);
                _lblQuestion.Font = _q.UseChineseFontForQuestion
                    ? new Font(TcPrimaryFontName, 22F, FontStyle.Regular)
                    : new Font("Segoe UI", 16F, FontStyle.Regular);

                _lblHint.Text = "Chọn đáp án đúng";
                _lblHint.Dock = DockStyle.Top;
                _lblHint.Height = 34;
                _lblHint.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
                _lblHint.ForeColor = Color.FromArgb(110, 110, 110);

                var grid = new TableLayoutPanel
                {
                    Dock = DockStyle.Top,
                    ColumnCount = 2,
                    RowCount = 2,
                    Height = 176,
                    BackColor = Color.White,
                    Padding = new Padding(0),
                    Margin = new Padding(0)
                };
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
                grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
                grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

                for (int i = 0; i < 4; i++)
                {
                    _btn[i] = MakeChoiceButton(_q.Choices[i]);
                    int idx = i;
                    _btn[i].Click += (_, __) => Answer(_btn[idx].Text);
                    grid.Controls.Add(_btn[i], i % 2, i / 2);
                }

                _lnkDontKnow.Text = "Bạn không biết?";
                _lnkDontKnow.Dock = DockStyle.Top;
                _lnkDontKnow.Height = 30;
                _lnkDontKnow.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
                _lnkDontKnow.LinkColor = Color.FromArgb(62, 92, 255);
                _lnkDontKnow.TextAlign = ContentAlignment.MiddleCenter;
                _lnkDontKnow.Click += (_, __) =>
                {
                    if (Locked) return;

                    SelectedChoice = null;
                    DontKnow = true;

                    ClearSelectedStyle();
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                };

                _card.Controls.Add(_lnkDontKnow);
                _card.Controls.Add(grid);
                _card.Controls.Add(_lblHint);
                _card.Controls.Add(_lblQuestion);
                _card.Controls.Add(topRow);

                Controls.Add(_card);
            }

            private RoundedButton MakeChoiceButton(string text)
            {
                var b = new RoundedButton
                {
                    Dock = DockStyle.Fill,
                    Height = 74,
                    Text = text,
                    Radius = 10,
                    BackColor = Color.White,
                    ForeColor = Color.FromArgb(45, 45, 45),
                    BorderColor = Color.FromArgb(230, 230, 230),
                    BorderThickness = 1,
                    Cursor = Cursors.Hand,
                    Margin = new Padding(10),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Padding = new Padding(0)
                };

                b.Font = _q.UseChineseFontForChoices
                    ? new Font(TcPrimaryFontName, 22F, FontStyle.Regular)
                    : new Font("Segoe UI", 12.5F, FontStyle.Regular);

                return b;
            }

            private void Answer(string picked)
            {
                if (Locked) return;

                SelectedChoice = picked;
                DontKnow = false;

                MarkSelected(picked);
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }

            public bool IsCorrectNow()
            {
                if (DontKnow) return false;
                if (SelectedChoice == null) return false;
                return string.Equals(SelectedChoice.Trim(), (_q.CorrectAnswer ?? "").Trim(), StringComparison.Ordinal);
            }

            public void Lock()
            {
                Locked = true;
                foreach (var b in _btn) b.Enabled = false;
                _lnkDontKnow.Enabled = false;
            }

            public void ApplyReview(Color greenBg, Color greenBorder, Color redBg, Color redBorder)
            {
                var correct = (_q.CorrectAnswer ?? "").Trim();
                var picked = (SelectedChoice ?? "").Trim();

                foreach (var b in _btn)
                {
                    var t = (b.Text ?? "").Trim();
                    bool isCorrect = string.Equals(t, correct, StringComparison.Ordinal);
                    bool isPicked = SelectedChoice != null && string.Equals(t, picked, StringComparison.Ordinal);

                    if (isCorrect)
                    {
                        b.BackColor = greenBg;
                        b.BorderColor = greenBorder;
                    }
                    else if (isPicked && !isCorrect)
                    {
                        b.BackColor = redBg;
                        b.BorderColor = redBorder;
                    }
                    else
                    {
                        b.BackColor = Color.White;
                        b.BorderColor = Color.FromArgb(230, 230, 230);
                    }
                    b.Invalidate();
                }
            }

            private void ClearSelectedStyle()
            {
                foreach (var b in _btn)
                {
                    b.BackColor = Color.White;
                    b.BorderColor = Color.FromArgb(230, 230, 230);
                    b.Invalidate();
                }
            }

            private void MarkSelected(string text)
            {
                foreach (var b in _btn)
                {
                    bool isSel = string.Equals((b.Text ?? "").Trim(), (text ?? "").Trim(), StringComparison.Ordinal);
                    b.BackColor = isSel ? Color.FromArgb(242, 247, 255) : Color.White;
                    b.BorderColor = isSel ? Color.FromArgb(140, 170, 255) : Color.FromArgb(230, 230, 230);
                    b.Invalidate();
                }
            }
        }

        // ================= Submit Section =================
        private sealed class SubmitSectionControl : UserControl
        {
            public enum Mode { Submit, GoHome }
            private Mode _mode = Mode.Submit;
            public event EventHandler? SubmitClicked;
            public event EventHandler? GoHomeClicked;


            private readonly Label _title = new();
            private readonly RoundedButton _btn = new();
            private readonly Label _icon = new();
            public void SetMode(Mode mode)
            {
                _mode = mode;

                if (mode == Mode.Submit)
                {
                    _title.Text = "Tất cả đã xong! Bạn đã sẵn sàng gửi bài kiểm tra?";
                    _btn.Text = "Gửi bài kiểm tra";

                    _btn.BackColor = Color.FromArgb(62, 92, 255);
                    _btn.BorderColor = _btn.BackColor;
                    _btn.ForeColor = Color.White;
                    _btn.Enabled = true;
                }
                else
                {
                    _title.Text = "Đã hiển thị kết quả. Bạn muốn quay về trang chủ?";
                    _btn.Text = "Quay về trang chủ";

                    // dùng style giống nút Thoát (ghost)
                    _btn.BackColor = Color.FromArgb(245, 246, 250);
                    _btn.BorderThickness = 1;
                    _btn.BorderColor = Color.FromArgb(230, 232, 238);
                    _btn.ForeColor = Color.FromArgb(50, 50, 50);
                    _btn.Enabled = true;
                }

                _title.Invalidate();
                _btn.Invalidate();
            }
            public SubmitSectionControl()
            {
                BackColor = Color.FromArgb(245, 245, 245);
                AutoSize = true;
                AutoSizeMode = AutoSizeMode.GrowAndShrink;

                _icon.Text = "📝";
                _icon.Font = new Font("Segoe UI Emoji", 34F, FontStyle.Regular);
                _icon.AutoSize = true;

                _title.Text = "Tất cả đã xong! Bạn đã sẵn sàng gửi bài kiểm tra?";
                _title.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
                _title.ForeColor = Color.FromArgb(35, 35, 35);
                _title.AutoSize = true;

                _btn.Text = "Gửi bài kiểm tra";
                _btn.Width = 220;
                _btn.Height = 46;

                _btn.Radius = 14;
                _btn.BorderThickness = 0;
                _btn.BackColor = Color.FromArgb(62, 92, 255);
                _btn.BorderColor = _btn.BackColor;
                _btn.ForeColor = Color.White;
                _btn.Font = new Font("Segoe UI", 11.5F, FontStyle.Bold);
                _btn.Cursor = Cursors.Hand;

                _btn.Click += (_, __) =>
                {
                    if (_mode == Mode.Submit) SubmitClicked?.Invoke(this, EventArgs.Empty);
                    else GoHomeClicked?.Invoke(this, EventArgs.Empty);
                };


                var wrap = new TableLayoutPanel
                {
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    ColumnCount = 1,
                    RowCount = 3,
                    BackColor = BackColor,
                    Padding = new Padding(0, 30, 0, 30)
                };
                wrap.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
                wrap.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                wrap.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));

                var iconRow = new Panel { Dock = DockStyle.Fill, BackColor = BackColor };
                iconRow.Controls.Add(_icon);
                iconRow.Layout += (_, __) => _icon.Location = new Point((iconRow.ClientSize.Width - _icon.Width) / 2, 6);

                var textRow = new Panel { Dock = DockStyle.Fill, BackColor = BackColor };
                textRow.Controls.Add(_title);
                textRow.Layout += (_, __) => _title.Location = new Point((textRow.ClientSize.Width - _title.Width) / 2, 0);

                var btnRow = new Panel { Dock = DockStyle.Fill, BackColor = BackColor };
                btnRow.Controls.Add(_btn);
                btnRow.Layout += (_, __) => _btn.Location = new Point((btnRow.ClientSize.Width - _btn.Width) / 2, 12);

                wrap.Controls.Add(iconRow, 0, 0);
                wrap.Controls.Add(textRow, 0, 1);
                wrap.Controls.Add(btnRow, 0, 2);

                Controls.Add(wrap);
            }

            public void SetCardWidth(int w)
            {
                Width = w;
                MinimumSize = new Size(w, 0);
                MaximumSize = new Size(w, 0);
            }

            public void EnableSubmit(bool enabled)
            {
                _btn.Enabled = enabled;
                _btn.BackColor = enabled ? Color.FromArgb(62, 92, 255) : Color.FromArgb(190, 190, 200);
                _btn.BorderColor = _btn.BackColor;
            }
        }

        // ================= Stack panel =================
        private sealed class VerticalStackPanel : Panel
        {
            public int Spacing { get; set; } = 16;

            public VerticalStackPanel()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.ResizeRedraw |
                         ControlStyles.UserPaint, true);
            }

            protected override void OnLayout(LayoutEventArgs levent)
            {
                base.OnLayout(levent);

                int availW = Math.Max(0, ClientSize.Width - Padding.Horizontal);
                int y = Padding.Top;

                foreach (Control c in Controls)
                {
                    if (!c.Visible) continue;

                    if (c.Width > availW && availW > 0)
                    {
                        c.Width = availW;
                        c.MinimumSize = new Size(availW, 0);
                        c.MaximumSize = new Size(availW, 0);
                    }

                    int x = Padding.Left + Math.Max(0, (availW - c.Width) / 2);
                    c.Location = new Point(x, y);

                    y += c.Height + Spacing;
                }

                Height = y + Padding.Bottom;
            }
        }

        // ================= Overlay + Panels + Buttons =================
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
                    using var pen = new Pen(BorderColor, BorderThickness);
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

                var rect = new Rectangle(0, 0, Width - 1, Height - 1);
                int r = Math.Max(1, Math.Min(Radius, Math.Min(Width, Height) / 2));

                using var path = RoundedRect(rect, r);
                using var fill = new SolidBrush(BackColor);

                g.FillPath(fill, path);

                if (BorderThickness > 0)
                {
                    using var pen = new Pen(BorderColor, BorderThickness);
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

        // ================= Modern pill inputs =================
        private sealed class PillNumberBox : UserControl
        {
            private readonly TextBox _txt = new();
            private readonly Panel _btnHost = new();
            private readonly Button _btnUp = new();
            private readonly Button _btnDown = new();

            private decimal _min = 0;
            private decimal _max = 0;
            private decimal _value = 0;

            public decimal Minimum { get => _min; set { _min = value; ClampAndSync(); } }
            public decimal Maximum { get => _max; set { _max = value; ClampAndSync(); } }
            public decimal Value { get => _value; set { _value = value; ClampAndSync(); } }

            public PillNumberBox()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.ResizeRedraw |
                         ControlStyles.UserPaint, true);

                BackColor = Color.FromArgb(245, 246, 250);
                Padding = new Padding(12, 9, 12, 9);

                _txt.BorderStyle = BorderStyle.None;
                _txt.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
                _txt.BackColor = BackColor;
                _txt.ForeColor = Color.FromArgb(40, 40, 40);
                _txt.TextAlign = HorizontalAlignment.Right;

                _txt.KeyPress += (_, e) =>
                {
                    if (char.IsControl(e.KeyChar)) return;
                    if (!char.IsDigit(e.KeyChar)) e.Handled = true;
                };

                _txt.Leave += (_, __) =>
                {
                    if (decimal.TryParse(_txt.Text, out var v)) Value = v;
                    else ClampAndSync();
                };

                _txt.KeyDown += (_, e) =>
                {
                    if (e.KeyCode == Keys.Enter)
                    {
                        if (decimal.TryParse(_txt.Text, out var v)) Value = v;
                        else ClampAndSync();
                        e.SuppressKeyPress = true;
                    }
                };

                _btnHost.BackColor = BackColor;

                StyleArrowButton(_btnUp, "▲");
                StyleArrowButton(_btnDown, "▼");

                _btnUp.Click += (_, __) => Value = _value + 1;
                _btnDown.Click += (_, __) => Value = _value - 1;

                _btnHost.Controls.Add(_btnUp);
                _btnHost.Controls.Add(_btnDown);

                Controls.Add(_txt);
                Controls.Add(_btnHost);

                Resize += (_, __) => LayoutInner();
                LayoutInner();

                ClampAndSync();
            }

            private void LayoutInner()
            {
                int btnW = 36;
                _btnHost.Width = btnW;
                _btnHost.Height = Height - Padding.Vertical;
                _btnHost.Location = new Point(Width - Padding.Right - btnW, Padding.Top);

                int half = _btnHost.Height / 2;
                _btnUp.SetBounds(0, 0, btnW, half);
                _btnDown.SetBounds(0, half, btnW, _btnHost.Height - half);

                _txt.Location = new Point(Padding.Left, Padding.Top - 1);
                _txt.Width = _btnHost.Left - Padding.Left - 6;
                _txt.Height = Height - Padding.Vertical;
            }

            private void StyleArrowButton(Button b, string text)
            {
                b.Text = text;
                b.Dock = DockStyle.None;
                b.FlatStyle = FlatStyle.Flat;
                b.FlatAppearance.BorderSize = 0;
                b.BackColor = BackColor;
                b.ForeColor = Color.FromArgb(120, 120, 120);
                b.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                b.Cursor = Cursors.Hand;

                b.MouseEnter += (_, __) => { b.BackColor = Color.FromArgb(238, 240, 246); };
                b.MouseLeave += (_, __) => { b.BackColor = BackColor; };
            }

            private void ClampAndSync()
            {
                if (_max < _min) _max = _min;

                if (_value < _min) _value = _min;
                if (_value > _max) _value = _max;

                _txt.Text = ((int)_value).ToString();
                _btnUp.Enabled = _value < _max;
                _btnDown.Enabled = _value > _min;
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                var rect = new Rectangle(0, 0, Width - 1, Height - 1);
                using var path = Round(rect, 12);
                using var fill = new SolidBrush(BackColor);
                using var pen = new Pen(Color.FromArgb(230, 232, 238), 1);

                g.FillPath(fill, path);
                g.DrawPath(pen, path);
            }

            private static GraphicsPath Round(Rectangle r, int radius)
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

        private sealed class PillComboBox : UserControl
        {
            private readonly ComboBox _cb = new();

            public ComboBox.ObjectCollection Items => _cb.Items;
            public int SelectedIndex { get => _cb.SelectedIndex; set => _cb.SelectedIndex = value; }

            public PillComboBox()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.ResizeRedraw |
                         ControlStyles.UserPaint, true);

                BackColor = Color.FromArgb(245, 246, 250);
                Padding = new Padding(12, 9, 34, 9);

                _cb.DropDownStyle = ComboBoxStyle.DropDownList;
                _cb.FlatStyle = FlatStyle.Flat;
                _cb.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
                _cb.BackColor = BackColor;
                _cb.ForeColor = Color.FromArgb(40, 40, 40);

                Controls.Add(_cb);

                Resize += (_, __) => LayoutInner();
                LayoutInner();
            }

            private void LayoutInner()
            {
                _cb.Location = new Point(Padding.Left, Padding.Top - 2);
                _cb.Width = Width - Padding.Horizontal;
                _cb.Height = Height - Padding.Vertical;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                var rect = new Rectangle(0, 0, Width - 1, Height - 1);
                using var path = Round(rect, 12);
                using var fill = new SolidBrush(BackColor);
                using var pen = new Pen(Color.FromArgb(230, 232, 238), 1);

                g.FillPath(fill, path);
                g.DrawPath(pen, path);

                var cx = rect.Right - 18;
                var cy = rect.Top + rect.Height / 2;
                using var arrowPen = new Pen(Color.FromArgb(130, 130, 130), 2);
                g.DrawLines(arrowPen, new[]
                {
                    new Point(cx - 6, cy - 2),
                    new Point(cx, cy + 3),
                    new Point(cx + 6, cy - 2)
                });
            }

            private static GraphicsPath Round(Rectangle r, int radius)
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

        private sealed class ToggleSwitchLike : Control
        {
            public bool Value { get; set; }

            public ToggleSwitchLike()
            {
                Width = 56;
                Height = 30;
                Cursor = Cursors.Hand;
                DoubleBuffered = true;

                Click += (_, __) =>
                {
                    Value = !Value;
                    Invalidate();
                };
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                var rTrack = new Rectangle(0, Height / 2 - 10, Width - 1, 20);
                var rThumb = new Rectangle(Value ? Width - 28 : 2, Height / 2 - 13, 26, 26);

                var trackColor = Value ? Color.FromArgb(62, 92, 255) : Color.FromArgb(210, 210, 210);
                using var brushTrack = new SolidBrush(trackColor);
                using var penTrack = new Pen(trackColor, 1.2f);
                using var pathTrack = new GraphicsPath();
                pathTrack.AddArc(rTrack.X, rTrack.Y, 20, 20, 90, 180);
                pathTrack.AddArc(rTrack.Right - 20, rTrack.Y, 20, 20, 270, 180);
                pathTrack.CloseFigure();
                g.FillPath(brushTrack, pathTrack);
                g.DrawPath(penTrack, pathTrack);

                using var brushThumb = new SolidBrush(Color.White);
                using var penThumb = new Pen(Color.FromArgb(210, 210, 210), 1);
                g.FillEllipse(brushThumb, rThumb);
                g.DrawEllipse(penThumb, rThumb);
            }
        }

        // ================= Font helper =================
        private static string? PickInstalledFont(string[] familyNames)
        {
            using var test = new InstalledFontCollection();
            foreach (var f in familyNames)
            {
                if (test.Families.Any(ff => string.Equals(ff.Name, f, StringComparison.OrdinalIgnoreCase)))
                    return f;
            }
            return null;
        }
    }
}
