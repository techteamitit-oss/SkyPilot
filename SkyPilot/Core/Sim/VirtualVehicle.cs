using SkyPilot.Core.Mavlink;

namespace SkyPilot.Core.Sim;

/// <summary>
/// Simulated vehicle generating realistic MAVLink telemetry.
/// Supports plane, copter, and rover with different flight patterns.
/// </summary>
public class VirtualVehicle : IDisposable
{
    private readonly System.Threading.Timer _timer;
    private readonly Random _rng = new();
    private double _angle;
    private double _batteryPercent = 100;
    private int _flightMode = 3; // Auto
    private bool _armed = true;
    private uint _bootMs;
    private readonly double _centerLat;
    private readonly double _centerLon;
    private readonly double _radius;
    private readonly float _cruiseSpeed;
    private readonly float _baseAlt;
    private float _altitude;
    private float _heading;
    private readonly string _vehicleType;

    public event Action<MavlinkPacket>? PacketGenerated;
    public bool IsRunning { get; private set; }
    public string VehicleType => _vehicleType;

    public VirtualVehicle(string vehicleType = "plane")
    {
        _vehicleType = vehicleType.ToLower();
        _centerLat = 51.5074;  // London
        _centerLon = -0.1278;

        switch (_vehicleType)
        {
            case "copter":
                _radius = 0.001;       // ~100m circle
                _cruiseSpeed = 10f;    // m/s
                _baseAlt = 50f;        // meters
                break;
            case "rover":
                _radius = 0.002;       // ~200m circle
                _cruiseSpeed = 5f;     // m/s
                _baseAlt = 0f;
                break;
            default: // plane
                _radius = 0.003;       // ~300m circle
                _cruiseSpeed = 25f;    // m/s
                _baseAlt = 100f;
                break;
        }

        _altitude = _baseAlt;
        _timer = new System.Threading.Timer(Tick, null, 0, 250); // 4Hz
        IsRunning = true;
    }

    private void Tick(object? state)
    {
        if (!IsRunning) return;

        _bootMs += 250;
        _batteryPercent = Math.Max(0, _batteryPercent - 0.01);

        switch (_vehicleType)
        {
            case "copter":
                TickCopter();
                break;
            case "rover":
                TickRover();
                break;
            default:
                TickPlane();
                break;
        }

        SendHeartbeat();
        SendAttitude();
        SendGlobalPosition();
        SendVfrHud();
        SendSysStatus();
        SendGpsRaw();
        SendScaledImu();
        SendVibration();
        SendEkfStatus();
        SendRcChannels();
        SendScaledPressure();
    }

    private void TickPlane()
    {
        _angle += 0.004;
        _altitude = _baseAlt + (float)Math.Sin(_angle * 2) * 10;
        _heading = (float)((_angle * 180.0 / Math.PI + 90) % 360);
    }

    private void TickCopter()
    {
        _angle += 0.008; // Faster circles
        _altitude = _baseAlt + (float)Math.Sin(_angle * 3) * 5;
        _heading = (float)((_angle * 180.0 / Math.PI + 90) % 360);
    }

    private void TickRover()
    {
        _angle += 0.003;
        _altitude = 0;
        _heading = (float)((_angle * 180.0 / Math.PI + 90) % 360);
    }

    private double GetLat() => _centerLat + Math.Cos(_angle) * _radius;
    private double GetLon() => _centerLon + Math.Sin(_angle) * _radius;
    private float GetGroundSpeed() => _cruiseSpeed + (float)(_rng.NextDouble() * 2 - 1);
    private float GetAirSpeed() => GetGroundSpeed() * 1.05f;

    private void SendHeartbeat()
    {
        var payload = new byte[9];
        byte baseMode = _armed ? (byte)128 : (byte)0;
        baseMode |= 1;
        payload[0] = baseMode;
        BitConverter.GetBytes((uint)_flightMode).CopyTo(payload, 1);
        payload[5] = 4; // MAV_STATE_ACTIVE
        payload[6] = 3; // MAV_AUTOPILOT_ARDUPILOTMEGA

        Emit(MavlinkCodec.Encode(1, 1, MavlinkDefinitions.HEARTBEAT, payload));
    }

