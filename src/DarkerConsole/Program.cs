using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using DarkerConsole.Infrastructure;

namespace DarkerConsole;

[SupportedOSPlatform("windows")]
class Program
{
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_HIDE = 0;

    [SupportedOSPlatform("windows")]
    static async Task Main(string[] args)
    {
        try
        {
            if (args.Length == 0)
            {
                HideConsoleWindow();
            }

            // Create Jab service provider (compile-time DI)
            var serviceProvider = new ServiceProvider();

            // Get and run tray command
            var trayCommand = serviceProvider.GetService<DarkerConsole.Commands.TrayCommand>();
            await trayCommand.RunAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Application terminated unexpectedly: {ex}");
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
