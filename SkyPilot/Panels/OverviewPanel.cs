using System.Drawing.Drawing2D;
using SkyPilot.Core.Mavlink;
using SkyPilot.Log;
using SkyPilot.Utils;

namespace SkyPilot.Panels;

/// <summary>
/// Modern overview panel with stat cards showing key vehicle status.
/// </summary>
public class OverviewPanel : UserControl
{
    private VehicleState? _state;
    private readonly System.Windows.Forms.Timer _redrawTimer;

    // Cached values for smooth rendering
    private string _battText = "--";
    private Color _battColor = ModernTheme.TextMuted;
    private string _gpsText = "--";
    private Color _gpsColor = ModernTheme.TextMuted;
    private string _vibText = "--";
    private Color _vibColor = ModernTheme.TextMuted;
    private string _ekfText = "--";
    private Color _ekfColor = ModernTheme.TextMuted;
    private string _modeText = "--";
    private string _altText = "--";
    private string _speedText = "--";
    private string _posText = "--";

    public OverviewPanel()
    {
        Dock = DockStyle.Fill;
        BackColor = ModernTheme.Background;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

        _redrawTimer = new System.Windows.Forms.Timer { Interval = 200 };
        _redrawTimer.Tick += (s, e) => Invalidate();
        _redrawTimer.Start();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        int w = Width, h = Height;
        int cardW = 260, cardH = 90, gap = 12;
        int startX = 20, startY = 20;

        // Row 1: Battery, GPS, Vibration, EKF
        ModernTheme.DrawStatCard(g, new Rectangle(startX, startY, cardW, cardH),
            "BATTERY", _battText, _battColor, _battColor);
        ModernTheme.DrawStatCard(g, new Rectangle(startX + cardW + gap, startY, cardW, cardH),
            "GPS", _gpsText, _gpsColor, _gpsColor);
        ModernTheme.DrawStatCard(g, new Rectangle(startX + (cardW + gap) * 2, startY, cardW, cardH),
            "VIBRATION", _vibText, _vibColor, _vibColor);
        ModernTheme.DrawStatCard(g, new Rectangle(startX + (cardW + gap) * 3, startY, cardW, cardH),
            "EKF", _ekfText, _ekfColor, _ekfColor);

        // Row 2: Mode, Altitude, Speed, Position
        int y2 = startY + cardH + gap;
        ModernTheme.DrawStatCard(g, new Rectangle(startX, y2, cardW, cardH),
            "MODE", _modeText, ModernTheme.Accent, ModernTheme.Accent);
        ModernTheme.DrawStatCard(g, new Rectangle(startX + cardW + gap, y2, cardW, cardH),
            "ALTITUDE", _altText, ModernTheme.Info, ModernTheme.Info);
        ModernTheme.DrawStatCard(g, new Rectangle(startX + (cardW + gap) * 2, y2, cardW, cardH),
            "SPEED", _speedText, ModernTheme.Success, ModernTheme.Success);
        ModernTheme.DrawStatCard(g, new Rectangle(startX + (cardW + gap) * 3, y2, cardW, cardH),
            "POSITION", _posText, ModernTheme.TextPrimary, ModernTheme.Accent);

        // Section title
        using var titleBrush = new SolidBrush(ModernTheme.TextMuted);
        g.DrawString("FLIGHT OVERVIEW", new Font("Segoe UI", 8f, FontStyle.Bold), titleBrush, startX, startY - 2);
    }

    public void UpdateFromState(VehicleState state)
    {
        _state = state;

        _battText = $"{state.BatteryVoltage:F1}V  {state.BatteryRemaining}%";
        _battColor = state.BatteryRemaining > 50 ? ModernTheme.Success :
                     state.BatteryRemaining > 20 ? ModernTheme.Warning : ModernTheme.Danger;

        _gpsText = $"Fix {state.GpsFix}  |  {state.SatelliteCount} sats";
        _gpsColor = state.GpsFix >= 3 ? ModernTheme.Success :
                    state.GpsFix >= 1 ? ModernTheme.Warning : ModernTheme.Danger;

        float vibMax = state.MaxVibration;
        _vibText = $"{vibMax:F0}";
        _vibColor = vibMax < 30 ? ModernTheme.Success :
                    vibMax < 60 ? ModernTheme.Warning : ModernTheme.Danger;

        float ekfMax = Math.Max(state.EkfVelVariance, Math.Max(state.EkfPosHorizVariance,
            Math.Max(state.EkfPosVertVariance, Math.Max(state.EkfCompassVariance, state.EkfTerrainAltVariance))));
        _ekfText = $"{ekfMax:F2}";
        _ekfColor = ekfMax < 0.5f ? ModernTheme.Success :
                    ekfMax < 0.8f ? ModernTheme.Warning : ModernTheme.Danger;

        _modeText = state.FlightModeName;
        _altText = $"{state.AltitudeRel:F1}m";
        _speedText = $"{state.GroundSpeed:F1}m/s";
        _posText = $"{state.Latitude:F6}\n{state.Longitude:F6}";
    }

    public void UpdateFromLogMessage(LogMessage msg)
    {
        if (msg.Fields.TryGetValue("VibeX", out var vx))
            _vibText = $"X:{vx:F0} Y:{msg.GetFloat("VibeY"):F0} Z:{msg.GetFloat("VibeZ"):F0}";
        if (msg.Fields.TryGetValue("PressAbs", out var pa))
            _battText = $"{pa:F1} hPa";
    }
}
