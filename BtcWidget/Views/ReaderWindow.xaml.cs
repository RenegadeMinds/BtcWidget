using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using BtcWidget.Services;
using BtcWidget.ViewModels;
using Microsoft.Web.WebView2.Core;

namespace BtcWidget.Views;

public partial class ReaderWindow : Window
{
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    private Task<bool>? _ready;
    private Uri? _pending;
    private Uri? _current;
    private bool _darkPages;

    /// <summary>
    /// Whether pages are asked for their dark theme. Off by default: many sites only half-implement
    /// prefers-color-scheme (dark background, dark text), so "as designed" is the safe choice.
    /// </summary>
    public bool DarkPages
    {
        get => _darkPages;
        set { _darkPages = value; ApplyTheme(); }
    }
    public event Action<bool>? DarkPagesChanged;

    private void ApplyTheme()
    {
        ThemeButton.Content = _darkPages ? "" : "";   // moon / sun
        ThemeButton.ToolTip = _darkPages ? "Pages in dark mode. Click for light." : "Pages as designed. Click for dark.";
        Web.DefaultBackgroundColor = _darkPages ? System.Drawing.Color.FromArgb(0x20, 0x23, 0x29) : System.Drawing.Color.White;
        if (Web.CoreWebView2 is { } core)
            core.Profile.PreferredColorScheme = _darkPages ? CoreWebView2PreferredColorScheme.Dark : CoreWebView2PreferredColorScheme.Light;
    }

    public ReaderWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            var dark = 1;
            DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
        };
        Closed += (_, _) => Web.Dispose();
        _ready = InitAsync();
    }

    private async Task<bool> InitAsync()
    {
        try
        {
            // Profile data (cookies, cache) lives beside settings.json so the installed Edge profile is never touched.
            var env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(SettingsStore.DataDirectory, "webview"));
            await Web.EnsureCoreWebView2Async(env);
            var core = Web.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = true;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.AreDevToolsEnabled = false;
            ApplyTheme();
            core.NewWindowRequested += (_, e) => { e.Handled = true; core.Navigate(e.Uri); };
            core.DocumentTitleChanged += (_, _) => { TitleText.Text = core.DocumentTitle; Title = core.DocumentTitle.Length > 0 ? core.DocumentTitle : "Reader"; };
            core.HistoryChanged += (_, _) => { BackButton.IsEnabled = core.CanGoBack; ForwardButton.IsEnabled = core.CanGoForward; };
            core.SourceChanged += (_, _) => { if (Uri.TryCreate(core.Source, UriKind.Absolute, out var u)) _current = u; };
            if (_pending is not null) core.Navigate(_pending.AbsoluteUri);
            return true;
        }
        catch (Exception ex)
        {
            Web.Visibility = Visibility.Collapsed;
            Fallback.Visibility = Visibility.Visible;
            Fallback.Text = "The built-in reader needs the Microsoft Edge WebView2 runtime, which could not be started.\n\n" +
                            ex.Message + "\n\nThe article has been opened in your browser instead; switch “Open headlines in” back to the default browser in Settings → News to stop seeing this.";
            if (_pending is not null) Shell.OpenUrl(_pending);
            return false;
        }
    }

    public void Navigate(Uri url, string title)
    {
        TitleText.Text = title;
        Title = title;
        _pending = url;
        _current = url;
        if (Web.CoreWebView2 is not null) Web.CoreWebView2.Navigate(url.AbsoluteUri);
        else if (_ready is { IsCompleted: true, Result: false }) Shell.OpenUrl(url);
        // else: InitAsync navigates to _pending once the control is ready
    }

    private void Theme_Click(object sender, RoutedEventArgs e)
    {
        DarkPages = !DarkPages;
        DarkPagesChanged?.Invoke(DarkPages);
    }

    private void Back_Click(object sender, RoutedEventArgs e) { if (Web.CoreWebView2?.CanGoBack == true) Web.CoreWebView2.GoBack(); }
    private void Forward_Click(object sender, RoutedEventArgs e) { if (Web.CoreWebView2?.CanGoForward == true) Web.CoreWebView2.GoForward(); }
    private void Reload_Click(object sender, RoutedEventArgs e) => Web.CoreWebView2?.Reload();
    private void OpenExternal_Click(object sender, RoutedEventArgs e) { if (_current is not null) Shell.OpenUrl(_current); }
    private void CopyLink_Click(object sender, RoutedEventArgs e)
    {
        try { if (_current is not null) Clipboard.SetText(_current.AbsoluteUri); } catch { }
    }
}
