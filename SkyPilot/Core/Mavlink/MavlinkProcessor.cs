namespace SkyPilot.Core.Mavlink;

/// <summary>
/// Processes incoming MAVLink packets and updates VehicleState.
/// </summary>
public class MavlinkProcessor
{
    private readonly VehicleState _state;

    public event Action? StateUpdated;
    public event Action<string, byte>? MessageReceived;
    public event Action<byte, byte, byte>? CommandAckReceived;
    public event Action<string, float>? ParameterReceived;

    public MavlinkProcessor(VehicleState state)
    {
        _state = state;
    }

    public void ProcessPacket(MavlinkPacket packet)
    {
        switch (packet.MessageId)
        {
            case MavlinkDefinitions.HEARTBEAT:
                ProcessHeartbeat(packet);
                break;
            case MavlinkDefinitions.ATTITUDE:
                ProcessAttitude(packet);
                break;
            case MavlinkDefinitions.GLOBAL_POSITION_INT:
                ProcessGlobalPosition(packet);
                break;
            case MavlinkDefinitions.VFR_HUD:
                ProcessVfrHud(packet);
                break;
            case MavlinkDefinitions.SYS_STATUS:
                ProcessSysStatus(packet);
                break;
            case MavlinkDefinitions.BATTERY_STATUS:
                ProcessBatteryStatus(packet);
                break;
            case MavlinkDefinitions.GPS_RAW_INT:
                ProcessGpsRaw(packet);
                break;
            case MavlinkDefinitions.SCALED_IMU:
                ProcessScaledImu(packet);
                break;
            case MavlinkDefinitions.SCALED_IMU2:
                ProcessScaledImu2(packet);
                break;
            case MavlinkDefinitions.RAW_IMU:
                ProcessRawImu(packet);
                break;
            case MavlinkDefinitions.SCALED_PRESSURE:
                ProcessScaledPressure(packet);
                break;
            case MavlinkDefinitions.VIBRATION:
                ProcessVibration(packet);
                break;
            case MavlinkDefinitions.EKF_STATUS_REPORT:
                ProcessEkfStatus(packet);
                break;
            case MavlinkDefinitions.RC_CHANNELS:
                ProcessRcChannels(packet);
                break;
            case MavlinkDefinitions.STATUSTEXT:
                ProcessStatustext(packet);
                break;
            case MavlinkDefinitions.COMMAND_ACK:
                ProcessCommandAck(packet);
                break;
            case MavlinkDefinitions.PARAM_VALUE:
                ProcessParamValue(packet);
                break;
        }

        _state.IsConnected = true;
        _state.LastHeartbeat = DateTime.UtcNow;
        StateUpdated?.Invoke();
    }

    private void ProcessHeartbeat(MavlinkPacket p)
    {
        _state.SystemId = p.SystemId;
        _state.ComponentId = p.ComponentId;

        byte baseMode = p.GetByte(0);
        uint customMode = p.GetUInt32(1);

        _state.IsArmed = (baseMode & MavlinkDefinitions.MAV_MODE_FLAG_SAFETY_ARMED) != 0;
        _state.IsCustomModeEnabled = (baseMode & MavlinkDefinitions.MAV_MODE_FLAG_CUSTOM_MODE_ENABLED) != 0;
        _state.FlightMode = (int)customMode;
    }

    private void ProcessAttitude(MavlinkPacket p)
    {
        p.GetFloat(4); // time_boot_ms
        _state.Roll = (float)(p.GetFloat(8) * 180.0 / Math.PI);
        _state.Pitch = (float)(p.GetFloat(12) * 180.0 / Math.PI);
        _state.Yaw = (float)(p.GetFloat(16) * 180.0 / Math.PI);
    }

    private void ProcessGlobalPosition(MavlinkPacket p)
    {
        _state.Latitude = p.GetInt32(4) / 1e7;
        _state.Longitude = p.GetInt32(8) / 1e7;
        _state.AltitudeRel = p.GetInt32(12) / 1000f;
        _state.GroundSpeed = p.GetInt16(20) / 100f;
    }

    private void ProcessVfrHud(MavlinkPacket p)
    {
        _state.AirSpeed = p.GetFloat(4);
        _state.GroundSpeed = p.GetFloat(8);
        _state.AltitudeMsl = p.GetFloat(12);
        _state.VerticalSpeed = p.GetFloat(16);
    }

    private void ProcessSysStatus(MavlinkPacket p)
    {
        _state.BatteryVoltage = p.GetUInt16(10) / 1000f;
        _state.BatteryCurrent = p.GetInt16(12) / 100f;
        _state.BatteryRemaining = p.GetByte(14);

        _state.SensorsPresent = p.GetInt32(16);
        _state.SensorsEnabled = p.GetInt32(20);
        _state.SensorsHealth = p.GetInt32(24);
    }

    private void ProcessBatteryStatus(MavlinkPacket p)
    {
        // cell voltages start at offset 6 (8 cells * 2 bytes each)
        _state.BatteryRemaining = p.GetByte(5);
    }

