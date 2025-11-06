namespace DarkerConsole.Models;

public sealed class AppConfig
{
    public string ThemeMode { get; init; } = "both";
    public bool ShowToasts { get; init; } = true;
    public bool AutoStartup { get; init; } = false;
    public IconConfig Icons { get; init; } = new();
    public ToastConfig Toasts { get; init; } = new();
    public LoggingConfig Logging { get; init; } = new();
}

public sealed class IconConfig
{
    public string LightIconPath { get; init; } = "icon-light.ico";
    public string DarkIconPath { get; init; } = "icon-dark.ico";
}

public sealed class ToastConfig
{
    public bool ShowOnThemeChange { get; init; } = true;
    public bool ShowOnError { get; init; } = true;
    public bool ShowOnStartup { get; init; } = false;
    public int DurationSeconds { get; init; } = 3;
}

public sealed class LoggingConfig
{
    public string MinimumLevel { get; init; } = "Information";
    public bool EnableFileLogging { get; init; } = true;
    public bool EnableConsoleLogging { get; init; } = false;
    public int RetainedFileCountLimit { get; init; } = 7;
}
