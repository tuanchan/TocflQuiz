#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using TocflQuiz.Models;

namespace TocflQuiz.Controls.Features.Quiz
{
    public sealed partial class QuizEssayControl : UserControl
    {
        // ========= Public events =========
        public event Action? ExitRequested;
        public event Action<int, int>? ProgressChanged;

        // ========= State =========
        private CardSet? _set;
        private AnswerMode _mode;
        private string? _dayTitle;
        private readonly Random _rng = new Random();

        private readonly List<QuizQuestion> _questions = new();
        private readonly List<QuizAnswerState> _states = new();
        private int _currentIndex;
        private int _correctCount;
        private DateTime _startedAt;
        private bool _submitted;

        // ========= UI (top header) =========
        private readonly Label _lblTopProgress = new();
        private readonly Label _lblTopDay = new();

        // ========= UI (main card) =========
        private readonly RoundedPanel _card = new();
        private readonly Label _lblSmall = new();
        private readonly Label _lblQNum = new();
        private readonly Label _lblPrompt = new();
        private readonly Label _lblAnswerHeader = new();
        private readonly FlowLayoutPanel _chips = new BufferedFlowLayoutPanel();

        // ✅ cache chips để đổi câu không lag + không rebuild font/handlers
        private readonly Dictionary<bool, List<ChipButton>> _chipCache = new(); // key: answerIsChinese
        private bool? _activeChipLang; // đang hiển thị chips của lang nào

        private readonly List<string> _tokensZh = new();
        private readonly List<string> _tokensVi = new();

        private readonly RoundedInput _input = new();
        private readonly RoundedButton _btnSkip = new();
        private readonly RoundedButton _btnNext = new();

        // ========= Overlays =========
        private readonly OverlayPanel _resultOverlay = new();
        private readonly RoundedPanel _resultDlg = new();
        private readonly Label _resSetTitle = new();
        private readonly Label _resTitle = new();
        private readonly Label _resIcon = new();
        private readonly Label _resCorrect = new();
        private readonly Label _resWrong = new();
        private readonly Label _resTime = new();
        private readonly ProgressCircle _resCircle = new();
        private readonly Button _resClose = new();
        private readonly RoundedButton _btnViewResult = new();
        private readonly RoundedButton _btnExit = new();

        private readonly OverlayPanel _reviewOverlay = new();
        private readonly RoundedPanel _reviewDlg = new();
        private readonly Button _revClose = new();
        private readonly Label _revSmall = new();
        private readonly Label _revQNum = new();
        private readonly Label _revPrompt = new();

        private readonly Label _revTryLater = new();
        private readonly RoundedPanel _revTryLaterBox = new();
        private readonly Label _revTryLaterIcon = new();
        private readonly Label _revTryLaterText = new();

        private readonly Label _revYourHeader = new();
        private readonly RoundedPanel _revYourBox = new();
        private readonly Label _revYourIcon = new();
        private readonly Label _revYourText = new();

        private readonly Label _revCorrectHeader = new();
        private readonly RoundedPanel _revCorrectBox = new();
        private readonly Panel _revCorrectDivider = new(); // ✅ divider xanh lá ở giữa
        private readonly Label _revCorrectIcon = new();
        private readonly Label _revCorrectText = new();

        private readonly RoundedButton _revPrev = new();
        private readonly RoundedButton _revNext = new();

        private int _reviewIndex;

        internal sealed class BufferedFlowLayoutPanel : FlowLayoutPanel
        {
            public BufferedFlowLayoutPanel()
            {
                DoubleBuffered = true;
                ResizeRedraw = true;
            }
        }

        private void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // ========= Fonts (Chinese-friendly) =========
        private static readonly string[] TcFontFamilies =
        {
            "DFKai-SB", "BiauKai", "KaiTi", "STKaiti",
            "Microsoft JhengHei UI", "Microsoft JhengHei",
            "PMingLiU", "MingLiU", "Microsoft YaHei UI", "Microsoft YaHei"
        };
        private static readonly string TcPrimaryFontName = PickInstalledFont(TcFontFamilies) ?? "Microsoft JhengHei";

        // ========= Strings =========
        private const string PH_ZH = "Nhập Tiếng Trung (Phồn thể)";
        private const string PH_VI = "Nhập Tiếng Việt";
        private const string BTN_NEXT = "Tiếp";
        private const string BTN_SUBMIT = "Gửi bài kiểm tra";

        public QuizEssayControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(245, 247, 250);
            Padding = new Padding(0);
            DoubleBuffered = true;

            BuildTopHeader();
            BuildMainCard();
            BuildResultOverlay();
            BuildReviewOverlay();

            Controls.Add(_reviewOverlay);
            Controls.Add(_resultOverlay);

            _resultOverlay.Visible = false;
            _reviewOverlay.Visible = false;

            Resize += (_, __) => LayoutNow();
            Load += (_, __) => LayoutNow();

            ShowEmptyState();
        }

        // ===================== Public API =====================
        public void BindSelectedSet(CardSet? set, AnswerMode mode, int count, string? dayTitle = null)
        {
            _set = set;
            _mode = mode;
            _dayTitle = dayTitle;
            _submitted = false;

            _questions.Clear();
            _states.Clear();
            _currentIndex = 0;
            _correctCount = 0;
            _startedAt = DateTime.Now;

            _resultOverlay.Visible = false;
            _reviewOverlay.Visible = false;

            if (set?.Items == null || set.Items.Count == 0)
            {
                ShowEmptyState("(Chưa có từ vựng trong học phần)");
                return;
            }

            var max = Math.Max(0, Math.Min(count, set.Items.Count));
            if (max == 0)
            {
                ShowEmptyState("(Số câu hỏi = 0)");
                return;
            }

            var config = new QuizConfig
            {
                Count = max,
                AnswerMode = mode,
                EnableMultipleChoice = false
            };

            _questions.Clear();
            var questions = QuizEngine.BuildQuestions(set, config);

            foreach (var q in questions)
            {
                _questions.Add(new QuizQuestion
                {
                    SmallLabel = q.SmallLabel,
                    QuestionText = q.QuestionText,
                    CorrectAnswer = q.CorrectAnswer,
                    Index = q.Index,
                    Total = q.Total,
                    UseChineseFontForQuestion = q.UseChineseFontForQuestion,
                    UseChineseFontForChoices = q.UseChineseFontForChoices
                });
            }

            for (int i = 0; i < _questions.Count; i++)
                _states.Add(new QuizAnswerState());

            _lblTopDay.Text = _dayTitle ?? (set.Title ?? "");

            // ✅ build token lists từ TOÀN BỘ học phần (set.Items) => không thiếu
            RebuildTokenPoolsFromSet(set);

            // reset chip cache để rebuild đúng tokens mới
            _chipCache.Clear();
            _activeChipLang = null;

            RenderQuestion(0);
        }
        private void RebuildTokenPoolsFromSet(CardSet set)
        {
            _tokensZh.Clear();
            _tokensVi.Clear();

            var zh = new HashSet<string>(StringComparer.Ordinal);
            var vi = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);

