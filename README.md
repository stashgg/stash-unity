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
| **`gg.stash.unity.codelink`** (optional, QR scanner; adds the camera permission) | `https://github.com/stashgg/stash-unity.git?path=Packages/gg.stash.unity.codelink` |

### Android dependencies

Stash Native needs AndroidX libraries Unity does not add by itself. Projects using the [External Dependency Manager](https://github.com/googlesamples/unity-jar-resolver) pick them up from the `Editor/*Dependencies.xml` files in both packages. Otherwise enable **Custom Main Gradle Template** (Player → Publishing Settings) and add to `dependencies { }` before `**DEPS**`:

```gradle
    implementation 'androidx.core:core:1.12.0'
    implementation 'androidx.webkit:webkit:1.11.0'
    implementation 'androidx.window:window:1.4.0'
    implementation 'androidx.window:window-java:1.4.0'
    implementation 'androidx.browser:browser:1.7.0'          // optional: Chrome Custom Tabs for OpenBrowser
    implementation 'androidx.camera:camera-camera2:1.3.4'    // the next four only with gg.stash.unity.codelink
    implementation 'androidx.camera:camera-lifecycle:1.3.4'
    implementation 'androidx.camera:camera-view:1.3.4'
    implementation 'com.google.zxing:core:3.5.3'
```

Unity 2022.3 ships Android Gradle Plugin 7.4, whose dexer cannot process CameraX 1.4.x. Pin CameraX so another plugin cannot pull it up:

```gradle
configurations.all {
    resolutionStrategy {
        force 'androidx.camera:camera-core:1.3.4'
        force 'androidx.camera:camera-camera2:1.3.4'
        force 'androidx.camera:camera-lifecycle:1.3.4'
        force 'androidx.camera:camera-view:1.3.4'
    }
}
```

Unity 6 (AGP 8.x) can use CameraX 1.4.2 without the pin. The Unity bridge class is annotated `@Keep`; if your ProGuard setup strips annotations, add `-keep class com.stash.unity.** { *; }`.

### iOS

With the CodeLink package installed, set **Camera Usage Description** in Player Settings. Both xcframeworks are embedded by Unity's Xcode export.

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

Everything after the URL is optional. Exactly one per-call callback ends each card, and the matching global event fires too (see [Callbacks](https://stashgg.github.io/stash-native/usage/callbacks.html) for what each one means). `OpenCard` returns `false` and leaves the open card alone if a card or scanner is already presented.

```csharp
// QR code linking (needs gg.stash.unity.codelink)
if (StashNative.Instance.IsScannerAvailable)
    StashNative.Instance.OpenScanner(code => LinkAccount(code));

// In-game browser; on iOS report the deeplink result to close Safari with a payment event
StashNative.Instance.OpenBrowser(url);
StashNative.Instance.DismissBrowser(paymentSucceeded: true);
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
| `void CloseBrowser()` / `void DismissBrowser(bool paymentSucceeded)` | iOS only; no-op on Android. |
| `void Dismiss()` / `void ResetPresentationState()` | Reset drops any event still in flight. |
| `void SetKeepAliveEnabled(bool)` / `SetKeepAliveConfig(StashNativeKeepAliveConfig)` / `bool IsKeepAliveEnabled` | Android only. |
| `void SetBackdropBytes(byte[])` | Android only. |
| `void SetInspectableWebViewsEnabled(bool)` / `bool IsInspectableWebViewsEnabled` | Debug only. |
| `bool IsCurrentlyPresented` / `bool IsPurchaseProcessing` / `bool IsScannerAvailable` | Android does not track Custom Tabs in `IsCurrentlyPresented`. |
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

Removed members carry `[Obsolete(error: true)]` so the compiler points at every call site.

| 2.x | 3.0 |
|-----|-----|
| `OpenModal`, `StashNativeModalConfig` | Removed; use `OpenCard`. |
| `forcePortrait` | `orientationPreference = Portrait` |
| Ratio fields, `backgroundColor` | Points-based `StashNativeCardConfig`; theming via the `theme=` URL parameter. |
| Config structs | Classes with native defaults. |
| `OpenCard` returns `void` | Returns `bool`; new `networkErrorCallback`, `externalPaymentCallback`. |
| `OnDialogDismissed` | `OnCardDismissed`; payment auto-close and network errors no longer emit a dismiss. |
| `OnBrowserClosed` | `OnBrowserDismissed` |
| `OnOptinResponse` | Removed. |
| Raw JNI `setBackdropBytes` | `SetBackdropBytes(bytes)` |
| iOS 13, Unity 2021.3, `androidx.core` recommended | iOS 15, Unity 2022.3, AndroidX set required. |

Bridges were renamed (`StashNativeUnityBridge.java`, `StashNativeBridge.mm`); delete any 2.x copies in `Assets/`.

## More

- [TROUBLESHOOTING.md](TROUBLESHOOTING.md): build issues on both platforms.
- [Stash Native SDK docs](https://stashgg.github.io/stash-native/) and [Stash Documentation](https://docs.stash.gg).
- Support: developers@stash.gg
