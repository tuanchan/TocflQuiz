using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using TocflQuiz.Models;
using TocflQuiz.Services;

namespace TocflQuiz.Forms
{
    public sealed partial class ListForm : Form
    {
        private readonly List<QuestionGroup> _groups;
        private readonly Dictionary<string, ProgressRecord> _progressMap;
        private readonly ProgressStoreJson _store;
        private readonly SpacedRepetition _sr;
        private readonly bool _dueOnly;

        private DataGridView grid = new();
        private Button btnStart = new();
        private Button btnStartDue = new();
        private Button btnRefresh = new();
        private Label lblInfo = new();
        private ComboBox cboFilter = new();

        public ListForm(
            List<QuestionGroup> groups,
            Dictionary<string, ProgressRecord> progressMap,
            ProgressStoreJson store,
            SpacedRepetition sr,
            bool dueOnly)
        {
            _groups = groups ?? new List<QuestionGroup>();
            _progressMap = progressMap ?? new Dictionary<string, ProgressRecord>(StringComparer.OrdinalIgnoreCase);
            _store = store;
            _sr = sr;
            _dueOnly = dueOnly;

            Text = dueOnly ? "📋 Danh sách - Due" : "📋 Danh sách - New/Done/Due";
            Width = 1100;
            Height = 650;
            MinimumSize = new System.Drawing.Size(900, 500);
            StartPosition = FormStartPosition.CenterParent;
            Font = new System.Drawing.Font("Segoe UI", 9F);

            BuildUi();
            Reload();

            cboFilter.SelectedItem = _dueOnly ? "Due" : "All";
            cboFilter.Enabled = !_dueOnly;
        }

        private void BuildUi()
        {
            var mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16),
                BackColor = System.Drawing.Color.FromArgb(245, 245, 245)
            };

