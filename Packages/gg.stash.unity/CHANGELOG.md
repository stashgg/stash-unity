# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).


## [2.2.3] - 2026-06-17

### Changed

- **Embedded Stash Native SDK [2.2.3](https://github.com/stashgg/stash-native/releases/tag/2.2.3)** (`StashNative-2.2.3.aar`, `StashNative.xcframework`). No wrapper API changes.

### Fixed

- **iOS**: `CloseBrowser` and deeplink dismissal now fully reset presentation state. A `SFSafariViewController` dismissed programmatically previously left the SDK's internal "presented" guard set, so the next `OpenCard`/`OpenModal` silently did nothing (no UI, no callback); the guard now also self-heals if left stale with no presentation on screen (upstream SDK fix; no wrapper API change).
- **Android**: pre-API-30 devices no longer push the card off-screen when the soft keyboard opens; a keyboard detector keeps the focused input visible above the keyboard (upstream SDK fix; no wrapper API change).
- **Android**: `OnDialogDismissed` now fires when `autoClose` is `false`.
- **Android**: `OpenCard` and `OpenModal` now emit `OnPageLoaded`; previously only the legacy popup WebView path reported page load.

## [2.2.1] - 2026-06-02

### Added

- **Embedded Stash Native SDK [2.2.1](https://github.com/stashgg/stash-native/releases/tag/2.2.1)** (`StashNative-2.2.1.aar`, `StashNative.xcframework`).
- **`autoClose`** on **`StashNativeCardConfig`** and **`StashNativeModalConfig`** (`bool`, default `true`). When `false`, the card/modal stays open after the payment success/failure callback (callbacks still fire immediately) until closed by the page, user, or host.

### Fixed

- **Android**: card no longer shifts off-screen when the soft keyboard opens; it now resizes to keep the focused input visible above the keyboard (upstream SDK fix; no wrapper API change).

## [2.2.0] - 2026-05-26

### Changed

- **Embedded Stash Native SDK [2.2.0](https://github.com/stashgg/stash-native/releases/tag/2.2.0)** (`StashNative-2.2.0.aar`, `StashNative.xcframework`).
- **Android**: `OnBrowserClosed` now fires out of the box with the default `UnityPlayerActivity` — no `AndroidManifest.xml` edits required. The SDK now owns the Chrome Custom Tabs `startActivityForResult` lifecycle internally via an invisible proxy activity (`StashNativeBrowserProxyActivity`, declared in the AAR manifest and auto-merged into your app). The `ACTION_VIEW` fallback (when `androidx.browser` is not on the classpath) continues to use lifecycle-based detection.

### Removed

- **Android (breaking)**: `StashNativeUnityActivity` — the optional `UnityPlayerActivity` subclass introduced in 2.1.4 is no longer needed and has been removed. If you previously opted in by setting `android:name="com.stash.popup.StashNativeUnityActivity"` in your project's `AndroidManifest.xml`, revert that to `com.unity3d.player.UnityPlayerActivity` (or your own activity).
- **Android (breaking)**: `StashNativeCardUnityBridge.onActivityResult(...)` — the upstream `StashNativeCard.onActivityResult` and `REQUEST_CODE_CUSTOM_TAB` symbols no longer exist in stash-native 2.2.0; the bridge no longer exposes a forwarder. If your project forwards `onActivityResult` to the bridge from a custom activity, remove that call.

### iOS

- xcframework bumped to 2.2.0; no API changes.

## [2.1.4] - 2026-05-12

### Added

- **Embedded Stash Native SDK [2.1.4](https://github.com/stashgg/stash-native/releases/tag/2.1.4)** (`StashNative-2.1.4.aar`, `StashNative.xcframework`).
- **`OnBrowserClosed`** (`Action`): fires when the external browser (Chrome Custom Tabs on Android / `SFSafariViewController` on iOS) is closed after `OpenBrowser` or an external payment redirect.
- **Android**: `StashNativeUnityActivity` — optional drop-in replacement for `UnityPlayerActivity` that forwards `onActivityResult` to the SDK, giving more reliable `OnBrowserClosed` delivery for Chrome Custom Tabs. Opt in by setting `android:name="com.stash.popup.StashNativeUnityActivity"` in your project's `AndroidManifest.xml`. The `ACTION_VIEW` fallback still uses lifecycle-based detection and works without it.

## [2.1.3] - 2026-04-09

### Fixed

- Unity warning “A meta data file (.meta) exists but its folder 'Packages/gg.stash.unity/Samples~' can't be found”: `package.json` declared a sample at `Samples~/StashSample`, but that folder was not in the repository (only `Samples~.meta` was). Added the missing `Samples~/StashSample` assets so the folder exists in version control and Package Manager sample import works.

### Changed

- Removed root `Samples~.meta`. Unity does not import assets under paths containing `~`, so that meta made **Samples~** show up in the Project window as an empty folder even though the sample files exist on disk. **Window > Package Manager > Samples > Import** is still how you add the sample to `Assets/`; the files are not meant to appear under the embedded package tree.

## [2.1.2] - 2026-04-09

### Changed

- Improved iOS forced rotation.

## [2.1.1] - 2026-04-02

### Changed

- **Embedded Stash Native SDK [2.1.1](https://github.com/stashgg/stash-native/releases/tag/2.1.1)** (`StashNative-2.1.1.aar`, `StashNative.xcframework`).

### Breaking Notes

- **`StashNativeModalConfig`**: removed `showDragBar` (no longer present in native modal config).
- **Payment success**: `OnPaymentSuccess` and per-call `successCallback` are now `Action<string>` — optional order payload from checkout (may be null or empty).

### Added

- **`OnExternalPayment`** (`Action<string>`): fired when checkout continues outside the app (GPay, Klarna, crypto, etc.).
- **`backgroundColor`** on **`StashNativeCardConfig`** and **`StashNativeModalConfig`** (optional hex string; null/empty keeps native default theme).
- **Android keep-alive**: `SetKeepAliveEnabled`, `SetKeepAliveConfig` / **`StashNativeKeepAliveConfig`** — short foreground service during external browser flows ([stash-native README](https://github.com/stashgg/stash-native/blob/main/README.md)).

## [2.0.0] - 2025-03-16

### Changed

- **UPM (Unity Package Manager) support**: Package restructured for full UPM compatibility. Content moved from `Assets/Stash.Native/` to `Packages/gg.stash.unity/` with standard layout: `Runtime/`, `Editor/`, `Plugins/`, `Samples~/`.
- Added assembly definitions (`Stash.Unity`, `Stash.Unity.Editor`) for compilation isolation.
- Samples are now importable via Package Manager (Window > Package Manager > Stash for Unity > Samples).

### Installation

- **Git URL**: Add package from git URL: `https://github.com/stashgg/stash-unity.git?path=Packages/gg.stash.unity`
- **manifest.json**: Add `"gg.stash.unity": "https://github.com/stashgg/stash-unity.git?path=Packages/gg.stash.unity"` to dependencies.