    private void SendAttitude()
    {
        float roll, pitch;
        switch (_vehicleType)
        {
            case "copter":
                roll = (float)(Math.Sin(_angle * 2) * 20);
                pitch = (float)(Math.Sin(_angle * 3) * 10);
                break;
            case "rover":
                roll = 0;
                pitch = 0;
                break;
            default:
                roll = (float)(Math.Sin(_angle * 2) * 25);
                pitch = (float)(Math.Sin(_angle * 3) * 8);
                break;
        }
        float yaw = _heading * (float)Math.PI / 180f;

        var payload = new byte[28];
        BitConverter.GetBytes(_bootMs).CopyTo(payload, 0);
        BitConverter.GetBytes(roll * (float)Math.PI / 180f).CopyTo(payload, 4);
        BitConverter.GetBytes(pitch * (float)Math.PI / 180f).CopyTo(payload, 8);
        BitConverter.GetBytes(yaw).CopyTo(payload, 12);

        Emit(MavlinkCodec.Encode(1, 1, MavlinkDefinitions.ATTITUDE, payload));
    }

    private void SendGlobalPosition()
    {
        double lat = GetLat();
        double lon = GetLon();
        float gs = GetGroundSpeed();

        var payload = new byte[28];
        BitConverter.GetBytes(_bootMs).CopyTo(payload, 0);
        BitConverter.GetBytes((int)(lat * 1e7)).CopyTo(payload, 4);
        BitConverter.GetBytes((int)(lon * 1e7)).CopyTo(payload, 8);
        BitConverter.GetBytes((int)(_altitude * 1000)).CopyTo(payload, 12);
        BitConverter.GetBytes((short)(gs * 100)).CopyTo(payload, 20);
        BitConverter.GetBytes((short)(_heading * 100)).CopyTo(payload, 22);

        Emit(MavlinkCodec.Encode(1, 1, MavlinkDefinitions.GLOBAL_POSITION_INT, payload));
    }

    private void SendVfrHud()
    {
        float airspeed = GetAirSpeed();
        float groundspeed = GetGroundSpeed();
        float climb = _vehicleType == "rover" ? 0 : (float)(_rng.NextDouble() * 2 - 1);

        var payload = new byte[20];
        BitConverter.GetBytes(airspeed).CopyTo(payload, 0);
        BitConverter.GetBytes(groundspeed).CopyTo(payload, 4);
        BitConverter.GetBytes(_altitude).CopyTo(payload, 8);
        BitConverter.GetBytes(climb).CopyTo(payload, 12);
        BitConverter.GetBytes((short)_heading).CopyTo(payload, 16);

        Emit(MavlinkCodec.Encode(1, 1, MavlinkDefinitions.VFR_HUD, payload));
    }

    private void SendSysStatus()
    {
        var payload = new byte[32];
        BitConverter.GetBytes((ushort)(_batteryPercent > 0 ? 12600 : 0)).CopyTo(payload, 10);
        BitConverter.GetBytes((short)(50 * 1.5f)).CopyTo(payload, 12);
        payload[14] = (byte)_batteryPercent;
        int sensors = 0x1FFFF;
        BitConverter.GetBytes(sensors).CopyTo(payload, 16);
        BitConverter.GetBytes(sensors).CopyTo(payload, 20);
        BitConverter.GetBytes(sensors).CopyTo(payload, 24);

        Emit(MavlinkCodec.Encode(1, 1, MavlinkDefinitions.SYS_STATUS, payload));
    }

    private void SendGpsRaw()
    {
        var payload = new byte[30];
        BitConverter.GetBytes((ulong)(DateTime.UtcNow - DateTime.MinValue).TotalMilliseconds * 1000).CopyTo(payload, 0);
        payload[8] = 3;
        BitConverter.GetBytes((int)(GetLat() * 1e7)).CopyTo(payload, 9);
        BitConverter.GetBytes((int)(GetLon() * 1e7)).CopyTo(payload, 13);
        BitConverter.GetBytes(_altitude).CopyTo(payload, 17);
        BitConverter.GetBytes((ushort)120).CopyTo(payload, 21);
        payload[25] = 14;

        Emit(MavlinkCodec.Encode(1, 1, MavlinkDefinitions.GPS_RAW_INT, payload));
    }

