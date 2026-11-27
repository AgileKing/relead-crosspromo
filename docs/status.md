# Status / handoff (2026-11-27, end of first session)

All 10 KICKOFF steps have code. 111 EditMode tests pass. Tested in the editor only: no device build has been made yet.

## Done

- Catalog parse (MiniJson, forgiving). Selection, daily cap, fallback selection. Daily catalog check with conditional GET and base-URL fallback. 2-slot wifi-only prefetch with eviction.
- Video and slideshow players, with rotate hint, rotated stage and letterbox. Store links. Installed check on iOS and Android. Plist and manifest post-process. Tracker with offline queue. `CrossPromo` facade. Editor tester window. Demo sample. `docs/integration.md`.
- Decisions 0001–0007 in `docs/decisions/`.

## Open / next

1. **Bundled fallback set**: done (kingofgames, puzzleking, gpking; 3 JPGs each, 854 KB total) from king-promo / puzzle-promo / race store screenshots. Source: `Runtime/FallbackScreenshots/fallback-games.json` + `<id>/NN.jpg`; rebuild with Relead > Cross-Promo > Rebuild Fallback Set. Open: GP King has no public listing yet (Play 404, no App Store id), so its slides link to a dead Play page until it is published; urlSchemes now match the live catalog (g01 kingofgames, g02 puzzleking, g05 gpking; a test checks the set against `Fixtures/promos.live.json`). Chopper King, Breakout King and Faster Numbers are in the catalog but have no bundled screenshots yet. No Windows product ids.
2. **Device test**: Android and iOS builds. Check the rotate hint angles on real hardware, `canOpenURL`, `<queries>` resolution, VideoPlayer from `file://` on Android, and the Info.plist merge.
3. **Play-mode smoke test** against the live catalog (`kingofgames-placeholder-01`). Store ids are still "TODO", so use the Tester window (RequireStoreLink=false).
4. **Android Play Install Referrer** for `first_open` attribution in game B. It needs Google's `installreferrer` library, which is a third-party dependency, so it needs a decision.
5. `first_open` uses the same name as a host's own first_open event, if it has one. Check for a stats clash at api.relead.com; maybe rename it to `crosspromo_first_open`.
6. UWP/Windows: there is no installed check. The store link is in place, but untested.
7. Before tagging v0.1.0: date the CHANGELOG, create the git tag, and create a GitHub remote. The remote needs the user's OK.
