using SkyPilot.Core.Mavlink;

namespace SkyPilot.Core.Sim;

/// <summary>
/// Simulated vehicle generating realistic MAVLink telemetry.
/// Supports fixed-wing plane flying a circular pattern.
/// </summary>
public class VirtualVehicle : IDisposable
{
    private readonly System.Threading.Timer _timer;
    private readonly Random _rng = new();
    private double _angle;
    private double _batteryPercent = 100;
    private float _throttle;
    private int _flightMode = 3; // Auto
    private bool _armed;
    private uint _bootMs;
    private readonly double _centerLat;
    private readonly double _centerLon;
    private readonly double _radius;
    private readonly float _cruiseSpeed;
    private readonly float _baseAlt;
    private float _altitude;
    private float _heading;

    public event Action<MavlinkPacket>? PacketGenerated;
    public bool IsRunning { get; private set; }
    public string VehicleType { get; }

    public VirtualVehicle(string vehicleType = "plane")
    {
        VehicleType = vehicleType;
        _centerLat = 51.5074;  // London
        _centerLon = -0.1278;
        _radius = 0.002;       // ~200m circle
        _cruiseSpeed = 25f;    // m/s
        _baseAlt = 100f;       // meters
        _altitude = _baseAlt;
        _throttle = 50;

        _timer = new System.Threading.Timer(Tick, null, 0, 250); // 4Hz
        IsRunning = true;
    }

    private void Tick(object? state)
    {
        if (!IsRunning) return;

        _bootMs += 250;
        _angle += 0.005; // slow circle
        _batteryPercent = Math.Max(0, _batteryPercent - 0.01);
        _throttle = 45 + (float)(_rng.NextDouble() * 10);
        _heading = (float)((_angle * 180.0 / Math.PI + 90) % 360);
        _altitude = _baseAlt + (float)Math.Sin(_angle * 3) * 5;
        float airspeed = _cruiseSpeed + (float)(_rng.NextDouble() * 2 - 1);
        float groundspeed = airspeed * 0.95f;

        double lat = _centerLat + Math.Cos(_angle) * _radius;
        double lon = _centerLon + Math.Sin(_angle) * _radius;
        float roll = (float)(Math.Sin(_angle * 2) * 15);
        float pitch = (float)(Math.Sin(_angle * 3) * 5);
        float yawRad = _heading * (float)Math.PI / 180f;

        // Generate all message types
        SendHeartbeat();
        SendAttitude(yawRad, pitch * (float)Math.PI / 180f, roll * (float)Math.PI / 180f);
        SendGlobalPosition(lat, lon, groundspeed);
        SendVfrHud(airspeed, groundspeed);
        SendSysStatus();
        SendGpsRaw(lat, lon);
        SendScaledImu(pitch, roll);
        SendVibration();
        SendEkfStatus();
        SendRcChannels();
        SendScaledPressure();
    }

    private void SendHeartbeat()
    {
        var payload = new byte[9];
        byte baseMode = (byte)(_armed ? MavlinkDefinitions.MAV_MODE_FLAG_SAFETY_ARMED : 0);
        baseMode |= MavlinkDefinitions.MAV_MODE_FLAG_CUSTOM_MODE_ENABLED;
        payload[0] = baseMode;
        BitConverter.GetBytes((uint)_flightMode).CopyTo(payload, 1);
        payload[5] = 0; // system_status
        payload[6] = 3; // MAV_AUTOPILOT_ARDUPILOTMEGA

        Emit(MavlinkCodec.Encode(1, 1, MavlinkDefinitions.HEARTBEAT, payload));
    }

    private void SendAttitude(float yaw, float pitch, float roll)
    {
        var payload = new byte[28];
        BitConverter.GetBytes(_bootMs).CopyTo(payload, 0);
        BitConverter.GetBytes(roll).CopyTo(payload, 4);
        BitConverter.GetBytes(pitch).CopyTo(payload, 8);
        BitConverter.GetBytes(yaw).CopyTo(payload, 12);
        BitConverter.GetBytes(0f).CopyTo(payload, 16); // rollspeed
        BitConverter.GetBytes(0f).CopyTo(payload, 20); // pitchspeed
        BitConverter.GetBytes(0f).CopyTo(payload, 24); // yawspeed

        Emit(MavlinkCodec.Encode(1, 1, MavlinkDefinitions.ATTITUDE, payload));
    }

