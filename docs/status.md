# Status / handoff

## 0.1.4 (2026-11-27)

Codex review (`docs/reviews/2026-11-27-codex-review.md`) fixed: daily cap counts completed promos only (0011); download trust and size caps (0012); broken/stalled video → deleted + slideshow, result exactly once (0013); ad pauses on focus loss / background (0014); safe area and host shape changes mid-ad (0015); no `device_model` (0016); clock-back cap, promo audio via AudioSource + `CrossPromoConfig.IsMuted`, atomic file writes (0017). 164 EditMode tests pass. **Device test needed**: pause/focus and returning from the store, AudioSource sound on Android/iOS, notch phones in landscape (safe area, rotated stage). Tell crosspromo-content: `bytes` + `sha256` and plain relative paths are now required (live catalog already complies).

## 0.1.3 (2026-11-27)

Mismatched-orientation video plays at once (letterboxed, small rotate icon in the bar) and goes full screen whenever the phone is turned and held 0.5 s (decision 0009). Daily cap notice + `PromoOutcome.Capped`, `IsCappedToday`, `RemainingToday` (decision 0010, for GP King). Glow behind Skip/countdown, smaller Skip. 142 EditMode tests pass. **Not checked visually yet**: hint position in the letterbox bar (portrait and landscape host, iPad), glow on white video, capped notice, rotate-after-panel rebuild. Device test still open (item 2 below).

## 0.1.2 (2026-11-27)

The 30 s ad timeline, plus media per language and screen ratio (decision 0008). 129 EditMode tests pass. Timeline, ranking and parsing are covered by tests. **The new overlay layout is not checked visually yet**: the Skip/Earned/× row and the download panel in portrait (≈ 730 canvas units wide) and landscape. Check it in the editor tester window or on a device, with an nl and an iPad cut (kingofgames-kp001 / puzzleking-pp001 are live).

## First session (2026-11-27)

All 10 KICKOFF steps have code. 111 EditMode tests pass. Tested in the editor only: no device build has been made yet.

## Done

- Catalog parse (MiniJson, forgiving). Selection, daily cap, fallback selection. Daily catalog check with conditional GET and base-URL fallback. 2-slot wifi-only prefetch with eviction.
- Video and slideshow players, with rotate hint, rotated stage and letterbox. Store links. Installed check on iOS and Android. Plist and manifest post-process. Tracker with offline queue. `CrossPromo` facade. Editor tester window. Demo sample. `docs/integration.md`.
- Decisions 0001–0007 in `docs/decisions/`.

## Open / next

1. **Bundled fallback set**: done (kingofgames, puzzleking, gpking; 3 JPGs each, 854 KB total) from king-promo / puzzle-promo / race store screenshots. Source: `Runtime/FallbackScreenshots/fallback-games.json` + `<id>/NN.jpg`; rebuild with Relead > Cross-Promo > Rebuild Fallback Set. Open: GP King has no public listing yet (Play 404, no App Store id), so its slides link to a dead Play page until it is published; urlSchemes now match the live catalog (g01 kingofgames, g02 puzzleking, g05 gpking; a test checks the set against `Fixtures/promos.live.json`). All 6 catalog games are in the set (1.4 MB of JPGs); Chopper King, Breakout King and Faster Numbers use their live store screenshots, fetched read-only via the vault store API keys. GP King has an App Store id (6837372954) but is not public yet: add it to the catalog and the set once live. ASC also lists Memory Kings (com.relead.memorykings) and Block Kings (com.relead.blockkings), which are not in the catalog. No Windows product ids.
2. **Device test**: Android and iOS builds. Check the rotate hint angles on real hardware, `canOpenURL`, `<queries>` resolution, VideoPlayer from `file://` on Android, and the Info.plist merge.
3. **Play-mode smoke test** against the live catalog (`kingofgames-placeholder-01`). Store ids are still "TODO", so use the Tester window (RequireStoreLink=false).
4. **Android Play Install Referrer** for `first_open` attribution in game B. It needs Google's `installreferrer` library, which is a third-party dependency, so it needs a decision.
5. `first_open` uses the same name as a host's own first_open event, if it has one. Check for a stats clash at api.relead.com; maybe rename it to `crosspromo_first_open`.
6. UWP/Windows: there is no installed check. The store link is in place, but untested.
7. Before tagging v0.1.0: date the CHANGELOG, create the git tag, and create a GitHub remote. The remote needs the user's OK.
