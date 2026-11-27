# 0002 — Offline fallback: slideshow of screenshots bundled in the package

Date: 2026-11-27 (user, via the King of Games session)

When no video is cached (first launch offline, no wifi yet, no catalog) the rewarded slot still works: a slideshow of screenshots of other Relead games, with name and store button. Same rules as a video: close after `skipAfterSec`, reward only on completion.

- Screenshots ship **only in the package** (not on the web host, not in the catalog). A new package version refreshes them. A briefly considered optional `games[].screenshots` catalog field was dropped again.
- Format JPG/PNG (Unity can't decode WebP), ~1080 px, a few per game.

Related changes from the same request: catalog check at most once a day with conditional GET; two prefetch slots, wifi only, rotating, disk cap; ~15 s timeouts, everything in the background (slow shared hosting possible).
