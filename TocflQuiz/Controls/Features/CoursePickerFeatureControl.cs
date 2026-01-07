using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using TocflQuiz.Models;
using TocflQuiz.Services;

namespace TocflQuiz.Controls.Features
{
    public sealed partial class CoursePickerFeatureControl : UserControl
    {
        // events -> CardForm sẽ nghe để chuyển view
        public event Action<string>? FeatureRequested;
        public event Action<CardSet?>? SelectedSetChanged;

        private readonly FlowLayoutPanel _grid = new();
        private readonly TextBox _txtSearch = new();
        private readonly Label _lblInfo = new();

        private readonly Dictionary<Button, CardSet> _map = new();
        private Button? _selectedBtn;

        public CardSet? SelectedSet { get; private set; }

        // Quizlet color palette
        private static readonly Color QuizletBlue = Color.FromArgb(76, 146, 245);
        private static readonly Color QuizletBlueDark = Color.FromArgb(46, 116, 215);
        private static readonly Color QuizletBlueLight = Color.FromArgb(240, 245, 255);
        private static readonly Color QuizletPrimary = Color.FromArgb(74, 85, 104);
        private static readonly Color QuizletBorder = Color.FromArgb(226, 232, 240);
        private static readonly Color QuizletBackground = Color.FromArgb(247, 250, 252);

        // Dark mode colors
        private static readonly Color DarkBackground = Color.FromArgb(30, 30, 40);
        private static readonly Color DarkSurface = Color.FromArgb(40, 40, 50);
        private static readonly Color DarkBorder = Color.FromArgb(60, 60, 70);
        private static readonly Color DarkText = Color.FromArgb(220, 220, 230);
        private static readonly Color DarkTextSecondary = Color.FromArgb(160, 160, 170);

        private bool _isDarkMode = false;

        public CoursePickerFeatureControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;

            BuildUi();
            Reload();
        }

        public void SetDarkMode(bool isDark)
        {
            _isDarkMode = isDark;
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            if (_isDarkMode)
            {
                BackColor = DarkSurface;
                _grid.BackColor = DarkSurface;
                _txtSearch.BackColor = DarkBackground;
                _txtSearch.ForeColor = DarkText;
                _txtSearch.BorderStyle = BorderStyle.FixedSingle;
                _lblInfo.ForeColor = DarkTextSecondary;
            }
            else
            {
                BackColor = Color.White;
                _grid.BackColor = Color.White;
                _txtSearch.BackColor = Color.White;
                _txtSearch.ForeColor = QuizletPrimary;
                _txtSearch.BorderStyle = BorderStyle.FixedSingle;
                _lblInfo.ForeColor = Color.FromArgb(100, 100, 100);
            }

            // Refresh all tiles
            foreach (var kvp in _map)
            {
                ApplyTileTheme(kvp.Key, kvp.Key == _selectedBtn);
            }

            // Refresh feature buttons
            foreach (Control ctrl in Controls)
            {
                RefreshControlTheme(ctrl);
            }
        }

        private void RefreshControlTheme(Control ctrl)
        {
            if (ctrl is TableLayoutPanel tlp)
            {
                tlp.BackColor = _isDarkMode ? DarkSurface : Color.White;
                foreach (Control child in tlp.Controls)
                {
                    RefreshControlTheme(child);
                }
            }
            else if (ctrl is Panel panel)
            {
                foreach (Control child in panel.Controls)
                {
                    if (child is Button btn && btn.Tag is string)
                    {
                        // Feature button
                        if (_isDarkMode)
                        {
                            btn.BackColor = DarkBackground;
                            btn.ForeColor = DarkText;
                            btn.FlatAppearance.BorderColor = DarkBorder;
                            panel.BackColor = Color.Transparent;
                        }
                        else
                        {
                            btn.BackColor = Color.White;
                            btn.ForeColor = QuizletPrimary;
                            btn.FlatAppearance.BorderColor = QuizletBorder;
                            panel.BackColor = Color.Transparent;
                        }
                    }
                }
            }
        }

