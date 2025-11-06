using System;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using DarkerConsole.Models;
using DarkerConsole.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DarkerConsole.Commands;

[SupportedOSPlatform("windows")]
public class TrayCommand(
    TrayIconService trayIconService,
    ThemeService themeService,
    ToastService toastService,
    StartupRegistrationService startupRegistrationService,
    IOptionsMonitor<AppConfig> configMon,
    ILogger<TrayCommand> logger
)
{
    private readonly CancellationTokenSource cancellationTokenSource = new();

    public async Task RunAsync()
    {
        logger.LogInformation("Starting DarkerConsole tray application");

        using var autoStartRegistration =
            configMon.OnChange((_, _) => startupRegistrationService.ApplyStartupPreference());

        try
        {
            startupRegistrationService.ApplyStartupPreference();
            await trayIconService.InitializeAsync(OnTrayIconClick, OnMenuExit);

            logger.LogInformation("Tray icon initialized successfully");

            var config = configMon.CurrentValue;
            if (config.ShowToasts && config.Toasts.ShowOnStartup)
                toastService.ShowInfoNotification(
                    "DarkerConsole",
                    "Tray application started"
                );

            SetupExitHandling();
            trayIconService.RunMessageLoop();
        }
        finally
        {
            await trayIconService.DisposeAsync();
            logger.LogInformation("DarkerConsole tray application stopped");
        }
    }

    private async Task OnTrayIconClick()
    {
        try
        {
            var wasLight = themeService.IsLightThemeEnabled();
            await themeService.ToggleThemeAsync();
            var isNowLight = themeService.IsLightThemeEnabled();

            await trayIconService.UpdateIconAsync(!isNowLight);

            var config = configMon.CurrentValue;
            if (config.ShowToasts && config.Toasts.ShowOnThemeChange)
            {
                var themeText = isNowLight ? "Light" : "Dark";
                toastService.ShowThemeChangedNotification(themeText);
            }

            logger.LogInformation(
                "Theme toggled: {From} -> {To}",
                wasLight ? "Light" : "Dark",
                isNowLight ? "Light" : "Dark"
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error toggling theme");
            var config = configMon.CurrentValue;
            if (config.ShowToasts && config.Toasts.ShowOnError)
                toastService.ShowErrorNotification("Failed to toggle theme");
        }
    }

    private void OnMenuExit()
    {
        logger.LogInformation("Exit requested from tray menu");
        trayIconService.ExitMessageLoop();
    }

    private void SetupExitHandling()
    {
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            logger.LogInformation("Ctrl+C received, shutting down gracefully");
            trayIconService.ExitMessageLoop();
        };

        cancellationTokenSource.Token.Register(() => trayIconService.ExitMessageLoop());

        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            logger.LogInformation("Process exit event received");
            trayIconService.ExitMessageLoop();
        };
    }
}
