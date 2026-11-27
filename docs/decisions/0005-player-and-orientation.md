# 0005 — Player overlay and orientation handling

Date: 2026-11-27

- UI is built in code (uGUI, `LegacyRuntime.ttf`, generated rounded-rect and phone textures): no prefabs or art assets, so nothing in the package can lose its `.meta` reference. Screen Space Overlay canvas, sorting order 30000, 1080×1080 reference, match 0.5.
- Shared `PromoOverlay` base (backdrop, rotatable stage, rotate hint, countdown → ×, progress bar, end card); `VideoPromoPlayer` and `SlideshowPromoPlayer` only supply content. A future `WebPromoPlayer` (option C) plugs into the same base.
- Orientation: the stage is rotated by `screenAngle − deviceAngle` (uGUI CCW-positive), size swapped. Rotated / Letterbox are sticky once chosen (no flapping when the player tilts back). Flat/unknown device orientation never matches → letterbox after `rotateHintTimeoutSec`. `Screen.orientation` is only read, never set.
- Device orientation comes from a provider (`Input.deviceOrientation` by default). Projects using only the new Input System can't call `UnityEngine.Input`; the provider then returns Unknown and the rotate hint simply times out to letterbox. A host can pass its own provider.
- Unscaled time everywhere (works with `timeScale = 0`), frame delta capped at 0.25 s so a return from background doesn't jump the skip/complete clock. The watch clock only ticks while content actually plays.
- Video: `file://` URL from the cache, `RenderTexture` sized to the video, `AspectRatioFitter` FitInParent (= letterbox), audio `Direct`, 10 s prepare timeout → Failed. Muting the host's own music is the host's job (callback).
- Slideshow: up to 2 screenshots per game, interleaved, 15 s total, 3–6 s per slide; always in the host orientation.
- Close before the end = Skipped (only possible after `skipAfterSec`). Content end → end card; closing it = Completed. The overlay reports exactly once, also if it is destroyed by a scene change (Failed "destroyed").
