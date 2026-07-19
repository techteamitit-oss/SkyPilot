namespace SkyPilot.Core.Mavlink;

/// <summary>
/// MAVLink message IDs and commonly used enums.
/// </summary>
public static class MavlinkDefinitions
{
    // Message IDs
    public const byte HEARTBEAT = 0;
    public const byte SYS_STATUS = 1;
    public const byte SYSTEM_TIME = 2;
    public const byte PING = 4;
    public const byte CHANGE_OPERATOR_CODE = 5;
    public const byte AUTH_KEY = 7;
    public const byte SET_MODE = 11;
    public const byte REQUEST_DATA_STREAM = 66;
    public const byte DATA_STREAM = 67;
    public const byte MISSION_ITEM = 39;
    public const byte MISSION_REQUEST = 40;
    public const byte MISSION_SET_CURRENT = 41;
    public const byte MISSION_COUNT = 44;
    public const byte MISSION_CLEAR_ALL = 45;
    public const byte GPS_RAW_INT = 24;
    public const byte GPS_STATUS = 25;
    public const byte SCALED_IMU = 26;
    public const byte RAW_IMU = 27;
    public const byte RAW_PRESSURE = 28;
    public const byte SCALED_PRESSURE = 29;
    public const byte ATTITUDE = 30;
    public const byte GLOBAL_POSITION_INT = 33;
    public const byte RC_CHANNELS_RAW = 35;
    public const byte VFR_HUD = 74;
    public const byte COMMAND_LONG = 76;
    public const byte COMMAND_ACK = 77;
    public const byte MISSION_ITEM_INT = 73;
    public const byte RC_CHANNELS = 65;
    public const byte BATTERY_STATUS = 147;
    public const byte EKF_STATUS_REPORT = 232;
    public const byte VIBRATION = 241;
    public const byte HIGHRES_IMU = 105;
    public const byte SCALED_IMU2 = 229;
    public const byte SCALED_IMU3 = 236;
    public const byte DISTANCE_SENSOR = 132;
    public const byte TERRAIN_REQUEST = 233;
    public const byte TERRAIN_REPORT = 234;
    public const byte AHRS2 = 154;
    public const ushort SYSTEM_TIME_UINT64 = 283;
    public const byte STATUSTEXT = 253;
    public const byte PARAM_VALUE = 22;
    public const byte PARAM_SET = 23;
    public const byte PARAM_REQUEST_LIST = 21;
    public const byte PARAM_REQUEST_READ = 20;

    // MAV_STATE
    public const byte MAV_STATE_UNINIT = 0;
    public const byte MAV_STATE_BOOT = 1;
    public const byte MAV_STATE_CALIBRATING = 2;
    public const byte MAV_STATE_STANDBY = 3;
    public const byte MAV_STATE_ACTIVE = 4;
    public const byte MAV_STATE_CRITICAL = 5;
    public const byte MAV_STATE_EMERGENCY = 6;
    public const byte MAV_STATE_POWEROFF = 7;

    // MAV_MODE_FLAG
    public const byte MAV_MODE_FLAG_SAFETY_ARMED = 128;
    public const byte MAV_MODE_FLAG_CUSTOM_MODE_ENABLED = 1;

    // MAV_AUTOPILOT
    public const byte MAV_AUTOPILOT_GENERIC = 0;
    public const byte MAV_AUTOPILOT_ARDUPILOTMEGA = 3;

    // MAV_TYPE
    public const byte MAV_TYPE_GENERIC = 0;
    public const byte MAV_TYPE_QUADROTOR = 2;
    public const byte MAV_TYPE_FIXED_WING = 1;
    public const byte MAV_TYPE_GROUND_ROVER = 10;
    public const byte MAV_TYPE_HEXAROTOR = 8;
    public const byte MAV_TYPE_OCTOROTOR = 14;
    public const byte MAV_TYPE_TRICOPTER = 15;

