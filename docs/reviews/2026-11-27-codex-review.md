# Codex review — 2026-11-27 (package 0.1.3 + decision 0011)

Read-only review by Codex (codex-cli 0.164.0). Prompt: [2026-11-27-codex-prompt.md](2026-11-27-codex-prompt.md). Follow-up: see the Outcome section at the end.


## Findings

1. **High — Catalog-controlled downloads are not constrained to trusted HTTPS hosts**

   [Http.cs:47](../../Packages/com.relead.crosspromo/Runtime/Net/Http.cs:47), [CatalogUpdater.cs:108](../../Packages/com.relead.crosspromo/Runtime/Cache/CatalogUpdater.cs:108), [CatalogParser.cs:165](../../Packages/com.relead.crosspromo/Runtime/Catalog/CatalogParser.cs:165)

   `UrlUtil.Join` accepts absolute `http://` and `https://` media/icon URLs, bypassing configured content hosts. Catalogs are accepted over TLS but are not signed. A compromised catalog/CDN could therefore make every game download cleartext or attacker-hosted files and feed them to native image/video decoders.

   Suggested fix: require relative media paths or HTTPS URLs on an explicit host allowlist; cryptographically sign catalogs; require trusted hashes for all downloadable media; validate MIME type and file signatures.

2. **High — No catalog, media, image, or decoded-dimension size limits**

   [UnityHttp.cs:29](../../Packages/com.relead.crosspromo/Runtime/Net/UnityHttp.cs:29), [Prefetcher.cs:160](../../Packages/com.relead.crosspromo/Runtime/Cache/Prefetcher.cs:160), [CrossPromo.cs:409](../../Packages/com.relead.crosspromo/Runtime/CrossPromo.cs:409), [VideoPromoPlayer.cs:69](../../Packages/com.relead.crosspromo/Runtime/Player/VideoPromoPlayer.cs:69)

   A large catalog is buffered completely in memory; media streams to disk without a maximum; icons are loaded with `File.ReadAllBytes`; video dimensions directly determine a `RenderTexture`. A bad catalog can fill storage, OOM the process, or request a huge GPU allocation.

   Suggested fix: enforce strict catalog/icon/video byte caps, reject excessive `Content-Length`, abort when streamed bytes exceed the cap, check free disk space, and cap decoded image/video dimensions before allocation.

3. **High — A video that prepares but then stops can block the game indefinitely**

   [VideoPromoPlayer.cs:34](../../Packages/com.relead.crosspromo/Runtime/Player/VideoPromoPlayer.cs:34), [VideoPromoPlayer.cs:86](../../Packages/com.relead.crosspromo/Runtime/Player/VideoPromoPlayer.cs:86), [PromoOverlay.cs:160](../../Packages/com.relead.crosspromo/Runtime/Player/PromoOverlay.cs:160)

   The only watchdog covers preparation. After `_prepared` becomes true, a zero-frame, truncated, unsupported, or stalled video can have `isPlaying == false` without emitting `errorReceived`. The watch clock then stops forever. If it stalls before a nonzero `skipAfterSec`, no exit control ever unlocks.

   Suggested fix: add first-frame and playback-progress watchdogs, handle unexpected `loopPointReached`/stops, and fail over to the bundled slideshow after a bounded timeout. Keep an unconditional emergency close available.

4. **Medium — Cache integrity is optional, and playback failure never falls back**

   [CatalogParser.cs:180](../../Packages/com.relead.crosspromo/Runtime/Catalog/CatalogParser.cs:180), [MediaCache.cs:29](../../Packages/com.relead.crosspromo/Runtime/Cache/MediaCache.cs:29), [MediaCache.cs:32](../../Packages/com.relead.crosspromo/Runtime/Cache/MediaCache.cs:32), [CrossPromo.cs:107](../../Packages/com.relead.crosspromo/Runtime/CrossPromo.cs:107)

   `bytes` and `sha256` may both be absent, in which case any nonempty response is committed. Later, cache validity is only `File.Exists`. A truncated MP4 returned with a clean EOF, wrong content, or a damaged existing file is considered ready. Video preparation/error returns `Failed`; the slideshow is only selected when no cached video was initially found.

   Suggested fix: require size and SHA-256 for video assets, reverify cached files before selection, validate basic container signatures, remove invalid entries, and retry the same `Show` using the slideshow.

