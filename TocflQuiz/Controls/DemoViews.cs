using System.Drawing;
using System.Windows.Forms;

namespace TocflQuiz.Controls
{
    public sealed class DemoStudyView : UserControl
    {
        public DemoStudyView()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                Text = "DemoStudyView\n(đây là vùng content khoanh đỏ)"
            });
        }
    }

    public sealed class DemoSetListView : UserControl
    {
        public DemoSetListView()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                Text = "DemoSetListView\n(danh sách học phần dạng embedded)"
            });
        }
    }

    public sealed class DemoCreateView : UserControl
    {
        public DemoCreateView()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                Text = "DemoCreateView\n(tạo học phần dạng embedded)"
            });
        }
    }
}
