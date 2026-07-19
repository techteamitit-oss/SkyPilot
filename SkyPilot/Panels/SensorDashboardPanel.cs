using System.Drawing;
using System.Drawing.Drawing2D;
using SkyPilot.Core.Mavlink;
using SkyPilot.Log;

namespace SkyPilot.Panels;

/// <summary>
/// Unified sensor dashboard with GDI+ charts (no ScottPlot dependency).
/// </summary>
public class SensorDashboardPanel : UserControl
{
    private readonly TabControl tabControl;
    private readonly SimpleChart chartAccel;
    private readonly SimpleChart chartGyro;
    private readonly List<double> accelXData = new(), accelYData = new(), accelZData = new();
    private readonly List<double> gyroXData = new(), gyroYData = new(), gyroZData = new();
    private readonly List<double> timeData = new();
    private int _tickStart;

    private readonly ProgressBar vibBarX, vibBarY, vibBarZ;
    private readonly Label lblVibClip0, lblVibClip1, lblVibClip2;
    private readonly ProgressBar ekfVel, ekfPosH, ekfPosV, ekfComp, ekfTerrain;
    private readonly DataGridView gridHealth;
    private readonly System.Windows.Forms.Timer updateTimer;

    public SensorDashboardPanel()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(30, 30, 30);
        _tickStart = Environment.TickCount;

        tabControl = new TabControl { Dock = DockStyle.Fill, BackColor = Color.FromArgb(35, 35, 35) };

        // === RAW SENSORS TAB ===
        var rawTab = new TabPage("Raw Sensors") { BackColor = Color.FromArgb(30, 30, 30) };

        chartAccel = new SimpleChart("Accelerometer (m/s²)") { Dock = DockStyle.Top, Height = 280 };
        chartGyro = new SimpleChart("Gyroscope (deg/s)") { Dock = DockStyle.Top, Height = 280 };

        rawTab.Controls.Add(chartGyro);
        rawTab.Controls.Add(chartAccel);

        // === VIBRATION TAB ===
        var vibTab = new TabPage("Vibration") { BackColor = Color.FromArgb(30, 30, 30) };

