using System.Drawing;
using SkyPilot.Core.Mavlink;
using SkyPilot.Log;
using SkyPilot.Utils;

namespace SkyPilot.Panels;

/// <summary>
/// Unified sensor dashboard with multiple sub-panels for all sensor data.
/// </summary>
public class SensorDashboardPanel : UserControl
{
    private readonly TabControl tabControl;
    private readonly ScottPlot.WinForms.FormsPlot chartAccel;
    private readonly ScottPlot.WinForms.FormsPlot chartGyro;
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

        chartAccel = new ScottPlot.WinForms.FormsPlot { Dock = DockStyle.Top, Height = 280 };
        ChartHelper.SetupChart(chartAccel, "Accelerometer", "Time (s)", "m/s²");

        chartGyro = new ScottPlot.WinForms.FormsPlot { Dock = DockStyle.Top, Height = 280 };
        ChartHelper.SetupChart(chartGyro, "Gyroscope", "Time (s)", "deg/s");

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

            chartAccel.Plot.Clear();
            chartGyro.Plot.Clear();

            double[] timeArr = timeData.ToArray();
            chartAccel.Plot.Add.Scatter(timeArr, accelXData.ToArray()).Color = ScottPlot.Color.FromHex("FF0000");
            chartAccel.Plot.Add.Scatter(timeArr, accelYData.ToArray()).Color = ScottPlot.Color.FromHex("00FF00");
            chartAccel.Plot.Add.Scatter(timeArr, accelZData.ToArray()).Color = ScottPlot.Color.FromHex("0000FF");

            chartGyro.Plot.Add.Scatter(timeArr, gyroXData.ToArray()).Color = ScottPlot.Color.FromHex("FF0000");
            chartGyro.Plot.Add.Scatter(timeArr, gyroYData.ToArray()).Color = ScottPlot.Color.FromHex("00FF00");
            chartGyro.Plot.Add.Scatter(timeArr, gyroZData.ToArray()).Color = ScottPlot.Color.FromHex("0000FF");

            double lastTime = timeArr[^1];
            chartAccel.Plot.Axes.SetLimitsX(lastTime - 10, lastTime);
            chartGyro.Plot.Axes.SetLimitsX(lastTime - 10, lastTime);

            chartAccel.Refresh();
            chartGyro.Refresh();
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

        ekfVel.Value = (int)(state.EkfVelVariance * 100);
        ekfPosH.Value = (int)(state.EkfPosHorizVariance * 100);
        ekfPosV.Value = (int)(state.EkfPosVertVariance * 100);
        ekfComp.Value = (int)(state.EkfCompassVariance * 100);
        ekfTerrain.Value = (int)(state.EkfTerrainAltVariance * 100);

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
