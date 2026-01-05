using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using TocflQuiz.Models;

namespace TocflQuiz.Forms
{
    /// <summary>
    /// Form "Danh sách học phần": hiển thị tile học phần + toolbar chức năng.
    /// Khi user chọn chức năng -> đóng form và trả về FeatureKey + SelectedModules.
    /// </summary>
    public sealed partial class CoursePickerForm : Form
    {
        private readonly List<CourseModule> _allModules;
        private readonly HashSet<string> _selectedIds = new(StringComparer.OrdinalIgnoreCase);

        private readonly TextBox _txtSearch = new();
        private readonly Label _lblCount = new();
        private readonly FlowLayoutPanel _flowTiles = new();

        public string SelectedFeatureKey { get; private set; } = string.Empty;
        public List<CourseModule> SelectedModules { get; private set; } = new();

        public CoursePickerForm(IEnumerable<CourseModule> modules, IEnumerable<string>? preselectIds = null)
        {
            _allModules = (modules ?? Array.Empty<CourseModule>()).ToList();
            if (preselectIds != null)
            {
                foreach (var id in preselectIds)
                {
                    var key = (id ?? string.Empty).Trim();
                    if (!string.IsNullOrWhiteSpace(key)) _selectedIds.Add(key);
                }
            }

            Text = "Danh sách học phần";
            StartPosition = FormStartPosition.CenterParent;
            Width = 1100;
            Height = 260;
            MinimumSize = new Size(860, 240);
            Font = new Font("Segoe UI", 9F);

            BuildUi();
            RenderTiles();
        }

        private void BuildUi()
        {
            BackColor = Color.White;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(18)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));  // toolbar
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // tiles
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));  // search

            // Toolbar: 2 rows x 3 cols
            var toolbar = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 2,
                Margin = new Padding(0)
            };
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));
            toolbar.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            toolbar.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

            toolbar.Controls.Add(BuildToolButton("🧠  Thẻ ghi nhớ", CardFeatureKeys.Flashcards), 0, 0);
            toolbar.Controls.Add(BuildToolButton("📦  Học phần", CardFeatureKeys.Course), 1, 0);
            toolbar.Controls.Add(BuildToolButton("✍️  Kiểm tra", CardFeatureKeys.Quiz), 2, 0);

            toolbar.Controls.Add(BuildToolButton("🧩  Blocks", CardFeatureKeys.Blocks), 0, 1);
            toolbar.Controls.Add(BuildToolButton("🚀  Blast", CardFeatureKeys.Blast), 1, 1);
            toolbar.Controls.Add(BuildToolButton("🧷  Ghép thẻ", CardFeatureKeys.MergeCards), 2, 1);

            // Tiles
            _flowTiles.Dock = DockStyle.Fill;
            _flowTiles.AutoScroll = true;
            _flowTiles.WrapContents = true;
            _flowTiles.FlowDirection = FlowDirection.LeftToRight;
            _flowTiles.BackColor = Color.White;
            _flowTiles.Padding = new Padding(0);
            _flowTiles.Margin = new Padding(0, 12, 0, 12);

            // Search row
            var searchRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0)
            };
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 420));
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            _txtSearch.Dock = DockStyle.Fill;
            _txtSearch.Font = new Font("Segoe UI", 9.5F);
            _txtSearch.PlaceholderText = "Tìm kiếm học phần...";
            _txtSearch.TextChanged += (_, __) => RenderTiles();

            _lblCount.Dock = DockStyle.Left;
            _lblCount.AutoSize = true;
            _lblCount.Padding = new Padding(12, 10, 0, 0);
            _lblCount.Font = new Font("Segoe UI", 9.5F);
            _lblCount.ForeColor = Color.FromArgb(80, 80, 80);

            searchRow.Controls.Add(_txtSearch, 0, 0);
            searchRow.Controls.Add(_lblCount, 1, 0);

            root.Controls.Add(toolbar, 0, 0);
            root.Controls.Add(_flowTiles, 0, 1);
            root.Controls.Add(searchRow, 0, 2);

            Controls.Add(root);
        }

        private Button BuildToolButton(string text, string featureKey)
        {
            var btn = new Button
            {
                Text = text,
                Dock = DockStyle.Fill,
                Height = 36,
                Margin = new Padding(8, 6, 8, 6),
                BackColor = Color.FromArgb(245, 247, 252),
                ForeColor = Color.FromArgb(35, 35, 35),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(225, 230, 240);
            btn.FlatAppearance.BorderSize = 1;

            btn.Click += (_, __) => Finish(featureKey);
            return btn;
        }

        private void RenderTiles()
        {
            _flowTiles.SuspendLayout();
            _flowTiles.Controls.Clear();

            var q = (_txtSearch.Text ?? string.Empty).Trim();
            var list = _allModules
                .Where(m => string.IsNullOrWhiteSpace(q) || m.Title.Contains(q, StringComparison.OrdinalIgnoreCase))
                .OrderBy(m => m.Title)
                .ToList();

            foreach (var m in list)
            {
                _flowTiles.Controls.Add(BuildTile(m));
            }

            _lblCount.Text = $"Có {list.Count} học phần";

            _flowTiles.ResumeLayout();
        }

        private Button BuildTile(CourseModule m)
        {
            var selected = _selectedIds.Contains(m.Id);

            var btn = new Button
            {
                Tag = m,
                Text = m.Title,
                Width = 120,
                Height = 120,
                Margin = new Padding(0, 0, 14, 14),
                BackColor = selected ? Color.FromArgb(225, 240, 255) : Color.White,
                ForeColor = Color.FromArgb(35, 35, 35),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand
            };

            btn.FlatAppearance.BorderColor = selected ? Color.FromArgb(70, 130, 180) : Color.FromArgb(220, 220, 220);
            btn.FlatAppearance.BorderSize = 2;

            btn.Click += (_, __) => ToggleSelect(btn);
            return btn;
        }

        private void ToggleSelect(Button tile)
        {
            if (tile.Tag is not CourseModule m) return;

            // Nếu không giữ Ctrl: select single
            if ((ModifierKeys & Keys.Control) == 0)
            {
                _selectedIds.Clear();
                _selectedIds.Add(m.Id);
            }
            else
            {
                if (!_selectedIds.Add(m.Id))
                    _selectedIds.Remove(m.Id);
            }

            // update style
            foreach (var ctrl in _flowTiles.Controls)
            {
                if (ctrl is not Button b || b.Tag is not CourseModule bm) continue;

                var sel = _selectedIds.Contains(bm.Id);
                b.BackColor = sel ? Color.FromArgb(225, 240, 255) : Color.White;
                b.FlatAppearance.BorderColor = sel ? Color.FromArgb(70, 130, 180) : Color.FromArgb(220, 220, 220);
            }
        }

        private void Finish(string featureKey)
        {
            SelectedFeatureKey = featureKey;

            SelectedModules = _allModules
                .Where(m => _selectedIds.Contains(m.Id))
                .OrderBy(m => m.Title)
                .ToList();

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
