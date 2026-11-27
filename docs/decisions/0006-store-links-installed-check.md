# 0006 — Store links, installed check, native setup

Date: 2026-11-27

- **Android installed check via JNI from C#**, no Java plugin: `getPackageInfo(pkg)` first, then `resolveActivity(VIEW relead-gNN://)`. Visibility on Android 11+ comes from `<queries>`: an `<intent>` per reserved scheme `relead-g01…g50` (works for games that don't exist yet) plus `<package>` entries from `CrossPromoBuildSettings.androidPackages`. Every game registers its own scheme as a VIEW/BROWSABLE intent-filter on the launcher activity. All added by `IPostGenerateGradleAndroidProject` (unityLibrary manifest; manifest merger carries it into the app).
- **iOS**: `Plugins/iOS/ReleadCrossPromo.mm` (`canOpenURL`, `identifierForVendor`). Post-build adds `relead-g01…g50` to `LSApplicationQueriesSchemes` (merged with existing; warning above iOS's 50-entry limit) and the host scheme to `CFBundleURLTypes`.
- The host's own scheme comes from a `CrossPromoBuildSettings` asset in the game (editor-only ScriptableObject). Missing/invalid → warning, build continues. The class must stay in `Editor/CrossPromoBuildSettings.cs` (Unity binds a ScriptableObject asset to its script only when file and class names match; fixed in 0.1.1, guarded by a round-trip test).
- **Links**: Android `market://details?id=…&referrer=utm_source=<host>&utm_campaign=crosspromo&utm_content=<promo>` (URL-encoded); iOS `https://apps.apple.com/app/id<id>?mt=8&ct=cp-<host>-<promo>` (≤ 40 chars, optional `pt` provider token from config); Windows `ms-windows-store://pdp/?productid=<id>&cid=crosspromo_<promo>`.
- Installed answers are cached per session and cleared on app resume (the player may just have installed a game). Exceptions = not installed.
- Not in 0.1.0: reading the Play Install Referrer in game B (needs Google's `installreferrer` library = third-party dependency). `first_open` is sent with the vendor id (iOS) / install id; referrer support is an open item.
