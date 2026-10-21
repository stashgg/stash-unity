# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).


## [3.0.0-preview.1] - 2026-10-19

Preview release tracking the **Stash Native 3.0.0 (Preview)** redesign. Breaking update from 2.x; the README has a [migration table](https://github.com/stashgg/stash-unity/blob/main/README.md#migrating-from-2x). Removed members carry `[Obsolete(error: true)]` so every call site is flagged at compile time.

### Changed

- **Embedded Stash Native SDK [3.0.0](https://github.com/stashgg/stash-native/releases/tag/3.0.0)** (`StashNative-3.0.0.aar`, `StashNative.xcframework`), using the patched preview binaries published 2026-10-21 18:23 UTC (iOS keeps card content visible while the card dismisses; iOS loading cover uses Liquid Glass on iOS 27+ and an opaque cover on iOS 26, fixing a transparent loading card on iPadOS 26.1; iPhone landscape card width follows `preferredContentWidth`; Android content stays above the navigation bar; an accepted iOS open that cannot be shown ends with its dismissal event; Android `SCANNER_UNAVAILABLE` no longer opens a card first and a missing CameraX/ZXing library no longer crashes; stalled-load retries end with a network error; `localhost:8080/...` URLs open as https on iOS). Entry point is `StashNative` with one delegate/listener; the bridges were rewritten and renamed to `StashNativeUnityBridge.java` (package `com.stash.unity`) and `StashNativeBridge.mm`. Card and browser opens pass the Unity activity / view controller as the presenter and are marshaled to the native main thread.
- **`StashNativeCardConfig`** now describes the responsive card in points: `preferredContentWidth` (400), `preferredContentHeight` (560), `maximumContentHeight` (720), `edgeMargin` (16), `allowDismiss`, `autoClose`, `orientationPreference`. Replaces the eight ratio fields, `forcePortrait` and `backgroundColor`.
- **Dismiss events are split**: `OnCardDismissed` (was `OnDialogDismissed`), `OnScannerDismissed`, `OnBrowserDismissed` (was `OnBrowserClosed`). Payment auto-close, network error and external-payment handoff no longer emit a dismiss; per-call callbacks are cleared on each of those endings instead.
- **iOS payment results arrive after the card has closed** (as on Android), so the next card can be opened from the success callback.
- **Requirements**: iOS 15.0+. Android now **requires** `androidx.core:core` 1.12.0, `androidx.webkit:webkit` 1.11.0, `androidx.window:window` 1.4.0 and `androidx.window:window-java` 1.4.0; an EDM4U manifest (`Editor/StashNativeDependencies.xml`) and `mainTemplate.gradle` instructions are provided.
- **Sample** rebuilt to match the stash-native sample apps: UI Toolkit (UXML + USS, no extra packages) with bottom tabs Test / Settings / Instances, option sub-screens for the card (orientation, dismiss, auto-close, content sizing, Android backdrop), browser (iOS portrait Safari, Android keep-alive) and scanner copy, named instances (App ID + ingress secret, test or production) persisted on the device, orientation lock, and every callback shown as a timestamped chip. Scales from the shorter screen side so it reads the same on every device; two columns in landscape. The scene is now just a camera and the sample component.

### Changed (review pass)

- **Config types are classes** with native defaults (`new StashNativeCardConfig { orientationPreference = ... }` is safe); the 2.x fields `forcePortrait`, `backgroundColor` and the ratio fields remain as `[Obsolete(error: true)]` members with migration text.
- **`OpenCard` / `OpenScanner` return `bool`** and refuse to open while a card or scanner session is active, so a double tap can no longer hand card A's result to card B's callbacks. Every session-scoped native event carries a generation tag; events from a session that was reset are dropped.
- **Receiver name enforced**: a scene-placed `StashNative` component is renamed to `StashNative` and moved to the scene root; duplicates remove only the component; destroying the singleton detaches the native listener.
- **Fallback paths** (no native library, bridge exception) clear the per-call callbacks they can never fire; `OpenBrowser` falls back to the system browser like `OpenCard`.
- User callbacks and event subscribers are invoked individually; a throwing handler is reported on `OnNativeException` and no longer suppresses the others.
- `OpenCard` no longer rewrites URLs (the native SDK normalizes and validates them). `OpenModal` stub keeps its 2.x `config` parameter.
- Docs: `IsCurrentlyPresented` excludes Android Custom Tabs; `OnExternalPayment` fires while the card is still closing; `Dismiss()` also closes Safari on iOS; `ScannerUnavailable` also covers missing CameraX/ZXing on Android; Android pauses Unity while a card is on screen, so mid-card events arrive after it closes.
- iOS bridge runs native calls inline when already on the main thread, so `IsCurrentlyPresented` is true in the frame that opened. Android bridge re-asserts its listener on every open and reports a pending open as presented.
- Editor simulator honours `autoClose = false` and `allowDismiss`, closes before invoking callbacks (so reopening from a callback works), never creates the singleton outside Play mode, and backs `Dismiss`, `ResetPresentationState` and `IsCurrentlyPresented`.
- Requirements: Unity 2022.3 LTS (the AndroidX set needs compileSdk 34). Sample `mainTemplate.gradle` forces CameraX 1.3.4 so another plugin cannot pull 1.4.x back in.

### Added

- **QuickStart sample**: one script, one uGUI button, the Stash test cart, and numbered comments on where the backend URL, card options and purchase verification go. Listed first under Package Manager → Samples.
- **`OpenCard(..., networkErrorCallback, externalPaymentCallback)`**: per-call callbacks for the two endings that previously only had global events.
- **`DismissBrowser(bool paymentSucceeded)`** (iOS): closes Safari with the deeplink payment result, wrapping `dismissSafariViewControllerWithResult:`.
- **`IsScannerAvailable`**, **`IsKeepAliveEnabled`** (Android), **`LastScannerErrorMessage`**, **`InstanceOrNull`**, **`OpenScanner(config)`** overload.
- Android bridge `@Keep` annotation and `clearBackdrop()`; iOS bridge `_StashNativeBridgeDetach`.
- **`OpenScanner(scanned, dismissed, error, StashNativeScannerConfig?)`**, **`OnQrCodeScanned`**, **`OnScannerDismissed`**, **`OnScannerError`** and the **`StashNativeScannerError`** enum for QR Code Linking. Requires the new optional **`gg.stash.unity.codelink`** package (`Packages/gg.stash.unity.codelink`), which bundles `StashNativeCodeLink-3.0.0.aar`, `StashNativeCodeLink.xcframework` and an EDM4U manifest for CameraX/ZXing. Without it the error callback receives `ScannerUnavailable`; the core package has no camera code or permission.
- **`OpenBrowser(url, StashNativeBrowserConfig?)`**: iOS portrait Safari sheet via `orientationPreference`.
- **`StashNativeOrientationPreference`** enum (`FollowHost`, `Portrait`).
- **`SetBackdropBytes(byte[])`**: Android backdrop helper wrapping `StashNative.setBackdropBytes` (handles the `sbyte[]` JNI conversion).
- **`SdkVersion`**: embedded native SDK version string.
- **Editor simulator**: `OpenScanner` opens a text field standing in for the camera; **Scan** fires `OnQrCodeScanned`, **Dismiss Scanner** fires `OnScannerDismissed`. Payment buttons no longer also emit a dismiss, matching native auto-close.

### Removed

- **`OpenModal`** and **`StashNativeModalConfig`** (no native counterpart in 3.x; use `OpenCard`).
- **`OnOptinResponse`** (removed from the native SDK).
- **`StashNativeCardConfig.forcePortrait`**, the ratio fields and **`backgroundColor`**.
- Android `setActivity` plumbing on resume (3.x takes the activity per call).

### Fixed

- Upstream 3.0.0 fixes: checkout keeps its state, WebView and focused fields through rotation, keyboard and window changes; a lost connection ends checkout with `OnNetworkError` instead of a dismissal; the card reveals as soon as the page draws content; the Android JavaScript bridge accepts calls only from the checkout document.
- `OnPageLoaded` parses the load time culture-invariantly.

## [2.3.1] - 2026-09-03

### Changed

- **Embedded Stash Native SDK [2.3.1](https://github.com/stashgg/stash-native/releases/tag/2.3.1)** (`StashNative-2.3.1.aar`, `StashNative.xcframework`).
- **New-window navigations** in checkout WebViews (anchor `target="_blank"` or `window.open`, main frame or iframe) now open in the external browser instead of being dropped — http/https via the system browser, other schemes via the existing deeplink handling. The checkout stays presented. Handled inside the embedded SDK; no Unity wrapper API change.
- **Deeplink interception** extended to sub-frames, not just the main frame. Android parses `intent://` URIs with `browser_fallback_url` / Play Store fallback; iOS offers user-tapped https links to a claiming app as universal links; both degrade gracefully when no app is installed. Handled inside the embedded SDK; no Unity wrapper API change.

### Added

- **`StashNative.SetInspectableWebViewsEnabled(bool)`** and **`IsInspectableWebViewsEnabled`**: opt-in flag that makes checkout WebViews inspectable (Safari Web Inspector on iOS 16.4+, `chrome://inspect` on Android) for QA/debug builds and automated UI tests. Off by default; do **not** enable in production. Call before opening any checkout. Wraps the new native `StashNativeCard.setInspectableWebViewsEnabled(...)` API.

### Removed

- **Android**: Google Pay WebView redirect handling (`checkGooglePayRedirect` / `openGooglePayInBrowser` and the `GOOGLE_PAY_*` constants). No public API change.

### Fixed

- **Android**: checkout WebView content is never darkened — force-dark / algorithmic darkening is now disabled unconditionally instead of keyed to the host theme, fixing near-invisible third-party (Adyen secured-field) input text in device dark mode. The checkout self-themes via the `theme=` URL parameter.
- **Android/iOS**: expanded card height is clamped to the real content box, fixing the scroll bug and a keyboard-triggered expand that shrank the card below its collapsed height. Covers phone and tablet, including rotation and drag-release snap-back.
- **Android**: phone sheet height ceilings subtract bottom insets and intersect with the root-layout content box, closing a race where `expand()` ran before the first insets dispatch; `expand()` is capped so the card can never grow past 100%.
- **Android**: `clampRatio` guarded against NaN/Infinity (parity with iOS `stashClampRatio`); scheme / provider-detection string matching uses `Locale.ROOT` to avoid Turkish-locale mismatches.


## [2.3.0] - 2026-07-28

### Changed

- **Embedded Stash Native SDK [2.3.0](https://github.com/stashgg/stash-native/releases/tag/2.3.0)** (`StashNative-2.3.0.aar`, `StashNative.xcframework`). No wrapper API changes.
- **`StashNativeCardConfig.Default`**: landscape phone ratios updated to match native 2.3.0 (`cardWidthRatioLandscape` `0.9` → `0.7`, `cardHeightRatioLandscape` `0.6` → `0.9`).
- **Sample**: `StashLinkGenerator` now authenticates with versioned HMAC (`x-stash-hmac-signature`) using **App ID** + **ingress API key** instead of the deprecated `X-Stash-Api-Key` header. Set both fields on the sample component (Studio → Project Settings). Failures also surface the API response body.

### Added

- **Web SDK**: `window.stash_sdk.openLink(url)` opens a URL in the external browser with no callbacks and no dismissal (terms and misc links). Handled inside the embedded SDK; no Unity wrapper API change.
- **Deeplink handling** in checkout WebViews: main-frame navigations to custom schemes no longer dismiss the card (iOS) or show an error page (Android). URLs containing `stash-pay/success`, `stash-pay/failure`, or `stash-pay/cancel` run the standard payment success / failure / close flows; every other deeplink is handed to the OS and the checkout stays presented. Handled inside the embedded SDK; no Unity wrapper API change.

### Fixed

- Stability pass across both platforms (callback integrity, reentrancy, network grace, Android entry animation / keep-alive / low-RAM / cookies, iOS theme query / sizing / `window.open`). Upstream SDK fixes; no Unity wrapper API change.


## [2.2.4] - 2026-06-24

### Changed

- **Embedded Stash Native SDK [2.2.4](https://github.com/stashgg/stash-native/releases/tag/2.2.4)** (`StashNative-2.2.4.aar`, `StashNative.xcframework`). No wrapper API changes.

### Added

- **Web SDK**: the checkout page can now call `window.stash_sdk.onProcessingCompleted()` to reverse `onPurchaseProcessing()` — re-enabling card dismissal (swipe, backdrop / overlay tap, back button, and `window.close()`) and fading the drag handle back in — when a purchase that called `onPurchaseProcessing()` finishes or is cancelled without auto-closing the card. After it runs, `IsPurchaseProcessing` reads `false` again. Handled inside the embedded SDK; no Unity wrapper API change (no-op when no processing state is active).

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
