namespace SkyPilot.UI;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;
    private Panel sidePanel;
    private Panel topBar;
    private Panel contentPanel;
    private StatusStrip statusStrip;
    private Label lblTitle;
    private Label lblConnection;
    private ToolStripStatusLabel lblMode;
    private ToolStripStatusLabel lblArmed;
    private ToolStripStatusLabel lblAlt;
    private ToolStripStatusLabel lblSpeed;
    private ToolStripStatusLabel lblBatt;

    // Navigation buttons
    private Panel navButtons;
    private Panels.ModernButton navOverview;
    private Panels.ModernButton navSensors;
    private Panels.ModernButton navMission;
    private Panels.ModernButton navMessages;
    private Panels.ModernButton navParams;
    private Panels.ModernButton navLogs;

    // Action buttons
    private Panels.ModernButton btnArm;
    private Panels.ModernButton btnDisarm;
    private Panels.ModernButton btnTakeoff;
    private Panels.ModernButton btnRTL;
    private Panels.ModernButton btnEmergencyStop;

    // Mode selector
    private Label lblModeSelector;
    private ComboBox cmbMode;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        SuspendLayout();

        // === TOP BAR ===
        topBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 52,
            BackColor = Utils.ModernTheme.Surface
        };

        lblTitle = new Label
        {
            Text = "SkyPilot",
            Font = new Font("Segoe UI", 16f, FontStyle.Bold),
            ForeColor = Utils.ModernTheme.Accent,
            AutoSize = true,
            Location = new Point(16, 12)
        };

        lblConnection = new Label
        {
            Text = "Disconnected",
            Font = Utils.ModernTheme.FontRegular,
            ForeColor = Utils.ModernTheme.TextMuted,
            AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(900, 18)
        };

        topBar.Controls.Add(lblTitle);
        topBar.Controls.Add(lblConnection);

        // === LEFT SIDE PANEL ===
        sidePanel = new Panel
        {
            Dock = DockStyle.Left,
            Width = 200,
            BackColor = Utils.ModernTheme.Surface
        };

        // Nav section label
        var navLabel = new Label
        {
            Text = "NAVIGATION",
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            ForeColor = Utils.ModernTheme.TextMuted,
            Location = new Point(16, 16),
            AutoSize = true
        };
        sidePanel.Controls.Add(navLabel);

        navOverview = MakeNavButton("Overview", 40);
        navSensors = MakeNavButton("Sensors", 80);
        navMission = MakeNavButton("Mission", 120);
        navMessages = MakeNavButton("Messages", 160);
        navParams = MakeNavButton("Parameters", 200);
        navLogs = MakeNavButton("Log Analysis", 240);

        navOverview.BaseColor = Utils.ModernTheme.Accent; // active

        sidePanel.Controls.Add(navOverview);
        sidePanel.Controls.Add(navSensors);
        sidePanel.Controls.Add(navMission);
        sidePanel.Controls.Add(navMessages);
        sidePanel.Controls.Add(navParams);
        sidePanel.Controls.Add(navLogs);

        // Action section
        var actionLabel = new Label
        {
            Text = "ACTIONS",
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            ForeColor = Utils.ModernTheme.TextMuted,
            Location = new Point(16, 300),
            AutoSize = true
        };
        sidePanel.Controls.Add(actionLabel);

        btnArm = MakeActionButton("Arm", 330, Utils.ModernTheme.Success);
        btnDisarm = MakeActionButton("Disarm", 370, Utils.ModernTheme.Warning);
        btnTakeoff = MakeActionButton("Takeoff", 410, Utils.ModernTheme.Accent);
        btnRTL = MakeActionButton("RTL", 450, Utils.ModernTheme.Info);

        btnEmergencyStop = new Panels.ModernButton("EMERGENCY STOP", Utils.ModernTheme.Danger)
        {
            Location = new Point(12, 495),
            Width = 176,
            Height = 36
        };

        sidePanel.Controls.Add(btnArm);
        sidePanel.Controls.Add(btnDisarm);
        sidePanel.Controls.Add(btnTakeoff);
        sidePanel.Controls.Add(btnRTL);
        sidePanel.Controls.Add(btnEmergencyStop);

        // Mode selector
        lblModeSelector = new Label
        {
            Text = "FLIGHT MODE",
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            ForeColor = Utils.ModernTheme.TextMuted,
            Location = new Point(16, 548),
            AutoSize = true
        };
        sidePanel.Controls.Add(lblModeSelector);

        cmbMode = new ComboBox
        {
            Location = new Point(12, 570),
            Width = 176,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Utils.ModernTheme.SurfaceLight,
            ForeColor = Utils.ModernTheme.TextPrimary,
            FlatStyle = FlatStyle.Standard,
            Font = Utils.ModernTheme.FontRegular
        };
        cmbMode.Items.AddRange(new object[] {
            "Stabilize", "Acro", "AltHold", "Auto", "Guided",
            "Loiter", "RTL", "Circle", "Land", "Sport", "PosHold", "Brake"
        });
        sidePanel.Controls.Add(cmbMode);

        // Connect/Disconnect buttons at top of side panel
        var btnConnect = new Panels.ModernButton("Connect", Utils.ModernTheme.Accent)
        {
            Location = new Point(12, 620),
            Width = 85,
            Height = 32
        };
        var btnDisconnect = new Panels.ModernButton("Disconnect", Utils.ModernTheme.TextMuted)
        {
            Location = new Point(103, 620),
            Width = 85,
            Height = 32
        };
        sidePanel.Controls.Add(btnConnect);
        sidePanel.Controls.Add(btnDisconnect);

        // Store references for event wiring
        _btnConnect = btnConnect;
        _btnDisconnect = btnDisconnect;

        // === CONTENT PANEL ===
        contentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Utils.ModernTheme.Background,
            Padding = new Padding(8)
        };

        // === STATUS BAR ===
        statusStrip = new StatusStrip
        {
            BackColor = Utils.ModernTheme.Surface,
            SizingGrip = false,
            Height = 28
        };

        lblMode = new ToolStripStatusLabel("Mode: --") { ForeColor = Utils.ModernTheme.TextSecondary, Font = Utils.ModernTheme.FontSmall };
        lblArmed = new ToolStripStatusLabel("Disarmed") { ForeColor = Utils.ModernTheme.Disarmed, Font = Utils.ModernTheme.FontSmall };
        lblAlt = new ToolStripStatusLabel("Alt: --") { ForeColor = Utils.ModernTheme.TextSecondary, Font = Utils.ModernTheme.FontSmall };
        lblSpeed = new ToolStripStatusLabel("GS: --") { ForeColor = Utils.ModernTheme.TextSecondary, Font = Utils.ModernTheme.FontSmall };
        lblBatt = new ToolStripStatusLabel("Batt: --") { ForeColor = Utils.ModernTheme.TextSecondary, Font = Utils.ModernTheme.FontSmall };

        statusStrip.Items.AddRange(new ToolStripItem[] {
            lblMode, new ToolStripStatusLabel("  |  ") { ForeColor = Utils.ModernTheme.Border },
            lblArmed, new ToolStripStatusLabel("  |  ") { ForeColor = Utils.ModernTheme.Border },
            lblAlt, new ToolStripStatusLabel("  |  ") { ForeColor = Utils.ModernTheme.Border },
            lblSpeed, new ToolStripStatusLabel(" | ") { ForeColor = Utils.ModernTheme.Border },
            lblBatt
        });

        // === ASSEMBLE ===
        Controls.Add(contentPanel);
        Controls.Add(sidePanel);
        Controls.Add(topBar);
        Controls.Add(statusStrip);

        // === MainForm ===
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1280, 780);
        BackColor = Utils.ModernTheme.Background;
        Name = "MainForm";
        Text = "SkyPilot";
        StartPosition = FormStartPosition.CenterScreen;
        Font = Utils.ModernTheme.FontRegular;

        ResumeLayout(false);
        PerformLayout();
    }

    private Panels.ModernButton MakeNavButton(string text, int y)
    {
        var btn = new Panels.ModernButton(text, Utils.ModernTheme.SurfaceLight)
        {
            Location = new Point(8, y),
            Width = 184,
            Height = 32
        };
        return btn;
    }

    private Panels.ModernButton MakeActionButton(string text, int y, Color color)
    {
        var btn = new Panels.ModernButton(text, color)
        {
            Location = new Point(12, y),
            Width = 176,
            Height = 32
        };
        return btn;
    }

    // Store references for event wiring in MainForm.cs
    private Panels.ModernButton? _btnConnect;
    private Panels.ModernButton? _btnDisconnect;
}
