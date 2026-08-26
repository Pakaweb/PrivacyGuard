using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using PrivacyGuard.Helpers;

namespace PrivacyGuard.Services;

/// <summary>
/// Win32 notify-icon for the unpackaged WinUI 3 process.
/// </summary>
public interface ITrayIconService : IDisposable
{
    void Initialize(IAppWindowController window);

    void SetEnabled(bool enabled);
}

/// <inheritdoc />
public sealed class TrayIconService : ITrayIconService
{
    private const int WmApp = 0x8000;
    private const int WmTray = WmApp + 1;
    private const int WmCommand = 0x0111;
    private const int WmRButtonUp = 0x0205;
    private const int WmLButtonDblClk = 0x0203;
    private const int WmContextMenu = 0x007B;
    private const uint NimAdd = 0;
    private const uint NimModify = 1;
    private const uint NimDelete = 2;
    private const uint NifMessage = 0x0001;
    private const uint NifIcon = 0x0002;
    private const uint NifTip = 0x0004;
    private const uint MfString = 0;
    private const uint MfSeparator = 0x0800;
    private const uint TpmRightButton = 0x0002;
    private const int IdOpen = 1;
    private const int IdRecommended = 2;
    private const int IdMaximum = 3;
    private const int IdMonitor = 4;
    private const int IdExit = 5;
    private const int ImageIcon = 1;
    private const uint LrLoadFromFile = 0x0010;
    private const uint LrDefaultSize = 0x0040;
    private static readonly nint HwndMessage = -3;

    private readonly ITrayCommandHandler _commands;
    private readonly ISettingsService _settings;
    private readonly IResetMonitorService _reset;
    private readonly ILocalizationService _loc;
    private readonly ILogger<TrayIconService> _logger;
    private readonly Native.WndProc _wndProc;

    private IAppWindowController? _window;
    private nint _messageHwnd;
    private nint _icon;
    private ushort _classAtom;
    private bool _visible;
    private bool _disposed;

    public TrayIconService(
        ITrayCommandHandler commands,
        ISettingsService settings,
        IResetMonitorService reset,
        ILocalizationService localization,
        ILogger<TrayIconService> logger)
    {
        _commands = commands;
        _settings = settings;
        _reset = reset;
        _loc = localization;
        _logger = logger;
        _wndProc = WndProc;
        _loc.LanguageChanged += (_, _) =>
        {
            if (_visible)
            {
                UpdateIcon();
            }
        };
    }

    public void Initialize(IAppWindowController window)
    {
        _window = window;
        SetEnabled(_settings.Current.EnableTray);
    }

    public void SetEnabled(bool enabled)
    {
        if (_disposed)
        {
            return;
        }

        if (enabled)
        {
            Show();
        }
        else
        {
            Hide();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Hide();
        DestroyMessageWindow();
        if (_icon != 0)
        {
            Native.DestroyIcon(_icon);
            _icon = 0;
        }
    }

    private void Show()
    {
        if (_window is null)
        {
            return;
        }

        EnsureMessageWindow();
        EnsureIcon();
        var data = BuildData();
        if (!Native.Shell_NotifyIcon(_visible ? NimModify : NimAdd, ref data))
        {
            Native.Shell_NotifyIcon(NimAdd, ref data);
        }

        _visible = true;
    }

    private void Hide()
    {
        if (!_visible)
        {
            return;
        }

        var data = BuildData();
        Native.Shell_NotifyIcon(NimDelete, ref data);
        _visible = false;
    }

    private void UpdateIcon()
    {
        if (!_visible)
        {
            return;
        }

        var data = BuildData();
        Native.Shell_NotifyIcon(NimModify, ref data);
    }

    private Native.NotifyIconData BuildData()
    {
        return new Native.NotifyIconData
        {
            cbSize = Marshal.SizeOf<Native.NotifyIconData>(),
            hWnd = _messageHwnd,
            uID = 1,
            uFlags = NifMessage | NifIcon | NifTip,
            uCallbackMessage = WmTray,
            hIcon = _icon,
            szTip = _loc.Get("tray.tooltip")
        };
    }

    private void EnsureIcon()
    {
        if (_icon != 0)
        {
            return;
        }

        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "tray.ico");
        if (File.Exists(path))
        {
            _icon = Native.LoadImage(0, path, ImageIcon, 0, 0, LrLoadFromFile | LrDefaultSize);
        }

        if (_icon == 0)
        {
            _icon = Native.LoadIcon(0, 32512); // IDI_APPLICATION
        }
    }

    private void EnsureMessageWindow()
    {
        if (_messageHwnd != 0)
        {
            return;
        }

        var className = "PrivacyGuardTrayHidden";
        var wndClass = new Native.WndClassEx
        {
            cbSize = (uint)Marshal.SizeOf<Native.WndClassEx>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = Native.GetModuleHandle(null),
            lpszClassName = className
        };

        _classAtom = Native.RegisterClassEx(ref wndClass);
        _messageHwnd = Native.CreateWindowEx(0, className, "PrivacyGuardTray", 0, 0, 0, 0, 0, HwndMessage, 0, wndClass.hInstance, 0);
        if (_messageHwnd == 0)
        {
            _logger.LogWarning("Could not create tray message window. Last error {Error}.", Marshal.GetLastWin32Error());
        }
    }

