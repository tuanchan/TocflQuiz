using System.Drawing;
using System.Windows.Forms;

namespace TocflQuiz.Forms
{
    public sealed partial class CardImportForm : Form
    {
        public CardImportForm()
        {
            Text = "Nhập liệu nhanh (Import)";
            StartPosition = FormStartPosition.CenterParent;
            Width = 1000;
            Height = 650;
            MinimumSize = new Size(820, 520);

            Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                Text = "Import Form\n\n(Tiếp theo: TextBox lớn để dán dữ liệu Tab / NewLine giống ảnh 3)"
            });
        }
    }
}
