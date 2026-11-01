using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using BtcWidget.ViewModels;

namespace BtcWidget.Views;

public partial class MainWindow : Window
{
    private const int WM_NCHITTEST = 0x0084, WM_NCLBUTTONDOWN = 0x00A1, WM_ENTERSIZEMOVE = 0x0231, WM_EXITSIZEMOVE = 0x0232;
    private const int HTCLIENT = 1, HTLEFT = 10, HTRIGHT = 11, HTBOTTOM = 15;
    private double _heightBeforeResize;
    private bool _resizingHeight;

    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int cmd);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int index);
    [DllImport("user32.dll")] private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int index, IntPtr value);
    private const int SW_SHOWNOACTIVATE = 4, GWL_EXSTYLE = -20;
    private const long WS_EX_NOACTIVATE = 0x08000000;
    private const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOACTIVATE = 0x0010, SWP_SHOWWINDOW = 0x0040;
    private static readonly IntPtr HWND_TOPMOST = new(-1), HWND_NOTOPMOST = new(-2);

    /// <summary>
    /// Bring the widget back from minimized (or hidden) and to the front WITHOUT taking focus: whatever the
    /// user is typing into keeps the keyboard. WPF's WindowState = Normal would activate, and so, in practice,
    /// does restoring a minimized window with SW_SHOWNOACTIVATE alone. So: remember who has the foreground,
    /// make the window un-activatable (WS_EX_NOACTIVATE) for the duration, restore and lift it with a
    /// topmost-then-not-topmost hop, drop the style once the pending messages have drained, and if the
    /// shell still handed us the foreground, give it straight back.
    /// </summary>
    public void RestoreWithoutActivating()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        var previous = GetForegroundWindow();
        var style = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(style.ToInt64() | WS_EX_NOACTIVATE));
        try
        {
            if (!IsVisible) Show();
            ShowWindow(hwnd, SW_SHOWNOACTIVATE);
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            if (!Topmost) SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }
        finally
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, () =>
            {
                SetWindowLongPtr(hwnd, GWL_EXSTYLE, style);
                if (previous != IntPtr.Zero && previous != hwnd && GetForegroundWindow() == hwnd) SetForegroundWindow(previous);
            });
        }
    }

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        SizeChanged += (_, _) => { if (IsLoaded) ClampToWorkArea(); };
        SourceInitialized += (_, _) => ((HwndSource)PresentationSource.FromVisual(this)!).AddHook(Hook);
    }

    static MainWindow()
    {
        // Tooltips setting. ToolTipOpening is a direct (non-bubbling) event, so a class handler is the one
        // place that sees every tooltip in the widget and the news popup; handling the event cancels the tooltip.
        EventManager.RegisterClassHandler(typeof(FrameworkElement), System.Windows.Controls.ToolTipService.ToolTipOpeningEvent,
            new System.Windows.Controls.ToolTipEventHandler((sender, e) =>
            {
                if (sender is DependencyObject d && GetWindow(d) is { DataContext: ITooltipPolicy { ShowTooltips: false } })
                    e.Handled = true;
            }));
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        var s = vm.Settings;

        // Restore last position if it is still on a screen; otherwise bottom-right of the primary work area.
        var wa = SystemParameters.WorkArea;
        if (s.Left is { } l && s.Top is { } t &&
            l > SystemParameters.VirtualScreenLeft - 50 && t > SystemParameters.VirtualScreenTop - 50 &&
            l < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 50 &&
            t < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 50)
        {
            Left = l; Top = t;
        }
        else
        {
            Left = wa.Right - ActualWidth - 12;
            Top = wa.Bottom - ActualHeight - 12;
        }
        ClampToWorkArea();
    }

    /// <summary>
    /// Keeps the whole widget visible when it grows (wider font, more lines, a width change from Settings):
    /// shifts it left / up as needed, never resizes it. Uses the work area of the monitor it is on.
    /// </summary>
    private void ClampToWorkArea()
    {
        if (WindowState != WindowState.Normal || double.IsNaN(Left) || double.IsNaN(Top)) return;
        var wa = SystemParameters.WorkArea;
        // Multi-monitor: only clamp against the primary work area when the window is actually on it.
        var onPrimary = Left < wa.Right && Left + ActualWidth > wa.Left && Top < wa.Bottom && Top + ActualHeight > wa.Top;
        if (!onPrimary) return;
        if (Left + ActualWidth > wa.Right) Left = Math.Max(wa.Left, wa.Right - ActualWidth);
        if (Top + ActualHeight > wa.Bottom) Top = Math.Max(wa.Top, wa.Bottom - ActualHeight);
    }

    private void Panel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    // ---- Width resizing ----
    // ResizeMode=CanResize gives the window a (invisible) thick frame so Windows can run a size drag,
    // but we take over hit-testing so only our two edge grips start one: height must stay SizeToContent.

    private void LeftGrip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => BeginResize(HTLEFT);
    private void RightGrip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => BeginResize(HTRIGHT);

    private void BottomGrip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            if (DataContext is MainViewModel vm) vm.ListHeight = 0;   // back to "grow with content"
            return;
        }
        _resizingHeight = true;
        BeginResize(HTBOTTOM);
    }

    private void BeginResize(int hitTest)
    {
        ReleaseCapture();
        SendMessage(new WindowInteropHelper(this).Handle, WM_NCLBUTTONDOWN, hitTest, IntPtr.Zero);
    }

    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case WM_NCHITTEST:
                // Everything is client area: no Windows-provided edge resizing (which would also allow height).
                handled = true;
                return HTCLIENT;
            case WM_ENTERSIZEMOVE:
                _heightBeforeResize = ActualHeight;
                break;
            case WM_EXITSIZEMOVE:
                // A user resize switches SizeToContent to Manual; put it back so rows keep driving the height,
                // and persist the new width. A bottom-edge drag becomes the list's maximum height.
                var grewBy = ActualHeight - _heightBeforeResize;
                var heightDrag = _resizingHeight;
                _resizingHeight = false;
                Dispatcher.BeginInvoke(() =>
                {
                    if (DataContext is MainViewModel vm)
                    {
                        if (heightDrag)
                        {
                            var current = vm.ListHeight > 0 ? Math.Min(vm.ListHeight, List.ActualHeight) : List.ActualHeight;
                            vm.ListHeight = Math.Max(40, current + grewBy);
                        }
                        vm.WindowWidth = ActualWidth;
                    }
                    SizeToContent = SizeToContent.Height;
                });
                break;
        }
        return IntPtr.Zero;
    }
}
