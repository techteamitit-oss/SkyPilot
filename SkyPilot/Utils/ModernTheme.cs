using System.Drawing.Drawing2D;

namespace SkyPilot.Utils;

/// <summary>
/// Modern dark theme colors and drawing helpers.
/// </summary>
public static class ModernTheme
{
    // Colors
    public static readonly Color Background = Color.FromArgb(18, 18, 24);
    public static readonly Color Surface = Color.FromArgb(26, 26, 35);
    public static readonly Color SurfaceLight = Color.FromArgb(34, 34, 46);
    public static readonly Color Border = Color.FromArgb(45, 45, 60);
    public static readonly Color TextPrimary = Color.FromArgb(240, 240, 245);
    public static readonly Color TextSecondary = Color.FromArgb(140, 140, 160);
    public static readonly Color TextMuted = Color.FromArgb(90, 90, 110);
    public static readonly Color Accent = Color.FromArgb(80, 140, 255);      // Blue
    public static readonly Color AccentHover = Color.FromArgb(100, 160, 255);
    public static readonly Color Success = Color.FromArgb(60, 200, 120);     // Green
    public static readonly Color Warning = Color.FromArgb(255, 180, 50);     // Orange
    public static readonly Color Danger = Color.FromArgb(255, 70, 80);       // Red
    public static readonly Color Info = Color.FromArgb(100, 200, 255);       // Cyan
    public static readonly Color Armed = Color.FromArgb(255, 60, 70);
    public static readonly Color Disarmed = Color.FromArgb(60, 200, 120);
    public static readonly Color GradientStart = Color.FromArgb(30, 30, 42);
    public static readonly Color GradientEnd = Color.FromArgb(20, 20, 28);

    // Fonts
    public static readonly Font FontRegular = new("Segoe UI", 9.5f);
    public static readonly Font FontBold = new("Segoe UI", 9.5f, FontStyle.Bold);
    public static readonly Font FontSmall = new("Segoe UI", 8.5f);
    public static readonly Font FontTitle = new("Segoe UI", 14f, FontStyle.Bold);
    public static readonly Font FontValue = new("Segoe UI", 20f, FontStyle.Bold);
    public static readonly Font FontMono = new("Cascadia Code", 9f);

    /// <summary>
    /// Draw a rounded rectangle with optional gradient fill.
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
    /// Draw a card with subtle border and gradient background.
    /// </summary>
    public static void DrawCard(Graphics g, Rectangle bounds, Color? fillColor = null)
    {
        int radius = 8;
        using var path = RoundedRect(bounds, radius);
        using var brush = new LinearGradientBrush(bounds, fillColor ?? Surface, SurfaceLight, LinearGradientMode.Vertical);
        g.FillPath(brush, path);
        using var borderPen = new Pen(Border, 1);
        g.DrawPath(borderPen, path);
    }

    /// <summary>
    /// Draw a modern gradient button.
    /// </summary>
    public static void DrawButton(Graphics g, Rectangle bounds, string text, Color baseColor, bool hovered, bool pressed)
    {
        int radius = 6;
        using var path = RoundedRect(bounds, radius);

        Color top = hovered ? ControlPaint.Light(baseColor, 0.2f) : baseColor;
        Color bottom = hovered ? baseColor : ControlPaint.Dark(baseColor, 0.1f);
        if (pressed) { top = ControlPaint.Dark(baseColor, 0.1f); bottom = ControlPaint.Dark(baseColor, 0.2f); }

        using var brush = new LinearGradientBrush(bounds, top, bottom, LinearGradientMode.Vertical);
        g.FillPath(brush, path);

        using var textBrush = new SolidBrush(TextPrimary);
        var textSize = g.MeasureString(text, FontRegular);
        g.DrawString(text, FontRegular, textBrush,
            bounds.X + (bounds.Width - textSize.Width) / 2,
            bounds.Y + (bounds.Height - textSize.Height) / 2);
    }

    /// <summary>
    /// Draw a stat card with icon, label, and value.
    /// </summary>
    public static void DrawStatCard(Graphics g, Rectangle bounds, string label, string value,
        Color valueColor, Color? iconColor = null)
    {
        DrawCard(g, bounds);

        // Left accent bar
        int barW = 3;
        using var barBrush = new SolidBrush(iconColor ?? Accent);
        using var barPath = RoundedRect(new Rectangle(bounds.X, bounds.Y + 4, barW, bounds.Height - 8), 2);
        g.FillPath(barBrush, barPath);

        // Label
        int padX = 16;
        using var labelBrush = new SolidBrush(TextSecondary);
        g.DrawString(label, FontSmall, labelBrush, bounds.X + padX, bounds.Y + 10);

        // Value
        using var valueBrush = new SolidBrush(valueColor);
        g.DrawString(value, FontValue, valueBrush, bounds.X + padX, bounds.Y + 26);
    }

    /// <summary>
    /// Draw a progress bar with gradient.
    /// </summary>
    public static void DrawProgressBar(Graphics g, Rectangle bounds, float value, float max,
        Color? color = null)
    {
        int radius = 4;
        float pct = Math.Clamp(value / max, 0, 1);
        Color barColor = color ?? (pct < 0.5f ? Success : pct < 0.8f ? Warning : Danger);

        // Background
        using var bgPath = RoundedRect(bounds, radius);
        using var bgBrush = new SolidBrush(SurfaceLight);
        g.FillPath(bgBrush, bgPath);

        // Fill
        if (pct > 0.01f)
        {
            int fillW = Math.Max(radius * 2, (int)(bounds.Width * pct));
            var fillBounds = new Rectangle(bounds.X, bounds.Y, fillW, bounds.Height);
            using var fillPath = RoundedRect(fillBounds, radius);
            using var fillBrush = new LinearGradientBrush(fillBounds,
                ControlPaint.Light(barColor, 0.3f), barColor, LinearGradientMode.Vertical);
            g.FillPath(fillBrush, fillPath);
        }
    }
}
