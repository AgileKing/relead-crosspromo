# Changelog

## [0.1.2] - 2026-11-27

- **Ad timeline** (catalog rules, defaults if missing): small Skip top left from `skipAfterSec` (now 0), download panel over the still-playing video at `endCardAtSec` (25 s), top-right countdown → × plus "Earned! Close" at `rewardAfterSec` (30 s). Closing from then on = `Completed`; before = `Skipped`. Video and slideshow loop until closed. Replaces the old full-screen end card after the video ended. Hosts: no API change, still grant the reward on `Completed`.
- **Media per language and screen ratio**: catalog media `lang` and `width`/`height`. Picks host orientation, then the player's language (else default), then the closest aspect ratio (iPad vs phone cuts). Prefetch downloads only that variant.
- New tracking events `promo_end_card`, `promo_earned`.
- Fix: `CrossPromoInfo.PackageVersion` was still 0.1.0 (now tested against package.json).

## [0.1.1] - 2026-11-27

- Fix: `CrossPromoBuildSettings` now lives in its own `CrossPromoBuildSettings.cs`. Before, the asset was saved with no script, so builds never registered the game's own url scheme (iOS CFBundleURLTypes, Android VIEW intent-filter) or the `androidPackages` `<queries>`: other games could not detect it as installed. Games that skipped creating the asset: create it now (Assets > Create > Relead > Cross-Promo Build Settings).
- Offline fallback set: PuzzleKing slides are now Sudoku, Crime Scene and Traffic Jam (from the current store screenshots), instead of Sudoku, Paint and Checkmate.

## [0.1.0] - 2026-11-27

- Video cross-promo (option A): catalog schemaVersion 1, daily catalog check (conditional GET, base-URL fallback), 2-slot wifi-only prefetch.
- Rewarded flow: `CrossPromo.Init / IsReady / Show`, close after `skipAfterSec`, end card with store button, `maxPerDay`.
- Orientation: rotate hint with animated phone icon, rotated stage inside the locked host, letterbox after timeout.
- Offline fallback slideshow from screenshots bundled in the package.
- Installed check (iOS canOpenURL, Android package/scheme), store links with campaign params, Info.plist / AndroidManifest post-process.
- Tracking to api.relead.com with offline queue.
- Prepared for playables (option C): `IPromoPlayer`, unknown media types skipped, `minPackageVersion`.
