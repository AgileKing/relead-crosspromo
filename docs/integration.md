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
watchButton.interactable = CrossPromo.IsReady;   // poll in Update or when the screen opens

CrossPromo.Show(result =>
{
    if (result.Outcome == PromoOutcome.Completed) GrantReward();   // watching only, never installing
});
```

- `IsReady` is also true when there is no video yet. In that case `Show` plays the bundled screenshot slideshow (offline fallback), with the same reward rules.
- `result.StoreClicked` is for information only. **Never** make the reward depend on it (Apple 3.2.2 / Google Play policy).
- `CrossPromo.Opened` / `CrossPromo.Closed` let you mute your music. The overlay uses unscaled time, so pausing with `Time.timeScale = 0` is fine.
- The daily cap (`rules.maxPerDay` in the catalog) applies. `IsReady` turns false when it is reached.

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

- **New Input System only:** `Input.deviceOrientation` is unavailable, so the rotate hint times out to letterbox. Pass `DeviceOrientation` from your own input code if you want rotation.
- **Data:** `persistentDataPath/relead-crosspromo/` holds the catalog, its state, the media cache (2 videos + icons) and the event queue.
- **Identifiers:** no IDFA/GAID and no ATT prompt. iOS sends `identifierForVendor`, and other platforms send a random install id.
