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
    private readonly IConfiguration _configuration;
    private readonly object _lock = new();
    private AppConfig? _currentValue;
    private IDisposable? _changeToken;
    private event Action<AppConfig, string?>? _onChange;
    private bool _disposed;

    public TomlOptionsMonitor(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        // Set up change tracking
        _changeToken = _configuration
            .GetReloadToken()
            .RegisterChangeCallback(_ => OnConfigurationChanged(), null);
    }

    public AppConfig CurrentValue
    {
        get
        {
            if (_currentValue == null)
                lock (_lock)

                    _currentValue ??= LoadConfiguration();

            return _currentValue;
        }
    }

    public AppConfig Get(string? name) => CurrentValue;

    public IDisposable OnChange(Action<AppConfig, string?> listener)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _onChange += listener;
        return new ChangeTokenDisposable(() => _onChange -= listener);
    }

    private AppConfig LoadConfiguration()
    {
        // Manual binding - AOT friendly, no reflection
        var themeMode = _configuration["ThemeMode"] ?? "both";
        var showToasts = !bool.TryParse(_configuration["ShowToasts"], out var st) || st;
        var autoStartup = bool.TryParse(_configuration["AutoStartup"], out var au) && au;

        var icons = new IconConfig
        {
            LightIconPath = _configuration["Icons:LightIconPath"] ?? "icon-light.ico",
            DarkIconPath = _configuration["Icons:DarkIconPath"] ?? "icon-dark.ico",
        };

        var toasts = new ToastConfig
        {
            ShowOnThemeChange =
                !bool.TryParse(_configuration["Toasts:ShowOnThemeChange"], out var stc) || stc,
            ShowOnError = !bool.TryParse(_configuration["Toasts:ShowOnError"], out var soe) || soe,
            ShowOnStartup =
                bool.TryParse(_configuration["Toasts:ShowOnStartup"], out var sos) && sos,
            DurationSeconds = int.TryParse(_configuration["Toasts:DurationSeconds"], out var ds)
                ? ds
                : 3,
        };

        var logging = new LoggingConfig
        {
            MinimumLevel = _configuration["Logging:MinimumLevel"] ?? "Information",
            EnableFileLogging =
                bool.TryParse(_configuration["Logging:EnableFileLogging"], out var efl) && efl,
            EnableConsoleLogging =
                bool.TryParse(_configuration["Logging:EnableConsoleLogging"], out var ecl) && ecl,
            RetainedFileCountLimit = int.TryParse(
                _configuration["Logging:RetainedFileCountLimit"],
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
        if (_disposed)
            return;

        AppConfig newValue;
        lock (_lock)
        {
            newValue = LoadConfiguration();
            _currentValue = newValue;
        }

        _onChange?.Invoke(newValue, Options.DefaultName);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _changeToken?.Dispose();
        _changeToken = null;
        _onChange = null;
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
