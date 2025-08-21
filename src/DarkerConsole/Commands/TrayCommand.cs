using System;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using ConsoleAppFramework;
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
    IOptionsMonitor<AppConfig> configMon,
    ILogger<TrayCommand> logger
)
{
    private readonly CancellationTokenSource cancellationTokenSource = new();

    public async Task RunAsync()
    {
        logger.LogInformation("Starting DarkerConsole tray application");

        try
        {
            await trayIconService.InitializeAsync(OnTrayIconClick, OnMenuExit);

            logger.LogInformation("Tray icon initialized successfully");

            SetupExitHandling();
            trayIconService.RunMessageLoop();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error running tray application");
            throw;
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

            if (configMon.CurrentValue.Toasts.ShowOnThemeChange)
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
            if (configMon.CurrentValue.Toasts.ShowOnError)
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
