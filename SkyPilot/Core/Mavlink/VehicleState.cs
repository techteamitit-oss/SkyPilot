namespace SkyPilot.Core.Mavlink;

/// <summary>
/// Complete vehicle state - all sensor data, position, mode, etc.
/// Updated from incoming MAVLink messages.
/// </summary>
public class VehicleState
{
    // Connection
    public bool IsConnected { get; set; }
    public byte SystemId { get; set; } = 1;
    public byte ComponentId { get; set; } = 1;
    public string FirmwareName { get; set; } = "";
    public DateTime LastHeartbeat { get; set; }

    // Attitude
    public float Roll { get; set; }
    public float Pitch { get; set; }
    public float Yaw { get; set; }

    // Position
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public float AltitudeMsl { get; set; }
    public float AltitudeRel { get; set; }

    // Speed
    public float GroundSpeed { get; set; }
    public float AirSpeed { get; set; }
    public float VerticalSpeed { get; set; }

    // GPS
    public int GpsFix { get; set; }
    public int SatelliteCount { get; set; }
    public float Hdop { get; set; }

    // Battery
    public float BatteryVoltage { get; set; }
    public float BatteryCurrent { get; set; }
    public int BatteryRemaining { get; set; }

    // IMU - Primary
    public float AccelX { get; set; }
    public float AccelY { get; set; }
    public float AccelZ { get; set; }
    public float GyroX { get; set; }
    public float GyroY { get; set; }
    public float GyroZ { get; set; }

    // IMU - Secondary
    public float Accel2X { get; set; }
    public float Accel2Y { get; set; }
    public float Accel2Z { get; set; }
    public float Gyro2X { get; set; }
    public float Gyro2Y { get; set; }
    public float Gyro2Z { get; set; }

    // Magnetometer
    public float MagX { get; set; }
    public float MagY { get; set; }
    public float MagZ { get; set; }

    // Barometer
    public float PressureAbs { get; set; }
    public float BaroTemp { get; set; }

    // Vibration
    public float VibeX { get; set; }
    public float VibeY { get; set; }
    public float VibeZ { get; set; }
    public uint VibeClip0 { get; set; }
    public uint VibeClip1 { get; set; }
    public uint VibeClip2 { get; set; }

    // EKF
    public int EkfFlags { get; set; }
    public float EkfVelVariance { get; set; }
    public float EkfPosHorizVariance { get; set; }
    public float EkfPosVertVariance { get; set; }
    public float EkfCompassVariance { get; set; }
    public float EkfTerrainAltVariance { get; set; }

    // Sensor health
    public int SensorsPresent { get; set; }
    public int SensorsEnabled { get; set; }
    public int SensorsHealth { get; set; }

    // RC Channels
    public float[] RcChannels { get; set; } = new float[18];

    // Mode
    public int FlightMode { get; set; }
    public bool IsArmed { get; set; }
    public bool IsCustomModeEnabled { get; set; }

    /// <summary>Get flight mode name from mode number.</summary>
    public string FlightModeName =>
        MavlinkDefinitions.FlightModes.TryGetValue(FlightMode, out var name) ? name : $"Mode {FlightMode}";

    /// <summary>Is the EKF healthy overall?</summary>
    public bool IsEkfHealthy =>
        EkfVelVariance < 0.5f && EkfPosHorizVariance < 0.5f && EkfCompassVariance < 0.5f;

    /// <summary>Get worst vibration level across axes.</summary>
    public float MaxVibration => Math.Max(VibeX, Math.Max(VibeY, VibeZ));
}
