# 0003 — Selection rules beyond the design

Date: 2026-11-27

On top of the filters in design.md (self, installed, dates, minPackageVersion, unplayable media, weight, maxPerDay):

- **No store id for this platform = filtered.** A promo the player can't act on is wasted. `"TODO"` ids count as missing, so the current sample catalog yields nothing on device until real ids are in. `SelectionContext.RequireStoreLink = false` for editor testing.
- `end` is exclusive, `start` inclusive, both UTC.
- **Cached media in the other orientation beats an uncached match**: showing something now (with rotate hint/letterbox) is better than nothing.
- **Same promo not twice in a row** when another one is ready.
- `maxPerDay` counts per **local** calendar day, every shown promo (video and fallback slideshow). `0` = off (kill switch).
- **Fallback slideshow**: never the host, never a game without bundled screenshots; prefers not-installed games with a store id; if none are left it shows installed ones rather than nothing, so the rewarded slot keeps working offline.
