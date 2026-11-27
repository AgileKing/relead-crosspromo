# 0017 — Daily cap ignores a clock set back; promo audio follows the game's volume

Date: 2026-11-27. From the Codex review (smaller issues); user decision.

- Daily cap: when the stored day is later than today (date set back, time zone west), the later day's count keeps applying, and new completions add to it. Moving the clock back and forth no longer refills the cap. Moving it forward still starts a fresh day (unavoidable locally; the cap is a soft limit).
- Promo audio goes through an `AudioSource` (was `Direct`), so `AudioListener.volume` (game master volume / mute) applies. `ignoreListenerPause` is on: a host that sets `AudioListener.pause` on `Opened` still hears the promo. New optional `CrossPromoConfig.IsMuted` (`Func<bool>`): true = promo muted (the game's own sound setting).
- Catalog, catalog state, media and the tracking queue are written via temp file + one rename (`AtomicFile`), so a crash or full disk never loses the last good copy.
