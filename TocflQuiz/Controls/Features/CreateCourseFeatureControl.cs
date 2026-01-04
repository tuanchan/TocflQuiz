using System;
using System.Drawing;
using System.Linq;
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

        public CreateCourseFeatureControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9F);

            BuildUi();
            Wire();
            ShowPlaceholder();
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(0),
                Margin = new Padding(0),
                ColumnCount = 1,
                RowCount = 2
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var topBar = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(0, 8, 0, 8),
                Margin = new Padding(0)
            };

            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 420,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            StyleAction(btnImport, "📥 Nhập liệu nhanh");
            StyleAction(btnManual, "✍️ Nhập thủ công");

            actions.Controls.Add(btnImport);
            actions.Controls.Add(btnManual);
            topBar.Controls.Add(actions);

            _contentHost.Dock = DockStyle.Fill;
            _contentHost.BackColor = Color.White;
            _contentHost.Padding = new Padding(0);
            _contentHost.Margin = new Padding(0);

            root.Controls.Add(topBar, 0, 0);
            root.Controls.Add(_contentHost, 0, 1);

            Controls.Clear();
            Controls.Add(root);
        }

        private void Wire()
        {
            btnManual.Click += (_, __) =>
            {
                MessageBox.Show("Manual Entry (sẽ làm sau).");
            };

            btnImport.Click += (_, __) => ShowImportEmbedded();
        }

        private void ShowPlaceholder()
        {
            _contentHost.Controls.Clear();
            _contentHost.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 11.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(80, 80, 80),
                Text = "Chọn một cách tạo học phần ở 2 nút phía trên."
            });
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

            // lấy học phần mới nhất (storage sort newest first)
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

        private static void StyleAction(Button b, string text)
        {
            b.Text = text;
            b.AutoSize = false;
            b.Width = 190;
            b.Height = 40;
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
