# 0018 — Ad closes itself 10 s after the reward

Date: 2026-11-27. User decision.

- 10 s after the reward is earned (so at 40 s with the 30 s timeline), the ad closes itself with `PromoOutcome.Completed`, exactly as if "Earned! Close" was tapped. Video and offline slideshow alike.
- The 10 s is watch-clock time: it doesn't run while the app is paused or unfocused (decision 0014), so a player who switches away comes back to the open ad.
- `PromoResult.AutoClosed` = true in that case; tracking: `promo_completed` gets prop `autoClosed`.
- Fixed in the package (`WatchClock.DefaultAutoCloseAfterEarnedSec`), not a catalog rule.
