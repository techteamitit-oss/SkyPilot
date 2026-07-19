namespace SkyPilot.UI;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;
    private MenuStrip menuStrip;
    private ToolStrip toolStrip;
    private StatusStrip statusStrip;
    private ToolStripStatusLabel lblConnection;
    private ToolStripStatusLabel lblMode;
    private ToolStripStatusLabel lblArmed;
    private ToolStripStatusLabel lblAlt;
    private ToolStripStatusLabel lblSpeed;
    private ToolStripStatusLabel lblBatt;
    private TabControl tabFlight;

    // Menus
    private ToolStripMenuItem fileMenu;
    private ToolStripMenuItem connectSerialToolStripMenuItem;
    private ToolStripMenuItem connectUdpToolStripMenuItem;
    private ToolStripMenuItem connectTcpToolStripMenuItem;
    private ToolStripMenuItem disconnectToolStripMenuItem;
    private ToolStripMenuItem startSimulatorToolStripMenuItem;
    private ToolStripMenuItem stopSimulatorToolStripMenuItem;
    private ToolStripMenuItem openLogFileToolStripMenuItem;
    private ToolStripMenuItem exitToolStripMenuItem;

    private ToolStripMenuItem flightMenu;
    private ToolStripMenuItem armToolStripMenuItem;
    private ToolStripMenuItem disarmToolStripMenuItem;
    private ToolStripMenuItem rtlToolStripMenuItem;
    private ToolStripMenuItem autoToolStripMenuItem;
    private ToolStripMenuItem takeoffToolStripMenuItem;
    private ToolStripMenuItem landToolStripMenuItem;

    // Toolbar buttons
    private ToolStripButton btnConnect;
    private ToolStripButton btnArm;
    private ToolStripButton btnDisarm;
    private ToolStripButton btnTakeoff;
    private ToolStripButton btnRTL;
    private ToolStripComboBox modeCmb;
    private ToolStripButton btnEmergencyStop;
    private ToolStripSeparator toolStripSeparator1;
    private ToolStripSeparator toolStripSeparator2;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        menuStrip = new MenuStrip();
        toolStrip = new ToolStrip();
        statusStrip = new StatusStrip();
        tabFlight = new TabControl();

        // === Menu ===
        fileMenu = new ToolStripMenuItem();
        connectSerialToolStripMenuItem = new ToolStripMenuItem();
        connectUdpToolStripMenuItem = new ToolStripMenuItem();
        connectTcpToolStripMenuItem = new ToolStripMenuItem();
        disconnectToolStripMenuItem = new ToolStripMenuItem();
        startSimulatorToolStripMenuItem = new ToolStripMenuItem();
        stopSimulatorToolStripMenuItem = new ToolStripMenuItem();
        openLogFileToolStripMenuItem = new ToolStripMenuItem();
        exitToolStripMenuItem = new ToolStripMenuItem();

        flightMenu = new ToolStripMenuItem();
        armToolStripMenuItem = new ToolStripMenuItem();
        disarmToolStripMenuItem = new ToolStripMenuItem();
        rtlToolStripMenuItem = new ToolStripMenuItem();
        autoToolStripMenuItem = new ToolStripMenuItem();
        takeoffToolStripMenuItem = new ToolStripMenuItem();
        landToolStripMenuItem = new ToolStripMenuItem();

        // === Status bar ===
        lblConnection = new ToolStripStatusLabel();
        lblMode = new ToolStripStatusLabel();
        lblArmed = new ToolStripStatusLabel();
        lblAlt = new ToolStripStatusLabel();
        lblSpeed = new ToolStripStatusLabel();
        lblBatt = new ToolStripStatusLabel();

        // === Toolbar buttons ===
        btnConnect = new ToolStripButton();
        btnArm = new ToolStripButton();
        btnDisarm = new ToolStripButton();
        btnTakeoff = new ToolStripButton();
        btnRTL = new ToolStripButton();
        modeCmb = new ToolStripComboBox();
        btnEmergencyStop = new ToolStripButton();
        toolStripSeparator1 = new ToolStripSeparator();
        toolStripSeparator2 = new ToolStripSeparator();

        SuspendLayout();

        // --- Menu Strip ---
        fileMenu.Text = "&File";
        fileMenu.DropDownItems.AddRange(new ToolStripItem[] {
            connectSerialToolStripMenuItem, connectUdpToolStripMenuItem,
            connectTcpToolStripMenuItem, disconnectToolStripMenuItem,
            new ToolStripSeparator(),
            startSimulatorToolStripMenuItem, stopSimulatorToolStripMenuItem,
            new ToolStripSeparator(),
            openLogFileToolStripMenuItem, new ToolStripSeparator(), exitToolStripMenuItem
        });
        connectSerialToolStripMenuItem.Text = "Connect &Serial...";
        connectSerialToolStripMenuItem.Click += connectSerialToolStripMenuItem_Click;
        connectUdpToolStripMenuItem.Text = "Connect &UDP...";
        connectUdpToolStripMenuItem.Click += connectUdpToolStripMenuItem_Click;
        connectTcpToolStripMenuItem.Text = "Connect &TCP...";
        connectTcpToolStripMenuItem.Click += connectTcpToolStripMenuItem_Click;
        disconnectToolStripMenuItem.Text = "&Disconnect";
        disconnectToolStripMenuItem.Click += disconnectToolStripMenuItem_Click;
        startSimulatorToolStripMenuItem.Text = "Start &Simulator";
        startSimulatorToolStripMenuItem.Click += startSimulatorToolStripMenuItem_Click;
        stopSimulatorToolStripMenuItem.Text = "Stop Sim&ulator";
        stopSimulatorToolStripMenuItem.Click += stopSimulatorToolStripMenuItem_Click;
        openLogFileToolStripMenuItem.Text = "Open &Log File...";
        openLogFileToolStripMenuItem.Click += openLogFileToolStripMenuItem_Click;
        exitToolStripMenuItem.Text = "E&xit";
        exitToolStripMenuItem.Click += exitToolStripMenuItem_Click;

        flightMenu.Text = "&Flight";
        flightMenu.DropDownItems.AddRange(new ToolStripItem[] {
            armToolStripMenuItem, disarmToolStripMenuItem,
            new ToolStripSeparator(),
            takeoffToolStripMenuItem, landToolStripMenuItem,
            new ToolStripSeparator(),
            rtlToolStripMenuItem, autoToolStripMenuItem
        });
        armToolStripMenuItem.Text = "&Arm";
        armToolStripMenuItem.Click += armToolStripMenuItem_Click;
        disarmToolStripMenuItem.Text = "&Disarm";
        disarmToolStripMenuItem.Click += disarmToolStripMenuItem_Click;
        rtlToolStripMenuItem.Text = "&RTL";
        rtlToolStripMenuItem.Click += rtlToolStripMenuItem_Click;
        autoToolStripMenuItem.Text = "&Auto";
        autoToolStripMenuItem.Click += autoToolStripMenuItem_Click;
        takeoffToolStripMenuItem.Text = "&Takeoff...";
        takeoffToolStripMenuItem.Click += takeoffToolStripMenuItem_Click;
        landToolStripMenuItem.Text = "&Land";
        landToolStripMenuItem.Click += landToolStripMenuItem_Click;

        menuStrip.Items.AddRange(new ToolStripItem[] { fileMenu, flightMenu });
        menuStrip.Dock = DockStyle.Top;

        // --- Toolbar ---
        btnConnect.Text = "Connect";
        btnConnect.DisplayStyle = ToolStripItemDisplayStyle.Text;
        btnConnect.Click += btnConnect_Click;
        btnConnect.BackColor = Color.FromArgb(50, 50, 50);

        btnArm.Text = "Arm";
        btnArm.DisplayStyle = ToolStripItemDisplayStyle.Text;
        btnArm.Click += btnArm_Click;
        btnArm.BackColor = Color.FromArgb(50, 50, 50);

        btnDisarm.Text = "Disarm";
        btnDisarm.DisplayStyle = ToolStripItemDisplayStyle.Text;
        btnDisarm.Click += btnDisarm_Click;
        btnDisarm.BackColor = Color.FromArgb(50, 50, 50);

        btnTakeoff.Text = "Takeoff";
        btnTakeoff.DisplayStyle = ToolStripItemDisplayStyle.Text;
        btnTakeoff.Click += btnTakeoff_Click;
        btnTakeoff.BackColor = Color.FromArgb(50, 50, 50);

        btnRTL.Text = "RTL";
        btnRTL.DisplayStyle = ToolStripItemDisplayStyle.Text;
        btnRTL.Click += btnRTL_Click;
        btnRTL.BackColor = Color.FromArgb(50, 50, 50);

        modeCmb.Text = "Mode...";
        modeCmb.Width = 120;
        modeCmb.BackColor = Color.FromArgb(50, 50, 50);
        modeCmb.ForeColor = Color.White;
        modeCmb.Items.AddRange(new object[] {
            "Stabilize", "Acro", "AltHold", "Auto", "Guided",
            "Loiter", "RTL", "Circle", "Land", "Sport", "PosHold", "Brake"
        });
        modeCmb.SelectedIndexChanged += modeCmb_SelectedIndexChanged;

        btnEmergencyStop.Text = "EMERGENCY STOP";
        btnEmergencyStop.DisplayStyle = ToolStripItemDisplayStyle.Text;
        btnEmergencyStop.ForeColor = Color.Red;
        btnEmergencyStop.BackColor = Color.FromArgb(80, 20, 20);
        btnEmergencyStop.Click += btnEmergencyStop_Click;

        toolStrip.Items.AddRange(new ToolStripItem[] {
            btnConnect, toolStripSeparator1,
            btnArm, btnDisarm, btnTakeoff, btnRTL,
            toolStripSeparator2,
            modeCmb, btnEmergencyStop
        });
        toolStrip.Dock = DockStyle.Top;
        toolStrip.Padding = new Padding(4, 2, 4, 2);

        // --- Tab pages ---
        tabFlight.TabPages.Add("tabOverview", "Overview");
        tabFlight.TabPages.Add("tabSensors", "Sensors");
        tabFlight.TabPages.Add("tabMission", "Mission");
        tabFlight.TabPages.Add("tabMessages", "Messages");
        tabFlight.TabPages.Add("tabParams", "Parameters");
        tabFlight.TabPages.Add("tabLogs", "Log Analysis");
        tabFlight.Dock = DockStyle.Fill;

        // --- Status bar ---
        lblConnection.Text = "Disconnected";
        lblConnection.ForeColor = Color.Gray;
        lblMode.Text = "Mode: --";
        lblArmed.Text = "Disarmed";
        lblAlt.Text = "Alt: --";
        lblSpeed.Text = "GS: --";
        lblBatt.Text = "Batt: --";
        statusStrip.Items.AddRange(new ToolStripItem[] {
            lblConnection, new ToolStripStatusLabel("  |  "),
            lblMode, new ToolStripStatusLabel("  |  "),
            lblArmed, new ToolStripStatusLabel("  |  "),
            lblAlt, new ToolStripStatusLabel("  |  "),
            lblSpeed, new ToolStripStatusLabel("  |  "),
            lblBatt
        });

        // --- MainForm ---
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1200, 700);
        Controls.Add(tabFlight);
        Controls.Add(toolStrip);
        Controls.Add(statusStrip);
        Controls.Add(menuStrip);
        MainMenuStrip = menuStrip;
        Name = "MainForm";
        Text = "SkyPilot - Ground Control Station";
        StartPosition = FormStartPosition.CenterScreen;
        ResumeLayout(false);
        PerformLayout();
    }
}
