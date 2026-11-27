# CLAUDE.md — relead-crosspromo

Unity package `com.relead.crosspromo`, shared by all Relead games. Design: `docs/design.md` (read it first). Content/catalog repo: `crosspromo-content` (owns `catalog/promos.schema.json` = the contract). Served from `https://ads.relead.com`. Tracking: `https://api.relead.com`, contract in the analytics platform doc (KOTGApi repo, §2 Ingest).
This repo is public: no secrets, no secret names, no local paths in committed files.

## Rules (caveman)

- Repo root = dev Unity project (6000.5.8f1). Package = `Packages/com.relead.crosspromo/` (embedded). Games consume via git URL `?path=/Packages/com.relead.crosspromo#vX.Y.Z`.
- Commit `.meta` files for everything in the package. Git UPM folder is immutable → missing meta = asset ignored.
- Min Unity 6000.5 (user 2026-11-27; FasterNumbers gets upgraded later). No third-party deps. No Newtonsoft.
- Namespace `Relead.CrossPromo`. Asmdefs: `Relead.CrossPromo`, `Relead.CrossPromo.Editor`, `Relead.CrossPromo.Tests`.
- Tests first, strict. EditMode tests for catalog parse, selection/filters, caps, orientation choice, store-link building. No test = not done. 0 tests reported = failed run.
- Run tests (Editor closed): `& "C:\Program Files\Unity\Hub\Editor\6000.5.8f1\Editor\Unity.exe" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults TestResults.xml -logFile Logs/test-run.log` → read only `<test-run ` line + failed cases; log: `Select-String 'error CS|Exception'`. Never `-logFile -`.
- Store rules: reward for watching only, never for install. Ad timeline from catalog rules (Skip from 0 s, download panel 25 s, reward 30 s; decision 0008). No IDFA/GAID.
- Secrets: PowerShell SecretManagement vault, never in repo (see global CLAUDE.md).
- App Store Connect and Play Developer API keys exist in the vault (shared by all Relead games). Use them read-only here (app ids, store screenshots for `Runtime/FallbackScreenshots`).
- Decisions: `docs/decisions/NNNN-title.md`, one per choice, same commit as the work.
- Versioning: semver in package.json + git tag. CHANGELOG.md per release. Games pin `#vX.Y.Z` (no ranges in git UPM). After pushing a tag: call agent `relead-crosspromo` with `release: vX.Y.Z` → it bumps + commits each game's manifest (no push).
- Context cap ~150k: commit, write handoff in `docs/status.md`, stop.
