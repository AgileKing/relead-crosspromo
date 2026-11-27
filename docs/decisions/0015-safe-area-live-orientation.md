# 0015 — Safe area, and host shape changes mid-ad

Date: 2026-11-27. From the Codex review; user decision.

- All controls (Skip, countdown, ×, "Earned! Close", download panel, progress bar, slideshow store bar) sit in a `Safe` rect inside `Screen.safeArea` (notch, rounded corners, home bar). The video itself stays full-bleed. When the stage is drawn rotated, the insets are mapped to the stage's own sides (`OrientationLogic.SafeInsets`). Recomputed every frame (cheap), so safe-area changes apply at once.
- The host orientation is asked every frame instead of once at `Show`. When it changes mid-ad (auto-rotating game, foldable opened/closed): matching media goes upright, otherwise letterbox without a second rotate hint, then the normal rules (turn the device → rotated). The media variant (ratio cut) stays until the next promo.
