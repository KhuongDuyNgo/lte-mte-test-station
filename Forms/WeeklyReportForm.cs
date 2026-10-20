using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using LteTestStation.Database;
using LteTestStation.Models;

namespace LteTestStation.Forms
{
    public class WeeklyReportForm : Form
    {
        private readonly DatabaseManager _db;
        private DateTimePicker _dtpFrom = null!;
        private DateTimePicker _dtpTo = null!;
        private Button _btnQuery = null!;
        private Button _btnExportCsv = null!;
        private Label _lblTotal = null!;
        private Label _lblPassed = null!;
        private Label _lblFailed = null!;
        private Label _lblYieldRate = null!;
        private Label _lblAvgCycleTime = null!;
        private DataGridView _dgvRecords = null!;
        private DataGridView _dgvPareto = null!;

        public WeeklyReportForm(DatabaseManager db)
        {
            _db = db;
            InitializeComponent();
            Shown += async (s, e) => await LoadDataAsync();
        }

        private void InitializeComponent()
        {
            Text = "MTE Weekly Report Review - Yield & Pareto Analysis Suite";
            Size = new Size(1100, 720);
            MinimumSize = new Size(950, 600);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(241, 245, 249); // Slate-100
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            // ==============================================================
            // 1. TOP FILTER BAR
            // ==============================================================
            var pnlTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 68,
                BackColor = Color.White,
                Padding = new Padding(20, 14, 20, 14)
            };

            var lblFrom = new Label { Text = "DATE RANGE:", AutoSize = true, Location = new Point(20, 24), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = Color.FromArgb(100, 116, 139) };
            _dtpFrom = new DateTimePicker { Location = new Point(105, 20), Width = 135, Value = DateTime.Today.AddDays(-7), Font = new Font("Segoe UI", 9.5f) };

            var lblTo = new Label { Text = "TO", AutoSize = true, Location = new Point(250, 24), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = Color.FromArgb(100, 116, 139) };
            _dtpTo = new DateTimePicker { Location = new Point(280, 20), Width = 135, Value = DateTime.Today.AddDays(1), Font = new Font("Segoe UI", 9.5f) };

            _btnQuery = new Button
            {
                Text = "🔍 Run Review Query",
                Location = new Point(435, 17),
                Size = new Size(175, 38),
                BackColor = Color.FromArgb(2, 132, 199), // Sky-600
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnQuery.FlatAppearance.BorderSize = 0;
            _btnQuery.Click += async (s, e) => await LoadDataAsync();

            _btnExportCsv = new Button
            {
                Text = "📥 Export Weekly CSV",
                Location = new Point(620, 17),
                Size = new Size(175, 38),
                BackColor = Color.FromArgb(16, 185, 129), // Emerald-500
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnExportCsv.FlatAppearance.BorderSize = 0;
            _btnExportCsv.Click += OnExportCsvClicked;

            pnlTop.Controls.AddRange(new Control[] { lblFrom, _dtpFrom, lblTo, _dtpTo, _btnQuery, _btnExportCsv });

            // ==============================================================
            // 2. METRICS KPI CARDS
            // ==============================================================
            var pnlCards = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 85,
                Padding = new Padding(20, 8, 20, 8),
                BackColor = Color.FromArgb(241, 245, 249)
            };

            pnlCards.Controls.Add(CreateMetricCard("TOTAL TESTED", out _lblTotal, "0", Color.FromArgb(15, 23, 42)));
            pnlCards.Controls.Add(CreateMetricCard("FIRST PASS YIELD (FPY)", out _lblYieldRate, "0.00%", Color.FromArgb(2, 132, 199)));
            pnlCards.Controls.Add(CreateMetricCard("PASSED UNITS", out _lblPassed, "0", Color.FromArgb(16, 185, 129)));
            pnlCards.Controls.Add(CreateMetricCard("FAILED DEFECTS", out _lblFailed, "0", Color.FromArgb(239, 68, 68)));
            pnlCards.Controls.Add(CreateMetricCard("AVG CYCLE TIME", out _lblAvgCycleTime, "0.00s", Color.FromArgb(124, 58, 237)));

            // ==============================================================
            // 3. MAIN SPLIT: Records on Left, Failure Pareto on Right
            // ==============================================================
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                Size = new Size(1000, 500)
            };
            try { split.SplitterDistance = 650; } catch { }

