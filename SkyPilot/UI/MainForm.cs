using SkyPilot.Core.Mavlink;
using SkyPilot.Core.Sim;
using SkyPilot.Log;
using SkyPilot.Panels;
using SkyPilot.Utils;
using SkyPilot.Core.Audio;

namespace SkyPilot.UI;

/// <summary>
/// Main application window - borderless glass cockpit design.
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
    private NeonHud? _hudPanel;
    private MessageLogPanel? _messageLog;
    private MissionPanel? _missionPanel;
    private ParameterPanel? _paramPanel;
    private MapPanel? _mapPanel;
    private AudioCallout? _audioCallout;
    private VirtualStickControl? _virtualSticks;
    private TelemetryHUD? _telemetryHud;
    private Panel? _mapContainer;
    private Panel? _sticksPanel;
    private bool _manualMode;

    // Nav state
    private Panels.ModernButton? _activeNav;
    private Control? _activeContent;
    private string _selectedVehicleType = "plane";
    private string _selectedPattern = "circle";
    private double _simStartLat = 51.5074;
    private double _simStartLon = -0.1278;
    private double _simTargetLat = 51.51;
    private double _simTargetLon = -0.13;

    // Borderless window dragging
    private bool _dragging;
    private Point _dragStart;

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
                0 => "OK", 1 => "Rejected", 2 => "Denied",
                3 => "Unsupported", 4 => "Failed", _ => $"#{result}"
            };
            BeginInvoke(() => _messageLog?.AddMessage($"ACK {cmd}: {resultText}"));
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
        SetupWindowControls();
        WireEvents();

        SwitchTab(navOverview, _overviewPanel!);
    }

    private void SetupPanels()
    {
        _overviewPanel = new OverviewPanel();
        _hudPanel = new NeonHud();

        // Split: HUD left, overview right
        var overviewSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 420,
            BackColor = ModernTheme.Background
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
        _mapPanel = new MapPanel();
        _audioCallout = new AudioCallout();

        // Map container with virtual sticks below
        _sticksPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 130,
            BackColor = ModernTheme.Background
        };
        _virtualSticks = new VirtualStickControl
        {
            Dock = DockStyle.Fill
        };
        _virtualSticks.StickChanged += (lx, ly, rx, ry) =>
        {
            _sim?.SetManualInput(ly, lx, ry, rx);
        };
        _sticksPanel.Controls.Add(_virtualSticks);

        _mapContainer = new Panel { Dock = DockStyle.Fill, BackColor = ModernTheme.Background };
        _telemetryHud = new TelemetryHUD
        {
            Size = new Size(180, 210),
            Location = new Point(10, 10),
            Anchor = AnchorStyles.Top | AnchorStyles.Left
        };
        _mapContainer.Controls.Add(_telemetryHud);
        _mapContainer.Controls.Add(_mapPanel);
        _mapContainer.Controls.Add(_sticksPanel);
        _sticksPanel.Visible = false;
        _telemetryHud.Visible = false;
        _mapPanel.WaypointAdded += (lat, lon, idx) =>
        {
            _missionPanel?.AddWaypointFromMap(lat, lon);
            _messageLog?.AddMessage($"WP{idx} added: ({lat:F6},{lon:F6})", 6);
        };
        _mapPanel.WaypointUpdated += (idx, lat, lon, alt, spd) =>
        {
            _missionPanel?.UpdateWaypointFromMap(idx, lat, lon, alt, spd);
        };
        _mapPanel.ExportKmlRequested += () =>
        {
            // Trigger KML export from mission panel
            var waypoints = _missionPanel?.GetWaypoints();
            if (waypoints == null || waypoints.Count == 0)
            {
                MessageBox.Show("No waypoints to export.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using var sfd = new SaveFileDialog { Filter = "KML files|*.kml|All files|*.*", FileName = "mission.kml" };
            if (sfd.ShowDialog(this) != DialogResult.OK) return;
            ExportKml(sfd.FileName, waypoints);
        };
        _mapPanel.SyncMissionRequested += () =>
        {
            var waypoints = _missionPanel?.GetWaypoints();
            if (waypoints != null && waypoints.Count > 0)
            {
                var coords = waypoints.Select(w => (w.Lat, w.Lon)).ToList();
                _mapPanel.SyncWaypoints(coords);
            }
        };
        _mapPanel.SimStartPosReceived += (lat, lon) =>
        {
            _simStartLat = lat;
            _simStartLon = lon;
            _messageLog?.AddMessage($"Start set: ({lat:F6},{lon:F6})", 6);
        };
        _mapPanel.SimTargetPosReceived += (lat, lon) =>
        {
            _simTargetLat = lat;
            _simTargetLon = lon;
            _messageLog?.AddMessage($"Target set: ({lat:F6},{lon:F6})", 6);
        };
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
        navSim.Click += (s, e) => StartSimulatorToolStripMenuItem_Click();
        cmbVehicle.SelectedIndexChanged += (s, e) =>
        {
            if (cmbVehicle.SelectedItem is string type)
                _selectedVehicleType = type.ToLower();
        };
        cmbPattern.SelectedIndexChanged += (s, e) =>
        {
            if (cmbPattern.SelectedItem is string pattern)
                _selectedPattern = pattern switch
                {
                    "Point to Point" => "point2point",
                    "Circle" => "circle",
                    "Distance" => "distance",
                    _ => pattern.ToLower().Replace(" ", "")
                };
        };
        navMap.Click += (s, e) => SwitchTab(navMap, _mapPanel!);
    }

    private void SetupWindowControls()
    {
        // Borderless window drag
        titleBar.MouseDown += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                _dragging = true;
                _dragStart = e.Location;
            }
        };
        titleBar.MouseMove += (s, e) =>
        {
            if (_dragging)
            {
                Location = new Point(Location.X + e.X - _dragStart.X, Location.Y + e.Y - _dragStart.Y);
            }
        };
        titleBar.MouseUp += (s, e) => _dragging = false;
        titleBar.MouseDoubleClick += (s, e) =>
        {
            WindowState = WindowState == FormWindowState.Maximized
                ? FormWindowState.Normal : FormWindowState.Maximized;
        };

        // Window buttons
        btnClose.Click += (s, e) => Close();
        btnMinimize.Click += (s, e) => WindowState = FormWindowState.Minimized;
        btnMaximize.Click += (s, e) =>
        {
            WindowState = WindowState == FormWindowState.Maximized
                ? FormWindowState.Normal : FormWindowState.Maximized;
        };

        btnClose.BackColor = ModernTheme.Danger;
        btnClose.ForeColor = Color.White;
    }

    private void SwitchTab(Panels.ModernButton nav, Control content)
    {
        if (_activeNav != null) _activeNav.BaseColor = ModernTheme.TextMuted;
        if (_activeContent != null) _activeContent.Visible = false;

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
        btnConnect.Click += (s, e) => ConnectSerialToolStripMenuItem_Click(s!, e);
        btnDisconnect.Click += (s, e) => DisconnectToolStripMenuItem_Click(s!, e);

        btnArm.Click += (s, e) => SendCommand(MavlinkDefinitions.MAV_CMD_COMPONENT_ARM_DISARM, 1.0f);
        btnDisarm.Click += (s, e) => SendCommand(MavlinkDefinitions.MAV_CMD_COMPONENT_ARM_DISARM, 0.0f);
        btnTakeoff.Click += (s, e) => TakeoffToolStripMenuItem_Click(s!, e);
        btnRTL.Click += (s, e) => SendCommand(MavlinkDefinitions.MAV_CMD_NAV_RETURN_TO_LAUNCH);
        btnManual.Click += (s, e) =>
        {
            if (_sim == null) return;
            _manualMode = !_manualMode;
            _sim.SetManualMode(_manualMode);
            btnManual.Text = _manualMode ? "AUTO Flight" : "Manual Flight";
            btnManual.BaseColor = _manualMode ? ModernTheme.Warning : ModernTheme.Accent;
            _messageLog?.AddMessage(_manualMode ? "Manual flight enabled" : "Auto flight resumed", 6);
        };
        btnExit.Click += (s, e) => Close();
        btnEmergencyStop.Click += (s, e) =>
        {
            if (MessageBox.Show("EMERGENCY MOTOR STOP?", "Emergency",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                SendCommand(MavlinkDefinitions.MAV_CMD_COMPONENT_ARM_DISARM, 0, 21196);
                _messageLog?.AddMessage("EMERGENCY STOP!", 2);
            }
        };

        cmbMode.SelectedIndexChanged += (s, e) =>
        {
            if (cmbMode.SelectedItem is string modeName)
            {
                var mode = MavlinkDefinitions.FlightModes.FirstOrDefault(x => x.Value == modeName);
                SetMode(mode.Key);
                _messageLog?.AddMessage($"Mode: {modeName}");
            }
        };

        lblConnection.Click += (s, e) => ConnectSerialToolStripMenuItem_Click(s!, e);
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
                _messageLog?.AddMessage($"Connected {dlg.SelectedPort} @ {dlg.SelectedBaud}");
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
                _heartbeatTimer.Start();
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
            MessageBox.Show("Disconnect first.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // Use manually set positions from map (Set Start / Set Target buttons)
        var mapWps = _mapPanel?.GetWaypoints() ?? new();
        _sim = new VirtualVehicle(_selectedVehicleType, _selectedPattern,
            startLat: _simStartLat, startLon: _simStartLon,
            targetLat: _simTargetLat, targetLon: _simTargetLon,
            intermediateWaypoints: mapWps.Count > 0 ? mapWps : null,
            altitude: trackAlt.Value);
        _mapPanel?.SetVehicleType(_selectedVehicleType);
        SwitchTab(navMap, _mapContainer!);
        _mapPanel?.ShowFlightPath(_sim.StartLat, _sim.StartLon,
            _sim.TargetLat, _sim.TargetLon, _selectedPattern, mapWps.Count > 0 ? mapWps : null);
        _stream.OpenSimulation(_sim);
        _sticksPanel!.Visible = true;
        _telemetryHud!.Visible = true;
        lblConnection.Text = $"SIM ({_selectedVehicleType}) - {_selectedPattern}";
        lblConnection.ForeColor = ModernTheme.Warning;
        _messageLog?.AddMessage($"Sim: ({_sim.StartLat:F6},{_sim.StartLon:F6}) → ({_sim.TargetLat:F6},{_sim.TargetLon:F6})", 6);
    }

    private void StopSimulatorToolStripMenuItem_Click(object? sender = null, EventArgs? e = null)
    {
        if (_sim != null)
        {
            _stream.Close();
            _sim = null;
            _vehicleState.IsConnected = false;
            _mapPanel?.HideFlightPath();
            _sticksPanel!.Visible = false;
            _telemetryHud!.Visible = false;
            _manualMode = false;
            lblConnection.Text = "Disconnected";
            lblConnection.ForeColor = ModernTheme.TextMuted;
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
        using var input = new InputBox("Takeoff altitude (m):", "50");
        if (input.ShowDialog(this) == DialogResult.OK && float.TryParse(input.Value, out float alt))
        {
            SetMode(4);
            SendCommand(MavlinkDefinitions.MAV_CMD_NAV_TAKEOFF, alt);
            _messageLog?.AddMessage($"Takeoff {alt}m");
        }
    }

    private void SendCommand(ushort command, float param1 = 0, float param2 = 0)
    {
        if (!_stream.IsOpen) return;
        var payload = new byte[32];
        BitConverter.GetBytes(command).CopyTo(payload, 0);
        BitConverter.GetBytes(param1).CopyTo(payload, 4);
        BitConverter.GetBytes(param2).CopyTo(payload, 8);
        _stream.Send(MavlinkCodec.Encode(_vehicleState.SystemId, 0, MavlinkDefinitions.COMMAND_LONG, payload));
    }

    private void SetMode(int mode)
    {
        if (!_stream.IsOpen) return;
        var payload = new byte[8];
        payload[0] = _vehicleState.SystemId;
        BitConverter.GetBytes((uint)mode).CopyTo(payload, 4);
        _stream.Send(MavlinkCodec.Encode(_vehicleState.SystemId, 0, MavlinkDefinitions.SET_MODE, payload));
    }

    private void SendParamRequestList()
    {
        if (!_stream.IsOpen) return;
        var payload = new byte[2];
        payload[0] = _vehicleState.SystemId;
        _stream.Send(MavlinkCodec.Encode(_vehicleState.SystemId, 0, MavlinkDefinitions.PARAM_REQUEST_LIST, payload));
    }

    private void SendParamSet(string name, float value)
    {
        if (!_stream.IsOpen) return;
        var payload = new byte[23];
        var nameBytes = System.Text.Encoding.ASCII.GetBytes(name);
        Array.Copy(nameBytes, 0, payload, 0, Math.Min(nameBytes.Length, 16));
        BitConverter.GetBytes(value).CopyTo(payload, 16);
        BitConverter.GetBytes((ushort)9).CopyTo(payload, 20);
        _stream.Send(MavlinkCodec.Encode(_vehicleState.SystemId, 0, MavlinkDefinitions.PARAM_SET, payload));
    }

    // === Timers ===

    private void StatusTimer_Tick(object? sender, EventArgs e)
    {
        if (_vehicleState.IsConnected &&
            (DateTime.UtcNow - _vehicleState.LastHeartbeat).TotalSeconds > 5)
        {
            _vehicleState.IsConnected = false;
            lblConnection.Text = "Lost";
            lblConnection.ForeColor = ModernTheme.Danger;
        }
    }

    private void HeartbeatTimer_Tick(object? sender, EventArgs e)
    {
        if (!_stream.IsOpen) return;
        var payload = new byte[9];
        BitConverter.GetBytes(0u).CopyTo(payload, 1);
        payload[5] = 6;
        payload[6] = MavlinkDefinitions.MAV_AUTOPILOT_GENERIC;
        _stream.Send(MavlinkCodec.Encode(_vehicleState.SystemId, 0, MavlinkDefinitions.HEARTBEAT, payload));
    }

    private void UpdateStatusBar()
    {
        if (!_vehicleState.IsConnected) return;

        lblStatusMode.Text = $"Mode: {_vehicleState.FlightModeName}";
        lblStatusMode.ForeColor = ModernTheme.Accent;

        lblStatusArmed.Text = _vehicleState.IsArmed ? "ARMED" : "Disarmed";
        lblStatusArmed.ForeColor = _vehicleState.IsArmed ? ModernTheme.Armed : ModernTheme.Disarmed;

        lblStatusAlt.Text = $"Alt: {_vehicleState.AltitudeRel:F1}m";
        lblStatusSpeed.Text = $"GS: {_vehicleState.GroundSpeed:F1}m/s";
        lblStatusBatt.Text = $"Batt: {_vehicleState.BatteryVoltage:F1}V";

        _hudPanel?.UpdateFromState(_vehicleState);
        _overviewPanel?.UpdateFromState(_vehicleState);
        _sensorsPanel?.UpdateFromState(_vehicleState);
        if (_vehicleState.Latitude != 0 && _vehicleState.Longitude != 0)
            _mapPanel?.UpdatePosition(_vehicleState.Latitude, _vehicleState.Longitude, _vehicleState.Yaw);

        // Update telemetry HUD
        _telemetryHud?.UpdateValues(
            _vehicleState.AltitudeRel,
            _vehicleState.GroundSpeed,
            _vehicleState.BatteryRemaining,
            _vehicleState.Yaw,
            _vehicleState.FlightModeName,
            _vehicleState.IsArmed,
            _vehicleState.SatelliteCount);
    }

    private void ExportKml(string path, List<Waypoint> waypoints)
    {
        var coords = new List<string>();
        var placemarks = new List<string>();

        foreach (var wp in waypoints)
        {
            coords.Add($"{wp.Lon:F7},{wp.Lat:F7},{wp.Alt:F1}");
            placemarks.Add($@"
    <Placemark>
      <name>WP{wp.Seq} - {wp.Command}</name>
      <description>Altitude: {wp.Alt:F1}m | Speed: {wp.Param2:F1}m/s | {wp.Command}</description>
      <Point>
        <coordinates>{wp.Lon:F7},{wp.Lat:F7},{wp.Alt:F1}</coordinates>
      </Point>
    </Placemark>");
        }

        string kml = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<kml xmlns=""http://www.opengis.net/kml/2.2"">
  <Document>
    <name>SkyPilot Mission</name>
    <Style id=""routeStyle""><LineStyle><color>ff00aaff</color><width>3</width></LineStyle></Style>
    <Placemark>
      <name>Mission Route</name>
      <styleUrl>#routeStyle</styleUrl>
      <LineString>
        <tessellate>1</tessellate>
        <altitudeMode>relativeToGround</altitudeMode>
        <coordinates>{string.Join(" ", coords)}</coordinates>
      </LineString>
    </Placemark>
    {string.Join("", placemarks)}
  </Document>
</kml>";
        File.WriteAllText(path, kml);
        MessageBox.Show($"KML exported: {waypoints.Count} waypoints\n{path}", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _statusTimer.Stop();
        _heartbeatTimer.Stop();
        _audioCallout?.Dispose();
        _stream.Dispose();
        _settings.Save();
        base.OnFormClosing(e);
    }
}

public class InputBox : Form
{
    public string Value => txtInput.Text;
    private readonly TextBox txtInput;

    public InputBox(string prompt, string defaultValue = "")
    {
        FormBorderStyle = FormBorderStyle.FixedDialog;
        Size = new Size(320, 180);
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = ModernTheme.Surface;
        ForeColor = ModernTheme.TextPrimary;

        Controls.Add(new Label { Text = prompt, Location = new Point(15, 15), AutoSize = true, ForeColor = ModernTheme.TextSecondary });
        txtInput = new TextBox { Location = new Point(15, 42), Width = 275, Text = defaultValue, BackColor = ModernTheme.SurfaceLight, ForeColor = ModernTheme.TextPrimary, BorderStyle = BorderStyle.FixedSingle };
        Controls.Add(txtInput);

        var btnOk = new Button
        {
            Text = "OK",
            Location = new Point(110, 100),
            Size = new Size(80, 32),
            DialogResult = DialogResult.OK,
            BackColor = ModernTheme.Accent,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnOk.FlatAppearance.BorderSize = 0;
        Controls.Add(btnOk);
        AcceptButton = btnOk;
    }
}
