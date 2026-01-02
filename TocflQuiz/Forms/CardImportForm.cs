using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using TocflQuiz.Models;
using TocflQuiz.Services;

namespace TocflQuiz.Forms
{
    public sealed partial class CardImportForm : Form
    {
        private TextBox txtTitle = new();
        private TextBox txtRaw = new();

        // Term/Definition separator
        private RadioButton rbTD_Tab = new();
        private RadioButton rbTD_Comma = new();
        private RadioButton rbTD_Custom = new();
        private TextBox txtTD_Custom = new();

        // Card separator
        private RadioButton rbCard_NewLine = new();
        private RadioButton rbCard_Semicolon = new();
        private RadioButton rbCard_Custom = new();
        private TextBox txtCard_Custom = new();

        private Button btnPreview = new();
        private Button btnSave = new();

        private ListView lvPreview = new();
        private Label lblCount = new();

        private CardItem[] _lastPreview = Array.Empty<CardItem>();

        // Watermark text
        private const string TitleWatermark = "Tên học phần (ví dụ: TOCFL A2 - Week 1)";
        private const string TdCustomWatermark = "Nhập ký tự (vd: | )";
        private const string CardCustomWatermark = "Nhập ký tự (vd: ### )";

        public CardImportForm()
        {
            Text = "Nhập dữ liệu (Import)";
            StartPosition = FormStartPosition.CenterParent;
            Width = 1100;
            Height = 720;
            MinimumSize = new Size(900, 600);
            Font = new Font("Segoe UI", 9F);

            BuildUi();
            Wire();
            ApplyWatermarks();
        }

