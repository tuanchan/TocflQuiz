using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using TocflQuiz.Controls.Features;
using TocflQuiz.Controls.Features.Quiz;
using TocflQuiz.Models;
using TocflQuiz.Services;

namespace TocflQuiz.Forms
{
    public sealed partial class CardForm : Form
    {
        // Dependencies
        private readonly AppConfig? _cfg;
        private readonly List<QuestionGroup>? _groups;
        private readonly Dictionary<string, ProgressRecord>? _progressMap;
        private readonly ProgressStoreJson? _store;
        private readonly SpacedRepetition? _sr;

        // Top navigation buttons - chỉ giữ 2 nút chính
        private readonly Button btnStudy = new();
        private readonly Button btnCreate = new();
        private readonly Button btnTheme = new();

        private bool _isDarkMode = false;

        private readonly Panel _host = new();
        private readonly Panel _sidebar = new();

        private readonly CoursePickerFeatureControl _coursePicker = new();
        private readonly CreateCourseFeatureControl _createCourse = new();

        private CardSet? _selectedSet;
        private QuizFeatureControl? _quizView;
        private QuizEssayControl? _essayView;

        // Quizlet color palette
        private static readonly Color QuizletPrimary = Color.FromArgb(74, 85, 104); // #4a5568
        private static readonly Color QuizletBlue = Color.FromArgb(76, 146, 245); // #4c92f5
        private static readonly Color QuizletBlueDark = Color.FromArgb(46, 116, 215); // #2e74d7
        private static readonly Color QuizletBackground = Color.FromArgb(247, 250, 252); // #f7fafc
        private static readonly Color QuizletWhite = Color.White;
        private static readonly Color QuizletBorder = Color.FromArgb(226, 232, 240); // #e2e8f0

        public CardForm() : this(null, null, null, null, null) { }

        public CardForm(
            AppConfig? cfg,
            List<QuestionGroup>? groups,
            Dictionary<string, ProgressRecord>? progressMap,
            ProgressStoreJson? store,
            SpacedRepetition? sr)
        {
            _cfg = cfg;
            _groups = groups;
            _progressMap = progressMap;
            _store = store;
            _sr = sr;

            Text = "Flashcards - Học tập thông minh";
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;
            MinimumSize = new Size(1200, 700);
            Font = new Font("Segoe UI", 9.5F);
            BackColor = QuizletBackground;

            BuildUi();
            Wire();
            ShowCourseList();
        }

        private void BuildUi()
        {
            // Main container
            var root = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = QuizletBackground,
                Padding = new Padding(0)
            };

            // Top navigation bar
            var topNav = new Panel
            {
                Dock = DockStyle.Top,
                Height = 70,
                BackColor = QuizletWhite,
                Padding = new Padding(20, 0, 20, 0)
            };

            // Remove shadow/border for dark mode compatibility
            topNav.Paint += (s, e) =>
            {
                if (!_isDarkMode)
                {
                    using (var pen = new Pen(QuizletBorder, 1))
                    {
                        e.Graphics.DrawLine(pen, 0, topNav.Height - 1, topNav.Width, topNav.Height - 1);
                    }
                }
            };

            // Logo/Title
            var lblLogo = new Label
            {
                Text = "📚 Flashcards",
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = QuizletPrimary,
                AutoSize = true,
                Location = new Point(20, 22)
            };

