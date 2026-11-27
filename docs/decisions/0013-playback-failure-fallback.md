# 0013 — Video that can't play: delete it, show the slideshow, stall watchdog

Date: 2026-11-27. From the Codex review; user decision.

- When a video ends `Failed` (decode error, prepare timeout, stall, bad size, missing file), its cached file is deleted (downloaded again on wifi) and the bundled slideshow plays in the same `Show`. The host gets one `Opened`, one `Closed` and one result: the slideshow's. Not after "destroyed" (scene change) and not when the slideshow itself fails.
- Stall watchdog (`StallWatch`): once prepared, if the frame number doesn't change for 5 s of active time, the video fails ("stalled") → slideshow. Before that, a stopped decoder is restarted (`Play()`) after 0.5 s, e.g. after returning from the background.
- Video too short: loops (unchanged). Too long: keeps playing past the reward. Zero frames / zero size: fails → slideshow.
- The result reaches the host exactly once, whatever throws: bookkeeping (cap, tracking, flush, prefetch, cache delete) is guarded step by step, a throwing `IPromoPlayer.Play` becomes `Failed("player: …")`, and all completion paths (also the capped notice) go through a once-guard.
