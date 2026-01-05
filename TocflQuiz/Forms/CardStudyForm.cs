using System.Drawing;
using System.Windows.Forms;

namespace TocflQuiz.Forms
{
    public sealed partial class CardStudyForm : Form
    {
        public CardStudyForm()
        {
            Text = "Thẻ ghi nhớ (Study)";
            StartPosition = FormStartPosition.CenterParent;
            Width = 1000;
            Height = 650;
            MinimumSize = new Size(820, 520);

            Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                Text = "Study Form\n\n(Tiếp theo: UI xem thẻ + lật thẻ giống Quizlet)"
            });
        }
    }
}
