using System;
using System.Threading;
using DarkerConsole.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace DarkerConsole.Infrastructure.Configuration;

/// <summary>
/// Custom IOptionsMonitor implementation that directly binds to configuration
/// without requiring the full DI container infrastructure
/// </summary>
internal sealed class TomlOptionsMonitor : IOptionsMonitor<AppConfig>, IDisposable
{
    private readonly IConfiguration configuration;
    private readonly Lock @lock = new();
    private AppConfig? currentValue;
    private IDisposable? changeToken;
    private event Action<AppConfig, string?>? onChange;
    private bool disposed;

    public TomlOptionsMonitor(IConfiguration configuration)
    {
        this.configuration =
            configuration ?? throw new ArgumentNullException(nameof(configuration));

        // Set up change tracking
        changeToken = this.configuration
            .GetReloadToken()
            .RegisterChangeCallback(_ => OnConfigurationChanged(), null);
    }

    public AppConfig CurrentValue
    {
        get
        {
            if (currentValue == null)
                lock (@lock)

                    currentValue ??= LoadConfiguration();

            return currentValue;
        }
    }

    public AppConfig Get(string? name) => CurrentValue;

    public IDisposable OnChange(Action<AppConfig, string?> listener)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        onChange += listener;
        return new ChangeTokenDisposable(() => onChange -= listener);
    }

    private AppConfig LoadConfiguration()
    {
        // Manual binding - AOT friendly, no reflection
        var themeMode = configuration["ThemeMode"] ?? "both";
        var showToasts = !bool.TryParse(configuration["ShowToasts"], out var st) || st;
        var autoStartup = bool.TryParse(configuration["AutoStartup"], out var au) && au;

        var icons = new IconConfig
        {
            LightIconPath = configuration["Icons:LightIconPath"] ?? "icon-light.ico",
            DarkIconPath = configuration["Icons:DarkIconPath"] ?? "icon-dark.ico",
        };

        var toasts = new ToastConfig
        {
            ShowOnThemeChange =
                !bool.TryParse(configuration["Toasts:ShowOnThemeChange"], out var stc) || stc,
            ShowOnError = !bool.TryParse(configuration["Toasts:ShowOnError"], out var soe) || soe,
            ShowOnStartup =
                bool.TryParse(configuration["Toasts:ShowOnStartup"], out var sos) && sos,
            DurationSeconds = int.TryParse(configuration["Toasts:DurationSeconds"], out var ds)
                ? ds
                : 3,
        };

        var logging = new LoggingConfig
        {
            MinimumLevel = configuration["Logging:MinimumLevel"] ?? "Information",
            EnableFileLogging =
                bool.TryParse(configuration["Logging:EnableFileLogging"], out var efl) && efl,
            EnableConsoleLogging =
                bool.TryParse(configuration["Logging:EnableConsoleLogging"], out var ecl) && ecl,
            RetainedFileCountLimit = int.TryParse(
                configuration["Logging:RetainedFileCountLimit"],
                out var rfcl
            )
                ? rfcl
                : 7,
        };

        return new AppConfig
        {
            ThemeMode = themeMode,
            ShowToasts = showToasts,
            AutoStartup = autoStartup,
            Icons = icons,
            Toasts = toasts,
            Logging = logging,
        };
    }

    private void OnConfigurationChanged()
    {
        if (disposed)
            return;

        AppConfig newValue;
        lock (@lock)
        {
            newValue = LoadConfiguration();
            currentValue = newValue;
        }

        onChange?.Invoke(newValue, Options.DefaultName);
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        changeToken?.Dispose();
        changeToken = null;
        onChange = null;
    }

    private sealed class ChangeTokenDisposable(Action dispose) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            dispose();
        }
    }
}
