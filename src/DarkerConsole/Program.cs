using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using DarkerConsole.Infrastructure;

namespace DarkerConsole;

[SupportedOSPlatform("windows")]
class Program
{
    private const int ATTACH_PARENT_PROCESS = -1;

    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile
    );

    private const int STD_OUTPUT_HANDLE = -11;
    private const int STD_INPUT_HANDLE = -10;
    private const uint GENERIC_WRITE = 0x40000000;
    private const uint GENERIC_READ = 0x80000000;
    private const uint FILE_SHARE_WRITE = 2;
    private const uint FILE_SHARE_READ = 1;
    private const uint OPEN_EXISTING = 3;

    [SupportedOSPlatform("windows")]
    static async Task Main(string[] args)
    {
        // Attach to the parent console if available
        if (AttachConsole(ATTACH_PARENT_PROCESS))
        {
            RedirectConsoleIO();
        }

        // Create Jab service provider (compile-time DI)
        var serviceProvider = new ServiceProvider();

        // Get and run tray command
        var trayCommand = serviceProvider.GetService<DarkerConsole.Commands.TrayCommand>();
        await trayCommand.RunAsync();
    }

    private static void RedirectConsoleIO()
    {
        var hOut = CreateFile(
            "CONOUT$",
            GENERIC_WRITE,
            FILE_SHARE_WRITE,
            IntPtr.Zero,
            OPEN_EXISTING,
            0,
            IntPtr.Zero
        );
        var stdOut = new System.IO.StreamWriter(
            new System.IO.FileStream(
                new Microsoft.Win32.SafeHandles.SafeFileHandle(hOut, false),
                System.IO.FileAccess.Write
            ),
            System.Console.OutputEncoding
        )
        {
            AutoFlush = true,
        };
        System.Console.SetOut(stdOut);

        var hIn = CreateFile(
            "CONIN$",
            GENERIC_READ,
            FILE_SHARE_READ,
            IntPtr.Zero,
            OPEN_EXISTING,
            0,
            IntPtr.Zero
        );
        var stdIn = new System.IO.StreamReader(
            new System.IO.FileStream(
                new Microsoft.Win32.SafeHandles.SafeFileHandle(hIn, false),
                System.IO.FileAccess.Read
            ),
            System.Console.InputEncoding
        );
        System.Console.SetIn(stdIn);
    }
}