            if (set.Items != null)
            {
                foreach (var it in set.Items)
                {
                    // Chinese chars from Term
                    var term = (it?.Term ?? "").Trim();
                    if (!string.IsNullOrWhiteSpace(term))
                    {
                        foreach (var ch in term)
                        {
                            if (!char.IsWhiteSpace(ch))
                                zh.Add(ch.ToString());
                        }
                    }

                    // Vietnamese words from Definition (bỏ (...) cuối)
                    var def = StripDefinitionForAnswer(it?.Definition, it?.Pinyin);
                    if (!string.IsNullOrWhiteSpace(def))
                    {
                        foreach (var w in def.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            var word = w.Trim();
                            if (word.Length > 0) vi.Add(word);
                        }
                    }
                }
            }

            _tokensZh.AddRange(zh);
            _tokensVi.AddRange(vi);

            Shuffle(_tokensZh);
            Shuffle(_tokensVi);
        }


        // ===================== Build UI =====================
        private void BuildTopHeader()
        {
            _lblTopProgress.AutoSize = false;
            _lblTopProgress.Height = 46;
            _lblTopProgress.Font = new Font("Segoe UI", 24F, FontStyle.Bold);
            _lblTopProgress.ForeColor = Color.FromArgb(30, 30, 30);
            _lblTopProgress.TextAlign = ContentAlignment.MiddleCenter;
            _lblTopProgress.Text = "0 / 0";

            _lblTopDay.AutoSize = false;
            _lblTopDay.Height = 28;
            _lblTopDay.Font = new Font("Segoe UI", 13F, FontStyle.Regular);
            _lblTopDay.ForeColor = Color.FromArgb(120, 120, 120);
            _lblTopDay.TextAlign = ContentAlignment.MiddleCenter;
            _lblTopDay.Text = "";

            Controls.Add(_lblTopDay);
            Controls.Add(_lblTopProgress);
        }


        private void BuildMainCard()
        {
            _card.Radius = 20;
            _card.BackColor = Color.White;
            _card.Padding = new Padding(28, 22, 28, 22);
            _card.Shadow = true;

            _lblSmall.AutoSize = true;
            _lblSmall.Font = new Font("Segoe UI", 11F, FontStyle.Regular);
            _lblSmall.ForeColor = Color.FromArgb(120, 120, 120);
            _lblSmall.Text = "Định nghĩa";

            _lblQNum.AutoSize = true;
            _lblQNum.Font = new Font("Segoe UI", 11F, FontStyle.Regular);
            _lblQNum.ForeColor = Color.FromArgb(120, 120, 120);
            _lblQNum.Text = "1/1";

            _lblPrompt.AutoSize = false;
            _lblPrompt.Font = new Font("Segoe UI", 24F, FontStyle.Regular);
            _lblPrompt.ForeColor = Color.FromArgb(35, 35, 35);
            _lblPrompt.TextAlign = ContentAlignment.MiddleCenter;

            _lblAnswerHeader.AutoSize = true;
            _lblAnswerHeader.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
            _lblAnswerHeader.ForeColor = Color.FromArgb(60, 60, 60);
            _lblAnswerHeader.Text = "Đáp án của bạn";

            _chips.AutoScroll = true;
            _chips.WrapContents = true;
            _chips.Padding = new Padding(4);
            _chips.Margin = new Padding(0);
            _chips.BackColor = Color.Transparent;
            _chips.Enabled = true;

            _input.Placeholder = PH_ZH;

            _btnSkip.Text = "Bỏ qua";
            _btnSkip.Width = 140;
            _btnSkip.Height = 56;
            _btnSkip.Radius = 16;
            _btnSkip.BorderThickness = 1;
            _btnSkip.BorderColor = Color.FromArgb(230, 232, 238);
            _btnSkip.BackColor = Color.FromArgb(245, 246, 250);
            _btnSkip.ForeColor = Color.FromArgb(50, 50, 50);
            _btnSkip.Font = new Font("Segoe UI", 12F, FontStyle.Bold);

            _btnNext.Text = BTN_NEXT;
            _btnNext.Width = 170;
            _btnNext.Height = 56;
            _btnNext.Radius = 16;
            _btnNext.BorderThickness = 0;
            _btnNext.BackColor = Color.FromArgb(62, 92, 255);
            _btnNext.ForeColor = Color.White;
            _btnNext.Font = new Font("Segoe UI", 12F, FontStyle.Bold);

            _btnSkip.Click += (_, __) => SkipCurrent();
            _btnNext.Click += (_, __) => SubmitCurrent();

            _input.InnerTextBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    SubmitCurrent();
                }
            };

            _card.Controls.Add(_lblSmall);
            _card.Controls.Add(_lblQNum);
            _card.Controls.Add(_lblPrompt);
            _card.Controls.Add(_lblAnswerHeader);
            _card.Controls.Add(_chips);
            _card.Controls.Add(_input);
            _card.Controls.Add(_btnSkip);
            _card.Controls.Add(_btnNext);

            Controls.Add(_card);
        }


        private void BuildResultOverlay()
        {
            _resultOverlay.Dock = DockStyle.Fill;
            _resultOverlay.Visible = false;

            _resultDlg.Radius = 18;
            _resultDlg.BackColor = Color.White;
            _resultDlg.Shadow = true;
            _resultDlg.Size = new Size(520, 420);
            _resultDlg.Padding = new Padding(18, 16, 18, 14);

            _resClose.FlatStyle = FlatStyle.Flat;
            _resClose.FlatAppearance.BorderSize = 0;
            _resClose.Text = "✕";
            _resClose.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            _resClose.ForeColor = Color.FromArgb(120, 120, 120);
            _resClose.BackColor = Color.Transparent;
            _resClose.Size = new Size(36, 32);
            _resClose.Click += (_, __) =>
            {
                _resultOverlay.Visible = false;
                _reviewOverlay.Visible = false;
                ExitRequested?.Invoke();
            };


            _resSetTitle.AutoSize = false;
            _resSetTitle.Height = 22;
            _resSetTitle.TextAlign = ContentAlignment.MiddleCenter;
            _resSetTitle.Font = new Font("Segoe UI", 10.5F, FontStyle.Regular);
            _resSetTitle.ForeColor = Color.FromArgb(110, 110, 110);

            _resIcon.AutoSize = false;
            _resIcon.Size = new Size(52, 52);
            _resIcon.TextAlign = ContentAlignment.MiddleCenter;
            _resIcon.Font = new Font("Segoe UI Emoji", 26F, FontStyle.Regular);
            _resIcon.Text = "🎉";

            _resTitle.AutoSize = false;
            _resTitle.Height = 30;
            _resTitle.TextAlign = ContentAlignment.MiddleCenter;
            _resTitle.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            _resTitle.ForeColor = Color.FromArgb(30, 30, 30);
            _resTitle.Text = "Kết quả";

            _resCorrect.AutoSize = false;
            _resCorrect.Height = 22;
            _resCorrect.TextAlign = ContentAlignment.MiddleCenter;
            _resCorrect.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            _resCorrect.ForeColor = Color.FromArgb(40, 160, 90);

            _resWrong.AutoSize = false;
            _resWrong.Height = 22;
            _resWrong.TextAlign = ContentAlignment.MiddleCenter;
            _resWrong.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            _resWrong.ForeColor = Color.FromArgb(210, 60, 60);

            _resTime.AutoSize = false;
            _resTime.Height = 20;
            _resTime.TextAlign = ContentAlignment.MiddleCenter;
            _resTime.Font = new Font("Segoe UI", 10F, FontStyle.Regular);
            _resTime.ForeColor = Color.FromArgb(120, 120, 120);

            _resCircle.Size = new Size(120, 120);

            _btnViewResult.Text = "Xem kết quả";
            _btnViewResult.Width = 150;
            _btnViewResult.Height = 44;
            _btnViewResult.Radius = 14;
            _btnViewResult.BorderThickness = 1;
            _btnViewResult.BorderColor = Color.FromArgb(230, 232, 238);
            _btnViewResult.BackColor = Color.FromArgb(245, 246, 250);
            _btnViewResult.ForeColor = Color.FromArgb(50, 50, 50);
            _btnViewResult.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
            _btnViewResult.Click += (_, __) =>
            {
                _resultOverlay.Visible = false;
                ShowReview(0);
            };

            _btnExit.Text = "Thoát";
            _btnExit.Width = 110;
            _btnExit.Height = 44;
            _btnExit.Radius = 14;
            _btnExit.BorderThickness = 0;
            _btnExit.BackColor = Color.FromArgb(62, 92, 255);
            _btnExit.ForeColor = Color.White;
            _btnExit.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
            _btnExit.Click += (_, __) => ExitRequested?.Invoke();

            _resultDlg.Controls.Add(_resClose);
            _resultDlg.Controls.Add(_resSetTitle);
            _resultDlg.Controls.Add(_resIcon);
            _resultDlg.Controls.Add(_resTitle);
            _resultDlg.Controls.Add(_resCircle);
            _resultDlg.Controls.Add(_resCorrect);
            _resultDlg.Controls.Add(_resWrong);
            _resultDlg.Controls.Add(_resTime);
            _resultDlg.Controls.Add(_btnViewResult);
            _resultDlg.Controls.Add(_btnExit);

            _resultDlg.Layout += (_, __) => LayoutResultDlg();

            _resultOverlay.Controls.Add(_resultDlg);
            _resultOverlay.Layout += (_, __) =>
            {
                _resultDlg.Left = (_resultOverlay.ClientSize.Width - _resultDlg.Width) / 2;
                _resultDlg.Top = (_resultOverlay.ClientSize.Height - _resultDlg.Height) / 2;
            };
        }

        private void BuildReviewOverlay()
        {
            _reviewOverlay.Dock = DockStyle.Fill;
            _reviewOverlay.Visible = false;

            _reviewDlg.Radius = 18;
            _reviewDlg.BackColor = Color.White;
            _reviewDlg.Shadow = true;
            _reviewDlg.Size = new Size(760, 520);
            _reviewDlg.Padding = new Padding(20, 16, 20, 14);

            _revClose.FlatStyle = FlatStyle.Flat;
            _revClose.FlatAppearance.BorderSize = 0;
            _revClose.Text = "✕";
            _revClose.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            _revClose.ForeColor = Color.FromArgb(120, 120, 120);
            _revClose.BackColor = Color.Transparent;
            _revClose.Size = new Size(36, 32);
            _revClose.Click += (_, __) =>
            {
                _reviewOverlay.Visible = false;
                _resultOverlay.Visible = false; // phòng trường hợp đang mở chồng
                ExitRequested?.Invoke();         // ✅ về danh sách học phần
            };


            _revSmall.AutoSize = true;
            _revSmall.Font = new Font("Segoe UI", 10F, FontStyle.Regular);
            _revSmall.ForeColor = Color.FromArgb(120, 120, 120);
            _revSmall.Text = "Definition";

            _revQNum.AutoSize = true;
            _revQNum.Font = new Font("Segoe UI", 10F, FontStyle.Regular);
            _revQNum.ForeColor = Color.FromArgb(120, 120, 120);
            _revQNum.Text = "1 of 1";

            _revPrompt.AutoSize = false;
            _revPrompt.Font = new Font("Segoe UI", 18F, FontStyle.Regular);
            _revPrompt.ForeColor = Color.FromArgb(35, 35, 35);
            _revPrompt.TextAlign = ContentAlignment.TopLeft;

            _revTryLater.AutoSize = true;
            _revTryLater.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            _revTryLater.ForeColor = Color.FromArgb(90, 90, 90);
            _revTryLater.Text = "Give this one a try later!";

            _revTryLaterBox.Radius = 14;
            _revTryLaterBox.BackColor = Color.FromArgb(245, 246, 250);
            _revTryLaterBox.Shadow = false;
            _revTryLaterBox.BorderThickness = 0;

            _revTryLaterIcon.AutoSize = false;
            _revTryLaterIcon.Size = new Size(28, 28);
            _revTryLaterIcon.TextAlign = ContentAlignment.MiddleCenter;
            _revTryLaterIcon.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            _revTryLaterIcon.ForeColor = Color.FromArgb(150, 150, 150);
            _revTryLaterIcon.Text = "✕";

            _revTryLaterText.AutoSize = false;
            _revTryLaterText.Font = new Font("Segoe UI", 11F, FontStyle.Regular);
            _revTryLaterText.ForeColor = Color.FromArgb(70, 70, 70);

            _revTryLaterBox.Controls.Add(_revTryLaterIcon);
            _revTryLaterBox.Controls.Add(_revTryLaterText);

            _revYourHeader.AutoSize = true;
            _revYourHeader.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            _revYourHeader.ForeColor = Color.FromArgb(90, 90, 90);
            _revYourHeader.Text = "Your answer";

            _revYourBox.Radius = 14;
            _revYourBox.BackColor = Color.FromArgb(245, 246, 250);
            _revYourBox.Shadow = false;
            _revYourBox.BorderThickness = 0;

            _revYourIcon.AutoSize = false;
            _revYourIcon.Size = new Size(28, 28);
            _revYourIcon.TextAlign = ContentAlignment.MiddleCenter;
            _revYourIcon.Font = new Font("Segoe UI", 14F, FontStyle.Bold);

            _revYourText.AutoSize = false;
            _revYourText.Font = new Font("Segoe UI", 11F, FontStyle.Regular);
            _revYourText.ForeColor = Color.FromArgb(70, 70, 70);

            _revYourBox.Controls.Add(_revYourIcon);
            _revYourBox.Controls.Add(_revYourText);

            _revCorrectHeader.AutoSize = true;
            _revCorrectHeader.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            _revCorrectHeader.ForeColor = Color.FromArgb(60, 140, 90);
            _revCorrectHeader.Text = "Correct answer";

            _revCorrectBox.Radius = 14;
            _revCorrectBox.BackColor = Color.FromArgb(236, 253, 245);
            _revCorrectBox.Shadow = false;
      

           

            _revCorrectIcon.AutoSize = false;
            _revCorrectIcon.Size = new Size(28, 28);
            _revCorrectIcon.TextAlign = ContentAlignment.MiddleCenter;
            _revCorrectIcon.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            _revCorrectIcon.ForeColor = Color.FromArgb(40, 160, 90);
            _revCorrectIcon.Text = "✓";

            _revCorrectText.AutoSize = false;
            _revCorrectText.Font = new Font("Segoe UI", 11F, FontStyle.Regular);
            _revCorrectText.ForeColor = Color.FromArgb(50, 50, 50);

            _revCorrectBox.Controls.Add(_revCorrectIcon);
            _revCorrectBox.Controls.Add(_revCorrectDivider);
            _revCorrectBox.Controls.Add(_revCorrectText);

            _revPrev.Text = "‹";
            _revPrev.Width = 52;
            _revPrev.Height = 42;
            _revPrev.Radius = 14;
            _revPrev.BorderThickness = 1;
            _revPrev.BorderColor = Color.FromArgb(230, 232, 238);
            _revPrev.BackColor = Color.FromArgb(245, 246, 250);
            _revPrev.ForeColor = Color.FromArgb(60, 60, 60);
            _revPrev.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
            _revPrev.Click += (_, __) => ShowReview(_reviewIndex - 1);

            _revNext.Text = "›";
            _revNext.Width = 52;
            _revNext.Height = 42;
            _revNext.Radius = 14;
            _revNext.BorderThickness = 1;
            _revNext.BorderColor = Color.FromArgb(230, 232, 238);
            _revNext.BackColor = Color.FromArgb(245, 246, 250);
            _revNext.ForeColor = Color.FromArgb(60, 60, 60);
            _revNext.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
            _revNext.Click += (_, __) => ShowReview(_reviewIndex + 1);

            _reviewDlg.Controls.Add(_revClose);
            _reviewDlg.Controls.Add(_revSmall);
            _reviewDlg.Controls.Add(_revQNum);
            _reviewDlg.Controls.Add(_revPrompt);

            _reviewDlg.Controls.Add(_revTryLater);
            _reviewDlg.Controls.Add(_revTryLaterBox);

            _reviewDlg.Controls.Add(_revYourHeader);
            _reviewDlg.Controls.Add(_revYourBox);

            _reviewDlg.Controls.Add(_revCorrectHeader);
            _reviewDlg.Controls.Add(_revCorrectBox);

            _reviewDlg.Controls.Add(_revPrev);
            _reviewDlg.Controls.Add(_revNext);

            _reviewDlg.Layout += (_, __) => LayoutReviewDlg();

            _reviewOverlay.Controls.Add(_reviewDlg);
            _reviewOverlay.Layout += (_, __) =>
            {
                _reviewDlg.Left = (_reviewOverlay.ClientSize.Width - _reviewDlg.Width) / 2;
                _reviewDlg.Top = (_reviewOverlay.ClientSize.Height - _reviewDlg.Height) / 2;
            };
        }

        // ===================== Layout =====================
        private void LayoutNow()
        {
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;

            // Header
            _lblTopProgress.SetBounds(0, 18, ClientSize.Width, 52);
            _lblTopDay.SetBounds(0, 70, ClientSize.Width, 30);

            int top = 118;

            // Card width
            int side = 22;
            int w = ClientSize.Width - side * 2;
            w = Math.Min(1320, w);
            w = Math.Max(980, w);

            int left = (ClientSize.Width - w) / 2;

            _card.Left = left;
            _card.Top = top;
            _card.Width = w;

            // ✅ FIX: Card height = phần còn lại của form (không auto theo content nữa)
            int bottomMargin = 18;
            int h = ClientSize.Height - top - bottomMargin;
            _card.Height = Math.Max(560, h); // đảm bảo không quá nhỏ

            LayoutCardInner();
        }





        private void LayoutCardInner()
        {
            int padL = _card.Padding.Left;
            int padT = _card.Padding.Top;
            int padB = _card.Padding.Bottom;

            int innerW = _card.ClientSize.Width - _card.Padding.Left - _card.Padding.Right;

            // top row
            _lblSmall.Location = new Point(padL, padT);
            _lblQNum.Location = new Point(_card.ClientSize.Width - _card.Padding.Right - _lblQNum.Width, padT);

            int y = padT + 36;

            // prompt
            int promptH = 190;
            _lblPrompt.SetBounds(padL, y, innerW, promptH);
            y += promptH + 18;

            // answer header
            _lblAnswerHeader.Location = new Point(padL, y);
            y += 36;

            // ✅ bottom row (input + buttons) GHIM Ở ĐÁY CARD
            int rowH = 64;
            int gap = 14;

            int rowY = _card.ClientSize.Height - padB - rowH;

            int btnNextW = _btnNext.Width;
            int btnSkipW = _btnSkip.Width;

            int inputW = innerW - btnNextW - btnSkipW - gap * 2;
            if (inputW < 320) inputW = Math.Max(320, innerW - btnNextW - gap);

            _input.SetBounds(padL, rowY, inputW, rowH);
            _btnSkip.Location = new Point(padL + inputW + gap, rowY);
            _btnNext.Location = new Point(padL + inputW + gap + btnSkipW + gap, rowY);

            // ✅ chips chiếm phần còn lại -> nhiều thì scroll trong chips
            int chipsTop = y;
            int chipsBottom = rowY - 18;               // chừa khoảng cách với hàng input
            int chipsH = Math.Max(160, chipsBottom - chipsTop);

            _chips.SetBounds(padL, chipsTop, innerW, chipsH);

            // ❌ bỏ dòng này vì nó làm card auto cao theo content
            // _card.Height = ...
        }




        private void LayoutResultDlg()
        {
            _resClose.Location = new Point(_resultDlg.ClientSize.Width - _resClose.Width - 6, 6);

            int y = 18;
            _resSetTitle.SetBounds(12, y, _resultDlg.ClientSize.Width - 24, 22);
            y += 26;

            _resIcon.SetBounds((_resultDlg.ClientSize.Width - 52) / 2, y, 52, 52);
            y += 54;

            _resTitle.SetBounds(12, y, _resultDlg.ClientSize.Width - 24, 30);
            y += 38;

            _resCircle.Location = new Point((_resultDlg.ClientSize.Width - _resCircle.Width) / 2, y);
            y += _resCircle.Height + 12;

            _resCorrect.SetBounds(12, y, _resultDlg.ClientSize.Width - 24, 22);
            y += 22;
            _resWrong.SetBounds(12, y, _resultDlg.ClientSize.Width - 24, 22);
            y += 26;

            _resTime.SetBounds(12, y, _resultDlg.ClientSize.Width - 24, 20);
            y += 34;

            int gap = 10;
            int totalW = _btnViewResult.Width + gap + _btnExit.Width;
            int left = (_resultDlg.ClientSize.Width - totalW) / 2;

            y += 10;

            _btnViewResult.Location = new Point(left, y);
            _btnExit.Location = new Point(left + _btnViewResult.Width + gap, y);
        }

        private void LayoutReviewDlg()
        {
            _revClose.Location = new Point(_reviewDlg.ClientSize.Width - _revClose.Width - 6, 6);

            int padL = _reviewDlg.Padding.Left;
            int innerW = _reviewDlg.ClientSize.Width - _reviewDlg.Padding.Left - _reviewDlg.Padding.Right;

            int y = 18;

            _revSmall.Location = new Point(padL, y);
            _revQNum.Location = new Point(
                Math.Max(padL, _revClose.Left - 10 - _revQNum.Width),
                y
            );

            y += 28;

            _revPrompt.SetBounds(padL, y, innerW, 90);
            y += 98;

            if (_revTryLater.Visible)
            {
                _revTryLater.Location = new Point(padL, y);
                y += 28;

                _revTryLaterBox.SetBounds(padL, y, innerW, 56);
                LayoutIconTextRow(_revTryLaterBox, _revTryLaterIcon, _revTryLaterText, divider: null);
                y += 68;
            }

            if (_revYourHeader.Visible)
            {
                _revYourHeader.Location = new Point(padL, y);
                y += 28;

                _revYourBox.SetBounds(padL, y, innerW, 56);
                LayoutIconTextRow(_revYourBox, _revYourIcon, _revYourText, divider: null);
                y += 68;
            }

            _revCorrectHeader.Location = new Point(padL, y);
            y += 28;

            _revCorrectBox.SetBounds(padL, y, innerW, 56);
            LayoutIconTextRow(_revCorrectBox, _revCorrectIcon, _revCorrectText, _revCorrectDivider); // ✅ divider
            y += 78;

            _revPrev.Location = new Point(padL, _reviewDlg.ClientSize.Height - _reviewDlg.Padding.Bottom - _revPrev.Height);
            _revNext.Location = new Point(_reviewDlg.ClientSize.Width - _reviewDlg.Padding.Right - _revNext.Width,
                                          _reviewDlg.ClientSize.Height - _reviewDlg.Padding.Bottom - _revNext.Height);
        }

        private static void LayoutIconTextRow(Control host, Label icon, Label text, Control? divider)
        {
            int pad = 14;

            icon.Location = new Point(pad, (host.Height - icon.Height) / 2);

            if (divider != null)
            {
                int divH = Math.Max(10, host.Height - 22);
                divider.Location = new Point(pad + icon.Width + 10, (host.Height - divH) / 2);
                divider.Height = divH;

                int textLeft = divider.Right + 12;
                text.Location = new Point(textLeft, 0);
                text.Size = new Size(host.Width - textLeft - pad, host.Height);
            }
            else
            {
                int textLeft = pad + icon.Width + 10;
                text.Location = new Point(textLeft, 0);
                text.Size = new Size(host.Width - textLeft - pad, host.Height);
            }

            text.TextAlign = ContentAlignment.MiddleLeft;
        }

        // ===================== Quiz Flow =====================
        private void RenderQuestion(int index)
        {
            SuspendLayout();
            _card.SuspendLayout();

            try
            {
                if (_questions.Count == 0)
                {
                    ShowEmptyState();
                    return;
                }

                index = Math.Max(0, Math.Min(index, _questions.Count - 1));
                _currentIndex = index;

                var q = _questions[index];
                bool answerIsChinese = q.UseChineseFontForChoices;

                _lblAnswerHeader.Visible = true;
                _chips.Visible = true;
                _input.Visible = true;
                _btnSkip.Visible = true;
                _btnNext.Visible = true;

                _lblPrompt.TextAlign = ContentAlignment.MiddleCenter;

                _lblTopProgress.Text = $"{index + 1} / {_questions.Count}";
                _lblTopDay.Text = _dayTitle ?? (_set?.Title ?? "");

                _lblSmall.Text = q.SmallLabel;
                _lblQNum.Text = $"{q.Index}/{q.Total}";

                _lblPrompt.Font = q.UseChineseFontForQuestion
                    ? new Font(TcPrimaryFontName, 22F, FontStyle.Regular)
                    : new Font("Segoe UI", 20F, FontStyle.Regular);

                _lblPrompt.Text = q.QuestionText;

                _input.Placeholder = answerIsChinese ? PH_ZH : PH_VI;
                _input.InnerTextBox.Font = answerIsChinese
                    ? new Font(TcPrimaryFontName, 14F, FontStyle.Regular)
                    : new Font("Segoe UI", 12F, FontStyle.Regular);

                // ✅ quan trọng: clear input cho câu mới (không nhét placeholder khi đang focus)
                _input.SetText("");

                BuildChips(answerIsChinese);

                _btnNext.Text = (index == _questions.Count - 1) ? BTN_SUBMIT : BTN_NEXT;

                LayoutNow();
                ProgressChanged?.Invoke(index + 1, _questions.Count);

                // ✅ đảm bảo qua câu mới là focus textbox (Enter hay click đều vậy)
                BeginInvoke(new Action(() =>
                {
                    _input.FocusInput();
                    _input.InnerTextBox.SelectionStart = _input.InnerTextBox.TextLength;
                }));
            }
            finally
            {
                _card.ResumeLayout(true);
                ResumeLayout(true);
            }
        }



        private void BuildChips(bool answerIsChinese)
        {
            var tokens = answerIsChinese ? _tokensZh : _tokensVi;

            if (_chipCache.TryGetValue(answerIsChinese, out var cached) && cached.Count != tokens.Count)
            {
                _chipCache.Remove(answerIsChinese);
                cached = null;
                _activeChipLang = null;
            }

            if (_activeChipLang.HasValue && _activeChipLang.Value == answerIsChinese && cached != null)
                return;

            if (!_chipCache.TryGetValue(answerIsChinese, out var list))
            {
                int take = tokens.Count;

                list = new List<ChipButton>(take);

                for (int i = 0; i < take; i++)
                {
                    var t = tokens[i];

                    var chip = new ChipButton
                    {
                        Text = t,
                        AnswerIsChinese = answerIsChinese,
                        Font = answerIsChinese
                            ? new Font(TcPrimaryFontName, 16F, FontStyle.Regular)   // ✅ to + nét
                            : new Font("Segoe UI", 13F, FontStyle.Regular),        // ✅ to + nét
                        Enabled = true
                    };

                    chip.Click += (_, __) =>
                    {
                        if (_submitted) return;

                        var cur = _input.GetText().Trim();

                        if (answerIsChinese) _input.SetText(cur + chip.Text);
                        else
                        {
                            if (cur.Length == 0) _input.SetText(chip.Text);
                            else
                            {
                                if (!cur.EndsWith(" ")) cur += " ";
                                _input.SetText(cur + chip.Text);
                            }
                        }

                        _input.FocusInput();
                    };

                    list.Add(chip);
                }

                _chipCache[answerIsChinese] = list;
            }

            _chips.SuspendLayout();
            _chips.Controls.Clear();
            _chips.Controls.AddRange(list.ToArray());
            _chips.ResumeLayout(true);

            var pref = _chips.GetPreferredSize(new Size(_chips.ClientSize.Width, int.MaxValue));
            _chips.AutoScrollMinSize = new Size(0, pref.Height);

            _activeChipLang = answerIsChinese;
        }




        private void SkipCurrent()
        {
            if (_submitted) return;

            var st = _states[_currentIndex];
            st.Skipped = true;
            st.UserAnswer = null;
            st.IsCorrect = false;

            GoNextOrSubmit();
        }

        private void SubmitCurrent()
        {
            if (_submitted) return;

            var q = _questions[_currentIndex];
            var st = _states[_currentIndex];

            var user = _input.GetText().Trim();
            if (string.IsNullOrWhiteSpace(user))
            {
                st.Skipped = true;
                st.UserAnswer = null;
                st.IsCorrect = false;
            }
            else
            {
                st.Skipped = false;
                st.UserAnswer = user;

                st.IsCorrect = IsAnswerCorrect(q, user);
                if (st.IsCorrect) _correctCount++;
            }

            GoNextOrSubmit();
        }

        private void GoNextOrSubmit()
        {
            if (_currentIndex < _questions.Count - 1)
            {
                RenderQuestion(_currentIndex + 1);
                return;
            }

            _submitted = true;

            var elapsed = DateTime.Now - _startedAt;
            int total = _questions.Count;
            int correct = _states.Count(x => x.IsCorrect);
            int wrong = total - correct;

            _resSetTitle.Text = _set?.Title ?? "";
            _resCorrect.Text = $"Đúng: {correct}";
            _resWrong.Text = $"Sai: {wrong}";
            _resTime.Text = $"Thời gian: {FormatTime(elapsed)}";

            int percent = total == 0 ? 0 : (int)Math.Round(100.0 * correct / total);
            _resCircle.Value = percent;

            _resultOverlay.Visible = true;
            _resultOverlay.BringToFront();
        }

        // ===================== Review =====================
        private void ShowReview(int index)
        {
            if (_questions.Count == 0) return;

            index = Math.Max(0, Math.Min(index, _questions.Count - 1));
            _reviewIndex = index;

            var q = _questions[index];
            var st = _states[index];

            _revSmall.Text = q.SmallLabel;
            _revQNum.Text = $"{q.Index} of {q.Total}";

            _revPrompt.Font = q.UseChineseFontForQuestion
                ? new Font(TcPrimaryFontName, 20F, FontStyle.Regular)
                : new Font("Segoe UI", 18F, FontStyle.Regular);
            _revPrompt.Text = q.QuestionText;

            _revCorrectText.Font = q.UseChineseFontForChoices
                ? new Font(TcPrimaryFontName, 16F, FontStyle.Regular)
                : new Font("Segoe UI", 12F, FontStyle.Regular);
            _revCorrectText.Text = q.CorrectAnswer;

            bool wrongOrSkip = st.Skipped || !st.IsCorrect;

            _revTryLater.Visible = wrongOrSkip;
            _revTryLaterBox.Visible = wrongOrSkip;

            bool showYour = !string.IsNullOrWhiteSpace(st.UserAnswer) || st.Skipped;
            _revYourHeader.Visible = showYour;
            _revYourBox.Visible = showYour;

            if (wrongOrSkip)
            {
                _revTryLaterIcon.ForeColor = Color.FromArgb(160, 160, 160);
                _revTryLaterIcon.Text = "✕";
                _revTryLaterText.Font = q.UseChineseFontForChoices
                    ? new Font(TcPrimaryFontName, 14F, FontStyle.Regular)
                    : new Font("Segoe UI", 11F, FontStyle.Regular);
                _revTryLaterText.Text = st.Skipped ? "Bỏ qua" : (st.UserAnswer ?? "");
            }

            if (showYour)
            {
                bool isCorrect = st.IsCorrect;

                _revYourIcon.ForeColor = isCorrect ? Color.FromArgb(40, 160, 90) : Color.FromArgb(210, 60, 60);
                _revYourIcon.Text = isCorrect ? "✓" : "✕";

                _revYourText.Font = q.UseChineseFontForChoices
                    ? new Font(TcPrimaryFontName, 14F, FontStyle.Regular)
                    : new Font("Segoe UI", 11F, FontStyle.Regular);

                _revYourText.Text = st.Skipped ? "Bỏ qua" : (st.UserAnswer ?? "");
            }

            _revPrev.Enabled = index > 0;
            _revNext.Enabled = index < _questions.Count - 1;

            _reviewOverlay.Visible = true;
            _reviewOverlay.BringToFront();
            LayoutReviewDlg();
        }

        // ===================== Helpers =====================
        private void ShowEmptyState(string? message = null)
        {
            _lblTopProgress.Text = "0 / 0";
            _lblTopDay.Text = _dayTitle ?? "";

            _lblSmall.Text = "";
            _lblQNum.Text = "";
            _lblPrompt.Font = new Font("Segoe UI", 14F, FontStyle.Regular);
            _lblPrompt.Text = message ?? "(Chọn học phần và bấm Bắt đầu)";
            _lblPrompt.TextAlign = ContentAlignment.MiddleCenter;

            _lblAnswerHeader.Visible = false;
            _chips.Visible = false;
            _input.Visible = false;
            _btnSkip.Visible = false;
            _btnNext.Visible = false;

            LayoutNow();
        }

        private static string StripDefinitionForAnswer(string? definition, string? pinyin)
        {
            var def = (definition ?? "").Trim();
            if (string.IsNullOrWhiteSpace(def)) return "";
            def = StripParenTail(def);
            return def;
        }

        private static string StripParenTail(string s)
        {
            int close = s.LastIndexOf(')');
            int open = s.LastIndexOf('(');
            if (open >= 0 && close == s.Length - 1 && open < close)
            {
                var head = s.Substring(0, open).Trim();
                if (head.Length > 0) return head;
            }
            return s.Trim();
        }

        private static bool IsAnswerCorrect(QuizQuestion q, string user)
        {
            bool answerIsChinese = q.UseChineseFontForChoices;

            string userN = Normalize(user, answerIsChinese);
            if (string.IsNullOrWhiteSpace(userN)) return false;

            string correctRaw = (q.CorrectAnswer ?? "").Trim();

            if (!answerIsChinese)
                correctRaw = StripParenTail(correctRaw);

            IEnumerable<string> candidates = SplitCandidates(correctRaw);
            if (!candidates.Any()) candidates = new[] { correctRaw };

            foreach (var cand in candidates)
            {
                string correctN = Normalize(cand, answerIsChinese);

                if (answerIsChinese)
                {
                    if (string.Equals(userN, correctN, StringComparison.Ordinal))
                        return true;
                }
                else
                {
                    var a = RemoveDiacritics(userN).ToLowerInvariant();
                    var b = RemoveDiacritics(correctN).ToLowerInvariant();
                    if (a == b) return true;
                }
            }

            return false;

            static IEnumerable<string> SplitCandidates(string s)
                => (s ?? "")
                    .Split(new[] { '/', ';', '|', ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .Where(x => x.Length > 0);
        }

        private static string Normalize(string s, bool isChinese)
        {
            s = (s ?? "").Trim();
            s = string.Join(" ", s.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
            if (isChinese) s = s.Replace(" ", "");
            return s;
        }

        private static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(normalized.Length);
            foreach (var ch in normalized)
            {
                var uc = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (uc != UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        private static string FormatTime(TimeSpan ts)
        {
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
            return $"{ts.Minutes:D2}:{ts.Seconds:D2}";
        }

        private static string? PickInstalledFont(IEnumerable<string> candidates)
        {
            try
            {
                using var fonts = new InstalledFontCollection();
                var installed = fonts.Families.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var c in candidates)
                    if (installed.Contains(c)) return c;
            }
            catch { }
            return null;
        }

        // ===================== Data types =====================
        private sealed class QuizAnswerState
        {
            public string? UserAnswer;
            public bool Skipped;
            public bool IsCorrect;
        }
    }

    // =====================================================================
    // Shared UI helpers
    // =====================================================================

    internal sealed class OverlayPanel : Panel
    {
        public OverlayPanel()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(140, 0, 0, 0);
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        }
    }

    internal sealed class RoundedPanel : Panel
    {
        public int Radius { get; set; } = 16;
        public bool Shadow { get; set; } = false;

        public int BorderThickness { get; set; } = 0;
        public Color BorderColor { get; set; } = Color.FromArgb(220, 220, 220);

        public RoundedPanel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = ClientRectangle;
            rect.Width -= 1;
            rect.Height -= 1;

            using var path = RoundedRect(rect, Radius);

            if (Shadow)
            {
                var shadowRect = rect;
                shadowRect.Offset(0, 3);
                using var shadowPath = RoundedRect(shadowRect, Radius);
                using var shadowBrush = new SolidBrush(Color.FromArgb(20, 0, 0, 0));
                e.Graphics.FillPath(shadowBrush, shadowPath);
            }

            using var fill = new SolidBrush(BackColor);
            e.Graphics.FillPath(fill, path);

            if (BorderThickness > 0)
            {
                using var pen = new Pen(BorderColor, BorderThickness);
                e.Graphics.DrawPath(pen, path);
            }

            Region = new Region(path);
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

    internal sealed class RoundedButton : Button
    {
        public int Radius { get; set; } = 14;
        public int BorderThickness { get; set; } = 0;
        public Color BorderColor { get; set; } = Color.Transparent;

        public RoundedButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = ClientRectangle;
            rect.Width -= 1;
            rect.Height -= 1;

            using var path = RoundedRect(rect, Radius);

            using (var brush = new SolidBrush(BackColor))
                e.Graphics.FillPath(brush, path);

            if (BorderThickness > 0)
            {
                using var pen = new Pen(BorderColor, BorderThickness);
                e.Graphics.DrawPath(pen, path);
            }

            TextRenderer.DrawText(
                e.Graphics,
                Text,
                Font,
                rect,
                ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
            );

            Region = new Region(path);
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

    internal sealed class ChipButton : Button
    {
        public bool AnswerIsChinese { get; set; }
        private bool _hover;

        public ChipButton()
        {
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

            Padding = new Padding(18, 12, 18, 12);   // ✅ chip to hơn
            Margin = new Padding(8, 8, 8, 8);       // ✅ thoáng hơn

            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;

            BackColor = Color.FromArgb(245, 246, 250);
            ForeColor = Color.FromArgb(50, 50, 50);

            Cursor = Cursors.Hand;

            MouseEnter += (_, __) => { _hover = true; Invalidate(); };
            MouseLeave += (_, __) => { _hover = false; Invalidate(); };

            SetStyle(ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, true);
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            var bg = _hover ? Color.FromArgb(235, 238, 245) : BackColor;
            var rect = ClientRectangle;
            rect.Width -= 1;
            rect.Height -= 1;

            using var path = RoundedRect(rect, 16);
            using (var b = new SolidBrush(bg)) e.Graphics.FillPath(b, path);
            using (var pen = new Pen(Color.FromArgb(230, 232, 238), 1)) e.Graphics.DrawPath(pen, path);

            // ✅ chữ nét hơn: flags NoPadding + NoPrefix
            TextRenderer.DrawText(
                e.Graphics,
                Text,
                Font,
                rect,
                ForeColor,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.NoPrefix |
                TextFormatFlags.EndEllipsis
            );
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (Width <= 0 || Height <= 0) return;

            var rect = ClientRectangle;
            rect.Width -= 1;
            rect.Height -= 1;
            using var path = RoundedRect(rect, 16);
            Region = new Region(path);
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


    internal sealed class RoundedInput : UserControl
    {
        public TextBox InnerTextBox { get; } = new TextBox();

        public string Placeholder
        {
            get => _placeholder;
            set
            {
                _placeholder = value ?? "";
                if (!_hasFocus && string.IsNullOrWhiteSpace(InnerTextBox.Text))
                    ApplyPlaceholder();
            }
        }

        private string _placeholder = "";
        private bool _hasFocus;
        private bool _showingPlaceholder;

        public RoundedInput()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(248, 250, 252);
            Padding = new Padding(14, 10, 14, 10);

            InnerTextBox.BorderStyle = BorderStyle.None;
            InnerTextBox.Font = new Font("Segoe UI", 12F, FontStyle.Regular);


            InnerTextBox.BackColor = BackColor;
            InnerTextBox.ForeColor = Color.FromArgb(40, 40, 40);
            InnerTextBox.Multiline = false;

            Controls.Add(InnerTextBox);

            InnerTextBox.GotFocus += (_, __) =>
            {
                _hasFocus = true;
                if (_showingPlaceholder)
                {
                    InnerTextBox.Text = "";
                    InnerTextBox.ForeColor = Color.FromArgb(40, 40, 40);
                    _showingPlaceholder = false;
                }
                Invalidate();
            };

            InnerTextBox.LostFocus += (_, __) =>
            {
                _hasFocus = false;
                if (string.IsNullOrWhiteSpace(InnerTextBox.Text))
                    ApplyPlaceholder();
                Invalidate();
            };

            Layout += (_, __) =>
            {
                InnerTextBox.Location = new Point(Padding.Left, Padding.Top);
                InnerTextBox.Width = Width - Padding.Left - Padding.Right;
                InnerTextBox.Height = Height - Padding.Top - Padding.Bottom;
            };
        }

        public string GetText()
        {
            if (_showingPlaceholder) return "";
            return InnerTextBox.Text ?? "";
        }

        public void SetText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                // ✅ FIX: nếu đang focus thì KHÔNG được set placeholder vào TextBox.Text
                if (_hasFocus || InnerTextBox.Focused)
                {
                    _showingPlaceholder = false;
                    InnerTextBox.ForeColor = Color.FromArgb(40, 40, 40);
                    InnerTextBox.Text = "";
                    return;
                }

                ApplyPlaceholder();
                return;
            }

            _showingPlaceholder = false;
            InnerTextBox.ForeColor = Color.FromArgb(40, 40, 40);
            InnerTextBox.Text = text;
        }


        public void FocusInput() => InnerTextBox.Focus();

        private void ApplyPlaceholder()
        {
            if (_hasFocus || InnerTextBox.Focused) return; // ✅ tránh mọi trường hợp nhét placeholder khi đang focus

            _showingPlaceholder = true;
            InnerTextBox.Text = _placeholder;
            InnerTextBox.ForeColor = Color.FromArgb(150, 160, 180);
        }


        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            var border = _hasFocus ? Color.FromArgb(170, 195, 235) : Color.FromArgb(200, 215, 235);
            using var pen = new Pen(border, 2);

            var rect = ClientRectangle;
            rect.Width -= 1;
            rect.Height -= 1;

            using var path = RoundedRect(rect, 14);
            e.Graphics.DrawPath(pen, path);
            Region = new Region(path);
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

    internal sealed class ProgressCircle : Control
    {
        public int Value
        {
            get => _value;
            set { _value = Math.Max(0, Math.Min(100, value)); Invalidate(); }
        }
        private int _value;

        public ProgressCircle()
        {
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = ClientRectangle;
            rect.Inflate(-8, -8);

            using var bg = new Pen(Color.FromArgb(235, 238, 245), 10);
            using var fg = new Pen(Color.FromArgb(62, 92, 255), 10)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };

            e.Graphics.DrawArc(bg, rect, -90, 360);
            e.Graphics.DrawArc(fg, rect, -90, 360f * Value / 100f);

            var txt = $"{Value}%";
            using var f = new Font("Segoe UI", 14F, FontStyle.Bold);
            var sz = e.Graphics.MeasureString(txt, f);
            e.Graphics.DrawString(txt, f, Brushes.Black,
                (Width - sz.Width) / 2,
                (Height - sz.Height) / 2);
        }
    }
}
