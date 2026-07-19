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

    public void UpdatePosition(double lat, double lon, float heading)
    {
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
