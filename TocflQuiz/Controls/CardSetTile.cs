using System;
using System.Drawing;
using System.Windows.Forms;
using TocflQuiz.Models;

namespace TocflQuiz.Controls
{
    public sealed class CardSetTile : UserControl
    {
        private readonly Panel card = new();
        private readonly Panel icon = new();
        private readonly Label lblTitle = new();
        private readonly Label lblMeta = new();

        private bool _selected;

        public CardSet? Data { get; private set; }

        public bool Selected
        {
            get => _selected;
            set
            {
                _selected = value;
                ApplyStyle();
            }
        }

        public event EventHandler? TileClick;
        public event EventHandler? TileDoubleClick;

        public CardSetTile()
        {
            Width = 180;
            Height = 180;
            Margin = new Padding(10);
            BackColor = Color.Transparent;

            BuildUi();
            WireAll(card);
            WireAll(this);
        }

        public void Bind(CardSet set)
        {
            Data = set;
            lblTitle.Text = string.IsNullOrWhiteSpace(set.Title) ? "(Untitled)" : set.Title.Trim();
            lblMeta.Text = $"{set.Items?.Count ?? 0} thuật ngữ • {set.CreatedAt:dd/MM/yyyy}";
        }

        private void BuildUi()
        {
            card.Dock = DockStyle.Fill;
            card.Padding = new Padding(14);
            card.BackColor = Color.White;
            card.BorderStyle = BorderStyle.FixedSingle;

            icon.Size = new Size(44, 44);
            icon.BackColor = Color.FromArgb(230, 244, 255);
            icon.Location = new Point(14, 14);

            // “icon” đơn giản
            var iconLbl = new Label
            {
                Dock = DockStyle.Fill,
                Text = "📚",
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI Emoji", 16F, FontStyle.Regular)
            };
            icon.Controls.Add(iconLbl);

            lblTitle.AutoSize = false;
            lblTitle.Location = new Point(14, 72);
            lblTitle.Size = new Size(150, 46);
            lblTitle.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
            lblTitle.ForeColor = Color.FromArgb(35, 35, 35);

            lblMeta.AutoSize = false;
            lblMeta.Location = new Point(14, 126);
            lblMeta.Size = new Size(150, 32);
            lblMeta.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            lblMeta.ForeColor = Color.FromArgb(110, 110, 110);

            card.Controls.Add(lblMeta);
            card.Controls.Add(lblTitle);
            card.Controls.Add(icon);

            Controls.Add(card);

            ApplyStyle();
        }

        private void ApplyStyle()
        {
            // hiệu ứng chọn: nền xanh nhạt + viền đậm hơn
            if (_selected)
            {
                card.BackColor = Color.FromArgb(242, 247, 255);
                card.Padding = new Padding(13);
                card.BorderStyle = BorderStyle.FixedSingle;
            }
            else
            {
                card.BackColor = Color.White;
                card.Padding = new Padding(14);
                card.BorderStyle = BorderStyle.FixedSingle;
            }
            Invalidate();
        }

        private void WireAll(Control c)
        {
            c.Click += (_, __) => TileClick?.Invoke(this, EventArgs.Empty);
            c.DoubleClick += (_, __) => TileDoubleClick?.Invoke(this, EventArgs.Empty);

            foreach (Control child in c.Controls)
                WireAll(child);
        }
    }
}
