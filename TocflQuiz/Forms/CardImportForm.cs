using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using TocflQuiz.Models;
using TocflQuiz.Services;

namespace TocflQuiz.Forms
{
    public sealed partial class CardImportForm : Form
    {
        private TextBox txtTitle = new();
        private TextBox txtRaw = new();

        private RadioButton rbTD_Tab = new();
        private RadioButton rbTD_Comma = new();
        private RadioButton rbTD_Custom = new();
        private TextBox txtTD_Custom = new();

        private RadioButton rbCard_NewLine = new();
        private RadioButton rbCard_Semicolon = new();
        private RadioButton rbCard_Custom = new();
        private TextBox txtCard_Custom = new();

        private Button btnPreview = new();
        private Button btnSave = new();

        private FlowLayoutPanel flPreview = new();
        private Label lblCount = new();
        // ===== Nice UI dialog (thông báo đẹp) =====
        private OverlayPanel? _uiOverlay;
        private RoundedPanel? _uiDialog;
        private Label? _uiIcon;
        private Label? _uiTitle;
        private Label? _uiMessage;
        private Button? _uiOk;
        private Button? _uiClose;
        private Action? _uiOkAction;
        private CardItem[] _lastPreview = Array.Empty<CardItem>();

        private const string TitleWatermark = "Tên học phần (ví dụ: TOCFL A2 - Week 1)";
        private const string RawWatermark =
            "Từ 1\tĐịnh nghĩa 1\r\n" +
            "Từ 2\tĐịnh nghĩa 2\r\n" +
            "Từ 3\tĐịnh nghĩa 3";

        private const string TdCustomWatermark = "Nhập ký tự (vd: | )";
        private const string CardCustomWatermark = "Nhập ký tự (vd: ### )";

        // Theme
        private static readonly Color Bg = Color.FromArgb(246, 247, 251);
        private static readonly Color CardColor = Color.White;
        private static readonly Color Field = Color.FromArgb(245, 246, 250);
        private static readonly Color Border = Color.FromArgb(229, 231, 235);
        private static readonly Color TextColor = Color.FromArgb(17, 24, 39);
        private static readonly Color Muted = Color.FromArgb(107, 114, 128);
        private static readonly Color Primary = Color.FromArgb(66, 104, 255);

