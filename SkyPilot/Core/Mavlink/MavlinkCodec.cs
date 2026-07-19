using System.Buffers.Binary;

namespace SkyPilot.Core.Mavlink;

/// <summary>
/// MAVLink v2 packet structure and codec.
/// </summary>
public static class MavlinkCodec
{
    public const byte StartByte = 0xFE;
    public const int HeaderLength = 6;  // v1 header
    public const int CrcLength = 2;
    public const int MaxPayload = 255;

    /// <summary>
    /// Parse a MAVLink v1 packet from a byte buffer.
    /// Returns null if the packet is invalid.
    /// </summary>
    public static MavlinkPacket? Decode(byte[] buffer, int offset, int length)
    {
        if (length < HeaderLength + CrcLength)
            return null;

        // Find start byte
        int pos = offset;
        while (pos < offset + length && buffer[pos] != StartByte)
            pos++;

        if (pos + HeaderLength + CrcLength > offset + length)
            return null;

        byte magic = buffer[pos];
        byte payloadLength = buffer[pos + 1];
        byte sequence = buffer[pos + 2];
        byte systemId = buffer[pos + 3];
        byte componentId = buffer[pos + 4];
        byte messageId = buffer[pos + 5];

        int totalLength = HeaderLength + payloadLength + CrcLength;
        if (pos + totalLength > offset + length)
            return null;

        // Extract payload
        byte[] payload = new byte[payloadLength];
        Array.Copy(buffer, pos + HeaderLength, payload, 0, payloadLength);

        // Verify CRC
        ushort receivedCrc = BinaryPrimitives.ReadUInt16LittleEndian(
            buffer.AsSpan(pos + HeaderLength + payloadLength, 2));
        ushort computedCrc = ComputeCrc(buffer, pos, HeaderLength + payloadLength);

        if (receivedCrc != computedCrc)
            return null;

        return new MavlinkPacket
        {
            Sequence = sequence,
            SystemId = systemId,
            ComponentId = componentId,
            MessageId = messageId,
            Payload = payload
        };
    }

    /// <summary>
    /// Encode a MAVLink v1 packet.
    /// </summary>
    public static byte[] Encode(byte systemId, byte componentId, byte messageId, byte[] payload)
    {
        byte sequence = (byte)(DateTime.Now.Millisecond & 0xFF);
        int totalLength = HeaderLength + payload.Length + CrcLength;
        byte[] buffer = new byte[totalLength];

        buffer[0] = StartByte;
        buffer[1] = (byte)payload.Length;
        buffer[2] = sequence;
        buffer[3] = systemId;
        buffer[4] = componentId;
        buffer[5] = messageId;

        Array.Copy(payload, 0, buffer, HeaderLength, payload.Length);

        ushort crc = ComputeCrc(buffer, 0, HeaderLength + payload.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(HeaderLength + payload.Length), crc);

        return buffer;
    }

    private static ushort ComputeCrc(byte[] buffer, int offset, int length)
    {
        ushort crc = 0xFFFF;
        for (int i = offset; i < offset + length; i++)
        {
            crc = CrcAccumulate(buffer[i], crc);
        }
        return crc;
    }

    private static ushort CrcAccumulate(byte data, ushort crc)
    {
        data ^= (byte)(crc & 0xFF);
        data ^= (byte)(data << 4);
        crc = (ushort)((crc >> 8) ^ (data << 8) ^ (data << 3) ^ (data >> 4));
        return crc;
    }
}
