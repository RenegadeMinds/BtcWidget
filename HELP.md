# BtcWidget User Guide

This guide covers everything the widget does and every setting it has. For downloads, building from source
and project information, see the [README](README.md). For version history, see the
[changelog](CHANGELOG.md).

## Contents

1. [Getting started](#getting-started)
2. [Reading the widget](#reading-the-widget)
3. [Moving and resizing](#moving-and-resizing)
4. [Column, tiles and the one-row bar](#column-tiles-and-the-one-row-bar)
5. [Adding and managing lines](#adding-and-managing-lines)
6. [Currencies and derived prices](#currencies-and-derived-prices)
7. [Stocks and exchanges](#stocks-and-exchanges)
8. [Gold, silver and other metals](#gold-silver-and-other-metals)
9. [Alerts](#alerts)
10. [News](#news)
11. [The built-in reader](#the-built-in-reader)
12. [Minimizing and coming back](#minimizing-and-coming-back)
13. [Hotkey, tray icon and startup](#hotkey-tray-icon-and-startup)
14. [Settings reference](#settings-reference)
15. [About and updates](#about-and-updates)
16. [Customizing with the data files](#customizing-with-the-data-files)
17. [Troubleshooting](#troubleshooting)
18. [Frequently asked questions](#frequently-asked-questions)

## Getting started

Run `BtcWidget.exe`. There is nothing to install and no account to create.

The first time it runs, the widget appears at the bottom right of your main screen with two lines: Bitcoin
from Kraken and Bitcoin from CoinGecko. Prices load within a few seconds.

A good first five minutes:

1. **Place it.** Drag the panel to where you want it.
2. **Add what you watch.** Click **+**, type a name or ticker, and click **Add**.
3. **Remove what you don't.** In the same dialog, click the remove button on a line.
4. **Set an alert.** Click the bell, pick a line and a condition, and enter a value.
5. **Pick your news.** Open **Settings**, scroll to **News**, and tick the feeds you want.
6. **Tune the look.** In **Settings → Appearance**, adjust the opacity, font size and width.

Everything is saved automatically as you go.

## Reading the widget

### The title bar

The title shows **Markets**, or the pair itself when you only watch one instrument. The buttons are:

| Button | What it does |
|---|---|
| Newspaper | Opens or closes the news popup. Turns orange when there are headlines you haven't seen |
| Bell | Opens the Alerts dialog. Turns orange while a fired alert is waiting for you |
| **+** | Opens the Lines dialog, to add, remove and arrange lines |
| Refresh | Fetches every line now |
| Gear | Opens Settings |
| **?** | Opens About. Turns orange when a newer version is available |
| **–** | Minimizes the widget |
| **✕** | Exits, or minimizes if you chose that in Settings |

### A line

Each line has two rows:

```text
(logo) Bitcoin  BTC/CAD · Kraken  (bell)            C$88,047   +1.63%
       ~~sparkline~~   7d -5.3%   30d -21.7%   23.1% below 30d high
```

**First row:**

- The coin logo, or the provider's badge.
- The name, then small labels: the pair if it isn't the default, the exchange for stocks, and the provider.
- **derived** in the label means the price was calculated rather than read from a market. See
  [Currencies and derived prices](#currencies-and-derived-prices).
- A small bell if the line has alerts.
- The price, then the change. For crypto, the change covers 24 hours. For stocks, ETFs, indices, FX and
  metals, it is the change since the previous close.

**Second row** (can be switched off in Settings):

- A 30-day **sparkline**, green if the 30-day change is up and red if it is down.
- The **7-day and 30-day change**. Stocks and other daily instruments show these as **1w** and **1m**.
- **Where the price sits in its range**, relative to whichever is closer: "4.2% below 30d high", "1.3% above
  30d low" or "at 30d high". Until a line has about 28 days of history, the label shows how many days it has,
  such as "12d".

**Interacting with a line:**

- **Hover** to see details: the source, all the changes, the exact high and low, the time of the last update
  and, for derived prices, every conversion step.
- **Left-click** to open the instrument on its provider's website. This also clears a fired alert on that line.
- **Right-click** for **Set an alert…** and **Open on (provider)**.

### The status line

The bottom line shows when prices last updated and when the next update is due, for example:

```text
Updated 17:16 · next 17:21 · 1 alert line every 5 min
```

It also shows the latest alert message while an alert is pending, or which provider failed if one did. The
small icons on the right are quick links for the **first line** in your list, such as its CoinGecko, Kraken
and TradingView pages.

## Moving and resizing

| Action | Result |
|---|---|
| Drag anywhere on the panel | Moves the widget |
| Drag the **left or right edge** | Changes the width. Lines reflow and the height adjusts live |
| Drag the **top or bottom edge** | Sets the maximum list height. The list scrolls beyond it |
| **Double-click the top or bottom edge** | Returns to automatic height, where the list grows with its content |

Good to know:

- The position and size are remembered.
- The maximum width is the width of the **monitor the widget is on**, whether that's 1600 pixels or a 4K
  screen. When you move it to another monitor, the limit updates as you let go.
- If you move a wide widget to a smaller monitor, it shrinks to fit. Move it back and it returns to the width
  you chose.
- If the widget is closer to the bottom of the screen, changing its width keeps the **bottom** edge in place
  as the height changes, so it stays docked.
- If the widget grows past the edge of the screen, it shifts itself back into view.

## Column, tiles and the one-row bar

The widget picks its layout from its width. You don't have to configure anything.

**Narrow: one column.** While only one line fits at full readability, lines stack and fill the width. If you
go narrower still, the text on each line trims with an ellipsis.

**Wider: tiles.** As soon as **two or more** lines fit side by side, they tile from left to right, then down.

- The tile width is **measured from your lines**. It is the width needed to show the whole detail row of your
  widest line, so it changes with the font size, the length of your prices and whether the detail row is on.
- Prices, changes, sparklines and percentages stay **aligned across all tiles**.
- A long instrument name doesn't force wider tiles. It uses the available room and trims.

**Spare room and alignment.** Tiles never stretch to fill the width, so you never have to look across the
screen from a name to its price. The group stays together on one side, with all the spare room on the other.

A **tiny arrow** sits at the far end of the empty space. Click it to move the tiles to that side:

- Tiles on the **left**: the arrow is at the right and points right.
- Tiles on the **right**: the arrow is at the left and points left.

The arrow only appears when there is room for it. An uneven last row, such as the fifth of five lines in two
columns, keeps the same tile width and sits on the same side as the rest. Your choice is remembered.

**Widest: the one-row bar.** When every line fits on a single row, the status text and link icons move up into
the title bar. This saves a full row of height and makes a slim bar you can run along the edge of the screen.

## Adding and managing lines

Click **+**, or choose **Lines…** from the tray menu.

### The right side: adding

Type in the search box. You can search by:

| You type | You get |
|---|---|
| A name | `tesla`, `shopify`, `bitcoin`, `samsung` |
| A ticker | `TSLA`, `SHOP.TO`, `XRP`, `005930.KS` |
| A pair | `BTC/CAD`, `BTC-CAD`, `btc cad`, `ETHBTC` |
| A metal | `gold`, `silver`, `xau`, `gold cad` |
| An index | `^GSPC`, `^VIX`, `^GSPTSE` |
| FX | `EURUSD=X` |
| Futures | `GC=F` |

With the search box empty, it lists the top coins, the spot metals, and common indices, stocks, ETFs, FX and
commodities.

Use the **chips** (All, Crypto, Stocks, ETFs, Indices, FX, Commodities, Metals) to narrow the results. Use the
**quote dropdown** to choose the currency for the line you are about to add, then click **Add**. The widget
picks the best provider automatically:

- **Crypto:** an exchange that has the exact pair (Kraken, then Coinbase), otherwise CoinGecko.
- **Metals:** Kitco.
- **Everything else:** Yahoo Finance.

### The left side: your lines

Each line has:

- **Move up / move down** to set the order. The first line also decides the footer links.
- A **provider** dropdown, listing the providers that can quote this kind of instrument.
- A **quote** dropdown. **Default** means USD for crypto and metals, and the listing currency for stocks.
- A **bell** that opens an inline alert editor for this line.
- **Remove.**

Changes apply to the widget immediately.

## Currencies and derived prices

Any line can be shown in any currency. Crypto can even be quoted in crypto, and a stock can be priced in BTC.

The widget gets the price in the simplest way available:

1. **Native market.** If the provider has the pair, its price is used directly. Kraken has BTC/CAD and ETH/BTC,
   for example.
2. **Derived.** Otherwise the provider supplies the price in USD or the listing currency, and a conversion rate
   does the rest:
   - **Between ordinary currencies**, such as IBM in CAD, the rate comes from Yahoo FX.
   - **When a crypto asset is involved**, its USD price comes from your conversion-rate provider. Tesla in BTC
     is TSLA/USD ÷ BTC/USD, and BTC/DOGE is BTC/USD ÷ DOGE/USD.

Derived rows say **derived** in their label, and the tooltip lists every step and its source. History is
derived the same way, so sparklines and 7-day / 30-day figures still work.

The conversion-rate provider and its fallback are set in **Settings → Conversion rates**. The defaults are
Kraken, then CoinGecko.

## Stocks and exchanges

Search by company name or ticker. The Lines dialog shows the exchange for each result, and Toronto listings
are ranked first when a company trades in both places.

| Exchange | Yahoo suffix | Example |
|---|---|---|
| NYSE, NASDAQ | none | `IBM`, `AAPL`, `TSLA` |
| Toronto (TSX) | `.TO` | `SHOP.TO`, `RY.TO` |
| TSX Venture | `.V` | `LIXX.V` |
| Canadian Securities Exchange | `.CN` | `GTII.CN` |
| Korea | `.KS` | `005930.KS` (Samsung Electronics) |
| Tokyo | `.T` | `7203.T` (Toyota) |
| Frankfurt | `.F` | `307.F` |

Prices appear in the listing currency unless you choose another quote. Stocks use the day's change since the
previous close, and their long-range labels are **1w** and **1m**. Free stock data can be delayed by 15 minutes
or more, depending on the exchange.

## Gold, silver and other metals

Type `gold`, `silver`, `platinum` or `palladium`, or their codes `xau`, `xag`, `xpt` and `xpd`. Add a currency
to go straight to it, such as `gold cad`. You can also click the **Metals** chip.

Two spot sources are available, and you can switch between them with the line's provider dropdown:

| Source | Provides | Notes |
|---|---|---|
| **Kitco** (default) | Mid price and change since the previous close | USD, CAD, EUR and many others |
| **gold-api.com** | Spot price | Major currencies. The change is calculated from history |

If a source doesn't carry your currency, the widget fetches USD and converts it with an FX rate.

**History.** Neither source offers free history, so a new metal line fills in about three months of daily
closes from the matching futures contract on Yahoo: gold `GC=F`, silver `SI=F`, platinum `PL=F`, palladium
`PA=F`. That makes the sparkline and 7-day / 30-day figures work immediately. Futures usually trade slightly
away from spot, so those early figures are close rather than exact. The widget's own spot readings take over as
they build up.

Yahoo's futures, such as `GC=F`, can also be added as ordinary lines under **Commodities**.

## Alerts

Open the Alerts dialog with the **bell**, by right-clicking a line and choosing **Set an alert…**, from the tray
menu, or with **Settings → Manage alerts…**.

### Creating an alert

Choose a **line**, a **condition** and a **value**, then click **Add alert** or press Enter.

| Condition | Fires when | Value |
|---|---|---|
| **Price above** | The price reaches or passes the level | A price in the line's currency |
| **Price below** | The price falls to or through the level | A price in the line's currency |
| **Up more than** | The change is at least this much | A percentage |
| **Down more than** | The drop is at least this much | A percentage |
| **New 30-day high** | The price tops every reading in the last 30 days | None |
| **New 30-day low** | The price is under every reading in the last 30 days | None |

The change is over 24 hours for crypto and since the previous close for stocks, ETFs, indices, FX and metals.

Values can be typed naturally: `110000`, `110,000`, `$110k`, `C$1.2m`, `3.5` or `3.5%`.

### How alerts behave

- **Checked on every poll.** A line with an enabled alert polls every 5 minutes by default, so a crossing is
  caught within minutes. You can change this in **Settings → Behaviour**.
- **Once per crossing.** A price or percentage alert fires once, then re-arms when the condition is no longer
  true. It won't nag you on every poll while the price stays above your level.
- **30-day alerts** need at least 7 days of history and fire at most once every 12 hours.

### When an alert fires

- The line gets an **orange bar and tint**.
- The title-bar **bell turns orange**.
- The **status line** shows the message.
- A **Windows notification** appears, if enabled.
- A minimized widget **comes back**, if enabled, without taking focus.
- The alert is added to **`alerts.log`**.

Click the line to clear the highlight. Clicking also opens its provider page.

### Managing alerts

The **Your alerts** list shows every rule on every line, with:

- A **checkbox** to switch it on or off without deleting it.
- Its **state**: watching, off, the time it last fired, or waiting to re-arm.
- A **show widget** link that controls whether this alert brings a minimized widget back. Click to cycle through
  **default** (follow the setting), **yes** and **no**.
- A **remove** button.

**Open log** shows the history of fired alerts, and whether a notification went out for each one.

## News

Click the **newspaper** in the title bar, or choose **News…** from the tray menu.

### The headline list

Headlines from all your enabled feeds appear in one list, newest first, with a separator for each day. Each row
has a coloured two-letter **source badge**, the **headline** and the **time**.

- **Bright** headlines are newer than the last time you had the popup open. **Muted** ones you've already seen.
- The newspaper button turns orange when something new has arrived.
- **Hover** for the full headline, source, country and exact time.
- **Click** to open the article.
- **Right-click** for:
  - **Open**
  - **Open in reader** or **Open in browser**, whichever isn't your default
  - **Copy link**
  - **Visit (source)**, the outlet's home page
  - **Hide (source)**, which switches that feed off

### The header

From left to right: the **text filter**, the **source box**, **pin**, **refresh**, **settings** and **close**. Drag
the "News" title to move the popup, and drag **any edge or corner** to resize it. The size is remembered, and the
popup is never larger than the monitor it opens on.

**Text filter.** Type words separated by **commas**. A headline is shown if its title or source name contains
**any** of them, ignoring case:

```text
bitcoin, gold, bank
interest rate, north korea
```

A term can contain spaces. Semicolons work as separators too. The list still fills up to your headline count,
using matches only. The footer shows how many matched. Your last filter is remembered.

**Source box.** Narrow the list to one **sector**, such as Crypto or Macro, or to a single **feed**.

**Pin.** Normally the popup closes as soon as you click somewhere else. With the pin on (orange), it stays open,
so you can read an article and come back for the next one. The pin state is remembered.

**Keyboard.** Escape clears the filter if you're typing in it. Otherwise it closes the popup, even when pinned.

### Choosing feeds

Go to **Settings → News**. Feeds are grouped by sector in collapsible sections, and each section shows how many
are on. Sections with something switched on start open.

| Sector | Feeds |
|---|---|
| Crypto | 36 |
| Stocks and markets | 78 |
| ETFs | 2 |
| Macro and central banks | 11 |
| Commodities | 4 |
| Forex | 3 |

On by default: CoinDesk, Cointelegraph, The Block, Decrypt, Bitcoin Magazine and ZeroHedge.

Each row has:

- A **checkbox** to switch the feed on or off.
- Its **name**. Click to rename it.
- A **tag** with its country, plus its language if it isn't English: `US`, `KR · ko`, `JP · ja`.
- **✕** to remove it.

**Korea and Japan.** Useful English feeds for companies such as Samsung, SK Hynix, Toyota or Honda include KED
Global (including its Korean Stock Market, Chipmakers and Automobiles feeds), Yonhap Economy & Finance, The Korea
Herald, Nikkei Asia, The Japan Times and The Mainichi. Korean- and Japanese-language feeds are tagged `ko` and
`ja`.

**Add a feed.** Paste any RSS or Atom URL, pick a sector, and click **Add**. It takes its real name from the feed
after the first successful fetch.

**Restore default feeds** adds back any shipped feed you've removed. It never changes feeds you've edited or
added.

### Headline settings

- **Headlines shown:** how many appear in the list (default 10).
- **Refresh every:** how often feeds are fetched (default 15 minutes).
- **Open headlines in:** your default browser or the built-in reader.

Headlines older than 48 hours are dropped. If a feed fails, the others carry on, and the footer names the one
that failed.

## The built-in reader

If you set **Open headlines in** to **Built-in reader window**, articles open in a separate window on the same
monitor as the widget. The same window is reused for each article.

The toolbar has **back**, **forward**, **reload**, **copy link**, a **light / dark pages** toggle and **open in
your browser**.

**Light / dark pages.** Pages show as the site designed them by default. The sun / moon button asks sites for
their dark theme. Many sites only half-implement one, which can leave dark text on a dark background, so it is off
by default. Your choice is remembered.

The reader runs on Microsoft Edge WebView2 with its own private profile, separate from your Edge browser. It
uses extra memory while open and releases it when you close the window, so the default is your normal browser.

## Minimizing and coming back

You can minimize the widget while you work and have it return on its own, so you can still see the price.

In **Settings → Behaviour**:

- **Come back after N minutes.** The widget reappears this long after you minimize it. **0** means never.
- **Come back when an alert fires.** This is the default for all alerts. Each alert can override it with its
  **show widget** link in the Alerts dialog.

For example, you can leave the setting off and turn **show widget: yes** on for the one alert you really care
about.

**It never takes focus.** The widget comes back in front of your other windows, but whatever you're typing into
keeps the keyboard. You can carry on typing without interruption.

## Hotkey, tray icon and startup

**Global hotkey.** The default is `Ctrl+Alt+B`, and it works from any program:

- If the widget is minimized or hidden, it comes back.
- If it's visible but behind other windows, it comes to the front.
- If it's already in front, it minimizes.

To change it, type a new combination in **Settings → Show / hide hotkey**, then click out of the box. Use `Ctrl`,
`Alt`, `Shift` or `Win` plus a key, such as `Win+Shift+F9`. For number keys, use `D1`, `D2` and so on. If another
program already owns the combination, the status line tells you.

**Tray icon.** Left-click does the same as the hotkey. Right-click for **Show / hide, Refresh now, Lines…, Alerts…,
News…, Settings…, About…** and **Exit**.

**Start with Windows.** Tick this in **Settings → Behaviour**. It adds a startup entry for your user account only,
and removing the tick removes the entry. It's off by default. If you move the exe, run it once and the entry
updates itself.

**One copy only.** If the widget is already running, launching it again brings the running copy forward.

## Settings reference

All settings apply immediately and save automatically.

### Appearance

| Setting | What it does | Default |
|---|---|---|
| Widget opacity | How solid the panel background is. Raise it if a bright window behind makes the text hard to read | 85% |
| Text opacity | How solid the text and icons are | 100% |
| Font size | Scales the whole widget and its dialogs | 13 px |
| Width | Widget width, up to the width of the current monitor | 300 px |
| List height | Maximum list height before it scrolls. Far left is "auto" | auto |
| Sparkline, 7d / 30d and range under each line | Shows or hides each line's second row | On |
| Tooltips when hovering the widget | Switches off all widget and news popup tooltips | On |

### Behaviour

| Setting | What it does | Default |
|---|---|---|
| Poll every | How often lines without alerts update | 60 minutes |
| Lines with alerts every | How often lines with an enabled alert update | 5 minutes |
| Come back after | Restores a minimized widget after this long. 0 = never | 0 |
| Come back when an alert fires | Restores a minimized widget on an alert. Each alert can override it | On |
| Start with Windows | Runs the widget when you sign in | Off |
| Title-bar ✕ | Exits or minimizes the widget. Exit is always in the tray menu | Exits |
| Show / hide hotkey | The global hotkey | `Ctrl+Alt+B` |
| Always on top | Keeps the widget above other windows | Off |
| Show in taskbar | Shows a taskbar button | On |

### Conversion rates

| Setting | What it does | Default |
|---|---|---|
| Crypto rates from | The provider asked first for a crypto asset's USD price | Kraken |
| If that fails | The fallback provider | CoinGecko |

Rates between ordinary currencies always come from Yahoo Finance.

### Alerts

| Setting | What it does | Default |
|---|---|---|
| Manage alerts… | Opens the Alerts dialog | |
| Windows notification when an alert fires | The row is highlighted either way | On |

### News

| Setting | What it does | Default |
|---|---|---|
| Headlines shown | Number of headlines in the popup | 10 |
| Refresh every | How often feeds are fetched | 15 minutes |
| Open headlines in | Default browser or built-in reader | Default browser |
| Feed list | Switch on, rename or remove feeds | Six on |
| Add a feed | Add any RSS or Atom URL under a sector | |
| Restore default feeds | Adds back shipped feeds you removed | |

### Only in `settings.json`

Close the widget before editing this file, because it saves its settings when it exits.

| Key | What it does | Default |
|---|---|---|
| `NewsMaxAgeHours` | Drops headlines older than this | 48 |

## About and updates

The **?** button, or **About…** in the tray menu, shows the version, the author, a link to the project and the
license.

**Check for updates** asks GitHub for the latest release right away. If there is a newer version, a download link
appears. Updating is manual: download the new file and replace the old one. Your data carries over.

**Check once a day.** With this on, the widget checks about 30 seconds after it starts and then once a day for as long
as it keeps running. If a check fails, for example when you're offline, it tries again an hour later. The **?** button
turns orange when a newer version exists.

## Customizing with the data files

Your files are in `%LocalAppData%\BtcWidget\`. To open the folder, press `Win+R` and paste that path. **Close the
widget before editing them**, and restart it afterwards.

**If you make a mistake**, such as a missing comma or quote, nothing is lost:

- **`feeds.json` or `settings.json`:** the widget saves your file as `feeds.broken-<date>.json` or
  `settings.broken-<date>.json`, shows a notification, and carries on with defaults. To recover, fix the mistake in
  the broken copy, close the widget, and rename the copy back to the original name.
- **`providers.json` or `currencies.json`:** the widget shows a notification, leaves your file exactly as it is, and
  uses the built-in values until you fix it.

### `feeds.json`: your news feeds

```json
{
  "id": "kedglobal-chips",
  "name": "KED Global: Korean Chipmakers",
  "sector": "stocks",
  "country": "KR",
  "url": "https://www.kedglobal.com/rss/korean-chipmakers",
  "site": "https://www.kedglobal.com/korean-chipmakers",
  "enabled": true,
  "color": "#3366AA"
}
```

| Field | Required | Meaning |
|---|---|---|
| `id` | Recommended | A unique id. Generated if missing |
| `name` | Recommended | The display name. Uses the feed's host if missing |
| `url` | **Yes** | The RSS or Atom address |
| `sector` | No | `crypto`, `stocks`, `etfs`, `macro`, `commodities`, `forex`, or your own word, which gets its own section |
| `country` | No | ISO country code. `INT` means international |
| `lang` | No | Language code if not English, such as `ko`, `ja` or `de` |
| `site` | No | The home page for **Visit** |
| `enabled` | No | `true` or `false` |
| `color` | No | Badge colour as `#RRGGBB`. Picked automatically if missing or invalid |

The order of the file sets the order within each section.

### `providers.json`: price sources and links

This file holds each provider's display name, icon, the web page a click opens, and its API endpoints. It also
holds the footer links, the metals list and the two starter lines. If a website changes its URLs, you can fix it
here without waiting for a new version.

For example, the Kraken page link is:

```json
"page": "https://pro.kraken.com/app/trade/{base}-{quote}"
```

Placeholders available in page and link templates:

| Placeholder | Meaning | Example |
|---|---|---|
| `{symbol}` | The ticker as stored | `SHOP.TO` |
| `{base}` / `{BASE}` | The ticker in lower / upper case | `btc` / `BTC` |
| `{quote}` / `{QUOTE}` | The quote currency in lower / upper case | `cad` / `CAD` |
| `{coinId}` | The CoinGecko id, or the metal's slug | `bitcoin`, `gold` |
| `{product}` | BASE-QUOTE | `BTC-CAD` |
| `{nasdaqClass}` | `stocks` or `etf` | `stocks` |
| `{symbolNoCaret}` | The ticker without a leading `^` | `GSPC` |

API templates also use `{pair}`, `{query}`, `{id}`, `{vs}`, `{range}`, `{code}` and `{assetClass}`.

**What happens when you upgrade.** The widget remembers the version your copy was last in step with, in the
`shipped` folder, so it can tell what you changed from what you never touched:

- A value you **didn't change** is updated to the new version's value, so corrected URLs reach you automatically.
- A value you **did change** is kept, even if the new version changed it too.
- **New** providers, links and settings are added.
- Your own **additions** stay.
- A list, such as the footer `links`, counts as one value: if you edited it, your whole list is kept.

Your file on disk is updated to match. The version from just before the update is saved as
`shipped\providers.json.before-update`.

You can keep your copy minimal. A file containing only your change is valid:

```json
{ "providers": { "kraken": { "page": "https://example.com/{BASE}/{QUOTE}" } } }
```

To go back to the shipped values, close the widget and delete the file. It's recreated on the next start.

### `currencies.json`: quote currencies

`quotes` is the list offered in the dropdowns. `fiat` maps each ordinary currency to the prefix shown before its
prices. Any currency not in `fiat` is treated as a crypto asset and shown after the number, like `0.0371 BTC`.
Upgrades work the same way as for `providers.json`.

## Troubleshooting

**A line shows an error or an old price.** Hover over it for the reason, then click Refresh. If a provider is down
or has dropped that market, switch the line to another provider in the Lines dialog.

**A price shows "derived".** That's normal when the provider has no direct market for the pair. Hover to see how it
was calculated.

**The sparkline or 7d / 30d is empty.** The provider offers no history and the widget hasn't collected enough yet.
CoinGecko lines build up over time. For a faster start, use Kraken, Coinbase or Binance for crypto.

**I'm not getting Windows notifications.** Check **Focus Assist / Do Not Disturb**, and that notifications are enabled
for the app in Windows Settings. Then look in `alerts.log`: each entry says `notified` or `not notified`.

**An alert didn't fire.** Check that it's ticked and that its state isn't "waiting to re-arm". If it fired earlier, the
price has to move back across the level before it can fire again. 30-day alerts also need 7 days of history.

**The text is hard to read over a white window.** Raise **Widget opacity** in Settings. 95% or more works well on bright
backgrounds.

**Lines won't tile side by side.** The widget isn't wide enough for two complete tiles yet. Widen it, reduce the font
size, or switch off the detail row.

**My lines or feeds disappeared after I edited a file.** The file had a mistake, so the widget set it aside and started
with defaults. Your version is still there as `settings.broken-<date>.json` or `feeds.broken-<date>.json`. See
[Customizing with the data files](#customizing-with-the-data-files).

**The hotkey doesn't work.** Another program probably owns it. The status line says so. Choose a different one.

**A news feed fails.** Sites change or remove feeds. Fix the URL in `feeds.json`, or remove the feed and add a new one.

**The reader window shows an error.** It needs the Microsoft Edge WebView2 runtime. Install it from Microsoft, or set
**Open headlines in** to your default browser.

**Windows says the app is unrecognized.** It isn't code-signed. Choose **More info**, then **Run anyway**, or build it from
source.

**Launching it does nothing.** It's probably already running and minimized. Look for the tray icon, or press the hotkey.

**The widget is off-screen after a monitor change.** Close it, delete the `Left` and `Top` lines from `settings.json`, and
start it again. It will return to the bottom right of your main screen.

**I want to start over.** Exit the widget and delete `%LocalAppData%\BtcWidget`.

## Frequently asked questions

**Do I need an account or API key?** No.

**Does it send my data anywhere?** No. It has no telemetry. It only requests prices, news, logos and the update check from
the public services listed in the [README](README.md#privacy-and-network-use).

**How fresh are the prices?** As fresh as your poll interval: hourly by default, or every 5 minutes for lines with alerts.
Clicking Refresh updates everything right away. Some stock data is delayed by the source.

**Why poll hourly by default?** It's plenty for keeping an eye on things, and it's gentle on free public services. You can
lower it to as little as 1 minute in Settings.

**Can I watch the same coin on two exchanges?** Yes. Add it twice and give each line a different provider.

**Can I hide a line without deleting it?** Not from the app at the moment. Each line has an `Enabled` value in
`settings.json`. Set it to `false` while the widget is closed.

**Can I run it on two monitors?** One widget runs at a time. It can sit on any monitor and adapts to that screen.

**Can I move my setup to another PC?** Yes. Copy `%LocalAppData%\BtcWidget` to the same place on the other PC while the
widget is closed.

**Is this financial advice?** No. It's a display of public data with no guarantee of accuracy.
