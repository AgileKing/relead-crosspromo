# Design: Relead Cross-Promo package

Decided 2026-11-27 with the user. Phase 1 = option A (video). Everything is prepared for option C (HTML5 playable in a web view) so C can be added without breaking games already in the stores.

## Goals

- Every Relead game (King of Games, GP King, Puzzle King, Chopper King, Breakout King, …; one new game per month) promotes the other games.
- The player chooses to watch a promo (rewarded slot) and gets a reward from the host game when it finishes.
- A new game is promoted in all existing games **without updating those games**: only the content catalog changes.
- Measure everything: shown, completed, skipped, store click, and installs caused by a promo.
- Platforms: Android, iOS, Windows (UWP / Microsoft Store). WebGL ignored.

## Store rules (hard constraints)

- Reward for **watching** only. Never a reward that depends on installing or playing another game (Apple 3.2.2, Google Play incentivized-install policy).
- Close button visible after `skipAfterSec` (default 5 s). Reward only on completion.
- No downloaded code (Apple 2.5.2). Video/images/JSON are fine. HTML/JS in the system web view (option C) is allowed.
- No advertising ID (IDFA/GAID), no ATT prompt. Identifiers: iOS `identifierForVendor` (same for all our apps on a device), Android/Windows: our own random install ID.

## Flow

1. `CrossPromo.Init(config)` at startup: `hostGameId`, platform, language, base URLs, tracking endpoint, current orientation provider.
2. Catalog: download `https://ads.relead.com/catalog/promos.json` (short cache), keep last good copy on disk, validate `schemaVersion`.
3. Prefetch: the media of the next promo is downloaded in the background (wifi preferred) to `persistentDataPath`, verified by `bytes`/`sha256`. A promo is only "available" when its media is cached, so showing it is instant and works offline.
4. Selection: filter out the host game itself, games already installed, expired/not started promos, promos needing a newer `minPackageVersion`, media types this build can't play; then weighted random; respect `maxPerDay`.
5. `CrossPromo.IsReady` → host enables its "watch for reward" button. `CrossPromo.Show(onResult)` → result `Completed | Skipped | Failed` plus `StoreClicked` flag. Host grants the reward on `Completed`.
6. End card: icon, headline, "Get it" button → store page with campaign parameters, close button.

## Portrait vs landscape

Games are locked to portrait or landscape; promos may exist in one or both orientations.

- Pick media matching the host's current orientation if available.
- Otherwise: overlay with an **animated rotate-phone icon**; playback starts when `Input.deviceOrientation` matches the media. The package never changes `Screen.orientation` — the host stays locked, the promo UI is drawn rotated 90° inside it.
- No rotation within `rotateHintTimeoutSec` (default 3 s): play letterboxed in the current orientation.
- Same rule later for playables (catalog says which orientation a playable needs).

## Prepared for option C

- `IPromoPlayer` interface: `VideoPromoPlayer` now, `WebPromoPlayer` later (gree unity-webview / Vuplex / WebView2 on UWP; JS bridge messages `ready`, `complete`, `storeClick`, `close`).
- Catalog media `type`: `video` now, `playable` later. A build that can't play a type skips it; a promo with only unplayable media is filtered out.
- `minPackageVersion` per promo.

## Installed check / attribution

- iOS: every game declares URL schemes `relead-g01` … `relead-g50` in `LSApplicationQueriesSchemes` **now**, and registers its own scheme (from the catalog `games[].urlScheme`). `canOpenURL` tells which games are installed. Attribution: store click event in game A and `first_open` in game B share `identifierForVendor`.
- Android: `<queries>` with the package names (also reserved ahead where known) or intent-scheme check; store link carries `referrer=utm_source=<hostGameId>&utm_campaign=crosspromo&utm_content=<promoId>`, game B reads it with the Play Install Referrer API on first launch and reports it.
- Windows: `ms-windows-store://pdp/?productid=<id>&cid=crosspromo_<promoId>`; no exact install attribution.

## Tracking events (to api.relead.com)

`promo_catalog_loaded`, `promo_shown`, `promo_rotate_hint`, `promo_completed` (watch seconds), `promo_skipped`, `promo_store_click`, `promo_failed` (reason), `first_open` (with install referrer / vendor id). Each carries hostGameId, promoId, targetGameId, platform, packageVersion, install id. Behind an `ITracker` interface; queue offline, flush later.

## Hosting

- `ads.relead.com` may be blocked by ad-blocking DNS (many blocklists match `ads.*`). The config therefore takes a **list** of base URLs; the first that answers wins. A second hostname can be added later without a code change if config is read from the catalog's companion file.

## Compatibility

- `"unity": "6000.5"` in package.json (user 2026-11-27). FasterNumbers (2021.3) is upgraded before it gets the package. Develop/test on 6000.5.8f1.
- No dependencies beyond Unity built-ins (VideoPlayer, UnityWebRequest, uGUI). JSON via `JsonUtility` or a tiny parser; Newtonsoft optional is not allowed (not every game has it).
