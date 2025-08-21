using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.Versioning;
using DarkerConsole.Commands;
using DarkerConsole.Infrastructure.Configuration;
using DarkerConsole.Models;
using DarkerConsole.Services;
using Jab;
using Microsoft.Extensions.Configuration;
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
[Singleton(typeof(IOptionsMonitor<AppConfig>), typeof(TomlOptionsMonitor))]
[Singleton(typeof(IConfiguration), Factory = nameof(CreateConfiguration))]
[SupportedOSPlatform("windows")]
internal partial class ServiceProvider
{
    private static ILoggerFactory CreateLoggerFactory() =>
        LoggerFactory.Create(builder =>
            builder.AddSimpleConsole(options =>
            {
                options.IncludeScopes = true;
                options.TimestampFormat = "[HH:mm:ss.fff] ";
                options.SingleLine = true;
            })
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

    private static IConfiguration CreateConfiguration() =>
        new ConfigurationBuilder()
            .AddTomlFile(
                Path.Combine(AppContext.BaseDirectory, "config.toml"),
                optional: true,
                reloadOnChange: true
            )
            .Build();
}
