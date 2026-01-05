using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using TocflQuiz.Models;
using TocflQuiz.Services;

namespace TocflQuiz.Forms
{
    public sealed partial class MainForm : Form
    {
        private readonly AppConfig _cfg;
        private readonly List<QuestionGroup> _groups;
        private readonly Dictionary<string, ProgressRecord> _progressMap;
        private readonly ProgressStoreJson _store;
        private readonly SpacedRepetition _sr;

        private ComboBox cboMode = new();
        private ComboBox cboCategory = new();
        private Panel pnlStats = new();
        private Button btnList = new();
        private Button btnDue = new();
        private Button btnStartRandom = new();
        private Button btnCards = new();

        // Config controls
        private TextBox txtDatasetRoot = new();
        private TextBox txtProgressFile = new();
        private Button btnBrowseDataset = new();
        private Button btnBrowseProgress = new(); // giữ nguyên (không đổi logic)
        private Button btnApplyConfig = new();
        private Button btnRescan = new(); // giữ nguyên logic (UI không show theo layout ảnh)

        // Selection extra (dòng Category thứ 3 như ảnh)
        private TextBox txtCategoryExtra = new();

        // Modern UI - Chart panels
        private Panel pnlChartNew = new();
        private Panel pnlChartDone = new();
        private Panel pnlChartDue = new();
        private Label lblChartNewValue = new();
        private Label lblChartDoneValue = new();
        private Label lblChartDueValue = new();

        // Theme constants (tuning theo ảnh)
        private static readonly Color AppBgTop = Color.FromArgb(10, 17, 33);
        private static readonly Color AppBgBottom = Color.FromArgb(18, 28, 48);

        private static readonly Color ShellTop = Color.FromArgb(23, 33, 52);
        private static readonly Color ShellBottom = Color.FromArgb(17, 24, 39);

        private static readonly Color CardTop = Color.FromArgb(34, 46, 67);
        private static readonly Color CardBottom = Color.FromArgb(24, 34, 54);

        private static readonly Color InputBg = Color.FromArgb(44, 57, 78);
        private static readonly Color InputBorder = Color.FromArgb(72, 85, 110);

        private static readonly Color MutedText = Color.FromArgb(148, 163, 184);
        private static readonly Color MainText = Color.FromArgb(226, 232, 240);

        private static readonly Color SoftBorder = Color.FromArgb(60, 75, 105);

        private const int ShellRadius = 24;
        private const int CardRadius = 22;
        private const int InputRadius = 18;
        private const int ButtonRadius = 18;
        private const int StatRadius = 18;

        public MainForm(
            AppConfig cfg,
            List<QuestionGroup> groups,
            Dictionary<string, ProgressRecord> progressMap,
            ProgressStoreJson store,
            SpacedRepetition sr)
        {
            _cfg = cfg;
            _groups = groups ?? new List<QuestionGroup>();
            _progressMap = progressMap ?? new Dictionary<string, ProgressRecord>(StringComparer.OrdinalIgnoreCase);
            _store = store;
            _sr = sr;

            Text = "TOCFL Quiz Manager";
            Width = 1600;
            Height = 1000;

            MinimumSize = new Size(1600, 1000);
            WindowState = FormWindowState.Maximized;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9.5F);
            DoubleBuffered = true;
            ResizeRedraw = true;

            ResizeBegin += (_, __) => SuspendLayout();
            ResizeEnd += (_, __) =>
            {
                ResumeLayout(true);
                Invalidate(true);   // repaint toàn bộ
                Update();           // đẩy repaint ngay
            };

            // giảm flicker
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            UpdateStyles();
            EnableDoubleBuffering(this);

