using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using BtcWidget.Models;
using BtcWidget.Services;
using BtcWidget.ViewModels;
using BtcWidget.Views;

namespace BtcWidget;

public partial class App : Application
{
    private SettingsStore _settingsStore = null!;
    private AppSettings _settings = null!;
    private FeedStore _feedStore = null!;
    private List<NewsFeed> _feeds = null!;
    private HistoryStore _history = null!;
    private PriceService _prices = null!;
    private NewsService _news = null!;
    private MainViewModel _vm = null!;
    private MainWindow _window = null!;
    private NewsWindow? _newsWindow;
    private DateTime _newsClosedAt;
    private ReaderWindow? _reader;
    private HotkeyManager? _hotkey;
    private TrayIcon? _tray;
    private ContextMenu? _trayMenu;
    private readonly DispatcherTimer _saveDebounce = new() { Interval = TimeSpan.FromMilliseconds(600) };
    /// <summary>Counts down from the moment the widget is minimized; at zero it comes back without taking focus.</summary>
    private readonly DispatcherTimer _restoreTimer = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _settingsStore = new SettingsStore();
        _settings = _settingsStore.Load();
        _history = new HistoryStore();
        _prices = new PriceService(_history, TimeSpan.FromMinutes(Math.Max(1, _settings.PollIntervalMinutes)));
        _news = new NewsService(TimeSpan.FromMinutes(Math.Max(1, _settings.NewsRefreshMinutes)));
        _feedStore = new FeedStore();
        _feeds = _feedStore.Load();
        MigrateFeedSettings();
        _vm = new MainViewModel(_settings, _prices, _news, _feeds);
        _vm.FeedsSaveRequested += () => { try { _feedStore.Save(_feeds); } catch { /* read-only profile etc. */ } };

        _window = new MainWindow { DataContext = _vm };
        MainWindow = _window;

