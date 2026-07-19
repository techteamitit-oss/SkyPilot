using SkyPilot.Core.Mavlink;
using SkyPilot.Log;

namespace SkyPilot.Panels;

/// <summary>
/// Overview panel showing key vehicle status at a glance.
/// </summary>
public class OverviewPanel : UserControl
{
    private readonly Label lblBattery;
    private readonly Label lblGps;
    private readonly Label lblVibration;
    private readonly Label lblEKF;
    private readonly Label lblMode;
    private readonly Label lblAltitude;
    private readonly Label lblSpeed;
    private readonly Label lblPosition;

    public OverviewPanel()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(30, 30, 30);
        ForeColor = Color.White;
        Padding = new Padding(10);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 4,
            BackColor = Color.Transparent
        };

        for (int i = 0; i < 4; i++)
        {
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        }

        lblBattery = CreateStatLabel("Battery", "--");
        lblGps = CreateStatLabel("GPS", "--");
        lblVibration = CreateStatLabel("Vibration", "--");
        lblEKF = CreateStatLabel("EKF", "--");
        lblMode = CreateStatLabel("Mode", "--");
        lblAltitude = CreateStatLabel("Altitude", "--");
        lblSpeed = CreateStatLabel("Speed", "--");
        lblPosition = CreateStatLabel("Position", "--");

        layout.Controls.Add(MakeCard("BATTERY", lblBattery), 0, 0);
        layout.Controls.Add(MakeCard("GPS", lblGps), 1, 0);
        layout.Controls.Add(MakeCard("VIBRATION", lblVibration), 2, 0);
        layout.Controls.Add(MakeCard("EKF", lblEKF), 3, 0);
        layout.Controls.Add(MakeCard("MODE", lblMode), 0, 1);
        layout.Controls.Add(MakeCard("ALTITUDE", lblAltitude), 1, 1);
        layout.Controls.Add(MakeCard("SPEED", lblSpeed), 2, 1);
        layout.Controls.Add(MakeCard("POSITION", lblPosition), 3, 1);

        Controls.Add(layout);
    }

    private Label CreateStatLabel(string title, string value)
    {
        return new Label
        {
            Text = value,
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            ForeColor = Color.White,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter
        };
    }

    private Panel MakeCard(string title, Label valueLabel)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(5),
            BackColor = Color.FromArgb(45, 45, 45)
        };

        var titleLabel = new Label
        {
            Text = title,
            Font = new Font("Segoe UI", 8F),
            ForeColor = Color.Gray,
            Dock = DockStyle.Top,
            Height = 20,
            TextAlign = ContentAlignment.MiddleCenter
        };

        panel.Controls.Add(valueLabel);
        panel.Controls.Add(titleLabel);

        return panel;
    }

    public void UpdateFromState(VehicleState state)
    {
        lblBattery.Text = $"{state.BatteryVoltage:F1}V";
        lblBattery.ForeColor = state.BatteryVoltage > 10.5f ? Color.LimeGreen :
                               state.BatteryVoltage > 9.5f ? Color.Orange : Color.Red;

        lblGps.Text = $"Fix {state.GpsFix} | {state.SatelliteCount} sats";
        lblGps.ForeColor = state.GpsFix >= 3 ? Color.LimeGreen :
                           state.GpsFix >= 1 ? Color.Orange : Color.Red;

        float vibMax = state.MaxVibration;
        lblVibration.Text = $"{vibMax:F0}";
        lblVibration.ForeColor = vibMax < 30 ? Color.LimeGreen :
                                 vibMax < 60 ? Color.Orange : Color.Red;

        float ekfMax = Math.Max(state.EkfVelVariance, Math.Max(state.EkfPosHorizVariance,
            Math.Max(state.EkfPosVertVariance, Math.Max(state.EkfCompassVariance, state.EkfTerrainAltVariance))));
        lblEKF.Text = $"{ekfMax:F2}";
        lblEKF.ForeColor = ekfMax < 0.5f ? Color.LimeGreen :
                           ekfMax < 0.8f ? Color.Orange : Color.Red;

        lblMode.Text = state.FlightModeName;
        lblAltitude.Text = $"{state.AltitudeRel:F1}m";
        lblSpeed.Text = $"{state.GroundSpeed:F1}m/s";
        lblPosition.Text = $"{state.Latitude:F6}\n{state.Longitude:F6}";
    }

    public void UpdateFromLogMessage(LogMessage msg)
    {
        // Extract available fields from log message
        if (msg.Fields.TryGetValue("VibeX", out var vx))
            lblVibration.Text = $"X:{vx:F0} Y:{msg.GetFloat("VibeY"):F0} Z:{msg.GetFloat("VibeZ"):F0}";
        if (msg.Fields.TryGetValue("PressAbs", out var pa))
            lblBattery.Text = $"{pa:F1} hPa";
    }
}
