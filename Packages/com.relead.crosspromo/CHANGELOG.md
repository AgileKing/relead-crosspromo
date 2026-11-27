# Changelog

## [0.1.4] - 2026-11-27

- **Daily cap counts completed promos only**: skipping, a playback failure or a scene change no longer uses up one of today's `maxPerDay` (decision 0011). `RemainingToday` drops only after a rewarded promo.
- **Broken video → slideshow**: a video that fails (decode error, timeout, stall, bad size) is deleted from the cache and the offline slideshow plays in the same `Show`. New stall watchdog (5 s without a new frame). The result always reaches the host exactly once, even when a tracker or callback throws (decision 0013).
- **Pause on focus loss**: notification shade, calls, app switch or opening the store pause the video and the reward timer (decision 0014).
- **Safe area**: Skip, countdown, ×, "Earned! Close", download panel and slideshow bar stay inside `Screen.safeArea`, also on a rotated stage. Host shape changes mid-ad (auto-rotate, foldables) re-layout the stage (decision 0015).
- **Download hardening**: catalog paths must be plain relative paths (no other hosts, no `..`); https base URLs only; media needs `bytes` + `sha256` and ≤ 50 MB; downloads abort above their cap (catalog 1 MB, icon 1 MB); icons ≤ 1024 px checked before decoding; video ≤ 4096 px (decision 0012).
- **Tracking**: `device_model` is no longer sent (decision 0016).
- Daily cap can't be refilled by setting the date back; promo audio follows `AudioListener.volume`, new `CrossPromoConfig.IsMuted`; catalog, state, media and the event queue are replaced atomically (decision 0017).

## [0.1.3] - 2026-11-27

- **Video starts at once** when its orientation differs from the game: it plays letterboxed with a small, non-blocking rotate-phone icon in the bar above (or beside) it. No more dark full-screen hint.
- **Go full screen whenever the player turns the phone** (match held 0.5 s), also after the hint timed out; the video doesn't restart and stays rotated. `rotateHintTimeoutSec` now only sets how long the icon shows (0 = none).
- **Daily cap notice**: `Show` with `maxPerDay` reached shows "Maxed out ads for today" / "Max aantal ads voor vandaag bereikt" (tap anywhere or ×, auto-closes after 3 s) and reports the new `PromoOutcome.Capped` (never reward it). New `CrossPromo.IsCappedToday`, `CrossPromo.RemainingToday`; enable buttons on `IsReady || IsCappedToday`. New tracking event `promo_capped`.
- `OrientationLogic.Next` takes the match-held seconds (internal logic; hosts unaffected).
- Soft black glow behind Skip, the countdown and the rotate hint, so they stay readable on white video.
- Smaller Skip / "Overslaan" button (font 24).

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
