# Adding Relead Cross-Promo to a game

Min Unity 6000.5. No dependencies beyond Unity built-ins.

## 1. Manifest

`Packages/manifest.json`, always pin a tag:

```json
"com.relead.crosspromo": "https://github.com/AgileKing/relead-crosspromo.git?path=/Packages/com.relead.crosspromo#v0.1.1"
```

## 2. Init at startup

```csharp
using Relead.CrossPromo;

CrossPromo.Init(new CrossPromoConfig
{
    HostGameId = "gpking",          // THIS game's Relead id: kingofgames | puzzleking | gpking (catalog games[].id)
    IngestKey  = "<gpking ingest key for api.relead.com>",
    // TrackingGameId = "gpking",   // only if the analytics id differs from HostGameId
    // InstallId = myAnalytics.InstallId, // if the game already has a stable install id
    // DeviceOrientation = () => ..., // only if the project disabled the legacy Input API (see below)
});
```

`HostGameId` is how the package knows which game not to advertise (in videos and in the offline slideshow). As a safety net, a game whose Android package equals `Application.identifier` is also never shown, and Init logs a warning if the id is unknown.

Everything runs in the background: the catalog is checked at most once a day, and two videos are prefetched, on wifi only.

## 3. Rewarded button

```csharp
// IsCappedToday: Show then shows "Maxed out ads for today" (outcome Capped), so keep the button tappable.
watchButton.interactable = !CrossPromo.IsShowing && (CrossPromo.IsReady || CrossPromo.IsCappedToday);

CrossPromo.Show(result =>
{
    if (result.Outcome == PromoOutcome.Completed) GrantReward();   // watching only, never installing
});
```

- `IsReady` is also true when there is no video yet. In that case `Show` plays the bundled screenshot slideshow (offline fallback), with the same reward rules.
- `result.StoreClicked` is for information only. **Never** make the reward depend on it (Apple 3.2.2 / Google Play policy).
- `CrossPromo.Opened` / `CrossPromo.Closed` let you mute your music. The overlay uses unscaled time, so pausing with `Time.timeScale = 0` is fine.
- Promo sound follows `AudioListener.volume` and ignores `AudioListener.pause`. If your game has its own sound-off setting, pass it: `IsMuted = () => !Settings.SoundOn` in `CrossPromoConfig`.
- Losing focus (notification shade, call, app switch, store opened) pauses the video and the 30 s reward timer; they continue when the game is back (decision 0014).
- A video that can't play is deleted from the cache and the offline slideshow plays instead, in the same `Show` (one `Opened`/`Closed`, one result).
- Catalog media need `bytes` and `sha256` and a plain relative `url`; media without them is never downloaded (decision 0012).
- The daily cap (`rules.maxPerDay` in the catalog) applies. Only completed (rewarded) promos count; a skip or failure never uses up the day. When it is reached, `IsReady` turns false and `IsCappedToday` true. `Show` then shows a small notice ("Maxed out ads for today" / "Max aantal ads voor vandaag bereikt"; tap anywhere or ×, closes itself after 3 s) and reports `PromoOutcome.Capped` (`FailReason` "daily cap"). Never reward it. `RemainingToday` gives the promos left today, e.g. for a label. The notice raises no `Opened`/`Closed`.

## 4. Own URL scheme (installed check)

Create **Assets > Create > Relead > Cross-Promo Build Settings**. Set `hostUrlScheme` to this game's `games[].urlScheme` from the catalog, e.g. `relead-g02`. Optionally list the Android package names of other Relead games.

The build post-process then adds, with nothing to do by hand:

- **iOS Info.plist:** `LSApplicationQueriesSchemes` `relead-g01` … `relead-g50`, plus `CFBundleURLTypes` with the own scheme. iOS honours at most 50 query schemes. If the game declares other schemes, you get a build warning.
- **Android manifest:** `<queries>` with an intent per `relead-gNN` scheme and the listed packages, plus a VIEW/BROWSABLE intent-filter for the own scheme on the launcher activity.

Without this asset you get a build warning and the game still works. Other games just can't see that this one is installed.

## 5. Testing

- Open **Window > Relead > Cross-Promo Tester** in Play Mode. You can download a promo, play it as a portrait or landscape host, simulate the device orientation, and play the offline slideshow.
- Import the **Demo scene** sample from Package Manager: put `DemoHost` on an empty GameObject.
- In the editor, games without a store id (`"TODO"`) are only shown when `RequireStoreLink = false`.

## Notes

- **New Input System only:** `Input.deviceOrientation` is unavailable, so the video stays letterboxed (it never rotates). Pass `DeviceOrientation` from your own input code if you want rotation.
- **Data:** `persistentDataPath/relead-crosspromo/` holds the catalog, its state, the media cache (2 videos + icons) and the event queue.
- **Identifiers:** no IDFA/GAID and no ATT prompt. iOS sends `identifierForVendor`, and other platforms send a random install id.