        private void BuildUi()
        {
            // Root layout: 5 rows
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16),
                BackColor = Color.White,
                ColumnCount = 1,
                RowCount = 5
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));  // header
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 260)); // raw input
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 150)); // options
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));  // preview title
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // preview table fill

            // ===== Header row =====
            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));
            var titleWrap = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 6, 0, 0) };
            var lblTitle = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                Text = "Nhập dữ liệu"
            };

            txtTitle.Dock = DockStyle.Fill;
            txtTitle.Margin = new Padding(0, 14, 10, 14);

            var btnWrap = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 12, 0, 0)
            };

            btnSave.Text = "Lưu";
            btnPreview.Text = "Xem trước";
            StylePrimary(btnSave);
            StyleGhost(btnPreview);

            btnWrap.Controls.Add(btnSave);
            btnWrap.Controls.Add(btnPreview);

            header.Controls.Add(lblTitle, 0, 0);
            header.Controls.Add(txtTitle, 1, 0);
            header.Controls.Add(btnWrap, 2, 0);

            // ===== Raw input row =====
            var gbRaw = new GroupBox
            {
                Dock = DockStyle.Fill,
                Text = "Nhập dữ liệu (copy paste từ Word/Excel/Google Docs...)",
                Padding = new Padding(10)
            };

            txtRaw.Dock = DockStyle.Fill;
            txtRaw.Multiline = true;
            txtRaw.ScrollBars = ScrollBars.Vertical;
            txtRaw.Font = new Font("Consolas", 10F);
            txtRaw.Text = "Từ 1\tĐịnh nghĩa 1\nTừ 2\tĐịnh nghĩa 2\nTừ 3\tĐịnh nghĩa 3";
            gbRaw.Controls.Add(txtRaw);

            // ===== Options row =====
            var optionsRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1
            };
            optionsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            optionsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            var gbTD = new GroupBox { Dock = DockStyle.Fill, Text = "Giữa thuật ngữ và định nghĩa", Padding = new Padding(10) };
            var gbCard = new GroupBox { Dock = DockStyle.Fill, Text = "Giữa các thẻ", Padding = new Padding(10) };
            optionsRow.Controls.Add(gbTD, 0, 0);
            optionsRow.Controls.Add(gbCard, 1, 0);

            BuildSepOptions_TD(gbTD);
            BuildSepOptions_Card(gbCard);

            // ===== Preview title row =====
            var previewHeader = new Panel { Dock = DockStyle.Fill };
            var lblPrev = new Label
            {
                Dock = DockStyle.Left,
                Width = 220,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Text = "Xem trước"
            };
            lblCount.Dock = DockStyle.Fill;
            lblCount.TextAlign = ContentAlignment.MiddleLeft;
            lblCount.ForeColor = Color.FromArgb(90, 90, 90);

            previewHeader.Controls.Add(lblCount);
            previewHeader.Controls.Add(lblPrev);

            // ===== Preview table row =====
            lvPreview.Dock = DockStyle.Fill;
            lvPreview.View = View.Details;
            lvPreview.FullRowSelect = true;
            lvPreview.GridLines = true;
            lvPreview.HideSelection = false;

            lvPreview.Columns.Clear();
            lvPreview.Columns.Add("Thuật ngữ", 260);
            lvPreview.Columns.Add("Định nghĩa", 520);
            lvPreview.Columns.Add("Pinyin", 200);

            // add to root
            root.Controls.Add(header, 0, 0);
            root.Controls.Add(gbRaw, 0, 1);
            root.Controls.Add(optionsRow, 0, 2);
            root.Controls.Add(previewHeader, 0, 3);
            root.Controls.Add(lvPreview, 0, 4);

            Controls.Clear();
            Controls.Add(root);

            // defaults
            rbTD_Tab.Checked = true;
            rbCard_NewLine.Checked = true;
            UpdateCustomState();
        }

        private void BuildSepOptions_TD(Control host)
        {
            var wrap = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3
            };
            wrap.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            wrap.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            wrap.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            wrap.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            wrap.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));

            rbTD_Tab.Text = "Tab";
            rbTD_Comma.Text = "Phẩy ( , )";
            rbTD_Custom.Text = "Tùy chỉnh";

            txtTD_Custom.Dock = DockStyle.Left;
            txtTD_Custom.Width = 220;

            wrap.Controls.Add(rbTD_Tab, 0, 0);
            wrap.SetColumnSpan(rbTD_Tab, 2);

            wrap.Controls.Add(rbTD_Comma, 0, 1);
            wrap.SetColumnSpan(rbTD_Comma, 2);

            wrap.Controls.Add(rbTD_Custom, 0, 2);
            wrap.Controls.Add(txtTD_Custom, 1, 2);

            host.Controls.Add(wrap);
        }

        private void BuildSepOptions_Card(Control host)
        {
            var wrap = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3
            };
            wrap.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            wrap.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            wrap.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            wrap.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            wrap.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));

            rbCard_NewLine.Text = "Dòng mới";
            rbCard_Semicolon.Text = "Chấm phẩy ( ; )";
            rbCard_Custom.Text = "Tùy chỉnh";

            txtCard_Custom.Dock = DockStyle.Left;
            txtCard_Custom.Width = 220;

            wrap.Controls.Add(rbCard_NewLine, 0, 0);
            wrap.SetColumnSpan(rbCard_NewLine, 2);

            wrap.Controls.Add(rbCard_Semicolon, 0, 1);
            wrap.SetColumnSpan(rbCard_Semicolon, 2);

            wrap.Controls.Add(rbCard_Custom, 0, 2);
            wrap.Controls.Add(txtCard_Custom, 1, 2);

            host.Controls.Add(wrap);
        }

        private void Wire()
        {
            rbTD_Tab.CheckedChanged += (_, __) => UpdateCustomState();
            rbTD_Comma.CheckedChanged += (_, __) => UpdateCustomState();
            rbTD_Custom.CheckedChanged += (_, __) => UpdateCustomState();

            rbCard_NewLine.CheckedChanged += (_, __) => UpdateCustomState();
            rbCard_Semicolon.CheckedChanged += (_, __) => UpdateCustomState();
            rbCard_Custom.CheckedChanged += (_, __) => UpdateCustomState();

            btnPreview.Click += (_, __) => DoPreview();
            btnSave.Click += (_, __) => DoSave();

            // nếu bạn muốn auto-preview mỗi lần gõ thì mở dòng này:
            // txtRaw.TextChanged += (_, __) => DoPreview();
            txtRaw.TextChanged += (_, __) => lblCount.Text = "";
        }

        private void UpdateCustomState()
        {
            txtTD_Custom.Enabled = rbTD_Custom.Checked;
            txtCard_Custom.Enabled = rbCard_Custom.Checked;
        }

        private string GetTermDefSep()
        {
            if (rbTD_Tab.Checked) return "\t";
            if (rbTD_Comma.Checked) return ",";
            return GetWatermarkSafeText(txtTD_Custom, TdCustomWatermark);
        }

        private string GetCardSep()
        {
            if (rbCard_NewLine.Checked) return "\n";
            if (rbCard_Semicolon.Checked) return ";";
            return GetWatermarkSafeText(txtCard_Custom, CardCustomWatermark);
        }

        private void DoPreview()
        {
            var termDefSep = GetTermDefSep();
            var cardSep = GetCardSep();

            if (rbTD_Custom.Checked && string.IsNullOrEmpty(termDefSep))
            {
                MessageBox.Show("Bạn đang chọn 'Tùy chỉnh' cho giữa thuật ngữ/định nghĩa nhưng chưa nhập ký tự.",
                    "Thiếu phân cách", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (rbCard_Custom.Checked && string.IsNullOrEmpty(cardSep))
            {
                MessageBox.Show("Bạn đang chọn 'Tùy chỉnh' cho giữa các thẻ nhưng chưa nhập ký tự.",
                    "Thiếu phân cách", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var raw = txtRaw.Text ?? "";
            var items = CardImportParser.Parse(raw, termDefSep, cardSep);
            _lastPreview = items.ToArray();

            lvPreview.BeginUpdate();
            lvPreview.Items.Clear();

            foreach (var it in items.Take(500))
            {
                var lvi = new ListViewItem(it.Term);
                lvi.SubItems.Add(it.Definition);
                lvi.SubItems.Add(it.Pinyin ?? "");
                lvPreview.Items.Add(lvi);
            }

            lvPreview.EndUpdate();
            lblCount.Text = $"• {items.Count} thẻ";
        }

        private void DoSave()
        {
            if (_lastPreview.Length == 0) DoPreview();
            if (_lastPreview.Length == 0)
            {
                MessageBox.Show("Chưa có thẻ nào hợp lệ để lưu. Hãy kiểm tra dữ liệu và phân cách.",
                    "Không có dữ liệu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var title = GetWatermarkSafeText(txtTitle, TitleWatermark).Trim();
            if (string.IsNullOrWhiteSpace(title))
                title = $"Set {DateTime.Now:yyyy-MM-dd HH:mm}";

            var set = new CardSet
            {
                Title = title,
                CreatedAt = DateTime.Now,
                Items = _lastPreview.ToList()
            };

            var dir = CardSetStorage.SaveSet(
                set,
                rawInput: CardImportParser.NormalizeNewlines(txtRaw.Text ?? ""),
                termDefSep: GetTermDefSep(),
                cardSep: GetCardSep()
            );

            MessageBox.Show($"Đã lưu {set.Items.Count} thẻ.\nFolder:\n{dir}",
                "OK", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }

        // ===== Styles =====
        private static void StylePrimary(Button b)
        {
            b.AutoSize = false;
            b.Width = 120;
            b.Height = 38;
            b.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            b.BackColor = Color.FromArgb(66, 104, 255);
            b.ForeColor = Color.White;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.Cursor = Cursors.Hand;
            b.Margin = new Padding(10, 0, 0, 0);
        }

        private static void StyleGhost(Button b)
        {
            b.AutoSize = false;
            b.Width = 120;
            b.Height = 38;
            b.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            b.BackColor = Color.White;
            b.ForeColor = Color.FromArgb(40, 40, 40);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = Color.FromArgb(220, 220, 220);
            b.Cursor = Cursors.Hand;
            b.Margin = new Padding(10, 0, 0, 0);
        }

        // ===== Watermark helper =====
        private void ApplyWatermarks()
        {
            ApplyWatermark(txtTitle, TitleWatermark);
            ApplyWatermark(txtTD_Custom, TdCustomWatermark);
            ApplyWatermark(txtCard_Custom, CardCustomWatermark);
        }

        private static void ApplyWatermark(TextBox tb, string watermark)
        {
            // set initial watermark if empty
            if (string.IsNullOrWhiteSpace(tb.Text))
            {
                tb.Text = watermark;
                tb.ForeColor = Color.Gray;
            }

            tb.GotFocus += (_, __) =>
            {
                if (tb.ForeColor == Color.Gray && tb.Text == watermark)
                {
                    tb.Text = "";
                    tb.ForeColor = SystemColors.WindowText;
                }
            };

            tb.LostFocus += (_, __) =>
            {
                if (string.IsNullOrWhiteSpace(tb.Text))
                {
                    tb.Text = watermark;
                    tb.ForeColor = Color.Gray;
                }
            };
        }

        private static string GetWatermarkSafeText(TextBox tb, string watermark)
        {
            if (tb.ForeColor == Color.Gray && tb.Text == watermark) return "";
            return tb.Text ?? "";
        }
    }
}
