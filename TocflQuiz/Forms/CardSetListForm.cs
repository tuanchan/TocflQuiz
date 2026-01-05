using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using TocflQuiz.Controls;
using TocflQuiz.Services;

namespace TocflQuiz.Forms
{
    public sealed partial class CardSetListForm : Form
    {
        private FlowLayoutPanel grid = new();
        private TextBox txtSearch = new();
        private Label lblInfo = new();
        public CardSetListCommand SelectedCommand { get; private set; } = CardSetListCommand.OpenSetListEmbedded;

        private CardSetTile? _selected;

        public CardSetListForm()
        {
            Text = "Danh sách học phần";
            StartPosition = FormStartPosition.CenterParent;
            Width = 1100;
            Height = 720;
            MinimumSize = new Size(900, 600);
            Font = new Font("Segoe UI", 9F);

            BuildUi();
            LoadSets();
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16),
                BackColor = Color.White,
                ColumnCount = 1,
                RowCount = 3
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));   // toolbar buttons
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));   // search/info
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // grid

            // ===== Toolbar (2 hàng như ảnh 2) =====
            var bar = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 2,
            };
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));
            bar.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            bar.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

            bar.Controls.Add(MakePill("🧠 Thẻ ghi nhớ"), 0, 0);
            bar.Controls.Add(MakePill("📚 Học phần"), 1, 0);
            bar.Controls.Add(MakePill("📝 Kiểm tra"), 2, 0);
            bar.Controls.Add(MakePill("🧩 Blocks"), 0, 1);
            bar.Controls.Add(MakePill("🚀 Blast"), 1, 1);
            bar.Controls.Add(MakePill("🧷 Ghép thẻ"), 2, 1);

            // ===== Search + info =====
            var searchRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1
            };
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 420));
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            txtSearch.Dock = DockStyle.Fill;
            txtSearch.Margin = new Padding(0, 10, 10, 10);
            txtSearch.PlaceholderText = "Tìm kiếm học phần...";

            lblInfo.Dock = DockStyle.Fill;
            lblInfo.TextAlign = ContentAlignment.MiddleLeft;
            lblInfo.ForeColor = Color.FromArgb(90, 90, 90);

            txtSearch.TextChanged += (_, __) => LoadSets();

            searchRow.Controls.Add(txtSearch, 0, 0);
            searchRow.Controls.Add(lblInfo, 1, 0);

            // ===== Grid =====
            grid.Dock = DockStyle.Fill;
            grid.AutoScroll = true;
            grid.WrapContents = true;
            grid.FlowDirection = FlowDirection.LeftToRight;
            grid.Padding = new Padding(4);
            grid.BackColor = Color.FromArgb(250, 250, 250);

            root.Controls.Add(bar, 0, 0);
            root.Controls.Add(searchRow, 0, 1);
            root.Controls.Add(grid, 0, 2);

            Controls.Add(root);
        }

        private void LoadSets()
        {
            grid.SuspendLayout();
            grid.Controls.Clear();

            var sets = CardSetStorage.LoadAllSetsSafe();

            var q = (txtSearch.Text ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(q))
            {
                sets = sets
                    .Where(s => (s.Title ?? "").Contains(q, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            lblInfo.Text = $"Có {sets.Count} học phần";

            foreach (var s in sets)
            {
                var tile = new CardSetTile();
                tile.Bind(s);

                tile.TileClick += (_, __) => SelectTile(tile);
                tile.TileDoubleClick += (_, __) =>
                {
                    // sau này: mở Study với set đã chọn
                    MessageBox.Show($"Bạn chọn học phần: {s.Title}\n(Bước sau sẽ mở Study)", "Chọn học phần");
                };

                grid.Controls.Add(tile);
            }

            grid.ResumeLayout(true);
        }

        private void SelectTile(CardSetTile tile)
        {
            if (_selected != null) _selected.Selected = false;
            _selected = tile;
            _selected.Selected = true;
        }

        private static Control MakePill(string text)
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
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;

            p.Controls.Add(btn);
            return p;
        }
        public enum CardSetListCommand
        {
            OpenSetListEmbedded,
            OpenStudy,
            OpenCreate
        }
    }
}
