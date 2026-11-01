using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using BtcWidget.Models;

namespace BtcWidget.ViewModels;

/// <summary>One sector in Settings → News: a collapsible header ("Stocks & markets · 3 of 78 on") over its feeds.</summary>
public sealed partial class NewsFeedGroup : ObservableObject
{
    public NewsFeedGroup(string sector)
    {
        Sector = sector;
        Label = NewsFeed.SectorLabel(sector);
        Options.CollectionChanged += (_, _) => Refresh();
    }

    public string Sector { get; }
    public string Label { get; }
    public ObservableCollection<NewsFeedOption> Options { get; } = [];

    [ObservableProperty] private string _summary = "";
    /// <summary>Open when any feed in it is on; the rest start folded so 130 feeds do not make a wall.</summary>
    [ObservableProperty] private bool _isExpanded;

    public void Refresh()
    {
        var on = Options.Count(o => o.IsEnabled);
        Summary = $"{on} of {Options.Count} on";
    }
}
