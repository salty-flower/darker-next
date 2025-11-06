using System;
using System.IO;
using DarkerConsole.Infrastructure.Logging;
using DarkerConsole.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace DarkerConsole.Infrastructure;

internal class LoggerFactoryProvider(IOptionsMonitor<AppConfig> optionsMonitor)
{
    public ILoggerFactory CreateLoggerFactory()
    {
        var config = optionsMonitor.CurrentValue;
        var minLogLevel = Enum.TryParse<LogLevel>(config.Logging.MinimumLevel, true, out var level)
            ? level
            : LogLevel.Information;

        return LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(minLogLevel);

            if (config.Logging.EnableConsoleLogging)
            {
                builder.AddSimpleConsole(options =>
                {
                    options.IncludeScopes = true;
                    options.TimestampFormat = "[HH:mm:ss.fff] ";
                    options.SingleLine = true;
                });
            }

            if (config.Logging.EnableFileLogging)
            {
                var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
                builder.AddProvider(
                    new FileLoggerProvider(logDirectory, config.Logging.RetainedFileCountLimit)
                );
            }
        });
    }
}
