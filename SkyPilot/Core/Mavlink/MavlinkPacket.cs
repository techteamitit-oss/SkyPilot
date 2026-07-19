namespace SkyPilot.Core.Mavlink;

/// <summary>
/// A decoded MAVLink v1 packet.
/// </summary>
public class MavlinkPacket
{
    public byte Sequence { get; set; }
    public byte SystemId { get; set; }
    public byte ComponentId { get; set; }
    public byte MessageId { get; set; }
    public byte[] Payload { get; set; } = Array.Empty<byte>();

    /// <summary>Extract a byte from payload at given offset.</summary>
    public byte GetByte(int offset) => Payload[offset];

    /// <summary>Extract a uint16 from payload at given offset (little-endian).</summary>
    public ushort GetUInt16(int offset) => BitConverter.ToUInt16(Payload, offset);

    /// <summary>Extract an int16 from payload at given offset (little-endian).</summary>
    public short GetInt16(int offset) => BitConverter.ToInt16(Payload, offset);

    /// <summary>Extract a uint32 from payload at given offset (little-endian).</summary>
    public uint GetUInt32(int offset) => BitConverter.ToUInt32(Payload, offset);

    /// <summary>Extract an int32 from payload at given offset (little-endian).</summary>
    public int GetInt32(int offset) => BitConverter.ToInt32(Payload, offset);

    /// <summary>Extract a float from payload at given offset (little-endian).</summary>
    public float GetFloat(int offset) => BitConverter.ToSingle(Payload, offset);

    /// <summary>Extract a uint64 from payload at given offset (little-endian).</summary>
    public ulong GetUInt64(int offset) => BitConverter.ToUInt64(Payload, offset);

    /// <summary>Extract an int64 from payload at given offset (little-endian).</summary>
    public long GetInt64(int offset) => BitConverter.ToInt64(Payload, offset);
}
