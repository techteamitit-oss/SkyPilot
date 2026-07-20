using System.Drawing.Drawing2D;
using SkyPilot.Core.Mavlink;
using SkyPilot.Log;
using SkyPilot.Utils;

namespace SkyPilot.Panels;

/// <summary>
/// Modern overview with gauges, stat cards, compass and minimap drawn in OnPaint.
/// Compass and minimap are draggable by clicking and dragging on them.
/// </summary>
public class OverviewPanel : UserControl
{
    private VehicleState? _state;

    // Gauges (child controls)
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

    // Minimap
    private double _lat, _lon, _hdg;
    private readonly List<(double Lat, double Lon)> _trail = new();
    private double _mapCenterLat = 51.5074, _mapCenterLon = -0.1278;
    private double _mapZoom = 0.003;

    // Drag state for compass and minimap
    private int _compassX, _compassY;
    private int _minimapX, _minimapY;
    private const int COMPASS_SIZE = 120;
    private const int MINIMAP_SIZE = 240;
    private bool _draggingCompass;
    private bool _draggingMinimap;
    private Point _dragOffset;

    // Notification
    private string _notification = "";
    private DateTime _notificationTime = DateTime.MinValue;

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

        Controls.Add(gaugeBattery);
        Controls.Add(gaugeSpeed);
        Controls.Add(gaugeAltitude);
        Controls.Add(gaugeSats);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutGauges();
        if (Width > 100 && _compassX == 0 && _compassY == 0)
        {
            _compassX = Width - COMPASS_SIZE - 20;
            _compassY = 20;
            _minimapX = Width - MINIMAP_SIZE - 20;
            _minimapY = 300;
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

    // --- Mouse handling for dragging compass and minimap ---

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;

        // Check minimap hit
        if (e.X >= _minimapX && e.X <= _minimapX + MINIMAP_SIZE &&
            e.Y >= _minimapY && e.Y <= _minimapY + MINIMAP_SIZE)
        {
            _draggingMinimap = true;
            _dragOffset = new Point(e.X - _minimapX, e.Y - _minimapY);
            return;
        }

        // Check compass hit
        int ccx = _compassX + COMPASS_SIZE / 2, ccy = _compassY + COMPASS_SIZE / 2;
        double dist = Math.Sqrt(Math.Pow(e.X - ccx, 2) + Math.Pow(e.Y - ccy, 2));
        if (dist <= COMPASS_SIZE / 2)
        {
            _draggingCompass = true;
            _dragOffset = new Point(e.X - _compassX, e.Y - _compassY);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (e.Button != MouseButtons.Left) return;

        if (_draggingMinimap)
        {
            _minimapX = Math.Max(0, Math.Min(Width - MINIMAP_SIZE, e.X - _dragOffset.X));
            _minimapY = Math.Max(0, Math.Min(Height - MINIMAP_SIZE, e.Y - _dragOffset.Y));
            Invalidate();
        }
        else if (_draggingCompass)
        {
            _compassX = Math.Max(0, Math.Min(Width - COMPASS_SIZE, e.X - _dragOffset.X));
            _compassY = Math.Max(0, Math.Min(Height - COMPASS_SIZE, e.Y - _dragOffset.Y));
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _draggingCompass = false;
        _draggingMinimap = false;
    }

    // --- Painting ---

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

        // === COMPASS (drawn in OnPaint) ===
        DrawCompass(g, _compassX + COMPASS_SIZE / 2, _compassY + COMPASS_SIZE / 2);

        // === MINIMAP (drawn in OnPaint) ===
        DrawMiniMap(g, _minimapX, _minimapY, MINIMAP_SIZE, MINIMAP_SIZE);

        // Notification banner
        if (!string.IsNullOrEmpty(_notification) && (DateTime.UtcNow - _notificationTime).TotalSeconds < 5)
        {
            float alpha = Math.Max(0, 1f - (float)(DateTime.UtcNow - _notificationTime).TotalSeconds / 5f);
            int bannerH = 36, bannerY = 170;
            using var bannerBg = new SolidBrush(Color.FromArgb((int)(180 * alpha), 33, 38, 45));
            g.FillRectangle(bannerBg, 20, bannerY, Width - 40, bannerH);
            using var bannerBorder = new Pen(Color.FromArgb((int)(200 * alpha), 0, 212, 255), 1);
            g.DrawRectangle(bannerBorder, 20, bannerY, Width - 40, bannerH);
            using var bannerFont = new Font("Segoe UI", 11f, FontStyle.Bold);
            using var bannerBrush = new SolidBrush(Color.FromArgb((int)(255 * alpha), 0, 212, 255));
            var sz = g.MeasureString(_notification, bannerFont);
            g.DrawString(_notification, bannerFont, bannerBrush, (Width - sz.Width) / 2, bannerY + (bannerH - sz.Height) / 2);
            Invalidate();
        }

        // Stat cards
        if (_state != null)
        {
            int cardY = 190, cardW = Math.Min(200, (Width - 60) / 3), cardH = 80, cardGap = 10;
            ModernTheme.DrawStatCard(g, new Rectangle(20, cardY, cardW, cardH), "MODE", _modeText, ModernTheme.Accent, ModernTheme.Accent);
            ModernTheme.DrawStatCard(g, new Rectangle(20 + cardW + cardGap, cardY, cardW, cardH), "VIBRATION", _vibText, _vibColor, _vibColor);
            ModernTheme.DrawStatCard(g, new Rectangle(20 + (cardW + cardGap) * 2, cardY, cardW, cardH), "EKF", _ekfText, _ekfColor, _ekfColor);
            ModernTheme.DrawStatCard(g, new Rectangle(20, cardY + cardH + cardGap, Width - 40, cardH), "POSITION", _posText, ModernTheme.TextPrimary, ModernTheme.Accent);
        }
    }

    private void DrawCompass(Graphics g, int cx, int cy)
    {
        int radius = COMPASS_SIZE / 2 - 4;

        using var bgBrush = new SolidBrush(Color.FromArgb(20, 20, 28));
        using var bgPen = new Pen(ModernTheme.Border, 1);
        g.FillEllipse(bgBrush, cx - radius, cy - radius, radius * 2, radius * 2);
        g.DrawEllipse(bgPen, cx - radius, cy - radius, radius * 2, radius * 2);

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
            float outerR = radius - 8, innerR = outerR - tickLen;
            float x1 = cx + outerR * (float)Math.Sin(angle), y1 = cy - outerR * (float)Math.Cos(angle);
            float x2 = cx + innerR * (float)Math.Sin(angle), y2 = cy - innerR * (float)Math.Cos(angle);
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
        using var centerBrush = new SolidBrush(ModernTheme.Accent);
        g.FillEllipse(centerBrush, cx - 4, cy - 4, 8, 8);

        // Heading text
        using var hdgFont = new Font("Cascadia Code", 11f, FontStyle.Bold);
        string hdgText = $"{(int)_heading:000}";
        var hdgSize = g.MeasureString(hdgText, hdgFont);
        using var hdgBrush = new SolidBrush(ModernTheme.Accent);
        g.DrawString(hdgText, hdgFont, hdgBrush, cx - hdgSize.Width / 2, cy + radius * 0.45f);

        using var labelFont = new Font("Segoe UI", 7f);
        using var labelBrush = new SolidBrush(ModernTheme.TextMuted);
        g.DrawString("COMPASS", labelFont, labelBrush, cx - 20, cy + radius * 0.65f);
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

        // Zoom buttons
        using var btnBg = new SolidBrush(Color.FromArgb(160, 33, 38, 45));
        using var btnFont = new Font("Segoe UI", 10f, FontStyle.Bold);
        using var btnBrush = new SolidBrush(Color.White);
        g.FillRectangle(btnBg, x + 2, y + 2, 22, 22);
        g.DrawString("+", btnFont, btnBrush, x + 7, y + 2);
        g.FillRectangle(btnBg, x + 26, y + 2, 22, 22);
        g.DrawString("-", btnFont, btnBrush, x + 31, y + 2);
        using var rstBrush = new SolidBrush(ModernTheme.Accent);
        g.FillRectangle(btnBg, x + 50, y + 2, 22, 22);
        using var rstFont = new Font("Segoe UI", 8f, FontStyle.Bold);
        g.DrawString("R", rstFont, rstBrush, x + 56, y + 4);

        using var lblFont = new Font("Segoe UI", 7f);
        using var lblBrush = new SolidBrush(ModernTheme.TextMuted);
        g.DrawString("MAP", lblFont, lblBrush, x + 4, y + 26);

        if (_trail.Count < 2)
        {
            using var noDataFont = new Font("Segoe UI", 8f);
            using var noDataBrush = new SolidBrush(ModernTheme.TextMuted);
            g.DrawString("No position data", noDataFont, noDataBrush, x + w / 2 - 50, y + h / 2 - 6);
            return;
        }

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

    // --- Zoom via mouse wheel ---

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (e.X >= _minimapX && e.X <= _minimapX + MINIMAP_SIZE &&
            e.Y >= _minimapY && e.Y <= _minimapY + MINIMAP_SIZE)
        {
            if (e.Delta > 0) _mapZoom = Math.Max(0.0005, _mapZoom * 0.8);
            else _mapZoom = Math.Min(0.01, _mapZoom * 1.25);
            Invalidate();
        }
    }

    // --- Zoom via click ---

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (_draggingCompass || _draggingMinimap) return;

        // Check zoom button hits on minimap
        if (e.X >= _minimapX + 2 && e.X <= _minimapX + 24 && e.Y >= _minimapY + 2 && e.Y <= _minimapY + 24)
        { _mapZoom = Math.Max(0.0005, _mapZoom * 0.7); Invalidate(); return; }
        if (e.X >= _minimapX + 26 && e.X <= _minimapX + 48 && e.Y >= _minimapY + 2 && e.Y <= _minimapY + 24)
        { _mapZoom = Math.Min(0.01, _mapZoom * 1.4); Invalidate(); return; }
        if (e.X >= _minimapX + 50 && e.X <= _minimapX + 72 && e.Y >= _minimapY + 2 && e.Y <= _minimapY + 24)
        { _mapZoom = 0.003; Invalidate(); return; }
    }

    // --- Public API ---

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