5. **Medium — Focus/background lifecycle is not connected to playback**

   [CrossPromo.cs:170](../../Packages/com.relead.crosspromo/Runtime/CrossPromo.cs:170), [CrossPromo.cs:469](../../Packages/com.relead.crosspromo/Runtime/CrossPromo.cs:469), [PromoOverlay.cs:147](../../Packages/com.relead.crosspromo/Runtime/Player/PromoOverlay.cs:147)

   Only `OnApplicationPause` is observed, and it merely flushes tracking/refreshes caches. There is no `OnApplicationFocus` handling and no explicit video pause/resume. A notification shade or permission dialog can leave video and reward time running while hidden; conversely, a platform-paused `VideoPlayer` may return with `isPlaying == false` and leave the clock frozen. Store round-trips have the same risk.

   Suggested fix: propagate pause and focus state to the active overlay, pause content and gate the watch clock whenever the app is not visible, then explicitly resume or restart playback after confirming foreground state.

6. **Medium — Controls ignore notches, rounded corners, and system gesture areas**

   [PromoUI.cs:25](../../Packages/com.relead.crosspromo/Runtime/Player/PromoUI.cs:25), [PromoOverlay.cs:106](../../Packages/com.relead.crosspromo/Runtime/Player/PromoOverlay.cs:106), [PromoOverlay.cs:337](../../Packages/com.relead.crosspromo/Runtime/Player/PromoOverlay.cs:337)

   UI is anchored to full-screen corners with fixed 32-unit margins. On landscape notched phones, rotated stages, or devices with large gesture insets, Skip/close/earned controls or the bottom CTA can be obscured or untappable.

   Suggested fix: lay interactive chrome inside `Screen.safeArea`, transform the safe area for rotated stages, enforce minimum physical touch sizes, and relayout when resolution or safe area changes.

7. **Medium — Device locale and regional/script language matching are incomplete**

   [CrossPromo.cs:416](../../Packages/com.relead.crosspromo/Runtime/CrossPromo.cs:416), [PromoSelector.cs:98](../../Packages/com.relead.crosspromo/Runtime/Selection/PromoSelector.cs:98), [PromoUI.cs:92](../../Packages/com.relead.crosspromo/Runtime/Player/PromoUI.cs:92)

   Automatic language detection recognizes only six languages; Japanese, Chinese, Arabic, and most others become English. Media matching strips the device language to its primary subtag but compares it to the complete media tag. Thus device `pt-BR` plus media `pt-BR` ranks that media below default English; `zh-Hans`/`zh-Hant` cannot be negotiated correctly. Legacy uGUI text and fixed left-aligned layouts also do not provide RTL shaping/layout.

   Suggested fix: use a canonical BCP-47 locale, rank exact tag → language/script → primary language → default, and add explicit RTL/bidi/font-glyph support.

8. **Medium — The “result exactly once” guarantee is not exception-safe**

   [CrossPromo.cs:261](../../Packages/com.relead.crosspromo/Runtime/CrossPromo.cs:261), [CrossPromo.cs:273](../../Packages/com.relead.crosspromo/Runtime/CrossPromo.cs:273), [CrossPromo.cs:184](../../Packages/com.relead.crosspromo/Runtime/CrossPromo.cs:184)

   If a supported custom tracker throws from `Flush`, or later bookkeeping throws, execution stops before `Closed` and `onResult`; a completed ad may not grant its reward. Separately, if `CappedNotice.Show` throws after assigning its callback, the catch invokes `done`, while the leaked notice can invoke it again during `OnDestroy`.

   Suggested fix: wrap each bookkeeping operation independently and deliver the result in `finally`; use a shared once-guard for all completion paths; catch synchronous `player.Play` failures and restore `_showing`.

9. **Medium — Cached state is replaced non-atomically**

   [CatalogUpdater.cs:135](../../Packages/com.relead.crosspromo/Runtime/Cache/CatalogUpdater.cs:135), [CatalogUpdater.cs:174](../../Packages/com.relead.crosspromo/Runtime/Cache/CatalogUpdater.cs:174), [MediaCache.cs:57](../../Packages/com.relead.crosspromo/Runtime/Cache/MediaCache.cs:57)

   Catalog and media replacement delete the old final file before moving the new one; catalog state is written directly. A crash, disk-full condition, or failed move in that window can destroy the last good offline catalog/media or leave catalog and state inconsistent.

   Suggested fix: write and flush a same-directory temporary file, then use atomic replace/rename while preserving the old copy until replacement succeeds. Apply the same approach to state and tracking queue files.

