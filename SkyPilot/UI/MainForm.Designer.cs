namespace SkyPilot.UI;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;
    private MenuStrip menuStrip;
    private StatusStrip statusStrip;
    private ToolStripStatusLabel lblConnection;
    private ToolStripStatusLabel lblMode;
    private ToolStripStatusLabel lblArmed;
    private ToolStripStatusLabel lblAlt;
    private ToolStripStatusLabel lblSpeed;
    private ToolStripStatusLabel lblBatt;
    private TabControl tabFlight;
    private ToolStripMenuItem fileMenu;
    private ToolStripMenuItem connectSerialToolStripMenuItem;
    private ToolStripMenuItem connectUdpToolStripMenuItem;
    private ToolStripMenuItem disconnectToolStripMenuItem;
    private ToolStripMenuItem openLogFileToolStripMenuItem;
    private ToolStripMenuItem exitToolStripMenuItem;
    private ToolStripMenuItem flightMenu;
    private ToolStripMenuItem armToolStripMenuItem;
    private ToolStripMenuItem disarmToolStripMenuItem;
    private ToolStripMenuItem rtlToolStripMenuItem;
    private ToolStripMenuItem autoToolStripMenuItem;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        menuStrip = new MenuStrip();
        statusStrip = new StatusStrip();
        tabFlight = new TabControl();

        fileMenu = new ToolStripMenuItem();
        connectSerialToolStripMenuItem = new ToolStripMenuItem();
        connectUdpToolStripMenuItem = new ToolStripMenuItem();
        disconnectToolStripMenuItem = new ToolStripMenuItem();
        openLogFileToolStripMenuItem = new ToolStripMenuItem();
        exitToolStripMenuItem = new ToolStripMenuItem();

        flightMenu = new ToolStripMenuItem();
        armToolStripMenuItem = new ToolStripMenuItem();
        disarmToolStripMenuItem = new ToolStripMenuItem();
        rtlToolStripMenuItem = new ToolStripMenuItem();
        autoToolStripMenuItem = new ToolStripMenuItem();

        lblConnection = new ToolStripStatusLabel();
        lblMode = new ToolStripStatusLabel();
        lblArmed = new ToolStripStatusLabel();
        lblAlt = new ToolStripStatusLabel();
        lblSpeed = new ToolStripStatusLabel();
        lblBatt = new ToolStripStatusLabel();

        SuspendLayout();

        // menuStrip
        fileMenu.Text = "&File";
        fileMenu.DropDownItems.AddRange(new ToolStripItem[] {
            connectSerialToolStripMenuItem, connectUdpToolStripMenuItem,
            disconnectToolStripMenuItem, new ToolStripSeparator(),
            openLogFileToolStripMenuItem, new ToolStripSeparator(), exitToolStripMenuItem
        });
        connectSerialToolStripMenuItem.Text = "Connect &Serial...";
        connectSerialToolStripMenuItem.Click += connectSerialToolStripMenuItem_Click;
        connectUdpToolStripMenuItem.Text = "Connect &UDP...";
        connectUdpToolStripMenuItem.Click += connectUdpToolStripMenuItem_Click;
        disconnectToolStripMenuItem.Text = "&Disconnect";
        disconnectToolStripMenuItem.Click += disconnectToolStripMenuItem_Click;
        openLogFileToolStripMenuItem.Text = "Open &Log File...";
        openLogFileToolStripMenuItem.Click += openLogFileToolStripMenuItem_Click;
        exitToolStripMenuItem.Text = "E&xit";
        exitToolStripMenuItem.Click += exitToolStripMenuItem_Click;

        flightMenu.Text = "&Flight";
        flightMenu.DropDownItems.AddRange(new ToolStripItem[] {
            armToolStripMenuItem, disarmToolStripMenuItem, rtlToolStripMenuItem, autoToolStripMenuItem
        });
        armToolStripMenuItem.Text = "&Arm";
        armToolStripMenuItem.Click += armToolStripMenuItem_Click;
        disarmToolStripMenuItem.Text = "&Disarm";
        disarmToolStripMenuItem.Click += disarmToolStripMenuItem_Click;
        rtlToolStripMenuItem.Text = "&RTL";
        rtlToolStripMenuItem.Click += rtlToolStripMenuItem_Click;
        autoToolStripMenuItem.Text = "&Auto";
        autoToolStripMenuItem.Click += autoToolStripMenuItem_Click;

        menuStrip.Items.AddRange(new ToolStripItem[] { fileMenu, flightMenu });
        menuStrip.Dock = DockStyle.Top;

        // Tab pages
        tabFlight.TabPages.Add("tabOverview", "Overview");
        tabFlight.TabPages.Add("tabSensors", "Sensors");
        tabFlight.TabPages.Add("tabMission", "Mission");
        tabFlight.TabPages.Add("tabLogs", "Log Analysis");
        tabFlight.Dock = DockStyle.Fill;

        // Status bar
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

        // MainForm
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1200, 700);
        Controls.Add(tabFlight);
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
