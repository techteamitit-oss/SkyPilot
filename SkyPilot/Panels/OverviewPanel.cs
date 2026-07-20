using System.Drawing.Drawing2D;
using SkyPilot.Core.Mavlink;
using SkyPilot.Log;
using SkyPilot.Utils;

namespace SkyPilot.Panels;

/// <summary>
/// Modern overview with circular gauges and stat cards.
/// Compass and minimap are draggable — hold and drag to reposition.
/// </summary>
public class OverviewPanel : UserControl
{
    private VehicleState? _state;

    // Gauges
    private readonly CircularGauge gaugeBattery;
    private readonly CircularGauge gaugeSpeed;
    private readonly CircularGauge gaugeAltitude;
    private readonly CircularGauge gaugeSats;

    // Stat labels
    private string _modeText = "--";
    private string _vibText = "--";
    private Color _vibColor = ModernTheme.TextMuted;
    private string _ekfText = "--";
    private Color _ekfColor = ModernTheme.TextMuted;
    private string _posText = "--";
    private readonly CompassWidget _compass;
    private readonly MiniMapControl _miniMap;

    // Drag state
    private Control? _dragTarget;
    private Point _dragOffset;
    private bool _compassPlaced;
    private bool _miniMapPlaced;

    // Notification
    private string _notification = "";
    private DateTime _notificationTime = DateTime.MinValue;

    public OverviewPanel()
    {
        Dock = DockStyle.Fill;
        BackColor = ModernTheme.Background;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

        gaugeBattery = new CircularGauge
        {
            Label = "BATTERY",
            Unit = "%",
            AccentColor = ModernTheme.Success,
            Size = new Size(140, 140)
        };

        gaugeSpeed = new CircularGauge
        {
            Label = "SPEED",
            Unit = "m/s",
            MaxValue = 50,
            AccentColor = ModernTheme.Accent,
            Size = new Size(140, 140)
        };

        gaugeAltitude = new CircularGauge
        {
            Label = "ALTITUDE",
            Unit = "m",
            MaxValue = 200,
            AccentColor = ModernTheme.Info,
            Size = new Size(140, 140)
        };

        gaugeSats = new CircularGauge
        {
            Label = "SATELLITES",
            Unit = "sats",
            MaxValue = 20,
            AccentColor = ModernTheme.Warning,
            Size = new Size(140, 140)
        };

        Controls.Add(gaugeBattery);
        Controls.Add(gaugeSpeed);
        Controls.Add(gaugeAltitude);
        Controls.Add(gaugeSats);

        _compass = new CompassWidget
        {
            Size = new Size(120, 120),
            BackColor = ModernTheme.Background
        };
        _compass.MouseDown += OnDragStart;
        _compass.MouseMove += OnDragMove;
        _compass.MouseUp += OnDragEnd;
        Controls.Add(_compass);

        _miniMap = new MiniMapControl
        {
            Size = new Size(240, 240)
        };
        _miniMap.MouseDown += OnDragStart;
        _miniMap.MouseMove += OnDragMove;
        _miniMap.MouseUp += OnDragEnd;
        Controls.Add(_miniMap);
    }

    private void OnDragStart(object? sender, MouseEventArgs e)
    {
        if (sender is Control ctrl && e.Button == MouseButtons.Left)
        {
            _dragTarget = ctrl;
            _dragOffset = e.Location;
            ctrl.BringToFront();
        }
    }

    private void OnDragMove(object? sender, MouseEventArgs e)
    {
        if (_dragTarget != null && e.Button == MouseButtons.Left)
        {
            int nx = _dragTarget.Left + e.X - _dragOffset.X;
            int ny = _dragTarget.Top + e.Y - _dragOffset.Y;
            _dragTarget.Location = new Point(
                Math.Max(0, Math.Min(Width - _dragTarget.Width, nx)),
                Math.Max(0, Math.Min(Height - _dragTarget.Height, ny)));
        }
    }

    private void OnDragEnd(object? sender, MouseEventArgs e)
    {
        if (_dragTarget != null)
        {
            if (_dragTarget == _compass) _compassPlaced = true;
            if (_dragTarget == _miniMap) _miniMapPlaced = true;
            _dragTarget = null;
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        // Ensure compass and minimap are always child controls
        if (!Controls.Contains(_compass)) Controls.Add(_compass);
        if (!Controls.Contains(_miniMap)) Controls.Add(_miniMap);
        LayoutGauges();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible)
        {
            if (!Controls.Contains(_compass)) Controls.Add(_compass);
            if (!Controls.Contains(_miniMap)) Controls.Add(_miniMap);
            _compass.BringToFront();
            _miniMap.BringToFront();
            LayoutGauges();
            Invalidate();
        }
    }