            // Left: Test Records
            var pnlRecordsWrap = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 5, 10, 20) };
            var grpRecords = new GroupBox
            {
                Text = "Historical Test Records (MSSQL Database)",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Padding = new Padding(10)
            };

            _dgvRecords = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor = Color.FromArgb(241, 245, 249),
                EnableHeadersVisualStyles = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowTemplate = { Height = 34 },
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };
            _dgvRecords.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(15, 23, 42);
            _dgvRecords.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            _dgvRecords.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _dgvRecords.ColumnHeadersHeight = 38;

            _dgvRecords.Columns.Add("SN", "Serial Number");
            _dgvRecords.Columns.Add("IMEI", "IMEI");
            _dgvRecords.Columns.Add("Model", "Model");
            _dgvRecords.Columns.Add("Result", "Result");
            _dgvRecords.Columns.Add("CycleTime", "Time (s)");
            _dgvRecords.Columns.Add("Timestamp", "Timestamp");
            _dgvRecords.Columns[3].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _dgvRecords.Columns[4].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

            EnableDoubleBuffering(_dgvRecords);
            grpRecords.Controls.Add(_dgvRecords);
            pnlRecordsWrap.Controls.Add(grpRecords);
            split.Panel1.Controls.Add(pnlRecordsWrap);

            // Right: Pareto Failure Table
            var pnlParetoWrap = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 5, 20, 20) };
            var grpPareto = new GroupBox
            {
                Text = "Pareto Defect Analysis",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Padding = new Padding(10)
            };

            _dgvPareto = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor = Color.FromArgb(241, 245, 249),
                EnableHeadersVisualStyles = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowTemplate = { Height = 34 },
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };
            _dgvPareto.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(15, 23, 42);
            _dgvPareto.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            _dgvPareto.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _dgvPareto.ColumnHeadersHeight = 38;

            _dgvPareto.Columns.Add("Step", "Failed Step Name");
            _dgvPareto.Columns.Add("Count", "Failure Count");
            _dgvPareto.Columns[1].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            _dgvPareto.Columns[1].DefaultCellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            _dgvPareto.Columns[1].DefaultCellStyle.ForeColor = Color.FromArgb(220, 38, 38);

            EnableDoubleBuffering(_dgvPareto);
            grpPareto.Controls.Add(_dgvPareto);
            pnlParetoWrap.Controls.Add(grpPareto);
            split.Panel2.Controls.Add(pnlParetoWrap);

            Controls.Add(split);
            Controls.Add(pnlCards);
            Controls.Add(pnlTop);
        }

        private Panel CreateMetricCard(string title, out Label valLabel, string defaultVal, Color valColor)
        {
            var pnl = new Panel
            {
                Width = 200,
                Height = 68,
                BackColor = Color.White,
                Margin = new Padding(0, 0, 10, 0),
                BorderStyle = BorderStyle.FixedSingle
            };

            var lblTitle = new Label
            {
                Text = title,
                ForeColor = Color.FromArgb(100, 116, 139),
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                Location = new Point(10, 8),
                AutoSize = true
            };

            valLabel = new Label
            {
                Text = defaultVal,
                ForeColor = valColor,
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                Location = new Point(10, 26),
                AutoSize = true
            };

            pnl.Controls.Add(lblTitle);
            pnl.Controls.Add(valLabel);
            return pnl;
        }

        private static void EnableDoubleBuffering(Control control)
        {
            typeof(Control).InvokeMember("DoubleBuffered",
                BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.NonPublic,
                null, control, new object[] { true });
        }

        private async Task LoadDataAsync()
        {
            DateTime fromDate = _dtpFrom.Value.Date;
            DateTime toDate = _dtpTo.Value.Date.AddDays(1).AddSeconds(-1);

            _btnQuery.Enabled = false;
            try
            {
                var stats = await _db.GetYieldStatisticsAsync(fromDate, toDate);
                var pareto = await _db.GetFailureParetoAsync(fromDate, toDate);
                var records = await _db.GetRecentRecordsAsync(100);

                _lblTotal.Text = stats.TotalTested.ToString();
                _lblPassed.Text = stats.PassedCount.ToString();
                _lblFailed.Text = stats.FailedCount.ToString();
                _lblYieldRate.Text = $"{stats.YieldRatePercent:F2}%";
                _lblAvgCycleTime.Text = $"{stats.AverageCycleTimeSeconds:F2}s";

                _dgvRecords.Rows.Clear();
                foreach (var r in records.Where(x => x.TestTimestamp >= fromDate && x.TestTimestamp <= toDate))
                {
                    int idx = _dgvRecords.Rows.Add(r.SerialNumber, r.Imei, r.ModelName, r.OverallResult, r.CycleTimeSeconds, r.TestTimestamp.ToString("yyyy-MM-dd HH:mm:ss"));
                    var row = _dgvRecords.Rows[idx];
                    row.Cells[3].Style.ForeColor = r.OverallResult == "PASS" ? Color.FromArgb(16, 185, 129) : Color.FromArgb(239, 68, 68);
                    row.Cells[3].Style.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
                }

                _dgvPareto.Rows.Clear();
                foreach (var kvp in pareto)
                {
                    _dgvPareto.Rows.Add(kvp.Key, kvp.Value);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error querying database: {ex.Message}", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                _btnQuery.Enabled = true;
            }
        }

        private void OnExportCsvClicked(object? sender, EventArgs e)
        {
            try
            {
                using var sfd = new SaveFileDialog
                {
                    Filter = "CSV File (*.csv)|*.csv",
                    FileName = $"MTE_Weekly_Review_{DateTime.Now:yyyyMMdd_HHmm}.csv"
                };

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("SerialNumber,IMEI,ModelName,Result,CycleTimeSeconds,Timestamp");
                    foreach (DataGridViewRow row in _dgvRecords.Rows)
                    {
                        if (row.IsNewRow) continue;
                        string sn = row.Cells[0].Value?.ToString() ?? "";
                        string imei = row.Cells[1].Value?.ToString() ?? "";
                        string model = row.Cells[2].Value?.ToString() ?? "";
                        string result = row.Cells[3].Value?.ToString() ?? "";
                        string ct = row.Cells[4].Value?.ToString() ?? "";
                        string ts = row.Cells[5].Value?.ToString() ?? "";
                        sb.AppendLine($"\"{sn}\",\"{imei}\",\"{model}\",\"{result}\",{ct},\"{ts}\"");
                    }

                    File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                    MessageBox.Show("Weekly report exported successfully!", "Export Completed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
