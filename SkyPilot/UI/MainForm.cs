using SkyPilot.Core.Mavlink;
using SkyPilot.Log;
using SkyPilot.Panels;
using SkyPilot.Utils;

namespace SkyPilot.UI;

/// <summary>
/// Main application window. Tab-based navigation between views.
/// </summary>
public partial class MainForm : Form
{
    private readonly VehicleState _vehicleState;
    private readonly MavlinkStream _stream;
    private readonly MavlinkProcessor _processor;
    private readonly AppSettings _settings;
    private readonly System.Windows.Forms.Timer _statusTimer;

    public MainForm()
    {
        InitializeComponent();

        _settings = AppSettings.Load();
        _vehicleState = new VehicleState();
        _stream = new MavlinkStream();
        _processor = new MavlinkProcessor(_vehicleState);

        _stream.PacketReceived += packet =>
            BeginInvoke(() => _processor.ProcessPacket(packet));

        _processor.StateUpdated += () =>
            BeginInvoke(UpdateStatusBar);

        // Status timer - check connection health
        _statusTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _statusTimer.Tick += StatusTimer_Tick;
        _statusTimer.Start();

        SetupTabs();
        ApplyTheme();
    }

    private void SetupTabs()
    {
        // Overview tab
        var overview = new OverviewPanel();
        tabFlight.TabPages["tabOverview"].Controls.Add(overview);

        // Sensors tab
        var sensors = new SensorDashboardPanel();
        tabFlight.TabPages["tabSensors"].Controls.Add(sensors);

        // Store references for data updates
        _overviewPanel = overview;
        _sensorsPanel = sensors;
    }

    private OverviewPanel? _overviewPanel;
    private SensorDashboardPanel? _sensorsPanel;

    private void ApplyTheme()
    {
        BackColor = Color.FromArgb(30, 30, 30);
        ForeColor = Color.White;
        menuStrip.BackColor = Color.FromArgb(45, 45, 45);
        menuStrip.ForeColor = Color.White;
        statusStrip.BackColor = Color.FromArgb(45, 45, 45);
        statusStrip.ForeColor = Color.LightGray;
        tabFlight.BackColor = Color.FromArgb(35, 35, 35);
        tabFlight.ForeColor = Color.White;

        foreach (ToolStripMenuItem item in menuStrip.Items)
        {
            item.BackColor = Color.FromArgb(45, 45, 45);
            item.ForeColor = Color.White;
        }
    }

    // === Menu Actions ===

    private void connectSerialToolStripMenuItem_Click(object sender, EventArgs e)
    {
        using var dlg = new ConnectSerialDialog(_settings);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            try
            {
                _stream.OpenSerial(dlg.SelectedPort, dlg.SelectedBaud);
                lblConnection.Text = $"Connected: {dlg.SelectedPort}";
                lblConnection.ForeColor = Color.LimeGreen;
                _settings.LastSerialPort = dlg.SelectedPort;
                _settings.LastBaudRate = dlg.SelectedBaud;
                _settings.Save();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Connection failed: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void connectUdpToolStripMenuItem_Click(object sender, EventArgs e)
    {
        using var dlg = new ConnectUdpDialog(_settings);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            try
            {
                _stream.OpenUdp(dlg.SelectedHost, dlg.SelectedPort);
                lblConnection.Text = $"UDP: {dlg.SelectedHost}:{dlg.SelectedPort}";
                lblConnection.ForeColor = Color.LimeGreen;
                _settings.LastUdpHost = dlg.SelectedHost;
                _settings.LastUdpPort = dlg.SelectedPort;
                _settings.Save();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Connection failed: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void disconnectToolStripMenuItem_Click(object sender, EventArgs e)
    {
        _stream.Close();
        _vehicleState.IsConnected = false;
        lblConnection.Text = "Disconnected";
        lblConnection.ForeColor = Color.Gray;
    }

    private void openLogFileToolStripMenuItem_Click(object sender, EventArgs e)
    {
        using var ofd = new OpenFileDialog();
        ofd.Filter = "Log files|*.bin;*.log;*.BIN;*.LOG|All files|*.*";
        if (ofd.ShowDialog() == DialogResult.OK)
        {
            var logForm = new LogAnalysisForm(ofd.FileName, _settings);
            logForm.Show(this);
        }
    }

    private void exitToolStripMenuItem_Click(object sender, EventArgs e)
    {
        Close();
    }

    // === Flight Actions ===

    private void armToolStripMenuItem_Click(object sender, EventArgs e)
    {
        SendCommand(MavlinkDefinitions.MAV_CMD_COMPONENT_ARM_DISARM, 1.0f);
    }

    private void disarmToolStripMenuItem_Click(object sender, EventArgs e)
    {
        SendCommand(MavlinkDefinitions.MAV_CMD_COMPONENT_ARM_DISARM, 0.0f);
    }

    private void rtlToolStripMenuItem_Click(object sender, EventArgs e)
    {
        SendCommand(MavlinkDefinitions.MAV_CMD_NAV_RETURN_TO_LAUNCH);
    }

    private void autoToolStripMenuItem_Click(object sender, EventArgs e)
    {
        SetMode(4); // Auto mode
    }

    private void SendCommand(ushort command, float param1 = 0, float param2 = 0)
    {
        if (!_stream.IsOpen) return;

        var payload = new byte[32];
        BitConverter.GetBytes(command).CopyTo(payload, 0);
        BitConverter.GetBytes(param1).CopyTo(payload, 4);
        BitConverter.GetBytes(param2).CopyTo(payload, 8);

        var packet = MavlinkCodec.Encode(
            _vehicleState.SystemId, 0, MavlinkDefinitions.COMMAND_LONG, payload);
        _stream.Send(packet);
    }

    private void SetMode(int mode)
    {
        if (!_stream.IsOpen) return;

        var payload = new byte[8];
        payload[0] = _vehicleState.SystemId;
        payload[1] = 0; // base_mode
        BitConverter.GetBytes((uint)mode).CopyTo(payload, 4);

        var packet = MavlinkCodec.Encode(
            _vehicleState.SystemId, 0, MavlinkDefinitions.SET_MODE, payload);
        _stream.Send(packet);
    }

    private void StatusTimer_Tick(object? sender, EventArgs e)
    {
        if (_vehicleState.IsConnected &&
            (DateTime.UtcNow - _vehicleState.LastHeartbeat).TotalSeconds > 5)
        {
            _vehicleState.IsConnected = false;
            lblConnection.Text = "Connection Lost";
            lblConnection.ForeColor = Color.Red;
        }
    }

    private void UpdateStatusBar()
    {
        if (_vehicleState.IsConnected)
        {
            lblMode.Text = _vehicleState.FlightModeName;
            lblMode.ForeColor = _vehicleState.IsArmed ? Color.LimeGreen : Color.Gray;
            lblArmed.Text = _vehicleState.IsArmed ? "ARMED" : "Disarmed";
            lblArmed.ForeColor = _vehicleState.IsArmed ? Color.Red : Color.Gray;
            lblAlt.Text = $"Alt: {_vehicleState.AltitudeRel:F1}m";
            lblSpeed.Text = $"GS: {_vehicleState.GroundSpeed:F1}m/s";
            lblBatt.Text = $"Batt: {_vehicleState.BatteryVoltage:F1}V";

            _overviewPanel?.UpdateFromState(_vehicleState);
            _sensorsPanel?.UpdateFromState(_vehicleState);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _statusTimer.Stop();
        _stream.Dispose();
        _settings.Save();
        base.OnFormClosing(e);
    }
}