            // Button container (right aligned)
            var btnContainer = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 500, // tăng từ 400 để chứa thêm nút
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0, 15, 0, 15),
                AutoSize = false,
                WrapContents = false
            };

            StylePrimaryButton(btnStudy, "📝 Kiểm tra", 140);
            StyleSecondaryButton(btnCreate, "✍️ Tạo học phần", 150);
            StyleThemeButton(btnTheme, _isDarkMode ? "🌙 Tối" : "☀️ Sáng", 90);

            btnContainer.Controls.Add(btnStudy);
            btnContainer.Controls.Add(btnCreate);
            btnContainer.Controls.Add(btnTheme);

            topNav.Controls.Add(lblLogo);
            topNav.Controls.Add(btnContainer);

            // Main content area with sidebar layout
            var mainContent = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = QuizletBackground,
                Padding = new Padding(0)
            };

            // Sidebar for course list (collapsible)
            _sidebar.Dock = DockStyle.Left;
            _sidebar.Width = 380; // tăng từ 320 lên 380
            _sidebar.BackColor = QuizletWhite;
            _sidebar.Padding = new Padding(0);

            // Remove border painting for dark mode
            _sidebar.Paint += (s, e) =>
            {
                if (!_isDarkMode)
                {
                    using (var pen = new Pen(QuizletBorder, 1))
                    {
                        e.Graphics.DrawLine(pen, _sidebar.Width - 1, 0, _sidebar.Width - 1, _sidebar.Height);
                    }
                }
            };

            // Host panel for main content
            _host.Dock = DockStyle.Fill;
            _host.BackColor = QuizletBackground;
            _host.Padding = new Padding(20);

            mainContent.Controls.Add(_host);
            mainContent.Controls.Add(_sidebar);

            root.Controls.Add(mainContent);
            root.Controls.Add(topNav);

            Controls.Add(root);
        }

        private void Wire()
        {
            btnCreate.Click += (_, __) => ShowCreateCourse();
            btnStudy.Click += (_, __) => ShowFeature(CardFeatureKeys.Quiz);
            btnTheme.Click += (_, __) => ToggleTheme();

            _coursePicker.SelectedSetChanged += set =>
            {
                _selectedSet = set;
                RefreshFeatureHeaderIfAny();
                _quizView?.BindSelectedSet(_selectedSet);
            };

            _coursePicker.FeatureRequested += key =>
            {
                ShowFeature(key);
            };

            _createCourse.ImportCompleted += newest =>
            {
                _coursePicker.Reload();

                if (newest != null)
                {
                    _selectedSet = newest;
                    _quizView?.BindSelectedSet(_selectedSet);
                    ShowCourseList();
                }
            };
        }

        private void ShowCourseList()
        {
            // Show sidebar with course list
            _sidebar.Visible = true;
            _sidebar.Controls.Clear();
            _sidebar.Controls.Add(_coursePicker);
            _coursePicker.BringToFront();

            // Show welcome screen in main area if no set selected
            if (_selectedSet == null)
            {
                ShowWelcomeScreen();
            }
        }

        private void ShowWelcomeScreen()
        {
            var welcome = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = QuizletBackground
            };

            var container = new Panel
            {
                Size = new Size(500, 300),
                BackColor = QuizletWhite,
                Location = new Point(0, 0)
            };

            var lblTitle = new Label
            {
                Text = "Chào mừng đến với Flashcards! 🎉",
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                ForeColor = QuizletPrimary,
                AutoSize = true,
                Location = new Point(50, 80)
            };

            var lblSubtitle = new Label
            {
                Text = "Chọn một học phần bên trái để bắt đầu học",
                Font = new Font("Segoe UI", 11F),
                ForeColor = Color.FromArgb(100, 100, 100),
                AutoSize = true,
                Location = new Point(50, 120)
            };

            container.Controls.Add(lblTitle);
            container.Controls.Add(lblSubtitle);

            // Center the container
            welcome.Resize += (s, e) =>
            {
                container.Location = new Point(
                    (welcome.Width - container.Width) / 2,
                    (welcome.Height - container.Height) / 2
                );
            };

            welcome.Controls.Add(container);

            _host.Controls.Clear();
            _host.Controls.Add(welcome);
            welcome.BringToFront();
        }

        private void ShowCreateCourse()
        {
            _sidebar.Visible = false;
            _host.Controls.Clear();
            _host.Controls.Add(_createCourse);
            _createCourse.BringToFront();
        }

        private void ShowFeature(string key)
        {
            if (_selectedSet == null)
            {
                MessageBox.Show("Bạn chưa chọn học phần.", "Thiếu học phần",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                ShowCourseList();
                return;
            }

            _sidebar.Visible = true;

            UserControl view = key switch
            {
                CardFeatureKeys.Flashcards => CreateFlashcardsView(),
                CardFeatureKeys.Course => MakeStub("Học phần", "Xem thông tin / quản lý học phần"),
                CardFeatureKeys.Quiz => CreateQuizView(),
                CardFeatureKeys.Blocks => MakeStub("Blocks", "Chế độ học Blocks"),
                CardFeatureKeys.Blast => MakeStub("Blast", "Chế độ học Blast"),
                CardFeatureKeys.MergeCards => MakeStub("Ghép thẻ", "Ghép thẻ / matching"),
                _ => MakeStub("Chức năng", "Không xác định")
            };

            _host.Controls.Clear();
            _host.Controls.Add(view);
            view.BringToFront();
        }

        private FeatureStubControl MakeStub(string title, string subtitle)
        {
            var stub = new FeatureStubControl(title, subtitle);
            stub.BindSelectedSet(_selectedSet);
            return stub;
        }

        private void RefreshFeatureHeaderIfAny()
        {
            if (_host.Controls.Count == 0) return;
            if (_host.Controls[0] is FeatureStubControl stub)
                stub.BindSelectedSet(_selectedSet);
        }

        private static void StylePrimaryButton(Button b, string text, int width)
        {
            b.Text = text;
            b.Width = width;
            b.Height = 40;
            b.Margin = new Padding(5, 0, 5, 0);
            b.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            b.BackColor = QuizletBlue;
            b.ForeColor = Color.White;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.Cursor = Cursors.Hand;

            b.MouseEnter += (s, e) => b.BackColor = QuizletBlueDark;
            b.MouseLeave += (s, e) => b.BackColor = QuizletBlue;
        }

        private static void StyleSecondaryButton(Button b, string text, int width)
        {
            b.Text = text;
            b.Width = width;
            b.Height = 40;
            b.Margin = new Padding(5, 0, 5, 0);
            b.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            b.BackColor = QuizletWhite;
            b.ForeColor = QuizletBlue;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 2;
            b.FlatAppearance.BorderColor = QuizletBlue;
            b.Cursor = Cursors.Hand;

            b.MouseEnter += (s, e) => b.BackColor = Color.FromArgb(240, 245, 255);
            b.MouseLeave += (s, e) => b.BackColor = QuizletWhite;
        }

        private static void StyleThemeButton(Button b, string text, int width)
        {
            b.Text = text;
            b.Width = width;
            b.Height = 40;
            b.Margin = new Padding(5, 0, 5, 0);
            b.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            b.BackColor = Color.White;
            b.ForeColor = QuizletPrimary;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 2;
            b.FlatAppearance.BorderColor = QuizletBorder;
            b.Cursor = Cursors.Hand;

            b.MouseEnter += (s, e) =>
            {
                b.BackColor = Color.FromArgb(240, 245, 255);
                b.FlatAppearance.BorderColor = QuizletBlue;
            };
            b.MouseLeave += (s, e) =>
            {
                b.BackColor = Color.White;
                b.FlatAppearance.BorderColor = QuizletBorder;
            };
        }

        private void ToggleTheme()
        {
            _isDarkMode = !_isDarkMode;

            if (_isDarkMode)
            {
                // Dark mode colors
                btnTheme.Text = "🌙 Tối";
                BackColor = Color.FromArgb(30, 30, 40);

                foreach (Control ctrl in Controls)
                {
                    ApplyDarkTheme(ctrl);
                }

                // Apply dark mode to course picker
                _coursePicker.SetDarkMode(true);
            }
            else
            {
                // Light mode colors
                btnTheme.Text = "☀️ Sáng";
                BackColor = QuizletBackground;

                foreach (Control ctrl in Controls)
                {
                    ApplyLightTheme(ctrl);
                }

                // Apply light mode to course picker
                _coursePicker.SetDarkMode(false);
            }
        }

        private void ApplyDarkTheme(Control ctrl)
        {
            if (ctrl is Panel panel)
            {
                if (panel == _sidebar)
                {
                    panel.BackColor = Color.FromArgb(40, 40, 50);
                }
                else if (panel.BackColor == QuizletBackground || panel.BackColor == Color.White)
                {
                    panel.BackColor = Color.FromArgb(30, 30, 40);
                }

                foreach (Control child in panel.Controls)
                {
                    ApplyDarkTheme(child);
                }
            }
            else if (ctrl is Label lbl)
            {
                lbl.ForeColor = Color.FromArgb(220, 220, 220);
            }

            // Refresh paint để remove borders
            ctrl.Refresh();
        }

        private void ApplyLightTheme(Control ctrl)
        {
            if (ctrl is Panel panel)
            {
                if (panel == _sidebar)
                {
                    panel.BackColor = QuizletWhite;
                }
                else if (panel.BackColor == Color.FromArgb(30, 30, 40))
                {
                    panel.BackColor = QuizletBackground;
                }

                foreach (Control child in panel.Controls)
                {
                    ApplyLightTheme(child);
                }
            }
            else if (ctrl is Label lbl)
            {
                lbl.ForeColor = QuizletPrimary;
            }

            // Refresh paint để hiển thị borders
            ctrl.Refresh();
        }

        private UserControl CreateFlashcardsView()
        {
            var fc = new TocflQuiz.Controls.Features.FlashcardsFeatureControl();
            if (_selectedSet != null)
                fc.LoadSet(_selectedSet);
            return fc;
        }

        private UserControl CreateQuizView()
        {
            if (_quizView == null)
            {
                _quizView = new QuizFeatureControl();
                _quizView.ExitToCourseListRequested += () => ShowCourseList();
                _quizView.EssayModeRequested += (set, cfg) => ShowEssayQuiz(set, cfg);
            }

            _quizView.BindSelectedSet(_selectedSet);
            return _quizView;
        }

        private void ShowEssayQuiz(CardSet set, QuizConfig cfg)
        {
            if (_essayView == null)
            {
                _essayView = new QuizEssayControl();
                _essayView.ExitRequested += () => ShowCourseList();
            }

            _essayView.BindSelectedSet(set, cfg.AnswerMode, cfg.Count, set.Title);

            _host.Controls.Clear();
            _host.Controls.Add(_essayView);
            _essayView.BringToFront();
        }
    }
}