    private void LayoutGauges()
    {
        if (Width < 100) return;

        int gaugeSize = Math.Min(140, (Width - 60) / 4);
        int gap = 20;
        int totalW = gaugeSize * 4 + gap * 3;
        int startX = (Width - totalW) / 2;
        int gaugeY = 30;

        foreach (var g in new[] { gaugeBattery, gaugeSpeed, gaugeAltitude, gaugeSats })
        {
            g.Size = new Size(gaugeSize, gaugeSize);
        }

        gaugeBattery.Location = new Point(startX, gaugeY);
        gaugeSpeed.Location = new Point(startX + gaugeSize + gap, gaugeY);
        gaugeAltitude.Location = new Point(startX + (gaugeSize + gap) * 2, gaugeY);
        gaugeSats.Location = new Point(startX + (gaugeSize + gap) * 3, gaugeY);

        // Default positions for compass and minimap (only if not yet dragged)
        if (!_compassPlaced)
            _compass.Location = new Point(Width - 140, 20);
        if (!_miniMapPlaced)
            _miniMap.Location = new Point(Width - 200, 150);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        // Grid background
        ModernTheme.DrawGridBackground(g, new Rectangle(0, 0, Width, Height));

        // Section title
        using var titleFont = new Font("Segoe UI", 9f, FontStyle.Bold);
        using var titleBrush = new SolidBrush(ModernTheme.TextMuted);
        g.DrawString("FLIGHT OVERVIEW", titleFont, titleBrush, 20, 10);

        // Compass heading
        if (_state != null)
        {
            _compass.Heading = _state.Yaw;
        }

        // Notification banner (fades after 5 seconds)
        if (!string.IsNullOrEmpty(_notification) && (DateTime.UtcNow - _notificationTime).TotalSeconds < 5)
        {
            float alpha = Math.Max(0, 1f - (float)(DateTime.UtcNow - _notificationTime).TotalSeconds / 5f);
            int bannerH = 36;
            int bannerY = 170;
            using var bannerBg = new SolidBrush(Color.FromArgb((int)(180 * alpha), 33, 38, 45));
            g.FillRectangle(bannerBg, 20, bannerY, Width - 40, bannerH);
            using var bannerBorder = new Pen(Color.FromArgb((int)(200 * alpha), 0, 212, 255), 1);
            g.DrawRectangle(bannerBorder, 20, bannerY, Width - 40, bannerH);
            using var bannerFont = new Font("Segoe UI", 11f, FontStyle.Bold);
            using var bannerBrush = new SolidBrush(Color.FromArgb((int)(255 * alpha), 0, 212, 255));
            var sz = g.MeasureString(_notification, bannerFont);
            g.DrawString(_notification, bannerFont, bannerBrush, (Width - sz.Width) / 2, bannerY + (bannerH - sz.Height) / 2);
            Invalidate(); // keep redrawing for fade
        }

        // Bottom stat cards
        if (_state != null)
        {
            int cardY = 190;
            int cardW = (Width - 60) / 3;
            int cardH = 80;
            int cardGap = 10;

            ModernTheme.DrawStatCard(g, new Rectangle(20, cardY, cardW, cardH),
                "MODE", _modeText, ModernTheme.Accent, ModernTheme.Accent);
            ModernTheme.DrawStatCard(g, new Rectangle(20 + cardW + cardGap, cardY, cardW, cardH),
                "VIBRATION", _vibText, _vibColor, _vibColor);
            ModernTheme.DrawStatCard(g, new Rectangle(20 + (cardW + cardGap) * 2, cardY, cardW, cardH),
                "EKF", _ekfText, _ekfColor, _ekfColor);

            // Position card (full width)
            ModernTheme.DrawStatCard(g, new Rectangle(20, cardY + cardH + cardGap, Width - 40, cardH),
                "POSITION", _posText, ModernTheme.TextPrimary, ModernTheme.Accent);
        }
    }

    public void UpdateFromState(VehicleState state)
    {
        _state = state;

        // Update gauges
        gaugeBattery.Value = state.BatteryRemaining;
        gaugeBattery.AccentColor = state.BatteryRemaining > 50 ? ModernTheme.Success :
                                   state.BatteryRemaining > 20 ? ModernTheme.Warning : ModernTheme.Danger;

        gaugeSpeed.Value = state.GroundSpeed;

        gaugeAltitude.Value = state.AltitudeRel;

        gaugeSats.Value = state.SatelliteCount;
        gaugeSats.AccentColor = state.GpsFix >= 3 ? ModernTheme.Success :
                                state.GpsFix >= 1 ? ModernTheme.Warning : ModernTheme.Danger;

        // Update stat cards
        _modeText = state.FlightModeName;

        float vibMax = state.MaxVibration;
        _vibText = $"{vibMax:F0}";
        _vibColor = vibMax < 30 ? ModernTheme.Success :
                    vibMax < 60 ? ModernTheme.Warning : ModernTheme.Danger;

        float ekfMax = Math.Max(state.EkfVelVariance, Math.Max(state.EkfPosHorizVariance,
            Math.Max(state.EkfPosVertVariance, Math.Max(state.EkfCompassVariance, state.EkfTerrainAltVariance))));
        _ekfText = $"{ekfMax:F2}";
        _ekfColor = ekfMax < 0.5f ? ModernTheme.Success :
                    ekfMax < 0.8f ? ModernTheme.Warning : ModernTheme.Danger;

        _posText = $"{state.Latitude:F6}  {state.Longitude:F6}";

        // Update mini map
        if (state.Latitude != 0 && state.Longitude != 0)
            _miniMap.UpdatePosition(state.Latitude, state.Longitude, state.Yaw);

        Invalidate();
    }

    public void ShowNotification(string text)
    {
        _notification = text;
        _notificationTime = DateTime.UtcNow;
        Invalidate();
    }

    public void UpdateFromLogMessage(LogMessage msg)
    {
        if (msg.Fields.TryGetValue("VibeX", out var vx))
            _vibText = $"X:{vx:F0} Y:{msg.GetFloat("VibeY"):F0} Z:{msg.GetFloat("VibeZ"):F0}";
    }
}
