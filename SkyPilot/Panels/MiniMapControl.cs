using System.Drawing.Drawing2D;
using SkyPilot.Utils;

namespace SkyPilot.Panels;

/// <summary>
/// Lightweight GDI+ mini map showing vehicle position with trail and zoom controls.
/// </summary>
public class MiniMapControl : UserControl
{
    private double _lat, _lon;
    private float _heading;
    private readonly List<(double Lat, double Lon)> _trail = new();
    private double _centerLat = 51.5074, _centerLon = -0.1278;
    private double _zoom = 0.002; // degrees per pixel
    private readonly Button _zoomInBtn;
    private readonly Button _zoomOutBtn;

    public MiniMapControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = ModernTheme.Background;

        _zoomInBtn = new Button
        {
            Text = "+",
            Size = new Size(22, 22),
            Location = new Point(0, 0),
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
            BackColor = Color.FromArgb(140, 33, 38, 45),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _zoomInBtn.FlatAppearance.BorderSize = 0;
        _zoomInBtn.Click += (s, e) => { _zoom = Math.Max(0.0002, _zoom * 0.7); Invalidate(); };
        Controls.Add(_zoomInBtn);

        _zoomOutBtn = new Button
        {
            Text = "-",
            Size = new Size(22, 22),
            Location = new Point(24, 0),
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
            BackColor = Color.FromArgb(140, 33, 38, 45),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _zoomOutBtn.FlatAppearance.BorderSize = 0;
        _zoomOutBtn.Click += (s, e) => { _zoom = Math.Min(0.01, _zoom * 1.4); Invalidate(); };
        Controls.Add(_zoomOutBtn);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (e.Delta > 0)
            _zoom = Math.Max(0.0002, _zoom * 0.8);
        else
            _zoom = Math.Min(0.01, _zoom * 1.25);
        Invalidate();
    }

    public void UpdatePosition(double lat, double lon, float heading)
    {
        _lat = lat;
        _lon = lon;
        _heading = heading;
        _trail.Add((lat, lon));
        if (_trail.Count > 200) _trail.RemoveAt(0);
        if (_trail.Count == 1)
        {
            _centerLat = lat;
            _centerLon = lon;
        }
        Invalidate();
    }

    public void Reset()
    {
        _trail.Clear();
        _centerLat = 51.5074;
        _centerLon = -0.1278;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int w = Width, h = Height;

        using var bgBrush = new SolidBrush(Color.FromArgb(13, 17, 23));
        g.FillRectangle(bgBrush, 0, 0, w, h);
        using var borderPen = new Pen(Color.FromArgb(60, 0, 212, 255), 1);
        g.DrawRectangle(borderPen, 0, 0, w - 1, h - 1);

        // Grid lines
        using var gridPen = new Pen(Color.FromArgb(20, 0, 212, 255), 1);
        for (int i = 1; i < 4; i++)
        {
            g.DrawLine(gridPen, w * i / 4, 0, w * i / 4, h);
            g.DrawLine(gridPen, 0, h * i / 4, w, h * i / 4);
        }

        if (_trail.Count < 2) return;

        double pixelsPerDeg = w / (2.0 * _zoom);
        int cx = w / 2, cy = h / 2;

        // Draw trail
        using var trailPen = new Pen(Color.FromArgb(100, 0, 212, 255), 2);
        for (int i = 1; i < _trail.Count; i++)
        {
            int x1 = cx + (int)((_trail[i - 1].Lon - _centerLon) * pixelsPerDeg);
            int y1 = cy - (int)((_trail[i - 1].Lat - _centerLat) * pixelsPerDeg);
            int x2 = cx + (int)((_trail[i].Lon - _centerLon) * pixelsPerDeg);
            int y2 = cy - (int)((_trail[i].Lat - _centerLat) * pixelsPerDeg);
            int alpha = 30 + (int)(200.0 * i / _trail.Count);
            trailPen.Color = Color.FromArgb(alpha, 0, 212, 255);
            g.DrawLine(trailPen, x1, y1, x2, y2);
        }

        // Draw vehicle marker
        int vx = cx + (int)((_lon - _centerLon) * pixelsPerDeg);
        int vy = cy - (int)((_lat - _centerLat) * pixelsPerDeg);

        using var glowBrush = new SolidBrush(Color.FromArgb(60, 0, 212, 255));
        g.FillEllipse(glowBrush, vx - 12, vy - 12, 24, 24);

        using var markerBrush = new SolidBrush(Color.FromArgb(220, 0, 212, 255));
        g.FillEllipse(markerBrush, vx - 5, vy - 5, 10, 10);

        // Heading arrow
        double rad = _heading * Math.PI / 180.0;
        int arrowLen = 15;
        int ax = vx + (int)(Math.Sin(rad) * arrowLen);
        int ay = vy - (int)(Math.Cos(rad) * arrowLen);
        using var arrowPen = new Pen(Color.FromArgb(200, 0, 212, 255), 2);
        g.DrawLine(arrowPen, vx, vy, ax, ay);

        // Label
        using var labelFont = new Font("Segoe UI", 7f);
        using var labelBrush = new SolidBrush(ModernTheme.TextMuted);
        g.DrawString("MAP", labelFont, labelBrush, 4, 2);

        // Zoom level indicator
        string zoomText = _zoom < 0.001 ? "Close" : _zoom < 0.003 ? "Medium" : "Far";
        g.DrawString(zoomText, labelFont, labelBrush, w - 30, h - 12);
    }
}
