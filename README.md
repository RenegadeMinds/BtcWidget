# BtcWidget

A small, always-available Windows desktop widget for crypto, stock and precious-metal prices, with alerts
and a news popup.

It is one dark, translucent panel that lives on your desktop. Keep it as a narrow column in a corner, or
stretch it into a slim bar across the screen. Put whatever you want to watch in it: Bitcoin on Kraken in
CAD, Tesla priced in BTC, the S&P 500, spot gold from Kitco, Samsung on the Korea Exchange.

There is no account, no API key, no telemetry and no installer.

- **Platform:** Windows 10 or 11, x64
- **Built with:** C#, WPF, .NET 10
- **License:** [WTFPL](LICENSE)
- **Full user guide:** [HELP.md](HELP.md)
- **Version history:** [CHANGELOG.md](CHANGELOG.md)

## Contents

- [Features](#features)
- [Download and run](#download-and-run)
- [Quick start](#quick-start)
- [Upgrading](#upgrading)
- [Uninstalling](#uninstalling)
- [Where the data comes from](#where-the-data-comes-from)
- [Privacy and network use](#privacy-and-network-use)
- [Your files](#your-files)
- [Building from source](#building-from-source)
- [Project layout](#project-layout)
- [Extending it](#extending-it)
- [Known limitations](#known-limitations)
- [License and disclaimer](#license-and-disclaimer)

## Features

### Prices

- **Lines.** Each line is one instrument, from one provider, in one currency. You can have the same
  instrument twice, for example Bitcoin from Kraken and from CoinGecko, to compare them.
- **Eight keyless price sources.** Kraken, Coinbase, Binance, CoinGecko, Yahoo Finance, Nasdaq, Kitco and
  gold-api.com.
- **Crypto.** Well over a thousand trading pairs on the exchanges, and many more coins through CoinGecko.
- **Stocks on major exchanges.** NYSE, NASDAQ, Toronto (`SHOP.TO`), TSX Venture (`LIXX.V`), the Canadian
  Securities Exchange (`GTII.CN`), Korea (`005930.KS`), Tokyo (`7203.T`), Frankfurt and anything else Yahoo
  Finance lists. ETFs, indices, FX and futures work the same way.
- **Spot precious metals.** Gold, silver, platinum and palladium from Kitco or gold-api.com.
- **Any currency.** Show any line in any quote currency. When no market quotes the pair directly, the widget
  derives the price through USD or an FX rate, labels the row "derived", and shows each step in the tooltip.
  Tesla in BTC is TSLA/USD ÷ BTC/USD.
- **Context at a glance.** Under each price: a 30-day sparkline, the 7-day and 30-day change, and where the
  price sits in its recent range, such as "4.2% below 30d high".
- **Local history.** Every reading is stored on your PC. New lines backfill history where the provider
  offers it, so the longer-range figures work straight away.

### Layout

- **Column or bar.** Lines stack in a narrow widget and tile side by side once two or more fit at full
  readability.
- **Measured, not configured.** The tile width comes from your actual lines, so it follows the font size,
  price lengths and whether the detail row is on. Columns stay aligned across tiles.
- **Stays together.** Spare width never spreads the tiles apart. They stay as a group on the left or right,
  with a tiny arrow at the end of the empty space to switch sides.
- **Slim one-row bar.** When everything fits on one row, the status and links move into the title bar.
- **Resizable from four edges.** Left and right set the width, up to the full width of the monitor the
  widget is on. Top and bottom set the list height.
- **Yours to tune.** Panel opacity and text opacity are separate. Font size scales everything, dialogs
  included.

### Alerts

- Price above or below a level, up or down more than a percentage, new 30-day high or low.
- Lines with an alert switched on poll every 5 minutes by default, while everything else polls hourly.
- A fired alert highlights its row, lights the bell, shows the message in the status line, sends a Windows
  notification and is written to a log.
- A minimized widget can come back when an alert fires, globally or per alert.

### News

- A popup of headlines from the feeds you pick, merged into one list, newest first, with date separators.
- **134 RSS and Atom feeds** to choose from, tagged by sector and country: crypto, stocks, ETFs, macro and
  central banks, commodities and forex, from the US, Canada, the UK, Europe, Korea, Japan, China, Hong Kong,
  Taiwan, India, Singapore and Australia. Add any other feed by URL.
- Comma-separated text filter (`bitcoin, gold, bank`), sector and source filter, and a pin that keeps the
  popup open while you read. Drag any edge or corner to resize it.
- Open articles in your default browser or in a built-in reader window.

### Staying out of your way

- **Never steals focus.** A minimized widget can come back on a timer or on an alert, in front of your
  other windows, while whatever you are typing into keeps the keyboard.
- Start with Windows, global hotkey, tray icon, always-on-top, optional taskbar button.
- Only one copy runs. Launching it again brings the running widget forward.
- Tooltips can be switched off.
- The title-bar ✕ can exit or minimize, your choice.

## Download and run

Get the latest version from the [Releases page](https://github.com/RenegadeMinds/BtcWidget/releases). There
are two builds of the same program:

| File | Size | Needs | Best for |
|---|---|---|---|
| `BtcWidget-<version>-win-x64.exe` | about 60 MB | Nothing | The simplest option. One file, run it from anywhere. |
| `BtcWidget-<version>-win-x64.zip` | about 2 MB | [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (x64) | A small download and lower memory use, if you have the runtime or don't mind installing it. |

To run the zip build, unzip it to a folder of your choice and start `BtcWidget.exe`.

**SmartScreen.** The program is not code-signed, so Windows may warn you the first time. Choose **More
info**, then **Run anyway**. If you would rather not trust a binary, [build it yourself](#building-from-source).

**Built-in reader.** The optional news reader uses the Microsoft Edge WebView2 runtime. Windows 11 includes
it. On Windows 10, install it from Microsoft if it is missing. Everything else works without it.

## Quick start

1. Run the program. The widget appears at the bottom right with Bitcoin from Kraken and CoinGecko.
2. Drag the panel to wherever you want it. The position is remembered.
3. Click **+** to add lines. Type `tesla`, `gold cad`, `BTC/CAD` or `shopify`, then click **Add**.
4. Click the **bell** to set an alert, such as "Tesla, price above 410" or "Bitcoin, up more than 3.5%".
5. Click the **newspaper** to read headlines. Choose your feeds in **Settings → News**.
6. Drag the left or right edge to make it wider. Lines tile side by side as soon as two fit.

The title-bar buttons, from left to right, are: **news, alerts, add or remove lines, refresh, settings,
about (?), minimize, close**.

For everything else, including every setting, see the **[user guide](HELP.md)**.

## Upgrading

Replace the old exe, or the old unzipped folder, with the new one. Your lines, alerts, settings, feeds and
price history live in a separate folder and carry over.

If you use **Start with Windows** and the new exe has a different name or location, run it once. The widget
updates its startup entry to point at itself if the setting is on.

**Your edits survive, and fixes still arrive.** If a new version corrects a provider URL or adds a footer link,
you get it automatically, unless you changed that same value yourself, in which case yours is kept. A copy of
your file from before the update is saved in the `shipped` folder.

## Uninstalling

There is no installer, so removal is manual:

1. If **Start with Windows** is on, switch it off in Settings.
2. Exit from the tray icon menu.
3. Delete the exe or the unzipped folder.
4. Delete `%LocalAppData%\BtcWidget` to remove your settings and history.
5. If you used the single-file exe, also delete its unpacked files in `%TEMP%\.net`, in the folder named
   after the exe.

## Where the data comes from

| Id | Provider | Covers | Search | History | Notes |
|---|---|---|---|---|---|
| `kraken` | Kraken | Crypto, about 1,400 pairs | | 30 days, hourly | Official API. First choice for coins and pairs it lists, and the default source of conversion rates. |
| `coinbase` | Coinbase | Crypto | | 75 days, 6-hourly | Official API. Second choice for coins. |
| `binance` | Binance | Crypto ("USD" means the USDT market) | | 30 days, hourly | Official API. binance.com is blocked in some regions. |
| `coingecko` | CoinGecko | Crypto in about 60 currencies | Yes | | Aggregator. Used when no exchange has the pair, and the default fallback for conversion rates. Supplies coin logos and the popular list. |
| `yahoo` | Yahoo Finance | Stocks worldwide, ETFs, indices, FX, futures, crypto pairs | Yes | 3 months, daily | Unofficial endpoint. Default for everything except crypto and metals. Source of all FX rates. |
| `nasdaq` | Nasdaq | US stocks and ETFs | | | Unofficial endpoint. An alternative if Yahoo misbehaves. |
| `kitco` | Kitco | Gold, silver, platinum, palladium in USD, CAD, EUR and more | Yes (metals) | Through Yahoo futures | Unofficial endpoint behind Kitco's charts. Mid price and change since the previous close. Default for metals. |
| `goldapi` | gold-api.com | Gold, silver, platinum, palladium in major currencies | | Through Yahoo futures | Price only. The change is calculated from history. |

All of them are public and need no key. Polling is hourly by default, or every 5 minutes for lines with an
alert.

**Metal history.** Neither metal source offers free history, so a new metal line backfills daily closes from
the matching futures contract on Yahoo (`GC=F`, `SI=F`, `PL=F`, `PA=F`). Futures usually trade slightly away
from spot, so the first month of sparkline and 7-day / 30-day figures is futures-based. The widget's own spot
readings then take over.

**News.** The shipped feed list is [`BtcWidget/Data/feeds.json`](BtcWidget/Data/feeds.json). Every URL was
checked on 2026-11-01: 253 candidates were probed and the 134 that worked were kept. These outlets are absent
because they block feed readers or have no working feed: Reuters, Yahoo Finance, Barron's, Korea JoongAng
Daily, Korea Times, Kyodo, NHK World (English), Asahi AJW, Caixin, Focus Taiwan, Financial Post, Kitco News,
Glassnode, Coinbase and Binance.

## Privacy and network use

BtcWidget has no accounts, analytics or telemetry. It only contacts:

- **The price providers** used by your lines.
- **Yahoo Finance** for FX rates and metal history, and **Kraken or CoinGecko** (your choice) for crypto
  conversion rates, when a line needs them.
- **CoinGecko and Yahoo Finance** when you type in the Lines search box or open that dialog, and the
  **exchanges** when you add a line, to find the best market for it.
- **CoinGecko's image host** the first time a coin logo is needed. Logos are then cached on disk.
- **The news feeds** you have switched on.
- **The GitHub API**, to check for a newer release. You can turn off the automatic check in the About dialog.
- **Any page you open** from the widget, in your browser or the built-in reader.

Everything you configure stays on your PC.

## Your files

Everything is in `%LocalAppData%\BtcWidget\`:

| File or folder | Contents |
|---|---|
| `settings.json` | Every setting, the window position, your lines and their alerts, and the last update check |
| `feeds.json` | Your news feeds. Created from the shipped list on first run, then entirely yours |
| `providers.json` | Provider names, icons, page links, API endpoints, footer links, the metals list and the starter lines |
| `currencies.json` | The quote currencies offered in dropdowns and the symbol for each fiat currency |
| `history.db` | SQLite database of every price reading |
| `alerts.log` | One line for each alert that fired |
| `shipped\` | Reference copies of the data files, used to tell your edits apart from shipped values when you upgrade, plus your file as it was before the last update |
| `*.broken-<date>.json` | A copy of a settings or feeds file that couldn't be read, kept so you can repair it |
| `cache\` | Downloaded logos. Safe to delete |
| `webview\` | Built-in reader profile. Only exists once the reader has been used. Safe to delete |

**Hand-edit safely.** If `feeds.json` or `settings.json` has a mistake, the widget keeps a copy of your file, tells
you with a notification, and carries on with defaults. If `providers.json` or `currencies.json` has a mistake, your
file is left alone and the built-in values are used until you fix it.

Delete the whole folder and the widget starts fresh. The [user guide](HELP.md#customizing-with-the-data-files)
explains how to edit the JSON files.

## Building from source

**Requirements:** Windows and the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). Visual
Studio 2026 with the **.NET desktop development** workload is optional, for the XAML designer and debugger.

```bash
git clone https://github.com/RenegadeMinds/BtcWidget.git
```

```bash
cd BtcWidget
```

```bash
dotnet build BtcWidget -c Release
```

The program is then at `BtcWidget\bin\Release\net10.0-windows\BtcWidget.exe`. You can also open
`BtcWidget.sln` in Visual Studio and press F5.

To produce both release files in the `release` folder:

```bash
powershell -ExecutionPolicy Bypass -File .\build-release.ps1
```

The script runs `dotnet publish` twice: framework-dependent for the zip, and self-contained single-file for
the exe. The version number comes from `<Version>` in `BtcWidget\BtcWidget.csproj`.

**NuGet dependencies:** CommunityToolkit.Mvvm, Microsoft.Data.Sqlite, SQLitePCLRaw.bundle_e_sqlite3 and
Microsoft.Web.WebView2.

## Project layout

| Folder (under `BtcWidget/`) | Purpose |
|---|---|
| `Data/` | `feeds.json`, `providers.json`, `currencies.json`: every external URL, name and list the app uses. Copied beside the exe and embedded in the assembly. `baseline/` holds what version 1.4.1 shipped, so older copies can be upgraded without losing edits |
| `Models/` | `AppSettings` and `LineConfig` (persisted), `NewsFeed`, `PricePoint`, `PriceQuote`, `SearchResult` |
| `Services/` | `IPriceSource` and `IInstrumentSearch`, the provider classes (`KrakenSource`, `CoinbaseSource`, `BinanceSource`, `CoinGeckoSource`, `YahooSource`, `NasdaqSource`, and `KitcoSource` and `GoldApiSource` in `MetalSources.cs`), `PriceService` (polling, derived prices, search), `HistoryStore`, `PriceAnalysis`, `AlertEngine`, `AlertLog`, `NewsService`, `FeedStore`, `ProviderData` (data-file loader and URL templates), `ImageCache`, `UpdateChecker`, `StartupManager`, `TrayIcon`, `HotkeyManager`, `SettingsStore`, `MemoryDiagnostics` |
| `ViewModels/` | `MainViewModel`, `PriceRowViewModel`, `LinesViewModel`, `AlertsViewModel`, `NewsViewModel`, `NewsFeedOption`, `NewsFeedGroup`, `AboutViewModel`, `LinkViewModel` |
| `Views/` | `MainWindow` (the widget), `LinesWindow`, `AlertsWindow`, `NewsWindow`, `ReaderWindow`, `SettingsWindow`, `AboutWindow` |
| `Controls/` | `Sparkline`, and `TilePanel` (measured-width tiling and alignment) |
| `Converters/` | Price, percent and colour value converters |
| `Assets/` | Application icon and provider badges |

Colours and shared control styles are in `App.xaml`.

## Extending it

**Change a URL or add a footer link.** Edit `Data/providers.json`. No code change is needed. Templates use
placeholders such as `{base}`, `{QUOTE}` and `{coinId}`, listed in the file's `_comment`.

**Add a news feed.** Add an entry to `Data/feeds.json`, or use **Settings → News → Add a feed** at runtime.

**Add a metal.** Add an entry to `metals` in `Data/providers.json`.

**Add a price provider:**

1. Add an entry for it under `providers` in `Data/providers.json`: name, icon, page template and API URLs.
2. Add a class implementing `IPriceSource` in `Services/`. `NasdaqSource` is the minimal shape, and
   `CoinbaseSource` shows history and a cheap pair check. Read its URLs through `Providers.Get("yourid")`.
   Implement `IInstrumentSearch` too if it can search.
3. Throw `PairNotSupportedException` when it has no market for `line.EffectiveQuote`. `PriceService` then
   derives the price.
4. Add it to the `all` array in the `PriceService` constructor, and put a badge PNG in `Assets/`.
5. It now appears in the provider dropdown for matching instrument kinds. To make it a default, extend
   `PriceService.PickProviderAsync`.

## Known limitations

- **`history.db` is never trimmed.** It grows slowly, by about 300 rows a day for each line with an alert.
- **Memory.** After startup, expect roughly 240 MB of private memory for the single-file exe and 170 MB for
  the zip build. Most of that is the .NET runtime and WPF. The first click into any text box adds about 60 MB
  for the life of the process, because Windows loads its text-input components. The built-in reader adds more
  while it is open and releases it when closed. Set the environment variable `BTCWIDGET_DIAG=1` to log memory
  snapshots to `diag.log`.
- **Unofficial endpoints.** Yahoo Finance, Nasdaq and Kitco don't offer official free APIs and could change
  without notice. Because their URLs are in `providers.json`, most breakages can be fixed by editing a file.
- **Not real-time.** Prices are as fresh as your polling interval and the provider's own delay. Some stock
  exchanges are delayed by 15 minutes or more on free sources.
- **Windows x64 only.**

## License and disclaimer

Released under the [WTFPL](http://www.wtfpl.net/), the Do What The F\*ck You Want To Public License. See
[LICENSE](LICENSE).

Copyright © 2025-2026 Ryan Smyth / Renegade Minds.

This software is for information only and is **not financial advice**. Prices, alerts and news come from
third-party public sources with no guarantee of accuracy, timeliness or availability. Don't rely on it for
trading decisions. All product and company names belong to their owners, and this project isn't affiliated
with any of the data providers or news outlets it can display.
