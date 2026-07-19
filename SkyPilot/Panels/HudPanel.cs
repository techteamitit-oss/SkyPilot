using SkyPilot.Core.Mavlink;

namespace SkyPilot.Panels;

/// <summary>
/// Head-Up Display with artificial horizon, speed/altitude tapes, heading, and status.
/// </summary>
public class HudPanel : UserControl
{
    private VehicleState? _state;

    public HudPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = Color.Black;
        DoubleBuffered = true;
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
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        int w = Width, h = Height;
        float roll = _state.Roll;
        float pitch = _state.Pitch;
        float yaw = _state.Yaw;
        float speed = _state.GroundSpeed;
        float alt = _state.AltitudeRel;
        float vs = _state.VerticalSpeed;

        // --- Artificial Horizon ---
        DrawHorizon(g, w, h, roll, pitch);

        // --- Speed tape (left) ---
        DrawSpeedTape(g, w, h, speed);

        // --- Altitude tape (right) ---
        DrawAltTape(g, w, h, alt, vs);

        // --- Heading tape (top) ---
        DrawHeadingTape(g, w, h, yaw);

        // --- Center pip (aircraft symbol) ---
        DrawAircraftSymbol(g, w, h);

        // --- Status text ---
        DrawStatusText(g, w, h);
    }

    private void DrawHorizon(Graphics g, int w, int h, float roll, float pitch)
    {
        int cx = w / 2, cy = h / 2;

        using var horizonPath = new System.Drawing.Drawing2D.GraphicsPath();
        horizonPath.StartFigure();

        float pitchOffset = pitch * 3; // pixels per degree
        float groundY = cy + pitchOffset;

        // Sky rectangle (top half)
        var skyBrush = new SolidBrush(Color.FromArgb(40, 100, 180));
        var groundBrush = new SolidBrush(Color.FromArgb(120, 80, 40));

        g.TranslateTransform(cx, cy);
        g.RotateTransform(-roll);

        // Sky
        g.FillRectangle(skyBrush, -w, -h, w * 2, (int)(groundY + h));
        // Ground
        g.FillRectangle(groundBrush, -w, (int)groundY, w * 2, h * 2);

        // Horizon line
        using var pen = new Pen(Color.White, 2);
        g.DrawLine(pen, -w * 2, (int)groundY, w * 2, (int)groundY);

        // Pitch ladder
        using var pitchPen = new Pen(Color.White, 1);
        using var pitchFont = new Font("Consolas", 8f);
        for (int deg = -30; deg <= 30; deg += 5)
        {
            if (deg == 0) continue;
            float y = groundY - deg * 3;
            int lineW = deg % 10 == 0 ? 60 : 30;
            g.DrawLine(pitchPen, -lineW, y, lineW, y);
            if (deg % 10 == 0)
            {
                var text = $"{Math.Abs(deg)}";
                var size = g.MeasureString(text, pitchFont);
                g.DrawString(text, pitchFont, Brushes.White,
                    -lineW - size.Width - 4, y - size.Height / 2);
                g.DrawString(text, pitchFont, Brushes.White,
                    lineW + 4, y - size.Height / 2);
            }
        }

        g.ResetTransform();
    }

    private void DrawSpeedTape(Graphics g, int w, int h, float speed)
    {
        int tapeW = 80, tapeX = 10;
        int tapeTop = 50, tapeBot = h - 80;

        // Background
        using var bg = new SolidBrush(Color.FromArgb(180, 20, 20, 20));
        g.FillRectangle(bg, tapeX, tapeTop, tapeW, tapeBot - tapeTop);

        // Border
        using var borderPen = new Pen(Color.FromArgb(100, 100, 100), 1);
        g.DrawRectangle(borderPen, tapeX, tapeTop, tapeW, tapeBot - tapeTop);

        // Tick marks and labels
        using var tickPen = new Pen(Color.White, 1);
        using var labelFont = new Font("Consolas", 9f);
        float pixPerUnit = (tapeBot - tapeTop) / 20f; // 20 m/s range visible

        for (int s = (int)speed - 10; s <= (int)speed + 10; s++)
        {
            if (s < 0) continue;
            float y = (tapeBot - tapeTop) / 2f - (s - speed) * pixPerUnit + tapeTop;
            if (y < tapeTop || y > tapeBot) continue;

            int tickLen = s % 5 == 0 ? 15 : 8;
            g.DrawLine(tickPen, tapeX + tapeW - tickLen, y, tapeX + tapeW, y);
            if (s % 5 == 0)
                g.DrawString($"{s}", labelFont, Brushes.White, tapeX + 4, y - 6);
        }

        // Speed value box
        using var valFont = new Font("Consolas", 12f, FontStyle.Bold);
        string speedText = $"{speed:F0}";
        var size = g.MeasureString(speedText, valFont);
        float boxY = (tapeBot - tapeTop) / 2f + tapeTop - size.Height / 2;
        using var boxBrush = new SolidBrush(Color.FromArgb(200, 40, 40, 40));
        g.FillRectangle(boxBrush, tapeX, boxY, tapeW, size.Height + 4);
        g.DrawString(speedText, valFont, Brushes.LimeGreen, tapeX + (tapeW - size.Width) / 2, boxY + 2);

        // Label
        using var smallFont = new Font("Segoe UI", 8f);
        g.DrawString("GS m/s", smallFont, Brushes.LightGray, tapeX + 10, tapeBot + 4);
    }

    private void DrawAltTape(Graphics g, int w, int h, float alt, float vs)
    {
        int tapeW = 80, tapeX = w - 90;
        int tapeTop = 50, tapeBot = h - 80;

        // Background
        using var bg = new SolidBrush(Color.FromArgb(180, 20, 20, 20));
        g.FillRectangle(bg, tapeX, tapeTop, tapeW, tapeBot - tapeTop);

        // Border
        using var borderPen = new Pen(Color.FromArgb(100, 100, 100), 1);
        g.DrawRectangle(borderPen, tapeX, tapeTop, tapeW, tapeBot - tapeTop);

        // Tick marks
        using var tickPen = new Pen(Color.White, 1);
        using var labelFont = new Font("Consolas", 9f);
        float pixPerUnit = (tapeBot - tapeTop) / 40f; // 40m range visible

        for (int a = (int)alt - 20; a <= (int)alt + 20; a++)
        {
            float y = (tapeBot - tapeTop) / 2f - (a - alt) * pixPerUnit + tapeTop;
            if (y < tapeTop || y > tapeBot) continue;

            int tickLen = a % 10 == 0 ? 15 : 8;
            g.DrawLine(tickPen, tapeX, y, tapeX + tickLen, y);
            if (a % 10 == 0)
                g.DrawString($"{a}", labelFont, Brushes.White, tapeX + tapeW - 30, y - 6);
        }

        // Alt value box
        using var valFont = new Font("Consolas", 12f, FontStyle.Bold);
        string altText = $"{alt:F0}";
        var size = g.MeasureString(altText, valFont);
        float boxY = (tapeBot - tapeTop) / 2f + tapeTop - size.Height / 2;
        using var boxBrush = new SolidBrush(Color.FromArgb(200, 40, 40, 40));
        g.FillRectangle(boxBrush, tapeX, boxY, tapeW, size.Height + 4);
        g.DrawString(altText, valFont, Brushes.Cyan, tapeX + (tapeW - size.Width) / 2, boxY + 2);

        // VSI triangle
        float vsiY = (tapeBot - tapeTop) / 2f + tapeTop;
        float vsiOffset = Math.Clamp(vs * 3, -(tapeBot - tapeTop) / 2f, (tapeBot - tapeTop) / 2f);
        using var vsiPen = new Pen(Color.Yellow, 2);
        float triY = vsiY - vsiOffset;
        g.FillPolygon(Brushes.Yellow, new PointF[] {
            new(tapeX - 12, triY),
            new(tapeX - 2, triY - 5),
            new(tapeX - 2, triY + 5)
        });

        // Label
        using var smallFont = new Font("Segoe UI", 8f);
        g.DrawString("ALT m", smallFont, Brushes.LightGray, tapeX + 10, tapeBot + 4);
    }

    private void DrawHeadingTape(Graphics g, int w, int h, float yaw)
    {
        int tapeH = 30, tapeY = 10;
        int tapeLeft = 100, tapeRight = w - 100;

        // Background
        using var bg = new SolidBrush(Color.FromArgb(180, 20, 20, 20));
        g.FillRectangle(bg, tapeLeft, tapeY, tapeRight - tapeLeft, tapeH);

        // Border
        using var borderPen = new Pen(Color.FromArgb(100, 100, 100), 1);
        g.DrawRectangle(borderPen, tapeLeft, tapeY, tapeRight - tapeLeft, tapeH);

        // Tick marks
        using var tickPen = new Pen(Color.White, 1);
        using var labelFont = new Font("Consolas", 8f);
        float cx = (tapeLeft + tapeRight) / 2f;
        float pixPerDeg = (tapeRight - tapeLeft) / 60f; // 60 degrees visible

        for (int d = (int)yaw - 30; d <= (int)yaw + 30; d++)
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
                g.DrawString(label, labelFont, Brushes.White, x - size.Width / 2, tapeY + 4);
            }
        }

        // Center marker
        g.FillPolygon(Brushes.Yellow, new PointF[] {
            new(cx, tapeY + tapeH),
            new(cx - 5, tapeY + tapeH + 8),
            new(cx + 5, tapeY + tapeH + 8)
        });

        // Heading value
        using var valFont = new Font("Consolas", 10f, FontStyle.Bold);
        string hdgText = $"{((int)yaw + 360) % 360:000}";
        var size2 = g.MeasureString(hdgText, valFont);
        g.DrawString(hdgText, valFont, Brushes.Yellow, cx - size2.Width / 2, tapeY + tapeH + 10);
    }

    private void DrawAircraftSymbol(Graphics g, int w, int h)
    {
        int cx = w / 2, cy = h / 2;
        using var pen = new Pen(Color.Yellow, 2);

        // Wings
        g.DrawLine(pen, cx - 30, cy, cx - 8, cy);
        g.DrawLine(pen, cx + 8, cy, cx + 30, cy);
        // Fuselage
        g.DrawLine(pen, cx - 8, cy, cx + 8, cy);
        // Center dot
        g.FillEllipse(Brushes.Yellow, cx - 3, cy - 3, 6, 6);
    }

    private void DrawStatusText(Graphics g, int w, int h)
    {
        if (_state == null) return;

        using var font = new Font("Segoe UI", 10f, FontStyle.Bold);
        using var smallFont = new Font("Segoe UI", 9f);

        // Mode (bottom center)
        string mode = _state.FlightModeName;
        var modeSize = g.MeasureString(mode, font);
        g.DrawString(mode, font, Brushes.White, w / 2 - modeSize.Width / 2, h - 35);

        // Armed status
        string armed = _state.IsArmed ? "ARMED" : "DISARMED";
        var armedColor = _state.IsArmed ? Brushes.Red : Brushes.LimeGreen;
        var armedSize = g.MeasureString(armed, font);
        g.DrawString(armed, font, armedColor, w / 2 - armedSize.Width / 2, h - 55);

        // Battery (top right)
        string batt = $"BAT {_state.BatteryVoltage:F1}V {_state.BatteryRemaining}%";
        var battColor = _state.BatteryRemaining > 50 ? Brushes.LimeGreen :
                        _state.BatteryRemaining > 20 ? Brushes.Orange : Brushes.Red;
        g.DrawString(batt, smallFont, battColor, w - 160, 15);

        // GPS (top left)
        string gps = $"GPS Fix:{_state.GpsFix} Sats:{_state.SatelliteCount}";
        var gpsColor = _state.GpsFix >= 3 ? Brushes.LimeGreen :
                       _state.GpsFix >= 1 ? Brushes.Orange : Brushes.Red;
        g.DrawString(gps, smallFont, gpsColor, 10, 15);

        // EKF (below GPS)
        string ekf = _state.IsEkfHealthy ? "EKF OK" : "EKF BAD";
        var ekfColor = _state.IsEkfHealthy ? Brushes.LimeGreen : Brushes.Red;
        g.DrawString(ekf, smallFont, ekfColor, 10, 35);
    }
}
