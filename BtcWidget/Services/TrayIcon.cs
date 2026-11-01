using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace BtcWidget.Services;

/// <summary>
/// Notification-area icon via Shell_NotifyIcon. Left click raises <see cref="Clicked"/>, right click
/// <see cref="ContextMenuRequested"/>. <see cref="ShowBalloon"/> is rendered by Windows 10/11 as an ordinary
/// toast (and kept in the notification centre) without loading the WinRT projection that the toast API
/// proper needs, which measured at ~70 MB of private memory in this app.
/// Create after the window's <c>SourceInitialized</c>.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private const uint NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2, NIM_SETVERSION = 4;
    private const uint NIF_MESSAGE = 0x01, NIF_ICON = 0x02, NIF_TIP = 0x04, NIF_INFO = 0x10, NIF_SHOWTIP = 0x80;
    private const uint NIIF_USER = 0x04, NIIF_LARGE_ICON = 0x20, NIIF_RESPECT_QUIET_TIME = 0x80;
    private const uint NOTIFYICON_VERSION_4 = 4;
    private const int WM_APP = 0x8000, WM_CALLBACK = WM_APP + 1, WM_CONTEXTMENU = 0x7B;
    private const int NIN_SELECT = 0x400, NIN_KEYSELECT = 0x401, NIN_BALLOONUSERCLICK = 0x405;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIconW(uint message, ref NOTIFYICONDATA data);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconExW(string file, int index, out IntPtr large, out IntPtr small, uint count);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr icon);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessageW(string message);

    private readonly HwndSource _source;
    private readonly uint _taskbarCreated;
    private IntPtr _large, _small;
    private string _tip;
    private bool _added;
    private DateTime _lastSelect;

    public event Action? Clicked;
    public event Action? ContextMenuRequested;
    public event Action? BalloonClicked;

    public TrayIcon(Window window, string tip)
    {
        _source = (HwndSource?)PresentationSource.FromVisual(window)
                  ?? throw new InvalidOperationException("TrayIcon needs an initialized window handle.");
        _tip = tip;
        ExtractIconExW(Environment.ProcessPath ?? "", 0, out _large, out _small, 1);
        // Explorer restarts drop every tray icon; it broadcasts this so apps can re-add theirs.
        _taskbarCreated = RegisterWindowMessageW("TaskbarCreated");
        _source.AddHook(Hook);
        Add();
    }

    public string Tip
    {
        get => _tip;
        set
        {
            _tip = value;
            if (!_added) return;
            var d = Data(NIF_TIP | NIF_SHOWTIP);
            Shell_NotifyIconW(NIM_MODIFY, ref d);
        }
    }

    /// <summary>Shows a notification with the app icon. Honours the user's quiet hours / focus assist.</summary>
    public bool ShowBalloon(string title, string body)
    {
        if (!_added) return false;
        var d = Data(NIF_INFO | NIF_ICON | NIF_TIP | NIF_SHOWTIP);
        d.szInfoTitle = Truncate(title, 63);
        d.szInfo = Truncate(body, 255);
        d.dwInfoFlags = NIIF_USER | NIIF_LARGE_ICON | NIIF_RESPECT_QUIET_TIME;
        d.hBalloonIcon = _large;
        return Shell_NotifyIconW(NIM_MODIFY, ref d);
    }

    private NOTIFYICONDATA Data(uint flags) => new()
    {
        cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
        hWnd = _source.Handle,
        uID = 1,
        uFlags = flags,
        uCallbackMessage = WM_CALLBACK,
        hIcon = _small,
        szTip = Truncate(_tip, 127),
        szInfo = "",
        szInfoTitle = ""
    };

    private void Add()
    {
        var d = Data(NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_SHOWTIP);
        _added = Shell_NotifyIconW(NIM_ADD, ref d);
        if (!_added) return;
        d.uTimeoutOrVersion = NOTIFYICON_VERSION_4;
        Shell_NotifyIconW(NIM_SETVERSION, ref d);
    }

    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_CALLBACK)
        {
            // Version 4: the event is in the low word of lParam.
            switch ((int)(lParam.ToInt64() & 0xFFFF))
            {
                case NIN_SELECT:
                case NIN_KEYSELECT:
                    // A double click arrives as two selects; treat it as one.
                    var now = DateTime.UtcNow;
                    if ((now - _lastSelect).TotalMilliseconds > 400) Clicked?.Invoke();
                    _lastSelect = now;
                    break;
                case WM_CONTEXTMENU:
                    ContextMenuRequested?.Invoke();
                    break;
                case NIN_BALLOONUSERCLICK:
                    BalloonClicked?.Invoke();
                    break;
            }
            handled = true;
        }
        else if (_taskbarCreated != 0 && msg == (int)_taskbarCreated)
        {
            _added = false;
            Add();
        }
        return IntPtr.Zero;
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";

    public void Dispose()
    {
        if (_added)
        {
            var d = Data(0);
            Shell_NotifyIconW(NIM_DELETE, ref d);
            _added = false;
        }
        _source.RemoveHook(Hook);
        if (_large != IntPtr.Zero) { DestroyIcon(_large); _large = IntPtr.Zero; }
        if (_small != IntPtr.Zero) { DestroyIcon(_small); _small = IntPtr.Zero; }
    }
}
