using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using TocflQuiz.Forms;
using TocflQuiz.Models;
using TocflQuiz.Services;

namespace TocflQuiz.Controls.Features
{
    public sealed partial class CreateCourseFeatureControl : UserControl
    {
        private readonly Button btnManual = new();
        private readonly Button btnImport = new();

        private readonly Panel _contentHost = new();
        private CardImportForm? _embeddedImportForm;

        public event Action<CardSet?>? ImportCompleted;

        // Theme
        private static readonly Color Bg = Color.FromArgb(246, 247, 251);
        private static readonly Color CardColor = Color.White;
        private static readonly Color Border = Color.FromArgb(229, 231, 235);
        private static readonly Color TextColor = Color.FromArgb(17, 24, 39);
        private static readonly Color Muted = Color.FromArgb(107, 114, 128);
        private static readonly Color Primary = Color.FromArgb(66, 104, 255);

        public CreateCourseFeatureControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Bg;
            Font = new Font("Segoe UI", 9F);

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            UpdateStyles();

            BuildUi();
            Wire();
            ShowPlaceholder();

            // mặc định chọn import
            SetPrimary(btnImport, true);
            SetPrimary(btnManual, false);
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Bg,
                Padding = new Padding(18),
                ColumnCount = 1,
                RowCount = 2
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var header = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            EnableDoubleBuffering(header);

            var title = new Label
            {
                Dock = DockStyle.Left,
                Width = 220,
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "Tạo học phần",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = TextColor
            };

