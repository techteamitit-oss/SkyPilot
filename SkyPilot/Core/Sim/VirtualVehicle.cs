using SkyPilot.Core.Mavlink;

namespace SkyPilot.Core.Sim;

public class VirtualVehicle : IDisposable
{
    private readonly System.Threading.Timer _timer;
    private readonly Random _rng = new();
    private double _angle;
    private double _batteryPercent = 100;
    private int _flightMode = 3;
    private bool _armed = true;
    private uint _bootMs;
    private double _lat, _lon;
    private float _altitude;
    private float _heading;
    private readonly string _vehicleType;
    private float _cruiseSpeed;
    private float _baseAlt;
    private double _radius;
    private readonly string _pattern;
    private double _startLat, _startLon;
    private double _targetLat, _targetLon;
    private double _distanceMeters;
    private double _patternProgress;

    public event Action<MavlinkPacket>? PacketGenerated;
    public bool IsRunning { get; private set; }
    public double StartLat => _startLat;
    public double StartLon => _startLon;
    public double TargetLat => _targetLat;
    public double TargetLon => _targetLon;
    public string VehicleType => _vehicleType;
    public string Pattern => _pattern;

    public VirtualVehicle(string vehicleType = "plane", string pattern = "circle",
        double? startLat = null, double? startLon = null, double? targetLat = null, double? targetLon = null, double? distance = null)
    {
        _vehicleType = vehicleType.ToLower();
        _pattern = pattern.ToLower();
        _startLat = startLat ?? 51.5074;
        _startLon = startLon ?? -0.1278;
        _lat = _startLat;
        _lon = _startLon;

        switch (_vehicleType)
        {
            case "copter": _cruiseSpeed = 10f; _baseAlt = 50f; break;
            case "rover": _cruiseSpeed = 5f; _baseAlt = 0f; break;
            default: _cruiseSpeed = 25f; _baseAlt = 100f; break;
        }
        _altitude = _baseAlt;

        switch (_pattern)
        {
            case "point2point":
                _targetLat = targetLat ?? 51.51;
                _targetLon = targetLon ?? -0.13;
                _distanceMeters = HaversineDistance(_startLat, _startLon, _targetLat, _targetLon);
                break;
            case "distance":
                _distanceMeters = distance ?? 500;
                // Calculate target from start + distance at 0 degrees
                double bearing = 0;
                var (tLat, tLon) = DestinationPoint(_startLat, _startLon, _distanceMeters, bearing);
                _targetLat = tLat;
                _targetLon = tLon;
                break;
            default: // circle
                _radius = 0.002;
                break;
        }

        _timer = new System.Threading.Timer(Tick, null, 0, 250);
        IsRunning = true;
    }

