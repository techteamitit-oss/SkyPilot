namespace SkyPilot.Log;

/// <summary>
/// Self-contained parser for ArduPilot text (.log) log files.
/// Text logs are CSV-like: TYPE,field1,field2,...
/// FMT lines define message types.
/// </summary>
public class TextLogParser
{
    private readonly Dictionary<string, LogFormat> _formats = new();
    private readonly List<LogMessage> _messages = new();

    public IReadOnlyList<LogMessage> Messages => _messages;

    public void Parse(string filePath, Action<double>? progress = null)
    {
        _formats.Clear();
        _messages.Clear();

        var lines = File.ReadLines(filePath);
        long totalLines = 0;
        long lineCount = 0;

        // Count lines first for progress
        try { totalLines = File.ReadLines(filePath).LongCount(); } catch { }

        foreach (var line in lines)
        {
            lineCount++;
            if (progress != null && lineCount % 10000 == 0)
                progress((double)lineCount / Math.Max(totalLines, 1) * 100);

            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = line.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length < 2) continue;

            string typeName = parts[0];

            if (typeName == "FMT")
            {
                ParseFmt(parts);
            }
            else
            {
                var msg = DecodeLine(typeName, parts);
                if (msg != null)
                    _messages.Add(msg);
            }
        }

        progress?.Invoke(100);
    }

    private void ParseFmt(string[] parts)
    {
        if (parts.Length < 6) return;

        if (!byte.TryParse(parts[1], out byte typeId)) return;
        if (!byte.TryParse(parts[2], out byte length)) return;
        string name = parts[3];
        string format = parts[4];
        string labels = parts[5];

        _formats[name] = new LogFormat
        {
            TypeId = typeId,
            Length = length,
            Name = name,
            Format = format,
            Labels = labels.Split(',', StringSplitOptions.RemoveEmptyEntries)
        };
    }

    private LogMessage? DecodeLine(string typeName, string[] parts)
    {
        if (!_formats.TryGetValue(typeName, out var format))
            return null;

        var msg = new LogMessage { TypeName = typeName, TypeId = format.TypeId };
        int fieldIndex = 1; // skip type name

        for (int i = 0; i < format.Labels.Length && fieldIndex < parts.Length; i++)
        {
            string label = format.Labels[i];
            string value = parts[fieldIndex++];

            if (double.TryParse(value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double d))
            {
                msg.Fields[label] = d;
            }
            else
            {
                msg.Fields[label] = 0.0;
            }
        }

        // Extract timestamp
        if (msg.Fields.TryGetValue("TimeUS", out var timeUs))
            msg.TimeSeconds = Convert.ToDouble(timeUs) / 1_000_000.0;
        else if (msg.Fields.TryGetValue("TimeMS", out var timeMs))
            msg.TimeSeconds = Convert.ToDouble(timeMs) / 1000.0;

        return msg;
    }
}
