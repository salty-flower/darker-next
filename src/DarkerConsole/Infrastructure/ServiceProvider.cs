using System;
using System.Collections.Generic;
using System.IO;
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
internal partial class ServiceProvider
{
    private static ILogger<TrayIconService> CreateTrayIconLogger()
    {
        return LoggerFactory
            .Create(builder =>
                builder
                    .AddConsole(options =>
                    {
                        options.FormatterName = "custom";
                    })
                    .AddConsoleFormatter<CompactConsoleFormatter, CompactConsoleFormatterOptions>()
            )
            .CreateLogger<TrayIconService>();
    }

    private static ILogger<ThemeService> CreateThemeServiceLogger()
    {
        return LoggerFactory
            .Create(builder =>
                builder
                    .AddConsole(options =>
                    {
                        options.FormatterName = "custom";
                    })
                    .AddConsoleFormatter<CompactConsoleFormatter, CompactConsoleFormatterOptions>()
            )
            .CreateLogger<ThemeService>();
    }

    private static ILogger<ToastService> CreateToastServiceLogger()
    {
        return LoggerFactory
            .Create(builder =>
                builder
                    .AddConsole(options =>
                    {
                        options.FormatterName = "custom";
                    })
                    .AddConsoleFormatter<CompactConsoleFormatter, CompactConsoleFormatterOptions>()
            )
            .CreateLogger<ToastService>();
    }

    private static ILogger<TrayCommand> CreateTrayCommandLogger()
    {
        return LoggerFactory
            .Create(builder =>
                builder
                    .AddConsole(options =>
                    {
                        options.FormatterName = "custom";
                    })
                    .AddConsoleFormatter<CompactConsoleFormatter, CompactConsoleFormatterOptions>()
            )
            .CreateLogger<TrayCommand>();
    }

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
