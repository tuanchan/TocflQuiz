using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
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
        private Label lblStats = new();
        private Button btnList = new();
        private Button btnDue = new();
        private Button btnStartRandom = new();


        private Button btnCards = new();
        // ===================== [CFG-UI] Controls chỉnh đường dẫn =====================
        private TextBox txtDatasetRoot = new();
        private TextBox txtProgressFile = new();
        private Button btnBrowseDataset = new();
        private Button btnBrowseProgress = new();
        private Button btnApplyConfig = new();

        // [CFG-UI] NEW: Rescan button
        private Button btnRescan = new();
        // ============================================================================

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

            Text = "TOCFL Quiz (Local) by TuanChandzx";
            Width = 1000;
            Height = 800;
            MinimumSize = new System.Drawing.Size(700, 450);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new System.Drawing.Font("Segoe UI", 9F);

            BuildUi();
            LoadModes();
            UpdateStats();
        }

        private void BuildUi()
        {
            var mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20),
                BackColor = System.Drawing.Color.FromArgb(245, 245, 245)
            };

            var contentPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = System.Drawing.Color.White,
                Padding = new Padding(24)
            };

            // Row 0: Config
            contentPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 180));
            // Row 1: Selection
            contentPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 180));
            // Row 2: Actions
            contentPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 160));
            // Row 3: Stats
            contentPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // ===================== [CFG-UI] Section 0: Config =====================
            var cfgGroup = BuildConfigSection();
            contentPanel.Controls.Add(cfgGroup, 0, 0);
            // =====================================================================

            // Section 1: Selection
            var selectionGroup = BuildSelectionSection();
            contentPanel.Controls.Add(selectionGroup, 0, 1);

            // Section 2: Actions
            var actionsGroup = BuildActionsSection();
            contentPanel.Controls.Add(actionsGroup, 0, 2);

            // Section 3: Statistics
            var statsGroup = BuildStatsSection();
            contentPanel.Controls.Add(statsGroup, 0, 3);

            mainPanel.Controls.Add(contentPanel);
            Controls.Add(mainPanel);
        }

        // ===================== [CFG-UI] GroupBox chỉnh đường dẫn =====================
        private GroupBox BuildConfigSection()
        {
            var group = new GroupBox
            {
                Text = "  Cấu hình đường dẫn (DatasetRoot)  ",
                Dock = DockStyle.Fill,
                Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold),
                Padding = new Padding(12, 8, 12, 12),
                ForeColor = System.Drawing.Color.FromArgb(50, 50, 50)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 3,
                Padding = new Padding(8)
            };

            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

            // Row 0: DatasetRoot
            var lblDataset = new Label
            {
                Text = "Dataset Root:",
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Font = new System.Drawing.Font("Segoe UI", 9.5F),
                ForeColor = System.Drawing.Color.FromArgb(70, 70, 70)
            };

            txtDatasetRoot.Dock = DockStyle.Fill;
            txtDatasetRoot.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            txtDatasetRoot.Text = _cfg.DatasetRoot ?? "";

            btnBrowseDataset.Text = "Browse...";
            btnBrowseDataset.Dock = DockStyle.Fill;
            btnBrowseDataset.Font = new System.Drawing.Font("Segoe UI", 9F);
            btnBrowseDataset.Click += (_, __) =>
            {
                using var dlg = new FolderBrowserDialog();
                dlg.Description = "Chọn thư mục DatasetRoot";
                if (dlg.ShowDialog() == DialogResult.OK)
                    txtDatasetRoot.Text = dlg.SelectedPath;
            };

            layout.Controls.Add(lblDataset, 0, 0);
            layout.Controls.Add(txtDatasetRoot, 1, 0);
            layout.Controls.Add(btnBrowseDataset, 2, 0);

            // Row 1: ProgressFilePath (read-only -> chỉ hiển thị để bạn biết đang lưu ở đâu)
            var lblProg = new Label
            {
                Text = "Progress File:",
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Font = new System.Drawing.Font("Segoe UI", 9.5F),
                ForeColor = System.Drawing.Color.FromArgb(70, 70, 70)
            };

            txtProgressFile.Dock = DockStyle.Fill;
            txtProgressFile.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            txtProgressFile.Text = _cfg.ProgressFilePath ?? "";
            txtProgressFile.ReadOnly = true;

            btnBrowseProgress.Text = "Browse...";
            btnBrowseProgress.Dock = DockStyle.Fill;
            btnBrowseProgress.Font = new System.Drawing.Font("Segoe UI", 9F);
            btnBrowseProgress.Enabled = false; // AppConfig.ProgressFilePath đang read-only nên disable để tránh hiểu nhầm

            layout.Controls.Add(lblProg, 0, 1);
            layout.Controls.Add(txtProgressFile, 1, 1);
            layout.Controls.Add(btnBrowseProgress, 2, 1);

            // Row 2: Apply
            btnApplyConfig.Text = "✔ Áp dụng";
            btnApplyConfig.Width = 160;
            btnApplyConfig.Height = 36;
            btnApplyConfig.Anchor = AnchorStyles.Left;
            btnApplyConfig.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold);
            btnApplyConfig.BackColor = System.Drawing.Color.FromArgb(60, 170, 100);
            btnApplyConfig.ForeColor = System.Drawing.Color.White;
            btnApplyConfig.FlatStyle = FlatStyle.Flat;
            btnApplyConfig.FlatAppearance.BorderSize = 0;
            btnApplyConfig.Cursor = Cursors.Hand;

            btnApplyConfig.Click += (_, __) =>
            {
                // ===================== [SETTINGS-SAVE] Lưu DatasetRoot vào settings.json =====================
                _cfg.DatasetRoot = (txtDatasetRoot.Text ?? "").Trim();

                SettingsService.Save(new AppSettings
                {
                    DatasetRoot = _cfg.DatasetRoot
                });

                MessageBox.Show(
                    "Đã lưu DatasetRoot vào settings.json (LocalAppData).",
                    "OK",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                // =============================================================================================

                UpdateStats();
                RescanDataset();

            };

            layout.Controls.Add(btnApplyConfig, 1, 2);


            // ===================== [CFG-UI] NEW: Rescan ngay trong app =====================
            btnRescan.Text = "🔄 Rescan";
            btnRescan.Width = 140;
            btnRescan.Height = 36;
            btnRescan.Anchor = AnchorStyles.Left;
            btnRescan.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold);
            btnRescan.BackColor = System.Drawing.Color.FromArgb(70, 130, 180);
            btnRescan.ForeColor = System.Drawing.Color.White;
            btnRescan.FlatStyle = FlatStyle.Flat;
            btnRescan.FlatAppearance.BorderSize = 0;
            btnRescan.Cursor = Cursors.Hand;
            btnRescan.Click += (_, __) => RescanDataset();

            // đặt cùng hàng với nút Apply (cột 2)
            layout.Controls.Add(btnRescan, 2, 2);
            // ============================================================================ 


            group.Controls.Add(layout);
            return group;
        }
        // ============================================================================

        private GroupBox BuildSelectionSection()
        {
            var group = new GroupBox
            {
                Text = "  Chọn Mode và Category  ",
                Dock = DockStyle.Fill,
                Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold),
                Padding = new Padding(12, 8, 12, 12),
                ForeColor = System.Drawing.Color.FromArgb(50, 50, 50)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Padding = new Padding(8)
            };

            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

            // Mode
            var lblMode = new Label
            {
                Text = "Mode:",
                AutoSize = false,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill,
                Font = new System.Drawing.Font("Segoe UI", 9.5F),
                ForeColor = System.Drawing.Color.FromArgb(70, 70, 70)
            };

            cboMode.Dock = DockStyle.Fill;
            cboMode.DropDownStyle = ComboBoxStyle.DropDownList;
            cboMode.Font = new System.Drawing.Font("Segoe UI", 10F);
            cboMode.Margin = new Padding(0, 6, 0, 6);
            cboMode.SelectedIndexChanged += (_, __) => LoadCategories();

            // Category
            var lblCategory = new Label
            {
                Text = "Category:",
                AutoSize = false,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill,
                Font = new System.Drawing.Font("Segoe UI", 9.5F),
                ForeColor = System.Drawing.Color.FromArgb(70, 70, 70)
            };

            cboCategory.Dock = DockStyle.Fill;
            cboCategory.DropDownStyle = ComboBoxStyle.DropDownList;
            cboCategory.Font = new System.Drawing.Font("Segoe UI", 10F);
            cboCategory.Margin = new Padding(0, 6, 0, 6);
            cboCategory.SelectedIndexChanged += (_, __) => UpdateStats();

            layout.Controls.Add(lblMode, 0, 0);
            layout.Controls.Add(cboMode, 1, 0);
            layout.Controls.Add(lblCategory, 0, 1);
            layout.Controls.Add(cboCategory, 1, 1);

            group.Controls.Add(layout);
            return group;
        }

        private GroupBox BuildActionsSection()
        {
            var group = new GroupBox
            {
                Text = "  Hành động  ",
                Dock = DockStyle.Fill,
                Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold),
                // Giảm padding trái/phải để nút rộng hơn
                Padding = new Padding(4, 8, 4, 8),
                ForeColor = System.Drawing.Color.FromArgb(50, 50, 50)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                // Bỏ padding trong layout để nút "full" bề ngang
                Padding = new Padding(0),
                Margin = new Padding(0)
            };

            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));

            // Button styles
            var buttonFont = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular);
            var buttonHeight = 36;

            btnList.Text = "📋 Danh sách (New/Done/Due)";
            btnList.Dock = DockStyle.Fill;
            btnList.Font = buttonFont;
            btnList.Height = buttonHeight;
            btnList.Margin = new Padding(0, 1, 0, 1);
            btnList.BackColor = System.Drawing.Color.FromArgb(70, 130, 180);
            btnList.ForeColor = System.Drawing.Color.White;
            btnList.FlatStyle = FlatStyle.Flat;
            btnList.FlatAppearance.BorderSize = 0;
            btnList.Cursor = Cursors.Hand;
            btnList.Click += (_, __) => OpenList(false);

            btnDue.Text = "⏰ Ôn đến hạn (Due)";
            btnDue.Dock = DockStyle.Fill;
            btnDue.Font = buttonFont;
            btnDue.Height = buttonHeight;
            btnDue.Margin = new Padding(0, 1, 0, 1);
            btnDue.BackColor = System.Drawing.Color.FromArgb(220, 100, 50);
            btnDue.ForeColor = System.Drawing.Color.White;
            btnDue.FlatStyle = FlatStyle.Flat;
            btnDue.FlatAppearance.BorderSize = 0;
            btnDue.Cursor = Cursors.Hand;
            btnDue.Click += (_, __) => OpenList(true);

            btnStartRandom.Text = "🎲 Làm ngẫu nhiên 1 đề trong Category";
            btnStartRandom.Dock = DockStyle.Fill;
            btnStartRandom.Font = buttonFont;
            btnStartRandom.Height = buttonHeight;
            btnStartRandom.Margin = new Padding(0, 1, 0, 1);
            btnStartRandom.BackColor = System.Drawing.Color.FromArgb(60, 170, 100);
            btnStartRandom.ForeColor = System.Drawing.Color.White;
            btnStartRandom.FlatStyle = FlatStyle.Flat;
            btnStartRandom.FlatAppearance.BorderSize = 0;
            btnStartRandom.Cursor = Cursors.Hand;
            btnStartRandom.Click += (_, __) => StartRandomInCategory();

            // Nút Flashcards (Card)
            btnCards.Text = "🃏 Flashcards (Card)";
            btnCards.Dock = DockStyle.Fill;
            btnCards.Font = buttonFont;
            btnCards.Height = buttonHeight;
            btnCards.Margin = new Padding(0, 1, 0, 1);
            btnCards.BackColor = System.Drawing.Color.FromArgb(120, 90, 160);
            btnCards.ForeColor = System.Drawing.Color.White;
            btnCards.FlatStyle = FlatStyle.Flat;
            btnCards.FlatAppearance.BorderSize = 0;
            btnCards.Cursor = Cursors.Hand;

            // Tạm thời test nút hoạt động (chưa mở CardForm)
            btnCards.Click += (_, __) =>
            {
                var f = new CardForm();
                f.Show(this);
            };

            layout.Controls.Add(btnList, 0, 0);
            layout.Controls.Add(btnDue, 0, 1);
            layout.Controls.Add(btnStartRandom, 0, 2);
            layout.Controls.Add(btnCards, 0, 3);

            group.Controls.Add(layout);
            return group;

        }

        private GroupBox BuildStatsSection()
        {
            var group = new GroupBox
            {
                Text = "  Thống kê  ",
                Dock = DockStyle.Fill,
                Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold),
                Padding = new Padding(12, 8, 12, 12),
                ForeColor = System.Drawing.Color.FromArgb(50, 50, 50)
            };

            lblStats.Dock = DockStyle.Fill;
            lblStats.AutoSize = false;
            lblStats.TextAlign = System.Drawing.ContentAlignment.TopLeft;
            lblStats.BackColor = System.Drawing.Color.FromArgb(250, 250, 250);
            lblStats.BorderStyle = BorderStyle.FixedSingle;
            lblStats.Padding = new Padding(12);
            lblStats.Font = new System.Drawing.Font("Consolas", 9F);
            lblStats.ForeColor = System.Drawing.Color.FromArgb(60, 60, 60);

            group.Controls.Add(lblStats);

            return group;
        }

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

            lblStats.Text =
                $"📁 Dataset Root:\n   {_cfg.DatasetRoot}\n\n" +
                $"📂 Mode / Category:\n   {mode} / {cat}\n\n" +
                $"📊 Thống kê:\n" +
                $"   • Tổng số đề: {total}\n" +
                $"   • Đề mới (New): {@new}\n" +
                $"   • Đã làm (Done): {done}\n" +
                $"   • Đến hạn ôn (Due): {due}\n\n" +
                $"💾 Progress File:\n   {_cfg.ProgressFilePath}";
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

            // ===== FIX: truyền đúng danh sách + index để QuizForm có thể "Bài trước/Bài tiếp" =====
            var idx = list.FindIndex(x => string.Equals(x.FileId, gsel.FileId, StringComparison.OrdinalIgnoreCase));
            if (idx < 0) idx = 0;

            var qf = new QuizForm(gsel, _progressMap, _store, _sr, list, idx);
            // ====================================================================================

            qf.FormClosed += (_, __) => UpdateStats();
            qf.Show(this);
        }
        // ===================== [RESCAN] Quét lại dataset và refresh combobox =====================
        private void RescanDataset()
        {
            try
            {
                Cursor = Cursors.WaitCursor;

                var scanner = new ContentScanner();
                var newGroups = scanner.ScanAll(_cfg) ?? new List<QuestionGroup>();

                // cập nhật list _groups (giữ reference cũ để không ảnh hưởng chỗ khác)
                _groups.Clear();
                _groups.AddRange(newGroups);

                // refresh UI theo dataset mới
                LoadModes();
                UpdateStats();

                if (_groups.Count == 0)
                {
                    var listeningDir = Path.Combine(_cfg.DatasetRoot ?? "", "Listening");
                    var readingDir = Path.Combine(_cfg.DatasetRoot ?? "", "Reading");

                    var msg =
                        "Rescan xong nhưng không scan được đề nào.\n\n" +
                        $"DatasetRoot: {_cfg.DatasetRoot}\n" +
                        $"Listening folder exists: {Directory.Exists(listeningDir)}\n" +
                        $"Reading folder exists: {Directory.Exists(readingDir)}\n\n" +
                        "Gợi ý: kiểm tra có file *_Answer.xlsx trong từng category.\n";

                    if (scanner.LastErrors != null && scanner.LastErrors.Count > 0)
                        msg += "\nMột vài lỗi khi đọc Excel:\n" + string.Join("\n", scanner.LastErrors.Take(5));

                    MessageBox.Show(msg, "Rescan Debug", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    MessageBox.Show($"Rescan OK: {_groups.Count} Câu.", "OK",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Rescan lỗi:\n" + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }
        // =========================================================================================

    }
}
