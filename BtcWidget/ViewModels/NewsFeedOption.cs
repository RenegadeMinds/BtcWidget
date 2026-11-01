using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BtcWidget.Models;

namespace BtcWidget.ViewModels;

/// <summary>One feed in Settings → News: on/off, an editable name, and a remove button.</summary>
public sealed partial class NewsFeedOption : ObservableObject
{
    private readonly MainViewModel _main;

    public NewsFeedOption(NewsFeed feed, MainViewModel main)
    {
        Feed = feed;
        _main = main;
        _isEnabled = feed.Enabled;
        _name = feed.Name.Length > 0 ? feed.Name : feed.Host;
    }

    public NewsFeed Feed { get; }
    public string Id => Feed.Id;
    public string Tooltip => $"{Feed.Url}\n{NewsFeed.SectorLabel(Feed.Sector)} · {Feed.Tag}";
    public string Tag => Feed.Tag;

    [ObservableProperty] private bool _isEnabled;
    [ObservableProperty] private string _name;

    partial void OnIsEnabledChanged(bool value) => _main.SetFeedEnabled(Id, value);
    partial void OnNameChanged(string value) => _main.RenameFeed(Id, value);

    [RelayCommand] private void Remove() => _main.RemoveFeed(Id);

    /// <summary>The service learned the feed's real title; show it.</summary>
    public void NameChanged() => Name = Feed.Name;
}
