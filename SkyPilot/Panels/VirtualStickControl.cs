using System.Drawing.Drawing2D;
using SkyPilot.Utils;

namespace SkyPilot.Panels;

/// <summary>
/// Dual virtual joystick control for manual flight.
/// Left stick: throttle (Y) + yaw (X). Right stick: pitch (Y) + roll (X).
/// Values range from -1.0 to 1.0.
/// </summary>
public class VirtualStickControl : UserControl
{
    private float _leftX, _leftY; // yaw, throttle
    private float _rightX, _rightY; // roll, pitch
    private int _stickRadius = 50;
    private int _thumbRadius = 14;
    private bool _draggingLeft, _draggingRight;
    private Point _leftCenter, _rightCenter;

    public float LeftX => _leftX;
    public float LeftY => _leftY;
    public float RightX => _rightX;
    public float RightY => _rightY;

    /// <summary>Fires when stick values change. Args: leftX, leftY, rightX, rightY</summary>
    public event Action<float, float, float, float>? StickChanged;

    public VirtualStickControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.FromArgb(13, 17, 23);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            int dxL = e.X - _leftCenter.X, dyL = e.Y - _leftCenter.Y;
            int dxR = e.X - _rightCenter.X, dyR = e.Y - _rightCenter.Y;
            if (dxL * dxL + dyL * dyL < (_stickRadius + 20) * (_stickRadius + 20))
                _draggingLeft = true;
            else if (dxR * dxR + dyR * dyR < (_stickRadius + 20) * (_stickRadius + 20))
                _draggingRight = true;
            UpdateStick(e.Location);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (e.Button == MouseButtons.Left && (_draggingLeft || _draggingRight))
            UpdateStick(e.Location);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _draggingLeft = false;
        _draggingRight = false;
        // Spring back to center
        if (Math.Abs(_leftX) > 0.01f || Math.Abs(_leftY) > 0.01f)
        {
            _leftX = 0; _leftY = 0;
            StickChanged?.Invoke(_leftX, _leftY, _rightX, _rightY);
            Invalidate();
        }
        if (Math.Abs(_rightX) > 0.01f || Math.Abs(_rightY) > 0.01f)
        {
            _rightX = 0; _rightY = 0;
            StickChanged?.Invoke(_leftX, _leftY, _rightX, _rightY);
            Invalidate();
        }
    }

    private void UpdateStick(Point p)
    {
        if (_draggingLeft)
        {
            float dx = (float)(p.X - _leftCenter.X) / _stickRadius;
            float dy = (float)(p.Y - _leftCenter.Y) / _stickRadius;
            float dist = MathF.Sqrt(dx * dx + dy * dy);
            if (dist > 1) { dx /= dist; dy /= dist; }
            _leftX = dx;
            _leftY = -dy; // invert Y (up = positive)
        }
        else if (_draggingRight)
        {
            float dx = (float)(p.X - _rightCenter.X) / _stickRadius;
            float dy = (float)(p.Y - _rightCenter.Y) / _stickRadius;
            float dist = MathF.Sqrt(dx * dx + dy * dy);
            if (dist > 1) { dx /= dist; dy /= dist; }
            _rightX = dx;
            _rightY = -dy;
        }
        StickChanged?.Invoke(_leftX, _leftY, _rightX, _rightY);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int w = Width, h = Height;
        _stickRadius = Math.Min(50, Math.Min(w / 4 - 10, h / 2 - 20));
        _leftCenter = new Point(w / 4, h / 2);
        _rightCenter = new Point(w * 3 / 4, h / 2);

        // Labels
        using var labelFont = new Font("Segoe UI", 7f, FontStyle.Bold);
        using var labelBrush = new SolidBrush(ModernTheme.TextMuted);
        g.DrawString("THROTTLE / YAW", labelFont, labelBrush, _leftCenter.X - 40, 4);
        g.DrawString("PITCH / ROLL", labelFont, labelBrush, _rightCenter.X - 32, 4);

        DrawStick(g, _leftCenter, _leftX, -_leftY, ModernTheme.Accent);
        DrawStick(g, _rightCenter, _rightX, -_rightY, ModernTheme.Warning);
    }

    private void DrawStick(Graphics g, Point center, float nx, float ny, Color color)
    {
        // Outer ring
        using var ringPen = new Pen(Color.FromArgb(60, color.R, color.G, color.B), 2);
        g.DrawEllipse(ringPen, center.X - _stickRadius, center.Y - _stickRadius,
            _stickRadius * 2, _stickRadius * 2);

        // Crosshair lines
        using var crossPen = new Pen(Color.FromArgb(30, color.R, color.G, color.B), 1);
        g.DrawLine(crossPen, center.X - _stickRadius, center.Y, center.X + _stickRadius, center.Y);
        g.DrawLine(crossPen, center.X, center.Y - _stickRadius, center.X, center.Y + _stickRadius);

        // Thumb position
        int tx = center.X + (int)(nx * _stickRadius);
        int ty = center.Y + (int)(ny * _stickRadius);

        // Glow
        using var glowBrush = new SolidBrush(Color.FromArgb(40, color.R, color.G, color.B));
        g.FillEllipse(glowBrush, tx - _thumbRadius - 4, ty - _thumbRadius - 4,
            (_thumbRadius + 4) * 2, (_thumbRadius + 4) * 2);

        // Thumb
        using var thumbBrush = new SolidBrush(Color.FromArgb(200, color.R, color.G, color.B));
        g.FillEllipse(thumbBrush, tx - _thumbRadius, ty - _thumbRadius,
            _thumbRadius * 2, _thumbRadius * 2);

        // Center dot
        using var centerBrush = new SolidBrush(Color.FromArgb(100, 255, 255, 255));
        g.FillEllipse(centerBrush, tx - 3, ty - 3, 6, 6);
    }
}
