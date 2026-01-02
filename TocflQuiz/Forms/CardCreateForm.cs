using System.Drawing;
using System.Windows.Forms;

namespace TocflQuiz.Forms
{
    public sealed partial class CardCreateForm : Form
    {
        private readonly Button btnManual = new();
        private readonly Button btnImport = new();

        public CardCreateForm()
        {
            Text = "Tạo học phần mới (Create)";
            StartPosition = FormStartPosition.CenterParent;
            Width = 1100;
            Height = 700;
            MinimumSize = new Size(900, 560);
            Font = new Font("Segoe UI", 9F);

            BuildUi();
        }

        private void BuildUi()
        {
            var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16), BackColor = Color.FromArgb(245, 245, 245) };

            var header = new Panel { Dock = DockStyle.Top, Height = 70, BackColor = Color.Transparent };

            var title = new Label
            {
                Dock = DockStyle.Left,
                AutoSize = false,
                Width = 500,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                Text = "Tạo một học phần mới"
            };

            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 420,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 10, 0, 0)
            };

            StyleAction(btnImport, "📥 Nhập liệu nhanh");
            StyleAction(btnManual, "✍️ Nhập thủ công");

            // ĐÚNG Ý BẠN: nhập liệu nằm trong Create (bấm nút mở form)
            btnManual.Click += (_, __) =>
                MessageBox.Show("Manual Entry (sẽ làm giống ảnh 2 khi bạn bảo 'tiếp tục')");

            btnImport.Click += (_, __) => new CardImportForm().Show(this);

            actions.Controls.Add(btnImport);
            actions.Controls.Add(btnManual);

            header.Controls.Add(actions);
            header.Controls.Add(title);

            var content = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(16) };
            content.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 12F, FontStyle.Regular),
                ForeColor = Color.FromArgb(70, 70, 70),
                Text = "Create Form (placeholder)\n\n- Bấm 'Nhập thủ công' (Manual) để tạo thẻ như ảnh 2\n- Bấm 'Nhập liệu nhanh' (Import) để dán dữ liệu như ảnh 3\n\n(Khi bạn nói 'tiếp tục' mình mới build UI chi tiết)"
            });

            root.Controls.Add(content);
            root.Controls.Add(header);
            Controls.Add(root);
        }

        private static void StyleAction(Button b, string text)
        {
            b.Text = text;
            b.AutoSize = false;
            b.Width = 190;
            b.Height = 42;
            b.Margin = new Padding(8, 0, 0, 0);
            b.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            b.BackColor = Color.White;
            b.ForeColor = Color.FromArgb(40, 40, 40);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = Color.FromArgb(225, 225, 225);
            b.Cursor = Cursors.Hand;
        }
    }
}