        var vibPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 4 };
        vibPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        vibPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        vibPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        vibPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        vibPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 70));
        vibPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
        vibPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));

        vibPanel.Controls.Add(new Label { Text = "X", ForeColor = Color.Red, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter }, 0, 0);
        vibPanel.Controls.Add(new Label { Text = "Y", ForeColor = Color.Green, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter }, 1, 0);
        vibPanel.Controls.Add(new Label { Text = "Z", ForeColor = Color.Blue, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter }, 2, 0);

        vibBarX = new ProgressBar { Maximum = 90, Dock = DockStyle.Fill, Style = ProgressBarStyle.Continuous };
        vibBarY = new ProgressBar { Maximum = 90, Dock = DockStyle.Fill, Style = ProgressBarStyle.Continuous };
        vibBarZ = new ProgressBar { Maximum = 90, Dock = DockStyle.Fill, Style = ProgressBarStyle.Continuous };
        vibPanel.Controls.Add(vibBarX, 0, 1);
        vibPanel.Controls.Add(vibBarY, 1, 1);
        vibPanel.Controls.Add(vibBarZ, 2, 1);

        vibPanel.Controls.Add(new Label { Text = "Clipping:", ForeColor = Color.Gray, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold) }, 0, 2);
        vibPanel.SetColumnSpan(vibPanel.GetControlFromPosition(0, 2)!, 3);

        lblVibClip0 = new Label { Text = "Primary: 0", ForeColor = Color.LightGray, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        lblVibClip1 = new Label { Text = "Secondary: 0", ForeColor = Color.LightGray, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        lblVibClip2 = new Label { Text = "Tertiary: 0", ForeColor = Color.LightGray, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        vibPanel.Controls.Add(lblVibClip0, 0, 3);
        vibPanel.Controls.Add(lblVibClip1, 1, 3);
        vibPanel.Controls.Add(lblVibClip2, 2, 3);

        vibTab.Controls.Add(vibPanel);

        // === EKF TAB ===
        var ekfTab = new TabPage("EKF Status") { BackColor = Color.FromArgb(30, 30, 30) };

        var ekfPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 3 };
        for (int i = 0; i < 5; i++)
            ekfPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        ekfPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        ekfPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 70));
        ekfPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));

        string[] ekfLabels = { "Velocity", "Pos Horiz", "Pos Vert", "Compass", "Terrain" };
        ekfVel = new ProgressBar { Maximum = 100, Dock = DockStyle.Fill };
        ekfPosH = new ProgressBar { Maximum = 100, Dock = DockStyle.Fill };
        ekfPosV = new ProgressBar { Maximum = 100, Dock = DockStyle.Fill };
        ekfComp = new ProgressBar { Maximum = 100, Dock = DockStyle.Fill };
        ekfTerrain = new ProgressBar { Maximum = 100, Dock = DockStyle.Fill };

        ProgressBar[] ekfBars = { ekfVel, ekfPosH, ekfPosV, ekfComp, ekfTerrain };
        for (int i = 0; i < 5; i++)
        {
            ekfPanel.Controls.Add(new Label { Text = ekfLabels[i], ForeColor = Color.LightGray, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter }, i, 0);
            ekfPanel.Controls.Add(ekfBars[i], i, 1);
            ekfPanel.Controls.Add(new Label { Text = "0%", ForeColor = Color.Gray, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Tag = i }, i, 2);
        }

        ekfTab.Controls.Add(ekfPanel);

        // === HEALTH TAB ===
        var healthTab = new TabPage("Sensor Health") { BackColor = Color.FromArgb(30, 30, 30) };

        gridHealth = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.White,
            GridColor = Color.FromArgb(60, 60, 60),
            BorderStyle = BorderStyle.None,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            Font = new System.Drawing.Font("Segoe UI", 9F)
        };
        gridHealth.Columns.Add("Sensor", "Sensor");
        gridHealth.Columns.Add("Enabled", "Enabled");
        gridHealth.Columns.Add("Present", "Present");
        gridHealth.Columns.Add("Health", "Health");

        foreach (var name in MavlinkDefinitions.SensorNames.Values)
            gridHealth.Rows.Add(name, "--", "--", "--");

        healthTab.Controls.Add(gridHealth);

        tabControl.TabPages.Add(rawTab);
        tabControl.TabPages.Add(vibTab);
        tabControl.TabPages.Add(ekfTab);
        tabControl.TabPages.Add(healthTab);

        Controls.Add(tabControl);

        updateTimer = new System.Windows.Forms.Timer { Interval = 100 };
        updateTimer.Tick += UpdateTimer_Tick;
        updateTimer.Start();
    }

    private void RefreshCharts()
    {
        try
        {
            if (timeData.Count < 2) return;

            chartAccel.SetData(timeData, accelXData, accelYData, accelZData);
            chartGyro.SetData(timeData, gyroXData, gyroYData, gyroZData);
        }
        catch { }
    }

    private void UpdateTimer_Tick(object? sender, EventArgs e) => RefreshCharts();

    public void UpdateFromState(VehicleState state)
    {
        double time = (Environment.TickCount - _tickStart) / 1000.0;
        timeData.Add(time);
        accelXData.Add(state.AccelX);
        accelYData.Add(state.AccelY);
        accelZData.Add(state.AccelZ);
        gyroXData.Add(state.GyroX);
        gyroYData.Add(state.GyroY);
        gyroZData.Add(state.GyroZ);

        if (timeData.Count > 2000)
        {
            timeData.RemoveRange(0, 500);
            accelXData.RemoveRange(0, 500);
            accelYData.RemoveRange(0, 500);
            accelZData.RemoveRange(0, 500);
            gyroXData.RemoveRange(0, 500);
            gyroYData.RemoveRange(0, 500);
            gyroZData.RemoveRange(0, 500);
        }

        vibBarX.Value = Math.Min((int)state.VibeX, vibBarX.Maximum);
        vibBarY.Value = Math.Min((int)state.VibeY, vibBarY.Maximum);
        vibBarZ.Value = Math.Min((int)state.VibeZ, vibBarZ.Maximum);
        lblVibClip0.Text = $"Primary: {state.VibeClip0}";
        lblVibClip1.Text = $"Secondary: {state.VibeClip1}";
        lblVibClip2.Text = $"Tertiary: {state.VibeClip2}";

        ekfVel.Value = Math.Min((int)(state.EkfVelVariance * 100), 100);
        ekfPosH.Value = Math.Min((int)(state.EkfPosHorizVariance * 100), 100);
        ekfPosV.Value = Math.Min((int)(state.EkfPosVertVariance * 100), 100);
        ekfComp.Value = Math.Min((int)(state.EkfCompassVariance * 100), 100);
        ekfTerrain.Value = Math.Min((int)(state.EkfTerrainAltVariance * 100), 100);

        UpdateHealthGrid(state);
    }

    public void UpdateFromLogMessage(LogMessage msg)
    {
        if (msg.Fields.TryGetValue("AccX", out var ax))
        {
            timeData.Add(msg.TimeSeconds);
            accelXData.Add((double)ax);
            accelYData.Add((double)msg.Fields.GetValueOrDefault("AccY", 0.0));
            accelZData.Add((double)msg.Fields.GetValueOrDefault("AccZ", 0.0));
        }
        if (msg.Fields.TryGetValue("GyrX", out var gx))
        {
            gyroXData.Add((double)gx);
            gyroYData.Add((double)msg.Fields.GetValueOrDefault("GyrY", 0.0));
            gyroZData.Add((double)msg.Fields.GetValueOrDefault("GyrZ", 0.0));
        }
        if (msg.Fields.TryGetValue("VibeX", out var vx))
        {
            vibBarX.Value = Math.Min(Convert.ToInt32(vx), vibBarX.Maximum);
            vibBarY.Value = Math.Min(Convert.ToInt32(msg.Fields.GetValueOrDefault("VibeY", 0.0)), vibBarY.Maximum);
            vibBarZ.Value = Math.Min(Convert.ToInt32(msg.Fields.GetValueOrDefault("VibeZ", 0.0)), vibBarZ.Maximum);
        }

        RefreshCharts();
    }

    private void UpdateHealthGrid(VehicleState state)
    {
        var names = MavlinkDefinitions.SensorNames;
        for (int i = 0; i < names.Count && i < gridHealth.Rows.Count; i++)
        {
            int mask = 1 << i;
            gridHealth.Rows[i].Cells["Enabled"].Value = (state.SensorsEnabled & mask) != 0 ? "Yes" : "No";
            gridHealth.Rows[i].Cells["Present"].Value = (state.SensorsPresent & mask) != 0 ? "Yes" : "No";
            gridHealth.Rows[i].Cells["Health"].Value = (state.SensorsHealth & mask) != 0 ? "OK" : "Bad";

            bool enabled = (state.SensorsEnabled & mask) != 0;
            bool present = (state.SensorsPresent & mask) != 0;
            bool healthy = (state.SensorsHealth & mask) != 0;

            gridHealth.Rows[i].Cells["Enabled"].Style.ForeColor = enabled ? Color.LimeGreen : Color.Red;
            gridHealth.Rows[i].Cells["Present"].Style.ForeColor = present ? Color.LimeGreen : Color.Red;
            gridHealth.Rows[i].Cells["Health"].Style.ForeColor = healthy ? Color.LimeGreen : Color.Red;
        }
    }
}