    // MAV_RESULT
    public const byte MAV_RESULT_ACCEPTED = 0;
    public const byte MAV_RESULT_TEMPORARILY_REJECTED = 1;
    public const byte MAV_RESULT_DENIED = 2;
    public const byte MAV_RESULT_UNSUPPORTED = 3;
    public const byte MAV_RESULT_FAILED = 4;
    public const byte MAV_RESULT_IN_PROGRESS = 5;

    // MAV_FRAME
    public const byte MAV_FRAME_GLOBAL = 0;
    public const byte MAV_FRAME_GLOBAL_RELATIVE_ALT = 3;
    public const byte MAV_FRAME_MISSION = 2;
    public const byte MAV_FRAME_GLOBAL_TERRAIN_ALT = 10;

    // MAV_CMD
    public const ushort MAV_CMD_NAV_WAYPOINT = 16;
    public const ushort MAV_CMD_NAV_LOITER_UNLIM = 17;
    public const ushort MAV_CMD_NAV_LOITER_TURNS = 18;
    public const ushort MAV_CMD_NAV_LOITER_TIME = 19;
    public const ushort MAV_CMD_NAV_RETURN_TO_LAUNCH = 20;
    public const ushort MAV_CMD_NAV_LAND = 21;
    public const ushort MAV_CMD_NAV_TAKEOFF = 22;
    public const ushort MAV_CMD_NAV_SET_YAW_SPEED = 213;
    public const ushort MAV_CMD_DO_SET_ROI = 201;
    public const ushort MAV_CMD_DO_CHANGE_SPEED = 178;
    public const ushort MAV_CMD_DO_SET_PARAMETER = 118;
    public const ushort MAV_CMD_DO_MOTOR_TEST = 209;
    public const ushort MAV_CMD_DO_GRIPPER = 211;
    public const ushort MAV_CMD_DO_WINCH = 210;
    public const ushort MAV_CMD_CONDITION_DELAY = 112;
    public const ushort MAV_CMD_CONDITION_YAW = 115;
    public const ushort MAV_CMD_PREFLIGHT_CALIBRATION = 241;
    public const ushort MAV_CMD_COMPONENT_ARM_DISARM = 400;

    // MAV_DATA_STREAM
    public const byte MAV_DATA_STREAM_ALL = 0;
    public const byte MAV_DATA_STREAM_RAW_SENSORS = 1;
    public const byte MAV_DATA_STREAM_EXTENDED_STATUS = 2;
    public const byte MAV_DATA_STREAM_RC_CHANNELS = 3;
    public const byte MAV_DATA_STREAM_POSITION = 6;
    public const byte MAV_DATA_STREAM_EXTRA1 = 10;
    public const byte MAV_DATA_STREAM_EXTRA2 = 11;
    public const byte MAV_DATA_STREAM_EXTRA3 = 12;

    // Flight modes (ArduCopter)
    public static readonly Dictionary<int, string> FlightModes = new()
    {
        {0, "Stabilize"}, {1, "Acro"}, {2, "AltHold"}, {3, "Auto"},
        {4, "Guided"}, {5, "Loiter"}, {6, "RTL"}, {7, "Circle"},
        {9, "Land"}, {11, "Drift"}, {13, "Sport"}, {14, "Flip"},
        {15, "AutoTune"}, {16, "PosHold"}, {17, "Brake"}, {18, "Throw"},
        {19, "AvoidADSB"}, {20, "Guided_NoGPS"}, {21, "SmartRTL"},
        {22, "FlowHold"}, {23, "Follow"}, {24, "ZigZag"}
    };

    // Sensor status flags (MAV_SYS_STATUS_SENSOR)
    public static readonly Dictionary<int, string> SensorNames = new()
    {
        {0, "Gyro"}, {1, "Accelerometer"}, {2, "Magnetometer"},
        {3, "Barometer"}, {4, "Differential Pressure"}, {5, "GPS"},
        {6, "Optical Flow"}, {7, "Vision Position"}, {8, "Laser Position"},
        {9, "Ground Truth"}, {10, "Rate Control"}, {11, "Attitude Stabilization"},
        {12, "Yaw Position"}, {13, "Altitude Control"}, {14, "XY Position Control"},
        {15, "Motor Control"}, {16, "RC Receiver"}
    };
}
