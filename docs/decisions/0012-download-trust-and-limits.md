# 0012 — Downloads: relative paths, https, size and hash required, size caps

Date: 2026-11-27. From the Codex review (docs/reviews/2026-11-27-codex-review.md), user decision "fix the OWASP issues".

- Catalog paths (`media[].url`, `games[].icon`, `poster`) must be plain relative paths: letters, digits, `.`, `_`, `-`, `/`; no `..`, no empty segment, no scheme, no `//host`, no `%`, `?`, `#` (`UrlUtil.IsSafeRelative`). Anything else is dropped at parse time; `UrlUtil.Join` returns null for it. Catalog content can never send a download to another host.
- Base URLs (config) are used only when they are `https://`. Media downloads only go to https.
- Media is only planned/downloaded with `bytes` and `sha256` in the catalog and `bytes` ≤ 50 MB (`Prefetcher.MaxMediaBytes`). A promo without them gives its prefetch slot to the next promo. The live catalog already has both for every video (guarded by `LiveCatalogMeetsTheDownloadRules`).
- Size caps, enforced while downloading (`HttpRequest.MaxBytes`: Content-Length or bytes received → abort, partial file deleted): catalog 1 MB, media = its catalog `bytes`, icon 1 MB.
- Icons: PNG/JPEG only, ≤ 1024 × 1024, checked from the file header before `Texture2D.LoadImage` (`ImageGuard`, no decompression bombs). Rejected icons are deleted from the cache.
- Video: `RenderTexture` only for ≤ 4096 px per side.
- A cached file whose size no longer matches the catalog is not "cached" (re-downloaded).
- Not done: catalog signing. TLS + host allowlist + per-file sha256 from the catalog limit a compromised CDN to what the catalog host serves.
