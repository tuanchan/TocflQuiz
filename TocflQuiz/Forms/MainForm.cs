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
        private Button btnList = new();
        private Button btnDue = new();
        private Button btnStartRandom = new();
        private Button btnCards = new();

        private TextBox txtDatasetRoot = new();
        private TextBox txtProgressFile = new();
        private Button btnBrowseDataset = new();
        private Button btnApplyConfig = new();

        private Label lblTotalDue = new();
        private Label lblChartNewValue = new();
        private Label lblChartDoneValue = new();
        private Label lblChartDueValue = new();
        private Label lblInfoText = new();

        // Professional color palette
        private static readonly Color BgPrimary = Color.FromArgb(15, 23, 42);
        private static readonly Color BgSecondary = Color.FromArgb(30, 41, 59);
        private static readonly Color BgCard = Color.FromArgb(51, 65, 85);
        private static readonly Color BgInput = Color.FromArgb(71, 85, 105);

        private static readonly Color AccentBlue = Color.FromArgb(59, 130, 246);
        private static readonly Color AccentGreen = Color.FromArgb(34, 197, 94);
        private static readonly Color AccentRed = Color.FromArgb(239, 68, 68);
        private static readonly Color AccentPurple = Color.FromArgb(168, 85, 247);
        private static readonly Color AccentYellow = Color.FromArgb(234, 179, 8);

        private static readonly Color TextPrimary = Color.FromArgb(248, 250, 252);
        private static readonly Color TextSecondary = Color.FromArgb(203, 213, 225);
        private static readonly Color TextMuted = Color.FromArgb(148, 163, 184);

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
            Width = 1400;
            Height = 950;
            MinimumSize = new Size(1400, 950);
            MaximumSize = new Size(1400, 950);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false; // ADDED: Disable maximize button
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = BgPrimary;

            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
            DoubleBuffered = true;

            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint, true);
            UpdateStyles();

            BuildUI();
            LoadModes();
            UpdateStats();
        }

        private void BuildUI()
        {
            var container = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = BgPrimary,
                Padding = new Padding(20),
                RowCount = 4,
                ColumnCount = 2
            };

            // Row heights - CHANGED: Increased spacing between sections
            container.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
            container.RowStyles.Add(new RowStyle(SizeType.Absolute, 140));
            container.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            container.RowStyles.Add(new RowStyle(SizeType.Absolute, 160)); // CHANGED: Increased from 140 to 160

            // Column widths
            container.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55F));
            container.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F));

            // HEADER
            var header = BuildHeaderSection();
            container.Controls.Add(header, 0, 0);
            container.SetColumnSpan(header, 2);

            // CONFIG
            var config = BuildConfigSection();
            container.Controls.Add(config, 0, 1);
            container.SetColumnSpan(config, 2);

            // SELECTION (Left)
            var selection = BuildSelectionSection();
            container.Controls.Add(selection, 0, 2);

            // STATS (Right)
            var stats = BuildStatsSection();
            container.Controls.Add(stats, 1, 2);

            // ACTIONS
            var actions = BuildActionsSection();
            container.Controls.Add(actions, 0, 3);
            container.SetColumnSpan(actions, 2);

            Controls.Add(container);
        }

        private Panel BuildHeaderSection()
        {
            var panel = CreateCard(new Padding(20, 14, 20, 14));
            panel.Margin = new Padding(0, 0, 0, 16);

            var title = new Label
            {
                Text = "📚  TOCFL Quiz Manager",
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = TextPrimary,
                AutoSize = true,
                Location = new Point(20, 18)
            };

            lblTotalDue = new Label
            {
                Text = "⚡ Loading...",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = AccentYellow,
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            panel.Controls.Add(title);
            panel.Controls.Add(lblTotalDue);

            panel.Resize += (s, e) =>
            {
                lblTotalDue.Location = new Point(panel.Width - lblTotalDue.Width - 20, 20);
            };

            return panel;
        }

        private Panel BuildConfigSection()
        {
            var panel = CreateCard(new Padding(20, 16, 20, 16));
            panel.Margin = new Padding(0, 0, 0, 16);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 2,
                BackColor = Color.Transparent
            };

            // CHANGED: Increased button column width from 180 to 220
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));

            // CHANGED: Increased row height for taller buttons
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));

            // Row 1
            var lblDataset = CreateLabel("Dataset Root:", 9.5F);
            txtDatasetRoot = CreateTextBox(_cfg.DatasetRoot ?? "", false);
            btnBrowseDataset = CreateConfigButton("📁 Browse", AccentBlue);
            btnBrowseDataset.Click += BtnBrowseDataset_Click;

            layout.Controls.Add(lblDataset, 0, 0);
            layout.Controls.Add(txtDatasetRoot, 1, 0);
            layout.Controls.Add(btnBrowseDataset, 2, 0);

            // Row 2
            var lblProgress = CreateLabel("Progress File:", 9.5F);
            txtProgressFile = CreateTextBox(_cfg.ProgressFilePath ?? "", true);
            btnApplyConfig = CreateConfigButton("✓ Apply & Rescan", AccentGreen);
            btnApplyConfig.Click += BtnApplyConfig_Click;

            layout.Controls.Add(lblProgress, 0, 1);
            layout.Controls.Add(txtProgressFile, 1, 1);
            layout.Controls.Add(btnApplyConfig, 2, 1);

            panel.Controls.Add(layout);
            return panel;
        }

        private Panel BuildSelectionSection()
        {
            var panel = CreateCard(new Padding(20, 18, 20, 18));
            panel.Margin = new Padding(0, 0, 14, 18); // CHANGED: Added bottom margin of 18

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3,
                BackColor = Color.Transparent
            };

            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            // CHANGED: Reduced row heights for more compact info panel
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            // Mode
            var lblMode = CreateLabel("Mode:", 10F, FontStyle.Bold);
            cboMode = CreateComboBox();
            cboMode.SelectedIndexChanged += (s, e) => LoadCategories();

            layout.Controls.Add(lblMode, 0, 0);
            layout.Controls.Add(cboMode, 1, 0);

            // Category
            var lblCategory = CreateLabel("Category:", 10F, FontStyle.Bold);
            cboCategory = CreateComboBox();
            cboCategory.SelectedIndexChanged += (s, e) => UpdateStats();

            layout.Controls.Add(lblCategory, 0, 1);
            layout.Controls.Add(cboCategory, 1, 1);

            // Info Panel
            var infoPanel = CreateInfoPanel();
            layout.Controls.Add(infoPanel, 0, 2);
            layout.SetColumnSpan(infoPanel, 2);

            panel.Controls.Add(layout);
            return panel;
        }

        private Panel CreateInfoPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BgInput,
                Padding = new Padding(14), // CHANGED: Reduced padding from 18 to 14
                Margin = new Padding(0, 10, 0, 0) // CHANGED: Reduced top margin from 14 to 10
            };

            lblInfoText = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F), // CHANGED: Reduced from 9.5F to 9F
                ForeColor = TextSecondary,
                AutoSize = false,
                Text = "Select mode and category to view statistics..."
            };

            panel.Controls.Add(lblInfoText);
            return panel;
        }

        private Panel BuildStatsSection()
        {
            var panel = CreateCard(new Padding(20, 18, 20, 18));
            panel.Margin = new Padding(14, 0, 0, 18); // CHANGED: Added bottom margin of 18

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                BackColor = Color.Transparent
            };

            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 33.34F));

            var cardNew = CreateStatCard("NEW", "📝", AccentBlue, out lblChartNewValue);
            var cardDone = CreateStatCard("DONE", "✓", AccentGreen, out lblChartDoneValue);
            var cardDue = CreateStatCard("DUE", "⏰", AccentRed, out lblChartDueValue);

            cardNew.Margin = new Padding(0, 0, 0, 14);
            cardDone.Margin = new Padding(0, 14, 0, 14);
            cardDue.Margin = new Padding(0, 14, 0, 0);

            layout.Controls.Add(cardNew, 0, 0);
            layout.Controls.Add(cardDone, 0, 1);
            layout.Controls.Add(cardDue, 0, 2);

            panel.Controls.Add(layout);
            return panel;
        }

        private Panel CreateStatCard(string label, string icon, Color accentColor, out Label valueLabel)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BgInput,
                Padding = new Padding(18, 16, 18, 16)
            };

            var lblLabel = new Label
            {
                Text = label,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = TextMuted,
                AutoSize = true,
                Location = new Point(18, 18)
            };

            var lblIcon = new Label
            {
                Text = icon,
                Font = new Font("Segoe UI", 16F),
                ForeColor = accentColor,
                AutoSize = true
            };

            var lblValue = new Label
            {
                Text = "0",
                Font = new Font("Segoe UI", 36F, FontStyle.Bold),
                ForeColor = accentColor,
                AutoSize = true,
                Location = new Point(18, 52)
            };

            panel.Controls.Add(lblLabel);
            panel.Controls.Add(lblIcon);
            panel.Controls.Add(lblValue);

            panel.Resize += (s, e) =>
            {
                lblIcon.Location = new Point(panel.Width - lblIcon.Width - 18, 16);
            };

            valueLabel = lblValue;
            return panel;
        }

        private Panel BuildActionsSection()
        {
            var panel = CreateCard(new Padding(20, 18, 20, 18));
            panel.Margin = new Padding(0);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                BackColor = Color.Transparent
            };

            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));

            btnList = CreateActionButton("List View", "View all questions", AccentBlue, "📋");
            btnList.Click += (s, e) => OpenList(false);
            btnList.Margin = new Padding(0, 0, 12, 0);

            btnDue = CreateActionButton("Review Due", "Practice due items", AccentRed, "⏰");
            btnDue.Click += (s, e) => OpenList(true);
            btnDue.Margin = new Padding(12, 0, 12, 0);

            btnStartRandom = CreateActionButton("Random Quiz", "Start random test", AccentGreen, "🎲");
            btnStartRandom.Click += (s, e) => StartRandomInCategory();
            btnStartRandom.Margin = new Padding(12, 0, 12, 0);

            btnCards = CreateActionButton("Flashcards", "Study with cards", AccentPurple, "🗂️");
            btnCards.Click += (s, e) =>
            {
                var f = new CardForm(_cfg, _groups, _progressMap, _store, _sr);
                f.Show(this);
            };
            btnCards.Margin = new Padding(12, 0, 0, 0);

            layout.Controls.Add(btnList, 0, 0);
            layout.Controls.Add(btnDue, 1, 0);
            layout.Controls.Add(btnStartRandom, 2, 0);
            layout.Controls.Add(btnCards, 3, 0);

            panel.Controls.Add(layout);
            return panel;
        }

        // ===== UI HELPERS =====
        private Panel CreateCard(Padding padding)
        {
            var panel = new Panel
            {
                BackColor = BgCard,
                Padding = padding,
                Dock = DockStyle.Fill
            };
            return panel;
        }

        private Label CreateLabel(string text, float fontSize, FontStyle style = FontStyle.Regular)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Segoe UI", fontSize, style),
                ForeColor = TextSecondary,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(0, 0, 10, 0)
            };
        }

        private TextBox CreateTextBox(string text, bool readOnly)
        {
            var txt = new TextBox
            {
                Text = text,
                ReadOnly = readOnly,
                Font = new Font("Segoe UI", 9.5F),
                BackColor = BgInput,
                ForeColor = readOnly ? TextMuted : TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 8, 10, 8)
            };
            return txt;
        }

        private ComboBox CreateComboBox()
        {
            var cbo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10F),
                BackColor = BgInput,
                ForeColor = TextPrimary,
                FlatStyle = FlatStyle.Flat,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 8, 0, 8)
            };
            return cbo;
        }

        private Button CreateConfigButton(string text, Color color)
        {
            var btn = new Button
            {
                Text = text,
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                BackColor = color,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Dock = DockStyle.Fill,
                Margin = new Padding(20, 4, 0, 4) // CHANGED: Reduced vertical margin for taller button
            };
            btn.FlatAppearance.BorderSize = 0;

            var originalColor = color;
            btn.MouseEnter += (s, e) => btn.BackColor = ControlPaint.Light(originalColor, 0.2f);
            btn.MouseLeave += (s, e) => btn.BackColor = originalColor;

            return btn;
        }

        private Button CreateActionButton(string title, string subtitle, Color color, string icon)
        {
            var btn = new Button
            {
                Text = "",
                BackColor = BgCard,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Dock = DockStyle.Fill,
                Tag = new { Title = title, Subtitle = subtitle, Icon = icon, Color = color }
            };
            btn.FlatAppearance.BorderSize = 3;
            btn.FlatAppearance.BorderColor = color;

            btn.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

                dynamic data = btn.Tag;

                using (var glowPen = new Pen(Color.FromArgb(100, data.Color), 8))
                {
                    e.Graphics.DrawRectangle(glowPen, 4, 4, btn.Width - 8, btn.Height - 8);
                }

                using (var glowPen2 = new Pen(Color.FromArgb(60, data.Color), 4))
                {
                    e.Graphics.DrawRectangle(glowPen2, 2, 2, btn.Width - 4, btn.Height - 4);
                }

                using var iconFont = new Font("Segoe UI", 32F);
                using var titleFont = new Font("Segoe UI", 12F, FontStyle.Bold);
                using var subFont = new Font("Segoe UI", 8.5F);

                var iconSize = e.Graphics.MeasureString(data.Icon, iconFont);
                var titleSize = e.Graphics.MeasureString(data.Title, titleFont);
                var subSize = e.Graphics.MeasureString(data.Subtitle, subFont);

                float iconY = 18;
                float titleY = iconY + iconSize.Height + 4;
                float subY = titleY + titleSize.Height + 3;

                using var iconBrush = new SolidBrush(data.Color);
                e.Graphics.DrawString(data.Icon, iconFont, iconBrush,
                    (btn.Width - iconSize.Width) / 2, iconY);

                e.Graphics.DrawString(data.Title, titleFont, Brushes.White,
                    (btn.Width - titleSize.Width) / 2, titleY);

                using var subBrush = new SolidBrush(TextMuted);
                e.Graphics.DrawString(data.Subtitle, subFont, subBrush,
                    (btn.Width - subSize.Width) / 2, subY);
            };

            var originalBg = BgCard;
            btn.MouseEnter += (s, e) =>
            {
                btn.BackColor = ControlPaint.Light(originalBg, 0.15f);
                dynamic data = btn.Tag;
                btn.FlatAppearance.BorderColor = ControlPaint.Light(data.Color, 0.3f);
                btn.Invalidate();
            };
            btn.MouseLeave += (s, e) =>
            {
                btn.BackColor = originalBg;
                dynamic data = btn.Tag;
                btn.FlatAppearance.BorderColor = data.Color;
                btn.Invalidate();
            };

            return btn;
        }

        // ===== EVENT HANDLERS =====
        private void BtnBrowseDataset_Click(object sender, EventArgs e)
        {
            using var dlg = new FolderBrowserDialog();
            dlg.Description = "Select Dataset Root Folder";
            if (dlg.ShowDialog() == DialogResult.OK)
                txtDatasetRoot.Text = dlg.SelectedPath;
        }

        private void BtnApplyConfig_Click(object sender, EventArgs e)
        {
            _cfg.DatasetRoot = (txtDatasetRoot.Text ?? "").Trim();
            SettingsService.Save(new AppSettings { DatasetRoot = _cfg.DatasetRoot });
            MessageBox.Show("Configuration saved successfully!", "Success",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            UpdateStats();
            RescanDataset();
        }

        // ===== LOGIC METHODS =====
        private void LoadModes()
        {
            cboMode.Items.Clear();
            var modes = _groups.Select(g => g.Mode)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();

            foreach (var m in modes)
                cboMode.Items.Add(m);

            if (cboMode.Items.Count > 0)
                cboMode.SelectedIndex = 0;
            else
                cboCategory.Items.Clear();
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

            foreach (var c in cats)
                cboCategory.Items.Add(c);

            if (cboCategory.Items.Count > 0)
                cboCategory.SelectedIndex = 0;

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

            int newCount = 0, doneCount = 0, dueCount = 0;

            foreach (var g in list)
            {
                if (_progressMap.TryGetValue(g.FileId, out var pr) && pr.IsDone)
                {
                    doneCount++;
                    if (pr.IsDue(today)) dueCount++;
                }
                else
                {
                    newCount++;
                }
            }

            lblChartNewValue.Text = newCount.ToString();
            lblChartDoneValue.Text = doneCount.ToString();
            lblChartDueValue.Text = dueCount.ToString();

            lblInfoText.Text =
                $"📁 Dataset: {_cfg.DatasetRoot}\n\n" +
                $"📂 Mode: {mode}\n" +
                $"📑 Category: {cat}\n\n" +
                $"📊 Total: {list.Count} questions";

            int totalDue = 0;
            foreach (var g in _groups)
            {
                if (_progressMap.TryGetValue(g.FileId, out var pr) && pr.IsDone && pr.IsDue(today))
                    totalDue++;
            }

            lblTotalDue.Text = $"⚡ Total Due: {totalDue} questions";

            if (lblTotalDue.Parent != null)
            {
                lblTotalDue.Location = new Point(
                    lblTotalDue.Parent.Width - lblTotalDue.Width - 20,
                    20
                );
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
            f.FormClosed += (s, e) => UpdateStats();
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
                MessageBox.Show("No questions found in this category.", "Info",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var rnd = new Random();
            var gsel = list[rnd.Next(list.Count)];

            var idx = list.FindIndex(x => string.Equals(x.FileId, gsel.FileId, StringComparison.OrdinalIgnoreCase));
            if (idx < 0) idx = 0;

            var qf = new QuizForm(gsel, _progressMap, _store, _sr, list, idx);
            qf.FormClosed += (s, e) => UpdateStats();
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
                        "Rescan completed but no questions found.\n\n" +
                        $"Dataset Root: {_cfg.DatasetRoot}\n" +
                        $"Listening folder exists: {System.IO.Directory.Exists(listeningDir)}\n" +
                        $"Reading folder exists: {System.IO.Directory.Exists(readingDir)}\n\n" +
                        "Tip: Check for *_Answer.xlsx files in each category folder.";

                    if (scanner.LastErrors != null && scanner.LastErrors.Count > 0)
                        msg += "\n\nSome Excel read errors:\n" + string.Join("\n", scanner.LastErrors.Take(5));

                    MessageBox.Show(msg, "Rescan Debug", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    MessageBox.Show($"Rescan successful: {_groups.Count} questions loaded.", "Success",
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