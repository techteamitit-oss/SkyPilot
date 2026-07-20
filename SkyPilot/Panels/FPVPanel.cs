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
    private float _battery;
    private float _verticalSpeed;
    private double _latitude, _longitude;
    private bool _armed;
    private DateTime _armTime = DateTime.MinValue;
    private TimeSpan _flightTime;
    private readonly MiniMapControl _miniMap;
    private bool _recording;
    private string? _recordDir;
    private int _frameCount;
    private System.Windows.Forms.Timer? _captureTimer;
    private Button _recBtn;
    private int _rainIntensity; // 0=off, 1=light, 2=medium, 3=heavy
    private readonly Random _rainRng = new();
    private readonly List<(int X, int Y, int Speed)> _raindrops = new();

    public bool IsRecording => _recording;

    public event Action<string>? RecordingSaved;

    public FPVPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.FromArgb(13, 17, 23);

        _miniMap = new MiniMapControl
        {
            Size = new Size(160, 160),
            Location = new Point(10, 10),
            Anchor = AnchorStyles.Top | AnchorStyles.Left
        };
        Controls.Add(_miniMap);

        _recBtn = new Button
        {
            Text = "REC",
            Size = new Size(60, 28),
            Location = new Point(10, 175),
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
            BackColor = Color.FromArgb(200, 40, 40, 40),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _recBtn.FlatAppearance.BorderSize = 0;
        _recBtn.Click += (s, e) => ToggleRecording();
        Controls.Add(_recBtn);
    }

    public void UpdateAttitude(float pitch, float roll, float heading, float altitude, float speed, float throttle, float battery, float verticalSpeed, bool armed)
    {
        _pitch = pitch;
        _roll = roll;
        _heading = heading;
        _altitude = altitude;
        _speed = speed;
        _throttle = throttle;
        _battery = battery;
        _verticalSpeed = verticalSpeed;

        if (armed && !_armed)
            _armTime = DateTime.UtcNow;
        else if (!armed)
            _armTime = DateTime.MinValue;

        _armed = armed;
        _flightTime = _armed && _armTime != DateTime.MinValue ? DateTime.UtcNow - _armTime : TimeSpan.Zero;

        Invalidate();
    }

    public void UpdatePosition(double lat, double lon, float heading)
    {
        _latitude = lat;
        _longitude = lon;
        _miniMap.UpdatePosition(lat, lon, heading);
    }

    public void SetRain(int intensity)
    {
        _rainIntensity = intensity;
        _raindrops.Clear();
        if (intensity > 0)
        {
            int count = intensity switch { 1 => 60, 2 => 150, 3 => 300, _ => 0 };
            for (int i = 0; i < count; i++)
                _raindrops.Add((_rainRng.Next(0, 800), _rainRng.Next(0, 600), 4 + _rainRng.Next(4)));
        }
    }

    private void ToggleRecording()
    {
        if (_recording)
        {
            StopRecording();
        }
        else
        {
            StartRecording();
        }
    }

    private void StartRecording()
    {
        _recordDir = Path.Combine(Path.GetTempPath(), "SkyPilot_Recording_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        Directory.CreateDirectory(_recordDir);
        _frameCount = 0;
        _recording = true;
        _recBtn.Text = "STOP";
        _recBtn.BackColor = Color.FromArgb(220, 200, 30, 30);

        _captureTimer = new System.Windows.Forms.Timer { Interval = 33 }; // ~30fps
        _captureTimer.Tick += (s, e) => CaptureFrame();
        _captureTimer.Start();
    }

    private void StopRecording()
    {
        _recording = false;
        _captureTimer?.Stop();
        _captureTimer?.Dispose();
        _captureTimer = null;
        _recBtn.Text = "REC";
        _recBtn.BackColor = Color.FromArgb(200, 40, 40, 40);

        if (_recordDir == null) return;

        int frames = _frameCount;
        string outputDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "SkyPilot");
        Directory.CreateDirectory(outputDir);
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        // Try to create MP4 with ffmpeg
        string ffmpegPath = FindFfmpeg();
        if (ffmpegPath != null && frames > 0)
        {
            string mp4Path = Path.Combine(outputDir, $"SkyPilot_FPV_{timestamp}.mp4");
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = $"-y -framerate 30 -i \"{_recordDir}/frame_%05d.png\" -c:v libx264 -pix_fmt yuv420p \"{mp4Path}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true
                };
                var proc = System.Diagnostics.Process.Start(psi);
                proc?.WaitForExit(30000);

                if (File.Exists(mp4Path))
                {
                    RecordingSaved?.Invoke(mp4Path);
                    // Clean up frames
                    try { Directory.Delete(_recordDir, true); } catch { }
                    _recordDir = null;
                    return;
                }
            }
            catch { }
        }

        // Fallback: keep PNG frames
        string framesDir = Path.Combine(outputDir, $"SkyPilot_FPV_{timestamp}_frames");
        try
        {
            if (Directory.Exists(framesDir)) Directory.Delete(framesDir, true);
            Directory.Move(_recordDir, framesDir);
            RecordingSaved?.Invoke(framesDir);
        }
        catch
        {
            RecordingSaved?.Invoke(_recordDir);
        }
        _recordDir = null;
    }

    private void CaptureFrame()
    {
        if (_recordDir == null || !Visible) return;
        try
        {
            var bmp = new Bitmap(Width, Height);
            DrawToBitmap(bmp, new Rectangle(0, 0, Width, Height));
            _frameCount++;
            bmp.Save(Path.Combine(_recordDir, $"frame_{_frameCount:D5}.png"), System.Drawing.Imaging.ImageFormat.Png);
            bmp.Dispose();
        }
        catch { }
    }

    private static string FindFfmpeg()
    {
        // Check common locations
        string[] paths = { "ffmpeg", @"C:\ffmpeg\bin\ffmpeg.exe", @"C:\Program Files\ffmpeg\bin\ffmpeg.exe" };
        foreach (var p in paths)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = p,
                    Arguments = "-version",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                };
                var proc = System.Diagnostics.Process.Start(psi);
                proc?.WaitForExit(2000);
                if (proc != null && proc.ExitCode == 0) return p;
            }
            catch { }
        }
        return null;
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

        // Ground (brown/earth)
        using var groundBrush = new SolidBrush(Color.FromArgb(255, 140, 100, 50));
        g.FillRectangle(groundBrush, -w, pitchOffset, w * 2, h);

        // Sky (blue)
        using var skyBrush = new SolidBrush(Color.FromArgb(255, 70, 130, 220));
        g.FillRectangle(skyBrush, -w, -h + pitchOffset, w * 2, h);

        // Horizon line
        using var horizonPen = new Pen(Color.FromArgb(200, 255, 255, 255), 2);
        g.DrawLine(horizonPen, -w, pitchOffset, w, pitchOffset);

        // Pitch ladder lines
        using var pitchPen = new Pen(Color.FromArgb(200, 255, 255, 255), 1);
        using var pitchFont = new Font("Cascadia Code", 8f);
        using var pitchBrush = new SolidBrush(Color.FromArgb(220, 255, 255, 255));
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
        DrawAltitudeTape(g, w - 80, cy, h);

        // === SPEED TAPE (left side) ===
        DrawSpeedTape(g, 10, cy, h);

        // === CENTER RETICLE ===
        using var reticlePen = new Pen(Color.FromArgb(150, 0, 212, 255), 2);
        g.DrawLine(reticlePen, cx - 30, cy, cx - 10, cy);
        g.DrawLine(reticlePen, cx + 10, cy, cx + 30, cy);
        g.DrawLine(reticlePen, cx, cy - 10, cx, cy - 6);

        // === THROTTLE BAR (bottom left) ===
        DrawThrottleBar(g, 10, h - 30, 80, 12);

        // === BATTERY INDICATOR (bottom left, below throttle) ===
        DrawBatteryIndicator(g, 10, h - 55, 80, 12);

        // === COMPASS ROSE (bottom right) ===
        DrawCompassRose(g, w - 70, h - 80);

        // === VSI (right side, next to altitude tape) ===
        DrawVSI(g, w - 100, cy, h);

        // === GPS COORDINATES (bottom center) ===
        DrawGPSCoords(g, cx, h - 30);

        // === FLIGHT TIMER (top left, below minimap + rec btn) ===
        DrawFlightTimer(g, 10, 210);

        // === BORDER ===
        using var borderPen = new Pen(Color.FromArgb(60, 0, 212, 255), 1);
        g.DrawRectangle(borderPen, 0, 0, w - 1, h - 1);
    }

    private void DrawHeadingTape(Graphics g, int cx, int y, int w)
    {
        int tapeW = Math.Min(320, w - 20);
        int tapeH = 30;
        int tapeX = cx - tapeW / 2;

        // Background
        using var bgBrush = new SolidBrush(Color.FromArgb(160, 13, 17, 23));
        g.FillRectangle(bgBrush, tapeX, y, tapeW, tapeH);
        using var borderPen = new Pen(Color.FromArgb(100, 0, 212, 255), 1);
        g.DrawRectangle(borderPen, tapeX, y, tapeW, tapeH);

        using var markFont = new Font("Cascadia Code", 8f);
        using var cardinalFont = new Font("Segoe UI", 9f, FontStyle.Bold);
        using var markBrush = new SolidBrush(ModernTheme.TextMuted);
        using var markPen = new Pen(Color.FromArgb(120, 0, 212, 255), 1);

        var cardinalDirs = new Dictionary<int, (string Label, Color Color)>
        {
            {0, ("N", Color.FromArgb(255, 255, 60, 60))},
            {90, ("E", ModernTheme.Accent)},
            {180, ("S", Color.FromArgb(255, 255, 255, 255))},
            {270, ("W", ModernTheme.Accent)}
        };

        float pxPerDeg = tapeW / 60f;
        for (int d = -30; d <= 30; d += 5)
        {
            float hdg = (_heading + d + 360) % 360;
            int x = cx + (int)(d * pxPerDeg);
            if (x < tapeX + 5 || x > tapeX + tapeW - 5) continue;

            bool isMajor = d % 10 == 0;
            g.DrawLine(markPen, x, y + tapeH - (isMajor ? 10 : 5), x, y + tapeH);

            int normHdg = (int)hdg % 360;
            if (cardinalDirs.TryGetValue(normHdg, out var dir))
            {
                var sz = g.MeasureString(dir.Label, cardinalFont);
                g.DrawString(dir.Label, cardinalFont, new SolidBrush(dir.Color), x - sz.Width / 2, y + 2);
            }
            else if (isMajor)
            {
                g.DrawString(normHdg.ToString("F0"), markFont, markBrush, x - 8, y + 4);
            }
        }

        // Center pointer (larger triangle)
        using var ptrBrush = new SolidBrush(ModernTheme.Accent);
        g.FillPolygon(ptrBrush, new Point[] {
            new(cx - 6, y + tapeH), new(cx + 6, y + tapeH), new(cx, y + tapeH + 8)
        });
        // Top pointer
        g.FillPolygon(ptrBrush, new Point[] {
            new(cx - 4, y), new(cx + 4, y), new(cx, y - 6)
        });

        // Heading readout below tape
        using var hdgFont = new Font("Cascadia Code", 10f, FontStyle.Bold);
        using var hdgBrush = new SolidBrush(ModernTheme.Accent);
        string hdgText = $"{_heading:F0}\u00B0";
        var hdgSize = g.MeasureString(hdgText, hdgFont);
        g.DrawString(hdgText, hdgFont, hdgBrush, cx - hdgSize.Width / 2, y + tapeH + 10);
    }

    private void DrawAltitudeTape(Graphics g, int x, int cy, int h)
    {
        int tapeW = 65, tapeH = Math.Min(240, h - 40);
        int tapeY = cy - tapeH / 2;

        // Background
        using var bgBrush = new SolidBrush(Color.FromArgb(160, 13, 17, 23));
        g.FillRectangle(bgBrush, x, tapeY, tapeW, tapeH);
        using var borderPen = new Pen(Color.FromArgb(100, 0, 212, 255), 1);
        g.DrawRectangle(borderPen, x, tapeY, tapeW, tapeH);

        // Label
        using var lblFont = new Font("Segoe UI", 7f, FontStyle.Bold);
        using var lblBrush = new SolidBrush(ModernTheme.TextMuted);
        g.DrawString("ALT m", lblFont, lblBrush, x + 4, tapeY - 14);

        using var markFont = new Font("Cascadia Code", 8f);
        using var markBrush = new SolidBrush(ModernTheme.TextMuted);
        using var markPen = new Pen(Color.FromArgb(120, 0, 212, 255), 1);

        float pxPerM = tapeH / 100f;
        for (int d = -50; d <= 50; d += 10)
        {
            float alt = _altitude + d;
            if (alt < 0) continue;
            int y = cy - (int)(d * pxPerM);
            if (y < tapeY + 5 || y > tapeY + tapeH - 5) continue;
            g.DrawLine(markPen, x, y, x + 10, y);
            g.DrawString(alt.ToString("F0"), markFont, markBrush, x + 12, y - 6);
        }

        // Current value box — bigger and brighter
        int boxY = cy - 14;
        using var boxBrush = new SolidBrush(Color.FromArgb(220, 0, 180, 255));
        g.FillRectangle(boxBrush, x, boxY, tapeW, 28);
        using var valFont = new Font("Cascadia Code", 14f, FontStyle.Bold);
        g.DrawString($"{_altitude:F0}", valFont, new SolidBrush(Color.White), x + 6, boxY + 3);

        // Arrow pointers
        using var arrowBrush = new SolidBrush(ModernTheme.Accent);
        g.FillPolygon(arrowBrush, new Point[] {
            new(x, boxY), new(x - 6, boxY + 14), new(x, boxY + 28)
        });
        g.FillPolygon(arrowBrush, new Point[] {
            new(x + tapeW, boxY), new(x + tapeW + 6, boxY + 14), new(x + tapeW, boxY + 28)
        });
    }

    private void DrawSpeedTape(Graphics g, int x, int cy, int h)
    {
        int tapeW = 65, tapeH = Math.Min(240, h - 40);
        int tapeY = cy - tapeH / 2;

        // Background
        using var bgBrush = new SolidBrush(Color.FromArgb(160, 13, 17, 23));
        g.FillRectangle(bgBrush, x, tapeY, tapeW, tapeH);
        using var borderPen = new Pen(Color.FromArgb(100, 0, 212, 255), 1);
        g.DrawRectangle(borderPen, x, tapeY, tapeW, tapeH);

        // Label
        using var lblFont = new Font("Segoe UI", 7f, FontStyle.Bold);
        using var lblBrush = new SolidBrush(ModernTheme.TextMuted);
        g.DrawString("SPD m/s", lblFont, lblBrush, x + 4, tapeY - 14);

        using var markFont = new Font("Cascadia Code", 8f);
        using var markBrush = new SolidBrush(ModernTheme.TextMuted);
        using var markPen = new Pen(Color.FromArgb(120, 0, 212, 255), 1);

        float pxPerMs = tapeH / 40f;
        for (int d = -20; d <= 20; d += 5)
        {
            float spd = _speed + d;
            if (spd < 0) continue;
            int y = cy - (int)(d * pxPerMs);
            if (y < tapeY + 5 || y > tapeY + tapeH - 5) continue;
            int lineX = x + tapeW;
            g.DrawLine(markPen, lineX - 10, y, lineX, y);
            g.DrawString(spd.ToString("F0"), markFont, markBrush, x + 2, y - 6);
        }

        // Current value box
        int boxY = cy - 14;
        Color spdColor = _speed > 30 ? ModernTheme.Warning : Color.FromArgb(220, 0, 200, 100);
        using var boxBrush = new SolidBrush(spdColor);
        g.FillRectangle(boxBrush, x, boxY, tapeW, 28);
        using var valFont = new Font("Cascadia Code", 14f, FontStyle.Bold);
        g.DrawString($"{_speed:F0}", valFont, new SolidBrush(Color.White), x + 6, boxY + 3);

        // Arrow pointers
        using var arrowBrush = new SolidBrush(spdColor);
        g.FillPolygon(arrowBrush, new Point[] {
            new(x, boxY), new(x - 6, boxY + 14), new(x, boxY + 28)
        });
        g.FillPolygon(arrowBrush, new Point[] {
            new(x + tapeW, boxY), new(x + tapeW + 6, boxY + 14), new(x + tapeW, boxY + 28)
        });
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

    private void DrawBatteryIndicator(Graphics g, int x, int y, int w, int h)
    {
        using var bgBrush = new SolidBrush(Color.FromArgb(140, 13, 17, 23));
        g.FillRectangle(bgBrush, x, y, w, h);
        using var borderPen = new Pen(Color.FromArgb(80, 0, 212, 255), 1);
        g.DrawRectangle(borderPen, x, y, w, h);

        float fill = Math.Clamp(_battery, 0, 100) / 100f;
        Color barColor = fill > 0.5f ? ModernTheme.Success :
                         fill > 0.2f ? ModernTheme.Warning : ModernTheme.Danger;
        using var barBrush = new SolidBrush(barColor);
        g.FillRectangle(barBrush, x + 1, y + 1, (int)((w - 2) * fill), h - 2);

        using var font = new Font("Cascadia Code", 7f);
        using var brush = new SolidBrush(ModernTheme.TextPrimary);
        g.DrawString($"BAT {_battery:F0}%", font, brush, x + 2, y - 12);
    }

    private void DrawCompassRose(Graphics g, int cx, int cy)
    {
        int radius = 50;

        // Semi-transparent background circle
        using var bgBrush = new SolidBrush(Color.FromArgb(100, 13, 17, 23));
        g.FillEllipse(bgBrush, cx - radius - 5, cy - radius - 5, (radius + 5) * 2, (radius + 5) * 2);
        using var ringPen = new Pen(Color.FromArgb(100, 0, 212, 255), 1);
        g.DrawEllipse(ringPen, cx - radius - 5, cy - radius - 5, (radius + 5) * 2, (radius + 5) * 2);

        g.TranslateTransform(cx, cy);
        g.RotateTransform(-_heading);

        using var tickPen = new Pen(Color.FromArgb(120, 255, 255, 255), 1);
        using var majorPen = new Pen(Color.FromArgb(200, 255, 255, 255), 2);
        using var dirFont = new Font("Segoe UI", 9f, FontStyle.Bold);
        using var degFont = new Font("Cascadia Code", 6f);

        // Cardinal + intercardinal + degree marks
        var directions = new Dictionary<int, string> { {0,"N"}, {45,"NE"}, {90,"E"}, {135,"SE"}, {180,"S"}, {225,"SW"}, {270,"W"}, {315,"NW"} };

        for (int deg = 0; deg < 360; deg += 5)
        {
            double rad = (deg - 90) * Math.PI / 180.0;
            bool isMajor = deg % 30 == 0;
            bool isCardinal = deg % 90 == 0;
            bool isIntercardinal = deg % 45 == 0 && !isCardinal;

            int innerR = isCardinal ? radius - 14 : isIntercardinal ? radius - 10 : isMajor ? radius - 8 : radius - 4;
            int x1 = (int)(Math.Cos(rad) * innerR);
            int y1 = (int)(Math.Sin(rad) * innerR);
            int x2 = (int)(Math.Cos(rad) * radius);
            int y2 = (int)(Math.Sin(rad) * radius);

            g.DrawLine(isCardinal ? majorPen : tickPen, x1, y1, x2, y2);

            // Labels for cardinal directions
            if (directions.TryGetValue(deg, out var label))
            {
                int labelR = radius - 24;
                int lx = (int)(Math.Cos(rad) * labelR) - 6;
                int ly = (int)(Math.Sin(rad) * labelR) - 6;
                Color dirColor = deg == 0 ? Color.FromArgb(255, 255, 60, 60) : // N = red
                                 deg == 180 ? Color.FromArgb(255, 255, 255, 255) : // S = white
                                 ModernTheme.Accent;
                using var dirBrush = new SolidBrush(dirColor);
                g.DrawString(label, dirFont, dirBrush, lx, ly);
            }
        }

        g.ResetTransform();

        // Fixed lubber line (triangle at top)
        using var lubberBrush = new SolidBrush(ModernTheme.Accent);
        g.FillPolygon(lubberBrush, new Point[] {
            new(cx - 4, cy - radius - 8),
            new(cx + 4, cy - radius - 8),
            new(cx, cy - radius + 2)
        });

        // Heading readout
        using var hdgFont = new Font("Cascadia Code", 8f, FontStyle.Bold);
        using var hdgBrush = new SolidBrush(ModernTheme.Accent);
        string hdgText = $"{_heading:F0}\u00B0";
        var hdgSize = g.MeasureString(hdgText, hdgFont);
        g.DrawString(hdgText, hdgFont, hdgBrush, cx - hdgSize.Width / 2, cy + radius + 4);
    }

    private void DrawVSI(Graphics g, int x, int cy, int h)
    {
        int tapeW = 18, tapeH = Math.Min(200, h - 60);
        int tapeY = cy - tapeH / 2;

        // Background
        using var bgBrush = new SolidBrush(Color.FromArgb(140, 13, 17, 23));
        g.FillRectangle(bgBrush, x, tapeY, tapeW, tapeH);
        using var borderPen = new Pen(Color.FromArgb(80, 0, 212, 255), 1);
        g.DrawRectangle(borderPen, x, tapeY, tapeW, tapeH);

        // Label
        using var lblFont = new Font("Segoe UI", 6f, FontStyle.Bold);
        using var lblBrush = new SolidBrush(ModernTheme.TextMuted);
        g.DrawString("VS", lblFont, lblBrush, x + 2, tapeY - 12);

        // Scale: -10 to +10 m/s, center = 0
        float maxVS = 10f;
        float pxPerMs = tapeH / (maxVS * 2);

        // Tick marks
        using var tickPen = new Pen(Color.FromArgb(100, 0, 212, 255), 1);
        using var tickFont = new Font("Cascadia Code", 6f);
        using var tickBrush = new SolidBrush(ModernTheme.TextMuted);
        for (int d = -10; d <= 10; d += 5)
        {
            int y = cy - (int)(d * pxPerMs);
            if (y < tapeY + 3 || y > tapeY + tapeH - 3) continue;
            g.DrawLine(tickPen, x, y, x + tapeW, y);
            if (d != 0)
                g.DrawString(d > 0 ? $"+{d}" : d.ToString(), tickFont, tickBrush, x + tapeW + 2, y - 4);
        }

        // Zero line (thicker)
        using var zeroPen = new Pen(Color.FromArgb(180, 255, 255, 255), 2);
        g.DrawLine(zeroPen, x, cy, x + tapeW, cy);

        // Current VS indicator — colored triangle
        float clampedVS = Math.Clamp(_verticalSpeed, -maxVS, maxVS);
        int indY = cy - (int)(clampedVS * pxPerMs);
        Color vsColor = _verticalSpeed > 0.5f ? ModernTheme.Success :
                        _verticalSpeed < -0.5f ? ModernTheme.Danger : ModernTheme.Accent;
        using var indBrush = new SolidBrush(vsColor);
        g.FillPolygon(indBrush, new Point[] {
            new(x, indY - 4), new(x, indY + 4), new(x - 6, indY)
        });
        g.FillPolygon(indBrush, new Point[] {
            new(x + tapeW, indY - 4), new(x + tapeW, indY + 4), new(x + tapeW + 6, indY)
        });

        // VS readout
        using var vsFont = new Font("Cascadia Code", 8f, FontStyle.Bold);
        using var vsBrush = new SolidBrush(vsColor);
        string vsText = $"{_verticalSpeed:+0.0;-0.0;0.0} m/s";
        var vsSize = g.MeasureString(vsText, vsFont);
        g.DrawString(vsText, vsFont, vsBrush, x - vsSize.Width / 2 + tapeW / 2, tapeY + tapeH + 4);
    }

    private void DrawGPSCoords(Graphics g, int cx, int y)
    {
        if (_latitude == 0 && _longitude == 0) return;

        string latDir = _latitude >= 0 ? "N" : "S";
        string lonDir = _longitude >= 0 ? "E" : "W";
        string latText = $"{Math.Abs(_latitude):F7}\u00B0 {latDir}";
        string lonText = $"{Math.Abs(_longitude):F7}\u00B0 {lonDir}";

        using var bgBrush = new SolidBrush(Color.FromArgb(140, 13, 17, 23));
        using var font = new Font("Cascadia Code", 8f);
        using var brush = new SolidBrush(ModernTheme.TextMuted);
        using var borderPen = new Pen(Color.FromArgb(60, 0, 212, 255), 1);

        var latSize = g.MeasureString(latText, font);
        var lonSize = g.MeasureString(lonText, font);
        float totalW = Math.Max(latSize.Width, lonSize.Width) + 16;
        float boxH = 34;
        int boxX = cx - (int)(totalW / 2);

        g.FillRectangle(bgBrush, boxX, y - 2, totalW, boxH);
        g.DrawRectangle(borderPen, boxX, y - 2, totalW, boxH);

        // Label
        using var lblFont = new Font("Segoe UI", 6f, FontStyle.Bold);
        using var lblBrush = new SolidBrush(ModernTheme.Accent);
        g.DrawString("GPS", lblFont, lblBrush, boxX + 3, y);

        g.DrawString(latText, font, brush, boxX + 8, y + 2);
        g.DrawString(lonText, font, brush, boxX + 8, y + 15);
    }

    private void DrawFlightTimer(Graphics g, int x, int y)
    {
        using var bgBrush = new SolidBrush(Color.FromArgb(140, 13, 17, 23));
        using var borderPen = new Pen(Color.FromArgb(60, 0, 212, 255), 1);

        string timeText = _flightTime.TotalHours >= 1
            ? _flightTime.ToString(@"hh\:mm\:ss")
            : _flightTime.ToString(@"mm\:ss");

        using var font = new Font("Cascadia Code", 11f, FontStyle.Bold);
        var sz = g.MeasureString(timeText, font);
        float boxW = sz.Width + 16;

        g.FillRectangle(bgBrush, x, y, boxW, 24);
        g.DrawRectangle(borderPen, x, y, boxW, 24);

        using var lblFont = new Font("Segoe UI", 6f, FontStyle.Bold);
        using var lblBrush = new SolidBrush(ModernTheme.Accent);
        g.DrawString("FLIGHT", lblFont, lblBrush, x + 3, y - 10);

        Color timerColor = _armed ? ModernTheme.Accent : ModernTheme.TextMuted;
        using var timerBrush = new SolidBrush(timerColor);
        g.DrawString(timeText, font, timerBrush, x + 8, y + 2);
    }
}