    private void Tick(object? state)
    {
        if (!IsRunning) return;
        _bootMs += 250;
        _batteryPercent = Math.Max(0, _batteryPercent - 0.01);

        switch (_pattern)
        {
            case "point2point": TickPoint2Point(); break;
            case "distance": TickDistance(); break;
            default: TickCircle(); break;
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

    private void TickCircle()
    {
        _angle += 0.004;
        _lat = _startLat + Math.Cos(_angle) * 0.002;
        _lon = _startLon + Math.Sin(_angle) * 0.002;
        _altitude = _baseAlt + (float)Math.Sin(_angle * 2) * 10;
        _heading = (float)((_angle * 180.0 / Math.PI + 90) % 360);
    }

    private void TickPoint2Point()
    {
        _patternProgress += 0.002;
        if (_patternProgress >= 1) _patternProgress = 0;

        double lat = _startLat + (_targetLat - _startLat) * _patternProgress;
        double lon = _startLon + (_targetLon - _startLon) * _patternProgress;

        double dlat = _targetLat - _startLat;
        double dlon = _targetLon - _startLon;
        _heading = (float)((Math.Atan2(dlon, dlat) * 180.0 / Math.PI + 360) % 360);

        _lat = lat;
        _lon = lon;
        _altitude = _baseAlt + (float)Math.Sin(_patternProgress * Math.PI) * 20;
    }

    private void TickDistance()
    {
        _patternProgress += 0.003;
        if (_patternProgress >= 1) _patternProgress = 0;

        // Move along distance, then return
        double progress = _patternProgress < 0.5 ? _patternProgress * 2 : (1 - _patternProgress) * 2;
        var (lat, lon) = DestinationPoint(_startLat, _startLon, _distanceMeters * progress, 0);

        _lat = lat;
        _lon = lon;
        _heading = _patternProgress < 0.5 ? 0 : 180;
        _altitude = _baseAlt + (float)Math.Sin(_patternProgress * Math.PI * 2) * 5;
    }

    private static double HaversineDistance(double lat1, double lon1, double lat2, double lon2)
    {
        double R = 6371000;
        double dLat = (lat2 - lat1) * Math.PI / 180;
        double dLon = (lon2 - lon1) * Math.PI / 180;
        double a = Math.Sin(dLat/2) * Math.Sin(dLat/2) + Math.Cos(lat1*Math.PI/180) * Math.Cos(lat2*Math.PI/180) * Math.Sin(dLon/2) * Math.Sin(dLon/2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1-a));
    }

    private static (double lat, double lon) DestinationPoint(double lat, double lon, double distance, double bearing)
    {
        double R = 6371000;
        double brng = bearing * Math.PI / 180;
        double d = distance / R;
        double lat1 = lat * Math.PI / 180;
        double lon1 = lon * Math.PI / 180;
        double lat2 = Math.Asin(Math.Sin(lat1) * Math.Cos(d) + Math.Cos(lat1) * Math.Sin(d) * Math.Cos(brng));
        double lon2 = lon1 + Math.Atan2(Math.Sin(brng) * Math.Sin(d) * Math.Cos(lat1), Math.Cos(d) - Math.Sin(lat1) * Math.Sin(lat2));
        return (lat2 * 180 / Math.PI, lon2 * 180 / Math.PI);
    }

    private void SendHeartbeat()
    {
        var p = new byte[9];
        p[0] = (byte)(_armed ? 129 : 1);
        BitConverter.GetBytes((uint)_flightMode).CopyTo(p, 1);
        p[5] = 4; p[6] = 3;
        Emit(MavlinkCodec.Encode(1, 1, 0, p));
    }

    private void SendAttitude()
    {
        float roll, pitch;
        switch (_vehicleType)
        {
            case "copter": roll = (float)(Math.Sin(_angle * 2) * 20); pitch = (float)(Math.Sin(_angle * 3) * 10); break;
            case "rover": roll = 0; pitch = 0; break;
            default: roll = (float)(Math.Sin(_patternProgress * 10) * 15); pitch = (float)(Math.Sin(_patternProgress * 15) * 5); break;
        }
        var p = new byte[28];
        BitConverter.GetBytes(_bootMs).CopyTo(p, 0);
        BitConverter.GetBytes(roll * (float)Math.PI / 180f).CopyTo(p, 4);
        BitConverter.GetBytes(pitch * (float)Math.PI / 180f).CopyTo(p, 8);
        BitConverter.GetBytes(_heading * (float)Math.PI / 180f).CopyTo(p, 12);
        Emit(MavlinkCodec.Encode(1, 1, 30, p));
    }

    private void SendGlobalPosition()
    {
        float gs = _cruiseSpeed + (float)(_rng.NextDouble() * 2 - 1);
        var p = new byte[28];
        BitConverter.GetBytes(_bootMs).CopyTo(p, 0);
        BitConverter.GetBytes((int)(_lat * 1e7)).CopyTo(p, 4);
        BitConverter.GetBytes((int)(_lon * 1e7)).CopyTo(p, 8);
        BitConverter.GetBytes((int)(_altitude * 1000)).CopyTo(p, 12);
        BitConverter.GetBytes((short)(gs * 100)).CopyTo(p, 20);
        BitConverter.GetBytes((short)(_heading * 100)).CopyTo(p, 22);
        Emit(MavlinkCodec.Encode(1, 1, 33, p));
    }

    private void SendVfrHud()
    {
        float airspeed = _cruiseSpeed * 1.05f;
        float gs = _cruiseSpeed;
        var p = new byte[20];
        BitConverter.GetBytes(airspeed).CopyTo(p, 0);
        BitConverter.GetBytes(gs).CopyTo(p, 4);
        BitConverter.GetBytes(_altitude).CopyTo(p, 8);
        BitConverter.GetBytes((float)(_rng.NextDouble() * 2 - 1)).CopyTo(p, 12);
        BitConverter.GetBytes((short)_heading).CopyTo(p, 16);
        Emit(MavlinkCodec.Encode(1, 1, 74, p));
    }

    private void SendSysStatus()
    {
        var p = new byte[32];
        BitConverter.GetBytes((ushort)(_batteryPercent > 0 ? 12600 : 0)).CopyTo(p, 10);
        BitConverter.GetBytes((short)75).CopyTo(p, 12);
        p[14] = (byte)_batteryPercent;
        int s = 0x1FFFF;
        BitConverter.GetBytes(s).CopyTo(p, 16);
        BitConverter.GetBytes(s).CopyTo(p, 20);
        BitConverter.GetBytes(s).CopyTo(p, 24);
        Emit(MavlinkCodec.Encode(1, 1, 1, p));
    }

    private void SendGpsRaw()
    {
        var p = new byte[30];
        payload8(p, 0, (ulong)(DateTime.UtcNow - DateTime.MinValue).TotalMilliseconds * 1000);
        p[8] = 3;
        BitConverter.GetBytes((int)(_lat * 1e7)).CopyTo(p, 9);
        BitConverter.GetBytes((int)(_lon * 1e7)).CopyTo(p, 13);
        BitConverter.GetBytes(_altitude).CopyTo(p, 17);
        BitConverter.GetBytes((ushort)120).CopyTo(p, 21);
        p[25] = 14;
        Emit(MavlinkCodec.Encode(1, 1, 24, p));
    }

    private void SendScaledImu()
    {
        var p = new byte[22];
        BitConverter.GetBytes(_bootMs).CopyTo(p, 0);
        BitConverter.GetBytes((short)9800).CopyTo(p, 2);
        BitConverter.GetBytes((short)(_rng.NextDouble() * 200 - 100)).CopyTo(p, 4);
        BitConverter.GetBytes((short)9800).CopyTo(p, 6);
        BitConverter.GetBytes((short)(_rng.NextDouble() * 20 - 10)).CopyTo(p, 8);
        BitConverter.GetBytes((short)(_rng.NextDouble() * 20 - 10)).CopyTo(p, 10);
        BitConverter.GetBytes((short)(_rng.NextDouble() * 20 - 10)).CopyTo(p, 12);
        BitConverter.GetBytes((short)200).CopyTo(p, 14);
        BitConverter.GetBytes((short)50).CopyTo(p, 16);
        BitConverter.GetBytes((short)400).CopyTo(p, 18);
        BitConverter.GetBytes((short)2500).CopyTo(p, 20);
        Emit(MavlinkCodec.Encode(1, 1, 26, p));
    }

    private void SendVibration()
    {
        float vb = _vehicleType == "copter" ? 8f : 3f;
        var p = new byte[28];
        BitConverter.GetBytes(_bootMs).CopyTo(p, 0);
        BitConverter.GetBytes(vb + (float)_rng.NextDouble() * 5).CopyTo(p, 4);
        BitConverter.GetBytes(vb + (float)_rng.NextDouble() * 4).CopyTo(p, 8);
        BitConverter.GetBytes(vb * 1.5f + (float)_rng.NextDouble() * 6).CopyTo(p, 12);
        BitConverter.GetBytes((uint)_rng.Next(0, 5)).CopyTo(p, 16);
        BitConverter.GetBytes((uint)_rng.Next(0, 3)).CopyTo(p, 20);
        BitConverter.GetBytes((uint)_rng.Next(0, 2)).CopyTo(p, 24);
        Emit(MavlinkCodec.Encode(1, 1, 241, p));
    }

    private void SendEkfStatus()
    {
        var p = new byte[24];
        BitConverter.GetBytes(0x1F).CopyTo(p, 0);
        BitConverter.GetBytes(0.1f + (float)_rng.NextDouble() * 0.2f).CopyTo(p, 4);
        BitConverter.GetBytes(0.05f + (float)_rng.NextDouble() * 0.1f).CopyTo(p, 8);
        BitConverter.GetBytes(0.1f + (float)_rng.NextDouble() * 0.15f).CopyTo(p, 12);
        BitConverter.GetBytes(0.08f + (float)_rng.NextDouble() * 0.1f).CopyTo(p, 16);
        BitConverter.GetBytes(0.2f + (float)_rng.NextDouble() * 0.3f).CopyTo(p, 20);
        Emit(MavlinkCodec.Encode(1, 1, 232, p));
    }

    private void SendRcChannels()
    {
        var p = new byte[38];
        BitConverter.GetBytes(_bootMs).CopyTo(p, 0);
        for (int i = 0; i < 18; i++)
            BitConverter.GetBytes((ushort)(1500 + _rng.Next(-50, 50))).CopyTo(p, 2 + i * 2);
        p[37] = 18;
        Emit(MavlinkCodec.Encode(1, 1, 65, p));
    }

    private void SendScaledPressure()
    {
        var p = new byte[14];
        BitConverter.GetBytes(_bootMs).CopyTo(p, 0);
        BitConverter.GetBytes(1013.25f).CopyTo(p, 4);
        BitConverter.GetBytes(0f).CopyTo(p, 8);
        BitConverter.GetBytes((short)2500).CopyTo(p, 12);
        Emit(MavlinkCodec.Encode(1, 1, 29, p));
    }

    public void SetMode(int mode) => _flightMode = mode;
    public void SetArmed(bool armed) => _armed = armed;
    public void SendStatustext(string text)
    {
        var p = new byte[50];
        p[0] = 6;
        var bytes = System.Text.Encoding.ASCII.GetBytes(text);
        Array.Copy(bytes, 0, p, 1, Math.Min(bytes.Length, 49));
        Emit(MavlinkCodec.Encode(1, 1, 253, p));
    }

    private void Emit(byte[] data)
    {
        var packet = MavlinkCodec.Decode(data, 0, data.Length);
        if (packet != null) PacketGenerated?.Invoke(packet);
    }

    private static void payload8(byte[] p, int offset, ulong val) { BitConverter.GetBytes(val).CopyTo(p, offset); }

    public void Dispose() { IsRunning = false; _timer?.Dispose(); }
}
