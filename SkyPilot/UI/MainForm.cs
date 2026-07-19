using SkyPilot.Core.Mavlink;
using SkyPilot.Core.Sim;
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
    private VirtualVehicle? _sim;
    private readonly System.Windows.Forms.Timer _heartbeatTimer;

    // Panels
    private OverviewPanel? _overviewPanel;
    private SensorDashboardPanel? _sensorsPanel;
    private HudPanel? _hudPanel;
    private MessageLogPanel? _messageLog;
    private MissionPanel? _missionPanel;
    private ParameterPanel? _paramPanel;

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

        _processor.MessageReceived += (text, severity) =>
            BeginInvoke(() => _messageLog?.AddMessage(text, severity));

        _processor.CommandAckReceived += (cmd, result, sysid) =>
        {
            string resultText = result switch
            {
                0 => "Accepted",
                1 => "Temporarily Rejected",
                2 => "Denied",
                3 => "Unsupported",
                4 => "Failed",
                _ => $"Result {result}"
            };
            BeginInvoke(() => _messageLog?.AddMessage($"CMD_ACK: {cmd} -> {resultText}"));
        };

        _processor.ParameterReceived += (name, value) =>
            BeginInvoke(() => _paramPanel?.SetParameter(name, value));

        // Status timer - check connection health
        _statusTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _statusTimer.Tick += StatusTimer_Tick;
        _statusTimer.Start();

        // GCS heartbeat timer (1Hz)
        _heartbeatTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _heartbeatTimer.Tick += HeartbeatTimer_Tick;

        SetupTabs();
        ApplyTheme();
    }

    private void SetupTabs()
    {
        // Overview tab with HUD on left, overview on right
        var overviewSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 450,
            BackColor = Color.FromArgb(30, 30, 30)
        };

        _hudPanel = new HudPanel { Dock = DockStyle.Fill };
        _overviewPanel = new OverviewPanel { Dock = DockStyle.Fill };
        overviewSplit.Panel1.Controls.Add(_hudPanel);
        overviewSplit.Panel2.Controls.Add(_overviewPanel);
        tabFlight.TabPages["tabOverview"].Controls.Add(overviewSplit);

        // Sensors tab
        _sensorsPanel = new SensorDashboardPanel();
        tabFlight.TabPages["tabSensors"].Controls.Add(_sensorsPanel);

        // Mission tab
        _missionPanel = new MissionPanel();
        tabFlight.TabPages["tabMission"].Controls.Add(_missionPanel);

        // Message Log tab
        _messageLog = new MessageLogPanel();
        tabFlight.TabPages["tabMessages"].Controls.Add(_messageLog);

        // Parameters tab
        _paramPanel = new ParameterPanel();
        _paramPanel.RequestAllParams += () => SendParamRequestList();
        _paramPanel.WriteParam += (name, value) => SendParamSet(name, value);
        tabFlight.TabPages["tabParams"].Controls.Add(_paramPanel);
    }

    private void ApplyTheme()
    {
        BackColor = Color.FromArgb(30, 30, 30);
        ForeColor = Color.White;
        menuStrip.BackColor = Color.FromArgb(45, 45, 45);
        menuStrip.ForeColor = Color.White;
        toolStrip.BackColor = Color.FromArgb(40, 40, 40);
        toolStrip.ForeColor = Color.White;
        statusStrip.BackColor = Color.FromArgb(45, 45, 45);
        statusStrip.ForeColor = Color.LightGray;
        tabFlight.BackColor = Color.FromArgb(35, 35, 35);
        tabFlight.ForeColor = Color.White;

        foreach (ToolStripMenuItem item in menuStrip.Items)
        {
            item.BackColor = Color.FromArgb(45, 45, 45);
            item.ForeColor = Color.White;
        }

        foreach (ToolStripItem item in toolStrip.Items)
        {
            if (item is ToolStripButton btn)
            {
                btn.BackColor = Color.FromArgb(50, 50, 50);
                btn.ForeColor = Color.White;
            }
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
                _heartbeatTimer.Start();
                _messageLog?.AddMessage($"Connected to {dlg.SelectedPort} @ {dlg.SelectedBaud}");
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
                _heartbeatTimer.Start();
                _messageLog?.AddMessage($"Connected UDP {dlg.SelectedHost}:{dlg.SelectedPort}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Connection failed: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void connectTcpToolStripMenuItem_Click(object sender, EventArgs e)
    {
        using var dlg = new ConnectTcpDialog(_settings);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            try
            {
                _stream.OpenTcp(dlg.SelectedHost, dlg.SelectedPort);
                lblConnection.Text = $"TCP: {dlg.SelectedHost}:{dlg.SelectedPort}";
                lblConnection.ForeColor = Color.LimeGreen;
                _heartbeatTimer.Start();
                _messageLog?.AddMessage($"Connected TCP {dlg.SelectedHost}:{dlg.SelectedPort}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Connection failed: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void startSimulatorToolStripMenuItem_Click(object sender, EventArgs e)
    {
        if (_stream.IsOpen)
        {
            MessageBox.Show("Disconnect first before starting simulator.", "Info",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _sim = new VirtualVehicle("plane");
        _stream.OpenSimulation(_sim);
        lblConnection.Text = "SIMULATED (Plane)";
        lblConnection.ForeColor = Color.Yellow;
        _messageLog?.AddMessage("Simulator started: Fixed-wing plane", 6);
        _messageLog?.AddMessage("Flying circular pattern over London (51.5074, -0.1278)", 14);
    }

    private void stopSimulatorToolStripMenuItem_Click(object sender, EventArgs e)
    {
        if (_sim != null)
        {
            _stream.Close();
            _sim = null;
            _vehicleState.IsConnected = false;
            lblConnection.Text = "Disconnected";
            lblConnection.ForeColor = Color.Gray;
            _messageLog?.AddMessage("Simulator stopped", 14);
        }
    }

    private void disconnectToolStripMenuItem_Click(object sender, EventArgs e)
    {
        _stream.Close();
        _sim = null;
        _vehicleState.IsConnected = false;
        lblConnection.Text = "Disconnected";
        lblConnection.ForeColor = Color.Gray;
        _heartbeatTimer.Stop();
        _messageLog?.AddMessage("Disconnected", 14);
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
        SetMode(3); // Auto mode
    }

    private void takeoffToolStripMenuItem_Click(object sender, EventArgs e)
    {
        using var input = new InputBox("Enter takeoff altitude (meters):", "50");
        if (input.ShowDialog(this) == DialogResult.OK && float.TryParse(input.Value, out float alt))
        {
            SetMode(4); // Guided
            SendCommand(MavlinkDefinitions.MAV_CMD_NAV_TAKEOFF, alt);
            _messageLog?.AddMessage($"Takeoff to {alt}m (Guided mode)");
        }
    }

    private void landToolStripMenuItem_Click(object sender, EventArgs e)
    {
        SetMode(9); // Land
        _messageLog?.AddMessage("Land mode");
    }

    private void modeCmb_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (modeCmb.SelectedItem is string modeName)
        {
            var mode = MavlinkDefinitions.FlightModes.FirstOrDefault(x => x.Value == modeName);
            SetMode(mode.Key);
            _messageLog?.AddMessage($"Mode change: {modeName}");
        }
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

    private void SendParamRequestList()
    {
        if (!_stream.IsOpen) return;

        var payload = new byte[2];
        payload[0] = _vehicleState.SystemId;
        payload[1] = 0;

        var packet = MavlinkCodec.Encode(
            _vehicleState.SystemId, 0, MavlinkDefinitions.PARAM_REQUEST_LIST, payload);
        _stream.Send(packet);
        _messageLog?.AddMessage("Requested parameter list");
    }

    private void SendParamSet(string name, float value)
    {
        if (!_stream.IsOpen) return;

        var payload = new byte[23];
        var nameBytes = System.Text.Encoding.ASCII.GetBytes(name);
        Array.Copy(nameBytes, 0, payload, 0, Math.Min(nameBytes.Length, 16));
        BitConverter.GetBytes(value).CopyTo(payload, 16);
        BitConverter.GetBytes((ushort)9).CopyTo(payload, 20); // PARAM_TYPE_REAL32

        var packet = MavlinkCodec.Encode(
            _vehicleState.SystemId, 0, MavlinkDefinitions.PARAM_SET, payload);
        _stream.Send(packet);
        _messageLog?.AddMessage($"Set parameter: {name} = {value}");
    }

    // === Toolbar Actions ===

    private void btnConnect_Click(object sender, EventArgs e)
    {
        // Show connection menu
        connectSerialToolStripMenuItem_Click(sender, e);
    }

    private void btnArm_Click(object sender, EventArgs e)
    {
        SendCommand(MavlinkDefinitions.MAV_CMD_COMPONENT_ARM_DISARM, 1.0f);
    }

    private void btnDisarm_Click(object sender, EventArgs e)
    {
        SendCommand(MavlinkDefinitions.MAV_CMD_COMPONENT_ARM_DISARM, 0.0f);
    }

    private void btnRTL_Click(object sender, EventArgs e)
    {
        SendCommand(MavlinkDefinitions.MAV_CMD_NAV_RETURN_TO_LAUNCH);
    }

    private void btnTakeoff_Click(object sender, EventArgs e)
    {
        takeoffToolStripMenuItem_Click(sender, e);
    }

    private void btnEmergencyStop_Click(object sender, EventArgs e)
    {
        if (MessageBox.Show("EMERGENCY MOTOR STOP?\nThis will immediately disarm the vehicle!",
            "Emergency Stop", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
        {
            SendCommand(MavlinkDefinitions.MAV_CMD_COMPONENT_ARM_DISARM, 0, 21196);
            _messageLog?.AddMessage("EMERGENCY MOTOR STOP sent!", 2);
        }
    }

    // === Timers ===

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

    private void HeartbeatTimer_Tick(object? sender, EventArgs e)
    {
        if (!_stream.IsOpen) return;

        // Send GCS heartbeat so vehicle accepts commands
        var payload = new byte[9];
        payload[0] = 0; // base_mode
        BitConverter.GetBytes(0u).CopyTo(payload, 1); // custom_mode
        payload[5] = 6; // MAV_STATE_ACTIVE
        payload[6] = MavlinkDefinitions.MAV_AUTOPILOT_GENERIC;

        var packet = MavlinkCodec.Encode(
            _vehicleState.SystemId, 0, MavlinkDefinitions.HEARTBEAT, payload);
        _stream.Send(packet);
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

            _hudPanel?.UpdateFromState(_vehicleState);
            _overviewPanel?.UpdateFromState(_vehicleState);
            _sensorsPanel?.UpdateFromState(_vehicleState);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _statusTimer.Stop();
        _heartbeatTimer.Stop();
        _stream.Dispose();
        _settings.Save();
        base.OnFormClosing(e);
    }
}

/// <summary>
/// Simple input dialog for getting a value from the user.
/// </summary>
public class InputBox : Form
{
    public string Value => txtInput.Text;
    private readonly TextBox txtInput;

    public InputBox(string prompt, string defaultValue = "")
    {
        Text = "Input";
        Size = new Size(350, 150);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(35, 35, 35);
        ForeColor = Color.White;

        Controls.Add(new Label { Text = prompt, Location = new Point(15, 15), AutoSize = true });
        txtInput = new TextBox { Location = new Point(15, 45), Width = 300, Text = defaultValue };
        Controls.Add(txtInput);

        var btnOk = new Button
        {
            Text = "OK",
            Location = new Point(120, 80),
            Size = new Size(80, 30),
            DialogResult = DialogResult.OK
        };
        Controls.Add(btnOk);
        AcceptButton = btnOk;
    }
}
