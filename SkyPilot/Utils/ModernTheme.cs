using System.Drawing.Drawing2D;

namespace SkyPilot.Utils;

/// <summary>
/// Neon glass cockpit theme - deep navy + cyan accents.
/// </summary>
public static class ModernTheme
{
    // Core palette
    public static readonly Color Background = Color.FromArgb(13, 17, 23);     // #0D1117
    public static readonly Color Surface = Color.FromArgb(22, 27, 34);       // #161B22
    public static readonly Color SurfaceLight = Color.FromArgb(33, 38, 45);  // #21262D
    public static readonly Color Border = Color.FromArgb(48, 54, 61);        // #30363D
    public static readonly Color BorderLight = Color.FromArgb(68, 76, 86);   // #484F58

    // Text
    public static readonly Color TextPrimary = Color.FromArgb(230, 237, 243); // #E6EDF3
    public static readonly Color TextSecondary = Color.FromArgb(139, 148, 158); // #8B949E
    public static readonly Color TextMuted = Color.FromArgb(110, 118, 129);  // #6E7681

    // Accents - neon
    public static readonly Color Accent = Color.FromArgb(0, 212, 255);       // Cyan #00D4FF
    public static readonly Color AccentDim = Color.FromArgb(0, 120, 150);    // Dim cyan
    public static readonly Color Success = Color.FromArgb(0, 255, 136);      // Neon green #00FF88
    public static readonly Color Warning = Color.FromArgb(255, 184, 0);      // Amber #FFB800
    public static readonly Color Danger = Color.FromArgb(255, 51, 102);      // Hot pink #FF3366
    public static readonly Color Info = Color.FromArgb(130, 200, 255);       // Light cyan

    // Status
    public static readonly Color Armed = Color.FromArgb(255, 51, 102);
    public static readonly Color Disarmed = Color.FromArgb(0, 255, 136);

    // Glow helpers
    public static readonly Color GlowCyan = Color.FromArgb(40, 0, 212, 255);
    public static readonly Color GlowGreen = Color.FromArgb(40, 0, 255, 136);
    public static readonly Color GlowPink = Color.FromArgb(40, 255, 51, 102);

    // Fonts
    public static readonly Font FontRegular = new("Segoe UI", 9.5f);
    public static readonly Font FontBold = new("Segoe UI", 9.5f, FontStyle.Bold);
    public static readonly Font FontSmall = new("Segoe UI", 8.5f);
    public static readonly Font FontTitle = new("Segoe UI", 14f, FontStyle.Bold);
    public static readonly Font FontMono = new("Cascadia Code", 9f);

    /// <summary>
    /// Draw a rounded rectangle.
    /// </summary>
    public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        int d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    /// <summary>
    /// Draw a glass-panel card.
    /// </summary>
    public static void DrawGlassCard(Graphics g, Rectangle bounds, Color? accentColor = null)
    {
        int radius = 10;
        using var path = RoundedRect(bounds, radius);

        // Fill
        using var brush = new SolidBrush(Surface);
        g.FillPath(brush, path);

        // Border with accent
        var accent = accentColor ?? Accent;
        using var borderPen = new Pen(Color.FromArgb(30, accent), 1);
        g.DrawPath(borderPen, path);

        // Top highlight line
        using var highlightPen = new Pen(Color.FromArgb(15, 255, 255, 255), 1);
        g.DrawLine(highlightPen, bounds.X + radius, bounds.Y + 1, bounds.Right - radius, bounds.Y + 1);
    }

    /// <summary>
    /// Draw a stat card with accent bar and glow.
    /// </summary>
    public static void DrawStatCard(Graphics g, Rectangle bounds, string label, string value,
        Color valueColor, Color? iconColor = null)
    {
        DrawGlassCard(g, bounds, iconColor);

        // Left accent bar with glow
        int barW = 3;
        using var glowBrush = new SolidBrush(Color.FromArgb(30, iconColor ?? Accent));
        g.FillRectangle(glowBrush, bounds.X + 2, bounds.Y + 6, 6, bounds.Height - 12);
        using var barBrush = new SolidBrush(iconColor ?? Accent);
        using var barPath = RoundedRect(new Rectangle(bounds.X, bounds.Y + 4, barW, bounds.Height - 8), 2);
        g.FillPath(barBrush, barPath);

        // Label
        int padX = 16;
        using var labelBrush = new SolidBrush(TextSecondary);
        g.DrawString(label, FontSmall, labelBrush, bounds.X + padX, bounds.Y + 10);

        // Value (monospace)
        using var valueFont = new Font("Cascadia Code", 16f, FontStyle.Bold);
        using var valueBrush = new SolidBrush(valueColor);
        g.DrawString(value, valueFont, valueBrush, bounds.X + padX, bounds.Y + 28);
    }

    /// <summary>
    /// Draw a dot grid background pattern.
    /// </summary>
    public static void DrawGridBackground(Graphics g, Rectangle bounds, int spacing = 30)
    {
        using var dotBrush = new SolidBrush(Color.FromArgb(15, 255, 255, 255));
        for (int x = bounds.X; x < bounds.Right; x += spacing)
            for (int y = bounds.Y; y < bounds.Bottom; y += spacing)
                g.FillEllipse(dotBrush, x, y, 1, 1);
    }
}
