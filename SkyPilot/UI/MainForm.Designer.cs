namespace SkyPilot.UI;

using SkyPilot.Utils;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    // Layout
    private Panel titleBar;
    private Panel sidePanel;
    private Panel contentPanel;
    private Panel bottomBar;

    // Title bar
    private Label lblTitle;
    private Label lblConnection;
    private Button btnMinimize;
    private Button btnMaximize;
    private Button btnClose;

    // Status bar items
    private Label lblStatusMode;
    private Label lblStatusArmed;
    private Label lblStatusAlt;
    private Label lblStatusSpeed;
    private Label lblStatusBatt;

    // Navigation
    private Panels.ModernButton navOverview;
    private Panels.ModernButton navSensors;
    private Panels.ModernButton navMission;
    private Panels.ModernButton navMessages;
    private Panels.ModernButton navParams;
    private Panels.ModernButton navLogs;
    private Panels.ModernButton navMap;
    private Panels.ModernButton navSim;
    private ComboBox cmbVehicle;
    private ComboBox cmbPattern;

    // Action buttons
    private Panels.ModernButton btnArm;
    private Panels.ModernButton btnDisarm;
    private Panels.ModernButton btnTakeoff;
    private Panels.ModernButton btnRTL;
    private Panels.ModernButton btnEmergencyStop;
    private Panels.ModernButton btnExit;
    private Panels.ModernButton btnConnect;
    private Panels.ModernButton btnDisconnect;

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

        // === BORDERLESS FORM ===
        FormBorderStyle = FormBorderStyle.None;
        BackColor = ModernTheme.Background;

        // === TITLE BAR ===
        titleBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 40,
            BackColor = ModernTheme.Surface
        };

        lblTitle = new Label
        {
            Text = "  SKYPILOT",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = ModernTheme.Accent,
            AutoSize = true,
            Location = new Point(12, 10),
            BackColor = Color.Transparent
        };

        lblConnection = new Label
        {
            Text = "Disconnected",
            Font = ModernTheme.FontSmall,
            ForeColor = ModernTheme.TextMuted,
            AutoSize = true,
            Location = new Point(200, 13),
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand
        };

        // Window control buttons
        btnMinimize = MakeWindowButton(ModernTheme.TextMuted, ModernTheme.Surface, "─", 0);
        btnMaximize = MakeWindowButton(ModernTheme.TextMuted, ModernTheme.Surface, "□", 1);
        btnClose = MakeWindowButton(ModernTheme.TextMuted, ModernTheme.Danger, "×", 2);

        titleBar.Controls.Add(lblTitle);
        titleBar.Controls.Add(lblConnection);
        titleBar.Controls.Add(btnMinimize);
        titleBar.Controls.Add(btnMaximize);
        titleBar.Controls.Add(btnClose);

        // === LEFT SIDE PANEL ===
        sidePanel = new Panel
        {
            Dock = DockStyle.Left,
            Width = 160,
            BackColor = ModernTheme.Surface
        };

        // Section: Navigation
        var navLabel = new Label { Text = "NAVIGATION", Font = new Font("Segoe UI", 7f, FontStyle.Bold), ForeColor = ModernTheme.TextMuted, Location = new Point(12, 10), AutoSize = true, BackColor = Color.Transparent };
        sidePanel.Controls.Add(navLabel);

        navOverview = MakeTextButton("Overview", 30, ModernTheme.Accent);
        navSensors = MakeTextButton("Sensors", 62, ModernTheme.TextMuted);
        navMission = MakeTextButton("Mission", 94, ModernTheme.TextMuted);
        navMessages = MakeTextButton("Messages", 126, ModernTheme.TextMuted);
        navParams = MakeTextButton("Parameters", 158, ModernTheme.TextMuted);
        navSim = MakeTextButton("Simulator", 190, ModernTheme.Warning);
        navMap = MakeTextButton("Map", 222, ModernTheme.TextMuted);
        navLogs = MakeTextButton("Logs", 222, ModernTheme.TextMuted);

        sidePanel.Controls.Add(navOverview);
        sidePanel.Controls.Add(navSensors);
        sidePanel.Controls.Add(navMission);
        sidePanel.Controls.Add(navMessages);
        sidePanel.Controls.Add(navParams);
        sidePanel.Controls.Add(navSim);

        // Vehicle type selector
        var vehicleLabel = new Label { Text = "VEHICLE", Font = new Font("Segoe UI", 7f, FontStyle.Bold), ForeColor = ModernTheme.TextMuted, Location = new Point(12, 252), AutoSize = true, BackColor = Color.Transparent };
        sidePanel.Controls.Add(vehicleLabel);

        cmbVehicle = new ComboBox
        {
            Location = new Point(8, 270),
            Width = 144,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = ModernTheme.SurfaceLight,
            ForeColor = ModernTheme.TextPrimary,
            Font = new Font("Segoe UI", 8.5f),
            FlatStyle = FlatStyle.Standard
        };
        cmbVehicle.Items.AddRange(new object[] { "Plane", "Copter", "Rover" });
        cmbVehicle.SelectedIndex = 0;
        sidePanel.Controls.Add(cmbVehicle);

        // Flight pattern selector
        var patternLabel = new Label { Text = "PATTERN", Font = new Font("Segoe UI", 7f, FontStyle.Bold), ForeColor = ModernTheme.TextMuted, Location = new Point(12, 310), AutoSize = true, BackColor = Color.Transparent };
        sidePanel.Controls.Add(patternLabel);

        cmbPattern = new ComboBox
        {
            Location = new Point(8, 328),
            Width = 144,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = ModernTheme.SurfaceLight,
            ForeColor = ModernTheme.TextPrimary,
            Font = new Font("Segoe UI", 8.5f),
            FlatStyle = FlatStyle.Standard
        };
        cmbPattern.Items.AddRange(new object[] { "Circle", "Point to Point", "Distance" });
        cmbPattern.SelectedIndex = 0;
        sidePanel.Controls.Add(cmbPattern);
        sidePanel.Controls.Add(navMap);
        sidePanel.Controls.Add(navLogs);

        // Divider
        var divider = new Panel { Location = new Point(12, 290), Width = 136, Height = 1, BackColor = ModernTheme.Border };
        sidePanel.Controls.Add(divider);

        // Section: Actions
        var actLabel = new Label { Text = "ACTIONS", Font = new Font("Segoe UI", 7f, FontStyle.Bold), ForeColor = ModernTheme.TextMuted, Location = new Point(12, 300), AutoSize = true, BackColor = Color.Transparent };
        sidePanel.Controls.Add(actLabel);

        btnArm = MakeTextButton("Arm", 320, ModernTheme.Success);
        btnDisarm = MakeTextButton("Disarm", 352, ModernTheme.Warning);
        btnTakeoff = MakeTextButton("Takeoff", 384, ModernTheme.Accent);
        btnRTL = MakeTextButton("RTL", 416, ModernTheme.Info);

        sidePanel.Controls.Add(btnArm);
        sidePanel.Controls.Add(btnDisarm);
        sidePanel.Controls.Add(btnTakeoff);
        sidePanel.Controls.Add(btnRTL);

        btnEmergencyStop = new Panels.ModernButton("EMERGENCY STOP", ModernTheme.Danger)
        {
            Location = new Point(8, 453),
            Width = 144,
            Height = 32
        };
        sidePanel.Controls.Add(btnEmergencyStop);

        // Divider 2
        var divider2 = new Panel { Location = new Point(12, 496), Width = 136, Height = 1, BackColor = ModernTheme.Border };
        sidePanel.Controls.Add(divider2);

        // Section: Connection
        var connLabel = new Label { Text = "CONNECTION", Font = new Font("Segoe UI", 7f, FontStyle.Bold), ForeColor = ModernTheme.TextMuted, Location = new Point(12, 506), AutoSize = true, BackColor = Color.Transparent };
        sidePanel.Controls.Add(connLabel);

        btnConnect = new Panels.ModernButton("Connect", ModernTheme.Accent)
        {
            Location = new Point(8, 526),
            Width = 68,
            Height = 28
        };
        btnDisconnect = new Panels.ModernButton("Disconnect", ModernTheme.TextMuted)
        {
            Location = new Point(82, 526),
            Width = 70,
            Height = 28
        };
        sidePanel.Controls.Add(btnConnect);
        sidePanel.Controls.Add(btnDisconnect);

        // Section: Mode
        lblModeSelector = new Label
        {
            Text = "FLIGHT MODE",
            Font = new Font("Segoe UI", 7f, FontStyle.Bold),
            ForeColor = ModernTheme.TextMuted,
            Location = new Point(12, 566),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        sidePanel.Controls.Add(lblModeSelector);

        cmbMode = new ComboBox
        {
            Location = new Point(2, 658),
            Width = 144,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = ModernTheme.SurfaceLight,
            ForeColor = ModernTheme.TextPrimary,
            Font = new Font("Cascadia Code", 7.5f),
            FlatStyle = FlatStyle.Standard
        };
        cmbMode.Items.AddRange(new object[] {
            "Stab", "Acro", "Alt", "Auto", "Guid",
            "Loit", "RTL", "Circ", "Land", "Sport", "Pos", "Brake"
        });
        sidePanel.Controls.Add(cmbMode);

        // Exit button at bottom (docked)
        btnExit = new Panels.ModernButton("Exit", ModernTheme.TextMuted)
        {
            Dock = DockStyle.Bottom,
            Height = 32,
            Margin = new Padding(8, 4, 8, 4)
        };
        sidePanel.Controls.Add(btnExit);

        // === CONTENT PANEL ===
        contentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ModernTheme.Background,
            Padding = new Padding(0)
        };

        // === BOTTOM STATUS BAR ===
        bottomBar = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 35,
            BackColor = ModernTheme.Surface
        };

        lblStatusMode = MakeStatusLabel("Mode: --", 12);
        lblStatusArmed = MakeStatusLabel("Disarmed", 120);
        lblStatusAlt = MakeStatusLabel("Alt: --", 240);
        lblStatusSpeed = MakeStatusLabel("GS: --", 360);
        lblStatusBatt = MakeStatusLabel("Batt: --", 480);

        bottomBar.Controls.Add(lblStatusMode);
        bottomBar.Controls.Add(lblStatusArmed);
        bottomBar.Controls.Add(lblStatusAlt);
        bottomBar.Controls.Add(lblStatusSpeed);
        bottomBar.Controls.Add(lblStatusBatt);

        // === ASSEMBLE ===
        Controls.Add(contentPanel);
        Controls.Add(sidePanel);
        Controls.Add(bottomBar);
        Controls.Add(titleBar);

        // === MainForm ===
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1280, 780);
        Name = "MainForm";
        Text = "SkyPilot";
        StartPosition = FormStartPosition.CenterScreen;
        Font = ModernTheme.FontRegular;
        MinimumSize = new Size(900, 600);

        ResumeLayout(false);
        PerformLayout();
    }

    private Button MakeWindowButton(Color textColor, Color bgColor, string text, int index)
    {
        var btn = new Button
        {
            Text = text,
            Font = new Font("Segoe UI", 11f),
            ForeColor = textColor,
            BackColor = bgColor,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(46, 40),
            Location = new Point(Width - 46 * (3 - index), 0),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 0;
        return btn;
    }

    private Panels.ModernButton MakeTextButton(string text, int y, Color color)
    {
        var btn = new Panels.ModernButton(text, color)
        {
            Location = new Point(8, y),
            Width = 144,
            Height = 26,
            Font = new Font("Segoe UI", 8.5f)
        };
        return btn;
    }

    private Label MakeStatusLabel(string text, int x)
    {
        return new Label
        {
            Text = text,
            Font = ModernTheme.FontSmall,
            ForeColor = ModernTheme.TextSecondary,
            Location = new Point(x, 6),
            AutoSize = true,
            BackColor = Color.Transparent
        };
    }
}
