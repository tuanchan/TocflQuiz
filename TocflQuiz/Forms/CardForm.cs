using System.Drawing;
using System.Windows.Forms;

namespace TocflQuiz.Forms
{
    public sealed partial class CardForm : Form
    {
        private readonly Button btnStudy = new();
        private readonly Button btnCreate = new();
        

        public CardForm()
        {
            Text = "Flashcards (Card)";
            StartPosition = FormStartPosition.CenterParent;
            Width = 1000;
            Height = 700;
            MinimumSize = new Size(800, 520);
            Font = new Font("Segoe UI", 9F);

            BuildUi();
        }

        private void BuildUi()
        {
            var root = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20),
                BackColor = Color.FromArgb(245, 245, 245),
            };

            var top = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 80,
                ColumnCount = 3,
                RowCount = 1,
                Padding = new Padding(0),
            };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
           

            StyleTopButton(btnStudy, "🧠 Thẻ ghi nhớ");
            StyleTopButton(btnCreate, "✍️ Tạo học phần mới");
          

            btnStudy.Click += (_, __) => new CardStudyForm().Show(this);
            btnCreate.Click += (_, __) => new CardCreateForm().Show(this);
          

            top.Controls.Add(btnStudy, 0, 0);
            top.Controls.Add(btnCreate, 1, 0);
           

            var content = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(16),
            };

            content.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Color.FromArgb(60, 60, 60),
                Text = "CardForm (Main)\n\nBấm nút phía trên để mở form chức năng.\n(Chưa tối ưu UI, làm từng bước)"
            });

            root.Controls.Add(content);
            root.Controls.Add(top);
            Controls.Add(root);
        }

        private static void StyleTopButton(Button b, string text)
        {
            b.Text = text;
            b.Dock = DockStyle.Fill;
            b.Margin = new Padding(6);
            b.Height = 52;
            b.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
            b.BackColor = Color.White;
            b.ForeColor = Color.FromArgb(40, 40, 40);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = Color.FromArgb(225, 225, 225);
            b.Cursor = Cursors.Hand;
        }
    }
}
