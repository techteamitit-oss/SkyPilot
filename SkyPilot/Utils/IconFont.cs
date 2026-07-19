namespace SkyPilot.Utils;

/// <summary>
/// Unicode symbols for sidebar navigation icons.
/// Use Segoe MDL2 Assets or fallback to Unicode.
/// </summary>
public static class IconFont
{
    // Navigation icons (Segoe MDL2 Assets or Unicode fallback)
    public const string Overview = "\uE80F";    // View / Dashboard
    public const string Sensors = "\uE9D9";     // Gauge
    public const string Mission = "\uE81D";     // Map / Route
    public const string Messages = "\uE8D2";    // Message
    public const string Params = "\uE713";      // Settings
    public const string Logs = "\uE81E";        // Document

    // Action icons
    public const string Connect = "\uE707";     // Plug
    public const string Disconnect = "\uE708";  // Unplug
    public const string Arm = "\uE768";         // Power
    public const string Disarm = "\uE7E8";      // Stop
    public const string Takeoff = "\uE709";     // Up arrow
    public const string RTL = "\uE72A";         // Return
    public const string Emergency = "\uE7BA";   // Warning

    // System icons
    public const string Minimize = "\uE921";
    public const string Maximize = "\uE922";
    public const string Restore = "\uE923";
    public const string Close = "\uE8BB";
    public const string Pin = "\uE840";

    // Status indicators (Unicode circles)
    public const string CircleFilled = "\u25CF";
    public const string CircleOutline = "\u25CB";
    public const string Diamond = "\u25C6";
    public const string Triangle = "\u25B2";
    public const string Square = "\u25A0";

    // Try Segoe MDL2 Assets first, fall back to basic symbols
    public static readonly Font IconFontSmall = GetIconFont(12f);
    public static readonly Font IconFontMedium = GetIconFont(16f);
    public static readonly Font IconFontLarge = GetIconFont(20f);

    private static Font GetIconFont(float size)
    {
        try
        {
            var font = new Font("Segoe MDL2 Assets", size);
            if (font.Name != "Segoe MDL2 Assets")
            {
                font.Dispose();
                return new Font("Segoe UI", size);
            }
            return font;
        }
        catch
        {
            return new Font("Segoe UI", size);
        }
    }
}
