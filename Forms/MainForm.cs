using System;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using LteTestStation.Database;
using LteTestStation.Hardware;
using LteTestStation.Models;
using LteTestStation.Services;

namespace LteTestStation.Forms
{
    public class MainForm : Form
    {
        private readonly LoggerService _logger;
        private readonly DatabaseManager _db;
        private TestEngine _engine;
        private MockLteDeviceSimulator _simulator;
        private SerialCommunicator? _serialComm;

        // UI Controls - Header
        private ComboBox _cboModel = null!;
        private TextBox _txtOperator = null!;
        private CheckBox _chkSimMode = null!;
        private ComboBox _cboFailInject = null!;
        private Label _lblDbStatus = null!;
        private Button _btnWeeklyReport = null!;
        private Button _btnDbConfig = null!;

        // UI Controls - Banner
        private Panel _pnlBanner = null!;
        private Label _lblBannerStatus = null!;
        private Label _lblBannerCycleTime = null!;
        private Label _lblBannerDetail = null!;

        // UI Controls - Action Dock
        private TextBox _txtBarcode = null!;
        private Button _btnStartTest = null!;
        private Button _btnAbortTest = null!;
        private CheckBox _chkStopOnFail = null!;
        private Button _btnGenSn = null!;

        // UI Controls - Grid & Log
        private DataGridView _dgvSteps = null!;
        private RichTextBox _rtbLog = null!;

        // UI Controls - KPI Cards
        private Label _lblStatTested = null!;
        private Label _lblStatPassed = null!;
        private Label _lblStatFailed = null!;
        private Label _lblStatYield = null!;
        private Label _lblStatAvgTime = null!;

        private int _shiftTested = 0;
        private int _shiftPassed = 0;
        private int _shiftFailed = 0;
        private double _totalCycleTimeSum = 0;

        private System.Windows.Forms.Timer _timerStopwatch = null!;
        private DateTime _testStartTime;

        public MainForm()
        {
            _logger = new LoggerService();
            _db = new DatabaseManager();
            _simulator = new MockLteDeviceSimulator();
            _engine = new TestEngine(_simulator, _db, _logger);

            InitializeComponent();
            SetupEventHandlers();
            CheckDatabaseConnectionAsync();
        }

        private void InitializeComponent()
        {
            Text = "MTE LTE Device Automated Test Station - Production Suite v2.8";
            Size = new Size(1360, 880);
            MinimumSize = new Size(1180, 740);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(241, 245, 249); // Modern Cool Slate Slate-100
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            // ==============================================================
            // 1. TOP HEADER - SLEEK DARK OBSIDIAN INDUSTRIAL BAR (#0F172A)
            // ==============================================================
            var pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 84,
                BackColor = Color.FromArgb(15, 23, 42), // Slate-900
                Padding = new Padding(20, 10, 20, 10)
            };

            var lblBadge = new Label
            {
                Text = "LTE AUTOMATED TEST SYSTEM",
                ForeColor = Color.FromArgb(56, 189, 248), // Sky-400
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                Location = new Point(20, 10),
                AutoSize = true
            };

            var lblTitle = new Label
            {
                Text = "⚡ MTE MANUFACTURING TEST STATION",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 13.5f, FontStyle.Bold),
                Location = new Point(19, 25),
                AutoSize = true
            };

            // Model Selector Pill
            var lblModel = new Label
            {
                Text = "MODEL",
                ForeColor = Color.FromArgb(148, 163, 184),
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                Location = new Point(410, 12),
                AutoSize = true
            };
            _cboModel = new ComboBox
            {
                Location = new Point(410, 32),
                Width = 160,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(30, 41, 59),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            };
            _cboModel.Items.AddRange(new object[] { "LTE-ROUTER-V2", "5G-CPE-MODEM", "LTE-MODULE-EC25" });
            _cboModel.SelectedIndex = 0;