10. **Low — Orientation and media choice are frozen at ad start**

    [CrossPromo.cs:238](../../Packages/com.relead.crosspromo/Runtime/CrossPromo.cs:238), [PromoOverlay.cs:70](../../Packages/com.relead.crosspromo/Runtime/Player/PromoOverlay.cs:70), [PromoOverlay.cs:153](../../Packages/com.relead.crosspromo/Runtime/Player/PromoOverlay.cs:153)

    The host orientation is captured as a constant. An auto-rotating game or unfolded foldable can change shape during the ad, but an initially `Upright` stage never re-enters orientation evaluation and the selected ratio variant is not reconsidered. It remains usable through letterboxing, but may show the wrong cut and layout.

    Suggested fix: observe screen-size/orientation changes and relayout/recompute stage mode. Define whether a mid-ad fold should retain the current asset or switch only on the next impression.

11. **Low — Local-clock changes can repeatedly reset the reward cap**

    [DailyCap.cs:25](../../Packages/com.relead.crosspromo/Runtime/Selection/DailyCap.cs:25), [DailyCap.cs:40](../../Packages/com.relead.crosspromo/Runtime/Selection/DailyCap.cs:40)

    Only one `yyyy-MM-dd|count` value is stored. Alternating the device date or time zone between two days makes each stored day mismatch the current day, repeatedly restoring the full cap.

    Suggested fix: retain several dated counters and a monotonic “latest day seen,” or enforce reward caps server-side where abuse matters.

## Not covered by tests

- Real `VideoPlayer` preparation, first-frame delay, short/long/zero-length video, looping, decoder errors, mid-play stalls, and slideshow fallback after failure.
- Pause/focus loss, store round-trips, process backgrounding, `timeScale = 0`, callback exceptions, synchronous player failure, and double-tap/re-entrancy.
- Rendered UI on safe areas, tall phones, tablets, foldables, rotated stages, varied DPI, long translations, CJK fonts, or RTL.
- Regional/script locale negotiation such as `pt-BR`, `zh-Hans`, `zh-Hant`, and Arabic.
- HTTPS/host restrictions, signatures, content-type checks, byte/dimension caps, oversized JSON, disk-full behavior, atomic recovery, or revalidation of damaged cache files.
- Auto-rotation or screen resizing during an ad, and manual clock/time-zone cap manipulation.

The existing tests do cover pure aspect ranking for 19.5:9, 16:9, and 4:3; orientation-state calculations; the abstract 30-second clock; slideshow looping; and hash verification when metadata is supplied. They do not exercise the actual rendered overlay or native video lifecycle.

No advertising-ID access or permissive certificate handler was found. Store URLs remain fixed to known store schemes and escape catalog IDs, and cache filenames are hashed, so direct path traversal and arbitrary catalog-provided deeplinks are mitigated. Tracking still transmits persistent install/IDFV identifiers plus device model, OS, and locale, which should be treated and disclosed as pseudonymous personal data.
## Outcome (Claude, same day)

Verified against the code. User decisions: fix the real problems and the OWASP issues, drop the device model, fix the safe area and the orientation bug and the smaller issues; languages stay as they are (everything not nl/de/fr/es/it/pt is English, no RTL).

| # | Finding | Result |
|---|---|---|
| 1 | Downloads not limited to trusted hosts | Fixed: relative paths only, https base URLs only (decision 0012). Catalog signing not done. |
| 2 | No size limits | Fixed: catalog 1 MB, media = catalog `bytes` (≤ 50 MB), icon 1 MB + ≤ 1024 px header check, video ≤ 4096 px (0012). Free-space check not done. |
| 3 | Stalled video freezes the timer | Fixed: stall watchdog, decoder restart (0013). Rated lower than High: Skip shows from 0 s, so the game was never blocked. |
| 4 | Integrity optional, no fallback on failure | Fixed: `bytes` + `sha256` required, size re-checked, failed file deleted, slideshow in the same `Show` (0012, 0013). |
| 5 | Focus / pause not connected | Fixed: `PlaybackGate`, video and clock pause (0014). |
| 6 | Safe area ignored | Fixed (0015). |
| 7 | Locale matching | Not changed (user: fine for now). |
| 8 | Result not exception-safe | Fixed: once-guard, guarded bookkeeping (0013). |
| 9 | Non-atomic replace | Fixed: `AtomicFile` (0017). |
| 10 | Orientation frozen at ad start | Fixed (0015). Same media until the next promo. |
| 11 | Clock changes reset the cap | Fixed for setting the date back (0017). |
| — | Tracking privacy | `device_model` removed (0016). |
| — | (Claude) Promo audio ignored the game's mute | Fixed: AudioSource + `CrossPromoConfig.IsMuted` (0017). |

Tests: `HardeningTests.cs` (URL safety, https, caps, hashes, image header, atomic write, clock-back cap, stall watch, pause gate, fallback rule, once-guard, safe-area mapping, host change). Still not covered by automated tests: the real `VideoPlayer` on devices, rendered layouts per device.