            var contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = System.Drawing.Color.White,
                Padding = new Padding(0)
            };

            // Grid phải add trước để ở dưới
            var gridPanel = BuildGridPanel();
            contentPanel.Controls.Add(gridPanel);

            // Toolbar add sau để ở trên
            var toolbar = BuildToolbar();
            contentPanel.Controls.Add(toolbar);

            mainPanel.Controls.Add(contentPanel);
            Controls.Add(mainPanel);
        }

        private Panel BuildToolbar()
        {
            var toolbar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 64,
                BackColor = System.Drawing.Color.FromArgb(250, 250, 250),
                Padding = new Padding(16, 12, 16, 12)
            };

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoSize = false
            };

            var buttonFont = new System.Drawing.Font("Segoe UI", 9.5F);
            var buttonHeight = 36;
            var buttonMargin = new Padding(0, 0, 8, 0);

            // Button Start (đang chọn)
            btnStart.Text = "▶ Start (đang chọn)";
            btnStart.Width = 160;
            btnStart.Height = buttonHeight;
            btnStart.Font = buttonFont;
            btnStart.Margin = buttonMargin;
            btnStart.BackColor = System.Drawing.Color.FromArgb(70, 130, 180);
            btnStart.ForeColor = System.Drawing.Color.White;
            btnStart.FlatStyle = FlatStyle.Flat;
            btnStart.FlatAppearance.BorderSize = 0;
            btnStart.Cursor = Cursors.Hand;
            btnStart.Click += (_, __) => StartSelected();

            // Button Start Due
            btnStartDue.Text = "🎲 Start (1 Due bất kỳ)";
            btnStartDue.Width = 170;
            btnStartDue.Height = buttonHeight;
            btnStartDue.Font = buttonFont;
            btnStartDue.Margin = buttonMargin;
            btnStartDue.BackColor = System.Drawing.Color.FromArgb(220, 100, 50);
            btnStartDue.ForeColor = System.Drawing.Color.White;
            btnStartDue.FlatStyle = FlatStyle.Flat;
            btnStartDue.FlatAppearance.BorderSize = 0;
            btnStartDue.Cursor = Cursors.Hand;
            btnStartDue.Click += (_, __) => StartAnyDue();

            // Button Refresh
            btnRefresh.Text = "🔄 Refresh";
            btnRefresh.Width = 110;
            btnRefresh.Height = buttonHeight;
            btnRefresh.Font = buttonFont;
            btnRefresh.Margin = buttonMargin;
            btnRefresh.BackColor = System.Drawing.Color.FromArgb(100, 100, 100);
            btnRefresh.ForeColor = System.Drawing.Color.White;
            btnRefresh.FlatStyle = FlatStyle.Flat;
            btnRefresh.FlatAppearance.BorderSize = 0;
            btnRefresh.Cursor = Cursors.Hand;
            btnRefresh.Click += (_, __) => Reload();

            // Separator
            var separator = new Panel
            {
                Width = 20,
                Height = buttonHeight,
                Margin = new Padding(4, 0, 4, 0)
            };

            // Filter Label
            var lblFilter = new Label
            {
                Text = "Lọc:",
                AutoSize = true,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Font = new System.Drawing.Font("Segoe UI", 9.5F),
                Margin = new Padding(0, 0, 6, 0),
                Padding = new Padding(0, 8, 0, 0),
                ForeColor = System.Drawing.Color.FromArgb(70, 70, 70)
            };

            // ComboBox Filter
            cboFilter.Width = 100;
            cboFilter.Height = buttonHeight;
            cboFilter.Font = buttonFont;
            cboFilter.Margin = buttonMargin;
            cboFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            cboFilter.Items.AddRange(new object[] { "All", "New", "Done", "Due" });
            cboFilter.SelectedIndexChanged += (_, __) => Reload();

            // Info Label
            lblInfo.AutoSize = true;
            lblInfo.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold);
            lblInfo.ForeColor = System.Drawing.Color.FromArgb(50, 120, 180);
            lblInfo.Margin = new Padding(12, 0, 0, 0);
            lblInfo.Padding = new Padding(0, 8, 0, 0);

            flow.Controls.Add(btnStart);
            flow.Controls.Add(btnStartDue);
            flow.Controls.Add(btnRefresh);
            flow.Controls.Add(separator);
            flow.Controls.Add(lblFilter);
            flow.Controls.Add(cboFilter);
            flow.Controls.Add(lblInfo);

            toolbar.Controls.Add(flow);
            return toolbar;
        }

        private Panel BuildGridPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16, 16, 16, 16),
                BackColor = System.Drawing.Color.White
            };

            // Tạo grid với các thiết lập cơ bản trước
            grid.Dock = DockStyle.Fill;
            grid.AutoGenerateColumns = false;

            // Thiết lập column header
            grid.ColumnHeadersVisible = true;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersHeight = 50;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            grid.EnableHeadersVisualStyles = false;

            // Style cho header
            grid.ColumnHeadersDefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(70, 130, 180);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = System.Drawing.Color.White;
            grid.ColumnHeadersDefaultCellStyle.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(10, 10, 10, 10);
            grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;

            // Các thiết lập khác
            grid.ReadOnly = true;
            grid.MultiSelect = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.BackgroundColor = System.Drawing.Color.White;
            grid.BorderStyle = BorderStyle.Fixed3D;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.RowHeadersVisible = false;
            grid.Font = new System.Drawing.Font("Segoe UI", 9F);

            // Row style
            grid.DefaultCellStyle.SelectionBackColor = System.Drawing.Color.FromArgb(200, 220, 240);
            grid.DefaultCellStyle.SelectionForeColor = System.Drawing.Color.FromArgb(30, 30, 30);
            grid.DefaultCellStyle.BackColor = System.Drawing.Color.White;
            grid.DefaultCellStyle.ForeColor = System.Drawing.Color.FromArgb(50, 50, 50);
            grid.DefaultCellStyle.Padding = new Padding(8, 6, 8, 6);
            grid.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
            grid.RowTemplate.Height = 36;
            grid.AlternatingRowsDefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(248, 248, 248);

            // Thêm columns (bỏ Số câu và Điểm)
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colFileId",
                HeaderText = "File ID",
                DataPropertyName = "FileId",
                Width = 120,
                MinimumWidth = 100
            });

            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colStatus",
                HeaderText = "Trạng thái",
                DataPropertyName = "Status",
                Width = 110,
                MinimumWidth = 90,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold)
                }
            });

            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colStage",
                HeaderText = "Stage",
                DataPropertyName = "Stage",
                Width = 80,
                MinimumWidth = 60,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });

            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colNextDue",
                HeaderText = "Ngày đến hạn",
                DataPropertyName = "NextDue",
                Width = 130,
                MinimumWidth = 110
            });

            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colLastAttempt",
                HeaderText = "Lần làm gần nhất",
                DataPropertyName = "LastAttempt",
                Width = 160,
                MinimumWidth = 130
            });

            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colPdf",
                HeaderText = "File PDF",
                DataPropertyName = "Pdf",
                Width = 200,
                MinimumWidth = 150,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });

            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colMp3",
                HeaderText = "File MP3",
                DataPropertyName = "Mp3",
                Width = 200,
                MinimumWidth = 150,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });

            grid.CellFormatting += Grid_CellFormatting;
            grid.DoubleClick += (_, __) => StartSelected();

            panel.Controls.Add(grid);
            return panel;
        }

        private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.ColumnIndex >= 0 && e.ColumnIndex < grid.Columns.Count)
            {
                var colName = grid.Columns[e.ColumnIndex].Name;
                if (colName == "colStatus" && e.Value != null)
                {
                    var status = e.Value.ToString();
                    switch (status)
                    {
                        case "Due":
                            e.CellStyle.BackColor = System.Drawing.Color.FromArgb(255, 200, 200);
                            e.CellStyle.ForeColor = System.Drawing.Color.FromArgb(180, 0, 0);
                            break;
                        case "Done":
                            e.CellStyle.BackColor = System.Drawing.Color.FromArgb(200, 240, 200);
                            e.CellStyle.ForeColor = System.Drawing.Color.FromArgb(0, 120, 0);
                            break;
                        case "New":
                            e.CellStyle.BackColor = System.Drawing.Color.FromArgb(220, 230, 250);
                            e.CellStyle.ForeColor = System.Drawing.Color.FromArgb(50, 100, 180);
                            break;
                    }
                }
            }
        }

        private void Reload()
        {
            var today = DateTime.Now.Date;

            var rows = _groups.Select(g =>
            {
                var has = _progressMap.TryGetValue(g.FileId, out var pr) && pr.IsDone;
                var due = has && pr!.IsDue(today);

                var status = has ? (due ? "Due" : "Done") : "New";
                var stage = has ? pr!.Stage : 0;
                var nextDue = has ? (pr!.NextDue == DateTime.MinValue ? "" : pr.NextDue.ToString("yyyy-MM-dd")) : "";
                var lastAttempt = has && pr!.LastAttempt.HasValue ? pr.LastAttempt.Value.ToString("yyyy-MM-dd HH:mm") : "";

                return new RowVm
                {
                    FileId = g.FileId,
                    Status = status,
                    Stage = stage,
                    NextDue = nextDue,
                    LastAttempt = lastAttempt,
                    Pdf = ShortPath(g.PdfQuestionPath),
                    Mp3 = ShortPath(g.Mp3Path),
                    Group = g
                };
            }).ToList();

            var filter = cboFilter.SelectedItem?.ToString() ?? (_dueOnly ? "Due" : "All");

            rows = filter switch
            {
                "New" => rows.Where(r => r.Status == "New").ToList(),
                "Done" => rows.Where(r => r.Status == "Done").ToList(),
                "Due" => rows.Where(r => r.Status == "Due").ToList(),
                _ => rows
            };

            rows = rows
                .OrderByDescending(r => r.Status == "Due")
                .ThenBy(r => r.Status)
                .ThenBy(r => r.NextDue)
                .ThenBy(r => r.FileId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            grid.DataSource = rows;

            lblInfo.Text = $"📊 Tổng: {rows.Count} đề";
        }

        private static string ShortPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "";
            try
            {
                return System.IO.Path.GetFileName(path);
            }
            catch { return path!; }
        }

        private void StartSelected()
        {
            if (grid.CurrentRow?.DataBoundItem is not RowVm vm)
            {
                MessageBox.Show("Vui lòng chọn 1 dòng trước.", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var list = _groups;
            var idx = list.FindIndex(x => string.Equals(x.FileId, vm.Group.FileId, StringComparison.OrdinalIgnoreCase));
            if (idx < 0) idx = 0;
            var qf = new QuizForm(vm.Group, _progressMap, _store, _sr, list, idx);

            qf.FormClosed += (_, __) => Reload();
            qf.Show(this);
        }

        private void StartAnyDue()
        {
            var today = DateTime.Now.Date;
            var dueList = _groups
                .Where(g => _progressMap.TryGetValue(g.FileId, out var pr) && pr.IsDone && pr.IsDue(today))
                .ToList();

            if (dueList.Count == 0)
            {
                MessageBox.Show("Không có đề đến hạn.", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var rnd = new Random();
            var gsel = dueList[rnd.Next(dueList.Count)];

            var idx = dueList.FindIndex(x => string.Equals(x.FileId, gsel.FileId, StringComparison.OrdinalIgnoreCase));
            if (idx < 0) idx = 0;
            var qf = new QuizForm(gsel, _progressMap, _store, _sr, dueList, idx);

            qf.FormClosed += (_, __) => Reload();
            qf.Show(this);
        }

        private sealed class RowVm
        {
            public string FileId { get; set; } = "";
            public string Status { get; set; } = "";
            public int Stage { get; set; }
            public string NextDue { get; set; } = "";
            public string LastAttempt { get; set; } = "";
            public string Pdf { get; set; } = "";
            public string Mp3 { get; set; } = "";

            public QuestionGroup Group { get; set; } = new();
        }
    }
}