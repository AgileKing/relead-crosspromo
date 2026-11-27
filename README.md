# Relead Cross-Promo

A Unity package (`com.relead.crosspromo`) that shows a ~20-second promo of another Relead game inside a Relead game, and gives the player a reward for watching. It replaces network ads: no third-party SDK, no advertising ID, no cookie banner.

- Content (catalog + videos + later playables) is **not** in this repo. It lives in the `crosspromo-content` repo and is served from **https://ads.relead.com**. A new game = a commit there, no new package version.
- Tracking goes to the Relead analytics platform at `https://api.relead.com` (contract: analytics platform doc in the KOTGApi repo).

## Install in a game

`Packages/manifest.json`:

```json
"com.relead.crosspromo": "https://github.com/AgileKing/relead-crosspromo.git?path=/Packages/com.relead.crosspromo#v0.1.1"
```

Always pin a tag, so an update in one game can't break another.

## Repo layout

The repo root is a small Unity project used to develop and test the package. The package itself is embedded at `Packages/com.relead.crosspromo/`.

See `docs/design.md` for how it works.