    private void SendGlobalPosition(double lat, double lon, float groundspeed)
    {
        var payload = new byte[28];
        BitConverter.GetBytes(_bootMs).CopyTo(payload, 0);
        BitConverter.GetBytes((int)(lat * 1e7)).CopyTo(payload, 4);
        BitConverter.GetBytes((int)(lon * 1e7)).CopyTo(payload, 8);
        BitConverter.GetBytes((int)(_altitude * 1000)).CopyTo(payload, 12);
        BitConverter.GetBytes(0).CopyTo(payload, 16); // vx
        BitConverter.GetBytes(0).CopyTo(payload, 18); // vy
        BitConverter.GetBytes((short)(groundspeed * 100)).CopyTo(payload, 20);
        BitConverter.GetBytes((short)(_heading * 100)).CopyTo(payload, 22);

        Emit(MavlinkCodec.Encode(1, 1, MavlinkDefinitions.GLOBAL_POSITION_INT, payload));
    }

    private void SendVfrHud(float airspeed, float groundspeed)
    {
        var payload = new byte[20];
        BitConverter.GetBytes(airspeed).CopyTo(payload, 0);
        BitConverter.GetBytes(groundspeed).CopyTo(payload, 4);
        BitConverter.GetBytes(_altitude).CopyTo(payload, 8);
        BitConverter.GetBytes((float)(_rng.NextDouble() * 2 - 1)).CopyTo(payload, 12); // climbs
        BitConverter.GetBytes((short)_heading).CopyTo(payload, 16);

        Emit(MavlinkCodec.Encode(1, 1, MavlinkDefinitions.VFR_HUD, payload));
    }

    private void SendSysStatus()
    {
        var payload = new byte[32];
        BitConverter.GetBytes((short)0).CopyTo(payload, 0);  // voltage_sensor
        BitConverter.GetBytes((short)0).CopyTo(payload, 2);
        BitConverter.GetBytes((short)0).CopyTo(payload, 4);
        BitConverter.GetBytes((short)0).CopyTo(payload, 6);
        BitConverter.GetBytes((short)0).CopyTo(payload, 8);
        BitConverter.GetBytes((ushort)(_batteryPercent > 0 ? 12600 : 0)).CopyTo(payload, 10); // battery_voltage
        BitConverter.GetBytes((short)(_throttle * 1.5f)).CopyTo(payload, 12); // battery_current
        payload[14] = (byte)_batteryPercent;
        int sensors = 0x1FFFF; // all sensors present/enabled/healthy
        BitConverter.GetBytes(sensors).CopyTo(payload, 16);
        BitConverter.GetBytes(sensors).CopyTo(payload, 20);
        BitConverter.GetBytes(sensors).CopyTo(payload, 24);
        payload[28] = 4; // drop_rate_comm

        Emit(MavlinkCodec.Encode(1, 1, MavlinkDefinitions.SYS_STATUS, payload));
    }

    private void SendGpsRaw(double lat, double lon)
    {
        var payload = new byte[30];
        BitConverter.GetBytes((ulong)(DateTime.UtcNow - DateTime.MinValue).TotalMilliseconds * 1000).CopyTo(payload, 0);
        payload[8] = 3; // 3D fix
        BitConverter.GetBytes((int)(lat * 1e7)).CopyTo(payload, 9);
        BitConverter.GetBytes((int)(lon * 1e7)).CopyTo(payload, 13);
        BitConverter.GetBytes(_altitude).CopyTo(payload, 17);
        BitConverter.GetBytes((ushort)120).CopyTo(payload, 21); // HDOP
        BitConverter.GetBytes((ushort)0).CopyTo(payload, 23); // VDOP
        payload[25] = 12; // satellites

        Emit(MavlinkCodec.Encode(1, 1, MavlinkDefinitions.GPS_RAW_INT, payload));
    }

    private void SendScaledImu(float pitch, float roll)
    {
        var payload = new byte[22];
        BitConverter.GetBytes(_bootMs).CopyTo(payload, 0);
        BitConverter.GetBytes((short)(9.8f * 1000)).CopyTo(payload, 2);   // accX
        BitConverter.GetBytes((short)(Math.Sin(pitch * Math.PI / 180) * 9800)).CopyTo(payload, 4);  // accY
        BitConverter.GetBytes((short)(Math.Cos(roll * Math.PI / 180) * 9800)).CopyTo(payload, 6);  // accZ
        BitConverter.GetBytes((short)(_rng.NextDouble() * 20 - 10)).CopyTo(payload, 8);   // gyroX
        BitConverter.GetBytes((short)(_rng.NextDouble() * 20 - 10)).CopyTo(payload, 10);  // gyroY
        BitConverter.GetBytes((short)(_rng.NextDouble() * 20 - 10)).CopyTo(payload, 12);  // gyroZ
        BitConverter.GetBytes((short)200).CopyTo(payload, 14);  // magX
        BitConverter.GetBytes((short)50).CopyTo(payload, 16);   // magY
        BitConverter.GetBytes((short)400).CopyTo(payload, 18);  // magZ
        BitConverter.GetBytes((short)2500).CopyTo(payload, 20); // temperature

        Emit(MavlinkCodec.Encode(1, 1, MavlinkDefinitions.SCALED_IMU, payload));
    }

