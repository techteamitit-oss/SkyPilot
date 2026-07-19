using System.Drawing.Drawing2D;
using SkyPilot.Core.Mavlink;
using SkyPilot.Utils;

namespace SkyPilot.Panels;

/// <summary>
/// Neon-style HUD with glow effects and glass cockpit aesthetics.
/// </summary>
public class NeonHud : UserControl
{
    private VehicleState? _state;
    private readonly System.Windows.Forms.Timer _animTimer;
    private float _glowPhase;

    public NeonHud()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = ModernTheme.Background;
        DoubleBuffered = true;

        _animTimer = new System.Windows.Forms.Timer { Interval = 33 }; // ~30fps
        _animTimer.Tick += (s, e) => { _glowPhase += 0.1f; Invalidate(); };
        _animTimer.Start();
    }

    public void UpdateFromState(VehicleState state)
    {
        _state = state;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_state == null) return;

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        int w = Width, h = Height;
        float roll = _state.Roll;
        float pitch = _state.Pitch;
        float yaw = _state.Yaw;
        float speed = _state.GroundSpeed;
        float alt = _state.AltitudeRel;
        float vs = _state.VerticalSpeed;

        DrawHorizon(g, w, h, roll, pitch);
        DrawSpeedTape(g, w, h, speed);
        DrawAltTape(g, w, h, alt, vs);
        DrawHeadingTape(g, w, h, yaw);
        DrawAircraftSymbol(g, w, h);
        DrawStatusOverlay(g, w, h);
    }

    private void DrawHorizon(Graphics g, int w, int h, float roll, float pitch)
    {
        int cx = w / 2, cy = h / 2;
        float pitchOffset = pitch * 3;
        float groundY = cy + pitchOffset;

        g.TranslateTransform(cx, cy);
        g.RotateTransform(-roll);

        // Sky - gradient from dark to neon cyan
        using var skyBrush = new LinearGradientBrush(new Point(0, -h), new Point(0, 0),
            Color.FromArgb(15, 20, 40), Color.FromArgb(20, 40, 60));
        g.FillRectangle(skyBrush, -w, -h, w * 2, (int)(groundY + h));

        // Ground - dark with subtle grid
        using var groundBrush = new SolidBrush(Color.FromArgb(30, 20, 15));
        g.FillRectangle(groundBrush, -w, (int)groundY, w * 2, h * 2);

        // Horizon line with neon glow
        int glowAlpha = 80 + (int)(40 * Math.Sin(_glowPhase));
        using var glowPen = new Pen(Color.FromArgb(glowAlpha, 0, 212, 255), 6);
        g.DrawLine(glowPen, -w * 2, groundY, w * 2, groundY);
        using var horizonPen = new Pen(Color.FromArgb(200, 0, 212, 255), 2);
        g.DrawLine(horizonPen, -w * 2, groundY, w * 2, groundY);

        // Pitch ladder
        using var pitchPen = new Pen(Color.FromArgb(120, 0, 212, 255), 1);
        using var pitchFont = new Font("Cascadia Code", 8f);
        for (int deg = -40; deg <= 40; deg += 5)
        {
            if (deg == 0) continue;
            float y = groundY - deg * 3;
            int lineW = deg % 10 == 0 ? 70 : 35;
            g.DrawLine(pitchPen, -lineW, y, lineW, y);
            if (deg % 10 == 0)
            {
                string text = $"{Math.Abs(deg)}";
                var size = g.MeasureString(text, pitchFont);
                using var tb = new SolidBrush(Color.FromArgb(150, 0, 212, 255));
                g.DrawString(text, pitchFont, tb, -lineW - size.Width - 4, y - size.Height / 2);
                g.DrawString(text, pitchFont, tb, lineW + 4, y - size.Height / 2);
            }
        }

        g.ResetTransform();
    }

    private void DrawSpeedTape(Graphics g, int w, int h, float speed)
    {
        int tapeW = 70, tapeX = 15;
        int tapeTop = 60, tapeBot = h - 90;

        // Glass background
        using var bgBrush = new SolidBrush(Color.FromArgb(120, 10, 15, 25));
        g.FillRectangle(bgBrush, tapeX, tapeTop, tapeW, tapeBot - tapeTop);
        using var borderPen = new Pen(Color.FromArgb(40, 0, 212, 255), 1);
        g.DrawRectangle(borderPen, tapeX, tapeTop, tapeW, tapeBot - tapeTop);

        // Ticks
        using var tickPen = new Pen(Color.FromArgb(80, 0, 212, 255), 1);
        using var labelFont = new Font("Cascadia Code", 8.5f);
        float pixPerUnit = (tapeBot - tapeTop) / 25f;

        for (int s = (int)speed - 12; s <= (int)speed + 12; s++)
        {
            if (s < 0) continue;
            float y = (tapeBot - tapeTop) / 2f - (s - speed) * pixPerUnit + tapeTop;
            if (y < tapeTop || y > tapeBot) continue;

            int tickLen = s % 5 == 0 ? 12 : 6;
            g.DrawLine(tickPen, tapeX + tapeW - tickLen, y, tapeX + tapeW, y);
            if (s % 5 == 0)
            {
                using var lb = new SolidBrush(ModernTheme.TextSecondary);
                g.DrawString($"{s}", labelFont, lb, tapeX + 4, y - 6);
            }
        }

        // Center value box with glow
        using var valFont = new Font("Cascadia Code", 13f, FontStyle.Bold);
        string speedText = $"{speed:F0}";
        var size = g.MeasureString(speedText, valFont);
        float boxY = (tapeBot - tapeTop) / 2f + tapeTop - size.Height / 2;
        using var boxBrush = new SolidBrush(Color.FromArgb(180, 0, 100, 150));
        g.FillRectangle(boxBrush, tapeX, boxY, tapeW, size.Height + 4);
        using var valBrush = new SolidBrush(Color.FromArgb(0, 212, 255));
        g.DrawString(speedText, valFont, valBrush, tapeX + (tapeW - size.Width) / 2, boxY + 2);

        // Label
        using var smallFont = new Font("Segoe UI", 7.5f);
        using var lb2 = new SolidBrush(ModernTheme.TextMuted);
        g.DrawString("GS m/s", smallFont, lb2, tapeX + 8, tapeBot + 4);
    }

    private void DrawAltTape(Graphics g, int w, int h, float alt, float vs)
    {
        int tapeW = 70, tapeX = w - 85;
        int tapeTop = 60, tapeBot = h - 90;

        // Glass background
        using var bgBrush = new SolidBrush(Color.FromArgb(120, 10, 15, 25));
        g.FillRectangle(bgBrush, tapeX, tapeTop, tapeW, tapeBot - tapeTop);
        using var borderPen = new Pen(Color.FromArgb(40, 0, 212, 255), 1);
        g.DrawRectangle(borderPen, tapeX, tapeTop, tapeW, tapeBot - tapeTop);

        // Ticks
        using var tickPen = new Pen(Color.FromArgb(80, 0, 212, 255), 1);
        using var labelFont = new Font("Cascadia Code", 8.5f);
        float pixPerUnit = (tapeBot - tapeTop) / 50f;

        for (int a = (int)alt - 25; a <= (int)alt + 25; a++)
        {
            float y = (tapeBot - tapeTop) / 2f - (a - alt) * pixPerUnit + tapeTop;
            if (y < tapeTop || y > tapeBot) continue;

            int tickLen = a % 10 == 0 ? 12 : 6;
            g.DrawLine(tickPen, tapeX, y, tapeX + tickLen, y);
            if (a % 10 == 0)
            {
                using var lb = new SolidBrush(ModernTheme.TextSecondary);
                g.DrawString($"{a}", labelFont, lb, tapeX + tapeW - 35, y - 6);
            }
        }

        // Center value box
        using var valFont = new Font("Cascadia Code", 13f, FontStyle.Bold);
        string altText = $"{alt:F0}";
        var size = g.MeasureString(altText, valFont);
        float boxY = (tapeBot - tapeTop) / 2f + tapeTop - size.Height / 2;
        using var boxBrush = new SolidBrush(Color.FromArgb(180, 0, 100, 150));
        g.FillRectangle(boxBrush, tapeX, boxY, tapeW, size.Height + 4);
        using var valBrush = new SolidBrush(Color.FromArgb(0, 255, 136));
        g.DrawString(altText, valFont, valBrush, tapeX + (tapeW - size.Width) / 2, boxY + 2);

        // VSI indicator
        float vsiY = (tapeBot - tapeTop) / 2f + tapeTop;
        float vsiOffset = Math.Clamp(vs * 3, -(tapeBot - tapeTop) / 2f, (tapeBot - tapeTop) / 2f);
        float triY = vsiY - vsiOffset;
        using var vsiBrush = new SolidBrush(Color.FromArgb(255, 184, 0));
        g.FillPolygon(vsiBrush, new PointF[] {
            new(tapeX - 12, triY),
            new(tapeX - 2, triY - 5),
            new(tapeX - 2, triY + 5)
        });

        using var smallFont = new Font("Segoe UI", 7.5f);
        using var lb2 = new SolidBrush(ModernTheme.TextMuted);
        g.DrawString("ALT m", smallFont, lb2, tapeX + 8, tapeBot + 4);
    }

    private void DrawHeadingTape(Graphics g, int w, int h, float yaw)
    {
        int tapeH = 28, tapeY = 12;
        int tapeLeft = 100, tapeRight = w - 100;

        // Glass background
        using var bgBrush = new SolidBrush(Color.FromArgb(120, 10, 15, 25));
        g.FillRectangle(bgBrush, tapeLeft, tapeY, tapeRight - tapeLeft, tapeH);
        using var borderPen = new Pen(Color.FromArgb(40, 0, 212, 255), 1);
        g.DrawRectangle(borderPen, tapeLeft, tapeY, tapeRight - tapeLeft, tapeH);

        // Ticks
        using var tickPen = new Pen(Color.FromArgb(80, 0, 212, 255), 1);
        using var labelFont = new Font("Cascadia Code", 7.5f);
        float cx = (tapeLeft + tapeRight) / 2f;
        float pixPerDeg = (tapeRight - tapeLeft) / 70f;

        for (int d = (int)yaw - 35; d <= (int)yaw + 35; d++)
        {
            int deg = ((d % 360) + 360) % 360;
            float x = cx + (d - yaw) * pixPerDeg;
            if (x < tapeLeft || x > tapeRight) continue;

            int tickLen = deg % 10 == 0 ? 10 : 5;
            g.DrawLine(tickPen, x, tapeY + tapeH, x, tapeY + tapeH + tickLen);

            if (deg % 30 == 0)
            {
                string label = deg switch
                {
                    0 => "N", 45 => "NE", 90 => "E", 135 => "SE",
                    180 => "S", 225 => "SW", 270 => "W", 315 => "NW",
                    _ => $"{deg}"
                };
                var size = g.MeasureString(label, labelFont);
                using var lb = new SolidBrush(ModernTheme.TextSecondary);
                g.DrawString(label, labelFont, lb, x - size.Width / 2, tapeY + 5);
            }
        }

        // Center marker with glow
        using var markerBrush = new SolidBrush(Color.FromArgb(0, 212, 255));
        g.FillPolygon(markerBrush, new PointF[] {
            new(cx, tapeY + tapeH),
            new(cx - 5, tapeY + tapeH + 8),
            new(cx + 5, tapeY + tapeH + 8)
        });

        // Heading value
        using var valFont = new Font("Cascadia Code", 10f, FontStyle.Bold);
        string hdgText = $"{((int)yaw + 360) % 360:000}";
        var size2 = g.MeasureString(hdgText, valFont);
        using var vb = new SolidBrush(Color.FromArgb(0, 212, 255));
        g.DrawString(hdgText, valFont, vb, cx - size2.Width / 2, tapeY + tapeH + 10);
    }

    private void DrawAircraftSymbol(Graphics g, int w, int h)
    {
        int cx = w / 2, cy = h / 2;

        // Glow effect
        using var glowPen = new Pen(Color.FromArgb(60, 0, 212, 255), 8);
        g.DrawLine(glowPen, cx - 35, cy, cx - 10, cy);
        g.DrawLine(glowPen, cx + 10, cy, cx + 35, cy);

        // Main symbol
        using var pen = new Pen(Color.FromArgb(0, 212, 255), 2);
        g.DrawLine(pen, cx - 35, cy, cx - 10, cy);
        g.DrawLine(pen, cx + 10, cy, cx + 35, cy);
        g.DrawLine(pen, cx - 10, cy, cx + 10, cy);

        // Center dot with glow
        using var glowBrush = new SolidBrush(Color.FromArgb(80, 0, 212, 255));
        g.FillEllipse(glowBrush, cx - 6, cy - 6, 12, 12);
        using var dotBrush = new SolidBrush(Color.FromArgb(0, 212, 255));
        g.FillEllipse(dotBrush, cx - 3, cy - 3, 6, 6);
    }

    private void DrawStatusOverlay(Graphics g, int w, int h)
    {
        if (_state == null) return;

        using var font = new Font("Cascadia Code", 9f, FontStyle.Bold);
        using var smallFont = new Font("Segoe UI", 8f);

        // Mode (bottom center)
        string mode = _state.FlightModeName;
        var modeSize = g.MeasureString(mode, font);
        using var modeBrush = new SolidBrush(Color.FromArgb(0, 212, 255));
        g.DrawString(mode, font, modeBrush, w / 2 - modeSize.Width / 2, h - 35);

        // Armed status
        string armed = _state.IsArmed ? "ARMED" : "DISARMED";
        var armedColor = _state.IsArmed ? new SolidBrush(Color.FromArgb(255, 51, 102)) :
                                                new SolidBrush(Color.FromArgb(0, 255, 136));
        var armedSize = g.MeasureString(armed, font);
        g.DrawString(armed, font, armedColor, w / 2 - armedSize.Width / 2, h - 55);

        // Battery (top right)
        string batt = $"BAT {_state.BatteryVoltage:F1}V {_state.BatteryRemaining}%";
        var battColor = _state.BatteryRemaining > 50 ? new SolidBrush(Color.FromArgb(0, 255, 136)) :
                        _state.BatteryRemaining > 20 ? new SolidBrush(Color.FromArgb(255, 184, 0)) :
                        new SolidBrush(Color.FromArgb(255, 51, 102));
        g.DrawString(batt, smallFont, battColor, w - 155, 15);

        // GPS (top left)
        string gps = $"GPS Fix:{_state.GpsFix} Sats:{_state.SatelliteCount}";
        var gpsColor = _state.GpsFix >= 3 ? new SolidBrush(Color.FromArgb(0, 255, 136)) :
                       _state.GpsFix >= 1 ? new SolidBrush(Color.FromArgb(255, 184, 0)) :
                       new SolidBrush(Color.FromArgb(255, 51, 102));
        g.DrawString(gps, smallFont, gpsColor, 10, 15);

        // EKF (below GPS)
        string ekf = _state.IsEkfHealthy ? "EKF OK" : "EKF BAD";
        var ekfColor = _state.IsEkfHealthy ? new SolidBrush(Color.FromArgb(0, 255, 136)) :
                                                new SolidBrush(Color.FromArgb(255, 51, 102));
        g.DrawString(ekf, smallFont, ekfColor, 10, 33);
    }
}
