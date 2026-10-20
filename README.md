# Stash for Unity [![Lint](https://github.com/stashgg/stash-unity/actions/workflows/lint.yml/badge.svg)](https://github.com/stashgg/stash-unity/actions/workflows/lint.yml) ![buildtest](https://github.com/stashgg/stash-unity/actions/workflows/main.yml/badge.svg)

<p align="left">
  <img src="https://github.com/stashgg/stash-native/raw/main/docs/assets/images/stash_unity_rounded.png" width="128" height="128" alt="Stash for Unity"/>
</p>

Unity package wrapper for [stash-native](https://github.com/stashgg/stash-native) (embedded **Stash Native 3.0.0**). Native-feeling Stash Pay checkout, pre-authenticated webshop presentation, and QR code account linking inside your Unity game on Android and iOS.

> **3.0.0 is a preview release** tracking the Stash Native 3.0.0 preview. It is a breaking update from 2.x; see [Migrating from 2.x](#migrating-from-2x).

## Requirements

- Unity 2022.3 LTS or newer
- iOS 15.0+
- Android API 21+, compileSdk 34+ (required by the AndroidX libraries below)

## Installation (UPM)

Two packages live in this repository:

| Package | Purpose |
|---------|---------|
| **`gg.stash.unity`** | Core: checkout card, in-game browser, callbacks. Required. |
| **`gg.stash.unity.codelink`** | Optional: native QR scanner for webshop account linking (`OpenScanner`). Adds the camera permission; install only if you use it. |

### Add from Git URL (recommended)

1. Open **Window > Package Manager**
2. Click **+** → **Add package from git URL**
3. Enter `https://github.com/stashgg/stash-unity.git?path=Packages/gg.stash.unity`
4. For QR Code Linking, repeat with `https://github.com/stashgg/stash-unity.git?path=Packages/gg.stash.unity.codelink`

### Add via manifest.json

Add to `Packages/manifest.json` under `dependencies`:

```json
"gg.stash.unity": "https://github.com/stashgg/stash-unity.git?path=Packages/gg.stash.unity",
"gg.stash.unity.codelink": "https://github.com/stashgg/stash-unity.git?path=Packages/gg.stash.unity.codelink"
```

### Android: Gradle dependencies (required)

Stash Native 3.0.0 depends on AndroidX libraries that Unity does not add on its own. The package ships an EDM4U manifest (`Editor/StashNativeDependencies.xml`) for projects using the [External Dependency Manager](https://github.com/googlesamples/unity-jar-resolver). Without EDM4U, add them through a custom Gradle template:

1. **Edit → Project Settings → Player → Android → Publishing Settings**
2. Enable **Custom Main Gradle Template** (Unity creates or uses `Assets/Plugins/Android/mainTemplate.gradle`).
3. In the `dependencies { }` block, **before** the `**DEPS**` line Unity injects, add:

```gradle
    // Required by Stash Native 3.0.0
    implementation 'androidx.core:core:1.12.0'
    implementation 'androidx.webkit:webkit:1.11.0'
    implementation 'androidx.window:window:1.4.0'
    implementation 'androidx.window:window-java:1.4.0'
    // Optional: Chrome Custom Tabs for OpenBrowser (otherwise the system browser opens)
    implementation 'androidx.browser:browser:1.7.0'
    // Only with gg.stash.unity.codelink (QR Code Linking)
    implementation 'androidx.camera:camera-camera2:1.3.4'
    implementation 'androidx.camera:camera-lifecycle:1.3.4'
    implementation 'androidx.camera:camera-view:1.3.4'
    implementation 'com.google.zxing:core:3.5.3'
```

4. Pin the versions so other plugins cannot pull an older `androidx.core` or a newer CameraX. Unity 2022.3 ships Android Gradle Plugin 7.4, whose dexer cannot process CameraX 1.4.x, and a plain `implementation` line is only a request that a higher version from another plugin overrides:

```gradle
configurations.all {
    resolutionStrategy {
        force 'androidx.core:core:1.12.0'
        force 'androidx.camera:camera-core:1.3.4'
        force 'androidx.camera:camera-camera2:1.3.4'
        force 'androidx.camera:camera-lifecycle:1.3.4'
        force 'androidx.camera:camera-view:1.3.4'
    }
}
```

Unity 6 projects (AGP 8.x) can use CameraX 1.4.2 and drop the CameraX pins.

**Minify / R8.** The Unity bridge class `com.stash.unity.StashNativeUnityBridge` is reached by name from C#. It carries `@androidx.annotation.Keep`, so R8 keeps it; if your ProGuard setup strips annotations, add `-keep class com.stash.unity.** { *; }`.

### iOS: Camera usage description (QR Code Linking only)

With `gg.stash.unity.codelink` installed, set **Camera Usage Description** in **Project Settings → Player → iOS → Other Settings**. Unity writes it to `NSCameraUsageDescription`; without it the scanner reports `MissingCameraUsageDescription`. The core package has no camera code and needs no description.

Both `StashNative.xcframework` and `StashNativeCodeLink.xcframework` are marked *Add to Embedded Binaries* by their packages; Unity's Xcode export embeds and signs them.

### Import a sample (optional)

Two samples ship with the core package, under **Package Manager → Stash for Unity → Samples**:

- **QuickStart**: one script, one uGUI button, the Stash test cart. Numbered comments walk through where your backend URL, card options and purchase verification go. Start here.
- **Stash Test App**: the full-featured test harness described below.

#### Stash Test App

The sample mirrors the stash-native sample apps: bottom tabs **Test** (checkout card, browser, QR scan), **Settings** (card, browser and scanner options, orientation lock, SDK version) and **Instances** (named App ID + ingress secret pairs, test or production). Every SDK callback appears as a timestamped chip. It is two scripts, a UI Toolkit layout and a near-empty scene; nothing else to install.

1. Open **Window > Package Manager**
2. Select **Stash for Unity**
3. Expand **Samples** and click **Import** next to **Stash Test App** (or **QuickStart**)
4. Open `StashTestApp.unity` (or `QuickStart.unity`) and press Play. Add an instance with your **App ID** and **ingress API secret** on the *Instances* tab (or pre-fill the `StashLinkGenerator` component) to generate real checkout and webshop links.

The sample signs requests on the device with `x-stash-hmac-signature`. That is for testing only; production links belong on your backend. Folders named `Samples~` are hidden from the Project window by design; **Import** copies them into `Assets/`.

## Sample / Demo

- **iOS:** [Open in Appetize.io](https://appetize.io/app/b_eyszozcrmyt2zifoh5bjyiifha)
- **Android:** [Open in Appetize.io](https://appetize.io/app/b_e7zfxgltohxm2rd5aw4zplzmwq?device=pixel7&osVersion=13.0&toolbar=true)

# Quick Start

## Package structure

When installed via UPM, the core package lives under `Packages/gg.stash.unity/`:

| Path | Description |
|------|-------------|
| **Runtime/** | **`StashNative.cs`** – Singleton API: `OpenCard`, `OpenScanner`, `OpenBrowser`, `DismissBrowser`, Android keep-alive and backdrop helpers, events. |
| **Editor/** | Editor window that simulates the card and scanner in the Unity Editor (Windows and macOS); EDM4U dependency manifest. |
| **Plugins/Android/** | `StashNative-3.0.0.aar` and the Unity bridge `StashNativeUnityBridge.java`. |
| **Plugins/iOS/** | `StashNative.xcframework` and the Unity bridge `StashNativeBridge.mm`. |
| **Samples~/QuickStart/** | QuickStart: `QuickStart.cs` and a one-object scene. |
| **Samples~/StashSample/** | Stash Test App: `StashSample.cs` (actions, callbacks, navigation, instances), `StashLinkGenerator.cs` (HMAC-signed link generation), `Resources/` (UXML, USS, PanelSettings, logo). |

`Packages/gg.stash.unity.codelink/` holds the `StashNativeCodeLink` binaries and their dependency manifest.

## Usage

Stash Native presents Stash links: Stash Pay links for IAP purchases, or pre-authenticated links for embedded webshops. Always generate these links on your backend so authentication and permissions stay server-side. Read the [integration guide](https://docs.stash.gg/guides/stash-pay/integration) first; the native SDK itself is documented at [stashgg.github.io/stash-native](https://stashgg.github.io/stash-native/).

All calls go through the `StashNative.Instance` singleton on the Unity main thread. One card, scanner or browser can be presented at a time.

> **iOS development note:** the first `OpenCard` can be slow under the Xcode debugger (especially wireless), because `WKWebView` processes are heavily instrumented. Debug sessions only.

### OpenCard()

Responsive checkout card for phones, tablets, foldables and split screen, built on the native iOS sheet and an equivalent Android presentation. It keeps its state through rotation, keyboard and window changes. The checkout page sizes the card itself; `StashNativeCardConfig` only sets preferred dimensions, a height cap, margins, dismissal and orientation.

```csharp
using Stash.Native;

public class MyStore : MonoBehaviour
{
    void PurchaseItem(string checkoutUrl)
    {
        // A new config carries the native defaults; set only what you need.
        var config = new StashNativeCardConfig
        {
            // Landscape-locked game? Present checkout in portrait on phones:
            // orientationPreference = StashNativeOrientationPreference.Portrait,
        };

        bool opened = StashNative.Instance.OpenCard(
            checkoutUrl,
            dismissCallback: OnCardDismissed,
            successCallback: OnPaymentSuccess,
            failureCallback: OnPaymentFailure,
            config: config,
            networkErrorCallback: OnCardDismissed,
            externalPaymentCallback: url => Debug.Log("Continuing in the browser: " + url));

        if (!opened) Debug.Log("A card or scanner is already open.");
    }

    void OnCardDismissed() => VerifyPurchaseStatus();
    void OnPaymentSuccess(string order) => VerifyPurchaseStatus();
    void OnPaymentFailure() => ShowErrorMessage("Payment could not be processed");
}
```

All callbacks and the config are optional. `OpenCard` returns `false`, and leaves the current session untouched, when a card or scanner is already presented; the native SDK ignores overlapping opens, so the wrapper refuses them instead of mixing up callbacks.

**How a card ends.** Exactly one of these happens per `OpenCard`:

| Ending | Per-call callback | Global event | Card already closed? |
|--------|-------------------|--------------|----------------------|
| User swipes/taps away, page calls `window.close()`, host calls `Dismiss()` | `dismissCallback` | `OnCardDismissed` | yes |
| Payment success, `autoClose = true` (default) | `successCallback` | `OnPaymentSuccess` | yes, so you can open the next card from the callback |
| Payment failure, `autoClose = true` | `failureCallback` | `OnPaymentFailure` | yes |
| Payment result, `autoClose = false` | `successCallback` / `failureCallback`, then `dismissCallback` when it closes | same | no |
| Network error | `networkErrorCallback` | `OnNetworkError` | yes |
| Checkout hands off to the external browser | `externalPaymentCallback` | `OnExternalPayment`, then `OnBrowserDismissed` | closing; do not open another card from this callback |

Every ending has a per-call callback, so "flow ended" cleanup (restore orientation, refresh inventory) can live in one place. If a callback throws, the exception is logged and reported on `OnNativeException`, and the remaining listeners still run.

> **Android: the game is paused while a card is on screen.** The checkout runs in its own activity, so Unity's script loop stops and `OnApplicationPause(true)` fires on every open. Callbacks that native raises while the card is up (`OnPageLoaded`, payment results with `autoClose = false`, non-fatal scanner errors, `OnExternalPayment`) are delivered in order once the card closes. Keep `autoClose = true` on Android if you need to react to the payment immediately.

### Portrait checkout in landscape games

Set **`orientationPreference = StashNativeOrientationPreference.Portrait`**. On phones the SDK presents portrait checkout in its own window and restores the game's orientation when the card closes; tablets ignore it. Unity Player Settings must allow portrait (`allowedAutorotateToPortrait`).

**Android backdrop.** While the OS switches orientation, the Unity surface behind the card can look black or distorted. Optionally capture the current frame and pass it to **`SetBackdropBytes`** right before **`OpenCard`**; it is shown full-screen behind the dim overlay and consumed by that open. See `Assets/StashSample.cs` for a full example.

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
yield return new WaitForEndOfFrame();
var snap = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
snap.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
snap.Apply();
StashNative.Instance.SetBackdropBytes(ImageConversion.EncodeToJPG(snap, 75));
Destroy(snap);
#endif
StashNative.Instance.OpenCard(url, /* ... */);
```

### OpenScanner()

QR Code Linking lets a player link their Stash webshop account by scanning the QR code the webshop shows. The scanner opens in the native card, confirms the scan, closes, and returns the raw payload to the game. Requires **`gg.stash.unity.codelink`**.

```csharp
if (!StashNative.Instance.IsScannerAvailable) { HideLinkButton(); return; }

var config = new StashNativeScannerConfig { headerText = "Scan the webshop code" }; // translate or hide copy as needed

StashNative.Instance.OpenScanner(
    scannedCallback: code => LinkAccount(code),  // send to your backend; see Account Linking in the Stash docs
    dismissCallback: () => Debug.Log("Scanner closed"),
    errorCallback: err => Debug.LogWarning("Scanner: " + err + " " + StashNative.Instance.LastScannerErrorMessage),
    config: config);
```

Most errors leave the scanner open so the user can grant camera access and retry. `ScannerUnavailable` is the exception: the module or its Android dependencies are missing, nothing stays presented, and no dismissal follows. `Dismiss()` closes the scanner. See the [QR Code Linking guide](https://stashgg.github.io/stash-native/usage/codelink.html).

### OpenBrowser()

Opens the URL in the platform browser (Chrome Custom Tabs on Android, SFSafariViewController on iOS). Use it when you want a lightweight, system-native view. There are no payment callbacks; the checkout returns through your deeplink.

```csharp
StashNative.Instance.OpenBrowser(url);

// iOS: portrait Safari sheet in a landscape game
StashNative.Instance.OpenBrowser(url, new StashNativeBrowserConfig { orientationPreference = StashNativeOrientationPreference.Portrait });

// iOS: your deeplink handler reports the outcome and closes Safari.
// The SDK then raises OnPaymentSuccess / OnPaymentFailure followed by OnBrowserDismissed.
StashNative.Instance.DismissBrowser(paymentSucceeded: true);

// iOS: close Safari without a result
StashNative.Instance.CloseBrowser();
```

**`OnBrowserDismissed`** fires when the browser closes, after `OpenBrowser()` and after an external payment handoff from `OpenCard()`. On Android the SDK owns the Custom Tabs result lifecycle through an invisible proxy activity merged from the AAR; no manifest edits are needed.

### Android keep-alive (optional)

Android may kill or suspend your game during external payments in Chrome Custom Tabs, especially on low-memory devices. Stash's optional keep-alive foreground service (off by default) starts when the browser opens and stops when the user returns. Android 13+ shows its notification only if the app holds `POST_NOTIFICATIONS`. See the [stash-native docs](https://stashgg.github.io/stash-native/usage/browser.html).

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
StashNative.Instance.SetKeepAliveEnabled(true);
StashNative.Instance.SetKeepAliveConfig(new StashNativeKeepAliveConfig
{
    notificationTitle = "Payment in progress",
    notificationText = "Tap to return to the app",
    notificationIconResId = 0  // 0 = library default icon; or your Android drawable resource id
});
#endif
StashNative.Instance.OpenBrowser(checkoutUrl);
```

---

## Full API Reference

All public API lives on the **`StashNative`** singleton.

### Singleton

| Member | Description |
|--------|-------------|
| **`StashNative Instance`** | Static read-only. Created on first access and persisted across scenes. The GameObject is always named `StashNative`; a scene-placed component is renamed and moved to the scene root to match. |
| **`StashNative InstanceOrNull`** | The instance if one exists, else null. Never creates one; safe from Editor code. |

### Methods

| Signature | Description |
|-----------|-------------|
| **`bool OpenCard(string url, Action dismissCallback, Action<string> successCallback, Action failureCallback, StashNativeCardConfig config, Action networkErrorCallback, Action<string> externalPaymentCallback)`** | Opens the URL in the native checkout card. Returns `false` if a card or scanner is already presented. `successCallback` receives the order payload (empty string if none). Everything after `url` is optional. |
| **`bool OpenScanner(Action<string> scannedCallback, Action dismissCallback, Action<StashNativeScannerError> errorCallback, StashNativeScannerConfig config)`** | Opens the QR scanner. Requires `gg.stash.unity.codelink`. Returns `false` if a session is already active. Also `OpenScanner(config)`. |
| **`void OpenBrowser(string url, StashNativeBrowserConfig config)`** | Opens the URL in the platform browser. `config` is iOS only. Falls back to the system browser without the native SDK. |
| **`void CloseBrowser()`** | **iOS only:** dismisses the Safari view without a payment result. No-op on Android. |
| **`void DismissBrowser(bool paymentSucceeded)`** | **iOS only:** closes Safari and reports the deeplink payment result; the SDK then raises the payment event and `OnBrowserDismissed`. |
| **`void Dismiss()`** | Dismisses the current card or scanner, even while purchase processing blocks user dismissal. iOS also closes an open Safari view. |
| **`void ResetPresentationState()`** | Tears down the native session without callbacks and drops any event still in flight from it. |
| **`void SetKeepAliveEnabled(bool enabled)`** | **Android only:** opt in to the keep-alive foreground service during external browser flows. |
| **`void SetKeepAliveConfig(StashNativeKeepAliveConfig config)`** | **Android only:** notification title, text, and optional icon resource id (`0` = library default). |
| **`void SetBackdropBytes(byte[] imageBytes)`** | **Android only:** JPEG/PNG shown behind a portrait card on a landscape host; consumed by the next open. `null` clears one set for an open that never happened. |
| **`void SetInspectableWebViewsEnabled(bool enabled)`** | **Debug/QA only:** makes checkout WebViews inspectable (Safari Web Inspector on iOS 16.4+, `chrome://inspect` on Android). Off by default; do **not** enable in production. Call before opening any checkout. |

### Properties

| Property | Description |
|----------|-------------|
| **`string SdkVersion`** | Version of the embedded native SDK (e.g. `"3.0.0"`). Empty in the Editor. |
| **`bool IsScannerAvailable`** | True when the CodeLink module (and, on Android, CameraX and ZXing) is in the build. True in the Editor. |
| **`bool IsCurrentlyPresented`** | True while a card or scanner is presented or an open is pending. iOS: also true while Safari is up. Android: Custom Tabs are not tracked. |
| **`bool IsPurchaseProcessing`** | True when a purchase is in progress and the card cannot be dismissed by the user. Not pollable on Android while the card is up. |
| **`bool IsKeepAliveEnabled`** | **Android only:** whether the keep-alive service is enabled. |
| **`bool IsInspectableWebViewsEnabled`** | True when checkout WebView inspection is enabled. |
| **`string LastScannerErrorMessage`** | Platform description of the most recent scanner error. |

### Global Events

Subscribe on `StashNative.Instance`. Per-call callbacks are ideal for the result of a specific open; events suit global listeners (analytics, logging) that run for every result. Both fire, per-call first.

| Event | When it fires |
|-------|----------------|
| **`OnCardDismissed`** | The card was closed by the user, the page, `Dismiss()`, or the host. Not after a payment auto-close, network error, or browser handoff. |
| **`OnPaymentSuccess`** | Payment completed. Argument: order string from the page, empty if none. With `autoClose` the card is already closed. |
| **`OnPaymentFailure`** | Payment failed. With `autoClose` the card is already closed. |
| **`OnPageLoaded`** | Checkout page finished loading (argument: load time in ms). Android: delivered after the card closes. |
| **`OnNetworkError`** | Checkout ended because of a network error (no connection, failed initial response, 15 s foreground deadline, or connection loss after load). The card is already closed. |
| **`OnExternalPayment`** | Checkout is handing off to an external URL (e.g. GPay, Klarna, crypto). The card is closing; do not open another card here. Finalize via deeplink. |
| **`OnBrowserDismissed`** | The external browser closed after `OpenBrowser` or an external payment handoff. |
| **`OnQrCodeScanned`** | The scanner read a QR code and closed. Argument: raw payload. |
| **`OnScannerDismissed`** | The scanner closed without a result. |
| **`OnScannerError`** | The scanner cannot use the camera. Argument: `StashNativeScannerError`; see `LastScannerErrorMessage`. |
| **`OnNativeException`** | Exception during a native call or inside one of your callbacks (operation name, exception). |

### Config types

All config types are classes whose new instances carry the native defaults, so object initialisers that set a single field are safe.

#### `StashNativeCardConfig`

Optional per-call config for **`OpenCard`**. Dimensions are points of web content, excluding safe areas.

| Field | Default | Description |
|-------|---------|-------------|
| **`preferredContentWidth`** | `400` | Preferred content width. |
| **`preferredContentHeight`** | `560` | Resting height ceiling; shorter page content shrinks the card. |
| **`maximumContentHeight`** | `720` | Height cap when the page expands. `0` uses all available height. |
| **`edgeMargin`** | `16` | Margin to the screen edges. |
| **`allowDismiss`** | `true` | User can dismiss (swipe, backdrop tap, back button). Page close, payment callbacks and `Dismiss()` are unaffected. |
| **`autoClose`** | `true` | When `false`, the card stays open after the payment callback until closed by the page, user, or host. |
| **`orientationPreference`** | `FollowHost` | `Portrait` presents portrait checkout on phones in landscape games. Ignored on tablets. |

#### `StashNativeBrowserConfig`

Optional config for **`OpenBrowser`** (iOS only).

| Field | Default | Description |
|-------|---------|-------------|
| **`orientationPreference`** | `FollowHost` | `Portrait` requests a portrait Safari sheet on iPhone. |

#### `StashNativeScannerConfig`

Optional config for **`OpenScanner`**. Null or empty strings keep the built-in wording.

| Field | Default |
|-------|---------|
| **`headerText`** | "Scan QR code" |
| **`calloutTitle`** | "Scan the webshop code" |
| **`calloutSubtitle`** | "Keep the QR code centered inside the frame." |
| **`cameraUnavailableText`** | "Camera unavailable" |
| **`connectedText`** | "Connected" |
| **`showCallout`** | `true`; `false` removes the callout and gives its space to the scan frame |

#### `StashNativeScannerError` (enum)

| Value | Cause | Fix |
|-------|-------|-----|
| `CameraPermissionDenied` | User denied camera access | None; the scanner offers a Settings button |
| `CameraUnavailable` | No camera | Offer another way to link |
| `CameraConfigurationFailed` | Camera setup failed | Retry later |
| `MissingCameraUsageDescription` | iOS: `NSCameraUsageDescription` missing | Set Camera Usage Description in Player Settings |
| `CameraPermissionNotDeclared` | Android: CAMERA permission stripped from the merged manifest | Keep the module's permission |
| `ScannerUnavailable` | `gg.stash.unity.codelink` not installed, or on Android its CameraX/ZXing libraries missing | Install the package and its Gradle dependencies; check `IsScannerAvailable` first |

#### `StashNativeKeepAliveConfig` (struct)

Used with **`SetKeepAliveConfig`** on **Android** only.

| Field | Description |
|-------|-------------|
| **`notificationTitle`** | Notification title. |
| **`notificationText`** | Notification body. |
| **`notificationIconResId`** | Android drawable resource id, or **`0`** for the library default icon. |

## Migrating from 2.x

Stash Native 3.0.0 redesigned its API; this wrapper follows. Removed members carry `[Obsolete(error: true)]` so the compiler points at every call site.

| 2.x | 3.0 |
|-----|-----|
| `OpenModal(...)`, `StashNativeModalConfig` | Removed. Use `OpenCard` with `StashNativeCardConfig`; the responsive card covers tablets and large screens. |
| `StashNativeCardConfig.forcePortrait` | `orientationPreference = StashNativeOrientationPreference.Portrait` |
| `cardHeightRatioPortrait`, `cardWidthRatioLandscape`, `tablet*Ratio*` | `preferredContentWidth`, `preferredContentHeight`, `maximumContentHeight`, `edgeMargin` (points, not ratios) |
| `backgroundColor` | Removed. The checkout themes itself via the `theme=` URL parameter. |
| Config structs, `.Default` required | Classes with native defaults; `new StashNativeCardConfig { ... }` is safe. `.Default` still works. |
| `OpenCard(...)` returns `void` | Returns `bool`; `false` when a session is already active. New optional `networkErrorCallback` and `externalPaymentCallback`. |
| `OnDialogDismissed` | `OnCardDismissed`. **Semantics changed:** payment auto-close and network errors no longer emit a dismiss. |
| `OnBrowserClosed` | `OnBrowserDismissed` |
| `OnOptinResponse` | Removed from the native SDK. |
| `OpenBrowser(url)` | Unchanged; optional `StashNativeBrowserConfig` added. `DismissBrowser(bool)` added for the iOS deeplink result. |
| Raw `AndroidJavaClass("com.stash.stashnative.StashNativeCard").setBackdropBytes` | `StashNative.Instance.SetBackdropBytes(bytes)` |
| iOS 13 minimum | iOS 15 minimum |
| Unity 2021.3 minimum | Unity 2022.3 minimum (compileSdk 34) |
| `androidx.core` recommended | `androidx.core`, `webkit`, `window`, `window-java` **required** (see Installation) |

The native bridges were renamed (`StashNativeUnityBridge.java`, `StashNativeBridge.mm`). If you copied them into `Assets/` in 2.x, delete those copies.

## Unity Editor Simulator

<div align="center">
  <img src="https://storage.googleapis.com/stash_sdk_demo/popup_editor.png" width="800px" /><br/>
  <em>Unity Editor Simulation</em>
</div>
</br>
</br>

The package includes an Editor extension so you can test Stash URLs without building to a device. In Play mode, `OpenCard()` opens the flow in a preview window where you can interact with the UI and trigger payment or dismiss callbacks; `autoClose` and `allowDismiss` behave as on device. `OpenScanner()` opens a text field standing in for the camera: paste a payload and press **Scan**. `Dismiss()`, `ResetPresentationState()` and `IsCurrentlyPresented` control and report the simulation. `OpenBrowser()` is not simulated; it opens the system browser.

> **Note:** Windows and macOS Editors are supported. Linux is not.

## Troubleshooting

See **[TROUBLESHOOTING.md](TROUBLESHOOTING.md)** for iOS/Android build issues (Xcode symbols, framework embedding, Gradle and CameraX, Minify, Custom Tabs, keep-alive, scanner, and related fixes).

## Documentation

- [Stash Documentation](https://docs.stash.gg) - Full API reference and integration guides.
- [Stash Native SDK docs](https://stashgg.github.io/stash-native/) - Native card, browser, QR Code Linking, callbacks.

## Versioning

This package follows [Semantic Versioning](https://semver.org/) (major.minor.patch) and tracks the embedded Stash Native version:

- **Major**: Breaking changes
- **Minor**: New features (backward compatible)
- **Patch**: Bug fixes

## Support

- Documentation: https://docs.stash.gg
- Email: developers@stash.gg
