# CLAUDE.md — relead-crosspromo

Unity package `com.relead.crosspromo`, shared by all Relead games. Design: `docs/design.md` (read it first). Content/catalog repo: `C:\Repos\crosspromo-content` (owns `catalog/promos.schema.json` = the contract). Served from `https://ads.relead.com`. Tracking: `https://api.relead.com`, contract in `C:\Repos\KOTGApi\docs\analytics-platform.md` §2 Ingest.

## Rules (caveman)

- Repo root = dev Unity project (6000.5.8f1). Package = `Packages/com.relead.crosspromo/` (embedded). Games consume via git URL `?path=/Packages/com.relead.crosspromo#vX.Y.Z`.
- Commit `.meta` files for everything in the package. Git UPM folder is immutable → missing meta = asset ignored.
- Min Unity 6000.5 (user 2026-11-27; FasterNumbers gets upgraded later). No third-party deps. No Newtonsoft.
- Namespace `Relead.CrossPromo`. Asmdefs: `Relead.CrossPromo`, `Relead.CrossPromo.Editor`, `Relead.CrossPromo.Tests`.
- Tests first, strict. EditMode tests for catalog parse, selection/filters, caps, orientation choice, store-link building. No test = not done. 0 tests reported = failed run.
- Run tests (Editor closed): `& "C:\Program Files\Unity\Hub\Editor\6000.5.8f1\Editor\Unity.exe" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults TestResults.xml -logFile Logs/test-run.log` → read only `<test-run ` line + failed cases; log: `Select-String 'error CS|Exception'`. Never `-logFile -`.
- Store rules: reward for watching only, never for install. Skip after 5 s. No IDFA/GAID.
- Secrets: PowerShell SecretManagement vault, never in repo (see global CLAUDE.md).
- Store API keys in vault (one Apple team + one Play account for all Relead games, despite the `PK_` prefix): `PK_ASC_ISSUER_ID`, `PK_ASC_KEY_ID`, `PK_ASC_KEY_P8` (App Store Connect API) and `PK_PLAY_SERVICE_ACCOUNT` (Play Developer API service-account JSON). Use them read-only here (app ids, store screenshots for `Runtime/FallbackScreenshots`); auth code to reuse: `C:\repos\puzzle-promo\agents\store_apple.py` / `store_play.py` (Play: open edit, read, delete edit).
- Decisions: `docs/decisions/NNNN-title.md`, one per choice, same commit as the work.
- Versioning: semver in package.json + git tag. CHANGELOG.md per release.
- Context cap ~150k: commit, write handoff in `docs/status.md`, stop.
