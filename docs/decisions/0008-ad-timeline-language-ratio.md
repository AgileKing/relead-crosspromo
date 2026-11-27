# 0008 — 30-second ad timeline, media per language and screen ratio

Date: 2026-11-27. User decision. Catalog side: crosspromo-content decisions 0002 and 0003. Supersedes the "close before the end / content end → end card" part of 0005.

## Ad timeline (catalog `rules`, defaults when missing)

| Time | Screen | Close now = |
|---|---|---|
| 0 s → (`skipAfterSec` 0) | Content plays. Small **Skip** top left, the whole time. Top right: countdown to the reward. | Skipped |
| 25 s → (`endCardAtSec`) | Download panel (icon, name, headline, store button) at the bottom, over the **still-playing** video. | Skipped |
| 30 s → (`rewardAfterSec`) | Countdown turns into ×, plus a green **"Earned! Close"** button next to it. Skip stays. | Completed |

- Content loops: the video player loops, and the slideshow wraps around. A 10 s video still runs into the panel and the reward, and a 60 s video keeps playing after 30 s.
- The clock only ticks while content actually plays, as before (rotate hint and prepare time don't count).
- The parser raises `rewardAfterSec` to `endCardAtSec` if the catalog has it lower.
- The slideshow keeps its own store button per slide, so it gets no extra download panel.
- New event `promo_end_card` when the panel appears, and `promo_earned` when the reward unlocks.
- Store policy is unchanged: the reward is for watching only, and the store button has no effect on it.

## Media choice

`ChooseMedia` ranks the playable (and cached) media of a promo by:

1. Host orientation (rotating is worse than a different language).
2. Language: `media.lang` equals the player's language (primary subtag, so `nl-NL` matches `nl`); then the default (no `lang` or `en`); then any other language.
3. Aspect ratio: smallest |ln(media long/short ÷ screen long/short)|. Media without `width`/`height` rank after sized ones.
4. Catalog order.

`SelectionContext.Language` comes from the config language. `ScreenAspect` comes from `Screen.width/height`. Prefetch uses the same ranking without the cache filter, so it downloads only the best variant. Showing uses the best cached variant. When the language changes, a cached default-language video still plays until the better one is downloaded.

## Rejected

- Device classes (`ipad` / `phone`): iOS-only thinking, and wrong for Android tablets and foldables.
- Stopping on the last frame for short videos: a frozen frame under the panel looks broken. Looping is what ad networks do.
