# BtcWidget

A small, always-available Windows desktop widget for crypto and stock prices, with alerts and a news
popup. One dark, translucent panel that sits in a corner of your screen: Bitcoin on Kraken, Tesla, the
S&P 500, gold, a stock in BTC, whatever you put in it. No account, no API key, no telemetry.

WPF on .NET 10. Windows 10 / 11, x64.

## What it is

- **Lines.** Each line is one instrument, from one provider, in one quote currency: Bitcoin on Kraken in
  CAD, ETH/BTC, Shopify on Toronto, IBM shown in CAD, Tesla priced in BTC. Price, change, and under it a
  30-day sparkline, 7d / 30d change and where the price sits in its recent range.
- **Six price sources, no keys.** Kraken, Coinbase, Binance, CoinGecko, Yahoo Finance, Nasdaq. Stocks on
  any exchange Yahoo knows (`SHOP.TO`, `005930.KS`, `307.F`), ETFs, indices, FX, futures. When no market
  quotes a pair directly the price is derived through USD or an FX rate and labelled as such.
- **Alerts.** Price above / below, up / down more than N% in 24h, new 30-day high / low. The row lights up,
  a Windows notification goes out, and the line polls every 5 minutes while it has an alert.
- **News.** A popup next to the widget with headlines from the feeds you choose, newest first: 134 crypto,
  stock, macro, commodity and forex feeds to pick from, tagged by sector and country (US, UK, EU, Canada,
  Australia, Korea, Japan, China, Taiwan, India…), plus any RSS / Atom URL you add. Comma-separated text
  filter, sector filter, pin it open, open in your browser or a built-in reader.
- **Stays out of your way.** Minimize it and it comes back after N minutes or when an alert fires, without
  ever taking focus. Start with Windows. Global hotkey. Tray icon. One instance only: launching it again
  just brings the running widget forward. Opacity, font size, width, always-on-top, tooltips on or off.
- **Local data only.** Settings, feeds and price history live in `%LocalAppData%\BtcWidget`. The only
  network traffic is to the providers and feeds you have switched on.

## Download

Grab the latest from the **Releases** page. Two flavours of the same build:

