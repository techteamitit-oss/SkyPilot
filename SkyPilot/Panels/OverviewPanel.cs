using System.Drawing.Drawing2D;
using SkyPilot.Core.Mavlink;
using SkyPilot.Log;
using SkyPilot.Utils;

namespace SkyPilot.Panels;

/// <summary>
/// Modern overview with circular gauges, stat cards, compass and minimap drawn directly.
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

    // Compass
    private float _heading;
    private readonly CompassWidget _compass;

    // Minimap
    private double _lat, _lon;
    private float _hdg;
    private readonly List<(double Lat, double Lon)> _trail = new();
    private double _mapCenterLat = 51.5074, _mapCenterLon = -0.1278;
    private double _mapZoom = 0.003;

    // Notification
    private string _notification = "";
    private DateTime _notificationTime = DateTime.MinValue;

    private readonly Button _zoomInBtn;
    private readonly Button _zoomOutBtn;

    public OverviewPanel()
    {
        Dock = DockStyle.Fill;
        BackColor = ModernTheme.Background;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

        gaugeBattery = new CircularGauge { Label = "BATTERY", Unit = "%", AccentColor = ModernTheme.Success, Size = new Size(140, 140) };
        gaugeSpeed = new CircularGauge { Label = "SPEED", Unit = "m/s", MaxValue = 50, AccentColor = ModernTheme.Accent, Size = new Size(140, 140) };
        gaugeAltitude = new CircularGauge { Label = "ALTITUDE", Unit = "m", MaxValue = 200, AccentColor = ModernTheme.Info, Size = new Size(140, 140) };
        gaugeSats = new CircularGauge { Label = "SATELLITES", Unit = "sats", MaxValue = 20, AccentColor = ModernTheme.Warning, Size = new Size(140, 140) };

        _zoomInBtn = new Button { Text = "+", Size = new Size(22, 22), BackColor = Color.FromArgb(160, 33, 38, 45), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10f, FontStyle.Bold), Cursor = Cursors.Hand };
        _zoomInBtn.FlatAppearance.BorderSize = 0;
        _zoomInBtn.Click += (s, e) => { _mapZoom = Math.Max(0.0005, _mapZoom * 0.7); Invalidate(); };
        Controls.Add(_zoomInBtn);

        _zoomOutBtn = new Button { Text = "-", Size = new Size(22, 22), BackColor = Color.FromArgb(160, 33, 38, 45), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10f, FontStyle.Bold), Cursor = Cursors.Hand };
        _zoomOutBtn.FlatAppearance.BorderSize = 0;
        _zoomOutBtn.Click += (s, e) => { _mapZoom = Math.Min(0.01, _mapZoom * 1.4); Invalidate(); };
        Controls.Add(_zoomOutBtn);

        Controls.Add(gaugeBattery);
        Controls.Add(gaugeSpeed);
        Controls.Add(gaugeAltitude);
        Controls.Add(gaugeSats);

        _compass = new CompassWidget { Size = new Size(120, 120), BackColor = Color.Transparent };
        Controls.Add(_compass);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutGauges();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        // Check if mouse is over the minimap area
        int mapX = Width - 260, mapY = 155, mapW = 240, mapH = 240;
        if (e.X >= mapX && e.X <= mapX + mapW && e.Y >= mapY && e.Y <= mapY + mapH)
        {
            if (e.Delta > 0)
                _mapZoom = Math.Max(0.0005, _mapZoom * 0.8);
            else
                _mapZoom = Math.Min(0.01, _mapZoom * 1.25);
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
            g.Size = new Size(gaugeSize, gaugeSize);
        gaugeBattery.Location = new Point(startX, gaugeY);
        gaugeSpeed.Location = new Point(startX + gaugeSize + gap, gaugeY);
        gaugeAltitude.Location = new Point(startX + (gaugeSize + gap) * 2, gaugeY);
        gaugeSats.Location = new Point(startX + (gaugeSize + gap) * 3, gaugeY);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        ModernTheme.DrawGridBackground(g, new Rectangle(0, 0, Width, Height));

        using var titleFont = new Font("Segoe UI", 9f, FontStyle.Bold);
        using var titleBrush = new SolidBrush(ModernTheme.TextMuted);
        g.DrawString("FLIGHT OVERVIEW", titleFont, titleBrush, 20, 10);

        // === COMPASS (top right) ===
        _compass.Location = new Point(Width - 140, 15);
        _compass.Size = new Size(120, 120);
        _compass.Heading = _heading;
        _compass.BringToFront();

        // === MINIMAP (right side, below compass) ===
        int mapX = Width - 260, mapY = 155, mapW = 240, mapH = 240;
        DrawMiniMap(g, mapX, mapY, mapW, mapH);

        // Position zoom buttons over minimap
        _zoomInBtn.Location = new Point(mapX + 2, mapY + 2);
        _zoomOutBtn.Location = new Point(mapX + 26, mapY + 2);

        // Notification banner
        if (!string.IsNullOrEmpty(_notification) && (DateTime.UtcNow - _notificationTime).TotalSeconds < 5)
        {
            float alpha = Math.Max(0, 1f - (float)(DateTime.UtcNow - _notificationTime).TotalSeconds / 5f);
            int bannerH = 36;
            int bannerY = 170;
            using var bannerBg = new SolidBrush(Color.FromArgb((int)(180 * alpha), 33, 38, 45));
            g.FillRectangle(bannerBg, 20, bannerY, Width - 280, bannerH);
            using var bannerBorder = new Pen(Color.FromArgb((int)(200 * alpha), 0, 212, 255), 1);
            g.DrawRectangle(bannerBorder, 20, bannerY, Width - 280, bannerH);
            using var bannerFont = new Font("Segoe UI", 11f, FontStyle.Bold);
            using var bannerBrush = new SolidBrush(Color.FromArgb((int)(255 * alpha), 0, 212, 255));
            var sz = g.MeasureString(_notification, bannerFont);
            g.DrawString(_notification, bannerFont, bannerBrush, 20 + (Width - 280 - sz.Width) / 2, bannerY + (bannerH - sz.Height) / 2);
            Invalidate();
        }

        // Bottom stat cards
        if (_state != null)
        {
            int cardY = 190;
            int cardW = Math.Min(200, (Width - 280) / 3);
            int cardH = 80;
            int cardGap = 10;
            int cardsStartX = 20;

            ModernTheme.DrawStatCard(g, new Rectangle(cardsStartX, cardY, cardW, cardH), "MODE", _modeText, ModernTheme.Accent, ModernTheme.Accent);
            ModernTheme.DrawStatCard(g, new Rectangle(cardsStartX + cardW + cardGap, cardY, cardW, cardH), "VIBRATION", _vibText, _vibColor, _vibColor);
            ModernTheme.DrawStatCard(g, new Rectangle(cardsStartX + (cardW + cardGap) * 2, cardY, cardW, cardH), "EKF", _ekfText, _ekfColor, _ekfColor);
            ModernTheme.DrawStatCard(g, new Rectangle(cardsStartX, cardY + cardH + cardGap, Width - 280, cardH), "POSITION", _posText, ModernTheme.TextPrimary, ModernTheme.Accent);
        }
    }

    private void DrawCompass(Graphics g, int cx, int cy)
    {
        int radius = 60;

        // Background
        using var bgBrush = new SolidBrush(Color.FromArgb(20, 20, 28));
        using var bgPen = new Pen(ModernTheme.Border, 1);
        g.FillEllipse(bgBrush, cx - radius, cy - radius, radius * 2, radius * 2);
        g.DrawEllipse(bgPen, cx - radius, cy - radius, radius * 2, radius * 2);

        // Inner glow
        using var glowPen = new Pen(Color.FromArgb(30, 0, 212, 255), 2);
        g.DrawEllipse(glowPen, cx - radius + 4, cy - radius + 4, (radius - 4) * 2, (radius - 4) * 2);

        // Cardinal directions
        using var cardFont = new Font("Cascadia Code", 10f, FontStyle.Bold);
        using var cardBrushN = new SolidBrush(ModernTheme.Danger);
        using var cardBrush = new SolidBrush(ModernTheme.TextSecondary);
        float cardDist = radius - 18;
        DrawCardinal(g, cx, cy, cardDist, "N", 0, cardFont, cardBrushN);
        DrawCardinal(g, cx, cy, cardDist, "E", 90, cardFont, cardBrush);
        DrawCardinal(g, cx, cy, cardDist, "S", 180, cardFont, cardBrush);
        DrawCardinal(g, cx, cy, cardDist, "W", 270, cardFont, cardBrush);

        // Tick marks
        using var tickPen = new Pen(ModernTheme.TextMuted, 1);
        for (int deg = 0; deg < 360; deg += 10)
        {
            float angle = (deg - _heading) * (float)Math.PI / 180f;
            int tickLen = deg % 30 == 0 ? 10 : 5;
            float outerR = radius - 8;
            float innerR = outerR - tickLen;
            float x1 = cx + outerR * (float)Math.Sin(angle);
            float y1 = cy - outerR * (float)Math.Cos(angle);
            float x2 = cx + innerR * (float)Math.Sin(angle);
            float y2 = cy - innerR * (float)Math.Cos(angle);
            g.DrawLine(tickPen, x1, y1, x2, y2);
        }

        // Needle
        float needleLen = radius - 25;
        using var needleBrush = new SolidBrush(ModernTheme.Accent);
        using var needleGlow = new SolidBrush(Color.FromArgb(60, 0, 212, 255));
        float ny = cy - needleLen;
        g.FillEllipse(needleGlow, cx - 6, ny - 6, 12, 12);
        g.FillEllipse(needleBrush, cx - 3, ny - 3, 6, 6);
        using var southBrush = new SolidBrush(Color.FromArgb(80, 255, 51, 102));
        float sy = cy + needleLen * 0.4f;
        g.FillEllipse(southBrush, cx - 3, sy - 3, 6, 6);
        using var needlePen = new Pen(ModernTheme.Accent, 2);
        g.DrawLine(needlePen, cx, cy - needleLen + 8, cx, cy + needleLen * 0.4f - 4);

        // Center dot
        using var centerBrush = new SolidBrush(ModernTheme.Accent);
        g.FillEllipse(centerBrush, cx - 4, cy - 4, 8, 8);

        // Heading text
        using var hdgFont = new Font("Cascadia Code", 11f, FontStyle.Bold);
        string hdgText = $"{(int)_heading:000}";
        var hdgSize = g.MeasureString(hdgText, hdgFont);
        using var hdgBrush = new SolidBrush(ModernTheme.Accent);
        g.DrawString(hdgText, hdgFont, hdgBrush, cx - hdgSize.Width / 2, cy + radius * 0.45f);

        // Label
        using var labelFont = new Font("Segoe UI", 7f);
        using var labelBrush = new SolidBrush(ModernTheme.TextMuted);
        g.DrawString("HDG", labelFont, labelBrush, cx - 12, cy + radius * 0.65f);
    }

    private static void DrawCardinal(Graphics g, float cx, float cy, float dist, string text, float deg, Font font, Brush brush)
    {
        float angle = deg * (float)Math.PI / 180f;
        float x = cx + dist * (float)Math.Sin(angle);
        float y = cy - dist * (float)Math.Cos(angle);
        var size = g.MeasureString(text, font);
        g.DrawString(text, font, brush, x - size.Width / 2, y - size.Height / 2);
    }

    private void DrawMiniMap(Graphics g, int x, int y, int w, int h)
    {
        // Background
        using var bgBrush = new SolidBrush(Color.FromArgb(13, 17, 23));
        g.FillRectangle(bgBrush, x, y, w, h);
        using var borderPen = new Pen(Color.FromArgb(60, 0, 212, 255), 1);
        g.DrawRectangle(borderPen, x, y, w, h);

        // Grid
        using var gridPen = new Pen(Color.FromArgb(20, 0, 212, 255), 1);
        for (int i = 1; i < 4; i++)
        {
            g.DrawLine(gridPen, x + w * i / 4, y, x + w * i / 4, y + h);
            g.DrawLine(gridPen, x, y + h * i / 4, x + w, y + h * i / 4);
        }

        // Label
        using var lblFont = new Font("Segoe UI", 7f);
        using var lblBrush = new SolidBrush(ModernTheme.TextMuted);
        g.DrawString("MAP", lblFont, lblBrush, x + 4, y + 2);

        if (_trail.Count < 2) return;

        double pxPerDeg = w / (2.0 * _mapZoom);
        int cx = x + w / 2, cy = y + h / 2;

        // Trail
        using var trailPen = new Pen(Color.FromArgb(100, 0, 212, 255), 2);
        for (int i = 1; i < _trail.Count; i++)
        {
            int x1 = cx + (int)((_trail[i - 1].Lon - _mapCenterLon) * pxPerDeg);
            int y1 = cy - (int)((_trail[i - 1].Lat - _mapCenterLat) * pxPerDeg);
            int x2 = cx + (int)((_trail[i].Lon - _mapCenterLon) * pxPerDeg);
            int y2 = cy - (int)((_trail[i].Lat - _mapCenterLat) * pxPerDeg);
            int alpha = 30 + (int)(200.0 * i / _trail.Count);
            trailPen.Color = Color.FromArgb(alpha, 0, 212, 255);
            g.DrawLine(trailPen, x1, y1, x2, y2);
        }

        // Vehicle marker
        int vx = cx + (int)((_lon - _mapCenterLon) * pxPerDeg);
        int vy = cy - (int)((_lat - _mapCenterLat) * pxPerDeg);
        using var glowBrush = new SolidBrush(Color.FromArgb(60, 0, 212, 255));
        g.FillEllipse(glowBrush, vx - 12, vy - 12, 24, 24);
        using var markerBrush = new SolidBrush(Color.FromArgb(220, 0, 212, 255));
        g.FillEllipse(markerBrush, vx - 5, vy - 5, 10, 10);

        // Heading arrow
        double rad = _hdg * Math.PI / 180.0;
        int ax = vx + (int)(Math.Sin(rad) * 15);
        int ay = vy - (int)(Math.Cos(rad) * 15);
        using var arrowPen = new Pen(Color.FromArgb(200, 0, 212, 255), 2);
        g.DrawLine(arrowPen, vx, vy, ax, ay);
    }

    public void UpdateFromState(VehicleState state)
    {
        _state = state;

        gaugeBattery.Value = state.BatteryRemaining;
        gaugeBattery.AccentColor = state.BatteryRemaining > 50 ? ModernTheme.Success :
                                   state.BatteryRemaining > 20 ? ModernTheme.Warning : ModernTheme.Danger;
        gaugeSpeed.Value = state.GroundSpeed;
        gaugeAltitude.Value = state.AltitudeRel;
        gaugeSats.Value = state.SatelliteCount;
        gaugeSats.AccentColor = state.GpsFix >= 3 ? ModernTheme.Success :
                                state.GpsFix >= 1 ? ModernTheme.Warning : ModernTheme.Danger;

        _modeText = state.FlightModeName;
        float vibMax = state.MaxVibration;
        _vibText = $"{vibMax:F0}";
        _vibColor = vibMax < 30 ? ModernTheme.Success : vibMax < 60 ? ModernTheme.Warning : ModernTheme.Danger;
        float ekfMax = Math.Max(state.EkfVelVariance, Math.Max(state.EkfPosHorizVariance,
            Math.Max(state.EkfPosVertVariance, Math.Max(state.EkfCompassVariance, state.EkfTerrainAltVariance))));
        _ekfText = $"{ekfMax:F2}";
        _ekfColor = ekfMax < 0.5f ? ModernTheme.Success : ekfMax < 0.8f ? ModernTheme.Warning : ModernTheme.Danger;
        _posText = $"{state.Latitude:F6}  {state.Longitude:F6}";

        _heading = state.Yaw;

        if (state.Latitude != 0 && state.Longitude != 0)
        {
            _lat = state.Latitude;
            _lon = state.Longitude;
            _hdg = state.Yaw;
            _trail.Add((state.Latitude, state.Longitude));
            if (_trail.Count > 200) _trail.RemoveAt(0);
            if (_trail.Count == 1) { _mapCenterLat = state.Latitude; _mapCenterLon = state.Longitude; }
        }

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
