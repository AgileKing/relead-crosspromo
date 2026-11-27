# 0014 — The ad pauses while the app has no focus

Date: 2026-11-27. From the Codex review; user question "what happens if the game loses focus".

- `OnApplicationFocus(false)` (notification shade, incoming call, permission or system dialog, app switcher) and `OnApplicationPause(true)` (app in background, store opened from the download panel) pause the video and stop the watch clock (`PlaybackGate`). The reward therefore needs 30 s of foreground watching.
- On return the video resumes where it was; the clock continues. Frame delta stays capped at 0.25 s, so no jump.
- Prepare timeout and stall watchdog only count active time, so a long pause never fails the ad.
- The ad stays open while in the background; there is no timeout for "away too long" (the player closes it with Skip / ×).
