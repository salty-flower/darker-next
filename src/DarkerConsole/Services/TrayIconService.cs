using System;
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
    private IntPtr _windowHandle;
    private IntPtr _lightIcon;
    private IntPtr _darkIcon;
    private IntPtr _menuHandle;
    private Func<Task>? _onTrayIconClick;
    private Action? _onMenuExit;
    private bool _disposed;
    private volatile bool _exitRequested;
    private WndProc? _wndProcDelegate;
    private uint _mainThreadId;

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
    private struct WNDCLASS
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
    private struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
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
    private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll", EntryPoint = "RegisterClassW")]
    private static extern short RegisterClass(ref WNDCLASS lpWndClass);

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
        _onTrayIconClick = onTrayIconClick;
        _onMenuExit = onMenuExit;
        _wndProcDelegate = WindowProc;

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
        var className = $"DarkerConsoleTray_{Guid.NewGuid():N}";

        if (_wndProcDelegate == null)
        {
            throw new InvalidOperationException("_wndProcDelegate is not initialized");
        }
        var wndClass = new WNDCLASS
        {
            style = 0,
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
            cbClsExtra = 0,
            cbWndExtra = 0,
            hInstance = hInstance,
            hIcon = IntPtr.Zero,
            hCursor = IntPtr.Zero,
            hbrBackground = IntPtr.Zero,
            lpszMenuName = null,
            lpszClassName = className,
        };

        if (RegisterClass(ref wndClass) == 0)
        {
            throw new InvalidOperationException("Failed to register window class");
        }

        _windowHandle = CreateWindowEx(
            0,
            className,
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

        if (_windowHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create message window");
        }
    }

    private void LoadIcons()
    {
        try
        {
            var basePath = AppContext.BaseDirectory;
            var lightIconPath = Path.Combine(basePath, "icon-light.ico");
            var darkIconPath = Path.Combine(basePath, "icon-dark.ico");

            if (File.Exists(lightIconPath) && File.Exists(darkIconPath))
            {
                _lightIcon = LoadImage(
                    IntPtr.Zero,
                    lightIconPath,
                    IMAGE_ICON,
                    16,
                    16,
                    LR_LOADFROMFILE
                );
                _darkIcon = LoadImage(
                    IntPtr.Zero,
                    darkIconPath,
                    IMAGE_ICON,
                    16,
                    16,
                    LR_LOADFROMFILE
                );

                if (_lightIcon != IntPtr.Zero && _darkIcon != IntPtr.Zero)
                {
                    logger.LogInformation("Successfully loaded custom icons from files");
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load custom icons, falling back to system icons");
        }

        // Fallback to system icons
        _lightIcon = LoadIcon(IntPtr.Zero, new IntPtr(32516)); // IDI_QUESTION
        _darkIcon = LoadIcon(IntPtr.Zero, new IntPtr(32514)); // IDI_ERROR

        logger.LogInformation("Using system fallback icons");
    }

    private void CreateContextMenu()
    {
        _menuHandle = CreatePopupMenu();
        AppendMenu(_menuHandle, MF_STRING, MENU_CONFIG_ID, "Open Config Directory");
        AppendMenu(_menuHandle, MF_STRING, MENU_EXIT_ID, "Exit");
    }

    private void CreateNotifyIcon()
    {
        var nid = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _windowHandle,
            uID = 1,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = WM_TRAYICON,
            hIcon = themeService.IsLightThemeEnabled() ? _lightIcon : _darkIcon,
            szTip = "DarkerConsole - Click to toggle theme",
        };

        if (!Shell_NotifyIcon(NIM_ADD, ref nid))
        {
            throw new InvalidOperationException("Failed to add tray icon");
        }
    }

    public async Task UpdateIconAsync(bool useDarkIcon)
    {
        await Task.Run(() =>
        {
            var icon = useDarkIcon ? _darkIcon : _lightIcon;
            if (icon == IntPtr.Zero)
                return;

            var nid = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _windowHandle,
                uID = 1,
                uFlags = NIF_ICON,
                hIcon = icon,
            };

            Shell_NotifyIcon(NIM_MODIFY, ref nid);
        });
    }

    private IntPtr WindowProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            logger.LogDebug(
                "WindowProc called: msg={Msg:X}, wParam={WParam:X}, lParam={LParam:X}",
                msg,
                wParam.ToInt64(),
                lParam.ToInt64()
            );

            if (msg == WM_TRAYICON)
            {
                var mouseMsg = (int)(lParam & 0xFFFF);
                logger.LogDebug("Tray icon message received: mouseMsg={MouseMsg:X}", mouseMsg);

                if (mouseMsg == WM_LBUTTONUP)
                {
                    logger.LogInformation("Left click detected on tray icon");
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            if (_onTrayIconClick != null)
                                await _onTrayIconClick();
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Error handling tray icon click");
                        }
                    });
                }
                else if (mouseMsg == WM_RBUTTONUP)
                {
                    logger.LogInformation("Right click detected on tray icon");
                    ShowContextMenu();
                }
            }
            else if (msg == WM_COMMAND)
            {
                var command = (int)(wParam & 0xFFFF);
                logger.LogDebug("Command received: {Command}", command);
                if (command == MENU_EXIT_ID)
                {
                    logger.LogInformation("Exit command selected");
                    _onMenuExit?.Invoke();
                }
                else if (command == MENU_CONFIG_ID)
                {
                    logger.LogInformation("Open config directory command selected");
                    OpenConfigDirectory();
                }
            }
            else if (msg == WM_QUIT)
            {
                logger.LogInformation("Quit message received, terminating loop");
                _exitRequested = true;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in window procedure");
        }

        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private void ShowContextMenu()
    {
        if (_menuHandle != IntPtr.Zero && GetCursorPos(out var point))
        {
            SetForegroundWindow(_windowHandle);
            var selectedItem = TrackPopupMenu(
                _menuHandle,
                TPM_RETURNCMD,
                point.x,
                point.y,
                0,
                _windowHandle,
                IntPtr.Zero
            );

            if (selectedItem == MENU_EXIT_ID)
            {
                logger.LogInformation("Exit selected from context menu");
                _onMenuExit?.Invoke();
            }
            else if (selectedItem == MENU_CONFIG_ID)
            {
                logger.LogInformation("Open config directory selected from context menu");
                OpenConfigDirectory();
            }
        }
    }

    private void OpenConfigDirectory()
    {
        try
        {
            var configDir = AppContext.BaseDirectory;
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{configDir}\"",
                UseShellExecute = true,
            };
            System.Diagnostics.Process.Start(startInfo);
            logger.LogInformation("Opened config directory: {ConfigDir}", configDir);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to open config directory");
        }
    }

    public void RunMessageLoop()
    {
        logger.LogInformation("Starting Win32 message loop");
        _mainThreadId = GetCurrentThreadId();

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
        if (!_exitRequested)
        {
            _exitRequested = true;
            PostThreadMessage(_mainThreadId, WM_QUIT, UIntPtr.Zero, IntPtr.Zero);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        ExitMessageLoop();

        await Task.Run(() =>
        {
            try
            {
                if (_windowHandle != IntPtr.Zero)
                {
                    var nid = new NOTIFYICONDATA
                    {
                        cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                        hWnd = _windowHandle,
                        uID = 1,
                    };
                    Shell_NotifyIcon(NIM_DELETE, ref nid);
                }

                if (_lightIcon != IntPtr.Zero)
                    DestroyIcon(_lightIcon);
                if (_darkIcon != IntPtr.Zero)
                    DestroyIcon(_darkIcon);
                if (_menuHandle != IntPtr.Zero)
                    DestroyMenu(_menuHandle);
                if (_windowHandle != IntPtr.Zero)
                    DestroyWindow(_windowHandle);

                logger.LogInformation("Tray icon service disposed");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error disposing tray icon service");
            }

            _disposed = true;
        });
    }
}
