using System.Drawing.Drawing2D;
using SkyPilot.Utils;

namespace SkyPilot.Panels;

/// <summary>
/// Transparent telemetry HUD overlay for the map. Shows altitude, speed, battery, heading.
/// </summary>
public class TelemetryHUD : UserControl
{
    private float _altitude, _speed, _battery, _heading;
    private string _mode = "--";
    private bool _armed;
    private int _sats;

    public TelemetryHUD()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }

    public void UpdateValues(float altitude, float speed, float battery, float heading, string mode, bool armed, int sats)
    {
        _altitude = altitude;
        _speed = speed;
        _battery = battery;
        _heading = heading;
        _mode = mode;
        _armed = armed;
        _sats = sats;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        int w = Width, h = Height;
        if (w < 50 || h < 50) return;

        // Semi-transparent background
        using var bgPath = GetRoundedRect(new Rectangle(0, 0, w, h), 10);
        using var bgBrush = new SolidBrush(Color.FromArgb(120, 13, 17, 23));
        g.FillPath(bgBrush, bgPath);
        using var borderPen = new Pen(Color.FromArgb(80, 0, 212, 255), 1);
        g.DrawPath(borderPen, bgPath);

        using var valueFont = new Font("Cascadia Code", 14f, FontStyle.Bold);
        using var labelFont = new Font("Segoe UI", 8f);
        using var smallFont = new Font("Segoe UI", 7f);
        using var valueBrush = new SolidBrush(ModernTheme.TextPrimary);
        using var labelBrush = new SolidBrush(ModernTheme.TextMuted);
        using var accentBrush = new SolidBrush(ModernTheme.Accent);

        int x = 12, y = 8;
        int rowH = 28;

        // Heading
        string hdg = $"HDG {_heading:F0}\u00B0";
        g.DrawString(hdg, valueFont, accentBrush, x, y);
        y += rowH;

        // Altitude
        Color altColor = _altitude > 200 ? ModernTheme.Warning : ModernTheme.Accent;
        using var altBrush = new SolidBrush(altColor);
        g.DrawString($"ALT", labelFont, labelBrush, x, y);
        g.DrawString($"{_altitude:F0}m", valueFont, altBrush, x + 30, y - 2);
        y += rowH;

        // Speed
        Color spdColor = _speed > 30 ? ModernTheme.Warning : ModernTheme.Success;
        using var spdBrush = new SolidBrush(spdColor);
        g.DrawString($"SPD", labelFont, labelBrush, x, y);
        g.DrawString($"{_speed:F1}", valueFont, spdBrush, x + 30, y - 2);
        g.DrawString("m/s", labelFont, spdBrush, x + 100, y + 2);
        y += rowH;

        // Battery
        Color batColor = _battery > 50 ? ModernTheme.Success :
                         _battery > 20 ? ModernTheme.Warning : ModernTheme.Danger;
        using var batBrush = new SolidBrush(batColor);
        g.DrawString($"BAT", labelFont, labelBrush, x, y);
        g.DrawString($"{_battery:F0}%", valueFont, batBrush, x + 30, y - 2);
        y += rowH;

        // Mode + Armed status
        g.DrawString($"MODE", labelFont, labelBrush, x, y);
        g.DrawString(_mode, valueFont, accentBrush, x + 40, y - 2);
        y += rowH;

        // Armed indicator
        Color armColor = _armed ? ModernTheme.Danger : ModernTheme.Success;
        using var armBrush = new SolidBrush(armColor);
        string armText = _armed ? "ARMED" : "SAFE";
        g.DrawString(armText, labelFont, armBrush, x, y);
        y += 18;

        // Satellites
        Color satColor = _sats >= 10 ? ModernTheme.Success :
                         _sats >= 6 ? ModernTheme.Warning : ModernTheme.Danger;
        using var satBrush = new SolidBrush(satColor);
        g.DrawString($"GPS: {_sats} sats", smallFont, satBrush, x, y);
    }

    private static GraphicsPath GetRoundedRect(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