            // Operator Badge
            var lblOp = new Label
            {
                Text = "OPERATOR",
                ForeColor = Color.FromArgb(148, 163, 184),
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                Location = new Point(585, 12),
                AutoSize = true
            };
            _txtOperator = new TextBox
            {
                Text = "OP-088",
                Location = new Point(585, 32),
                Width = 85,
                BackColor = Color.FromArgb(30, 41, 59),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                TextAlign = HorizontalAlignment.Center
            };

            // Virtual Simulator Mode Checkbox
            _chkSimMode = new CheckBox
            {
                Text = "Virtual Simulator Mode",
                ForeColor = Color.FromArgb(250, 204, 21), // Amber-400
                Location = new Point(690, 33),
                AutoSize = true,
                Checked = true,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            };
            _chkSimMode.CheckedChanged += OnSimModeChanged;

            // Demo Injection
            var lblInject = new Label
            {
                Text = "DEMO INJECTION",
                ForeColor = Color.FromArgb(148, 163, 184),
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                Location = new Point(885, 12),
                AutoSize = true
            };
            _cboFailInject = new ComboBox
            {
                Location = new Point(885, 32),
                Width = 150,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(30, 41, 59),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };
            _cboFailInject.Items.AddRange(new object[] { "Normal (PASS)", "Force SIM Missing", "Force Low RF Signal" });
            _cboFailInject.SelectedIndex = 0;
            _cboFailInject.SelectedIndexChanged += (s, e) =>
            {
                _simulator.ForceFailureMode = _cboFailInject.SelectedIndex switch
                {
                    1 => "SIM_FAIL",
                    2 => "RF_FAIL",
                    _ => "NONE"
                };
            };

            // Action Buttons in Header
            _btnWeeklyReport = new Button
            {
                Text = "📊 Weekly Review",
                Location = new Point(1060, 22),
                Size = new Size(140, 42),
                BackColor = Color.FromArgb(2, 132, 199), // Sky-600
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnWeeklyReport.FlatAppearance.BorderSize = 0;
            _btnWeeklyReport.Click += (s, e) =>
            {
                using var reportForm = new WeeklyReportForm(_db);
                reportForm.ShowDialog();
            };

            _btnDbConfig = new Button
            {
                Text = "⚙ DB Setup",
                Location = new Point(1210, 22),
                Size = new Size(110, 42),
                BackColor = Color.FromArgb(51, 65, 85), // Slate-700
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnDbConfig.FlatAppearance.BorderSize = 0;
            _btnDbConfig.Click += OnDbConfigClicked;

            // Sub-status line for DB
            _lblDbStatus = new Label
            {
                Text = "MSSQL: Checking...",
                ForeColor = Color.FromArgb(148, 163, 184),
                Location = new Point(20, 56),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular)
            };

            pnlHeader.Controls.AddRange(new Control[] {
                lblBadge, lblTitle, _lblDbStatus, lblModel, _cboModel, lblOp, _txtOperator,
                _chkSimMode, lblInject, _cboFailInject, _btnWeeklyReport, _btnDbConfig
            });

            // ==============================================================
            // 2. GIANT INDUSTRIAL STATUS BANNER (HUD DESIGN)
            // ==============================================================
            _pnlBanner = new Panel
            {
                Dock = DockStyle.Top,
                Height = 90,
                BackColor = Color.FromArgb(30, 41, 59), // Initial Slate-800
                Padding = new Padding(25, 12, 25, 12)
            };

            _lblBannerStatus = new Label
            {
                Text = "READY TO TEST",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 30f, FontStyle.Bold),
                Location = new Point(25, 10),
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft
            };

            _lblBannerDetail = new Label
            {
                Text = "Scan or enter serial number and press Start Test",
                ForeColor = Color.FromArgb(203, 213, 225),
                Font = new Font("Segoe UI", 10f, FontStyle.Regular),
                Location = new Point(28, 58),
                AutoSize = true
            };