    private void SendVibration()
    {
        var payload = new byte[28];
        BitConverter.GetBytes(_bootMs).CopyTo(payload, 0);
        BitConverter.GetBytes(5f + (float)_rng.NextDouble() * 10).CopyTo(payload, 4);   // vibeX
        BitConverter.GetBytes(3f + (float)_rng.NextDouble() * 8).CopyTo(payload, 8);    // vibeY
        BitConverter.GetBytes(10f + (float)_rng.NextDouble() * 15).CopyTo(payload, 12);  // vibeZ
        BitConverter.GetBytes((uint)(_rng.Next(0, 10))).CopyTo(payload, 16);  // clip0
        BitConverter.GetBytes((uint)(_rng.Next(0, 5))).CopyTo(payload, 20);   // clip1
        BitConverter.GetBytes((uint)(_rng.Next(0, 3))).CopyTo(payload, 24);   // clip2

        Emit(MavlinkCodec.Encode(1, 1, MavlinkDefinitions.VIBRATION, payload));
    }

    private void SendEkfStatus()
    {
        var payload = new byte[24];
        BitConverter.GetBytes(0x1F).CopyTo(payload, 0); // flags - all healthy
        BitConverter.GetBytes(0.1f + (float)_rng.NextDouble() * 0.2f).CopyTo(payload, 4);  // vel
        BitConverter.GetBytes(0.05f + (float)_rng.NextDouble() * 0.1f).CopyTo(payload, 8); // posH
        BitConverter.GetBytes(0.1f + (float)_rng.NextDouble() * 0.15f).CopyTo(payload, 12); // posV
        BitConverter.GetBytes(0.08f + (float)_rng.NextDouble() * 0.1f).CopyTo(payload, 16); // compass
        BitConverter.GetBytes(0.2f + (float)_rng.NextDouble() * 0.3f).CopyTo(payload, 20); // terrain

        Emit(MavlinkCodec.Encode(1, 1, MavlinkDefinitions.EKF_STATUS_REPORT, payload));
    }

    private void SendRcChannels()
    {
        var payload = new byte[38];
        BitConverter.GetBytes(_bootMs).CopyTo(payload, 0);
        for (int i = 0; i < 18; i++)
            BitConverter.GetBytes((ushort)(1500 + _rng.Next(-50, 50))).CopyTo(payload, 2 + i * 2);
        payload[37] = 18; // chancount

        Emit(MavlinkCodec.Encode(1, 1, MavlinkDefinitions.RC_CHANNELS, payload));
    }

    private void SendScaledPressure()
    {
        var payload = new byte[14];
        BitConverter.GetBytes(_bootMs).CopyTo(payload, 0);
        BitConverter.GetBytes(1013.25f).CopyTo(payload, 4); // abs pressure
        BitConverter.GetBytes(0f).CopyTo(payload, 8);       // diff pressure
        BitConverter.GetBytes((short)2500).CopyTo(payload, 12); // temperature

        Emit(MavlinkCodec.Encode(1, 1, MavlinkDefinitions.SCALED_PRESSURE, payload));
    }

    public void SetMode(int mode)
    {
        _flightMode = mode;
        SendStatustext($"Mode changed to {MavlinkDefinitions.FlightModes.GetValueOrDefault(mode, $"Mode {mode}")}");
    }

    public void SetArmed(bool armed)
    {
        _armed = armed;
        SendStatustext(armed ? "Arm" : "Disarm");
    }

    public void SendStatustext(string text)
    {
        var payload = new byte[50];
        payload[0] = 6; // severity: MAV_SEVERITY_INFO
        var bytes = System.Text.Encoding.ASCII.GetBytes(text);
        Array.Copy(bytes, 0, payload, 1, Math.Min(bytes.Length, 49));

        Emit(MavlinkCodec.Encode(1, 1, 253, payload)); // STATUSTEXT = 253
    }

    private void Emit(byte[] data)
    {
        var packet = MavlinkCodec.Decode(data, 0, data.Length);
        if (packet != null)
            PacketGenerated?.Invoke(packet);
    }

    public void Dispose()
    {
        IsRunning = false;
        _timer?.Dispose();
    }
}
