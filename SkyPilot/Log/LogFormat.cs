namespace SkyPilot.Log;

/// <summary>
/// FMT message definition: maps a type_id to a name, format string, and column labels.
/// </summary>
public class LogFormat
{
    public byte TypeId { get; set; }
    public byte Length { get; set; }
    public string Name { get; set; } = "";
    public string Format { get; set; } = "";
    public string[] Labels { get; set; } = Array.Empty<string>();

    /// <summary>Get the byte size of a single field from the format character.</summary>
    public static int FormatCharSize(char c) => c switch
    {
        'b' => 1, 'B' => 1,
        'h' => 2, 'H' => 2,
        'i' => 4, 'I' => 4,
        'q' => 8, 'Q' => 8,
        'f' => 4, 'd' => 8,
        'n' => 4, 'N' => 16, 'Z' => 64,
        'C' => 1, 'E' => 4,
        'L' => 4,
        _ => 1
    };

    /// <summary>Get total payload size from the format string.</summary>
    public int PayloadSize => Format.Sum(FormatCharSize);
}
