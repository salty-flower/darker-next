using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using DarkerConsole.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Win32;

namespace DarkerConsole.Services;

[SupportedOSPlatform("windows")]
public class ThemeService(ILogger<ThemeService> logger, IOptionsMonitor<AppConfig> configMon)
{
    private const string PERSONALIZE_KEY =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string SYSTEM_THEME_VALUE = "SystemUsesLightTheme";
    private const string APPS_THEME_VALUE = "AppsUseLightTheme";
    private const int LIGHT_THEME = 1;
    private const int DARK_THEME = 0;

    public bool IsLightThemeEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PERSONALIZE_KEY, false);
            if (key == null)
            {
                logger.LogWarning("Personalization registry key not found, assuming dark theme");
                return false;
            }

            var systemTheme = (int)(key.GetValue(SYSTEM_THEME_VALUE) ?? DARK_THEME);
            var appsTheme = (int)(key.GetValue(APPS_THEME_VALUE) ?? DARK_THEME);

            return configMon.CurrentValue.ThemeMode switch
            {
                "system-only" => systemTheme == LIGHT_THEME,
                "apps-only" => appsTheme == LIGHT_THEME,
                _ => systemTheme == LIGHT_THEME || appsTheme == LIGHT_THEME,
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reading theme from registry");
            return false;
        }
    }

    public async Task ToggleThemeAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(PERSONALIZE_KEY, true);
                if (key == null)
                {
                    logger.LogError("Cannot open personalization registry key for writing");
                    throw new InvalidOperationException("Cannot access Windows theme settings");
                }

                var currentSystemValue = (int)(key.GetValue(SYSTEM_THEME_VALUE) ?? DARK_THEME);
                var currentAppsValue = (int)(key.GetValue(APPS_THEME_VALUE) ?? DARK_THEME);

                var isCurrentlyLight = configMon.CurrentValue.ThemeMode switch
                {
                    "system-only" => currentSystemValue == LIGHT_THEME,
                    "apps-only" => currentAppsValue == LIGHT_THEME,
                    _ => currentSystemValue == LIGHT_THEME || currentAppsValue == LIGHT_THEME,
                };

                var newValue = isCurrentlyLight ? DARK_THEME : LIGHT_THEME;

                switch (configMon.CurrentValue.ThemeMode)
                {
                    case "system-only":
                        key.SetValue(SYSTEM_THEME_VALUE, newValue, RegistryValueKind.DWord);
                        logger.LogInformation(
                            "System theme changed to {Theme}",
                            newValue == LIGHT_THEME ? "Light" : "Dark"
                        );
                        break;

                    case "apps-only":
                        key.SetValue(APPS_THEME_VALUE, newValue, RegistryValueKind.DWord);
                        logger.LogInformation(
                            "Apps theme changed to {Theme}",
                            newValue == LIGHT_THEME ? "Light" : "Dark"
                        );
                        break;

                    case "both":
                    default:
                        key.SetValue(SYSTEM_THEME_VALUE, newValue, RegistryValueKind.DWord);
                        key.SetValue(APPS_THEME_VALUE, newValue, RegistryValueKind.DWord);
                        logger.LogInformation(
                            "Both system and apps theme changed to {Theme}",
                            newValue == LIGHT_THEME ? "Light" : "Dark"
                        );
                        break;
                }

                NotifySystemOfThemeChange();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error toggling theme in registry");
                throw;
            }
        });
    }

    public async Task SetThemeAsync(bool isLight)
    {
        await Task.Run(() =>
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(PERSONALIZE_KEY, true);
                if (key == null)
                {
                    logger.LogError("Cannot open personalization registry key for writing");
                    throw new InvalidOperationException("Cannot access Windows theme settings");
                }

                var value = isLight ? LIGHT_THEME : DARK_THEME;

                switch (configMon.CurrentValue.ThemeMode)
                {
                    case "system-only":
                        key.SetValue(SYSTEM_THEME_VALUE, value, RegistryValueKind.DWord);
                        logger.LogInformation(
                            "System theme set to {Theme}",
                            isLight ? "Light" : "Dark"
                        );
                        break;

                    case "apps-only":
                        key.SetValue(APPS_THEME_VALUE, value, RegistryValueKind.DWord);
                        logger.LogInformation(
                            "Apps theme set to {Theme}",
                            isLight ? "Light" : "Dark"
                        );
                        break;

                    case "both":
                    default:
                        key.SetValue(SYSTEM_THEME_VALUE, value, RegistryValueKind.DWord);
                        key.SetValue(APPS_THEME_VALUE, value, RegistryValueKind.DWord);
                        logger.LogInformation(
                            "Both system and apps theme set to {Theme}",
                            isLight ? "Light" : "Dark"
                        );
                        break;
                }

                NotifySystemOfThemeChange();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error setting theme in registry");
                throw;
            }
        });
    }

    private void NotifySystemOfThemeChange()
    {
        try
        {
            SendSettingChangeMessage();
            logger.LogDebug("Notified system of theme change");
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to notify system of theme change, changes may require restart"
            );
        }
    }

    private static void SendSettingChangeMessage()
    {
        const int HWND_BROADCAST = 0xFFFF;
        const int WM_SETTINGCHANGE = 0x001A;
        const int SMTO_NORMAL = 0x0000;
        const int SMTO_BLOCK = 0x0001;
        const int SMTO_ABORTIFHUNG = 0x0002;
        const int SMTO_NOTIMEOUTIFNOTHUNG = 0x0008;

        var flags = SMTO_NORMAL | SMTO_BLOCK | SMTO_ABORTIFHUNG | SMTO_NOTIMEOUTIFNOTHUNG;

        SendMessageTimeout(
            new IntPtr(HWND_BROADCAST),
            WM_SETTINGCHANGE,
            IntPtr.Zero,
            "ImmersiveColorSet",
            flags,
            5000,
            out _
        );
    }

    [DllImport("user32.dll", EntryPoint = "SendMessageTimeoutW", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        int msg,
        IntPtr wParam,
        string lParam,
        int flags,
        int timeout,
        out IntPtr result
    );
}
