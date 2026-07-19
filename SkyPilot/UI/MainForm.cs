using SkyPilot.Core.Mavlink;
using SkyPilot.Core.Sim;
using SkyPilot.Log;
using SkyPilot.Panels;
using SkyPilot.Utils;

namespace SkyPilot.UI;

/// <summary>
/// Main application window with modern UI.
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

    // Current nav
    private Panels.ModernButton? _activeNav;
    private Control? _activeContent;

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
                0 => "Accepted", 1 => "Temp Rejected", 2 => "Denied",
                3 => "Unsupported", 4 => "Failed", _ => $"Result {result}"
            };
            BeginInvoke(() => _messageLog?.AddMessage($"CMD_ACK: {cmd} -> {resultText}"));
        };

        _processor.ParameterReceived += (name, value) =>
            BeginInvoke(() => _paramPanel?.SetParameter(name, value));

        _statusTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _statusTimer.Tick += StatusTimer_Tick;
        _statusTimer.Start();

        _heartbeatTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _heartbeatTimer.Tick += HeartbeatTimer_Tick;

        SetupPanels();
        SetupNavigation();
        WireEvents();

        // Start on Overview
        SwitchTab(navOverview, _overviewPanel!);
    }

    private void SetupPanels()
    {
        _overviewPanel = new OverviewPanel();
        _hudPanel = new HudPanel();

        // Wrap HUD + Overview in a split
        var overviewSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 450,
            BackColor = ModernTheme.Background,
            FixedPanel = FixedPanel.Panel1
        };
        overviewSplit.Panel1.BackColor = ModernTheme.Background;
        overviewSplit.Panel2.BackColor = ModernTheme.Background;
        overviewSplit.SplitterWidth = 4;
        _hudPanel.Dock = DockStyle.Fill;
        _overviewPanel.Dock = DockStyle.Fill;
        overviewSplit.Panel1.Controls.Add(_hudPanel);
        overviewSplit.Panel2.Controls.Add(_overviewPanel);

        _sensorsPanel = new SensorDashboardPanel();
        _messageLog = new MessageLogPanel();
        _missionPanel = new MissionPanel();
        _paramPanel = new ParameterPanel();
        _paramPanel.RequestAllParams += () => SendParamRequestList();
        _paramPanel.WriteParam += (name, value) => SendParamSet(name, value);
    }

    private void SetupNavigation()
    {
        navOverview.Click += (s, e) => SwitchTab(navOverview, _overviewPanel!);
        navSensors.Click += (s, e) => SwitchTab(navSensors, _sensorsPanel!);
        navMission.Click += (s, e) => SwitchTab(navMission, _missionPanel!);
        navMessages.Click += (s, e) => SwitchTab(navMessages, _messageLog!);
        navParams.Click += (s, e) => SwitchTab(navParams, _paramPanel!);
        navLogs.Click += (s, e) => OpenLogFile();
    }

    private void SwitchTab(Panels.ModernButton nav, Control content)
    {
        // Deactivate old nav
        if (_activeNav != null) _activeNav.BaseColor = ModernTheme.SurfaceLight;
        if (_activeContent != null) _activeContent.Visible = false;

        // Activate new nav
        nav.BaseColor = ModernTheme.Accent;
        content.Dock = DockStyle.Fill;
        contentPanel.Controls.Clear();
        contentPanel.Controls.Add(content);
        content.Visible = true;

        _activeNav = nav;
        _activeContent = content;
    }

    private void WireEvents()
    {
        if (_btnConnect != null)
            _btnConnect.Click += (s, e) => ConnectSerialToolStripMenuItem_Click(s!, e);
        if (_btnDisconnect != null)
            _btnDisconnect.Click += (s, e) => DisconnectToolStripMenuItem_Click(s!, e);

        btnArm.Click += (s, e) => SendCommand(MavlinkDefinitions.MAV_CMD_COMPONENT_ARM_DISARM, 1.0f);
        btnDisarm.Click += (s, e) => SendCommand(MavlinkDefinitions.MAV_CMD_COMPONENT_ARM_DISARM, 0.0f);
        btnTakeoff.Click += (s, e) => TakeoffToolStripMenuItem_Click(s!, e);
        btnRTL.Click += (s, e) => SendCommand(MavlinkDefinitions.MAV_CMD_NAV_RETURN_TO_LAUNCH);
        btnEmergencyStop.Click += (s, e) =>
        {
            if (MessageBox.Show("EMERGENCY MOTOR STOP?\nThis will immediately disarm the vehicle!",
                "Emergency Stop", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                SendCommand(MavlinkDefinitions.MAV_CMD_COMPONENT_ARM_DISARM, 0, 21196);
                _messageLog?.AddMessage("EMERGENCY MOTOR STOP sent!", 2);
            }
        };

        cmbMode.SelectedIndexChanged += (s, e) =>
        {
            if (cmbMode.SelectedItem is string modeName)
            {
                var mode = MavlinkDefinitions.FlightModes.FirstOrDefault(x => x.Value == modeName);
                SetMode(mode.Key);
                _messageLog?.AddMessage($"Mode change: {modeName}");
            }
        };

        lblConnection.Click += (s, e) => ConnectSerialToolStripMenuItem_Click(s!, e);
        lblConnection.Cursor = Cursors.Hand;
    }

    // === Connection ===

    private void ConnectSerialToolStripMenuItem_Click(object sender, EventArgs e)
    {
        using var dlg = new ConnectSerialDialog(_settings);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            try
            {
                _stream.OpenSerial(dlg.SelectedPort, dlg.SelectedBaud);
                lblConnection.Text = $"Connected: {dlg.SelectedPort}";
                lblConnection.ForeColor = ModernTheme.Success;
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

    private void ConnectUdpToolStripMenuItem_Click(object sender, EventArgs e)
    {
        using var dlg = new ConnectUdpDialog(_settings);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            try
            {
                _stream.OpenUdp(dlg.SelectedHost, dlg.SelectedPort);
                lblConnection.Text = $"UDP: {dlg.SelectedHost}:{dlg.SelectedPort}";
                lblConnection.ForeColor = ModernTheme.Success;
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

    private void ConnectTcpToolStripMenuItem_Click(object sender, EventArgs e)
    {
        using var dlg = new ConnectTcpDialog(_settings);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            try
            {
                _stream.OpenTcp(dlg.SelectedHost, dlg.SelectedPort);
                lblConnection.Text = $"TCP: {dlg.SelectedHost}:{dlg.SelectedPort}";
                lblConnection.ForeColor = ModernTheme.Success;
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

    private void StartSimulatorToolStripMenuItem_Click(object? sender = null, EventArgs? e = null)
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
        lblConnection.ForeColor = ModernTheme.Warning;
        _messageLog?.AddMessage("Simulator started: Fixed-wing plane", 6);
        _messageLog?.AddMessage("Flying circular pattern over London", 14);
    }

    private void StopSimulatorToolStripMenuItem_Click(object? sender = null, EventArgs? e = null)
    {
        if (_sim != null)
        {
            _stream.Close();
            _sim = null;
            _vehicleState.IsConnected = false;
            lblConnection.Text = "Disconnected";
            lblConnection.ForeColor = ModernTheme.TextMuted;
            _messageLog?.AddMessage("Simulator stopped", 14);
        }
    }

    private void DisconnectToolStripMenuItem_Click(object sender, EventArgs e)
    {
        _stream.Close();
        _sim = null;
        _vehicleState.IsConnected = false;
        lblConnection.Text = "Disconnected";
        lblConnection.ForeColor = ModernTheme.TextMuted;
        _heartbeatTimer.Stop();
        _messageLog?.AddMessage("Disconnected", 14);
    }

    private void OpenLogFile()
    {
        using var ofd = new OpenFileDialog();
        ofd.Filter = "Log files|*.bin;*.log;*.BIN;*.LOG|All files|*.*";
        if (ofd.ShowDialog() == DialogResult.OK)
        {
            var logForm = new LogAnalysisForm(ofd.FileName, _settings);
            logForm.Show(this);
        }
    }

    // === Flight Actions ===

    private void TakeoffToolStripMenuItem_Click(object? sender = null, EventArgs? e = null)
    {
        using var input = new InputBox("Enter takeoff altitude (meters):", "50");
        if (input.ShowDialog(this) == DialogResult.OK && float.TryParse(input.Value, out float alt))
        {
            SetMode(4); // Guided
            SendCommand(MavlinkDefinitions.MAV_CMD_NAV_TAKEOFF, alt);
            _messageLog?.AddMessage($"Takeoff to {alt}m (Guided mode)");
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
        payload[1] = 0;
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
        BitConverter.GetBytes((ushort)9).CopyTo(payload, 20);

        var packet = MavlinkCodec.Encode(
            _vehicleState.SystemId, 0, MavlinkDefinitions.PARAM_SET, payload);
        _stream.Send(packet);
        _messageLog?.AddMessage($"Set parameter: {name} = {value}");
    }

    // === Timers ===

    private void StatusTimer_Tick(object? sender, EventArgs e)
    {
        if (_vehicleState.IsConnected &&
            (DateTime.UtcNow - _vehicleState.LastHeartbeat).TotalSeconds > 5)
        {
            _vehicleState.IsConnected = false;
            lblConnection.Text = "Connection Lost";
            lblConnection.ForeColor = ModernTheme.Danger;
        }
    }

    private void HeartbeatTimer_Tick(object? sender, EventArgs e)
    {
        if (!_stream.IsOpen) return;

        var payload = new byte[9];
        payload[0] = 0;
        BitConverter.GetBytes(0u).CopyTo(payload, 1);
        payload[5] = 6;
        payload[6] = MavlinkDefinitions.MAV_AUTOPILOT_GENERIC;

        var packet = MavlinkCodec.Encode(
            _vehicleState.SystemId, 0, MavlinkDefinitions.HEARTBEAT, payload);
        _stream.Send(packet);
    }

    private void UpdateStatusBar()
    {
        if (_vehicleState.IsConnected)
        {
            lblMode.Text = $"Mode: {_vehicleState.FlightModeName}";
            lblMode.ForeColor = _vehicleState.IsArmed ? ModernTheme.Success : ModernTheme.TextSecondary;
            lblArmed.Text = _vehicleState.IsArmed ? "ARMED" : "Disarmed";
            lblArmed.ForeColor = _vehicleState.IsArmed ? ModernTheme.Armed : ModernTheme.Disarmed;
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
/// Simple input dialog.
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
        BackColor = Utils.ModernTheme.Surface;
        ForeColor = Utils.ModernTheme.TextPrimary;

        Controls.Add(new Label { Text = prompt, Location = new Point(15, 15), AutoSize = true, ForeColor = Utils.ModernTheme.TextSecondary });
        txtInput = new TextBox { Location = new Point(15, 45), Width = 300, Text = defaultValue, BackColor = Utils.ModernTheme.SurfaceLight, ForeColor = Utils.ModernTheme.TextPrimary, BorderStyle = BorderStyle.FixedSingle };
        Controls.Add(txtInput);

        var btnOk = new Button
        {
            Text = "OK",
            Location = new Point(120, 80),
            Size = new Size(80, 30),
            DialogResult = DialogResult.OK,
            BackColor = Utils.ModernTheme.Accent,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        Controls.Add(btnOk);
        AcceptButton = btnOk;
    }
}