        _vm.MinimizeRequested += () => _window.WindowState = WindowState.Minimized;
        _vm.CloseRequested += () => _window.Close();
        _vm.SettingsRequested += ShowSettings;
        _vm.ManageLinesRequested += ShowLines;
        _vm.AlertsRequested += ShowAlerts;
        _vm.NewsRequested += ToggleNews;
        _vm.RestoreRequested += RestoreQuietly;
        _restoreTimer.Tick += (_, _) => { _restoreTimer.Stop(); RestoreQuietly(); };
        _window.StateChanged += (_, _) => ArmRestoreTimer();
        _vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(MainViewModel.RestoreAfterMinutes)) ArmRestoreTimer(); };
        _vm.ArticleRequested += ShowReader;
        _vm.Notifier = (title, body) => _tray?.ShowBalloon(title, body) ?? false;
        _vm.SettingsChanged += () => { _saveDebounce.Stop(); _saveDebounce.Start(); };
        _saveDebounce.Tick += (_, _) => { _saveDebounce.Stop(); SaveSettings(); ApplyHotkey(); };

        _window.Closing += (_, _) => SaveSettings();
        _window.SourceInitialized += (_, _) =>
        {
            _hotkey = new HotkeyManager(_window);
            _hotkey.Pressed += ToggleWindow;
            ApplyHotkey();

            try
            {
                _tray = new TrayIcon(_window, "BtcWidget");
                _tray.Clicked += ToggleWindow;
                _tray.BalloonClicked += ShowWindow;
                _tray.ContextMenuRequested += ShowTrayMenu;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"tray: {ex.Message}");
            }
        };

        _window.Show();
        _prices.Start();
        _news.Start();
        MemoryDiagnostics.Snapshot("startup");
    }

    /// <summary>
    /// Versions before 1.3 kept the enabled feed ids and user-added feeds in settings.json. Fold them into
    /// feeds.json once, then drop them from settings.
    /// </summary>
    private void MigrateFeedSettings()
    {
        if (_settings.NewsFeeds is null && _settings.CustomNewsFeeds is null) return;
        if (_settings.CustomNewsFeeds is { } custom)
            foreach (var c in custom.Where(c => !string.IsNullOrWhiteSpace(c.Url) && _feeds.All(f => f.Id != c.Id)))
                _feeds.Add(new NewsFeed { Id = c.Id, Name = c.Name, Url = c.Url, Site = c.Site, Enabled = true });
        if (_settings.NewsFeeds is { } on)
        {
            var set = on.ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var f in _feeds) f.Enabled = set.Contains(f.Id);
        }
        _settings.NewsFeeds = null;
        _settings.CustomNewsFeeds = null;
        try { _feedStore.Save(_feeds); _settingsStore.Save(_settings); } catch { /* read-only profile etc. */ }
    }

    private void ShowLines()
    {
        MemoryDiagnostics.Snapshot("before Lines dialog");
        var scale = _settings.FontSize / 13.0;
        var dlg = new LinesWindow
        {
            Owner = _window, DataContext = new LinesViewModel(_vm, _prices),
            FontSize = _settings.FontSize, Width = 860 * scale, Height = 520 * scale
        };
        dlg.ShowDialog();
        TrimHeap();
        MemoryDiagnostics.Snapshot("after Lines dialog + trim");
    }

    /// <summary>Alerts dialog; <paramref name="lineId"/> preselects a line (row context menu).</summary>
    private void ShowAlerts(Guid? lineId)
    {
        var scale = _settings.FontSize / 13.0;
        var dlg = new AlertsWindow
        {
            Owner = _window, DataContext = new AlertsViewModel(_vm, lineId),
            FontSize = _settings.FontSize, Width = 640 * scale, Height = 460 * scale
        };
        dlg.ShowDialog();
        TrimHeap();
    }

    /// <summary>
    /// News popup, anchored to the widget: below it when there is room, else above, right edges aligned.
    /// It closes when it loses focus; clicking the title-bar button while it is open therefore first closes
    /// it (deactivation) and must not reopen it straight away.
    /// </summary>
    private void ToggleNews()
    {
        if (_newsWindow is not null) { _newsWindow.Close(); return; }
        if ((DateTime.UtcNow - _newsClosedAt).TotalMilliseconds < 400) return;

        var vm = new NewsViewModel(_vm, _news);
        var win = new NewsWindow
        {
            Owner = _window, DataContext = vm, FontSize = _settings.FontSize,
            Width = Math.Max(300, _settings.NewsWidth), Height = Math.Max(200, _settings.NewsHeight)
        };
        vm.CloseRequested += () => win.Close();

        var wa = SystemParameters.WorkArea;
        var below = _window.Top + _window.ActualHeight - 8;
        var top = below + win.Height <= wa.Bottom ? below : _window.Top - win.Height + 8;
        var left = _window.Left + _window.ActualWidth - win.Width;
        win.Left = Math.Max(wa.Left, Math.Min(left, wa.Right - win.Width));
        win.Top = Math.Max(wa.Top, Math.Min(top, wa.Bottom - win.Height));

        win.Closed += (_, _) =>
        {
            vm.Detach();
            _vm.SaveNewsSize(win.ActualWidth, win.ActualHeight);
            _vm.MarkNewsSeen();
            _newsWindow = null;
            _newsClosedAt = DateTime.UtcNow;
            TrimHeap();
        };
        _newsWindow = win;
        win.Show();
    }

    /// <summary>Built-in reader (Settings → News → open in). One window, reused while it is open.</summary>
    private void ShowReader(Uri url, string title)
    {
        if (_reader is null)
        {
            _reader = new ReaderWindow
            {
                Width = Math.Max(400, _settings.ReaderWidth), Height = Math.Max(300, _settings.ReaderHeight),
                FontSize = _settings.FontSize, DarkPages = _settings.ReaderDarkPages
            };
            _reader.DarkPagesChanged += dark => { _settings.ReaderDarkPages = dark; SaveSettings(); };
            _reader.Closed += (_, _) =>
            {
                if (_reader is not null)
                {
                    _settings.ReaderWidth = _reader.ActualWidth;
                    _settings.ReaderHeight = _reader.ActualHeight;
                }
                _reader = null;
                SaveSettings();
                TrimHeap();
            };
            var wa = SystemParameters.WorkArea;
            _reader.Left = wa.Left + (wa.Width - _reader.Width) / 2;
            _reader.Top = wa.Top + (wa.Height - _reader.Height) / 2;
            _reader.Show();
        }
        _reader.Navigate(url, title);
        _reader.Activate();
    }

    /// <summary>
    /// The dialogs allocate far more than the widget ever does (search results, logos, templates). The idle
    /// widget then allocates so little that the GC would sit on that garbage for hours; collect it now.
    /// </summary>
    private static void TrimHeap()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
    }

    private void ApplyHotkey()
    {
        if (_hotkey is null) return;
        if (!_hotkey.Register(_settings.ToggleHotkey))
            _vm.Status = $"Hotkey '{_settings.ToggleHotkey}' unavailable";
    }

    /// <summary>Global hotkey / tray click: bring the widget to the front, or minimize it if it is already in front.</summary>
    private void ToggleWindow()
    {
        if (_window.WindowState == WindowState.Minimized || !_window.IsVisible)
        {
            ShowWindow();
        }
        else if (_window.IsActive)
        {
            _window.WindowState = WindowState.Minimized;
        }
        else
        {
            _window.Activate();
        }
    }

    /// <summary>Start (or cancel) the "come back after X minutes" countdown to match the window state and setting.</summary>
    private void ArmRestoreTimer()
    {
        _restoreTimer.Stop();
        var minutes = _settings.RestoreAfterMinutes;
        if (minutes <= 0 || _window.WindowState != WindowState.Minimized) return;
        _restoreTimer.Interval = TimeSpan.FromMinutes(minutes);
        _restoreTimer.Start();
    }

    /// <summary>Bring a minimized widget back without stealing focus. No-op when it is already showing.</summary>
    private void RestoreQuietly()
    {
        if (_window.WindowState != WindowState.Minimized && _window.IsVisible) return;
        _window.RestoreWithoutActivating();
    }

    private void ShowWindow()
    {
        _window.Show();
        _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    private void ShowTrayMenu()
    {
        _trayMenu ??= BuildTrayMenu();
        // A menu opened from the tray needs its owner in front, or it will not close when focus moves away.
        _window.Activate();
        _trayMenu.IsOpen = true;
    }

    private ContextMenu BuildTrayMenu()
    {
        var menu = new ContextMenu();
        menu.Items.Add(Item("Show / hide", ToggleWindow));
        menu.Items.Add(Item("Refresh now", () => _vm.RefreshCommand.Execute(null)));
        menu.Items.Add(Item("Lines…", ShowLines));
        menu.Items.Add(Item("Alerts…", () => ShowAlerts(null)));
        menu.Items.Add(Item("News…", () => { ShowWindow(); ToggleNews(); }));
        menu.Items.Add(Item("Settings…", ShowSettings));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Exit", () => _window.Close()));
        return menu;

        static MenuItem Item(string header, Action action)
        {
            var mi = new MenuItem { Header = header };
            mi.Click += (_, _) => action();
            return mi;
        }
    }

    private void ShowSettings()
    {
        MemoryDiagnostics.Snapshot("before Settings dialog");
        var dlg = new SettingsWindow { Owner = _window, DataContext = _vm };
        dlg.ShowDialog();
        TrimHeap();
        MemoryDiagnostics.Snapshot("after Settings dialog + trim");
    }

    private void SaveSettings()
    {
        if (_window is { WindowState: WindowState.Normal })
        {
            _settings.Left = _window.Left;
            _settings.Top = _window.Top;
        }
        try { _settingsStore.Save(_settings); } catch { /* read-only profile etc. */ }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _hotkey?.Dispose();
        _prices.Dispose();
        _news.Dispose();
        _history.Dispose();
        base.OnExit(e);
    }
}
