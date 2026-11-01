using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BtcWidget.Services;

namespace BtcWidget.ViewModels;

/// <summary>The About dialog: version, author, links, update check.</summary>
public sealed partial class AboutViewModel : ObservableObject
{
    private readonly MainViewModel? _main;

    public string Version => UpdateChecker.CurrentText;
    public string Copyright => "Ryan Smyth / Renegade Minds";
    public string RepoUrl => UpdateChecker.RepoUrl;
    public string LicenseName => "WTFPL";
    public string LicenseUrl => "http://www.wtfpl.net/";

    [ObservableProperty] private string _updateStatus = "";
    [ObservableProperty] private bool _isChecking;
    [ObservableProperty] private string? _downloadUrl;
    public bool HasDownload => DownloadUrl is not null;

    public bool CheckDaily
    {
        get => _main?.CheckForUpdates ?? true;
        set { if (_main is not null) _main.CheckForUpdates = value; }
    }

    public AboutViewModel() : this(null) { }

    public AboutViewModel(MainViewModel? main)
    {
        _main = main;
        if (main?.LatestVersion is { } latest && main.HasUpdate)
        {
            UpdateStatus = $"Version {latest} is available.";
            DownloadUrl = main.LatestUrl ?? UpdateChecker.ReleasesUrl;
        }
        else if (main?.Settings.LastUpdateCheck is { } t)
        {
            UpdateStatus = $"Up to date as of {t.ToLocalTime():d MMM HH:mm}.";
        }
    }

    partial void OnDownloadUrlChanged(string? value) => OnPropertyChanged(nameof(HasDownload));

    [RelayCommand]
    private async Task CheckAsync()
    {
        if (IsChecking) return;
        IsChecking = true;
        UpdateStatus = "Checking…";
        try
        {
            var info = await UpdateChecker.CheckAsync();
            _main?.RecordUpdateCheck(info);
            if (info is null) { UpdateStatus = UpdateChecker.LastError; return; }
            if (info.IsNewer) { UpdateStatus = $"Version {info.Latest.ToString(3)} is available."; DownloadUrl = info.Url; }
            else { UpdateStatus = $"You have the latest version ({Version})."; DownloadUrl = null; }
        }
        finally { IsChecking = false; }
    }

    [RelayCommand] private void OpenRepo() => Shell.OpenUrl(new Uri(RepoUrl));
    [RelayCommand] private void OpenReleases() => Shell.OpenUrl(new Uri(UpdateChecker.ReleasesUrl));
    [RelayCommand] private void OpenLicense() => Shell.OpenUrl(new Uri(LicenseUrl));
    [RelayCommand] private void OpenDownload() { if (DownloadUrl is not null) Shell.OpenUrl(new Uri(DownloadUrl)); }
}
