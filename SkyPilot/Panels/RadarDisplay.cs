using System.Drawing.Drawing2D;
using SkyPilot.Utils;

namespace SkyPilot.Panels;

/// <summary>
/// Rotating radar display showing vehicle at center, waypoints, home position, and range rings.
/// Rotates with vehicle heading so "forward" is always up.
/// </summary>
public class RadarDisplay : UserControl
{
    private double _lat, _lon;
    private float _heading;
    private double _homeLat, _homeLon;
    private bool _hasHome;
    private readonly List<(double Lat, double Lon, int Index)> _waypoints = new();
    private double _range = 0.005; // degrees (~500m)

    public RadarDisplay()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.FromArgb(13, 17, 23);
    }

    public void UpdatePosition(double lat, double lon, float heading)
    {
        _lat = lat;
        _lon = lon;
        _heading = heading;
        Invalidate();
    }

    public void SetHome(double lat, double lon)
    {
        _homeLat = lat;
        _homeLon = lon;
        _hasHome = true;
        Invalidate();
    }

    public void SetWaypoints(List<(double Lat, double Lon)> waypoints)
    {
        _waypoints.Clear();
        for (int i = 0; i < waypoints.Count; i++)
            _waypoints.Add((waypoints[i].Lat, waypoints[i].Lon, i + 1));
        Invalidate();
    }

    public void ClearWaypoints()
    {
        _waypoints.Clear();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        int w = Width, h = Height;
        int cx = w / 2, cy = h / 2;
        int radius = Math.Min(cx, cy) - 8;

        // Background circle
        using var bgBrush = new SolidBrush(Color.FromArgb(18, 22, 28));
        g.FillEllipse(bgBrush, cx - radius, cy - radius, radius * 2, radius * 2);

        // Clip to circle
        var prevClip = g.Clip;
        var clipRegion = new Region(new Rectangle(cx - radius, cy - radius, radius * 2, radius * 2));
        g.Clip = clipRegion;

        // Rotate so heading is up
        g.TranslateTransform(cx, cy);
        g.RotateTransform(-_heading);

        // Range rings (3 rings)
        using var ringPen = new Pen(Color.FromArgb(30, 0, 212, 255), 1);
        for (int i = 1; i <= 3; i++)
        {
            int r = radius * i / 3;
            g.DrawEllipse(ringPen, -r, -r, r * 2, r * 2);
        }

        // Cross hairs
        using var crossPen = new Pen(Color.FromArgb(20, 0, 212, 255), 1);
        g.DrawLine(crossPen, -radius, 0, radius, 0);
        g.DrawLine(crossPen, 0, -radius, 0, radius);

        // Diagonal lines
        using var diagPen = new Pen(Color.FromArgb(12, 0, 212, 255), 1);
        int diag = (int)(radius * 0.707);
        g.DrawLine(diagPen, -diag, -diag, diag, diag);
        g.DrawLine(diagPen, diag, -diag, -diag, diag);

        // Scale labels on rings
        using var scaleFont = new Font("Cascadia Code", 6f);
        using var scaleBrush = new SolidBrush(Color.FromArgb(50, 0, 212, 255));
        double[] ringDist = { _range * 3, _range * 2, _range };
        for (int i = 0; i < 3; i++)
        {
            int r = radius * (i + 1) / 3;
            double distM = ringDist[i] * 111320.0; // degrees to meters (approx)
            string label = distM >= 1000 ? $"{distM / 1000:F1}km" : $"{distM:F0}m";
            // Draw label rotated back so text is readable
            g.RotateTransform(_heading);
            var sz = g.MeasureString(label, scaleFont);
            g.DrawString(label, scaleFont, scaleBrush, -sz.Width / 2, -r - sz.Height + 2);
            g.RotateTransform(-_heading);
        }

        // Home marker
        if (_hasHome)
        {
            int hx = (int)((_homeLon - _lon) / _range * radius);
            int hy = -((int)((_homeLat - _lat) / _range * radius));
            double hDist = Math.Sqrt(Math.Pow((_homeLon - _lon) * 111320, 2) + Math.Pow((_homeLat - _lat) * 111320, 2));

            if (Math.Abs(hx) < radius && Math.Abs(hy) < radius)
            {
                // Home glow
                using var homeGlow = new SolidBrush(Color.FromArgb(40, 255, 100, 100));
                g.FillEllipse(homeGlow, hx - 10, hy - 10, 20, 20);

                // Home diamond
                using var homeBrush = new SolidBrush(Color.FromArgb(200, 255, 80, 80));
                var diamond = new Point[] {
                    new(hx, hy - 7), new(hx + 5, hy), new(hx, hy + 7), new(hx - 5, hy)
                };
                g.FillPolygon(homeBrush, diamond);

                // H label
                using var homeFont = new Font("Segoe UI", 7f, FontStyle.Bold);
                using var homeLabelBrush = new SolidBrush(Color.FromArgb(220, 255, 80, 80));
                g.DrawString("H", homeFont, homeLabelBrush, hx - 4, hy - 18);
            }
        }

        // Waypoint markers
        foreach (var wp in _waypoints)
        {
            int wx = (int)((wp.Lon - _lon) / _range * radius);
            int wy = -((int)((wp.Lat - _lat) / _range * radius));

            if (Math.Abs(wx) > radius || Math.Abs(wy) > radius) continue;

            double wpDist = Math.Sqrt(Math.Pow((wp.Lon - _lon) * 111320, 2) + Math.Pow((wp.Lat - _lat) * 111320, 2));

            // Waypoint glow
            using var wpGlow = new SolidBrush(Color.FromArgb(30, 0, 212, 255));
            g.FillEllipse(wpGlow, wx - 8, wy - 8, 16, 16);

            // Waypoint circle
            using var wpPen = new Pen(Color.FromArgb(200, 0, 212, 255), 2);
            g.DrawEllipse(wpPen, wx - 5, wy - 5, 10, 10);

            // Waypoint dot
            using var wpDot = new SolidBrush(ModernTheme.Accent);
            g.FillEllipse(wpDot, wx - 3, wy - 3, 6, 6);

            // Waypoint number
            using var wpFont = new Font("Segoe UI", 7f, FontStyle.Bold);
            using var wpLabelBrush = new SolidBrush(ModernTheme.Accent);
            g.DrawString(wp.Index.ToString(), wpFont, wpLabelBrush, wx + 7, wy - 5);
        }

        g.ResetTransform();
        g.Clip = prevClip;

        // Outer ring
        using var outerPen = new Pen(Color.FromArgb(80, 0, 212, 255), 2);
        g.DrawEllipse(outerPen, cx - radius, cy - radius, radius * 2, radius * 2);

        // Cardinal direction labels (fixed, not rotating)
        using var dirFont = new Font("Segoe UI", 8f, FontStyle.Bold);
        var dirs = new[] {
            ("N", 0, -radius - 12, Color.FromArgb(255, 60, 60)),
            ("E", radius + 4, -4, ModernTheme.Accent),
            ("S", -3, radius + 2, Color.FromArgb(200, 200, 200)),
            ("W", -radius - 14, -4, ModernTheme.Accent)
        };
        foreach (var (label, dx, dy, color) in dirs)
        {
            using var dirBrush = new SolidBrush(color);
            var sz = g.MeasureString(label, dirFont);
            g.DrawString(label, dirFont, dirBrush, cx + dx - sz.Width / 2, cy + dy);
        }

        // Vehicle marker (center) — triangle pointing up
        using var vehBrush = new SolidBrush(ModernTheme.Accent);
        g.FillPolygon(vehBrush, new Point[] {
            new(cx, cy - 8), new(cx - 5, cy + 4), new(cx + 5, cy + 4)
        });

        // Vehicle glow
        using var vehGlow = new SolidBrush(Color.FromArgb(40, 0, 212, 255));
        g.FillEllipse(vehGlow, cx - 12, cy - 12, 24, 24);
        g.FillPolygon(vehBrush, new Point[] {
            new(cx, cy - 8), new(cx - 5, cy + 4), new(cx + 5, cy + 4)
        });

        // Range label
        using var rangeFont = new Font("Segoe UI", 7f);
        using var rangeBrush = new SolidBrush(ModernTheme.TextMuted);
        double maxDistM = _range * 3 * 111320.0;
        string rangeText = maxDistM >= 1000 ? $"Range: {maxDistM / 1000:F1}km" : $"Range: {maxDistM:F0}m";
        g.DrawString(rangeText, rangeFont, rangeBrush, 4, h - 14);

        // Label
        using var lblFont = new Font("Segoe UI", 7f, FontStyle.Bold);
        using var lblBrush = new SolidBrush(ModernTheme.Accent);
        g.DrawString("RADAR", lblFont, lblBrush, 4, 2);
    }
}
