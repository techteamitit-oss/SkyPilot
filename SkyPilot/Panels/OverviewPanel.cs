using System.Drawing.Drawing2D;
using SkyPilot.Core.Mavlink;
using SkyPilot.Log;
using SkyPilot.Utils;

namespace SkyPilot.Panels;

/// <summary>
/// Modern overview with circular gauges and stat cards.
/// </summary>
public class OverviewPanel : UserControl
{
    private VehicleState? _state;

    // Gauges
    private readonly CircularGauge gaugeBattery;
    private readonly CircularGauge gaugeSpeed;
    private readonly CircularGauge gaugeAltitude;
    private readonly CircularGauge gaugeSats;

    // Stat labels
    private string _modeText = "--";
    private string _vibText = "--";
    private Color _vibColor = ModernTheme.TextMuted;
    private string _ekfText = "--";
    private Color _ekfColor = ModernTheme.TextMuted;
    private string _posText = "--";

    public OverviewPanel()
    {
        Dock = DockStyle.Fill;
        BackColor = ModernTheme.Background;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

        gaugeBattery = new CircularGauge
        {
            Label = "BATTERY",
            Unit = "%",
            AccentColor = ModernTheme.Success,
            Size = new Size(140, 140)
        };

        gaugeSpeed = new CircularGauge
        {
            Label = "SPEED",
            Unit = "m/s",
            MaxValue = 50,
            AccentColor = ModernTheme.Accent,
            Size = new Size(140, 140)
        };

        gaugeAltitude = new CircularGauge
        {
            Label = "ALTITUDE",
            Unit = "m",
            MaxValue = 200,
            AccentColor = ModernTheme.Info,
            Size = new Size(140, 140)
        };

        gaugeSats = new CircularGauge
        {
            Label = "SATELLITES",
            Unit = "sats",
            MaxValue = 20,
            AccentColor = ModernTheme.Warning,
            Size = new Size(140, 140)
        };

        Controls.Add(gaugeBattery);
        Controls.Add(gaugeSpeed);
        Controls.Add(gaugeAltitude);
        Controls.Add(gaugeSats);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutGauges();
    }

    private void LayoutGauges()
    {
        if (Width < 100) return;

        int gaugeSize = Math.Min(140, (Width - 60) / 4);
        int gap = 20;
        int totalW = gaugeSize * 4 + gap * 3;
        int startX = (Width - totalW) / 2;
        int gaugeY = 30;

        foreach (var g in new[] { gaugeBattery, gaugeSpeed, gaugeAltitude, gaugeSats })
        {
            g.Size = new Size(gaugeSize, gaugeSize);
        }

        gaugeBattery.Location = new Point(startX, gaugeY);
        gaugeSpeed.Location = new Point(startX + gaugeSize + gap, gaugeY);
        gaugeAltitude.Location = new Point(startX + (gaugeSize + gap) * 2, gaugeY);
        gaugeSats.Location = new Point(startX + (gaugeSize + gap) * 3, gaugeY);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        // Grid background
        ModernTheme.DrawGridBackground(g, new Rectangle(0, 0, Width, Height));

        // Section title
        using var titleFont = new Font("Segoe UI", 9f, FontStyle.Bold);
        using var titleBrush = new SolidBrush(ModernTheme.TextMuted);
        g.DrawString("FLIGHT OVERVIEW", titleFont, titleBrush, 20, 10);

        // Bottom stat cards
        if (_state != null)
        {
            int cardY = 190;
            int cardW = (Width - 60) / 3;
            int cardH = 80;
            int cardGap = 10;

            ModernTheme.DrawStatCard(g, new Rectangle(20, cardY, cardW, cardH),
                "MODE", _modeText, ModernTheme.Accent, ModernTheme.Accent);
            ModernTheme.DrawStatCard(g, new Rectangle(20 + cardW + cardGap, cardY, cardW, cardH),
                "VIBRATION", _vibText, _vibColor, _vibColor);
            ModernTheme.DrawStatCard(g, new Rectangle(20 + (cardW + cardGap) * 2, cardY, cardW, cardH),
                "EKF", _ekfText, _ekfColor, _ekfColor);

            // Position card (full width)
            ModernTheme.DrawStatCard(g, new Rectangle(20, cardY + cardH + cardGap, Width - 40, cardH),
                "POSITION", _posText, ModernTheme.TextPrimary, ModernTheme.Accent);
        }
    }

    public void UpdateFromState(VehicleState state)
    {
        _state = state;

        // Update gauges
        gaugeBattery.Value = state.BatteryRemaining;
        gaugeBattery.AccentColor = state.BatteryRemaining > 50 ? ModernTheme.Success :
                                   state.BatteryRemaining > 20 ? ModernTheme.Warning : ModernTheme.Danger;

        gaugeSpeed.Value = state.GroundSpeed;

        gaugeAltitude.Value = state.AltitudeRel;

        gaugeSats.Value = state.SatelliteCount;
        gaugeSats.AccentColor = state.GpsFix >= 3 ? ModernTheme.Success :
                                state.GpsFix >= 1 ? ModernTheme.Warning : ModernTheme.Danger;

        // Update stat cards
        _modeText = state.FlightModeName;

        float vibMax = state.MaxVibration;
        _vibText = $"{vibMax:F0}";
        _vibColor = vibMax < 30 ? ModernTheme.Success :
                    vibMax < 60 ? ModernTheme.Warning : ModernTheme.Danger;

        float ekfMax = Math.Max(state.EkfVelVariance, Math.Max(state.EkfPosHorizVariance,
            Math.Max(state.EkfPosVertVariance, Math.Max(state.EkfCompassVariance, state.EkfTerrainAltVariance))));
        _ekfText = $"{ekfMax:F2}";
        _ekfColor = ekfMax < 0.5f ? ModernTheme.Success :
                    ekfMax < 0.8f ? ModernTheme.Warning : ModernTheme.Danger;

        _posText = $"{state.Latitude:F6}  {state.Longitude:F6}";

        Invalidate();
    }

    public void UpdateFromLogMessage(LogMessage msg)
    {
        if (msg.Fields.TryGetValue("VibeX", out var vx))
            _vibText = $"X:{vx:F0} Y:{msg.GetFloat("VibeY"):F0} Z:{msg.GetFloat("VibeZ"):F0}";
    }
}
