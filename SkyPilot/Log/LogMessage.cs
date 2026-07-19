namespace SkyPilot.Log;

/// <summary>
/// A single parsed log message.
/// </summary>
public class LogMessage
{
    public DateTime Timestamp { get; set; }
    public double TimeSeconds { get; set; }
    public string TypeName { get; set; } = "";
    public byte TypeId { get; set; }
    public Dictionary<string, object> Fields { get; set; } = new();

    public double GetDouble(string field) =>
        Fields.TryGetValue(field, out var v) && v is double d ? d : 0;

    public float GetFloat(string field) => (float)GetDouble(field);

    public int GetInt(string field) =>
        Fields.TryGetValue(field, out var v) && v is double d ? (int)d : 0;

    public uint GetUInt(string field) =>
        Fields.TryGetValue(field, out var v) && v is double d ? (uint)d : 0;

    public long GetLong(string field) =>
        Fields.TryGetValue(field, out var v) && v is double d ? (long)d : 0;
}
