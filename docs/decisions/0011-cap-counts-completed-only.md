# 0011 — Daily cap counts completed promos only

Date: 2026-11-27. User decision.

- `maxPerDay` counts a promo only when it ends `Completed` (watched to `rewardAfterSec`, reward granted). Skipped, Failed (incl. playback errors and "destroyed" on scene change) and the Capped notice don't use up the day.
- Counted when the promo closes (local day at that moment), not when it opens. A promo still open at midnight counts for the new day.
- Reason: the cap limits rewards per day; a player who skips should not lose today's rewarded ads.
- Unchanged: rotation history (`ShowHistory`, last-shown promo) still records every shown promo, and `promo_shown` is still tracked when a promo opens.
- Supersedes the "every shown promo" line in 0003.
