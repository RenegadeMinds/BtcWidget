# Changelog

All notable changes to BtcWidget are listed here, newest first. Version numbers follow
[Semantic Versioning](https://semver.org/).

Download releases from the [Releases page](https://github.com/RenegadeMinds/BtcWidget/releases). For how to use
any of these features, see the [user guide](HELP.md).

## [1.6.0] - 2026-11-23

The widget can now be a wide horizontal bar as well as a tall column. This release also includes everything from
1.5.0, which was not published separately.

### Added

- **Tiled layout.** Lines now tile side by side, left to right and then down, whenever two or more fit at full
  readability. Below that width, they stack in a single column exactly as before.
- **Measured tile width.** The width of a tile is measured from your actual lines rather than being a fixed number.
  It follows the font size, the length of your prices and whether the detail row is on.
- **Aligned columns.** Prices, changes, sparklines and percentages stay lined up across all tiles.
- **Left or right alignment.** Tiles keep their width and stay together as a group, with all spare room on one side.
  A tiny arrow at the far end of the empty space moves the group to that side. An uneven last row follows the same
  side. The choice is remembered.
- **One-row bar.** When all lines fit on a single row, the status text and link icons move into the title bar for a
  slim bar.
- **Top-edge resize.** Drag the top edge to set the list height, as with the bottom edge. Double-click either edge to
  return to automatic height.
- **Resizable news popup.** Drag any edge or corner. The size is remembered, and the popup is never larger than the
  monitor it opens on.

### Changed

- **Live resizing.** Lines reflow and the widget's height adjusts while you drag its width, instead of when you let go.
- **Stays docked.** When the widget is nearer the bottom of the screen, changing its width keeps its bottom edge in
  place.
- **Maximum width follows your monitor.** The fixed 1600-pixel limit is gone. The widget can be as wide as the monitor
  it is on, and the limit updates when you move it to another screen. Your chosen width is kept, so it regrows when you
  move back to a larger monitor.
- The width slider in Settings follows the same per-monitor limit.
- **Upgrades deliver fixes and keep your edits.** The widget now tells values you changed in `providers.json` and
  `currencies.json` from ones you never touched. Untouched values, such as a corrected URL, update to the new version.
  Your edits are kept. Your previous file is saved in the `shipped` folder.
- **The daily update check keeps running.** It used to run only at startup, so a widget left open never checked again.
  A failed check is now retried an hour later and no longer reports "up to date".

### Fixed

- The news popup opened on the primary monitor when the widget was on another screen. It now opens beside the widget.
- The built-in reader opened on the primary monitor. It now opens centered on the widget's monitor and is never larger
  than that screen. Fitting onto a small monitor no longer overwrites your saved reader size.
- Keeping the widget on screen now uses the monitor it is on, rather than only the primary monitor.
- **Data loss on a bad edit.** A mistake in a hand-edited `feeds.json` or `settings.json` used to reset the file and lose
  its contents. The widget now keeps a copy as `<name>.broken-<date>.json`, shows a notification and carries on with
  defaults. A mistake in `providers.json` or `currencies.json` now also produces a notification.
- **Crash on an invalid badge colour.** A bad `color` in `feeds.json` crashed the widget when that feed's headlines
  appeared. It is now ignored and a colour is picked automatically.
- **"Open in reader" opened the browser.** The headline right-click menu now opens the built-in reader as it says.

## [1.5.0] - 2026-11-20

Not published as a separate release. These changes first ship in 1.6.0.

### Added

- **Spot precious metals.** Gold, silver, platinum and palladium are now available as lines.
- **Kitco** as a price provider, with mid price and change since the previous close, in USD, CAD, EUR and many other
  currencies. It is the default source for new metal lines.
- **gold-api.com** as a second spot price provider.
- **Metals search.** Type `gold`, `silver`, `xau`, or a metal and currency such as `gold cad` in the Lines dialog. Spot
  metals rank above stocks and tokens that borrow the same word.
- **Metals chip** in the Lines dialog, and the four metals in the popular list.
- **Instant history for metals.** New metal lines backfill daily closes from the matching Yahoo futures contract (`GC=F`,
  `SI=F`, `PL=F`, `PA=F`), so sparklines and 7-day / 30-day figures work straight away.
- **Any currency for metals.** Prices use the source's own currency where it has one, and USD with an FX conversion
  where it doesn't.
- Kitco and TradingView footer links for metal lines.

### Changed

- The metals list, Kitco codes, futures symbols and provider URLs are all data in `providers.json`, not code.
- Pair search accepts longer base words, so `palladium cad` works.

## [1.4.1] - 2026-11-01

### Fixed

- **Kraken links opened a 404 page.** Rows and footer icons now open
  `https://pro.kraken.com/app/trade/<base>-<quote>`, for example `btc-cad`.

### Changed

- **Provider data moved out of the code.** Every provider name, icon, page link, API endpoint, footer link and the starter
  lines are now in `Data/providers.json`. Quote currencies and fiat symbols are in `Data/currencies.json`. Both are copied
  to `%LocalAppData%\BtcWidget` on first run, and your copy is applied over the shipped file on every start.
- **Logos are cached.** Coin logos are downloaded once to `%LocalAppData%\BtcWidget\cache` and reused, instead of being
  fetched on every launch.

## [1.4.0] - 2026-11-01

First public release.

### Prices

- Lines from six keyless providers: Kraken, Coinbase, Binance, CoinGecko, Yahoo Finance and Nasdaq.
- Crypto, stocks on exchanges worldwide, ETFs, indices, FX and futures.
- Any quote currency. Prices are derived through USD or FX when no market has the pair, such as Tesla in BTC or BTC/DOGE,
  with a selectable conversion-rate provider and fallback.
- A sparkline, 7-day and 30-day change, and 30-day range under each line, with history kept locally in SQLite.

### Alerts

- Price above or below, up or down more than a percentage over 24 hours, and new 30-day high or low.
- Lines with an alert poll every 5 minutes by default. Other lines poll hourly.
- Windows notification, row highlight, title-bar bell and an `alerts.log` history.

### News

- A headline popup with 134 RSS and Atom feeds tagged by sector and country, stored in an editable `feeds.json`, with six
  on by default.
- Comma-separated text filter, sector and source filter, pin, source badges and date separators.
- Open articles in the default browser or a built-in WebView2 reader.

### Behaviour

- A minimized widget can come back after a set time or when an alert fires, without taking focus, with a per-alert
  override.
- Start with Windows.
- The title-bar ✕ can exit or minimize.
- Single instance: a second launch brings the running widget forward.
- About dialog with version, links, and manual and daily update checks against GitHub Releases.
- Global hotkey, tray icon, and settings for opacity, font size, width, always-on-top and tooltips.

### Release

- `build-release.ps1` produces a framework-dependent zip and a self-contained single-file exe.
- Licensed under the WTFPL.

[1.6.0]: https://github.com/RenegadeMinds/BtcWidget/releases/tag/v1.6.0
[1.5.0]: #150---2026-11-20
[1.4.1]: https://github.com/RenegadeMinds/BtcWidget/releases/tag/v1.4.1
[1.4.0]: https://github.com/RenegadeMinds/BtcWidget/releases/tag/v1.4.0
