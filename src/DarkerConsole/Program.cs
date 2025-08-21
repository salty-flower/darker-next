using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DarkerConsole.Commands;
using DarkerConsole.Models;
using DarkerConsole.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Tomlyn;

namespace DarkerConsole;

class Program
{
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_HIDE = 0;

    static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File("logs/darkerconsole-.txt", rollingInterval: RollingInterval.Day)
            .WriteTo.Console()
            .CreateLogger();

        try
        {
            var builder = Host.CreateApplicationBuilder(args);

            // Load TOML config if available
            var configPath = "config.toml";
            if (File.Exists(configPath))
            {
                var tomlContent = File.ReadAllText(configPath);
                var tomlModel = Toml.ToModel(tomlContent);
                // For now, we'll use default configuration
                // TODO: Implement TOML to IConfiguration conversion
            }

            builder.Services.Configure<AppConfig>(
                builder.Configuration.GetSection("DarkerConsole")
                    ?? builder.Configuration.GetSection("")
            );

            builder.Services.AddSingleton<TrayIconService>();
            builder.Services.AddSingleton<ThemeService>();
            builder.Services.AddSingleton<ToastService>();

            builder.Services.AddSerilog();

            var host = builder.Build();

            if (args.Length == 0)
            {
                HideConsoleWindow();
            }

            // Get services from DI container
            var trayIconService = host.Services.GetRequiredService<TrayIconService>();
            var themeService = host.Services.GetRequiredService<ThemeService>();
            var toastService = host.Services.GetRequiredService<ToastService>();
            var config = host.Services.GetRequiredService<IOptions<AppConfig>>();
            var logger = host.Services.GetRequiredService<ILogger<TrayCommand>>();

            // Create and run tray command directly
            var trayCommand = new TrayCommand(
                trayIconService,
                themeService,
                toastService,
                config,
                logger
            );
            await trayCommand.RunAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    private static void HideConsoleWindow()
    {
        var handle = GetConsoleWindow();
        if (handle != IntPtr.Zero)
        {
            ShowWindow(handle, SW_HIDE);
        }
    }
}