        public CardImportForm()
        {
            this.Text = "Import";
            StartPosition = FormStartPosition.CenterParent;
            Width = 1100;
            Height = 720;
            MinimumSize = new Size(900, 600);
            Font = new Font("Segoe UI", 9F);
            BackColor = Bg;

            AutoScaleMode = AutoScaleMode.Dpi;

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            UpdateStyles();

            BuildUi();
            Wire();
            ApplyWatermarks();
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(18),
                BackColor = Bg,
                ColumnCount = 1,
                RowCount = 5,
                Margin = new Padding(0)
            };

            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 51));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 55));

            // ===== HEADER: txtTitle + buttons =====
            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                Margin = new Padding(0),
                BackColor = Bg

            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));

            var titleHost = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                FillColor = CardColor,
                BorderColor = Border,
                Radius = 12,
                Padding = new Padding(12, 9, 12, 9),
                Margin = new Padding(0, 6, 12, 6)

            };

            txtTitle = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Dock = DockStyle.Fill,
                BackColor = CardColor,
                ForeColor = TextColor,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold)
            };
            titleHost.Controls.Add(txtTitle);

            var btnWrap = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 10, 0, 0),
                Margin = new Padding(0),
                BackColor = Bg

            };

            btnSave.Text = "Lưu";
            btnPreview.Text = "Xem trước";

            StylePrimaryPill(btnSave);
            StyleSecondaryPill(btnPreview);


            btnWrap.Controls.Add(btnSave);
            btnWrap.Controls.Add(btnPreview);

            header.Controls.Add(titleHost, 0, 0);
            header.Controls.Add(btnWrap, 1, 0);

            // ===== RAW CARD =====
            var rawCard = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                FillColor = CardColor,
                BorderColor = Border,
                Radius = 14,
                Padding = new Padding(14),
                Margin = new Padding(0, 16, 0, 0)
            };



            txtRaw = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 10.8F, FontStyle.Regular),
                BorderStyle = BorderStyle.None,
                BackColor = CardColor,
                ForeColor = TextColor
            };
            var rawHeader = CreateSectionHeader("NHẬP DỮ LIỆU (copy paste từ Word/Excel/Google Docs...)");
            rawHeader.Margin = new Padding(0, 0, 0, 8);

            var rawSection = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            rawCard.Controls.Add(txtRaw);

            rawSection.Controls.Add(rawCard);
            rawSection.Controls.Add(rawHeader);


            // ===== OPTIONS (mở rộng để chứa đủ text) =====
            var optionsRow = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                Margin = new Padding(0, 16, 0, 16),
                BackColor = Color.Transparent
            };
            optionsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            optionsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            optionsRow.RowCount = 2;
            optionsRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            optionsRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var tdHeader = CreateSectionHeader("Giữa thuật ngữ và định nghĩa");
            tdHeader.Margin = new Padding(0, 0, 0, 6);

            var cardHeader = CreateSectionHeader("Giữa các thẻ");
            cardHeader.Margin = new Padding(0, 0, 0, 6);

            var tdCard = CreateOptionCard();
            var cardCard = CreateOptionCard();

            BuildSepOptions_TD(tdCard.ContentHost);
            BuildSepOptions_Card(cardCard.ContentHost);

            optionsRow.Controls.Add(tdHeader, 0, 0);
            optionsRow.Controls.Add(cardHeader, 1, 0);
            optionsRow.Controls.Add(tdCard.Wrapper, 0, 1);
            optionsRow.Controls.Add(cardCard.Wrapper, 1, 1);


            // ===== PREVIEW HEADER =====
            var previewHeader = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0),
                Padding = new Padding(0, 6, 0, 6),
                BackColor = Color.Transparent
            };

            var lblPrev = new Label
            {
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = TextColor,
                Text = "Xem trước",
                Margin = new Padding(0, 0, 14, 0)
            };

            lblCount = new Label
            {
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Muted,
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                Text = "",
                Margin = new Padding(0, 2, 0, 0)
            };

            previewHeader.Controls.Add(lblPrev);
            previewHeader.Controls.Add(lblCount);

            // ===== PREVIEW LIST =====
            var previewWrap = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                FillColor = CardColor,
                BorderColor = Border,
                Radius = 14,
                Padding = new Padding(12),
                Margin = new Padding(0)
            };

            flPreview = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = CardColor,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            EnableDoubleBuffering(flPreview);

            flPreview.SizeChanged += (_, __) => RelayoutPreviewRows();
            flPreview.Layout += (_, __) => RelayoutPreviewRows();

            previewWrap.Controls.Add(flPreview);

            // ===== Add to root =====
            root.Controls.Add(header, 0, 0);
            root.Controls.Add(rawSection, 0, 1);

            root.Controls.Add(optionsRow, 0, 2);
            root.Controls.Add(previewHeader, 0, 3);
            root.Controls.Add(previewWrap, 0, 4);

            Controls.Clear();
            Controls.Add(root);
            BuildUiDialog();
            rbTD_Tab.Checked = true;
            rbCard_NewLine.Checked = true;
            UpdateCustomState();
        }
        private void BuildUiDialog()
        {
            if (_uiOverlay != null) return;

            _uiOverlay = new OverlayPanel
            {
                Dock = DockStyle.Fill,
                Visible = false,
                BackColor = Color.Transparent
            };

            _uiDialog = new RoundedPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FillColor = Color.White,
                BorderColor = Color.FromArgb(229, 231, 235),
                Radius = 16,
                Padding = new Padding(80, 32, 80, 32), // Increased padding more
                Margin = new Padding(0),
                BackColor = Color.Transparent
            };

            _uiIcon = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 32F, FontStyle.Bold), // Larger icon
                ForeColor = TextColor,
                Text = "⚠️",
                Margin = new Padding(0, 0, 14, 0)
            };

            _uiTitle = new Label
            {
                AutoSize = false,
                Width = 280, // Wider
                Font = new Font("Segoe UI", 14F, FontStyle.Bold), // Larger font
                ForeColor = TextColor,
                Text = "",
                Margin = new Padding(0, 6, 0, 0)
            };

            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2, // Changed from 3 to 2 (removed close button)
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, 20)
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.Controls.Add(_uiIcon, 0, 0);
            header.Controls.Add(_uiTitle, 1, 0);

            _uiMessage = new Label
            {
                AutoSize = false,
                Width = 450, // Much wider
                Font = new Font("Segoe UI", 11.5F, FontStyle.Regular), // Slightly larger
                ForeColor = Muted,
                Text = "",
                Margin = new Padding(0, 0, 0, 28)
            };

            _uiOk = new Button { Text = "OK" };
            StylePrimaryPill(_uiOk);
            _uiOk.Width = 140; // Wider button
            _uiOk.Height = 44; // Taller button

            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            actions.Controls.Add(_uiOk);

            _uiDialog.Controls.Add(actions);
            _uiDialog.Controls.Add(_uiMessage);
            _uiDialog.Controls.Add(header);

            _uiOverlay.Controls.Add(_uiDialog);

            void CenterDialog()
            {
                if (_uiOverlay == null || _uiDialog == null) return;
                _uiDialog.Left = (_uiOverlay.ClientSize.Width - _uiDialog.Width) / 2;
                _uiDialog.Top = (_uiOverlay.ClientSize.Height - _uiDialog.Height) / 2;
            }

            _uiOverlay.SizeChanged += (_, __) => CenterDialog();
            _uiDialog.SizeChanged += (_, __) => CenterDialog();

            _uiOk.Click += (_, __) =>
            {
                var act = _uiOkAction;
                HideUiDialog();
                act?.Invoke();
            };

            Controls.Add(_uiOverlay);
            _uiOverlay.BringToFront();
        }

        private void ShowUiDialog(string title, string message, bool warning, Action? onOk = null)
        {
            BuildUiDialog();

            _uiOkAction = onOk;
            _uiTitle!.Text = title;
            _uiMessage!.Text = message;
            _uiIcon!.Text = warning ? "⚠️" : "✅";

            _uiOverlay!.Visible = true;
            _uiOverlay.BringToFront();
            _uiDialog!.BringToFront();
        }

        private void HideUiDialog()
        {
            if (_uiOverlay == null) return;
            _uiOkAction = null;
            _uiOverlay.Visible = false;
        }

        private sealed class OverlayPanel : Panel
        {
            public OverlayPanel()
            {
                BackColor = Color.Transparent;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
                UpdateStyles();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                // Use semi-transparent dark overlay
                using var b = new SolidBrush(Color.FromArgb(120, 0, 0, 0));
                e.Graphics.FillRectangle(b, ClientRectangle);
                base.OnPaint(e);
            }
        }

        private static FlowLayoutPanel CreateSectionHeader(string text)
        {
            var header = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0),
                Padding = new Padding(0, 6, 0, 6),
                BackColor = Color.Transparent
            };

            var lbl = new Label
            {
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = TextColor,
                Text = text,
                Margin = new Padding(0)
            };

            header.Controls.Add(lbl);
            return header;
        }

        private (RoundedPanel Wrapper, Panel ContentHost) CreateOptionCard()
        {
            var card = new RoundedPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FillColor = CardColor,
                BorderColor = Border,
                Radius = 14,
                Padding = new Padding(16, 16, 16, 18),
                Margin = new Padding(0)
            };

            var host = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            card.Controls.Add(host);
            return (card, host);
        }

        private void BuildSepOptions_TD(Control host)
        {
            var wrap = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                ColumnCount = 2,
                RowCount = 3,
                BackColor = Color.Transparent
            };
            wrap.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            wrap.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            wrap.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            wrap.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            wrap.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            rbTD_Tab.Text = "Tab";
            rbTD_Comma.Text = "Phẩy ( , )";
            rbTD_Custom.Text = "Tùy chỉnh";

            StyleRadio(rbTD_Tab);
            StyleRadio(rbTD_Comma);
            StyleRadio(rbTD_Custom);

            txtTD_Custom = new TextBox { Dock = DockStyle.Left, Width = 240, Font = new Font("Segoe UI", 10F) };
            txtTD_Custom.BorderStyle = BorderStyle.FixedSingle;

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
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                ColumnCount = 2,
                RowCount = 3,
                BackColor = Color.Transparent
            };
            wrap.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            wrap.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            wrap.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            wrap.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            wrap.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            rbCard_NewLine.Text = "Dòng mới";
            rbCard_Semicolon.Text = "Chấm phẩy ( ; ) ";
            rbCard_Custom.Text = "Tùy chỉnh";

            StyleRadio(rbCard_NewLine);
            StyleRadio(rbCard_Semicolon);
            StyleRadio(rbCard_Custom);

            txtCard_Custom = new TextBox { Dock = DockStyle.Left, Width = 240, Font = new Font("Segoe UI", 10F) };
            txtCard_Custom.BorderStyle = BorderStyle.FixedSingle;

            wrap.Controls.Add(rbCard_NewLine, 0, 0);
            wrap.SetColumnSpan(rbCard_NewLine, 2);

            wrap.Controls.Add(rbCard_Semicolon, 0, 1);
            wrap.SetColumnSpan(rbCard_Semicolon, 2);

            wrap.Controls.Add(rbCard_Custom, 0, 2);
            wrap.Controls.Add(txtCard_Custom, 1, 2);

            host.Controls.Add(wrap);
        }

        private static void StyleRadio(RadioButton rb)
        {
            rb.AutoSize = true;
            rb.ForeColor = TextColor;
            rb.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            rb.Margin = new Padding(0, 5, 0, 5);
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
                ShowUiDialog("Thiếu phân cách", "Bạn đang chọn 'Tùy chỉnh' nhưng chưa nhập ký tự phân cách.", warning: true);
                return;
            }

            if (rbCard_Custom.Checked && string.IsNullOrEmpty(cardSep))
            {
                ShowUiDialog("Thiếu phân cách", "Bạn đang chọn 'Tùy chỉnh' nhưng chưa nhập ký tự phân cách.", warning: true);
                return;
            }

            var raw = GetWatermarkSafeText(txtRaw, RawWatermark);
            if (string.IsNullOrWhiteSpace(raw))
            {
                flPreview.Controls.Clear();
                lblCount.Text = "";
                ShowUiDialog("Thiếu dữ liệu", "Bạn chưa nhập dữ liệu.", warning: true);
                _lastPreview = Array.Empty<CardItem>();
                return;
            }

            var items = CardImportParser.Parse(raw, termDefSep, cardSep);
            _lastPreview = items.ToArray();

            RenderPreview(items);
        }

        private void RenderPreview(System.Collections.Generic.IReadOnlyList<CardItem> items)
        {
            flPreview.SuspendLayout();
            flPreview.Controls.Clear();

            lblCount.Text = $"{items.Count} thẻ";

            int take = Math.Min(items.Count, 500);

            for (int i = 0; i < take; i++)
            {
                var it = items[i];
                var row = new PreviewRow(i + 1, it.Term, it.Definition);
                row.Margin = new Padding(0, 0, 0, 12);
                flPreview.Controls.Add(row);
            }

            flPreview.ResumeLayout(true);
            RelayoutPreviewRows();
            flPreview.PerformLayout();
            flPreview.Invalidate(true);
        }

        private void RelayoutPreviewRows()
        {
            if (flPreview.Controls.Count == 0) return;

            int w = flPreview.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 2;
            if (w < 200) w = 200;

            foreach (Control c in flPreview.Controls)
                c.Width = w;
        }

        private void DoSave()
        {
            if (_lastPreview.Length == 0) DoPreview();
            if (_lastPreview.Length == 0)
            {
                ShowUiDialog("Không có dữ liệu", "Chưa có thẻ nào hợp lệ để lưu.", warning: true);
                return;

            }

            var title = GetWatermarkSafeText(txtTitle, TitleWatermark).Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                ShowUiDialog("Thiếu tên học phần",
                    "Bạn chưa đặt tên học phần.\nHãy nhập tên học phần trước khi lưu.",
                    warning: true,
                    onOk: () => txtTitle.Focus());
                return;
            }


            var rawSafe = GetWatermarkSafeText(txtRaw, RawWatermark);

            var set = new CardSet
            {
                Title = title,
                CreatedAt = DateTime.Now,
                Items = _lastPreview.ToList()
            };

            var dir = CardSetStorage.SaveSet(
                set,
                rawInput: CardImportParser.NormalizeNewlines(rawSafe),
                termDefSep: GetTermDefSep(),
                cardSep: GetCardSep()
            );

            ShowUiDialog("Đã lưu",
     $"Đã lưu {set.Items.Count} thẻ.\nFolder:\n{dir}",
     warning: false,
     onOk: () =>
     {
         DialogResult = DialogResult.OK;
         Close();
     });
            return;

        }

        // ===== Preview Row =====
        private sealed class PreviewRow : Panel
        {
            public PreviewRow(int index, string term, string def)
            {
                BackColor = Color.Transparent;
                Height = 96;

                var number = new Label
                {
                    AutoSize = false,
                    Width = 34,
                    Dock = DockStyle.Left,
                    TextAlign = ContentAlignment.TopCenter,
                    Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                    ForeColor = Muted,
                    Text = index.ToString()
                };

                var card = new RoundedPanel
                {
                    Dock = DockStyle.Fill,
                    FillColor = CardColor,
                    BorderColor = Border,
                    Radius = 16,
                    Padding = new Padding(16, 14, 16, 14)
                };

                var grid = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 2,
                    RowCount = 1,
                    BackColor = Color.Transparent
                };
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

                var left = BuildField(term, "THUẬT NGỮ");
                var right = BuildField(def, "ĐỊNH NGHĨA");

                left.Margin = new Padding(0, 0, 10, 0);
                right.Margin = new Padding(10, 0, 0, 0);

                grid.Controls.Add(left, 0, 0);
                grid.Controls.Add(right, 1, 0);

                card.Controls.Add(grid);

                Controls.Add(card);
                Controls.Add(number);
            }

            private static RoundedPanel BuildField(string value, string caption)
            {
                var p = new RoundedPanel
                {
                    Dock = DockStyle.Fill,
                    FillColor = Field,
                    BorderColor = Color.FromArgb(0, 0, 0, 0),
                    Radius = 14,
                    Padding = new Padding(14, 10, 14, 10)
                };

                var lblCap = new Label
                {
                    Dock = DockStyle.Bottom,
                    Height = 20,
                    TextAlign = ContentAlignment.BottomLeft,
                    Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                    ForeColor = Muted,
                    Text = caption
                };

                var lblValue = new Label
                {
                    Dock = DockStyle.Fill,
                    AutoSize = false,
                    TextAlign = ContentAlignment.TopLeft,
                    Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
                    ForeColor = TextColor,
                    Text = value
                };

                p.Controls.Add(lblValue);
                p.Controls.Add(lblCap);
                return p;
            }
        }

        // ===== Buttons style =====
        private static void StylePrimaryPill(Button b)
        {
            b.AutoSize = false;
            b.Width = 120;
            b.Height = 40;
            b.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
            b.BackColor = Primary;
            b.ForeColor = Color.White;

            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.Cursor = Cursors.Hand;
            b.Margin = new Padding(10, 0, 0, 0);

            RoundButton(b, 12);
        }

        private static void StyleSecondaryPill(Button b)
        {
            b.AutoSize = false;
            b.Width = 120;
            b.Height = 40;
            b.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
            b.BackColor = Color.White;
            b.ForeColor = TextColor;

            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = Border;
            b.Cursor = Cursors.Hand;
            b.Margin = new Padding(10, 0, 0, 0);

            RoundButton(b, 12);
        }


        private static void RoundButton(Button b, int radius)
        {
            void Apply()
            {
                if (b.Width <= 2 || b.Height <= 2) return;
                b.Region = Region.FromHrgn(NativeRoundRectRgn.Create(0, 0, b.Width, b.Height, radius, radius));
            }

            b.SizeChanged += (_, __) => Apply();
            Apply();
        }

        // ===== Watermark =====
        private void ApplyWatermarks()
        {
            ApplyWatermark(txtTitle, TitleWatermark);
            ApplyWatermark(txtRaw, RawWatermark);
            ApplyWatermark(txtTD_Custom, TdCustomWatermark);
            ApplyWatermark(txtCard_Custom, CardCustomWatermark);
        }

        private static void ApplyWatermark(TextBox tb, string watermark)
        {
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
                    tb.ForeColor = TextColor;
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

        private static void EnableDoubleBuffering(Control c)
        {
            try
            {
                var prop = typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
                prop?.SetValue(c, true, null);
            }
            catch { }
        }

        // ===== RoundedPanel =====
        private sealed class RoundedPanel : Panel
        {
            public int Radius { get; set; } = 14;
            public Color FillColor { get; set; } = Color.White;
            public Color BorderColor { get; set; } = Color.FromArgb(229, 231, 235);
            public float BorderThickness { get; set; } = 1f;

            public RoundedPanel()
            {
                BackColor = Color.Transparent;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
                UpdateStyles();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                // Only clear for non-dialog panels
                if (!(Parent is OverlayPanel))
                {
                    var bg = Parent?.BackColor ?? Color.White;
                    if (bg.A == 0 || bg == Color.Transparent)
                        bg = FindForm()?.BackColor ?? Color.White;
                    e.Graphics.Clear(bg);
                }

                const int inset = 2;
                var rect = new Rectangle(inset, inset, Width - 1 - inset * 2, Height - 1 - inset * 2);
                if (rect.Width <= 0 || rect.Height <= 0) return;

                using var path = RoundPath(rect, Radius);
                using var fill = new SolidBrush(FillColor);
                using var pen = new Pen(BorderColor, BorderThickness)
                {
                    Alignment = System.Drawing.Drawing2D.PenAlignment.Inset
                };

                e.Graphics.FillPath(fill, path);

                if (BorderColor.A > 0 && BorderThickness > 0)
                    e.Graphics.DrawPath(pen, path);

                base.OnPaint(e);
            }


            private static System.Drawing.Drawing2D.GraphicsPath RoundPath(Rectangle r, int radius)
            {
                var p = new System.Drawing.Drawing2D.GraphicsPath();
                int d = radius * 2;

                p.AddArc(r.X, r.Y, d, d, 180, 90);
                p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                p.CloseFigure();
                return p;
            }
        }

        private static class NativeRoundRectRgn
        {
            [System.Runtime.InteropServices.DllImport("gdi32.dll")]
            private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect,
                int nBottomRect, int nWidthEllipse, int nHeightEllipse);

            public static IntPtr Create(int l, int t, int r, int b, int w, int h)
                => CreateRoundRectRgn(l, t, r, b, w, h);
        }
    }
}