    private void DestroyMessageWindow()
    {
        if (_messageHwnd != 0)
        {
            Native.DestroyWindow(_messageHwnd);
            _messageHwnd = 0;
        }
    }

    private nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        if (msg == WmTray)
        {
            var mouse = (int)lParam & 0xFFFF;
            if (mouse is WmRButtonUp or WmContextMenu)
            {
                ShowMenu();
                return 0;
            }

            if (mouse == WmLButtonDblClk)
            {
                Dispatch(() => _commands.ShowMainWindow());
                return 0;
            }
        }

        if (msg == WmCommand)
        {
            HandleCommand((int)wParam & 0xFFFF);
            return 0;
        }

        return Native.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private void ShowMenu()
    {
        if (_window is null)
        {
            return;
        }

        Native.GetCursorPos(out var point);
        var menu = Native.CreatePopupMenu();
        Native.AppendMenu(menu, MfString, IdOpen, _loc.Get("tray.open"));
        Native.AppendMenu(menu, MfSeparator, 0, string.Empty);
        Native.AppendMenu(menu, MfString, IdRecommended, _loc.Get("tray.recommended"));
        Native.AppendMenu(menu, MfString, IdMaximum, _loc.Get("tray.maximum"));
        Native.AppendMenu(menu, MfString, IdMonitor, _reset.IsMonitoringPaused ? _loc.Get("tray.resume") : _loc.Get("tray.pause"));
        Native.AppendMenu(menu, MfSeparator, 0, string.Empty);
        Native.AppendMenu(menu, MfString, IdExit, _loc.Get("tray.exit"));

        Native.SetForegroundWindow(_window.Handle);
        Native.TrackPopupMenu(menu, TpmRightButton, point.X, point.Y, 0, _messageHwnd, 0);
        Native.PostMessage(_messageHwnd, 0, 0, 0);
        Native.DestroyMenu(menu);
    }

    private void HandleCommand(int id)
    {
        switch (id)
        {
            case IdOpen:
                Dispatch(() => _commands.ShowMainWindow());
                break;
            case IdRecommended:
                Dispatch(() => _ = _commands.ApplyRecommendedAsync());
                break;
            case IdMaximum:
                Dispatch(() => _ = _commands.ApplyMaximumAsync());
                break;
            case IdMonitor:
                Dispatch(() => _ = _commands.ToggleMonitoringAsync());
                break;
            case IdExit:
                Dispatch(() => _commands.Exit());
                break;
        }
    }

    private void Dispatch(Action action)
    {
        var dispatcher = _window?.DispatcherQueue;
        if (dispatcher is null || dispatcher.HasThreadAccess)
        {
            action();
            return;
        }

        dispatcher.TryEnqueue(DispatcherQueuePriority.Normal, () => action());
    }

    private static class Native
    {
        public delegate nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct NotifyIconData
        {
            public int cbSize;
            public nint hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public nint hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WndClassEx
        {
            public uint cbSize;
            public uint style;
            public nint lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public nint hInstance;
            public nint hIcon;
            public nint hCursor;
            public nint hbrBackground;
            public string? lpszMenuName;
            public string lpszClassName;
            public nint hIconSm;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Point
        {
            public int X;
            public int Y;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern bool Shell_NotifyIcon(uint dwMessage, ref NotifyIconData lpData);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern ushort RegisterClassEx(ref WndClassEx lpwcx);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern nint CreateWindowEx(
            int dwExStyle,
            string lpClassName,
            string lpWindowName,
            int dwStyle,
            int x,
            int y,
            int nWidth,
            int nHeight,
            nint hWndParent,
            nint hMenu,
            nint hInstance,
            nint lpParam);

        [DllImport("user32.dll")]
        public static extern nint DefWindowProc(nint hWnd, uint msg, nint wParam, nint lParam);

        [DllImport("user32.dll")]
        public static extern bool DestroyWindow(nint hWnd);

        [DllImport("user32.dll")]
        public static extern nint LoadIcon(nint hInstance, nint lpIconName);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern nint LoadImage(nint hInst, string name, int type, int cx, int cy, uint fuLoad);

        [DllImport("user32.dll")]
        public static extern bool DestroyIcon(nint hIcon);

        [DllImport("user32.dll")]
        public static extern nint CreatePopupMenu();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool AppendMenu(nint hMenu, uint uFlags, nint uIDNewItem, string lpNewItem);

        [DllImport("user32.dll")]
        public static extern bool TrackPopupMenu(nint hMenu, uint uFlags, int x, int y, int nReserved, nint hWnd, nint prcRect);

        [DllImport("user32.dll")]
        public static extern bool DestroyMenu(nint hMenu);

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out Point lpPoint);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(nint hWnd);

        [DllImport("user32.dll")]
        public static extern bool PostMessage(nint hWnd, uint msg, nint wParam, nint lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern nint GetModuleHandle(string? lpModuleName);
    }
}
