using System.Drawing.Drawing2D;
using SkyPilot.Utils;

namespace SkyPilot.Panels;

/// <summary>
/// Circular gauge with arc segments and center value display.
/// </summary>
public class CircularGauge : Control
{
    private float _value;
    private float _maxValue = 100;
    private string _label = "";
    private string _unit = "";
    private Color _accentColor = ModernTheme.Accent;
    private Color _trackColor = ModernTheme.SurfaceLight;
    private float _arcStart = 135;  // degrees
    private float _arcSweep = 270;  // degrees
    private float _arcWidth = 8;
    private float _glowIntensity = 0.3f;

    public float Value
    {
        get => _value;
        set { _value = Math.Clamp(value, 0, _maxValue); Invalidate(); }
    }

    public float MaxValue
    {
        get => _maxValue;
        set { _maxValue = value; Invalidate(); }
    }

    public string Label
    {
        get => _label;
        set { _label = value ?? ""; Invalidate(); }
    }

    public string Unit
    {
        get => _unit;
        set { _unit = value ?? ""; Invalidate(); }
    }

    public Color AccentColor
    {
        get => _accentColor;
        set { _accentColor = value; Invalidate(); }
    }

    public CircularGauge()
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
        float radius = size / 2f - _arcWidth - 4;

        // Track arc (background)
        using var trackPen = new Pen(_trackColor, _arcWidth) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawArc(trackPen, cx - radius, cy - radius, radius * 2, radius * 2, _arcStart, _arcSweep);

        // Value arc
        float pct = _maxValue > 0 ? _value / _maxValue : 0;
        float sweepAngle = _arcSweep * pct;

        if (pct > 0.01f)
        {
            // Glow effect (wider, transparent)
            using var glowPen = new Pen(Color.FromArgb((int)(60 * _glowIntensity), _accentColor), _arcWidth + 6)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            g.DrawArc(glowPen, cx - radius, cy - radius, radius * 2, radius * 2, _arcStart, sweepAngle);

            // Main arc
            using var valuePen = new Pen(_accentColor, _arcWidth) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawArc(valuePen, cx - radius, cy - radius, radius * 2, radius * 2, _arcStart, sweepAngle);
        }

        // Tick marks
        using var tickPen = new Pen(ModernTheme.Border, 1);
        int tickCount = 12;
        for (int i = 0; i <= tickCount; i++)
        {
            float angle = (_arcStart + _arcSweep * i / tickCount) * (float)Math.PI / 180f;
            float innerR = radius - _arcWidth - 2;
            float outerR = radius - _arcWidth - 6;
            float x1 = cx + innerR * (float)Math.Cos(angle);
            float y1 = cy + innerR * (float)Math.Sin(angle);
            float x2 = cx + outerR * (float)Math.Cos(angle);
            float y2 = cy + outerR * (float)Math.Sin(angle);
            g.DrawLine(tickPen, x1, y1, x2, y2);
        }

        // Value text (large)
        string valueText = _value >= 1000 ? $"{_value:F0}" : _value >= 100 ? $"{_value:F0}" : $"{_value:F1}";
        using var valueFont = new Font("Cascadia Code", size * 0.18f, FontStyle.Bold);
        var valueSize = g.MeasureString(valueText, valueFont);
        using var valueBrush = new SolidBrush(ModernTheme.TextPrimary);
        g.DrawString(valueText, valueFont, valueBrush, cx - valueSize.Width / 2, cy - valueSize.Height / 2 - 4);

        // Unit text
        if (!string.IsNullOrEmpty(_unit))
        {
            using var unitFont = new Font("Segoe UI", size * 0.08f);
            var unitSize = g.MeasureString(_unit, unitFont);
            using var unitBrush = new SolidBrush(ModernTheme.TextMuted);
            g.DrawString(_unit, unitFont, unitBrush, cx - unitSize.Width / 2, cy + valueSize.Height / 2 - 6);
        }

        // Label (bottom)
        if (!string.IsNullOrEmpty(_label))
        {
            using var labelFont = new Font("Segoe UI", size * 0.07f, FontStyle.Bold);
            var labelSize = g.MeasureString(_label, labelFont);
            using var labelBrush = new SolidBrush(ModernTheme.TextSecondary);
            g.DrawString(_label, labelFont, labelBrush, cx - labelSize.Width / 2, cy + radius * 0.55f);
        }
    }
}
