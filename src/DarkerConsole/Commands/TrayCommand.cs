using System;
using System.Threading;
using System.Threading.Tasks;
using ConsoleAppFramework;
using DarkerConsole.Models;
using DarkerConsole.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DarkerConsole.Commands;

public class TrayCommand(
    TrayIconService trayIconService,
    ThemeService themeService,
    ToastService toastService,
    IOptions<AppConfig> config,
    ILogger<TrayCommand> logger
)
{
    private readonly TrayIconService _trayIconService = trayIconService;
    private readonly ThemeService _themeService = themeService;
    private readonly ToastService _toastService = toastService;
    private readonly AppConfig _config = config.Value;
    private readonly ILogger<TrayCommand> _logger = logger;
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public async Task RunAsync()
    {
        _logger.LogInformation("Starting DarkerConsole tray application");

        try
        {
            await _trayIconService.InitializeAsync(OnTrayIconClick, OnMenuExit);

            _logger.LogInformation("Tray icon initialized successfully");

            SetupExitHandling();
            _trayIconService.RunMessageLoop();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running tray application");
            throw;
        }
        finally
        {
            await _trayIconService.DisposeAsync();
            _logger.LogInformation("DarkerConsole tray application stopped");
        }
    }

    private async Task OnTrayIconClick()
    {
        try
        {
            var wasLight = _themeService.IsLightThemeEnabled();
            await _themeService.ToggleThemeAsync();
            var isNowLight = _themeService.IsLightThemeEnabled();

            await _trayIconService.UpdateIconAsync(!isNowLight);

            if (_config.ShowToasts)
            {
                var themeText = isNowLight ? "Light" : "Dark";
                await _toastService.ShowThemeChangedNotificationAsync(themeText);
            }

            _logger.LogInformation(
                "Theme toggled: {From} -> {To}",
                wasLight ? "Light" : "Dark",
                isNowLight ? "Light" : "Dark"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling theme");
            if (_config.ShowToasts)
            {
                await _toastService.ShowErrorNotificationAsync("Failed to toggle theme");
            }
        }
    }

    private void OnMenuExit()
    {
        _logger.LogInformation("Exit requested from tray menu");
        _trayIconService.ExitMessageLoop();
    }

    private void SetupExitHandling()
    {
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            _logger.LogInformation("Ctrl+C received, shutting down gracefully");
            _trayIconService.ExitMessageLoop();
        };

        _cancellationTokenSource.Token.Register(() => _trayIconService.ExitMessageLoop());

        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            _logger.LogInformation("Process exit event received");
            _trayIconService.ExitMessageLoop();
        };
    }
}
