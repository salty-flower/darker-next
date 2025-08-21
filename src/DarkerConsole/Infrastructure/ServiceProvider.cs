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
            var tomlContent = File.ReadAllText(configPath);
            config = Tomlyn.Toml.ToModel<AppConfig>(tomlContent);
        }
        else
        {
            config = new AppConfig();
        }

        return Options.Create(config);
    }
}