        public void Reload()
        {
            var sets = CardSetStorage.LoadAllSetsSafe().ToList();

            var q = (_txtSearch.Text ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(q))
            {
                sets = sets
                    .Where(s => (s.Title ?? "").Contains(q, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            _lblInfo.Text = $"Có {sets.Count} học phần";

            _grid.SuspendLayout();
            _grid.Controls.Clear();
            _map.Clear();
            _selectedBtn = null;
            SelectedSet = null;
            SelectedSetChanged?.Invoke(null);

            foreach (var s in sets)
            {
                var btn = MakeTileButton(s);
                _map[btn] = s;
                _grid.Controls.Add(btn);
            }

            _grid.ResumeLayout(true);
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(16),
                ColumnCount = 1,
                RowCount = 3
            };

            // Toolbar (2 hàng) - tăng height để chứa đủ text
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 170));
            // Grid
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            // Bottom search
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));

            var bar = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 2,
                Margin = new Padding(0, 0, 0, 8)
            };
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));
            bar.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            bar.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

            bar.Controls.Add(MakePill("🧠 Thẻ ghi nhớ", CardFeatureKeys.Flashcards), 0, 0);
            bar.Controls.Add(MakePill("📚 Học phần", CardFeatureKeys.Course), 1, 0);
            bar.Controls.Add(MakePill("📝 Kiểm tra", CardFeatureKeys.Quiz), 2, 0);
            bar.Controls.Add(MakePill("🧩 Blocks", CardFeatureKeys.Blocks), 0, 1);
            bar.Controls.Add(MakePill("🚀 Blast", CardFeatureKeys.Blast), 1, 1);
            bar.Controls.Add(MakePill("🧷 Ghép thẻ", CardFeatureKeys.MergeCards), 2, 1);

            // Grid
            _grid.Dock = DockStyle.Fill;
            _grid.AutoScroll = true;
            _grid.WrapContents = true;
            _grid.FlowDirection = FlowDirection.LeftToRight;
            _grid.Padding = new Padding(4);
            _grid.BackColor = Color.White;

            // Bottom search row
            var bottom = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0, 8, 0, 0)
            };
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300)); // giảm từ 420 xuống 300
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            _txtSearch.Dock = DockStyle.Fill;
            _txtSearch.Margin = new Padding(0, 10, 10, 10);
            _txtSearch.PlaceholderText = "Tìm kiếm...";
            _txtSearch.Font = new Font("Segoe UI", 10F);
            _txtSearch.BorderStyle = BorderStyle.FixedSingle;
            _txtSearch.TextChanged += (_, __) => Reload();

            _lblInfo.Dock = DockStyle.Fill;
            _lblInfo.TextAlign = ContentAlignment.MiddleLeft;
            _lblInfo.Font = new Font("Segoe UI", 9.5F);
            _lblInfo.ForeColor = Color.FromArgb(100, 100, 100);

            bottom.Controls.Add(_txtSearch, 0, 0);
            bottom.Controls.Add(_lblInfo, 1, 0);

            root.Controls.Add(bar, 0, 0);
            root.Controls.Add(_grid, 0, 1);
            root.Controls.Add(bottom, 0, 2);

            Controls.Add(root);
        }

        private Control MakePill(string text, string featureKey)
        {
            var p = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(5), // giảm margin
                BackColor = Color.Transparent,
            };

            var btn = new Button
            {
                Dock = DockStyle.Fill,
                Text = text,
                Font = new Font("Segoe UI", 11.5F, FontStyle.Bold), // tăng font lên 11.5
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = QuizletPrimary,
                Cursor = Cursors.Hand,
                Tag = featureKey,
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = new Padding(10, 6, 10, 6), // tăng padding
                AutoSize = false
            };

            btn.FlatAppearance.BorderSize = 2;
            btn.FlatAppearance.BorderColor = QuizletBorder;

            // Hover effects
            btn.MouseEnter += (s, e) =>
            {
                if (_isDarkMode)
                {
                    btn.BackColor = Color.FromArgb(50, 50, 60);
                    btn.ForeColor = QuizletBlue;
                    btn.FlatAppearance.BorderColor = QuizletBlue;
                }
                else
                {
                    btn.BackColor = QuizletBlueLight;
                    btn.ForeColor = QuizletBlue;
                    btn.FlatAppearance.BorderColor = QuizletBlue;
                }
            };

            btn.MouseLeave += (s, e) =>
            {
                if (_isDarkMode)
                {
                    btn.BackColor = DarkBackground;
                    btn.ForeColor = DarkText;
                    btn.FlatAppearance.BorderColor = DarkBorder;
                }
                else
                {
                    btn.BackColor = Color.White;
                    btn.ForeColor = QuizletPrimary;
                    btn.FlatAppearance.BorderColor = QuizletBorder;
                }
            };

            btn.Click += (_, __) =>
            {
                if (SelectedSet == null)
                {
                    MessageBox.Show("Bạn chưa chọn học phần.", "Thiếu học phần",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                FeatureRequested?.Invoke(featureKey);
            };

            p.Controls.Add(btn);
            return p;
        }

        private Button MakeTileButton(CardSet set)
        {
            var title = string.IsNullOrWhiteSpace(set.Title) ? "(Untitled)" : set.Title.Trim();
            title = WrapForTile(title);

            var b = new Button
            {
                Width = 140, // tăng từ 120
                Height = 140, // tăng từ 120
                Margin = new Padding(12),
                Text = title,
                Tag = set,
                BackColor = Color.White,
                ForeColor = QuizletPrimary,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold), // tăng font
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter,
                UseCompatibleTextRendering = true
            };

            b.FlatAppearance.BorderSize = 2;
            b.FlatAppearance.BorderColor = QuizletBorder;

            // Hover effect
            b.MouseEnter += (s, e) =>
            {
                if (_selectedBtn != b)
                {
                    if (_isDarkMode)
                    {
                        b.BackColor = Color.FromArgb(50, 50, 60);
                        b.FlatAppearance.BorderColor = QuizletBlue;
                    }
                    else
                    {
                        b.BackColor = QuizletBlueLight;
                        b.FlatAppearance.BorderColor = QuizletBlue;
                    }
                }
            };

            b.MouseLeave += (s, e) =>
            {
                if (_selectedBtn != b)
                {
                    ApplyTileTheme(b, false);
                }
            };

            b.Click += (_, __) => SelectTile(b);

            // double click: chọn học phần + vào luôn flashcards
            b.DoubleClick += (_, __) =>
            {
                SelectTile(b);
                if (SelectedSet != null)
                    FeatureRequested?.Invoke(CardFeatureKeys.Flashcards);
            };

            return b;
        }

        private void SelectTile(Button b)
        {
            if (_selectedBtn == b) return;

            if (_selectedBtn != null) ApplyTileSelected(_selectedBtn, selected: false);

            _selectedBtn = b;
            ApplyTileSelected(_selectedBtn, selected: true);

            SelectedSet = b.Tag as CardSet;
            SelectedSetChanged?.Invoke(SelectedSet);
        }

        private void ApplyTileSelected(Button b, bool selected)
        {
            ApplyTileTheme(b, selected);
        }

        private void ApplyTileTheme(Button b, bool selected)
        {
            if (_isDarkMode)
            {
                if (selected)
                {
                    b.BackColor = Color.FromArgb(46, 116, 215);
                    b.ForeColor = Color.White;
                    b.FlatAppearance.BorderColor = QuizletBlue;
                    b.FlatAppearance.BorderSize = 3;
                }
                else
                {
                    b.BackColor = DarkBackground;
                    b.ForeColor = DarkText;
                    b.FlatAppearance.BorderColor = DarkBorder;
                    b.FlatAppearance.BorderSize = 2;
                }
            }
            else
            {
                if (selected)
                {
                    b.BackColor = QuizletBlueLight;
                    b.ForeColor = QuizletBlue;
                    b.FlatAppearance.BorderColor = QuizletBlue;
                    b.FlatAppearance.BorderSize = 3;
                }
                else
                {
                    b.BackColor = Color.White;
                    b.ForeColor = QuizletPrimary;
                    b.FlatAppearance.BorderColor = QuizletBorder;
                    b.FlatAppearance.BorderSize = 2;
                }
            }
        }

        private static string WrapForTile(string s)
        {
            // convert underscore to space
            s = (s ?? "").Replace("_", " ").Trim();
            if (s.Length <= 14) return s; // tăng từ 12 lên 14

            var words = s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length <= 1) return s;

            var lines = new List<string>();
            var cur = "";

            foreach (var w in words)
            {
                if (cur.Length == 0)
                {
                    cur = w;
                    continue;
                }

                if ((cur.Length + 1 + w.Length) <= 14) // tăng từ 12 lên 14
                {
                    cur += " " + w;
                }
                else
                {
                    lines.Add(cur);
                    cur = w;
                    if (lines.Count == 2) break; // tối đa 3 dòng
                }
            }

            if (lines.Count < 3 && cur.Length > 0)
                lines.Add(cur);

            return string.Join("\n", lines.Take(3));
        }
    }
}