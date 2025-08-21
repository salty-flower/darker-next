namespace DarkerConsole.Models;

public class AppConfig
{
    public string ThemeMode { get; set; } = "both";

    public bool ShowToasts { get; set; } = true;

    public bool AutoStartup { get; set; } = false;

    public IconConfig Icons { get; set; } = new();

    public ToastConfig Toasts { get; set; } = new();

    public LoggingConfig Logging { get; set; } = new();
}

public class IconConfig
{
    public string LightIconPath { get; set; } = "Resources/icon-light.ico";

    public string DarkIconPath { get; set; } = "Resources/icon-dark.ico";
}

public class ToastConfig
{
    public bool ShowOnThemeChange { get; set; } = true;

    public bool ShowOnError { get; set; } = true;

    public bool ShowOnStartup { get; set; } = false;

    public int DurationSeconds { get; set; } = 3;
}

public class LoggingConfig
{
    public string MinimumLevel { get; set; } = "Information";

    public bool EnableFileLogging { get; set; } = true;

    public bool EnableConsoleLogging { get; set; } = false;

    public int RetainedFileCountLimit { get; set; } = 7;
}
