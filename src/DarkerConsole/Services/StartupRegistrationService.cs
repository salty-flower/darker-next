using System;
using System.Diagnostics;
using System.Runtime.Versioning;
using DarkerConsole.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Win32;

namespace DarkerConsole.Services;

public sealed class StartupRegistrationService(
    ILogger<StartupRegistrationService> logger,
    IOptionsMonitor<AppConfig> optionsMonitor
)
{
    private const string RunKeyPath = @"Software\\Microsoft\\Windows\\CurrentVersion\\Run";
    private const string AppName = "DarkerConsole";

    public void ApplyStartupPreference()
    {
        var config = optionsMonitor.CurrentValue;

        if (!OperatingSystem.IsWindows())
        {
            if (config.AutoStartup)
                logger.LogWarning("Auto-start is only supported on Windows.");
            return;
        }

        ApplyStartupPreferenceWindows(config);
    }

    [SupportedOSPlatform("windows")]
    private void ApplyStartupPreferenceWindows(AppConfig config)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);

            if (key == null)
            {
                logger.LogWarning("Unable to access registry for auto-start configuration.");
                return;
            }

            var executablePath = Environment.ProcessPath
                ?? Process.GetCurrentProcess().MainModule?.FileName;

            if (string.IsNullOrEmpty(executablePath))
            {
                logger.LogWarning("Unable to determine executable path for auto-start configuration.");
                return;
            }

            if (config.AutoStartup)
            {
                key.SetValue(AppName, executablePath);
                logger.LogInformation(
                    "Auto-start enabled with executable path {ExecutablePath}",
                    executablePath
                );
            }
            else if (key.GetValue(AppName) != null)
            {
                key.DeleteValue(AppName, false);
                logger.LogInformation("Auto-start disabled.");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to apply auto-start preference.");
        }
    }
}