            BuildModernUi();
            LoadModes();
            UpdateStats();
        }
        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x02000000; // WS_EX_COMPOSITED
                return cp;
            }
        }
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            Invalidate(true);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // nền gradient giống ảnh
            e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
            using var br = new LinearGradientBrush(ClientRectangle, AppBgTop, AppBgBottom, 90f);
            e.Graphics.FillRectangle(br, ClientRectangle);
        }

        private void BuildModernUi()
        {
            var mainContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = new Padding(18)
            };
            EnableDoubleBuffering(mainContainer);

            var shell = CreateShellPanel();
            shell.Dock = DockStyle.Fill;

            var contentLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3,
                BackColor = Color.Transparent
            };

            // Columns: Left (60%) | Right (40%)
            contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
            contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));

            // Rows: Config | Main | Actions (tuning theo ảnh)
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 220));

            var configPanel = BuildModernConfigSection();
            contentLayout.Controls.Add(configPanel, 0, 0);
            contentLayout.SetColumnSpan(configPanel, 2);

            var selectionPanel = BuildModernSelectionSection();
            contentLayout.Controls.Add(selectionPanel, 0, 1);

            var statsPanel = BuildModernStatsSection();
            contentLayout.Controls.Add(statsPanel, 1, 1);

            var actionsPanel = BuildModernActionsSection();
            contentLayout.Controls.Add(actionsPanel, 0, 2);
            contentLayout.SetColumnSpan(actionsPanel, 2);

            shell.Controls.Add(contentLayout);
            mainContainer.Controls.Add(shell);
            Controls.Add(mainContainer);
        }

        private Panel CreateShellPanel()
        {
            var shell = new Panel
            {
                BackColor = Color.Transparent,
                Padding = new Padding(18)
            };
            EnableDoubleBuffering(shell);

            shell.SizeChanged += (_, __) =>
            {
                ApplyRoundedRegion(shell, ShellRadius);
                shell.Invalidate();
            };
            ApplyRoundedRegion(shell, ShellRadius);

            shell.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                // shadow nhẹ
                using (var shadowPath = CreateRoundedRectangle(new Rectangle(2, 3, shell.Width - 6, shell.Height - 6), ShellRadius))
                using (var shadowBrush = new SolidBrush(Color.FromArgb(70, 0, 0, 0)))
                    e.Graphics.FillPath(shadowBrush, shadowPath);

                using var path = CreateRoundedRectangle(new Rectangle(0, 0, shell.Width - 1, shell.Height - 1), ShellRadius);
                using var br = new LinearGradientBrush(shell.ClientRectangle, ShellTop, ShellBottom, 90f);
                e.Graphics.FillPath(br, path);

                using var pen = new Pen(Color.FromArgb(80, SoftBorder), 1);
                e.Graphics.DrawPath(pen, path);
            };

            return shell;
        }

        private Panel CreateCardPanel(Padding padding)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = padding
            };
            EnableDoubleBuffering(panel);

            panel.SizeChanged += (_, __) =>
            {
                ApplyRoundedRegion(panel, CardRadius);
                panel.Invalidate();
            };
            ApplyRoundedRegion(panel, CardRadius);

            panel.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                // shadow nhẹ
                using (var shadowPath = CreateRoundedRectangle(new Rectangle(2, 3, panel.Width - 6, panel.Height - 6), CardRadius))
                using (var shadowBrush = new SolidBrush(Color.FromArgb(60, 0, 0, 0)))
                    e.Graphics.FillPath(shadowBrush, shadowPath);

                using var path = CreateRoundedRectangle(new Rectangle(0, 0, panel.Width - 1, panel.Height - 1), CardRadius);
                using var br = new LinearGradientBrush(panel.ClientRectangle, CardTop, CardBottom, 90f);
                e.Graphics.FillPath(br, path);

                using var pen = new Pen(Color.FromArgb(90, SoftBorder), 1);
                e.Graphics.DrawPath(pen, path);
            };

            return panel;
        }

        private Panel BuildModernConfigSection()
        {
            var card = CreateCardPanel(new Padding(22, 18, 22, 18));

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 2,
                BackColor = Color.Transparent
            };
            EnableDoubleBuffering(layout);

            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));

            // Row 0
            var lblDataset = CreateModernLabel("Dataset Root:");
            var datasetContainer = CreateRoundedInputContainer(new Padding(16, 10, 16, 10));
            txtDatasetRoot = CreateInnerTextBox(_cfg.DatasetRoot ?? "", readOnly: false, foreColor: MainText);
            BindTextBoxToContainer(txtDatasetRoot, datasetContainer);

            btnBrowseDataset = CreateModernButton("▢  Browse", Color.FromArgb(59, 130, 246));
            btnBrowseDataset.Dock = DockStyle.Fill;
            btnBrowseDataset.Margin = new Padding(6, 6, 0, 6);
            btnBrowseDataset.Click += (_, __) =>
            {
                using var dlg = new FolderBrowserDialog();
                dlg.Description = "Chọn thư mục DatasetRoot";
                if (dlg.ShowDialog() == DialogResult.OK)
                    txtDatasetRoot.Text = dlg.SelectedPath;
            };

            layout.Controls.Add(lblDataset, 0, 0);
            layout.Controls.Add(datasetContainer, 1, 0);
            layout.Controls.Add(btnBrowseDataset, 2, 0);

            // Row 1
            var lblProgress = CreateModernLabel("Progress File:");
            var progContainer = CreateRoundedInputContainer(new Padding(16, 10, 16, 10));
            txtProgressFile = CreateInnerTextBox(_cfg.ProgressFilePath ?? "", readOnly: true, foreColor: MutedText);
            BindTextBoxToContainer(txtProgressFile, progContainer);

            btnApplyConfig = CreateModernButton("✓  Apply Config", Color.FromArgb(34, 197, 94));
            btnApplyConfig.Dock = DockStyle.Fill;
            btnApplyConfig.Margin = new Padding(6, 6, 0, 6);
            btnApplyConfig.Click += (_, __) =>
            {
                _cfg.DatasetRoot = (txtDatasetRoot.Text ?? "").Trim();
                SettingsService.Save(new AppSettings { DatasetRoot = _cfg.DatasetRoot });
                MessageBox.Show("Đã lưu DatasetRoot vào settings.json (LocalAppData).", "Success",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                UpdateStats();
                RescanDataset();
            };

            layout.Controls.Add(lblProgress, 0, 1);
            layout.Controls.Add(progContainer, 1, 1);
            layout.Controls.Add(btnApplyConfig, 2, 1);

            card.Controls.Add(layout);
            return card;
        }

        private Panel BuildModernSelectionSection()
        {
            var card = CreateCardPanel(new Padding(22, 20, 22, 18));
            card.Margin = new Padding(0, 0, 10, 0);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3,
                BackColor = Color.Transparent
            };
            EnableDoubleBuffering(layout);

            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // 2 dòng chọn mode/category + phần còn lại là info
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            // Mode
            var lblMode = CreateModernLabel("Mode:");
            var modeContainer = CreateRoundedInputContainer(new Padding(14, 0, 14, 0));
            cboMode = CreateModernComboBox();
            cboMode.SelectedIndexChanged += (_, __) => LoadCategories();
            BindComboBoxToContainer(cboMode, modeContainer);

            // Category
            var lblCategory = CreateModernLabel("Category:");
            var catContainer = CreateRoundedInputContainer(new Padding(14, 0, 14, 0));
            cboCategory = CreateModernComboBox();
            cboCategory.SelectedIndexChanged += (_, __) => UpdateStats();
            BindComboBoxToContainer(cboCategory, catContainer);

            layout.Controls.Add(lblMode, 0, 0);
            layout.Controls.Add(modeContainer, 1, 0);

            layout.Controls.Add(lblCategory, 0, 1);
            layout.Controls.Add(catContainer, 1, 1);

            // ======= INFO PANEL (chuyển từ bên phải sang đây) =======
            var infoHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 14, 0, 0)
            };
            EnableDoubleBuffering(infoHost);

            pnlStats = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = InputBg,
                Padding = new Padding(18)
            };
            EnableDoubleBuffering(pnlStats);

            ApplyRoundedRegion(pnlStats, 16);
            pnlStats.SizeChanged += (_, __) => { ApplyRoundedRegion(pnlStats, 16); pnlStats.Invalidate(); };

            var lblStatsText = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = MainText,
                Font = new Font("Segoe UI", 10F),
                AutoSize = false,
                BackColor = Color.Transparent
            };

            pnlStats.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = CreateRoundedRectangle(new Rectangle(0, 0, pnlStats.Width - 1, pnlStats.Height - 1), 16);
                using var brush = new SolidBrush(InputBg);
                e.Graphics.FillPath(brush, path);

                using var pen = new Pen(Color.FromArgb(90, InputBorder), 1);
                e.Graphics.DrawPath(pen, path);
            };

            pnlStats.Controls.Add(lblStatsText);
            pnlStats.Tag = lblStatsText;

            infoHost.Controls.Add(pnlStats);

            // chiếm toàn bộ hàng 3
            layout.Controls.Add(infoHost, 0, 2);
            layout.SetColumnSpan(infoHost, 2);

            card.Controls.Add(layout);
            return card;
        }


        private Panel BuildModernStatsSection()
        {
            var card = CreateCardPanel(new Padding(22, 20, 22, 18));
            card.Margin = new Padding(10, 0, 0, 0);

            var container = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 1,
                ColumnCount = 1,
                BackColor = Color.Transparent
            };
            EnableDoubleBuffering(container);

            container.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var chartContainer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 4, 0, 0)
            };
            EnableDoubleBuffering(chartContainer);

            chartContainer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            chartContainer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            chartContainer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));

            pnlChartNew = CreateStatCard("NEW", Color.FromArgb(96, 165, 250), "📄", out lblChartNewValue);
            pnlChartDone = CreateStatCard("DONE", Color.FromArgb(74, 222, 128), "✎", out lblChartDoneValue);
            pnlChartDue = CreateStatCard("DUE", Color.FromArgb(248, 113, 113), "⏰", out lblChartDueValue);

            pnlChartNew.Margin = new Padding(0, 0, 10, 0);
            pnlChartDone.Margin = new Padding(5, 0, 5, 0);
            pnlChartDue.Margin = new Padding(10, 0, 0, 0);

            chartContainer.Controls.Add(pnlChartNew, 0, 0);
            chartContainer.Controls.Add(pnlChartDone, 1, 0);
            chartContainer.Controls.Add(pnlChartDue, 2, 0);

            container.Controls.Add(chartContainer, 0, 0);
            card.Controls.Add(container);

            return card;
        }


        private Panel BuildModernActionsSection()
        {
            var card = CreateCardPanel(new Padding(22, 18, 22, 18));

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 6, 0, 0)
            };
            EnableDoubleBuffering(layout);

            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));

            btnList = CreateModernActionButton("▢  List View", Color.FromArgb(59, 130, 246),
                "View all questions by status");
            btnList.Click += (_, __) => OpenList(false);
            btnList.Margin = new Padding(0, 0, 12, 0);

            btnDue = CreateModernActionButton("⏱  Review Due", Color.FromArgb(239, 68, 68),
                "Practice questions due for review");
            btnDue.Click += (_, __) => OpenList(true);
            btnDue.Margin = new Padding(6, 0, 12, 0);

            btnStartRandom = CreateModernActionButton("⌁  Random Quiz", Color.FromArgb(34, 197, 94),
                "Start a random quiz from category");
            btnStartRandom.Click += (_, __) => StartRandomInCategory();
            btnStartRandom.Margin = new Padding(6, 0, 12, 0);

            btnCards = CreateModernActionButton("▢  Flashcards", Color.FromArgb(168, 85, 247),
                "Study with flashcard mode");
            btnCards.Click += (_, __) =>
            {
                var f = new CardForm(_cfg, _groups, _progressMap, _store, _sr);
                f.Show(this);
            };
            btnCards.Margin = new Padding(6, 0, 0, 0);

            layout.Controls.Add(btnList, 0, 0);
            layout.Controls.Add(btnDue, 1, 0);
            layout.Controls.Add(btnStartRandom, 2, 0);
            layout.Controls.Add(btnCards, 3, 0);

            card.Controls.Add(layout);
            return card;
        }

        // =========================
        // UI Helper Methods
        // =========================

        private Label CreateModernLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 10F, FontStyle.Regular),
                ForeColor = MutedText,
                Padding = new Padding(0, 0, 10, 0)
            };
        }

        private ComboBox CreateModernComboBox()
        {
            var cbo = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10.5F),
                BackColor = InputBg,
                ForeColor = MainText,
                FlatStyle = FlatStyle.Flat,
                IntegralHeight = false,
                Height = 44,
                Margin = new Padding(0, 8, 0, 8)
            };
            EnableDoubleBuffering(cbo);
            return cbo;
        }

        private Panel CreateRoundedInputContainer(Padding innerPadding)
        {
            var p = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = InputBg,
                Padding = innerPadding,
                Margin = new Padding(0, 8, 0, 8)
            };
            EnableDoubleBuffering(p);

            ApplyRoundedRegion(p, InputRadius);
            p.SizeChanged += (_, __) =>
            {
                ApplyRoundedRegion(p, InputRadius);
                p.Invalidate();

                if (p.Tag is TextBox tb) CenterInnerTextBox(tb, p);
                if (p.Tag is ComboBox cb) CenterInnerComboBox(cb, p);
            };

            p.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                using var path = CreateRoundedRectangle(new Rectangle(0, 0, p.Width - 1, p.Height - 1), InputRadius);
                using var brush = new SolidBrush(InputBg);
                e.Graphics.FillPath(brush, path);

                // top highlight (giả gradient như ảnh)
                using var hiPen = new Pen(Color.FromArgb(45, 255, 255, 255), 1);
                e.Graphics.DrawLine(hiPen, 18, 6, p.Width - 18, 6);

                using var pen = new Pen(Color.FromArgb(100, InputBorder), 1);
                e.Graphics.DrawPath(pen, path);
            };

            return p;
        }

        private TextBox CreateInnerTextBox(string text, bool readOnly, Color foreColor)
        {
            var tb = new TextBox
            {
                ReadOnly = readOnly,
                BorderStyle = BorderStyle.None,
                BackColor = InputBg,          // khít màu với pill
                ForeColor = foreColor,
                Font = new Font("Segoe UI", 10F),
                Text = text ?? "",
                TabStop = true
            };

            // đảm bảo không bị đổi màu khi ReadOnly
            tb.ReadOnlyChanged += (_, __) => tb.BackColor = InputBg;

            return tb;
        }

        private void BindTextBoxToContainer(TextBox tb, Panel container)
        {
            container.Controls.Add(tb);
            container.Tag = tb;

            tb.BackColor = container.BackColor;
            tb.BorderStyle = BorderStyle.None;
            tb.Anchor = AnchorStyles.Left | AnchorStyles.Right;

            CenterInnerTextBox(tb, container);
        }

        private void BindComboBoxToContainer(ComboBox cb, Panel container)
        {
            container.Controls.Add(cb);
            container.Tag = cb;

            cb.Margin = new Padding(0);
            cb.Anchor = AnchorStyles.Left | AnchorStyles.Right;

            CenterInnerComboBox(cb, container);
        }

        private void CenterInnerTextBox(TextBox tb, Panel container)
        {
            var innerWidth = Math.Max(10, container.ClientSize.Width - container.Padding.Left - container.Padding.Right);
            tb.Width = innerWidth;
            tb.Location = new Point(container.Padding.Left, Math.Max(0, (container.ClientSize.Height - tb.Height) / 2));
        }

        private void CenterInnerComboBox(ComboBox cb, Panel container)
        {
            var innerWidth = Math.Max(10, container.ClientSize.Width - container.Padding.Left - container.Padding.Right);
            cb.Width = innerWidth;
            cb.Location = new Point(container.Padding.Left, Math.Max(0, (container.ClientSize.Height - cb.Height) / 2));
        }

        private Button CreateModernButton(string text, Color baseColor)
        {
            var btn = new Button
            {
                Text = "",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = baseColor,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Height = 46,
                Tag = text,
                UseVisualStyleBackColor = false
            };
            EnableDoubleBuffering(btn);

            btn.FlatAppearance.BorderSize = 0;
            ApplyRoundedRegion(btn, ButtonRadius);
            btn.SizeChanged += (_, __) => { ApplyRoundedRegion(btn, ButtonRadius); btn.Invalidate(); };

            btn.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

                using var path = CreateRoundedRectangle(new Rectangle(0, 0, btn.Width - 1, btn.Height - 1), ButtonRadius);

                // gradient nhẹ như ảnh
                using var brush = new LinearGradientBrush(btn.ClientRectangle,
                    ControlPaint.Light(btn.BackColor, 0.08f),
                    ControlPaint.Dark(btn.BackColor, 0.10f),
                    90f);

                e.Graphics.FillPath(brush, path);

                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                e.Graphics.DrawString(btn.Tag?.ToString() ?? "", btn.Font, Brushes.White, btn.ClientRectangle, sf);
            };

            var originalColor = baseColor;
            btn.MouseEnter += (s, e) => { btn.BackColor = ControlPaint.Light(originalColor, 0.12f); btn.Invalidate(); };
            btn.MouseLeave += (s, e) => { btn.BackColor = originalColor; btn.Invalidate(); };

            return btn;
        }

        private Button CreateModernActionButton(string text, Color baseColor, string subtitle)
        {
            var btn = new Button
            {
                Text = "",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = baseColor,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Height = 130,
                Dock = DockStyle.Fill,
                Tag = new { Title = text, Subtitle = subtitle },
                UseVisualStyleBackColor = false
            };
            EnableDoubleBuffering(btn);

            btn.FlatAppearance.BorderSize = 0;
            ApplyRoundedRegion(btn, 18);
            btn.SizeChanged += (_, __) => { ApplyRoundedRegion(btn, 18); btn.Invalidate(); };

            btn.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

                using var path = CreateRoundedRectangle(new Rectangle(0, 0, btn.Width - 1, btn.Height - 1), 18);
                using var brush = new LinearGradientBrush(btn.ClientRectangle,
                    ControlPaint.Light(btn.BackColor, 0.10f),
                    ControlPaint.Dark(btn.BackColor, 0.12f),
                    90f);
                e.Graphics.FillPath(brush, path);

                dynamic data = btn.Tag;

                using var titleFont = new Font("Segoe UI", 14F, FontStyle.Bold);
                using var subFont = new Font("Segoe UI", 9.5F, FontStyle.Regular);
                using var subBrush = new SolidBrush(Color.FromArgb(220, 255, 255, 255));

                var titleSize = e.Graphics.MeasureString(data.Title, titleFont);
                e.Graphics.DrawString(data.Title, titleFont, Brushes.White,
                    (btn.Width - titleSize.Width) / 2, 32);

                var subSize = e.Graphics.MeasureString(data.Subtitle, subFont);
                e.Graphics.DrawString(data.Subtitle, subFont, subBrush,
                    (btn.Width - subSize.Width) / 2, 74);
            };

            var originalColor = baseColor;
            btn.MouseEnter += (s, e) => { btn.BackColor = ControlPaint.Light(originalColor, 0.12f); btn.Invalidate(); };
            btn.MouseLeave += (s, e) => { btn.BackColor = originalColor; btn.Invalidate(); };

            return btn;
        }

        private Panel CreateStatCard(string label, Color accentColor, string icon, out Label valueLabel)
        {
            var host = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent
            };
            EnableDoubleBuffering(host);

            var inner = new Panel
            {
                BackColor = Color.Transparent
            };
            EnableDoubleBuffering(inner);

            var lblTitle = new Label
            {
                Text = label,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = accentColor,
                AutoSize = true
            };

            var value = new Label
            {
                Text = "0",
                Font = new Font("Segoe UI", 44F, FontStyle.Bold),
                ForeColor = accentColor,
                AutoSize = true
            };

            var lblIcon = new Label
            {
                Text = icon,
                Font = new Font("Segoe UI", 26F),
                ForeColor = Color.FromArgb(140, 148, 163, 184),
                AutoSize = true
            };

            inner.Controls.Add(lblTitle);
            inner.Controls.Add(value);
            inner.Controls.Add(lblIcon);
            host.Controls.Add(inner);

            void LayoutSquare()
            {
                if (host.Width <= 0 || host.Height <= 0) return;

                int pad = 6;
                int size = Math.Min(host.Width, host.Height) - pad * 2;
                if (size < 10) size = Math.Min(host.Width, host.Height);

                var x = (host.Width - size) / 2;
                var y = (host.Height - size) / 2;
                inner.Bounds = new Rectangle(x, y, size, size);

                ApplyRoundedRegion(inner, StatRadius);
                inner.Invalidate();

                lblTitle.Location = new Point(18, 14);
                value.Location = new Point(18, 44);

                var desiredIconY = value.Bottom + 4;
                var maxIconY = inner.Height - lblIcon.Height - 14;
                var iconY = Math.Min(desiredIconY, maxIconY);
                lblIcon.Location = new Point((inner.Width - lblIcon.Width) / 2, Math.Max(value.Bottom + 2, iconY));
            }

            inner.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                // shadow nhẹ
                using (var shadowPath = CreateRoundedRectangle(new Rectangle(2, 3, inner.Width - 6, inner.Height - 6), StatRadius))
                using (var shadowBrush = new SolidBrush(Color.FromArgb(55, 0, 0, 0)))
                    e.Graphics.FillPath(shadowBrush, shadowPath);

                using var path = CreateRoundedRectangle(new Rectangle(0, 0, inner.Width - 1, inner.Height - 1), StatRadius);
                using var brush = new LinearGradientBrush(inner.ClientRectangle,
                    ControlPaint.Light(InputBg, 0.05f),
                    ControlPaint.Dark(InputBg, 0.05f),
                    90f);
                using var pen = new Pen(accentColor, 2);

                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            };

            host.SizeChanged += (_, __) => LayoutSquare();
            inner.SizeChanged += (_, __) => { ApplyRoundedRegion(inner, StatRadius); LayoutSquare(); };

            LayoutSquare();

            valueLabel = value;
            return host;
        }

        private void ApplyRoundedRegion(Control c, int radius)
        {
            if (c.Width <= 1 || c.Height <= 1) return;
            var rect = new Rectangle(0, 0, c.Width - 1, c.Height - 1);

            using var path = CreateRoundedRectangle(rect, radius);
            c.Region = new Region(path);
        }

        private void EnableDoubleBuffering(Control c)
        {
            try
            {
                var prop = typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
                prop?.SetValue(c, true, null);
            }
            catch { /* ignore */ }
        }

        private GraphicsPath CreateRoundedRectangle(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                path.AddRectangle(Rectangle.Empty);
                return path;
            }

            int maxR = Math.Min(rect.Width, rect.Height) / 2;
            radius = Math.Max(1, Math.Min(radius, Math.Max(1, maxR)));
            int diameter = radius * 2;

            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();

            return path;
        }

        // =========================
        // Original Logic Methods (UNCHANGED)
        // =========================

        private void LoadModes()
        {
            cboMode.Items.Clear();
            var modes = _groups.Select(g => g.Mode).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
            foreach (var m in modes) cboMode.Items.Add(m);

            if (cboMode.Items.Count > 0) cboMode.SelectedIndex = 0;
            else cboCategory.Items.Clear();
        }

        private void LoadCategories()
        {
            cboCategory.Items.Clear();
            var mode = cboMode.SelectedItem?.ToString() ?? "";
            var cats = _groups
                .Where(g => string.Equals(g.Mode, mode, StringComparison.OrdinalIgnoreCase))
                .Select(g => g.Category)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();

            foreach (var c in cats) cboCategory.Items.Add(c);
            if (cboCategory.Items.Count > 0) cboCategory.SelectedIndex = 0;

            UpdateStats();
        }

        private void UpdateStats()
        {
            var mode = cboMode.SelectedItem?.ToString() ?? "";
            var cat = cboCategory.SelectedItem?.ToString() ?? "";

            var list = _groups.Where(g =>
                string.Equals(g.Mode, mode, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(g.Category, cat, StringComparison.OrdinalIgnoreCase)).ToList();

            var today = DateTime.Now.Date;

            int total = list.Count;
            int done = 0, due = 0, @new = 0;

            foreach (var g in list)
            {
                if (_progressMap.TryGetValue(g.FileId, out var pr) && pr.IsDone)
                {
                    done++;
                    if (pr.IsDue(today)) due++;
                }
                else
                {
                    @new++;
                }
            }

            lblChartNewValue.Text = @new.ToString();
            lblChartDoneValue.Text = done.ToString();
            lblChartDueValue.Text = due.ToString();

            var statsText = (Label)pnlStats.Tag;
            if (statsText != null)
            {
                // text gọn giống khung info trong ảnh
                statsText.Text =
                    $"  📁  Dataset Root:\n" +
                    $"     {_cfg.DatasetRoot}\n\n" +
                    $"  📂  Mode / Category:\n" +
                    $"     {mode}  /  {cat}";
            }
        }

        private void OpenList(bool dueOnly)
        {
            var mode = cboMode.SelectedItem?.ToString() ?? "";
            var cat = cboCategory.SelectedItem?.ToString() ?? "";

            var list = _groups.Where(g =>
                string.Equals(g.Mode, mode, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(g.Category, cat, StringComparison.OrdinalIgnoreCase)).ToList();

            var f = new ListForm(list, _progressMap, _store, _sr, dueOnly);
            f.FormClosed += (_, __) => { UpdateStats(); };
            f.Show(this);
        }

        private void StartRandomInCategory()
        {
            var mode = cboMode.SelectedItem?.ToString() ?? "";
            var cat = cboCategory.SelectedItem?.ToString() ?? "";

            var list = _groups.Where(g =>
                string.Equals(g.Mode, mode, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(g.Category, cat, StringComparison.OrdinalIgnoreCase)).ToList();

            if (list.Count == 0)
            {
                MessageBox.Show("Không có đề nào trong Category này.", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var rnd = new Random();
            var gsel = list[rnd.Next(list.Count)];

            var idx = list.FindIndex(x => string.Equals(x.FileId, gsel.FileId, StringComparison.OrdinalIgnoreCase));
            if (idx < 0) idx = 0;

            var qf = new QuizForm(gsel, _progressMap, _store, _sr, list, idx);
            qf.FormClosed += (_, __) => UpdateStats();
            qf.Show(this);
        }

        private void RescanDataset()
        {
            try
            {
                Cursor = Cursors.WaitCursor;

                var scanner = new ContentScanner();
                var newGroups = scanner.ScanAll(_cfg) ?? new List<QuestionGroup>();

                _groups.Clear();
                _groups.AddRange(newGroups);

                LoadModes();
                UpdateStats();

                if (_groups.Count == 0)
                {
                    var listeningDir = System.IO.Path.Combine(_cfg.DatasetRoot ?? "", "Listening");
                    var readingDir = System.IO.Path.Combine(_cfg.DatasetRoot ?? "", "Reading");

                    var msg =
                        "Rescan xong nhưng không scan được đề nào.\n\n" +
                        $"DatasetRoot: {_cfg.DatasetRoot}\n" +
                        $"Listening folder exists: {System.IO.Directory.Exists(listeningDir)}\n" +
                        $"Reading folder exists: {System.IO.Directory.Exists(readingDir)}\n\n" +
                        "Gợi ý: kiểm tra có file *_Answer.xlsx trong từng category.\n";

                    if (scanner.LastErrors != null && scanner.LastErrors.Count > 0)
                        msg += "\nMột vài lỗi khi đọc Excel:\n" + string.Join("\n", scanner.LastErrors.Take(5));

                    MessageBox.Show(msg, "Rescan Debug", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    MessageBox.Show($"Rescan OK: {_groups.Count} questions loaded.", "Success",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Rescan error:\n" + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }
    }
}
