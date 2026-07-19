using System.Drawing.Drawing2D;
using SkyPilot.Utils;

namespace SkyPilot.Panels;

/// <summary>
/// Simulated FPV (First Person View) with artificial horizon, heading tape, altitude/speed readouts.
/// Renders a synthetic flight view based on vehicle attitude.
/// </summary>
public class FPVPanel : UserControl
{
    private float _pitch, _roll, _heading;
    private float _altitude, _speed;
    private float _throttle;

    public FPVPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.FromArgb(13, 17, 23);
    }

    public void UpdateAttitude(float pitch, float roll, float heading, float altitude, float speed, float throttle)
    {
        _pitch = pitch;
        _roll = roll;
        _heading = heading;
        _altitude = altitude;
        _speed = speed;
        _throttle = throttle;
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

        int cx = w / 2, cy = h / 2;

        // === ARTIFICIAL HORIZON ===
        // Sky color (top) and ground color (bottom) based on pitch and roll
        g.TranslateTransform(cx, cy);
        g.RotateTransform(-_roll);

        float pitchOffset = _pitch * 2.5f; // pixels per degree

        // Ground (brown)
        using var groundBrush = new SolidBrush(Color.FromArgb(180, 120, 80, 40));
        g.FillRectangle(groundBrush, -w, pitchOffset, w * 2, h);

        // Sky (blue)
        using var skyBrush = new SolidBrush(Color.FromArgb(180, 40, 80, 160));
        g.FillRectangle(skyBrush, -w, -h + pitchOffset, w * 2, h);

        // Horizon line
        using var horizonPen = new Pen(Color.FromArgb(200, 255, 255, 255), 2);
        g.DrawLine(horizonPen, -w, pitchOffset, w, pitchOffset);

        // Pitch ladder lines
        using var pitchPen = new Pen(Color.FromArgb(120, 255, 255, 255), 1);
        using var pitchFont = new Font("Cascadia Code", 8f);
        using var pitchBrush = new SolidBrush(Color.FromArgb(150, 255, 255, 255));
        for (int deg = -30; deg <= 30; deg += 10)
        {
            if (deg == 0) continue;
            float y = pitchOffset - deg * 2.5f;
            int lineW = deg % 20 == 0 ? 60 : 30;
            g.DrawLine(pitchPen, -lineW, y, lineW, y);
            if (deg % 20 == 0)
            {
                g.DrawString(Math.Abs(deg).ToString(), pitchFont, pitchBrush, lineW + 4, y - 6);
            }
        }

        g.ResetTransform();

        // === FLIGHT PATH MARKER (velocity vector) ===
        float fpmX = cx;
        float fpmY = cy - _pitch * 2.5f;
        using var fpmPen = new Pen(Color.FromArgb(200, 0, 255, 0), 2);
        g.DrawEllipse(fpmPen, fpmX - 8, fpmY - 8, 16, 16);
        g.DrawLine(fpmPen, fpmX - 14, fpmY, fpmX - 8, fpmY);
        g.DrawLine(fpmPen, fpmX + 8, fpmY, fpmX + 14, fpmY);
        g.DrawLine(fpmPen, fpmX, fpmY + 8, fpmX, fpmY + 14);

        // === BANK ANGLE INDICATOR (top center) ===
        DrawBankIndicator(g, cx, 30);

        // === HEADING TAPE (top) ===
        DrawHeadingTape(g, cx, 10, w);

        // === ALTITUDE TAPE (right side) ===
        DrawAltitudeTape(g, w - 60, cy, h);

        // === SPEED TAPE (left side) ===
        DrawSpeedTape(g, 10, cy, h);

        // === CENTER RETICLE ===
        using var reticlePen = new Pen(Color.FromArgb(150, 0, 212, 255), 2);
        g.DrawLine(reticlePen, cx - 30, cy, cx - 10, cy);
        g.DrawLine(reticlePen, cx + 10, cy, cx + 30, cy);
        g.DrawLine(reticlePen, cx, cy - 10, cx, cy - 6);

        // === THROTTLE BAR (bottom left) ===
        DrawThrottleBar(g, 10, h - 30, 80, 12);

        // === BORDER ===
        using var borderPen = new Pen(Color.FromArgb(60, 0, 212, 255), 1);
        g.DrawRectangle(borderPen, 0, 0, w - 1, h - 1);
    }

    private void DrawHeadingTape(Graphics g, int cx, int y, int w)
    {
        int tapeW = 200;
        int tapeH = 22;
        int tapeX = cx - tapeW / 2;

        // Background
        using var bgBrush = new SolidBrush(Color.FromArgb(140, 13, 17, 23));
        g.FillRectangle(bgBrush, tapeX, y, tapeW, tapeH);
        using var borderPen = new Pen(Color.FromArgb(80, 0, 212, 255), 1);
        g.DrawRectangle(borderPen, tapeX, y, tapeW, tapeH);

        // Draw heading marks
        using var markFont = new Font("Cascadia Code", 7f);
        using var markBrush = new SolidBrush(ModernTheme.TextMuted);
        using var markPen = new Pen(Color.FromArgb(100, 0, 212, 255), 1);

        float pxPerDeg = tapeW / 60f; // show 60 degrees range
        for (int d = -30; d <= 30; d += 5)
        {
            float hdg = (_heading + d + 360) % 360;
            int x = cx + (int)(d * pxPerDeg);
            if (x < tapeX || x > tapeX + tapeW) continue;
            g.DrawLine(markPen, x, y + tapeH - 6, x, y + tapeH);
            if (d % 10 == 0)
            {
                string label = hdg.ToString("F0");
                g.DrawString(label, markFont, markBrush, x - 8, y + 2);
            }
        }

        // Center pointer
        using var ptrBrush = new SolidBrush(ModernTheme.Accent);
        g.FillPolygon(ptrBrush, new Point[] {
            new(cx - 4, y + tapeH), new(cx + 4, y + tapeH), new(cx, y + tapeH + 6)
        });
    }

    private void DrawAltitudeTape(Graphics g, int x, int cy, int h)
    {
        int tapeW = 50, tapeH = Math.Min(200, h - 20);
        int tapeY = cy - tapeH / 2;

        using var bgBrush = new SolidBrush(Color.FromArgb(140, 13, 17, 23));
        g.FillRectangle(bgBrush, x, tapeY, tapeW, tapeH);
        using var borderPen = new Pen(Color.FromArgb(80, 0, 212, 255), 1);
        g.DrawRectangle(borderPen, x, tapeY, tapeW, tapeH);

        using var markFont = new Font("Cascadia Code", 7f);
        using var markBrush = new SolidBrush(ModernTheme.TextMuted);
        using var markPen = new Pen(Color.FromArgb(100, 0, 212, 255), 1);
        using var valFont = new Font("Cascadia Code", 10f, FontStyle.Bold);
        using var valBrush = new SolidBrush(ModernTheme.Accent);

        float pxPerM = tapeH / 100f; // 100m range
        for (int d = -50; d <= 50; d += 10)
        {
            float alt = _altitude + d;
            if (alt < 0) continue;
            int y = cy - (int)(d * pxPerM);
            if (y < tapeY || y > tapeY + tapeH) continue;
            g.DrawLine(markPen, x, y, x + 8, y);
            g.DrawString(alt.ToString("F0"), markFont, markBrush, x + 10, y - 5);
        }

        // Current value box
        int boxY = cy - 10;
        using var boxBrush = new SolidBrush(Color.FromArgb(200, 0, 212, 255));
        g.FillRectangle(boxBrush, x, boxY, tapeW, 20);
        g.DrawString($"{_altitude:F0}", valFont, new SolidBrush(Color.White), x + 4, boxY + 2);
    }

    private void DrawSpeedTape(Graphics g, int x, int cy, int h)
    {
        int tapeW = 50, tapeH = Math.Min(200, h - 20);
        int tapeY = cy - tapeH / 2;

        using var bgBrush = new SolidBrush(Color.FromArgb(140, 13, 17, 23));
        g.FillRectangle(bgBrush, x, tapeY, tapeW, tapeH);
        using var borderPen = new Pen(Color.FromArgb(80, 0, 212, 255), 1);
        g.DrawRectangle(borderPen, x, tapeY, tapeW, tapeH);

        using var markFont = new Font("Cascadia Code", 7f);
        using var markBrush = new SolidBrush(ModernTheme.TextMuted);
        using var markPen = new Pen(Color.FromArgb(100, 0, 212, 255), 1);
        using var valFont = new Font("Cascadia Code", 10f, FontStyle.Bold);

        float pxPerMs = tapeH / 40f; // 40 m/s range
        for (int d = -20; d <= 20; d += 5)
        {
            float spd = _speed + d;
            if (spd < 0) continue;
            int y = cy - (int)(d * pxPerMs);
            if (y < tapeY || y > tapeY + tapeH) continue;
            int lineX = x + tapeW;
            g.DrawLine(markPen, lineX - 8, y, lineX, y);
            g.DrawString(spd.ToString("F0"), markFont, markBrush, x, y - 5);
        }

        // Current value box
        int boxY = cy - 10;
        using var boxBrush = new SolidBrush(Color.FromArgb(200, 0, 212, 255));
        g.FillRectangle(boxBrush, x, boxY, tapeW, 20);
        g.DrawString($"{_speed:F0}", valFont, new SolidBrush(Color.White), x + 4, boxY + 2);
    }

    private void DrawBankIndicator(Graphics g, int cx, int y)
    {
        using var pen = new Pen(Color.FromArgb(120, 255, 255, 255), 1);
        using var font = new Font("Cascadia Code", 7f);
        using var brush = new SolidBrush(ModernTheme.TextMuted);

        int radius = 30;
        for (int deg = -60; deg <= 60; deg += 10)
        {
            double rad = (deg - 90) * Math.PI / 180.0;
            int x1 = cx + (int)(Math.Cos(rad) * radius);
            int y1 = y + (int)(Math.Sin(rad) * radius);
            int x2 = cx + (int)(Math.Cos(rad) * (radius - 6));
            int y2 = y + (int)(Math.Sin(rad) * (radius - 6));
            g.DrawLine(pen, x1, y1, x2, y2);
        }

        // Current bank pointer
        double bankRad = (_roll - 90) * Math.PI / 180.0;
        int bx = cx + (int)(Math.Cos(bankRad) * radius);
        int by = y + (int)(Math.Sin(bankRad) * radius);
        using var ptrBrush = new SolidBrush(ModernTheme.Accent);
        g.FillPolygon(ptrBrush, new Point[] {
            new(bx - 4, by + 4), new(bx + 4, by + 4), new(bx, by - 2)
        });
    }

    private void DrawThrottleBar(Graphics g, int x, int y, int w, int h)
    {
        using var bgBrush = new SolidBrush(Color.FromArgb(140, 13, 17, 23));
        g.FillRectangle(bgBrush, x, y, w, h);
        using var borderPen = new Pen(Color.FromArgb(80, 0, 212, 255), 1);
        g.DrawRectangle(borderPen, x, y, w, h);

        float fill = Math.Clamp(_throttle, 0, 1);
        Color barColor = fill > 0.8f ? ModernTheme.Warning : ModernTheme.Accent;
        using var barBrush = new SolidBrush(barColor);
        g.FillRectangle(barBrush, x + 1, y + 1, (int)((w - 2) * fill), h - 2);

        using var font = new Font("Cascadia Code", 7f);
        using var brush = new SolidBrush(ModernTheme.TextPrimary);
        g.DrawString($"THR {fill * 100:F0}%", font, brush, x + 2, y - 12);
    }
}