            // Dedicated Stopwatch Card on Right
            var pnlTimerCard = new Panel
            {
                Dock = DockStyle.Right,
                Width = 240,
                BackColor = Color.FromArgb(15, 23, 42),
                Padding = new Padding(10, 5, 10, 5)
            };

            var lblTimerTitle = new Label
            {
                Text = "CYCLE TIME (STOPWATCH)",
                ForeColor = Color.FromArgb(148, 163, 184),
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                Location = new Point(12, 8),
                AutoSize = true
            };

            _lblBannerCycleTime = new Label
            {
                Text = "0.00 s",
                ForeColor = Color.FromArgb(250, 204, 21), // Gold
                Font = new Font("Consolas", 28f, FontStyle.Bold),
                Location = new Point(10, 24),
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft
            };

            pnlTimerCard.Controls.Add(lblTimerTitle);
            pnlTimerCard.Controls.Add(_lblBannerCycleTime);

            _pnlBanner.Controls.Add(pnlTimerCard);
            _pnlBanner.Controls.Add(_lblBannerStatus);
            _pnlBanner.Controls.Add(_lblBannerDetail);

            // ==============================================================
            // 3. BARCODE DOCK & ACTION BUTTONS
            // ==============================================================
            var pnlBarcode = new Panel
            {
                Dock = DockStyle.Top,
                Height = 72,
                BackColor = Color.White,
                Padding = new Padding(20, 14, 20, 14)
            };

            var lblBarcodeIcon = new Label
            {
                Text = "█║▌ BARCODE / SN:",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Location = new Point(20, 23),
                AutoSize = true
            };

