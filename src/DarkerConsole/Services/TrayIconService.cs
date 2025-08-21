using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DarkerConsole.Services;

[SupportedOSPlatform("windows")]
public class TrayIconService(ThemeService themeService, ILogger<TrayIconService> logger)
    : IAsyncDisposable
{
    private IntPtr windowHandle;
    private IntPtr lightIcon;
    private IntPtr darkIcon;
    private IntPtr menuHandle;
    private Func<Task>? onTrayIconClick;
    private Action? onMenuExit;
    private bool disposed;
    private volatile bool exitRequested;
    private WndProc? wndProcDelegate;
    private uint mainThreadId;
    private static readonly string WindowClassName = $"DarkerConsoleTray_{Environment.ProcessId}";
    private static ReadOnlySpan<byte> LightIconName => "icon-light.ico"u8;
    private static ReadOnlySpan<byte> DarkIconName => "icon-dark.ico"u8;

    private const uint WM_QUIT = 0x0012;
    private const int WM_TRAYICON = 0x8000;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_COMMAND = 0x0111;
    private const int NIM_ADD = 0x00000000;
    private const int NIM_MODIFY = 0x00000001;
    private const int NIM_DELETE = 0x00000002;
    private const int NIF_MESSAGE = 0x00000001;
    private const int NIF_ICON = 0x00000002;
    private const int NIF_TIP = 0x00000004;
    private const int MENU_EXIT_ID = 1000;
    private const int MENU_CONFIG_ID = 1001;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public int dwState;
        public int dwStateMask;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public int uTimeoutOrVersion;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public int dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private ref struct WNDCLASS
    {
        public int style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpszMenuName;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpszClassName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private ref struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private ref struct MSG
    {
        public IntPtr hwnd;
        public int message;
        public IntPtr wParam;
        public IntPtr lParam;
        public int time;
        public POINT pt;
    }

    private delegate IntPtr WndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW")]
    private static extern bool Shell_NotifyIcon(int dwMessage, in NOTIFYICONDATA lpData);

    [DllImport("user32.dll", EntryPoint = "RegisterClassW")]
    private static extern short RegisterClass(in WNDCLASS lpWndClass);

    [DllImport("user32.dll", EntryPoint = "CreateWindowExW", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(
        int dwExStyle,
        string lpClassName,
        string lpWindowName,
        int dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam
    );

    [DllImport("user32.dll", EntryPoint = "DefWindowProcW")]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll", EntryPoint = "LoadImageW", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadImage(
        IntPtr hInst,
        string name,
        int type,
        int cx,
        int cy,
        int fuLoad
    );

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", EntryPoint = "AppendMenuW", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(
        IntPtr hMenu,
        int uFlags,
        int uIDNewItem,
        string lpNewItem
    );

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenu(
        IntPtr hMenu,
        int uFlags,
        int x,
        int y,
        int nReserved,
        IntPtr hWnd,
        IntPtr prcRect
    );

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern int GetMessage(
        out MSG lpMsg,
        IntPtr hWnd,
        int wMsgFilterMin,
        int wMsgFilterMax
    );

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern void PostQuitMessage(int nExitCode);

    [DllImport("user32.dll")]
    private static extern bool PostThreadMessage(
        uint idThread,
        uint Msg,
        UIntPtr wParam,
        IntPtr lParam
    );

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    private const int IMAGE_ICON = 1;
    private const int LR_LOADFROMFILE = 0x00000010;
    private const int MF_STRING = 0x00000000;
    private const int TPM_RETURNCMD = 0x0100;

    public async Task InitializeAsync(Func<Task> onTrayIconClick, Action onMenuExit)
    {
        this.onTrayIconClick = onTrayIconClick;
        this.onMenuExit = onMenuExit;
        wndProcDelegate = WindowProc;

        await Task.Run(() =>
        {
            try
            {
                CreateMessageWindow();
                LoadIcons();
                CreateContextMenu();
                CreateNotifyIcon();
                logger.LogInformation("Tray icon service initialized successfully");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to initialize tray icon service");
                throw;
            }
        });
    }

    private void CreateMessageWindow()
    {
        var hInstance = GetModuleHandle(null);

        if (wndProcDelegate == null)
            throw new InvalidOperationException("_wndProcDelegate is not initialized");

        var wndClass = new WNDCLASS
        {
            style = 0,
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(wndProcDelegate),
            cbClsExtra = 0,
            cbWndExtra = 0,
            hInstance = hInstance,
            hIcon = IntPtr.Zero,
            hCursor = IntPtr.Zero,
            hbrBackground = IntPtr.Zero,
            lpszMenuName = null,
            lpszClassName = WindowClassName,
        };

        if (RegisterClass(in wndClass) == 0)
            throw new InvalidOperationException("Failed to register window class");

        windowHandle = CreateWindowEx(
            0,
            WindowClassName,
            "DarkerConsole",
            0,
            0,
            0,
            0,
            0,
            IntPtr.Zero,
            IntPtr.Zero,
            hInstance,
            IntPtr.Zero
        );

        if (windowHandle == IntPtr.Zero)
            throw new InvalidOperationException("Failed to create message window");
    }

    private void LoadIcons()
    {
        var basePath = AppContext.BaseDirectory;
        var lightIconPath = Path.Combine(basePath, "icon-light.ico");
        var darkIconPath = Path.Combine(basePath, "icon-dark.ico");

        if (!File.Exists(lightIconPath) || !File.Exists(darkIconPath))
        {
            // Fallback to system icons
            lightIcon = LoadIcon(IntPtr.Zero, new IntPtr(32516)); // IDI_QUESTION
            darkIcon = LoadIcon(IntPtr.Zero, new IntPtr(32514)); // IDI_ERROR
            logger.LogInformation("Using system fallback icons");
            return;
        }
        lightIcon = LoadImage(IntPtr.Zero, lightIconPath, IMAGE_ICON, 256, 256, LR_LOADFROMFILE);
        darkIcon = LoadImage(IntPtr.Zero, darkIconPath, IMAGE_ICON, 256, 256, LR_LOADFROMFILE);

        if (lightIcon != IntPtr.Zero && darkIcon != IntPtr.Zero)
            logger.LogInformation("Successfully loaded custom icons from files");
        else
            logger.LogWarning("Failed to load custom icons from files");
    }

    private void CreateContextMenu()
    {
        menuHandle = CreatePopupMenu();
        AppendMenu(menuHandle, MF_STRING, MENU_CONFIG_ID, "Open Config Directory");
        AppendMenu(menuHandle, MF_STRING, MENU_EXIT_ID, "Exit");
    }

    private void CreateNotifyIcon()
    {
        var nid = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = windowHandle,
            uID = 1,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = WM_TRAYICON,
            hIcon = themeService.IsLightThemeEnabled() ? lightIcon : darkIcon,
            szTip = "DarkerConsole - Click to toggle theme",
        };

        if (!Shell_NotifyIcon(NIM_ADD, in nid))
            throw new InvalidOperationException("Failed to add tray icon");
    }

    public async Task UpdateIconAsync(bool useDarkIcon)
    {
        await Task.Run(() =>
        {
            var icon = useDarkIcon ? darkIcon : lightIcon;
            if (icon == IntPtr.Zero)
                return;

            var nid = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = windowHandle,
                uID = 1,
                uFlags = NIF_ICON,
                hIcon = icon,
            };

            Shell_NotifyIcon(NIM_MODIFY, in nid);
        });
    }

    private IntPtr WindowProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam)
    {
        logger.LogDebug(
            "WindowProc called: msg={Msg:X}, wParam={WParam:X}, lParam={LParam:X}",
            msg,
            wParam.ToInt64(),
            lParam.ToInt64()
        );

        switch (msg)
        {
            case WM_TRAYICON:
                var mouseMsg = (int)(lParam & 0xFFFF);
                logger.LogDebug("Tray icon message received: mouseMsg={MouseMsg:X}", mouseMsg);

                switch (mouseMsg)
                {
                    case WM_LBUTTONUP:
                        logger.LogInformation("Left click detected on tray icon");
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                if (onTrayIconClick != null)
                                    await onTrayIconClick();
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "Error handling tray icon click");
                            }
                        });
                        break;
                    case WM_RBUTTONUP:
                        logger.LogInformation("Right click detected on tray icon");
                        ShowContextMenu();
                        break;
                }

                break;

            case WM_COMMAND:
                var command = (int)(wParam & 0xFFFF);
                logger.LogDebug("Command received: {Command}", command);

                switch (command)
                {
                    case MENU_EXIT_ID:
                        logger.LogInformation("Exit command selected");
                        onMenuExit?.Invoke();
                        break;
                    case MENU_CONFIG_ID:
                        logger.LogInformation("Open config directory command selected");
                        OpenConfigDirectory();
                        break;
                }
                break;

            case (int)WM_QUIT:
                logger.LogInformation("Quit message received, terminating loop");
                exitRequested = true;
                break;

            default:
                logger.LogDebug("Unhandled message: {Msg:X}", msg);
                break;
        }
        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private void ShowContextMenu()
    {
        if (menuHandle == IntPtr.Zero || !GetCursorPos(out var point))
            return;

        SetForegroundWindow(windowHandle);
        var selectedItem = TrackPopupMenu(
            menuHandle,
            TPM_RETURNCMD,
            point.x,
            point.y,
            0,
            windowHandle,
            IntPtr.Zero
        );

        switch (selectedItem)
        {
            case MENU_EXIT_ID:
                logger.LogInformation("Exit selected from context menu");
                onMenuExit?.Invoke();
                break;

            case MENU_CONFIG_ID:
                logger.LogInformation("Open config directory selected from context menu");
                OpenConfigDirectory();
                break;
        }
    }

    private void OpenConfigDirectory()
    {
        var configDir = AppContext.BaseDirectory;
        var startInfo = new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{configDir}\"",
            UseShellExecute = true,
        };
        Process.Start(startInfo);
        logger.LogInformation("Opened config directory: {ConfigDir}", configDir);
    }

    public void RunMessageLoop()
    {
        logger.LogInformation("Starting Win32 message loop");
        mainThreadId = GetCurrentThreadId();

        MSG msg;
        int ret;
        while ((ret = GetMessage(out msg, IntPtr.Zero, 0, 0)) != 0)
        {
            if (ret == -1)
            {
                // Handle error
                logger.LogError("Error in GetMessage");
                break;
            }
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }

        logger.LogInformation("Message loop exited");
    }

    public void ExitMessageLoop()
    {
        logger.LogInformation("Requesting message loop exit");
        if (!exitRequested)
        {
            exitRequested = true;
            PostThreadMessage(mainThreadId, WM_QUIT, UIntPtr.Zero, IntPtr.Zero);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
            return;

        ExitMessageLoop();

        await Task.Run(() =>
        {
            try
            {
                if (windowHandle != IntPtr.Zero)
                {
                    var nid = new NOTIFYICONDATA
                    {
                        cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                        hWnd = windowHandle,
                        uID = 1,
                    };
                    Shell_NotifyIcon(NIM_DELETE, in nid);
                }

                if (lightIcon != IntPtr.Zero)
                    DestroyIcon(lightIcon);
                if (darkIcon != IntPtr.Zero)
                    DestroyIcon(darkIcon);
                if (menuHandle != IntPtr.Zero)
                    DestroyMenu(menuHandle);
                if (windowHandle != IntPtr.Zero)
                    DestroyWindow(windowHandle);

                logger.LogInformation("Tray icon service disposed");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error disposing tray icon service");
            }
            finally
            {
                disposed = true;
            }
        });
    }
}