    private void ProcessGpsRaw(MavlinkPacket p)
    {
        _state.GpsFix = p.GetByte(8);
        _state.Latitude = p.GetInt32(9) / 1e7;
        _state.Longitude = p.GetInt32(13) / 1e7;
        _state.AltitudeMsl = p.GetInt32(17) / 1000f;
        _state.Hdop = p.GetUInt16(21) / 100f;
        _state.SatelliteCount = p.GetByte(29);
    }

    private void ProcessScaledImu(MavlinkPacket p)
    {
        p.GetUInt16(0); // time_boot_ms
        _state.AccelX = p.GetInt16(2) / 1000f;
        _state.AccelY = p.GetInt16(4) / 1000f;
        _state.AccelZ = p.GetInt16(6) / 1000f;
        _state.GyroX = p.GetInt16(8) / 1000f;
        _state.GyroY = p.GetInt16(10) / 1000f;
        _state.GyroZ = p.GetInt16(12) / 1000f;
        _state.MagX = p.GetInt16(14) / 1000f;
        _state.MagY = p.GetInt16(16) / 1000f;
        _state.MagZ = p.GetInt16(18) / 1000f;
    }

    private void ProcessScaledImu2(MavlinkPacket p)
    {
        p.GetUInt16(0);
        _state.Accel2X = p.GetInt16(2) / 1000f;
        _state.Accel2Y = p.GetInt16(4) / 1000f;
        _state.Accel2Z = p.GetInt16(6) / 1000f;
        _state.Gyro2X = p.GetInt16(8) / 1000f;
        _state.Gyro2Y = p.GetInt16(10) / 1000f;
        _state.Gyro2Z = p.GetInt16(12) / 1000f;
    }

    private void ProcessRawImu(MavlinkPacket p)
    {
        p.GetUInt64(0); // time_usec
        _state.AccelX = p.GetInt16(8) / 1000f;
        _state.AccelY = p.GetInt16(10) / 1000f;
        _state.AccelZ = p.GetInt16(12) / 1000f;
        _state.GyroX = p.GetInt16(14) / 1000f;
        _state.GyroY = p.GetInt16(16) / 1000f;
        _state.GyroZ = p.GetInt16(18) / 1000f;
        _state.MagX = p.GetInt16(20) / 1000f;
        _state.MagY = p.GetInt16(22) / 1000f;
        _state.MagZ = p.GetInt16(24) / 1000f;
    }

    private void ProcessScaledPressure(MavlinkPacket p)
    {
        _state.PressureAbs = p.GetFloat(4);
        _state.BaroTemp = p.GetInt16(8) / 100f;
    }

    private void ProcessVibration(MavlinkPacket p)
    {
        p.GetUInt32(0); // time_boot_ms
        _state.VibeX = p.GetFloat(4);
        _state.VibeY = p.GetFloat(8);
        _state.VibeZ = p.GetFloat(12);
        _state.VibeClip0 = p.GetUInt32(16);
        _state.VibeClip1 = p.GetUInt32(20);
        _state.VibeClip2 = p.GetUInt32(24);
    }

    private void ProcessEkfStatus(MavlinkPacket p)
    {
        _state.EkfFlags = p.GetInt32(0);
        _state.EkfVelVariance = p.GetFloat(4);
        _state.EkfPosHorizVariance = p.GetFloat(8);
        _state.EkfPosVertVariance = p.GetFloat(12);
        _state.EkfCompassVariance = p.GetFloat(16);
        _state.EkfTerrainAltVariance = p.GetFloat(20);
    }

    private void ProcessRcChannels(MavlinkPacket p)
    {
        byte count = p.GetByte(20);
        for (int i = 0; i < Math.Min((int)count, 18); i++)
        {
            _state.RcChannels[i] = p.GetUInt16(2 + i * 2) / 1000f;
        }
    }

    private void ProcessStatustext(MavlinkPacket p)
    {
        byte severity = p.GetByte(0);
        // Text starts at offset 1, null-terminated
        var textBytes = new List<byte>();
        for (int i = 1; i < p.Payload.Length; i++)
        {
            byte b = p.Payload[i];
            if (b == 0) break;
            textBytes.Add(b);
        }
        string text = System.Text.Encoding.ASCII.GetString(textBytes.ToArray());
        MessageReceived?.Invoke(text, severity);
    }

    private void ProcessCommandAck(MavlinkPacket p)
    {
        byte command = p.GetByte(0);
        byte result = p.GetByte(1);
        CommandAckReceived?.Invoke(command, result, p.SystemId);
    }

    private void ProcessParamValue(MavlinkPacket p)
    {
        // PARAM_VALUE: param_id (16 bytes), param_value (float), param_type (2 bytes)
        var nameBytes = new List<byte>();
        for (int i = 0; i < 16; i++)
        {
            byte b = p.Payload[i];
            if (b == 0) break;
            nameBytes.Add(b);
        }
        string name = System.Text.Encoding.ASCII.GetString(nameBytes.ToArray());
        float value = p.GetFloat(16);
        ParameterReceived?.Invoke(name, value);
    }
}