            _txtBarcode = new TextBox
            {
                Location = new Point(175, 18),
                Width = 280,
                Font = new Font("Consolas", 13.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 23, 42),
                BackColor = Color.FromArgb(248, 250, 252)
            };
            _txtBarcode.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && _btnStartTest.Enabled)
                {
                    e.SuppressKeyPress = true;
                    OnStartTestClicked(s, e);
                }
            };

            _btnGenSn = new Button
            {
                Text = "🎲 Random SN",
                Location = new Point(465, 17),
                Size = new Size(125, 38),
                BackColor = Color.FromArgb(241, 245, 249),
                ForeColor = Color.FromArgb(30, 41, 59),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnGenSn.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            _btnGenSn.Click += (s, e) => GenerateNewSerialNumber();

            _btnStartTest = new Button
            {
                Text = "▶ START TEST (Enter)",
                Location = new Point(605, 15),
                Size = new Size(190, 42),
                BackColor = Color.FromArgb(16, 185, 129), // Emerald-500
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnStartTest.FlatAppearance.BorderSize = 0;
            _btnStartTest.Click += OnStartTestClicked;

            _btnAbortTest = new Button
            {
                Text = "⏹ ABORT",
                Location = new Point(805, 15),
                Size = new Size(110, 42),
                BackColor = Color.FromArgb(239, 68, 68), // Rose-500
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Enabled = false,
                Cursor = Cursors.Hand
            };
            _btnAbortTest.FlatAppearance.BorderSize = 0;
            _btnAbortTest.Click += (s, e) => _engine.AbortTest();

            _chkStopOnFail = new CheckBox
            {
                Text = "Stop test immediately on first failure",
                Location = new Point(935, 25),
                AutoSize = true,
                Checked = true,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(71, 85, 105)
            };
            _chkStopOnFail.CheckedChanged += (s, e) => _engine.StopOnFirstFailure = _chkStopOnFail.Checked;

            pnlBarcode.Controls.AddRange(new Control[] {
                lblBarcodeIcon, _txtBarcode, _btnGenSn, _btnStartTest, _btnAbortTest, _chkStopOnFail
            });

            // ==============================================================
            // 4. MAIN SPLIT: Steps Grid on Top, Bottom: KPI Cards & Live Logs
            // ==============================================================
            var splitVertical = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                Size = new Size(1300, 600)
            };
            try { splitVertical.SplitterDistance = 370; } catch { }

            // Custom Styled Modern DataGridView
            _dgvSteps = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor = Color.FromArgb(241, 245, 249),
                EnableHeadersVisualStyles = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowTemplate = { Height = 36 },
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular)
            };

            // Header Style
            _dgvSteps.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(15, 23, 42); // Slate-900
            _dgvSteps.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(248, 250, 252);
            _dgvSteps.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            _dgvSteps.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 0, 8, 0);
            _dgvSteps.ColumnHeadersHeight = 42;
            _dgvSteps.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            // Default Row Style
            _dgvSteps.DefaultCellStyle.SelectionBackColor = Color.FromArgb(224, 231, 255); // Indigo-100
            _dgvSteps.DefaultCellStyle.SelectionForeColor = Color.FromArgb(15, 23, 42);
            _dgvSteps.DefaultCellStyle.Padding = new Padding(6, 0, 6, 0);

            // Columns definition
            _dgvSteps.Columns.Add("StepId", "#");
            _dgvSteps.Columns[0].FillWeight = 30;
            _dgvSteps.Columns[0].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            _dgvSteps.Columns.Add("StepName", "Test Step Name");
            _dgvSteps.Columns[1].FillWeight = 110;
            _dgvSteps.Columns[1].DefaultCellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);

            _dgvSteps.Columns.Add("Desc", "Description");
            _dgvSteps.Columns[2].FillWeight = 170;

            _dgvSteps.Columns.Add("Cmd", "Command");
            _dgvSteps.Columns[3].FillWeight = 85;
            _dgvSteps.Columns[3].DefaultCellStyle.Font = new Font("Consolas", 9f);
            _dgvSteps.Columns[3].DefaultCellStyle.ForeColor = Color.FromArgb(3, 105, 161);

            _dgvSteps.Columns.Add("Expected", "Expected");
            _dgvSteps.Columns[4].FillWeight = 100;
            _dgvSteps.Columns[4].DefaultCellStyle.Font = new Font("Consolas", 9f);

            _dgvSteps.Columns.Add("Actual", "Actual Response");
            _dgvSteps.Columns[5].FillWeight = 170;
            _dgvSteps.Columns[5].DefaultCellStyle.Font = new Font("Consolas", 9f);

            _dgvSteps.Columns.Add("Status", "Status");
            _dgvSteps.Columns[6].FillWeight = 80;
            _dgvSteps.Columns[6].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _dgvSteps.Columns[6].DefaultCellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);

            _dgvSteps.Columns.Add("TimeMs", "Time (ms)");
            _dgvSteps.Columns[7].FillWeight = 65;
            _dgvSteps.Columns[7].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            _dgvSteps.Columns[7].DefaultCellStyle.Font = new Font("Consolas", 9.5f);

            EnableDoubleBuffering(_dgvSteps);
            PopulateTestStepsGrid();
            splitVertical.Panel1.Controls.Add(_dgvSteps);

            // ==============================================================
            // 5. BOTTOM SECTION: KPI Production Cards on Left, Logs on Right
            // ==============================================================
            var splitBottom = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                Size = new Size(1300, 300)
            };
            try { splitBottom.SplitterDistance = 380; } catch { }

            // Modern KPI Cards Container
            var pnlKpiContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(15, 12, 15, 12)
            };

            var lblKpiHeader = new Label
            {
                Text = "📈 SHIFT PRODUCTION METRICS",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Location = new Point(15, 10),
                AutoSize = true
            };

            var flowKpis = new FlowLayoutPanel
            {
                Location = new Point(10, 35),
                Size = new Size(355, 175),
                FlowDirection = FlowDirection.LeftToRight,
                AutoScroll = false
            };

            flowKpis.Controls.Add(CreateModernKpiCard("TOTAL TESTED", out _lblStatTested, "0", Color.FromArgb(15, 23, 42), 165));
            flowKpis.Controls.Add(CreateModernKpiCard("FIRST PASS YIELD", out _lblStatYield, "0.00%", Color.FromArgb(2, 132, 199), 165));
            flowKpis.Controls.Add(CreateModernKpiCard("PASSED UNITS", out _lblStatPassed, "0", Color.FromArgb(16, 185, 129), 110));
            flowKpis.Controls.Add(CreateModernKpiCard("FAILED DEFECTS", out _lblStatFailed, "0", Color.FromArgb(239, 68, 68), 110));
            flowKpis.Controls.Add(CreateModernKpiCard("AVG CYCLE TIME", out _lblStatAvgTime, "0.00 s", Color.FromArgb(124, 58, 237), 110));

            var btnResetStats = new Button
            {
                Text = "🔄 Reset Shift Counter",
                Location = new Point(15, 220),
                Size = new Size(160, 34),
                BackColor = Color.FromArgb(241, 245, 249),
                ForeColor = Color.FromArgb(71, 85, 105),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnResetStats.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            btnResetStats.Click += (s, e) => ResetShiftStats();

            pnlKpiContainer.Controls.Add(lblKpiHeader);
            pnlKpiContainer.Controls.Add(flowKpis);
            pnlKpiContainer.Controls.Add(btnResetStats);

            splitBottom.Panel1.Controls.Add(pnlKpiContainer);

            // Live Log Terminal (Developer Console)
            var pnlTerminal = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(11, 15, 25), // Obsidian dark
                Padding = new Padding(12)
            };

            var pnlTermHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 32,
                BackColor = Color.FromArgb(11, 15, 25)
            };

            var lblTermTitle = new Label
            {
                Text = "⚡ REAL-TIME DIAGNOSTIC CONSOLE (TX / RX & LOGS)",
                ForeColor = Color.FromArgb(148, 163, 184),
                Font = new Font("Consolas", 8.5f, FontStyle.Bold),
                Location = new Point(4, 8),
                AutoSize = true
            };

            var btnClearLog = new Button
            {
                Text = "Clear",
                Dock = DockStyle.Right,
                Width = 70,
                BackColor = Color.FromArgb(30, 41, 59),
                ForeColor = Color.FromArgb(203, 213, 225),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8f),
                Cursor = Cursors.Hand
            };
            btnClearLog.FlatAppearance.BorderSize = 0;
            btnClearLog.Click += (s, e) => _rtbLog.Clear();

            var btnOpenLogFolder = new Button
            {
                Text = "📁 Open Logs",
                Dock = DockStyle.Right,
                Width = 100,
                BackColor = Color.FromArgb(30, 41, 59),
                ForeColor = Color.FromArgb(203, 213, 225),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8f),
                Cursor = Cursors.Hand
            };
            btnOpenLogFolder.FlatAppearance.BorderSize = 0;
            btnOpenLogFolder.Click += (s, e) =>
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                if (Directory.Exists(path))
                {
                    System.Diagnostics.Process.Start("explorer.exe", path);
                }
            };

            pnlTermHeader.Controls.Add(lblTermTitle);
            pnlTermHeader.Controls.Add(btnOpenLogFolder);
            pnlTermHeader.Controls.Add(btnClearLog);

            _rtbLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(11, 15, 25),
                ForeColor = Color.FromArgb(226, 232, 240),
                Font = new Font("Consolas", 9.2f, FontStyle.Regular),
                BorderStyle = BorderStyle.None,
                ReadOnly = true
            };

            pnlTerminal.Controls.Add(_rtbLog);
            pnlTerminal.Controls.Add(pnlTermHeader);
            splitBottom.Panel2.Controls.Add(pnlTerminal);

            splitVertical.Panel2.Controls.Add(splitBottom);

            // Assemble main layout
            Controls.Add(splitVertical);
            Controls.Add(pnlBarcode);
            Controls.Add(_pnlBanner);
            Controls.Add(pnlHeader);

            // Stopwatch Timer
            _timerStopwatch = new System.Windows.Forms.Timer { Interval = 40 };
            _timerStopwatch.Tick += (s, e) =>
            {
                var elapsed = DateTime.Now - _testStartTime;
                _lblBannerCycleTime.Text = $"{elapsed.TotalSeconds:F2} s";
            };

            GenerateNewSerialNumber();
        }

        private Panel CreateModernKpiCard(string title, out Label valLabel, string defaultVal, Color valColor, int width)
        {
            var pnl = new Panel
            {
                Width = width,
                Height = 68,
                BackColor = Color.FromArgb(248, 250, 252),
                Margin = new Padding(3, 4, 3, 4),
                BorderStyle = BorderStyle.FixedSingle
            };

            var lblTitle = new Label
            {
                Text = title,
                ForeColor = Color.FromArgb(100, 116, 139),
                Font = new Font("Segoe UI", 7f, FontStyle.Bold),
                Location = new Point(8, 8),
                AutoSize = true
            };

            valLabel = new Label
            {
                Text = defaultVal,
                ForeColor = valColor,
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                Location = new Point(8, 26),
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

        private void PopulateTestStepsGrid()
        {
            _dgvSteps.Rows.Clear();
            foreach (var step in _engine.TestPlan)
            {
                int idx = _dgvSteps.Rows.Add(
                    step.StepId,
                    step.StepName,
                    step.Description,
                    step.Command,
                    step.ExpectedPattern,
                    "",
                    "WAITING",
                    "0"
                );
                _dgvSteps.Rows[idx].Cells[6].Style.ForeColor = Color.FromArgb(100, 116, 139);
            }
        }

        private void SetupEventHandlers()
        {
            _logger.LogMessageReceived += msg =>
            {
                if (InvokeRequired) BeginInvoke(new Action(() => AppendLog(msg)));
                else AppendLog(msg);
            };

            _engine.StepStarted += (step, index) =>
            {
                if (InvokeRequired) BeginInvoke(new Action(() => UpdateStepUI(step, index)));
                else UpdateStepUI(step, index);
            };

            _engine.StepCompleted += (step, index) =>
            {
                if (InvokeRequired) BeginInvoke(new Action(() => UpdateStepUI(step, index)));
                else UpdateStepUI(step, index);
            };

            _engine.TestFinished += report =>
            {
                if (InvokeRequired) BeginInvoke(new Action(() => OnTestCompleted(report)));
                else OnTestCompleted(report);
            };
        }

        private void AppendLog(string message)
        {
            if (_rtbLog.IsDisposed) return;
            if (message.Contains("[ERROR]") || message.Contains("FAIL"))
            {
                _rtbLog.SelectionColor = Color.FromArgb(248, 113, 113); // Red
            }
            else if (message.Contains("PASS") || message.Contains("READY"))
            {
                _rtbLog.SelectionColor = Color.FromArgb(74, 222, 128); // Green
            }
            else if (message.Contains("[TX]"))
            {
                _rtbLog.SelectionColor = Color.FromArgb(56, 189, 248); // Cyan
            }
            else if (message.Contains("[RX]"))
            {
                _rtbLog.SelectionColor = Color.FromArgb(167, 139, 250); // Violet
            }
            else
            {
                _rtbLog.SelectionColor = Color.FromArgb(203, 213, 225); // Slate
            }
            _rtbLog.AppendText(message + Environment.NewLine);
            _rtbLog.ScrollToCaret();
        }

        private void UpdateStepUI(TestItem step, int index)
        {
            if (index >= 0 && index < _dgvSteps.Rows.Count)
            {
                var row = _dgvSteps.Rows[index];
                row.Cells[5].Value = step.ActualResponse;
                row.Cells[6].Value = step.Status.ToString();
                row.Cells[7].Value = step.ExecutionTimeMs;

                switch (step.Status)
                {
                    case TestStatus.RUNNING:
                        row.DefaultCellStyle.BackColor = Color.FromArgb(254, 243, 199); // Amber-50
                        row.Cells[6].Style.ForeColor = Color.FromArgb(180, 83, 9); // Amber-700
                        break;
                    case TestStatus.PASS:
                        row.DefaultCellStyle.BackColor = Color.FromArgb(236, 253, 245); // Emerald-50
                        row.Cells[6].Style.ForeColor = Color.FromArgb(5, 150, 105); // Emerald-600
                        break;
                    case TestStatus.FAIL:
                        row.DefaultCellStyle.BackColor = Color.FromArgb(254, 242, 242); // Rose-50
                        row.Cells[6].Style.ForeColor = Color.FromArgb(225, 29, 72); // Rose-600
                        break;
                    case TestStatus.SKIPPED:
                        row.DefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
                        row.Cells[6].Style.ForeColor = Color.FromArgb(148, 163, 184);
                        break;
                    default:
                        row.DefaultCellStyle.BackColor = Color.White;
                        row.Cells[6].Style.ForeColor = Color.FromArgb(100, 116, 139);
                        break;
                }
            }
        }

        private async void OnStartTestClicked(object? sender, EventArgs e)
        {
            string sn = _txtBarcode.Text.Trim();
            if (string.IsNullOrWhiteSpace(sn))
            {
                MessageBox.Show("Please scan or enter a Serial Number before testing.", "Serial Number Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtBarcode.Focus();
                return;
            }

            _btnStartTest.Enabled = false;
            _btnAbortTest.Enabled = true;
            _txtBarcode.Enabled = false;

            // HUD Banner transition to TESTING
            _pnlBanner.BackColor = Color.FromArgb(217, 119, 6); // Amber-600
            _lblBannerStatus.Text = "⏳ TESTING IN PROGRESS...";
            _lblBannerDetail.Text = $"Testing device SN: {sn} | Running sequence...";

            _testStartTime = DateTime.Now;
            _timerStopwatch.Start();

            PopulateTestStepsGrid();

            string model = _cboModel.SelectedItem?.ToString() ?? "LTE-ROUTER-V2";
            string op = _txtOperator.Text.Trim();

            await Task.Run(() => _engine.RunTestSequenceAsync(sn, model, "MTE-STATION-01", op));
        }

        private void OnTestCompleted(TestReportRecord report)
        {
            _timerStopwatch.Stop();
            _lblBannerCycleTime.Text = $"{report.CycleTimeSeconds:F2} s";

            if (report.OverallResult == "PASS")
            {
                _pnlBanner.BackColor = Color.FromArgb(5, 150, 105); // Emerald-600
                _lblBannerStatus.Text = "✔ PASS";
                _lblBannerDetail.Text = $"Device PASSED all { _engine.TestPlan.Count } test steps. Cycle Time: {report.CycleTimeSeconds:F2}s";
                _shiftPassed++;
            }
            else
            {
                _pnlBanner.BackColor = Color.FromArgb(220, 38, 38); // Crimson-600
                _lblBannerStatus.Text = "✖ FAIL";
                _lblBannerDetail.Text = $"Failed at step: [{report.FailedStepName}] - {report.ErrorMessage}";
                _shiftFailed++;
            }

            _shiftTested++;
            _totalCycleTimeSum += report.CycleTimeSeconds;
            UpdateShiftStatsUI();

            _btnStartTest.Enabled = true;
            _btnAbortTest.Enabled = false;
            _txtBarcode.Enabled = true;
            _txtBarcode.SelectAll();
            _txtBarcode.Focus();
        }

        private void UpdateShiftStatsUI()
        {
            _lblStatTested.Text = _shiftTested.ToString();
            _lblStatPassed.Text = _shiftPassed.ToString();
            _lblStatFailed.Text = _shiftFailed.ToString();

            double yieldRate = _shiftTested > 0 ? (double)_shiftPassed / _shiftTested * 100.0 : 0.0;
            _lblStatYield.Text = $"{yieldRate:F2}%";

            double avgTime = _shiftTested > 0 ? _totalCycleTimeSum / _shiftTested : 0.0;
            _lblStatAvgTime.Text = $"{avgTime:F2} s";
        }

        private void ResetShiftStats()
        {
            _shiftTested = 0;
            _shiftPassed = 0;
            _shiftFailed = 0;
            _totalCycleTimeSum = 0;
            UpdateShiftStatsUI();
        }

        private void GenerateNewSerialNumber()
        {
            string rand = new Random().Next(1000, 9999).ToString();
            _txtBarcode.Text = $"SN-LTE2026-{rand}";
            _txtBarcode.SelectAll();
            _txtBarcode.Focus();
        }

        private void OnSimModeChanged(object? sender, EventArgs e)
        {
            if (_chkSimMode.Checked)
            {
                _engine.Communicator = _simulator;
                _cboFailInject.Enabled = true;
                _logger.Log("[MODE] Switched to Virtual LTE Simulator Mode.");
            }
            else
            {
                _cboFailInject.Enabled = false;
                string[] ports = SerialPort.GetPortNames();
                string chosenPort = ports.Length > 0 ? ports[0] : "COM1";
                _serialComm = new SerialCommunicator(chosenPort, 115200);
                _engine.Communicator = _serialComm;
                _logger.Log($"[MODE] Switched to Physical Serial Hardware ({chosenPort} @ 115200).");
            }
        }

        private async void CheckDatabaseConnectionAsync()
        {
            bool ok = await _db.EnsureDatabaseAndTableAsync();
            if (ok)
            {
                _lblDbStatus.Text = "🟢 MSSQL: Connected (LteTestStationDb)";
                _lblDbStatus.ForeColor = Color.FromArgb(74, 222, 128); // Light Emerald
            }
            else
            {
                _lblDbStatus.Text = "🟠 MSSQL: Offline (Logs saved locally to ./Logs/)";
                _lblDbStatus.ForeColor = Color.FromArgb(251, 191, 36); // Amber
            }
        }

        private void OnDbConfigClicked(object? sender, EventArgs e)
        {
            string input = ShowInputDialog(
                "Enter MSSQL Connection String:",
                "MSSQL Configuration",
                _db.ConnectionString);

            if (!string.IsNullOrWhiteSpace(input))
            {
                _db.ConnectionString = input;
                _lblDbStatus.Text = "🟡 MSSQL: Testing connection...";
                _lblDbStatus.ForeColor = Color.FromArgb(250, 204, 21);
                CheckDatabaseConnectionAsync();
            }
        }

        private static string ShowInputDialog(string text, string caption, string defaultValue)
        {
            using var prompt = new Form
            {
                Width = 550,
                Height = 180,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = caption,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false,
                Font = new Font("Segoe UI", 9.5f)
            };
            var textLabel = new Label { Left = 20, Top = 15, Width = 500, Text = text };
            var textBox = new TextBox { Left = 20, Top = 45, Width = 490, Text = defaultValue };
            var confirmation = new Button { Text = "Save", Left = 320, Width = 90, Top = 85, DialogResult = DialogResult.OK };
            var cancel = new Button { Text = "Cancel", Left = 420, Width = 90, Top = 85, DialogResult = DialogResult.Cancel };
            prompt.Controls.AddRange(new Control[] { textLabel, textBox, confirmation, cancel });
            prompt.AcceptButton = confirmation;
            prompt.CancelButton = cancel;
            return prompt.ShowDialog() == DialogResult.OK ? textBox.Text : "";
        }
    }
}
