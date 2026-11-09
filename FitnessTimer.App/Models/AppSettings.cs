namespace FitnessTimer.App.Models;

public sealed class AppSettings
{
    public int PreparationSeconds { get; set; }

    public string LastPage { get; set; } = string.Empty;

    public bool CompactAlwaysOnTop { get; set; }

    public bool UseDarkMode { get; set; }

    public double? CompactLeft { get; set; }

    public double? CompactTop { get; set; }

    public double? CompactWidth { get; set; }

    public double? CompactHeight { get; set; }

    public bool IsCompactMode { get; set; }

    public static AppSettings CreateDefault()
    {
        return new AppSettings
        {
            PreparationSeconds = 5,
            LastPage = "Timer",
            CompactAlwaysOnTop = false,
            UseDarkMode = false,
            CompactLeft = null,
            CompactTop = null,
            CompactWidth = null,
            CompactHeight = null,
            IsCompactMode = false
        };
    }
}
