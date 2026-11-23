using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using BtcWidget.Controls;
using BtcWidget.ViewModels;

namespace BtcWidget.Views;

public partial class MainWindow : Window
{
    private const int WM_NCHITTEST = 0x0084, WM_NCLBUTTONDOWN = 0x00A1, WM_ENTERSIZEMOVE = 0x0231, WM_EXITSIZEMOVE = 0x0232,
        WM_SIZING = 0x0214, WM_DISPLAYCHANGE = 0x007E, WM_DPICHANGED = 0x02E0, WM_SETTINGCHANGE = 0x001A;
    private const int HTCLIENT = 1, HTLEFT = 10, HTRIGHT = 11, HTTOP = 12, HTBOTTOM = 15;
    private const int WMSZ_LEFT = 1, SPI_SETWORKAREA = 47;
    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private double _heightBeforeResize, _bottomBeforeResize;
    private int _resizeEdge;
    private bool _keepBottom;
    private TilePanel? _tiles;

    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MONITORINFO { public int cbSize; public RECT rcMonitor, rcWork; public uint dwFlags; }
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

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
        UpdateMonitorLimits();
        ClampToWorkArea();
    }

    private void Tiles_Loaded(object sender, RoutedEventArgs e)
    {
        _tiles = (TilePanel)sender;
        _tiles.Reflowed += t =>
        {
            if (DataContext is not MainViewModel vm) return;
            vm.IsSingleRow = t.IsSingleRow;
            vm.HasTileSpace = t.HasSpareRoom;
        };
        if (DataContext is not MainViewModel m) return;
        m.IsSingleRow = _tiles.IsSingleRow;
        m.HasTileSpace = _tiles.HasSpareRoom;

        // Anything that can change how wide a line wants to be re-measures the tiles.
        foreach (var row in m.Rows) row.PropertyChanged += Row_PropertyChanged;
        m.Rows.CollectionChanged += (_, c) =>
        {
            if (c.OldItems is not null) foreach (PriceRowViewModel row in c.OldItems) row.PropertyChanged -= Row_PropertyChanged;
            if (c.NewItems is not null) foreach (PriceRowViewModel row in c.NewItems) row.PropertyChanged += Row_PropertyChanged;
            _tiles?.Refresh();
        };
        m.PropertyChanged += (_, p) =>
        {
            if (p.PropertyName is nameof(MainViewModel.FontSize) or nameof(MainViewModel.ShowDetails)) _tiles?.Refresh();
        };
    }

    private void Row_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => _tiles?.Refresh();

    /// <summary>Work area of the monitor the widget is mostly on, in WPF units.</summary>
    public Rect CurrentWorkArea()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var target = PresentationSource.FromVisual(this)?.CompositionTarget;
        if (hwnd == IntPtr.Zero || target is null) return SystemParameters.WorkArea;
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST), ref info)) return SystemParameters.WorkArea;
        var m = target.TransformFromDevice;
        return new Rect(m.Transform(new Point(info.rcWork.Left, info.rcWork.Top)), m.Transform(new Point(info.rcWork.Right, info.rcWork.Bottom)));
    }

    /// <summary>The widget can be as wide as the screen it is on. The chosen width is kept, so it regrows on a bigger one.</summary>
    private void UpdateMonitorLimits()
    {
        if (DataContext is MainViewModel vm) vm.MaxWindowWidth = Math.Max(MinWidth, CurrentWorkArea().Width);
    }

    /// <summary>
    /// Keeps the whole widget visible when it grows (wider font, more lines, a width change from Settings):
    /// shifts it left / up as needed, never resizes it. Uses the work area of the monitor it is on.
    /// </summary>
    private void ClampToWorkArea()
    {
        if (_resizeEdge != 0 || WindowState != WindowState.Normal || double.IsNaN(Left) || double.IsNaN(Top)) return;
        var wa = CurrentWorkArea();
        if (Left + ActualWidth > wa.Right) Left = Math.Max(wa.Left, wa.Right - ActualWidth);
        if (Top + ActualHeight > wa.Bottom) Top = Math.Max(wa.Top, wa.Bottom - ActualHeight);
    }

    private void Panel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    // ---- Resizing ----
    // ResizeMode=CanResize gives the window a (invisible) thick frame so Windows can run a size drag,
    // but we take over hit-testing so only our edge grips start one. Left / right change the width and the
    // height follows the tiles live; top / bottom set the list's maximum height.

    private void LeftGrip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => BeginResize(HTLEFT);
    private void RightGrip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => BeginResize(HTRIGHT);

    private void TopGrip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => HeightGrip(e, HTTOP);
    private void BottomGrip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => HeightGrip(e, HTBOTTOM);

    private void HeightGrip(MouseButtonEventArgs e, int edge)
    {
        if (e.ClickCount == 2)
        {
            if (DataContext is MainViewModel vm) vm.ListHeight = 0;   // back to "grow with content"
            return;
        }
        BeginResize(edge);
    }

    private void BeginResize(int hitTest)
    {
        _resizeEdge = hitTest;
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
                _bottomBeforeResize = Top + ActualHeight;
                // A width drag changes the height: keep whichever edge is nearer the screen edge where it is.
                var area = CurrentWorkArea();
                _keepBottom = _resizeEdge == HTTOP
                              || (_resizeEdge != HTBOTTOM && area.Bottom - _bottomBeforeResize < Top - area.Top);
                break;
            case WM_SIZING:
                if (_resizeEdge is HTLEFT or HTRIGHT)
                {
                    FitHeightToTiles(wParam.ToInt32(), lParam);
                    handled = true;
                    return new IntPtr(1);
                }
                break;
            case WM_EXITSIZEMOVE:
                // A user resize switches SizeToContent to Manual; put it back so rows keep driving the height.
                // A top or bottom drag becomes the list's maximum height; a side drag is the new width.
                var edge = _resizeEdge;
                var grewBy = ActualHeight - _heightBeforeResize;
                var keepBottom = _keepBottom;
                var bottom = Top + ActualHeight;
                _resizeEdge = 0;
                Dispatcher.BeginInvoke(() =>
                {
                    if (edge != 0 && DataContext is MainViewModel vm)
                    {
                        if (edge is HTTOP or HTBOTTOM)
                        {
                            var current = vm.ListHeight > 0 ? Math.Min(vm.ListHeight, List.ActualHeight) : List.ActualHeight;
                            vm.ListHeight = Math.Max(40, current + grewBy);
                        }
                        else vm.WindowWidth = ActualWidth;
                        SizeToContent = SizeToContent.Height;
                        if (keepBottom)
                            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () => Top = bottom - ActualHeight);
                    }
                    // A drag may have ended on another monitor.
                    UpdateMonitorLimits();
                    Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, ClampToWorkArea);
                });
                break;
            case WM_DISPLAYCHANGE:
            case WM_DPICHANGED:
            case WM_SETTINGCHANGE when wParam.ToInt32() == SPI_SETWORKAREA:
                Dispatcher.BeginInvoke(() => { UpdateMonitorLimits(); ClampToWorkArea(); });
                break;
        }
        return IntPtr.Zero;
    }

    /// <summary>During a side drag: keep the width in limits and size the window to the tiles at that width, live.</summary>
    private void FitHeightToTiles(int sizingEdge, IntPtr rectPtr)
    {
        var target = PresentationSource.FromVisual(this)?.CompositionTarget;
        if (target is null || Content is not UIElement root) return;
        var toDip = target.TransformFromDevice;
        var r = Marshal.PtrToStructure<RECT>(rectPtr);

        var minPx = (int)Math.Ceiling(MinWidth / toDip.M11);
        var maxPx = (int)Math.Floor(MaxWidth / toDip.M11);
        var widthPx = Math.Clamp(r.Right - r.Left, minPx, Math.Max(minPx, maxPx));
        if (sizingEdge == WMSZ_LEFT) r.Left = r.Right - widthPx; else r.Right = r.Left + widthPx;

        var settled = root.RenderSize;
        var probe = new Size(widthPx * toDip.M11, double.PositiveInfinity);
        root.Measure(probe);
        // The one-line header changes the height, so settle it before reading the result.
        if (_tiles is not null && DataContext is MainViewModel vm && vm.IsSingleRow != _tiles.MeasuredSingleRow)
        {
            vm.IsSingleRow = _tiles.MeasuredSingleRow;
            root.Measure(probe);
        }
        var heightPx = (int)Math.Ceiling(root.DesiredSize.Height / toDip.M22);
        root.Measure(settled);

        if (_keepBottom) r.Top = r.Bottom - heightPx; else r.Bottom = r.Top + heightPx;
        Marshal.StructureToPtr(r, rectPtr, false);
    }
}
