using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace DarkerConsole.Infrastructure;

public class CompactConsoleFormatterOptions : ConsoleFormatterOptions { }

public sealed class CompactConsoleFormatter : ConsoleFormatter
{
    public CompactConsoleFormatter(IOptionsMonitor<CompactConsoleFormatterOptions> options)
        : base("custom") { }

    public override void Write<TState>(
        in LogEntry<TState> logEntry,
        IExternalScopeProvider? scopeProvider,
        TextWriter textWriter
    )
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var level = GetLogLevelString(logEntry.LogLevel);
        var category = GetShortCategoryName(logEntry.Category);
        var callerInfo = GetCallerInfo(logEntry.State);

        textWriter.Write(
            $"[{timestamp}] {level} {category}{callerInfo}: {logEntry.Formatter(logEntry.State, logEntry.Exception)}"
        );

        if (logEntry.Exception != null)
        {
            textWriter.Write($" | {logEntry.Exception}");
        }

        textWriter.WriteLine();
    }

    private static string GetLogLevelString(LogLevel logLevel) =>
        logLevel switch
        {
            LogLevel.Trace => "TRC",
            LogLevel.Debug => "DBG",
            LogLevel.Information => "INF",
            LogLevel.Warning => "WRN",
            LogLevel.Error => "ERR",
            LogLevel.Critical => "CRT",
            _ => "UNK",
        };

    private static string GetShortCategoryName(string category)
    {
        var lastDot = category.LastIndexOf('.');
        return lastDot >= 0 ? category[(lastDot + 1)..] : category;
    }

    private static string GetCallerInfo<TState>(TState state)
    {
        // Try to extract caller info from state if it's a formatted log message
        if (state is IReadOnlyList<KeyValuePair<string, object>> formattedLogValues)
        {
            foreach (var kvp in formattedLogValues)
            {
                if (kvp.Key == "{OriginalFormat}" && kvp.Value is string format)
                {
                    // Extract method name from common logging patterns
                    if (format.Contains("WindowProc"))
                        return ".WindowProc";
                    if (format.Contains("Initialize"))
                        return ".Initialize";
                    if (format.Contains("Toggle"))
                        return ".Toggle";
                    if (format.Contains("Exit"))
                        return ".Exit";
                }
            }
        }

        return "";
    }
}
