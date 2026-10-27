# Stash for Unity [![Lint](https://github.com/stashgg/stash-unity/actions/workflows/lint.yml/badge.svg)](https://github.com/stashgg/stash-unity/actions/workflows/lint.yml) ![buildtest](https://github.com/stashgg/stash-unity/actions/workflows/main.yml/badge.svg)

<p align="left">
  <img src="https://github.com/stashgg/stash-native/raw/main/docs/assets/images/stash_unity_rounded.png" width="128" height="128" alt="Stash for Unity"/>
</p>

Unity wrapper for the [Stash Native SDK](https://stashgg.github.io/stash-native/) 3.0.0: Stash Pay checkout, pre-authenticated webshops and QR code account linking inside your Unity game on Android and iOS.

This README covers what is specific to Unity: installing the packages, the C# API, and Editor simulation. How the card, browser, scanner and callbacks behave is documented once, for every platform, in the native docs:

- [Checkout Card](https://stashgg.github.io/stash-native/usage/checkout-card.html)
- [In-Game Browser](https://stashgg.github.io/stash-native/usage/browser.html)
- [QR Code Linking](https://stashgg.github.io/stash-native/usage/codelink.html)
- [Callbacks](https://stashgg.github.io/stash-native/usage/callbacks.html)
- [Engine wrappers](https://stashgg.github.io/stash-native/architecture/engine-wrappers.html)

> **3.0.0 is a preview release** tracking the Stash Native 3.0.0 preview. It is a breaking update from 2.x; see [Migrating from 2.x](#migrating-from-2x).

## Requirements

- Unity 2022.3 LTS or newer
- iOS 15.0+
- Android API 21+, compileSdk 34+

## Installation

| Package | Git URL (Package Manager → **+** → *Add package from git URL*) |
|---------|------------------------------------------------------------------|
| **`gg.stash.unity`** (required) | `https://github.com/stashgg/stash-unity.git?path=Packages/gg.stash.unity` |
| **`gg.stash.unity.codelink`** (optional, QR scanner) | `https://github.com/stashgg/stash-unity.git?path=Packages/gg.stash.unity.codelink` |

The QR scanner package has its own camera setup and Android dependencies; see its [README](Packages/gg.stash.unity.codelink/README.md).

### Android dependencies

Stash Native needs AndroidX libraries Unity does not add by itself. Projects using the [External Dependency Manager](https://github.com/googlesamples/unity-jar-resolver) pick them up from the package's `Editor/StashNativeDependencies.xml`. Otherwise enable **Custom Main Gradle Template** (Player → Publishing Settings) and add to `dependencies { }` before `**DEPS**`:

```gradle
    implementation 'androidx.core:core:1.12.0'
    implementation 'androidx.webkit:webkit:1.11.0'
    implementation 'androidx.window:window:1.4.0'
    implementation 'androidx.window:window-java:1.4.0'
    implementation 'androidx.browser:browser:1.7.0'          // optional: Chrome Custom Tabs for OpenBrowser
```

The Unity bridge class is annotated `@Keep`; if your ProGuard setup strips annotations, add `-keep class com.stash.unity.** { *; }`.

### iOS

`StashNative.xcframework` is embedded by Unity's Xcode export; no setup needed.

### Samples

Package Manager → **Stash for Unity** → **Samples**:

- **QuickStart**: one button that opens a Stash checkout, with numbered comments on where your backend URL, options and purchase verification go. Start here.
- **Stash Test App**: every feature and option, mirroring the [native sample apps](https://stashgg.github.io/stash-native/usage/sample-apps.html). Add your App ID and ingress secret on the *Instances* tab to generate real links (test only; production links come from your backend).

## Usage

Links come from your backend ([integration guide](https://docs.stash.gg/guides/stash-pay/integration)). Everything goes through the `StashNative.Instance` singleton on the Unity main thread.

```csharp
using Stash.Native;

bool opened = StashNative.Instance.OpenCard(
    checkoutUrl,
    dismissCallback: () => RefreshShop(),
    successCallback: order => VerifyAndGrant(order),   // verify with your backend before granting
    failureCallback: () => ShowError(),
    config: new StashNativeCardConfig { orientationPreference = StashNativeOrientationPreference.Portrait },
    networkErrorCallback: () => RefreshShop(),
    externalPaymentCallback: url => { /* checkout continues in the browser; finish via deeplink */ });
```

Everything after the URL is optional. Exactly one per-call callback ends each card, and the matching global event fires too (see [Callbacks](https://stashgg.github.io/stash-native/usage/callbacks.html) for what each one means). `externalPaymentCallback` is informational: after a default handoff the card has closed and the presentation ends with `OnBrowserDismissed`; with the page's `keepOpen` the card stays open and ends with its own callback. `OpenCard` returns `false` and leaves the open card alone if a card or scanner is already presented.

```csharp
// QR code linking (needs gg.stash.unity.codelink)
if (StashNative.Instance.IsScannerAvailable)
    StashNative.Instance.OpenScanner(code => LinkAccount(code));

// In-game browser. When your payment deep link arrives:
StashNative.Instance.OpenBrowser(url);
StashNative.Instance.CloseBrowser();     // closes it and raises OnBrowserDismissed (both platforms)
StashNative.Instance.DismissBrowser();   // iOS: closes Safari silently; the game handles the result itself
```

### Unity-specific behaviour

- **Android pauses Unity while a card is on screen.** The checkout runs in its own activity, so `OnApplicationPause(true)` fires on every open and callbacks native raises mid-card (`OnPageLoaded`, `autoClose = false` results, non-fatal scanner errors, `OnExternalPayment`) arrive after the card closes. Keep `autoClose = true` if you need the result immediately.
- **Portrait checkout in a landscape game**: set `orientationPreference = Portrait`; Player Settings must allow portrait. On Android, `SetBackdropBytes(jpegBytes)` right before `OpenCard` shows a frame capture behind the card during the rotation (see `Assets/StashSample.cs`).
- **Receiver GameObject**: callbacks arrive on a GameObject named `StashNative`. The component creates it, or renames a scene-placed one to match.
- **Callback exceptions** are logged and reported on `OnNativeException`; other listeners still run.

## API

| Member | Notes |
|--------|-------|
| `bool OpenCard(url, dismiss, success, failure, StashNativeCardConfig, networkError, externalPayment)` | All but `url` optional. |
| `bool OpenScanner(scanned, dismissed, error, StashNativeScannerConfig)` / `OpenScanner(config)` | Needs `gg.stash.unity.codelink`. |
| `void OpenBrowser(url, StashNativeBrowserConfig)` | Config is iOS only (portrait Safari). |
| `void CloseBrowser()` / `void DismissBrowser()` | `CloseBrowser` raises `OnBrowserDismissed`. `DismissBrowser` closes Safari silently; iOS only. |
| `void Dismiss()` / `void ResetPresentationState()` | Reset drops any event still in flight. |
| `void SetKeepAliveEnabled(bool)` / `SetKeepAliveConfig(StashNativeKeepAliveConfig)` / `bool IsKeepAliveEnabled` | Android only. |
| `void SetBackdropBytes(byte[])` | Android only. |
| `void SetInspectableWebViewsEnabled(bool)` / `bool IsInspectableWebViewsEnabled` | Debug only. |
| `bool IsCurrentlyPresented` / `bool IsPurchaseProcessing` / `bool IsScannerAvailable` | `IsCurrentlyPresented` stays true while a browser is open over a card; a standalone Android browser does not count. |
| `string SdkVersion` / `string LastScannerErrorMessage` | |
| `StashNative InstanceOrNull` | Never creates the singleton. |

**Events**: `OnCardDismissed`, `OnPaymentSuccess(string order)`, `OnPaymentFailure`, `OnPageLoaded(double ms)`, `OnNetworkError`, `OnExternalPayment(string url)`, `OnBrowserDismissed`, `OnQrCodeScanned(string)`, `OnScannerDismissed`, `OnScannerError(StashNativeScannerError)`, `OnNativeException(string, Exception)`.

**Config types** are classes whose new instances carry the native defaults: `StashNativeCardConfig` (`preferredContentWidth` 400, `preferredContentHeight` 560, `maximumContentHeight` 720, `edgeMargin` 16, `allowDismiss`, `autoClose`, `orientationPreference`), `StashNativeBrowserConfig` (`orientationPreference`), `StashNativeScannerConfig` (header, callout and status texts, `showCallout`), `StashNativeKeepAliveConfig` (title, text, icon resource id). Field meaning is defined in the native docs for the [card](https://stashgg.github.io/stash-native/usage/checkout-card.html) and [scanner](https://stashgg.github.io/stash-native/usage/codelink.html).

## Unity Editor simulator

<div align="center">
  <img src="https://storage.googleapis.com/stash_sdk_demo/popup_editor.png" width="800px" /><br/>
  <em>Unity Editor Simulation</em>
</div>
</br>

In Play mode, `OpenCard()` opens the checkout in a preview window (macOS and Windows) with buttons to trigger payment and dismiss callbacks; `autoClose` and `allowDismiss` behave as on device. `OpenScanner()` opens a text field standing in for the camera. `OpenBrowser()` opens the system browser.

## Migrating from 2.x

Removed and renamed members carry `[Obsolete(error: true)]`, so the compiler points at every call site.

**Removed**

- `OpenModal` and `StashNativeModalConfig`. Use `OpenCard`; the card now adapts to tablets and large screens.
- `OnOptinResponse`. The checkout page handles channel selection itself.
- `backgroundColor`. The checkout themes itself via the `theme=` URL parameter.

**Renamed**

- `OnDialogDismissed` is now `OnCardDismissed`.
- `OnBrowserClosed` is now `OnBrowserDismissed`.
- `forcePortrait = true` is now `orientationPreference = StashNativeOrientationPreference.Portrait`.

**Changed**

- Card sizing uses points instead of screen ratios: `preferredContentWidth`, `preferredContentHeight`, `maximumContentHeight`, `edgeMargin`. The `cardHeightRatioPortrait` and `tablet*Ratio*` fields are gone.
- `OnCardDismissed` no longer fires after a payment auto-close, a network error or an external-payment handoff. If you cleaned up in the dismiss callback, pass `networkErrorCallback` to `OpenCard` and handle the success and failure callbacks too.
- `OpenCard` returns `bool`, `false` when a card or scanner is already open. Existing calls still compile.
- Config types are classes, so `new StashNativeCardConfig { ... }` starts from the native defaults.
- The Android backdrop is set with `SetBackdropBytes(bytes)` instead of calling the native class through JNI.

**Requirements**

- Unity 2022.3, iOS 15 and compileSdk 34 are now the minimums.
- The AndroidX libraries listed under [Installation](#android-dependencies) are required, not optional.
- The native bridges were renamed to `StashNativeUnityBridge.java` and `StashNativeBridge.mm`. Delete any 2.x copies in `Assets/`.

## More

- [TROUBLESHOOTING.md](TROUBLESHOOTING.md): build issues on both platforms.
- [Stash Native SDK docs](https://stashgg.github.io/stash-native/) and [Stash Documentation](https://docs.stash.gg).
- Support: developers@stash.gg
