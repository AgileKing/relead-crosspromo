# Status / handoff (2026-11-27, end of first session)

All 10 KICKOFF steps have code. 111 EditMode tests pass. Tested in the editor only: no device build has been made yet.

## Done

- Catalog parse (MiniJson, forgiving). Selection, daily cap, fallback selection. Daily catalog check with conditional GET and base-URL fallback. 2-slot wifi-only prefetch with eviction.
- Video and slideshow players, with rotate hint, rotated stage and letterbox. Store links. Installed check on iOS and Android. Plist and manifest post-process. Tracker with offline queue. `CrossPromo` facade. Editor tester window. Demo sample. `docs/integration.md`.
- Decisions 0001–0007 in `docs/decisions/`.

## Open / next

1. **Bundled fallback screenshots**: `Runtime/Resources/ReleadCrossPromoFallback.asset` does not exist yet. Create it via Assets > Create > Relead > Cross-Promo Fallback Game Set inside the package. Add a few JPGs (~1080 px) per game with names and store ids. Until then the offline slideshow has nothing to show, so `IsReady` is false without a cached video.
2. **Device test**: Android and iOS builds. Check the rotate hint angles on real hardware, `canOpenURL`, `<queries>` resolution, VideoPlayer from `file://` on Android, and the Info.plist merge.
3. **Play-mode smoke test** against the live catalog (`kingofgames-placeholder-01`). Store ids are still "TODO", so use the Tester window (RequireStoreLink=false).
4. **Android Play Install Referrer** for `first_open` attribution in game B. It needs Google's `installreferrer` library, which is a third-party dependency, so it needs a decision.
5. `first_open` uses the same name as a host's own first_open event, if it has one. Check for a stats clash at api.relead.com; maybe rename it to `crosspromo_first_open`.
6. UWP/Windows: there is no installed check. The store link is in place, but untested.
7. Before tagging v0.1.0: date the CHANGELOG, create the git tag, and create a GitHub remote. The remote needs the user's OK.