/// <summary>
/// Simple GDI+ time-series chart with no external dependencies.
/// </summary>
public class SimpleChart : Control
{
    private readonly string _title;
    private List<double> _time = new();
    private List<double> _y1 = new(), _y2 = new(), _y3 = new();
    private static readonly Color[] Colors = { Color.Red, Color.LimeGreen, Color.DodgerBlue };
    private static readonly string[] Labels = { "X", "Y", "Z" };

    public SimpleChart(string title)
    {
        _title = title;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = Color.FromArgb(25, 25, 25);
    }

    public void SetData(List<double> time, List<double> y1, List<double> y2, List<double> y3)
    {
        _time = time;
        _y1 = y1;
        _y2 = y2;
        _y3 = y3;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int w = Width, h = Height;
        int padL = 50, padR = 15, padT = 25, padB = 25;
        int chartW = w - padL - padR;
        int chartH = h - padT - padB;

        // Background
        using var bgBrush = new SolidBrush(Color.FromArgb(25, 25, 25));
        g.FillRectangle(bgBrush, 0, 0, w, h);

        // Title
        using var titleFont = new Font("Segoe UI", 10f, FontStyle.Bold);
        g.DrawString(_title, titleFont, Brushes.LightGray, padL, 3);

        if (_time.Count < 2)
        {
            using var waitFont = new Font("Segoe UI", 10f);
            g.DrawString("Waiting for data...", waitFont, Brushes.Gray, w / 2 - 60, h / 2 - 8);
            return;
        }

        // Find data range
        double tMin = _time[^1] - 10; // Show last 10 seconds
        double tMax = _time[^1];
        double yMin = double.MaxValue, yMax = double.MinValue;

        for (int i = 0; i < _time.Count; i++)
        {
            if (_time[i] < tMin) continue;
            yMin = Math.Min(yMin, Math.Min(_y1[i], Math.Min(_y2[i], _y3[i])));
            yMax = Math.Max(yMax, Math.Max(_y1[i], Math.Max(_y2[i], _y3[i])));
        }

        if (yMin == double.MaxValue) { yMin = -10; yMax = 10; }
        double yRange = yMax - yMin;
        if (yRange < 0.1) { yMin -= 0.5; yMax += 0.5; yRange = yMax - yMin; }
        yMin -= yRange * 0.1;
        yMax += yRange * 0.1;
        yRange = yMax - yMin;

        // Grid lines
        using var gridPen = new Pen(Color.FromArgb(40, 40, 40), 1);
        using var labelFont = new Font("Consolas", 8f);
        int gridLines = 5;
        for (int i = 0; i <= gridLines; i++)
        {
            float y = padT + (float)(i * chartH / gridLines);
            g.DrawLine(gridPen, padL, y, padL + chartW, y);
            double val = yMax - i * yRange / gridLines;
            g.DrawString($"{val:F1}", labelFont, Brushes.Gray, 2, y - 6);
        }

        // Time labels
        for (int i = 0; i <= 2; i++)
        {
            float x = padL + (float)(i * chartW / 2);
            double t = tMin + i * (tMax - tMin) / 2;
            g.DrawLine(gridPen, x, padT, x, padT + chartH);
            g.DrawString($"{t:F0}s", labelFont, Brushes.Gray, x - 8, padT + chartH + 4);
        }

        // Draw data lines
        DrawLine(g, _time, _y1, tMin, tMax, yMin, yMax, padL, padT, chartW, chartH, Colors[0]);
        DrawLine(g, _time, _y2, tMin, tMax, yMin, yMax, padL, padT, chartW, chartH, Colors[1]);
        DrawLine(g, _time, _y3, tMin, tMax, yMin, yMax, padL, padT, chartW, chartH, Colors[2]);

        // Legend
        using var legendFont = new Font("Segoe UI", 8f);
        for (int i = 0; i < 3; i++)
        {
            float lx = padL + chartW - 100 + i * 35;
            using var brush = new SolidBrush(Colors[i]);
            g.FillRectangle(brush, lx, 5, 10, 10);
            g.DrawString(Labels[i], legendFont, brush, lx + 13, 3);
        }

        // Border
        using var borderPen = new Pen(Color.FromArgb(60, 60, 60), 1);
        g.DrawRectangle(borderPen, padL, padT, chartW, chartH);
    }

    private static void DrawLine(Graphics g, List<double> xData, List<double> yData,
        double xMin, double xMax, double yMin, double yMax,
        int padL, int padT, int chartW, int chartH, Color color)
    {
        using var pen = new Pen(color, 1.5f);
        bool started = false;
        float lastX = 0, lastY = 0;

        for (int i = 0; i < xData.Count; i++)
        {
            if (xData[i] < xMin) continue;

            float px = padL + (float)((xData[i] - xMin) / (xMax - xMin) * chartW);
            float py = padT + (float)((yMax - yData[i]) / (yMax - yMin) * chartH);

            if (started)
                g.DrawLine(pen, lastX, lastY, px, py);

            lastX = px;
            lastY = py;
            started = true;
        }
    }
}
