# Changelog

## 1.6.0 — 2026-11-23

- Lines tile side by side when two or more fit at full readability. The tile width is measured from the
  lines themselves, so it follows font size, price lengths and the detail row. Columns stay aligned.
- Tiles stay together on the left or right; a tiny arrow at the end of the spare room switches sides.
- One-row bar: when everything fits on one row, the status and links move into the title bar.
- The widget reflows live while you drag its width, and keeps its bottom edge when docked low on the screen.
- New: drag the top edge to set the list height, like the bottom edge.
- Maximum width is now the width of the monitor the widget is on, instead of a fixed 1600px.
- Fixed: the news popup and the built-in reader opened on the primary monitor when the widget was on another
  screen. Both now open on the widget's monitor, and the reader is never larger than that screen.

## 1.5.0 — 2026-11-20

- Spot precious metals: gold, silver, platinum and palladium from Kitco and gold-api.com, in USD, CAD or any
  other currency (native where the source has it, else USD × FX). New Metals chip in the Lines dialog;
  `gold`, `xau`, `gold cad` all search. Kitco is the default source for a new metal line.
- Metal lines backfill history from Yahoo futures (`GC=F`, `SI=F`, `PL=F`, `PA=F`) so sparklines and
  7d / 30d work immediately.
- The metals list, codes and providers live in `providers.json` → `metals`.

## 1.4.1 — 2026-11-01

- Fixed: Kraken links opened a 404 page. They now go to `pro.kraken.com/app/trade/<base>-<quote>`.
- Every provider name, icon, page link, API endpoint, footer link and the starter lines moved out of the
  code into `Data/providers.json`; quote currencies and fiat symbols into `Data/currencies.json`. Both are
  copied to `%LocalAppData%\BtcWidget` on first run and your edits are merged over the shipped files on
  every start.
- Logos are downloaded once into `%LocalAppData%\BtcWidget\cache` instead of on every launch.

## 1.4.0 — 2026-11-01

First public release.

**Prices**
- Lines from six keyless providers: Kraken, Coinbase, Binance, CoinGecko, Yahoo Finance, Nasdaq.
- Any quote currency; prices derived through USD or FX when no market has the pair (Tesla in BTC,
  BTC/DOGE), with the conversion-rate provider and fallback selectable in Settings.
- Sparkline, 7d / 30d change and 30-day range under each line; history kept in SQLite.

**Alerts**
- Price above / below, up / down more than N% (24h), new 30-day high / low.
- Lines with an alert poll every 5 minutes (configurable); the rest hourly.
- Windows notification, row highlight, title-bar bell, `alerts.log`.

**News**
- Headline popup: 134 RSS / Atom feeds tagged by sector and country, in `feeds.json` (yours to edit),
  six on by default. Comma-separated text filter, sector filter, pin, source badges, date separators.
- Open in the default browser or a built-in WebView2 reader.

**Behaviour**
- Minimized widget comes back after N minutes or when an alert fires, without taking focus; per-alert
  override.
- Start with Windows. Title-bar ✕ can exit or minimize. Single instance: a second launch brings the
  running widget forward.
- About dialog with version, links, manual and daily update check against GitHub Releases.
- Global hotkey, tray icon, opacity / font / width / always-on-top / tooltip settings.

**Release**
- `build-release.ps1` produces a framework-dependent zip and a self-contained single-file exe.
- License: WTFPL.
