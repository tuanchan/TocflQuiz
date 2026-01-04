using System.Drawing;
using System.Windows.Forms;
using TocflQuiz.Models;

namespace TocflQuiz.Controls.Features
{
    public sealed partial class FeatureStubControl : UserControl
    {
        private readonly Label _title = new();
        private readonly Label _sub = new();
        private readonly Label _info = new();
        private readonly Label _body = new();

        public FeatureStubControl(string title, string subtitle, string body = "(Chưa làm UI/logic)")
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            Padding = new Padding(24);

            _title.Text = title;
            _title.Dock = DockStyle.Top;
            _title.Height = 46;
            _title.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
            _title.ForeColor = Color.FromArgb(35, 35, 35);

            _sub.Text = subtitle;
            _sub.Dock = DockStyle.Top;
            _sub.Height = 26;
            _sub.Font = new Font("Segoe UI", 10.5F, FontStyle.Regular);
            _sub.ForeColor = Color.FromArgb(90, 90, 90);

            _info.Text = "Học phần: (chưa chọn)";
            _info.Dock = DockStyle.Top;
            _info.Height = 34;
            _info.Padding = new Padding(0, 8, 0, 0);
            _info.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            _info.ForeColor = Color.FromArgb(70, 130, 180);

            _body.Text = body;
            _body.Dock = DockStyle.Fill;
            _body.TextAlign = ContentAlignment.MiddleCenter;
            _body.Font = new Font("Segoe UI", 12F, FontStyle.Regular);
            _body.ForeColor = Color.FromArgb(110, 110, 110);

            Controls.Add(_body);
            Controls.Add(_info);
            Controls.Add(_sub);
            Controls.Add(_title);
        }

        public void BindSelectedSet(CardSet? set)
        {
            if (set == null)
            {
                _info.Text = "Học phần: (chưa chọn)";
                return;
            }

            var count = set.Items?.Count ?? 0;
            _info.Text = $"Học phần: {set.Title}  •  {count} thẻ";
        }
    }
}