            var sub = new Label
            {
                Dock = DockStyle.Left,
                Width = 560,
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "Chọn một cách tạo học phần và nhập dữ liệu.",
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), // đậm + to
                ForeColor = Muted,
                Padding = new Padding(10, 8, 0, 0)
            };

            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 520,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 10, 0, 0),
                BackColor = Color.Transparent
            };

            // ✅ 2 nút cùng style (primary) theo yêu cầu
            StylePrimaryPill(btnImport, "Nhập liệu nhanh");
            StylePrimaryPill(btnManual, "Nhập thủ công");

            actions.Controls.Add(btnImport);
            actions.Controls.Add(btnManual);

            header.Controls.Add(actions);
            header.Controls.Add(sub);
            header.Controls.Add(title);

            var hostCard = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                FillColor = CardColor,
                BorderColor = Border,
                Radius = 14,
                Padding = new Padding(16)
            };

            _contentHost.Dock = DockStyle.Fill;
            _contentHost.BackColor = CardColor;

            hostCard.Controls.Add(_contentHost);

            root.Controls.Add(header, 0, 0);
            root.Controls.Add(hostCard, 0, 1);

            Controls.Clear();
            Controls.Add(root);
        }

        private void Wire()
        {
            btnManual.Click += (_, __) =>
            {
                SetPrimary(btnManual, true);
                SetPrimary(btnImport, false);
                ShowManualPlaceholder();
            };

            btnImport.Click += (_, __) =>
            {
                SetPrimary(btnImport, true);
                SetPrimary(btnManual, false);
                ShowImportEmbedded();
            };
        }

        private void ShowPlaceholder()
        {
            _contentHost.Controls.Clear();
            _contentHost.Controls.Add(BuildEmptyState(
                "Chọn một cách tạo học phần",
                "Nhấn “Nhập liệu nhanh” để dán dữ liệu và xem trước dạng thẻ.",
                "💡"));
        }

        private void ShowManualPlaceholder()
        {
            _contentHost.Controls.Clear();
            _contentHost.Controls.Add(BuildEmptyState(
                "Nhập thủ công",
                "Sẽ làm sau (có thể làm dạng form từng thẻ hoặc dạng bảng).",
                "✍️"));
        }

        private Control BuildEmptyState(string title, string desc, string icon)
        {
            var wrap = new Panel { Dock = DockStyle.Fill, BackColor = CardColor };
            EnableDoubleBuffering(wrap);

            var box = new RoundedPanel
            {
                // ✅ rộng hơn để không bị che chữ
                Size = new Size(920, 260),
                FillColor = Color.FromArgb(249, 250, 251),
                BorderColor = Color.FromArgb(230, 233, 238),
                Radius = 14,
                Padding = new Padding(18)
            };

            var lblIcon = new Label
            {
                AutoSize = true,
                Text = icon,
                Font = new Font("Segoe UI", 28F),
                ForeColor = Primary,
                Location = new Point(18, 18)
            };

            var lblTitle = new Label
            {
                AutoSize = false,
                Text = title,
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = TextColor,
                Location = new Point(78, 26),
                Height = 34
            };

            var lblDesc = new Label
            {
                AutoSize = false,
                Text = desc,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Muted,
                Location = new Point(78, 68),
                Height = 90
            };

            box.Controls.Add(lblIcon);
            box.Controls.Add(lblTitle);
            box.Controls.Add(lblDesc);

            wrap.Controls.Add(box);

            void LayoutBox()
            {
                int maxW = Math.Min(980, wrap.ClientSize.Width - 80);
                int maxH = Math.Min(320, wrap.ClientSize.Height - 80);

                box.Width = Math.Max(760, maxW);
                box.Height = Math.Max(240, maxH);

                box.Left = Math.Max(0, (wrap.ClientSize.Width - box.Width) / 2);
                box.Top = Math.Max(0, (wrap.ClientSize.Height - box.Height) / 2);

                // ✅ textLeft bám theo icon thật, không hard-code 78 nữa
                int gap = 18;
                int textLeft = lblIcon.Right + gap;         // <-- QUAN TRỌNG
                int textRightPad = 18;
                int textW = Math.Max(10, box.ClientSize.Width - textLeft - textRightPad);

                lblTitle.Location = new Point(textLeft, lblTitle.Top);
                lblDesc.Location = new Point(textLeft, lblDesc.Top);

                lblTitle.Width = textW;
                lblDesc.Width = textW;

                lblTitle.BringToFront();
                lblDesc.BringToFront();
            }


            wrap.Resize += (_, __) => LayoutBox();
            LayoutBox();

            return wrap;
        }


        private void ShowImportEmbedded()
        {
            DisposeEmbeddedImport();
            _contentHost.Controls.Clear();

            _embeddedImportForm = new CardImportForm
            {
                TopLevel = false,
                FormBorderStyle = FormBorderStyle.None,
                Dock = DockStyle.Fill,
                Margin = new Padding(0)
            };

            _embeddedImportForm.FormClosed += EmbeddedImportFormClosed;
            _contentHost.Controls.Add(_embeddedImportForm);
            _embeddedImportForm.Show();
        }

        private void EmbeddedImportFormClosed(object? sender, FormClosedEventArgs e)
        {
            var ok = _embeddedImportForm?.DialogResult == DialogResult.OK;

            DisposeEmbeddedImport();
            ShowPlaceholder();

            if (!ok)
            {
                ImportCompleted?.Invoke(null);
                return;
            }

            var newest = CardSetStorage.LoadAllSetsSafe().FirstOrDefault();
            ImportCompleted?.Invoke(newest);
        }

        private void DisposeEmbeddedImport()
        {
            if (_embeddedImportForm == null) return;

            try
            {
                _embeddedImportForm.FormClosed -= EmbeddedImportFormClosed;

                if (_contentHost.Controls.Contains(_embeddedImportForm))
                    _contentHost.Controls.Remove(_embeddedImportForm);

                _embeddedImportForm.Dispose();
            }
            catch { }

            _embeddedImportForm = null;
        }

        private static void StylePrimaryPill(Button b, string text)
        {
            b.Text = "  " + text;
            b.AutoSize = false;
            b.Width = 220;
            b.Height = 42;
            b.Margin = new Padding(10, 0, 0, 0);

            b.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
            b.BackColor = Primary;
            b.ForeColor = Color.White;

            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.Cursor = Cursors.Hand;

            RoundButton(b, 12);
        }

        private static void SetPrimary(Button b, bool active)
        {
            // giữ cho đúng logic highlight nếu bạn cần, nhưng vẫn primary style
            b.BackColor = Primary;
            b.ForeColor = Color.White;
            b.Invalidate();
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

        private static void EnableDoubleBuffering(Control c)
        {
            try
            {
                var prop = typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
                prop?.SetValue(c, true, null);
            }
            catch { }
        }

        // RoundedPanel: bo góc nét
        private sealed class RoundedPanel : Panel
        {
            public int Radius { get; set; } = 14;
            public Color FillColor { get; set; } = Color.White;
            public Color BorderColor { get; set; } = Color.FromArgb(229, 231, 235);
            public float BorderThickness { get; set; } = 1f;

            public RoundedPanel()
            {
                BackColor = Color.Transparent;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
                UpdateStyles();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                var bg = Parent?.BackColor ?? Color.White;
                using var bgBrush = new SolidBrush(bg);
                e.Graphics.FillRectangle(bgBrush, ClientRectangle);

                var rect = new Rectangle(0, 0, Width - 1, Height - 1);
                using var path = RoundPath(rect, Radius);
                using var fill = new SolidBrush(FillColor);
                using var pen = new Pen(BorderColor, BorderThickness);

                e.Graphics.FillPath(fill, path);
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
