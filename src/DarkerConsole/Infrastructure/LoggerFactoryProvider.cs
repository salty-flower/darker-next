using System;
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
            builder
                .AddSimpleConsole(options =>
                {
                    options.IncludeScopes = true;
                    options.TimestampFormat = "[HH:mm:ss.fff] ";
                    options.SingleLine = true;
                })
                .SetMinimumLevel(minLogLevel)
        );
    }
}
