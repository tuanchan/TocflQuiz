using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using TocflQuiz.Services;

namespace TocflQuiz.Forms
{
    public sealed partial class VocabToastSettingsForm : Form
    {
        private readonly CheckBox _chkEnabled = new();
        private readonly NumericUpDown _numEveryMinutes = new();
        private readonly NumericUpDown _numShowSeconds = new();

        private readonly Button _btnOk = new();
        private readonly Button _btnCancel = new();

        public VocabToastSettings Result { get; private set; }

        public VocabToastSettingsForm(VocabToastSettings current)
        {
            Result = (current ?? new VocabToastSettings()).Clone();

            Text = "Cài đặt nhắc từ";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            Width = 420;
            Height = 260;
            Font = new Font("Segoe UI", 9.5f);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(14),
                ColumnCount = 2,
                RowCount = 5
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));

            _chkEnabled.Text = "Bật nhắc từ";
            _chkEnabled.Checked = Result.Enabled;
            _chkEnabled.Dock = DockStyle.Fill;

            var lblEvery = new Label { Text = "Mỗi (phút):", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill };
            _numEveryMinutes.DecimalPlaces = 2;
            _numEveryMinutes.Minimum = 0.01M;
            _numEveryMinutes.Maximum = 999M;
            _numEveryMinutes.Value = ClampDecimal((decimal)Result.EveryMinutes, 0.01M, 999M);
            _numEveryMinutes.Dock = DockStyle.Fill;

            var lblShow = new Label { Text = "Hiện (giây):", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill };
            _numShowSeconds.Minimum = 1;
            _numShowSeconds.Maximum = 120;
            _numShowSeconds.Value = ClampDecimal(Result.ShowSeconds, 1, 120);
            _numShowSeconds.Dock = DockStyle.Fill;

            var btnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };

            _btnOk.Text = "OK";
            _btnOk.Width = 90;
            _btnOk.Height = 34;
            _btnOk.Click += (_, __) => OnOk();

            _btnCancel.Text = "Hủy";
            _btnCancel.Width = 90;
            _btnCancel.Height = 34;
            _btnCancel.Click += (_, __) => { DialogResult = DialogResult.Cancel; Close(); };

            btnPanel.Controls.Add(_btnOk);
            btnPanel.Controls.Add(_btnCancel);

            // Layout
            root.Controls.Add(_chkEnabled, 0, 0);
            root.SetColumnSpan(_chkEnabled, 2);

            root.Controls.Add(lblEvery, 0, 1);
            root.Controls.Add(_numEveryMinutes, 1, 1);

            root.Controls.Add(lblShow, 0, 2);
            root.Controls.Add(_numShowSeconds, 1, 2);

            root.Controls.Add(new Label { Text = "", AutoSize = true }, 0, 3);
            root.Controls.Add(new Label { Text = "", AutoSize = true }, 1, 3);

            root.Controls.Add(btnPanel, 0, 4);
            root.SetColumnSpan(btnPanel, 2);

            Controls.Add(root);

            AcceptButton = _btnOk;
            CancelButton = _btnCancel;
        }

        private void OnOk()
        {
            Result.Enabled = _chkEnabled.Checked;
            Result.EveryMinutes = (double)_numEveryMinutes.Value;
            Result.ShowSeconds = (int)_numShowSeconds.Value;

            DialogResult = DialogResult.OK;
            Close();
        }

        private static decimal ClampDecimal(decimal value, decimal min, decimal max)
            => value < min ? min : (value > max ? max : value);

        private static decimal ClampDecimal(int value, int min, int max)
            => value < min ? min : (value > max ? max : value);
    }
}
