using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TocflQuiz.Forms
{
    public sealed partial class VocabToastForm : Form
    {
        private readonly System.Windows.Forms.Timer _closeTimer = new System.Windows.Forms.Timer();
        private int _cornerRadius = 16;
        private bool _positionedOnce = false;

        public VocabToastForm(string han, string pinyin, string meaning, int showSeconds)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;

            Width = 460;
            Height = 140;

            BackColor = Color.FromArgb(40, 40, 50);

            // ===== Layout: Left = Han, Right = Pinyin(top) + Meaning(bottom) =====
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16, 12, 16, 12),
                ColumnCount = 2,
                RowCount = 1
            };

            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            var left = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = new Padding(18, 0, 0, 0) // đẩy chữ Hán qua phải nhẹ
            };

            var right = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(12, 2, 0, 0)
            };
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var lblHan = new Label
            {
                Text = han,
                Dock = DockStyle.Fill,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.White,
                Font = MakeFont("DFKai-SB", 34f, FontStyle.Bold)
            };

            var lblPy = new Label
            {
                Text = pinyin,
                Dock = DockStyle.Fill,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(210, 210, 220),
                Font = new Font("Segoe UI", 12f, FontStyle.Italic)
            };

            var lblMean = new Label
            {
                Text = meaning,
                Dock = DockStyle.Fill,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(210, 210, 220),
                Font = new Font("Segoe UI", 12f, FontStyle.Regular)
            };

            left.Controls.Add(lblHan);
            right.Controls.Add(lblPy, 0, 0);
            right.Controls.Add(lblMean, 0, 1);

            root.Controls.Add(left, 0, 0);
            root.Controls.Add(right, 1, 0);

            Controls.Add(root);

            // Rounded corners
            SetRoundedRegion();
            Resize += (_, __) => SetRoundedRegion();

            // Auto close
            _closeTimer.Interval = Math.Max(1, showSeconds) * 1000;
            _closeTimer.Tick += (_, __) => Close();
        }

        // ✅ không activate (no focus)
        protected override bool ShowWithoutActivation => true;

        // ✅ ép Windows không kích hoạt cửa sổ khi show
        protected override CreateParams CreateParams
        {
            get
            {
                const int WS_EX_TOOLWINDOW = 0x00000080;
                const int WS_EX_NOACTIVATE = 0x08000000;

                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
                return cp;
            }
        }

        // ✅ set vị trí TRƯỚC khi visible để không bị “chớp” ở chỗ khác
        protected override void SetVisibleCore(bool value)
        {
            if (value && !_positionedOnce)
            {
                _positionedOnce = true;
                PositionBottomRight();
            }
            base.SetVisibleCore(value);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _closeTimer.Start();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _closeTimer.Stop();
            _closeTimer.Dispose();
            base.OnFormClosed(e);
        }

        private void PositionBottomRight()
        {
            // màn hình tại vị trí chuột
            var wa = Screen.FromPoint(Cursor.Position).WorkingArea;
            Location = new Point(wa.Right - Width - 16, wa.Bottom - Height - 16);
        }

        private void SetRoundedRegion()
        {
            using var path = new GraphicsPath();
            int r = Math.Max(0, _cornerRadius);
            var rect = new Rectangle(0, 0, Width, Height);

            if (r <= 0)
            {
                Region = new Region(rect);
                return;
            }

            int d = r * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();

            Region = new Region(path);
        }

        private static Font MakeFont(string name, float size, FontStyle style)
        {
            try { return new Font(name, size, style); }
            catch { return new Font("Segoe UI", size, style); }
        }
    }
}