| File | Size | Needs |
|---|---|---|
| `BtcWidget-<version>-win-x64.exe` | ~70 MB | Nothing. Self-contained single file; unzip nothing, run it. |
| `BtcWidget-<version>-win-x64.zip` | ~4 MB | The [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (x64). Unzip anywhere and run `BtcWidget.exe`. |

The built-in news reader uses the Edge WebView2 runtime, which Windows 11 includes; on Windows 10 install
it from Microsoft if you want the reader (everything else works without it).

The exe is not code-signed, so SmartScreen will ask once ("More info" → "Run anyway"). If you would rather
not trust a binary, build it yourself in two commands; see below.

## How to use it

**First run.** The widget appears bottom-right with Bitcoin on Kraken and on CoinGecko. Drag the panel
anywhere; the position is remembered. Drag the left or right edge to change the width, the bottom edge to
cap the list height (double-click the bottom edge to go back to "grow with content").

**Title bar, left to right:** news, alerts, add / remove lines, refresh, settings, about (?), minimize,
close. The ✕ exits by default; Settings → Behaviour can make it minimize instead (Exit stays in the tray
menu either way).

**Lines** (the **+** button). Left: your lines, with reorder, remove, provider and quote switches. Right:
type a name, ticker or pair (`shopify`, `tesla`, `xrp`, `BTC/CAD`, `ETHBTC`, `gold`, `^VIX`, `EURUSD=X`),
pick a quote if you want one other than the default, click Add. Empty search shows the top coins and
common indices, stocks, ETFs, FX and commodities. The default quote is USD for crypto and the listing
currency for stocks; any other quote is derived when needed (Tesla in BTC = TSLA/USD ÷ BTC/USD) and the
row says so, with every leg and its source in the tooltip.

**Alerts** (the bell, or right-click a row → "Set an alert…"). Pick a line, a condition and a value; every
rule on every line is listed with an on / off switch. Price and percent rules fire once per crossing and
re-arm when the condition clears; the 30-day ones fire at most every 12 hours. Rows with rules show a small
bell. When one fires the row gets an orange wash until you click it, the title-bar bell lights up, the footer
shows the message, and a Windows notification goes out (switchable). Each rule can also decide whether a
minimized widget comes back when it fires ("show widget: default / yes / no" on its row). Everything that
fired is in `alerts.log` (Open log in the dialog).

**News** (the newspaper button, or the tray menu). One merged list across the feeds you have enabled,
newest first, with date separators. Bright = newer than your last look, muted = seen; the button glows
while there is something new. Click a headline to open it; right-click for Copy link, Visit the site, Hide
that source. The header has:

- a **text filter**, comma separated: `bitcoin, gold, bank` shows headlines containing any of those (a
  term may contain spaces) and still fills the list up to your headline count with matches only;
- a **source box** that narrows to one sector or one feed;
- a **pin**: pinned, the popup stays open when you click elsewhere, so you can read one item and come back
  for the next. Unpinned, it closes on focus loss. Escape clears the filter, then closes.

Feeds are in **Settings → News**, grouped by sector in collapsible sections with a country tag (and a
language tag when not English). Tick to enable, click a name to rename, ✕ to remove, add any RSS / Atom
URL with a sector, or "Restore default feeds" to get back anything you removed. Six are on by default:
CoinDesk, Cointelegraph, The Block, Decrypt, Bitcoin Magazine, ZeroHedge. "Open headlines in" switches
between your browser and the built-in reader (a sun / moon button in the reader toggles dark pages; off
by default because many sites half-implement it).

**Come back on its own.** Settings → Behaviour: "Come back after N minutes" (0 = never) and "Come back
when an alert fires". Minimize the widget while you work and it reappears on its own, in front but
**without taking focus**: whatever you were typing into keeps the keyboard.

**Hotkey and tray.** `Ctrl+Alt+B` (changeable) brings the widget to the front or minimizes it if it is
already in front. Tray icon: left click shows / hides, right click gives Show, Refresh, Lines, Alerts, News,
Settings, Exit.

**Settings** covers opacity (panel and text separately), font size (everything scales, dialogs included),
width, list height, tooltips, poll intervals, hotkey, always-on-top, taskbar, start with Windows, what the
✕ does, the conversion-rate provider and its fallback for derived prices, alerts, and news.

**About** (the ? button, or the tray menu) shows the version, links here, and checks GitHub Releases for a
newer version on demand. It also checks once a day on its own (switchable there) and lights the ? button
when one is out. Nothing else ever phones home.

### Where your data lives

`%LocalAppData%\BtcWidget\`

| File | Contents |
|---|---|
| `settings.json` | Everything in Settings, window position, your lines (each with its alerts), the last update check |
| `feeds.json` | Your news feeds: `id`, `name`, `sector`, `country`, optional `lang`, `url`, `site`, `enabled`, optional `color` (`#RRGGBB` badge). Seeded from the shipped list on first run, then yours to edit by hand or in Settings |
| `providers.json` | Everything about the price providers that can change: names, icons, the web page each click opens, API endpoints, the footer links, the two starter lines. Your edits are merged over the shipped file on every start, so new versions bring fixes and your changes survive |
| `currencies.json` | Quote currencies offered in the dropdowns and the symbol shown for each fiat currency. Same merge rule |
| `cache\` | Downloaded logos, one file each, fetched once and reused. Safe to delete |
| `history.db` | SQLite, every reading, used for the sparklines, 7d / 30d and 30-day high / low |
| `alerts.log` | One line per fired alert |
| `webview\` | Profile of the built-in reader. Only exists once you have used it; safe to delete |

Delete the folder and the widget starts fresh.

## How to build it

Requirements: the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) on Windows. Visual
Studio 2026 (or 2022 17.14+) with the .NET desktop workload if you want the designer; not required.

```bash
git clone https://github.com/RenegadeMinds/BtcWidget.git
```

```bash
dotnet build BtcWidget -c Release
```

The exe lands in `BtcWidget\bin\Release\net10.0-windows\`. Or open `BtcWidget.sln` and press F5.

To produce the two release artifacts (zip and single-file exe) into `release\`:

```bash
.\build-release.ps1
```

That runs `dotnet publish` twice: framework-dependent for the zip, self-contained single-file for the exe.
The version comes from `<Version>` in `BtcWidget\BtcWidget.csproj`.

Dependencies (all NuGet): CommunityToolkit.Mvvm, Microsoft.Data.Sqlite, Microsoft.Web.WebView2.

## Price providers

| Id | Covers | Search | History | Notes |
|---|---|---|---|---|
| `kraken` | Crypto, ~1,450 pairs | | 30d hourly | Official API. Default for coins / pairs it lists; default conversion-rate provider. |
| `coinbase` | Crypto | | 75d 6-hourly | Official API. Second choice for coins. |
| `binance` | Crypto ("USD" = USDT market) | | 30d hourly | Official API. binance.com geo-blocks some regions. |
| `coingecko` | Crypto in ~60 quote currencies | yes | | Aggregator. Fallback when no exchange has the pair; default fallback conversion-rate provider. Supplies coin logos and the popular list. |
| `yahoo` | Stocks worldwide, ETFs, indices, FX, futures, crypto pairs | yes | 3mo daily | Unofficial chart endpoint. Default for everything non-crypto, and the FX source for conversions. |
| `nasdaq` | US stocks, ETFs | | | Unofficial. Alternative if Yahoo misbehaves. |

All are public endpoints with no key. They are polled hourly by default (every 5 minutes for lines with an
alert), which is well inside every provider's unauthenticated limits. Stooq, Alpha Vantage, Finnhub, Twelve
Data, Polygon, FRED and CoinCap were considered and skipped: they need a key or sit behind a browser check.

## Provider links and endpoints

Nothing about an external site is hardcoded. `providers.json` holds, per provider, the display name, icon,
the page a click opens (`"page": "https://pro.kraken.com/app/trade/{base}-{quote}"`) and each API endpoint,
all as templates with placeholders (`{symbol}`, `{base}`, `{BASE}`, `{quote}`, `{QUOTE}`, `{coinId}`,
`{product}`, `{pair}`, …; the file's `_comment` lists them). The footer links and the two starter lines are
there too. When a site changes its URL scheme, fix the template in your `%LocalAppData%\BtcWidget\providers.json`
and it takes effect at the next start; fix it in `Data/providers.json` for everyone. Logos are downloaded
once into `%LocalAppData%\BtcWidget\cache` and served from there.

## News feeds

The shipped list is [`BtcWidget/Data/feeds.json`](BtcWidget/Data/feeds.json): 134 feeds, every URL
verified on 2026-11-01 (253 candidates probed, the rest were dead, blocked feed readers or had no RSS).
Add a feed there and it reaches new installs and "Restore default feeds". Outlets that are not in the
list because they block readers or have no working feed: Reuters, Yahoo Finance, Barron's, Korea JoongAng
Daily, Korea Times, Kyodo, NHK World (English), Asahi AJW, Caixin, Focus Taiwan, Financial Post, Kitco,
Glassnode, Coinbase, Binance.

Feeds are fetched in parallel every 15 minutes (configurable), each on its own, so one dead site never
blocks the rest; the popup footer names any that failed. RSS 2.0, RSS 1.0 and Atom are accepted. Headlines
older than 48 hours are dropped.

## Layout of the code

| Folder | Purpose |
|---|---|
| `Models/` | `AppSettings` + `LineConfig` (persisted), `NewsFeed` (one feeds.json entry), `PricePoint` / `PriceQuote` / `SearchResult` |
| `Data/` | `feeds.json`, `providers.json`, `currencies.json`: every external URL, name and list the app uses. Data, not code. Copied beside the exe and embedded in the assembly; the user's copies in `%LocalAppData%` override them |
| `Services/` | `IPriceSource` + `IInstrumentSearch` and the six `*Source` providers; `PriceService` (per-line poll loop, derived prices, search fan-out); `HistoryStore` (SQLite); `PriceAnalysis`; `AlertEngine`; `AlertLog`; `NewsService` (RSS / Atom fetch, parse, merge); `FeedStore` (feeds.json); `ProviderData` (providers.json / currencies.json loader, URL templates); `ImageCache` (logo files); `TrayIcon` (Shell_NotifyIcon, balloon notifications); `HotkeyManager`; `SettingsStore`; `MemoryDiagnostics` |
| `ViewModels/` | `MainViewModel` (widget + settings + alert dispatch + news state), `PriceRowViewModel`, `LinesViewModel`, `AlertsViewModel`, `NewsViewModel`, `NewsFeedOption` / `NewsFeedGroup`, `LinkViewModel` |
| `Views/` | `MainWindow` (the widget), `LinesWindow`, `AlertsWindow`, `NewsWindow` (popup), `ReaderWindow` (WebView2), `SettingsWindow` |
| `Controls/` | `Sparkline` |
| `Converters/` | Price / percent formatting and colour converters |
| `Assets/` | Icons. The provider badges are generated PNGs; drop in real logos with the same names if you like |

Colours and the shared control styles live in `App.xaml`. Design decisions and gotchas that are not obvious
from the code (why tooltips are a class handler, why restoring without focus needs `WS_EX_NOACTIVATE`, the
60 MB first-TextBox cost) are in comments at the relevant spots.

### Adding a price source

1. Add a class implementing `IPriceSource` in `Services/` (`NasdaqSource` is the minimal shape,
   `CoinbaseSource` one with history and a cheap "do you have this pair" check). Declare which
   `InstrumentKind`s it covers; implement `IInstrumentSearch` too if it can search. Throw
   `PairNotSupportedException` when the provider has no market for `line.EffectiveQuote`; `PriceService`
   then derives the price.
2. Add it to the `all` array in `PriceService`'s constructor and a badge PNG to `Assets/`.
3. It appears in the provider dropdown for matching kinds. To make it a default, extend
   `PriceService.PickProviderAsync`.

## Notes

- **Memory.** Idle, about 150 MB private bytes (the .NET runtime plus WPF; it does not grow), managed heap
  3 to 6 MB. The first time any text box gets focus Windows loads a text-input component that costs ~60 MB
  for the life of the process; accepted as a one-time OS cost. Set `BTCWIDGET_DIAG=1` before launching to
  get memory snapshots in `%LocalAppData%\BtcWidget\diag.log`.
- **Notifications** are the classic notification-area balloon, which Windows 10 / 11 render as a normal
  toast and keep in the notification centre. They respect Focus Assist / Do Not Disturb, so if nothing
  appears check that first; `alerts.log` records whether the call succeeded.
- **Reader memory.** The built-in reader starts Edge WebView2 processes (100 MB+) while open and releases
  them when the reader window closes, which is why the default is your system browser.
- **Not financial advice**, obviously. Prices come from public endpoints with no guarantee of accuracy or
  availability.

## License

[WTFPL](http://www.wtfpl.net/) — Do What The F\*ck You Want To Public License. See [LICENSE](LICENSE).
Copyright © 2025–2026 Ryan Smyth / Renegade Minds.
