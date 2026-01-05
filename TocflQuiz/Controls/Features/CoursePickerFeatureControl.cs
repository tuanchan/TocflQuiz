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

        public CoursePickerFeatureControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;

            BuildUi();
            Reload();
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

            // Toolbar (2 hàng)
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
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
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 420));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            _txtSearch.Dock = DockStyle.Fill;
            _txtSearch.Margin = new Padding(0, 10, 10, 10);
            _txtSearch.PlaceholderText = "Tìm kiếm học phần...";
            _txtSearch.TextChanged += (_, __) => Reload();

            _lblInfo.Dock = DockStyle.Fill;
            _lblInfo.TextAlign = ContentAlignment.MiddleLeft;
            _lblInfo.ForeColor = Color.FromArgb(90, 90, 90);

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
                Margin = new Padding(8),
                BackColor = Color.FromArgb(245, 247, 255),
            };

            var btn = new Button
            {
                Dock = DockStyle.Fill,
                Text = text,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(245, 247, 255),
                ForeColor = Color.FromArgb(35, 35, 35),
                Cursor = Cursors.Hand,
                Tag = featureKey
            };
            btn.FlatAppearance.BorderSize = 0;

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
                Width = 120,
                Height = 120,
                Margin = new Padding(12),
                Text = title,
                Tag = set,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(35, 35, 35),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter,
                UseCompatibleTextRendering = true
            };

            b.FlatAppearance.BorderSize = 2;
            b.FlatAppearance.BorderColor = Color.FromArgb(225, 225, 225);

            b.Click += (_, __) => SelectTile(b);

            // double click: chọn học phần + vào luôn flashcards (giống kiểu “chọn nhanh”)
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

        private static void ApplyTileSelected(Button b, bool selected)
        {
            if (selected)
            {
                b.BackColor = Color.FromArgb(242, 247, 255);
                b.FlatAppearance.BorderColor = Color.FromArgb(140, 170, 255);
            }
            else
            {
                b.BackColor = Color.White;
                b.FlatAppearance.BorderColor = Color.FromArgb(225, 225, 225);
            }
        }

        private static string WrapForTile(string s)
        {
            // convert underscore to space
            s = (s ?? "").Replace("_", " ").Trim();
            if (s.Length <= 12) return s;

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

                if ((cur.Length + 1 + w.Length) <= 12)
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
