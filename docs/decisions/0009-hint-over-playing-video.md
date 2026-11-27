# 0009 — Rotate hint over the playing video, glow behind Skip and countdown

Date: 2026-11-27. User decision. Amends the hint and "sticky letterbox" parts of 0005 and the "rotate hint doesn't count" note in 0008. Also requested by crosspromo-content.

- Content starts at once, also when its orientation differs from the host. While the hint is up the stage is laid out like letterbox (video fitted in the host screen).
- The hint is small and non-blocking: no dark full-screen backdrop, just the animated phone icon and "Turn your phone" centred in the letterbox bar above the video (landscape media on a portrait host) or right of it (portrait media on a landscape host). Skip and countdown sit in the corners, so the middle of the bar is free. Bar thickness: `OrientationLogic.LetterboxBar` (16:9 assumed). On squarish screens it overlaps the video edge; its glow keeps it readable.
- Whenever the device matches the media for `OrientationLogic.MatchStableSec` (0.5 s), the stage switches to the rotated full-screen stage under the running video (no restart), also long after the hint timed out. Already matching at the start: rotated at once. Rotated sticks (no flapping when tilting back). Flat/unknown never matches, so it never switches.
- `rotateHintTimeoutSec` now only says how long the icon shows (0 = no icon). Letterbox is no longer sticky.
- If the stage turns after the download panel appeared, the panel is rebuilt for the new shape (`promo_end_card` still tracked once).
- The hint time now counts towards the watch clock, because the video plays.
- A soft black glow (generated sliced sprite, `PromoUI.Glow`, 28 units beyond the element, 60 % black) sits behind Skip, the countdown and the hint, so they stay readable on white video.
- Skip is smaller: 140×60, font 24 (was 160×72, 32), so "Overslaan" fits without dominating.