    private void SendScaledImu()
    {
        var payload = new byte[22];
        BitConverter.GetBytes(_bootMs).CopyTo(payload, 0);
        BitConverter.GetBytes((short)(9.8f * 1000)).CopyTo(payload, 2);
        BitConverter.GetBytes((short)(_rng.NextDouble() * 200 - 100)).CopyTo(payload, 4);
        BitConverter.GetBytes((short)(9.8f * 1000)).CopyTo(payload, 6);
        BitConverter.GetBytes((short)(_rng.NextDouble() * 20 - 10)).CopyTo(payload, 8);
        BitConverter.GetBytes((short)(_rng.NextDouble() * 20 - 10)).CopyTo(payload, 10);
        BitConverter.GetBytes((short)(_rng.NextDouble() * 20 - 10)).CopyTo(payload, 12);
        BitConverter.GetBytes((short)200).CopyTo(payload, 14);
        BitConverter.GetBytes((short)50).CopyTo(payload, 16);
        BitConverter.GetBytes((short)400).CopyTo(payload, 18);
        BitConverter.GetBytes((short)2500).CopyTo(payload, 20);

        Emit(MavlinkCodec.Encode(1, 1, MavlinkDefinitions.SCALED_IMU, payload));
    }

    private void SendVibration()
    {
        var vibBase = _vehicleType == "copter" ? 8f : 3f;
        var payload = new byte[28];
        BitConverter.GetBytes(_bootMs).CopyTo(payload, 0);
        BitConverter.GetBytes(vibBase + (float)_rng.NextDouble() * 5).CopyTo(payload, 4);
        BitConverter.GetBytes(vibBase + (float)_rng.NextDouble() * 4).CopyTo(payload, 8);
        BitConverter.GetBytes(vibBase * 1.5f + (float)_rng.NextDouble() * 6).CopyTo(payload, 12);
        BitConverter.GetBytes((uint)_rng.Next(0, 5)).CopyTo(payload, 16);
        BitConverter.GetBytes((uint)_rng.Next(0, 3)).CopyTo(payload, 20);
        BitConverter.GetBytes((uint)_rng.Next(0, 2)).CopyTo(payload, 24);

        Emit(MavlinkCodec.Encode(1, 1, MavlinkDefinitions.VIBRATION, payload));
    }

    private void SendEkfStatus()
    {
        var payload = new byte[24];
        BitConverter.GetBytes(0x1F).CopyTo(payload, 0);
        BitConverter.GetBytes(0.1f + (float)_rng.NextDouble() * 0.2f).CopyTo(payload, 4);
        BitConverter.GetBytes(0.05f + (float)_rng.NextDouble() * 0.1f).CopyTo(payload, 8);
        BitConverter.GetBytes(0.1f + (float)_rng.NextDouble() * 0.15f).CopyTo(payload, 12);
        BitConverter.GetBytes(0.08f + (float)_rng.NextDouble() * 0.1f).CopyTo(payload, 16);
        BitConverter.GetBytes(0.2f + (float)_rng.NextDouble() * 0.3f).CopyTo(payload, 20);

        Emit(MavlinkCodec.Encode(1, 1, MavlinkDefinitions.EKF_STATUS_REPORT, payload));
    }

    private void SendRcChannels()
    {
        var payload = new byte[38];
        BitConverter.GetBytes(_bootMs).CopyTo(payload, 0);
        for (int i = 0; i < 18; i++)
            BitConverter.GetBytes((ushort)(1500 + _rng.Next(-50, 50))).CopyTo(payload, 2 + i * 2);
        payload[37] = 18;

        Emit(MavlinkCodec.Encode(1, 1, MavlinkDefinitions.RC_CHANNELS, payload));
    }

    private void SendScaledPressure()
    {
        var payload = new byte[14];
        BitConverter.GetBytes(_bootMs).CopyTo(payload, 0);
        BitConverter.GetBytes(1013.25f).CopyTo(payload, 4);
        BitConverter.GetBytes(0f).CopyTo(payload, 8);
        BitConverter.GetBytes((short)2500).CopyTo(payload, 12);

        Emit(MavlinkCodec.Encode(1, 1, MavlinkDefinitions.SCALED_PRESSURE, payload));
    }

    public void SetMode(int mode) => _flightMode = mode;
    public void SetArmed(bool armed) => _armed = armed;

    public void SendStatustext(string text)
    {
        var payload = new byte[50];
        payload[0] = 6;
        var bytes = System.Text.Encoding.ASCII.GetBytes(text);
        Array.Copy(bytes, 0, payload, 1, Math.Min(bytes.Length, 49));

        Emit(MavlinkCodec.Encode(1, 1, 253, payload));
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
