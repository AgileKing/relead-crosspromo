# 0004 — Catalog refresh and prefetch mechanics

Date: 2026-11-27

- **Catalog**: at most one network check per 24 h (`catalog-state.json`: lastCheckUtc, ETag, Last-Modified, base URL). 200 and 304 both count as a check. A failed check stores nothing (next launch retries) and is not repeated in the same session. Clock moved back = due. A 200 with an unparseable/unsupported body counts as a failure for that host and the next base URL is tried; the disk copy is never replaced by a bad one.
- **Base URLs**: the one that last served the catalog is tried first; media are fetched from it. Conditional headers are only sent to that same host.
- **Timeouts**: 15 s for the catalog (slow shared hosting), 120 s total for a media file (UnityWebRequest timeout is whole-request).
- **Prefetch plan**: 2 slots. Order = never shown → least recently shown → higher weight → id. Deterministic on purpose: rotation via show history gives variety, and it is testable. Target game icons are fetched with the video (optional: an icon failure doesn't fail the pass).
- **Wifi only** (`ReachableViaLocalAreaNetwork`), re-checked before each file.
- **Eviction** = everything not in the plan, but only once every planned video is on disk, so a failed download never leaves the player with nothing to show. The 2-slot plan is the disk cap.
- Cached file name = hash of the (immutable) relative URL; files only enter the cache after size/sha256 verification, so "exists" = valid.
