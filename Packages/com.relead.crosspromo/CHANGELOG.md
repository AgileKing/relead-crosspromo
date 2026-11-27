# Changelog

## [0.1.0] - 2026-11-27

- Video cross-promo (option A): catalog schemaVersion 1, daily catalog check (conditional GET, base-URL fallback), 2-slot wifi-only prefetch.
- Rewarded flow: `CrossPromo.Init / IsReady / Show`, close after `skipAfterSec`, end card with store button, `maxPerDay`.
- Orientation: rotate hint with animated phone icon, rotated stage inside the locked host, letterbox after timeout.
- Offline fallback slideshow from screenshots bundled in the package.
- Installed check (iOS canOpenURL, Android package/scheme), store links with campaign params, Info.plist / AndroidManifest post-process.
- Tracking to api.relead.com with offline queue.
- Prepared for playables (option C): `IPromoPlayer`, unknown media types skipped, `minPackageVersion`.
