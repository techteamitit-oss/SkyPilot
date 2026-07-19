using System.Drawing.Drawing2D;
using SkyPilot.Utils;

namespace SkyPilot.Panels;

/// <summary>
/// Compass widget showing vehicle heading with neon glow effects.
/// </summary>
public class CompassWidget : Control
{
    private float _heading;

    public float Heading
    {
        get => _heading;
        set { _heading = value % 360; if (_heading < 0) _heading += 360; Invalidate(); }
    }

    public CompassWidget()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Size = new Size(120, 120);
        BackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        float size = Math.Min(Width, Height);
        float cx = Width / 2f;
        float cy = Height / 2f;
        float radius = size / 2f - 8;

        // Background circle
        using var bgBrush = new SolidBrush(Color.FromArgb(20, 20, 28));
        using var bgPen = new Pen(ModernTheme.Border, 1);
        g.FillEllipse(bgBrush, cx - radius, cy - radius, radius * 2, radius * 2);
        g.DrawEllipse(bgPen, cx - radius, cy - radius, radius * 2, radius * 2);

        // Inner ring glow
        using var glowPen = new Pen(Color.FromArgb(30, 0, 212, 255), 2);
        g.DrawEllipse(glowPen, cx - radius + 4, cy - radius + 4, (radius - 4) * 2, (radius - 4) * 2);

        // Cardinal directions (fixed, not rotating)
        using var cardFont = new Font("Cascadia Code", 10f, FontStyle.Bold);
        using var cardBrushN = new SolidBrush(ModernTheme.Danger);
        using var cardBrush = new SolidBrush(ModernTheme.TextSecondary);
        float cardDist = radius - 18;

        DrawCardinal(g, cx, cy, cardDist, "N", 0, cardFont, cardBrushN);
        DrawCardinal(g, cx, cy, cardDist, "E", 90, cardFont, cardBrush);
        DrawCardinal(g, cx, cy, cardDist, "S", 180, cardFont, cardBrush);
        DrawCardinal(g, cx, cy, cardDist, "W", 270, cardFont, cardBrush);

        // Tick marks (rotate with heading)
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

        // Heading needle (points up, rotates with heading)
        float needleLen = radius - 25;
        float needleWidth = 6;

        // North needle (red/cyan)
        using var needleBrush = new SolidBrush(ModernTheme.Accent);
        using var needleGlow = new SolidBrush(Color.FromArgb(60, 0, 212, 255));
        float nx = cx;
        float ny = cy - needleLen;
        g.FillEllipse(needleGlow, nx - needleWidth, ny - needleWidth, needleWidth * 2, needleWidth * 2);
        g.FillEllipse(needleBrush, nx - needleWidth / 2, ny - needleWidth / 2, needleWidth, needleWidth);

        // South needle (dim)
        using var southBrush = new SolidBrush(Color.FromArgb(80, 255, 51, 102));
        float sy = cy + needleLen * 0.4f;
        g.FillEllipse(southBrush, cx - needleWidth / 2, sy - needleWidth / 2, needleWidth, needleWidth);

        // Needle line
        using var needlePen = new Pen(ModernTheme.Accent, 2);
        g.DrawLine(needlePen, cx, cy - needleLen + 8, cx, cy + needleLen * 0.4f - 4);

        // Center dot
        using var centerBrush = new SolidBrush(ModernTheme.Accent);
        g.FillEllipse(centerBrush, cx - 4, cy - 4, 8, 8);

        // Heading text at bottom
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
}
