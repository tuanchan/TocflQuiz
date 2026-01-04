using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using TocflQuiz.Controls.Features;
using TocflQuiz.Models;
using TocflQuiz.Services;



namespace TocflQuiz.Forms
{
    public sealed partial class CardForm : Form
    {
        // deps (nếu bạn đang dùng ctor có cfg/groups thì vẫn compile)
        private readonly AppConfig? _cfg;
        private readonly List<QuestionGroup>? _groups;
        private readonly Dictionary<string, ProgressRecord>? _progressMap;
        private readonly ProgressStoreJson? _store;
        private readonly SpacedRepetition? _sr;

        private readonly Button btnSets = new();
        private readonly Button btnStudy = new();
        private readonly Button btnCreate = new();

        private readonly Panel _host = new();

        private readonly CoursePickerFeatureControl _coursePicker = new();
        private readonly CreateCourseFeatureControl _createCourse = new();

        private CardSet? _selectedSet;
        private QuizFeatureControl? _quizView;


        // ✅ để tương thích nhiều chỗ gọi
        public CardForm() : this(null, null, null, null, null) { }

        // ✅ nếu project bạn đang yêu cầu ctor có cfg/... thì dùng ctor này
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

            Text = "Flashcards (Card)";
            StartPosition = FormStartPosition.CenterScreen;   // ✅ nên dùng CenterScreen cho full screen

            // ✅ full màn hình khi mở
            WindowState = FormWindowState.Maximized;

            // ✅ để tránh bị resize xuống nhỏ (tùy bạn giữ hay bỏ)
            MinimumSize = new Size(1000, 650);
            Font = new Font("Segoe UI", 9F);

            BuildUi();
            Wire();

            // default: show danh sách học phần
            ShowCourseList();
        }

        private void BuildUi()
        {
            var root = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20),
                BackColor = Color.FromArgb(245, 245, 245),
            };

            var top = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 80,
                ColumnCount = 3,
                RowCount = 1,
                Padding = new Padding(0),
            };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));

            StyleTopButton(btnSets, "📚 Danh sách học phần");
            StyleTopButton(btnStudy, "🧠 Thẻ ghi nhớ");
            StyleTopButton(btnCreate, "✍️ Tạo học phần mới");

            top.Controls.Add(btnSets, 0, 0);
            top.Controls.Add(btnStudy, 1, 0);
            top.Controls.Add(btnCreate, 2, 0);

            _host.Dock = DockStyle.Fill;
            _host.BackColor = Color.White;
            _host.Padding = new Padding(0);

            root.Controls.Add(_host);
            root.Controls.Add(top);
            Controls.Add(root);
        }

        private void Wire()
        {
            btnSets.Click += (_, __) => ShowCourseList();
            btnCreate.Click += (_, __) => ShowCreateCourse();
            btnStudy.Click += (_, __) => ShowFeature(CardFeatureKeys.Flashcards);

            _coursePicker.SelectedSetChanged += set =>
            {
                _selectedSet = set;
                // nếu đang ở màn feature -> cập nhật info
                RefreshFeatureHeaderIfAny();
                _quizView?.BindSelectedSet(_selectedSet);
            };

            _coursePicker.FeatureRequested += key =>
            {
                // bấm 1 trong 6 nút -> đóng danh sách -> show feature trong host
                ShowFeature(key);
            };

            _createCourse.ImportCompleted += newest =>
            {
                // import xong -> reload list và auto select set mới nhất
                _coursePicker.Reload();

                if (newest != null)
                {
                    _selectedSet = newest;
                    _quizView?.BindSelectedSet(_selectedSet);
                    // quay về danh sách để thấy học phần mới (giữ đúng flow bạn mô tả)
                    ShowCourseList();
                }
            };
        }

        private void ShowCourseList()
        {
            _host.Controls.Clear();
            _host.Controls.Add(_coursePicker);
            _coursePicker.BringToFront();
        }

        private void ShowCreateCourse()
        {
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

        private static void StyleTopButton(Button b, string text)
        {
            b.Text = text;
            b.Dock = DockStyle.Fill;
            b.Margin = new Padding(6);
            b.Height = 52;
            b.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
            b.BackColor = Color.White;
            b.ForeColor = Color.FromArgb(40, 40, 40);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = Color.FromArgb(225, 225, 225);
            b.Cursor = Cursors.Hand;
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
                _quizView.ExitToCourseListRequested += () => ShowCourseList(); // ✅ thêm dòng này
            }

            _quizView.BindSelectedSet(_selectedSet);
            return _quizView;
        }




    }
}
