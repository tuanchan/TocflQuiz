using System;
using System.Drawing;
using System.Windows.Forms;

namespace TocflQuiz.Controls
{
    public sealed partial class ToggleSwitch : Control
    {
        public event EventHandler? CheckedChanged;

        private bool _checked;

        public bool Checked
        {
            get => _checked;
            set
            {
                if (_checked == value) return;
                _checked = value;
                Invalidate();
                CheckedChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint, true);

            Cursor = Cursors.Hand;
            Size = new Size(44, 22);
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            Checked = !Checked;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            var radius = rect.Height / 2;

            var bg = Checked ? Color.FromArgb(90, 120, 255) : Color.FromArgb(210, 210, 210);
            using var bgBrush = new SolidBrush(bg);

            using (var path = RoundedRect(rect, radius))
                g.FillPath(bgBrush, path);

            // knob
            int knobSize = rect.Height - 4;
            int knobX = Checked ? rect.Width - knobSize - 2 : 2;
            var knobRect = new Rectangle(knobX, 2, knobSize, knobSize);

            using var knobBrush = new SolidBrush(Color.White);
            using var knobPath = RoundedRect(knobRect, knobRect.Height / 2);
            g.FillPath(knobBrush, knobPath);
        }

        private static System.Drawing.Drawing2D.GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
