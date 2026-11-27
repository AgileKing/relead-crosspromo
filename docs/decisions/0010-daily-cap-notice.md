# 0010 — Daily cap: "Maxed out ads for today" notice and `PromoOutcome.Capped`

Date: 2026-11-27. User decision (requested via GP King, which keeps its rewarded buttons active while capped and drops its own house-ad fallback).

- `Show` with `maxPerDay` used up no longer returns `Failed("daily cap")` silently. It shows a small, light notice: "Maxed out ads for today" / "Max aantal ads voor vandaag bereikt". Light dim, tap anywhere or × closes it at once (no timer), closes itself after 3 s (`CappedNotice.AutoCloseSec`, unscaled time).
- Result: new `PromoOutcome.Capped` (appended to the enum, so existing values keep their numbers), `FailReason` "daily cap". Distinct from `Failed`; never rewarded. Reported once, also on scene change.
- New queries: `CrossPromo.IsCappedToday` and `CrossPromo.RemainingToday`. `IsReady` stays false when capped (it means "a promo would play"), so hosts enable their button on `IsReady || IsCappedToday`.
- While the notice shows, `IsShowing` is true (a second `Show` = Failed "already showing"). No `Opened`/`Closed` events: no audio, nothing for the host to pause.
- Tracking: new event `promo_capped` (prop `maxPerDay`).
