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

    // Action buttons
    private Panels.ModernButton btnArm;
    private Panels.ModernButton btnDisarm;
    private Panels.ModernButton btnTakeoff;
    private Panels.ModernButton btnRTL;
    private Panels.ModernButton btnEmergencyStop;
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
            Width = 56,
            BackColor = ModernTheme.Surface
        };

        // Nav buttons (icon-only, stacked vertically)
        int navY = 8;
        navOverview = MakeIconButton(Utils.IconFont.Overview, "Overview", navY, ModernTheme.Accent);
        navSensors = MakeIconButton(Utils.IconFont.Sensors, "Sensors", navY + 44, ModernTheme.TextMuted);
        navMission = MakeIconButton(Utils.IconFont.Mission, "Mission", navY + 88, ModernTheme.TextMuted);
        navMessages = MakeIconButton(Utils.IconFont.Messages, "Messages", navY + 132, ModernTheme.TextMuted);
        navParams = MakeIconButton(Utils.IconFont.Params, "Parameters", navY + 176, ModernTheme.TextMuted);
        navLogs = MakeIconButton(Utils.IconFont.Logs, "Logs", navY + 220, ModernTheme.TextMuted);
        navMap = MakeIconButton(Utils.IconFont.Mission, "Map", navY + 264, ModernTheme.TextMuted);

        sidePanel.Controls.Add(navOverview);
        sidePanel.Controls.Add(navSensors);
        sidePanel.Controls.Add(navMission);
        sidePanel.Controls.Add(navMessages);
        sidePanel.Controls.Add(navParams);
        sidePanel.Controls.Add(navLogs);
        sidePanel.Controls.Add(navMap);

        // Divider
        var divider = new Panel { Location = new Point(12, navY + 270), Width = 32, Height = 1, BackColor = ModernTheme.Border };
        sidePanel.Controls.Add(divider);

        // Action buttons (smaller, icon-style)
        int actY = navY + 285;
        btnArm = MakeSmallButton("▶", "Arm", actY, ModernTheme.Success);
        btnDisarm = MakeSmallButton("■", "Stop", actY + 38, ModernTheme.Warning);
        btnTakeoff = MakeSmallButton("▲", "Up", actY + 76, ModernTheme.Accent);
        btnRTL = MakeSmallButton("↺", "RTL", actY + 114, ModernTheme.Info);

        btnEmergencyStop = new Panels.ModernButton("⚠ STOP", ModernTheme.Danger)
        {
            Location = new Point(2, actY + 158),
            Width = 52,
            Height = 30
        };

        sidePanel.Controls.Add(btnArm);
        sidePanel.Controls.Add(btnDisarm);
        sidePanel.Controls.Add(btnTakeoff);
        sidePanel.Controls.Add(btnRTL);
        sidePanel.Controls.Add(btnEmergencyStop);

        // Connect/Disconnect at bottom
        btnConnect = new Panels.ModernButton("⚡", ModernTheme.Accent)
        {
            Location = new Point(2, 560),
            Width = 52,
            Height = 30
        };
        btnDisconnect = new Panels.ModernButton("⊘", ModernTheme.TextMuted)
        {
            Location = new Point(2, 596),
            Width = 52,
            Height = 30
        };
        sidePanel.Controls.Add(btnConnect);
        sidePanel.Controls.Add(btnDisconnect);

        // Mode selector
        lblModeSelector = new Label
        {
            Text = "MODE",
            Font = new Font("Segoe UI", 7f, FontStyle.Bold),
            ForeColor = ModernTheme.TextMuted,
            Location = new Point(6, 640),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        sidePanel.Controls.Add(lblModeSelector);

        cmbMode = new ComboBox
        {
            Location = new Point(2, 658),
            Width = 52,
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
            Height = 28,
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

    private Panels.ModernButton MakeIconButton(string icon, string tooltip, int y, Color color)
    {
        var btn = new Panels.ModernButton(icon, color)
        {
            Location = new Point(2, y),
            Width = 52,
            Height = 38,
            Font = Utils.IconFont.IconFontMedium
        };
        btn.MouseHover += (s, e) => btn.Text = tooltip;
        btn.MouseLeave += (s, e) => btn.Text = icon;
        return btn;
    }

    private Panels.ModernButton MakeSmallButton(string icon, string label, int y, Color color)
    {
        var btn = new Panels.ModernButton(icon, color)
        {
            Location = new Point(2, y),
            Width = 52,
            Height = 30,
            Font = Utils.IconFont.IconFontSmall
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
