using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.Versioning;
using DarkerConsole.Commands;
using DarkerConsole.Models;
using DarkerConsole.Services;
using Jab;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using Tomlyn;
using Tomlyn.Model;

namespace DarkerConsole.Infrastructure;

[ServiceProvider]
[Singleton(typeof(ILogger<TrayIconService>), Factory = nameof(CreateTrayIconLogger))]
[Singleton(typeof(ILogger<ThemeService>), Factory = nameof(CreateThemeServiceLogger))]
[Singleton(typeof(ILogger<ToastService>), Factory = nameof(CreateToastServiceLogger))]
[Singleton(typeof(ILogger<TrayCommand>), Factory = nameof(CreateTrayCommandLogger))]
[Singleton(typeof(TrayIconService))]
[Singleton(typeof(ThemeService))]
[Singleton(typeof(ToastService))]
[Singleton(typeof(TrayCommand))]
[Singleton(typeof(IOptions<AppConfig>), Factory = nameof(CreateAppConfig))]
[SupportedOSPlatform("windows")]
internal partial class ServiceProvider
{
    [UnconditionalSuppressMessage(
        "Aot",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "CompactConsoleFormatterOptions is simple and statically analyzable"
    )]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "CompactConsoleFormatterOptions is simple and statically analyzable"
    )]
    private static ILoggerFactory CreateLoggerFactory() =>
        LoggerFactory.Create(builder =>
            builder
                .AddConsole(options =>
                {
                    options.FormatterName = "custom";
                })
                .AddConsoleFormatter<CompactConsoleFormatter, CompactConsoleFormatterOptions>()
        );

    [UnconditionalSuppressMessage(
        "Aot",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "The options pattern is not used here"
    )]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "CompactConsoleFormatterOptions is simple and can be statically analyzed"
    )]
    private static ILogger<TrayIconService> CreateTrayIconLogger() =>
        CreateLoggerFactory().CreateLogger<TrayIconService>();

    [UnconditionalSuppressMessage(
        "Aot",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "The options pattern is not used here"
    )]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "CompactConsoleFormatterOptions is simple and can be statically analyzed"
    )]
    private static ILogger<ThemeService> CreateThemeServiceLogger() =>
        CreateLoggerFactory().CreateLogger<ThemeService>();

    [UnconditionalSuppressMessage(
        "Aot",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "The options pattern is not used here"
    )]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "CompactConsoleFormatterOptions is simple and can be statically analyzed"
    )]
    private static ILogger<ToastService> CreateToastServiceLogger() =>
        CreateLoggerFactory().CreateLogger<ToastService>();

    [UnconditionalSuppressMessage(
        "Aot",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "The options pattern is not used here"
    )]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "CompactConsoleFormatterOptions is simple and can be statically analyzed"
    )]
    private static ILogger<TrayCommand> CreateTrayCommandLogger() =>
        CreateLoggerFactory().CreateLogger<TrayCommand>();

    private static IOptions<AppConfig> CreateAppConfig()
    {
        // Load TOML configuration
        var configPath = Path.Combine(AppContext.BaseDirectory, "config.toml");
        AppConfig config;

        if (File.Exists(configPath))
        {
            try
            {
                var tomlContent = File.ReadAllText(configPath);
                var tomlModel = Toml.ToModel(tomlContent);
                config = MapTomlToConfig(tomlModel);
            }
            catch
            {
                // Fallback to default on any parsing error
                config = new AppConfig();
            }
        }
        else
        {
            config = new AppConfig();
        }

        return Options.Create(config);
    }

    private static AppConfig MapTomlToConfig(object tomlModel)
    {
        if (tomlModel is not TomlTable tomlTable)
        {
            return new AppConfig();
        }

        var themeMode = GetTomlStringValue(tomlTable, "theme_mode", "both");
        var showToasts = GetTomlBoolValue(tomlTable, "show_toasts", true);
        var autoStartup = GetTomlBoolValue(tomlTable, "auto_startup", false);

        var icons = new IconConfig();
        if (tomlTable.TryGetValue("icons", out var iconsObj) && iconsObj is TomlTable iconsTable)
        {
            var lightPath = GetTomlStringValue(
                iconsTable,
                "light_icon_path",
                "Resources/icon-light.ico"
            );
            var darkPath = GetTomlStringValue(
                iconsTable,
                "dark_icon_path",
                "Resources/icon-dark.ico"
            );
            icons = new IconConfig { LightIconPath = lightPath, DarkIconPath = darkPath };
        }

        var toasts = new ToastConfig();
        if (
            tomlTable.TryGetValue("toasts", out var toastsObj) && toastsObj is TomlTable toastsTable
        )
        {
            var showOnThemeChange = GetTomlBoolValue(toastsTable, "show_on_theme_change", true);
            var showOnError = GetTomlBoolValue(toastsTable, "show_on_error", true);
            var showOnStartup = GetTomlBoolValue(toastsTable, "show_on_startup", false);
            var duration = GetTomlIntValue(toastsTable, "duration_seconds", 3);
            toasts = new ToastConfig
            {
                ShowOnThemeChange = showOnThemeChange,
                ShowOnError = showOnError,
                ShowOnStartup = showOnStartup,
                DurationSeconds = duration,
            };
        }

        var logging = new LoggingConfig();
        if (
            tomlTable.TryGetValue("logging", out var loggingObj)
            && loggingObj is TomlTable loggingTable
        )
        {
            var minLevel = GetTomlStringValue(loggingTable, "minimum_level", "Information");
            var enableFile = GetTomlBoolValue(loggingTable, "enable_file_logging", true);
            var enableConsole = GetTomlBoolValue(loggingTable, "enable_console_logging", false);
            var retainedCount = GetTomlIntValue(loggingTable, "retained_file_count_limit", 7);
            logging = new LoggingConfig
            {
                MinimumLevel = minLevel,
                EnableFileLogging = enableFile,
                EnableConsoleLogging = enableConsole,
                RetainedFileCountLimit = retainedCount,
            };
        }

        return new AppConfig
        {
            ThemeMode = themeMode,
            ShowToasts = showToasts,
            AutoStartup = autoStartup,
            Icons = icons,
            Toasts = toasts,
            Logging = logging,
        };
    }

    private static string GetTomlStringValue(TomlTable table, string key, string defaultValue)
    {
        return table.TryGetValue(key, out var value) && value is string str ? str : defaultValue;
    }

    private static bool GetTomlBoolValue(TomlTable table, string key, bool defaultValue)
    {
        return table.TryGetValue(key, out var value) && value is bool b ? b : defaultValue;
    }

    private static int GetTomlIntValue(TomlTable table, string key, int defaultValue)
    {
        return table.TryGetValue(key, out var value) && value is long l ? (int)l : defaultValue;
    }
}
