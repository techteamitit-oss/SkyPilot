using System.Drawing.Drawing2D;
using SkyPilot.Utils;

namespace SkyPilot.Panels;

/// <summary>
/// Modern flat button with gradient, rounded corners, and hover effects.
/// </summary>
public class ModernButton : Control
{
    private Color _baseColor;
    private bool _hovered;
    private bool _pressed;
    private readonly System.Windows.Forms.Timer _hoverTimer;

    public Color BaseColor
    {
        get => _baseColor;
        set { _baseColor = value; Invalidate(); }
    }

    public ModernButton(string text = "", Color? color = null)
    {
        _baseColor = color ?? ModernTheme.Accent;
        Text = text;
        Size = new Size(100, 36);
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

        _hoverTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _hoverTimer.Tick += (s, e) => { _hoverTimer.Stop(); Invalidate(); };
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        _pressed = true;
        Invalidate();
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _pressed = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        int radius = 6;
        var bounds = new Rectangle(1, 1, Width - 3, Height - 3);

        using var path = ModernTheme.RoundedRect(bounds, radius);

        Color top = _hovered ? ControlPaint.Light(_baseColor, 0.15f) : _baseColor;
        Color bottom = _hovered ? _baseColor : ControlPaint.Dark(_baseColor, 0.1f);
        if (_pressed) { top = ControlPaint.Dark(_baseColor, 0.1f); bottom = ControlPaint.Dark(_baseColor, 0.2f); }

        using var brush = new LinearGradientBrush(bounds, top, bottom, LinearGradientMode.Vertical);
        g.FillPath(brush, path);

        // Border
        using var borderPen = new Pen(ControlPaint.Light(_baseColor, 0.2f), 1);
        g.DrawPath(borderPen, path);

        // Text
        using var textBrush = new SolidBrush(ModernTheme.TextPrimary);
        var textSize = g.MeasureString(Text, ModernTheme.FontRegular);
        g.DrawString(Text, ModernTheme.FontRegular, textBrush,
            (Width - textSize.Width) / 2, (Height - textSize.Height) / 2);
    }
}
