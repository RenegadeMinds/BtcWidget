using CommunityToolkit.Mvvm.Input;

namespace BtcWidget.ViewModels;

/// <summary>An icon in the footer that opens a web page (CoinGecko, Kraken, TradingView, ...).</summary>
public sealed partial class LinkViewModel(string name, string iconUri, Uri url)
{
    public string Name { get; } = name;
    public string IconUri { get; } = iconUri;
    public Uri Url { get; } = url;

    [RelayCommand]
    private void Open() => Shell.OpenUrl(Url);
}
