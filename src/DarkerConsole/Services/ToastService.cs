using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DarkerConsole.Services;

public class ToastService(ILogger<ToastService> logger)
{
    private readonly ILogger<ToastService> _logger = logger;

    public async Task ShowThemeChangedNotificationAsync(string themeName)
    {
        await Task.Run(() =>
        {
            try
            {
                _logger.LogInformation("Theme switched to {Theme}", themeName);
                // Note: Toast notifications require UWP APIs that may not work in console apps
                // For now, we'll just log the notification
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to show theme change notification");
            }
        });
    }

    public async Task ShowErrorNotificationAsync(string message)
    {
        await Task.Run(() =>
        {
            try
            {
                _logger.LogError("Error: {Message}", message);
                // Note: Toast notifications require UWP APIs that may not work in console apps
                // For now, we'll just log the error
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to show error notification");
            }
        });
    }

    public async Task ShowInfoNotificationAsync(string title, string message)
    {
        await Task.Run(() =>
        {
            try
            {
                _logger.LogInformation("{Title}: {Message}", title, message);
                // Note: Toast notifications require UWP APIs that may not work in console apps
                // For now, we'll just log the info
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to show info notification");
            }
        });
    }

    public void Dispose()
    {
        try
        {
            _logger.LogDebug("Toast notification system disposed");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disposing toast notification system");
        }
    }
}
