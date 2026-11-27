# Kickoff brief (first session)

Goal: build v0.1.0 of the package = option A (video cross-promo) with orientation handling, ready for option C. Read `CLAUDE.md` and `docs/design.md` first.

Steps:
1. Create dev Unity project in repo root (6000.5.8f1, batch mode `-createProject` is fine) + `.gitignore` for Unity. Embedded package at `Packages/com.relead.crosspromo/` with the existing `package.json` moved there.
2. Catalog model + parser for `crosspromo-content/catalog/promos.schema.json` (schemaVersion 1). Copy `promos.json` sample there as test fixture. Unknown fields/types must be ignored, not crash.
3. Selection: filter self / installed / dates / minPackageVersion / unplayable media types, weighted pick, maxPerDay cap. Pure C#, testable.
4. Cache + prefetch (UnityWebRequest → persistentDataPath, size/sha256 check, last good catalog on disk). Base URL list with fallback.
5. `IPromoPlayer` + `VideoPromoPlayer` (uGUI + VideoPlayer + RenderTexture), close button after skipAfterSec, end card with store button.
6. Orientation: rotate overlay with animated phone icon (code-drawn or simple sprite in package), wait for `Input.deviceOrientation`, timeout → letterbox. Never touch `Screen.orientation`.
7. Store links per platform (+ campaign params), installed check (iOS canOpenURL, Android package query, Windows none). Native bits via small plugins in `Plugins/iOS` and `Plugins/Android`. Document the Info.plist / manifest entries a game must add (or add them via post-process build step — preferred).
8. `ITracker` → api.relead.com ingest (read the KOTGApi doc). Offline queue.
9. Editor window: "Play promo X as portrait/landscape host" for testing. Sample scene in `Samples~`.
10. `docs/integration.md`: how a game adds the package (manifest line, Init call, reward callback, its gameId + urlScheme).

Tests per step. Commit per step. Ask the user before: creating a GitHub remote, anything costing money. Keep the user posted with short updates in the console. End with a ≤ 20-line summary: what works, test totals, what's open.
