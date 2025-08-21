using System;
using Microsoft.Extensions.Logging;

namespace DarkerConsole.Services;

public class ToastService(ILogger<ToastService> logger)
{
    public void ShowThemeChangedNotification(string themeName)
    {
        try
        {
            logger.LogInformation("Theme switched to {Theme}", themeName);
            // Note: Toast notifications require UWP APIs that may not work in console apps
            // For now, we'll just log the notification
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to show theme change notification");
        }
    }

    public void ShowErrorNotification(string message)
    {
        try
        {
            logger.LogError("Error: {Message}", message);
            // Note: Toast notifications require UWP APIs that may not work in console apps
            // For now, we'll just log the error
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to show error notification");
        }
    }

    public void ShowInfoNotification(string title, string message)
    {
        try
        {
            logger.LogInformation("{Title}: {Message}", title, message);
            // Note: Toast notifications require UWP APIs that may not work in console apps
            // For now, we'll just log the info
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to show info notification");
        }
    }

    public void Dispose()
    {
        try
        {
            logger.LogDebug("Toast notification system disposed");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error disposing toast notification system");
        }
    }
}
