using System.Buffers.Binary;

namespace SkyPilot.Log;

/// <summary>
/// Self-contained parser for ArduPilot binary (.bin) log files.
/// Based on the DataFlash binary log format documented in ArduPilot.
/// </summary>
public class BinaryLogParser
{
    private const byte HEAD_BYTE1 = 0xA3;
    private const byte HEAD_BYTE2 = 0x95;

    private readonly Dictionary<byte, LogFormat> _formats = new();
    private readonly List<LogMessage> _messages = new();

    public IReadOnlyList<LogMessage> Messages => _messages;
    public IReadOnlyDictionary<byte, LogFormat> Formats => _formats;

    public void Parse(string filePath, Action<double>? progress = null)
    {
        _formats.Clear();
        _messages.Clear();

        using var fs = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        long length = fs.Length;

        while (fs.Position < length)
        {
            if (progress != null && fs.Position % 100000 == 0)
                progress((double)fs.Position / length * 100);

            // Find next header bytes
            int b1 = fs.ReadByte();
            if (b1 < 0) break;
            if (b1 != HEAD_BYTE1) continue;

            int b2 = fs.ReadByte();
            if (b2 < 0) break;
            if (b2 != HEAD_BYTE2) continue;

            // Read message type and length
            int typeByte = fs.ReadByte();
            int lenByte = fs.ReadByte();
            if (typeByte < 0 || lenByte < 0) break;

            byte typeId = (byte)typeByte;
            byte payloadLen = (byte)lenByte;

            // Read payload
            byte[] payload = new byte[payloadLen];
            if (fs.Read(payload, 0, payloadLen) < payloadLen) break;

            // Read CRC (2 bytes)
            byte[] crc = new byte[2];
            fs.Read(crc, 0, 2);

            // Process FMT messages immediately to build format table
            if (typeId == 128 && payloadLen >= 89)
            {
                var fmt = ParseFmt(payload);
                if (fmt != null)
                    _formats[fmt.TypeId] = fmt;
            }

            // Decode message if we have its format
            if (_formats.TryGetValue(typeId, out var format))
            {
                var msg = DecodeMessage(typeId, format, payload);
                if (msg != null)
                    _messages.Add(msg);
            }
        }

        progress?.Invoke(100);
    }

    private LogFormat? ParseFmt(byte[] payload)
    {
        if (payload.Length < 89) return null;

        byte typeId = payload[0];
        byte length = payload[1];

        string name = System.Text.Encoding.ASCII.GetString(payload, 2, 4).TrimEnd('\0');
        string format = System.Text.Encoding.ASCII.GetString(payload, 6, 16).TrimEnd('\0');
        string labels = System.Text.Encoding.ASCII.GetString(payload, 22, 64).TrimEnd('\0');

        return new LogFormat
        {
            TypeId = typeId,
            Length = length,
            Name = name,
            Format = format,
            Labels = labels.Split(',', StringSplitOptions.RemoveEmptyEntries)
        };
    }

    private LogMessage? DecodeMessage(byte typeId, LogFormat format, byte[] payload)
    {
        if (payload.Length < format.PayloadSize) return null;

        var msg = new LogMessage { TypeId = typeId, TypeName = format.Name };
        int offset = 0;

        for (int i = 0; i < format.Format.Length && i < format.Labels.Length; i++)
        {
            char c = format.Format[i];
            string label = format.Labels[i];

            if (offset + LogFormat.FormatCharSize(c) > payload.Length)
                break;

            object value = c switch
            {
                'b' => (double)payload[offset],
                'B' => (double)payload[offset],
                'h' => (double)BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(offset)),
                'H' => (double)BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(offset)),
                'i' => (double)BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(offset)),
                'I' => (double)BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(offset)),
                'q' => (double)BinaryPrimitives.ReadInt64LittleEndian(payload.AsSpan(offset)),
                'Q' => (double)BinaryPrimitives.ReadUInt64LittleEndian(payload.AsSpan(offset)),
                'f' => (double)BitConverter.ToSingle(payload, offset),
                'd' => BitConverter.ToDouble(payload, offset),
                'E' => (double)BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(offset)),
                'L' => (double)BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(offset)),
                'C' => (double)payload[offset],
                _ => 0.0
            };

            msg.Fields[label] = value;
            offset += LogFormat.FormatCharSize(c);
        }

        // Extract timestamp if available
        if (msg.Fields.TryGetValue("TimeUS", out var timeUs))
            msg.TimeSeconds = (double)timeUs / 1_000_000.0;
        else if (msg.Fields.TryGetValue("TimeMS", out var timeMs))
            msg.TimeSeconds = (double)timeMs / 1000.0;

        return msg;
    }
}
