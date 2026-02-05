# Stash for Unity ![buildtest](https://github.com/stashgg/stash-unity/actions/workflows/main.yml/badge.svg)

<p align="left">
  <img src="https://github.com/stashgg/stash-native/raw/main/.github/assets/stash_unity.png" width="128" height="128" alt="Stash Unity Logo"/>
</p>

Unity package wrapper for [stash-native](https://github.com/stashgg/stash-native) for integrating Stash services and in-app Stash Pay checkout and webshop. 

## Requirements

- Unity 2019.4+ (LTS recommended)
- iOS 12.0+ / Android API 21+

## Package Components

All components are optional. Mix and match based on your needs.

| Component | Description |
|-----------|-------------|
| **Stash.Popup | Stash-native wrapper. |
| **Stash.DemoApp** | Stash Demo app and playground for testing. **(No need to import, demo app only)** |

## Sample Apps

You can build **Stash.DemoApp** in Unity to test Stash flows in your own project, or try them right away in your browser using the Appetize online emulator:

- **iOS:** [Open in Appetize.io](https://appetize.io/app/b_eyszozcrmyt2zifoh5bjyiifha)
- **Android:** [Open in Appetize.io](https://appetize.io/app/b_e7zfxgltohxm2rd5aw4zplzmwq?device=pixel7&osVersion=13.0&toolbar=true)

## Downloads

### Import package manually

1. Download the [latest release](https://github.com/stashgg/stash-unity/releases) or repository as a zip file.
2. Import the `.unitypackage` file into your Unity project
3. Select the components you need (Stash.Popup for Stash Pay)

### Import via Git URL

1. Open **Window > Package Manager**
2. Click **+** → **Add package from git URL**
3. Enter: `https://github.com/stashgg/stash-unity.git?path=Assets`

## Quick Start

###  Plugin Folder Structure

#### ./Editor
- **`StashPopupAndroidPostProcess.cs`** - Adds permissions for the Stash Pay AAR (e.g. foreground service). The AAR declares its own components via manifest merge.
- **`StashPayCardEditor/`** - Editor window for testing checkout and modal in the Unity Editor (Windows and macOS).

#### ./Plugins
- **`Plugins/Android/`** - `StashPay-1.2.4.aar` (native SDK) and `StashPayCardUnityBridge.java` (Unity bridge).
- **`Plugins/iOS/`** - `StashPayCardBridge.mm` (Unity bridge). Place `StashPay.xcframework` here before building for iOS so the bridge links to the SDK; ensure the framework is set to **Embed & Sign** in the generated Xcode project (see [Troubleshooting](#ios-app-crashes-with-library-not-loaded-related-to-stashpay)).

#### ./Scripts
- **`StashPayCard.cs`** - Singleton API: `OpenCheckout`, `OpenModal`, `OpenPopup`, configuration properties, and events.

#### ./Sample
- **`StashPaySample.cs`** / **`StashPaySample.unity`** - Simple demo: Open Checkout, Open Modal, config toggles, Force Web Checkout, and callback status.


###  Usage

Before using Stash.Popup, make sure your game server is set up to create Stash Pay checkout URLs using the Stash API. If you haven't already set up checkout URL generation, see our [integration guide](https://docs.stash.gg/guides/stash-pay/integration) for instructions.


### Using Drawer Dialog

Use `OpenCheckout()` to display a Stash Pay URL in a native card dialog inside your game:

```csharp
using StashPopup;

public class MyStore : MonoBehaviour
{
    void PurchaseItem(string checkoutUrl)
    {
        // checkoutUrl is a Stash Pay URL generated on your game backend.
        // OpenCheckout offers three different callbacks.
        StashPayCard.Instance.OpenCheckout(
            checkoutUrl,
            dismissCallback: OnCheckoutDismissed,
            successCallback: OnPaymentSuccess,
            failureCallback: OnPaymentFailure
        );
    }
    
    void OnCheckoutDismissed()
    {
        // User closed the dialog without finishing the purchase flow.
        // This also fires if browser mode is enabled and user closed the browser and returned to the game.
        VerifyPurchaseStatus();
    }
    
    void OnPaymentSuccess()
    {
        // Payment completed inside in-app dialog - verify on backend before granting items.
        // Note: This callback is only available for in-app dialog, does not fire in the browser mode.
        VerifyPurchaseStatus();
    }
    
    void OnPaymentFailure()
    {
        // Payment failed inside in-app dialog - show error to user.
        // Note: This callback is only available for in-app dialog, does not fire in the browser mode.
        ShowErrorMessage("Payment could not be processed");
    }
}
```

> **iOS Development Note:**  
> The first Stash checkout call may be slow when running under the Xcode debugger (especially if connected wirelessly), due to `WKWebView` processes being heavily instrumented by Xcode. This delay only affects debug sessions on the first call, not production builds.

Check the ./Sample folder for more detailed implementation details.


## Full API Reference

All public API lives on the **`StashPayCard`** singleton. Access it via **`StashPayCard.Instance`**.

---

### Singleton

| Member | Description |
|--------|-------------|
| **`StashPayCard Instance`** | Static read-only. The single `StashPayCard` instance. Created on first access and persisted across scenes (`DontDestroyOnLoad`). |

---

### Methods

| Signature | Description |
|-----------|-------------|
| **`void OpenCheckout(string url, Action dismissCallback = null, Action successCallback = null, Action failureCallback = null)`** | Opens checkout using **current instance properties** (ForcePortraitOnCheckout, card/tablet ratios). Use when you want one global checkout look for the whole app. |
| **`void OpenCheckout(string url, Action dismissCallback, Action successCallback, Action failureCallback, StashPayCheckoutConfig? config)`** | Opens checkout with **per-call config**. When `config` is set, that config is used for this open only; previous instance state is restored when the dialog is dismissed. Use for different sizes per flow (e.g. webshop vs. IAP) without mutating global state. |
| **`void OpenModal(string url, Action dismissCallback = null, Action successCallback = null, Action failureCallback = null, StashPayModalConfig? config = null)`** | Opens a URL in a centered modal (e.g. opt-in / channel selection). Optional `config` controls drag bar, allow dismiss, and phone/tablet size ratios. If `config` is null, platform defaults are used. |
| **`void ResetPresentationState()`** | Dismisses any currently presented checkout card or modal and resets internal state. Effect only on iOS and Android; no-op in Editor. |
| **`void DismissSafariViewController()`** | **iOS only.** Dismisses the current SFSafariViewController and invokes `OnSafariViewDismissed`. No success/failure callbacks. Use for browser (not in-app) checkouts when the user returns via deeplink and you only need to close the browser. |
| **`void DismissSafariViewController(bool success)`** | **iOS only.** Dismisses the current SFSafariViewController and invokes `OnPaymentSuccess` (if `success` is true) or `OnPaymentFailure` (if false). Use when handling deeplinks (`stash/purchaseSuccess` or `stash/purchaseFailure`). |

---

### Events

Subscribe to these on `StashPayCard.Instance` to react to user actions and lifecycle.

| Event | Signature | When it fires |
|-------|------------|----------------|
| **`OnSafariViewDismissed`** | `event Action` | Checkout or modal was dismissed by the user (closed without completing, or after completing in browser mode). |
| **`OnPaymentSuccess`** | `event Action` | User completed a payment successfully inside the in-app dialog. Not fired in web-based checkout; use deeplinks. |
| **`OnPaymentFailure`** | `event Action` | Payment failed inside the in-app dialog. Not fired in web-based checkout; use deeplinks. |
| **`OnOptinResponse`** | `event Action<string>` | Opt-in / channel selection response (e.g. `"NATIVE_IAP"` or `"STASH_PAY"`). |
| **`OnPageLoaded`** | `event Action<double>` | Page finished loading. Argument is load time in milliseconds. |
| **`OnNetworkError`** | `event Action` | Initial page load failed (no connection, 4xx/5xx, timeout). Dialog is auto-dismissed; `OnSafariViewDismissed` is not called. |
| **`OnNativeException`** | `event Action<string, Exception>` | Unhandled exception during a native plugin call. First argument: operation name (e.g. `"OpenCheckout"`, `"OpenModal"`). Second: the exception. **Android:** Java exceptions trigger this. **iOS:** Only exceptions during P/Invoke marshalling; native crashes are not catchable. |

---

### Types

#### `StashPayCheckoutConfig` (struct)

Used with **`OpenCheckout(url, dismiss, success, failure, config)`** for per-call checkout configuration. When provided, the config is applied for that call only; instance properties are restored when the dialog is dismissed.

| Field | Type | Default (in `Default`) | Description |
|-------|------|------------------------|-------------|
| **`forcePortraitOnCheckout`** | `bool` | `false` | Portrait-locked on phone when true. |
| **`cardHeightRatioPortrait`** | `float` | `0.68f` | Card height ratio in portrait (0.1–1.0). |
| **`cardWidthRatioLandscape`** | `float` | `0.9f` | Card width ratio in landscape (0.1–1.0). |
| **`cardHeightRatioLandscape`** | `float` | `0.6f` | Card height ratio in landscape (0.1–1.0). |
| **`tabletWidthRatioPortrait`** | `float` | `0.4f` | Tablet width in portrait (0.1–1.0). |
| **`tabletHeightRatioPortrait`** | `float` | `0.5f` | Tablet height in portrait (0.1–1.0). |
| **`tabletWidthRatioLandscape`** | `float` | `0.3f` | Tablet width in landscape (0.1–1.0). |
| **`tabletHeightRatioLandscape`** | `float` | `0.6f` | Tablet height in landscape (0.1–1.0). |

**Static:** **`StashPayCheckoutConfig.Default`** – Returns a struct with the defaults above. Copy and override fields as needed.

#### `StashPayModalConfig` (struct)

Used with **`OpenModal`** to control modal appearance and size. All values are ratios in the range 0.1–1.0 unless noted.

| Field | Type | Default (in `Default`) | Description |
|-------|------|------------------------|-------------|
| **`showDragBar`** | `bool` | `true` | Whether to show the drag bar. |
| **`allowDismiss`** | `bool` | `true` | Whether the user can dismiss the modal. |
| **`phoneWidthRatioPortrait`** | `float` | `0.8f` | Phone modal width (portrait). |
| **`phoneHeightRatioPortrait`** | `float` | `0.5f` | Phone modal height (portrait). |
| **`phoneWidthRatioLandscape`** | `float` | `0.5f` | Phone modal width (landscape). |
| **`phoneHeightRatioLandscape`** | `float` | `0.8f` | Phone modal height (landscape). |
| **`tabletWidthRatioPortrait`** | `float` | `0.4f` | Tablet modal width (portrait). |
| **`tabletHeightRatioPortrait`** | `float` | `0.3f` | Tablet modal height (portrait). |
| **`tabletWidthRatioLandscape`** | `float` | `0.3f` | Tablet modal width (landscape). |
| **`tabletHeightRatioLandscape`** | `float` | `0.4f` | Tablet modal height (landscape). |

**Static:** **`StashPayModalConfig.Default`** – Returns a struct with the defaults above. Copy and override fields as needed.


## Unity Editor Simulator 

<div align="center">
  <img src="https://github.com/stashgg/stash-unity/blob/main/.github/popup_editor.png?raw=true" width="800px" /><br/>
  <em>Unity Editor Simulation</em>
</div>
</br>
</br>

Package includes a Unity editor extension that allows you to test Stash Pay checkout dialogs directly in the Unity Editor without building to a device.

When you call `OpenCheckout()` in the Editor, the extension automatically intercepts these calls and displays the flow in an "emulator" window within Unity editor. This enables you to interact with the Stash Pay UI, complete purchases, and verify callback events. You can finish both test and production purchases.

> **Note:** Currently **Windows** and **macOS** versions of Unity are supported for editor simulator. Linux versions of editor are not supported.


## Troubleshooting

### [iOS] Build Error in Xcode: Undefined symbol

While highly unlikely, however if this happens add frameworks in Unity Project Settings → iOS → Other Settings → Linked Frameworks:
- `WebKit.framework`
- `SafariServices.framework`

Clean and rebuild Xcode project.

### [iOS] App crashes with "Library not loaded related to StashPay"

The app is linked with StashPay, but the framework has not been embedded in the app bundle.
In the Unity Editor, select `StashPay.xcframework` file and make sure "Add to embedded binaries" is enabled in Inspector panel.

Or fix it in Xcode project:
1. Open the Unity-generated Xcode project (e.g. after **File → Build Settings → iOS → Build**).
2. Select the **Unity-iPhone** (main app) target in the project navigator.
3. Open the **General** tab and scroll to **Frameworks, Libraries, and Embedded Content**.
4. If **StashPay.framework** is missing, click **+** and add it from the project (it should appear under Frameworks or Plugins/iOS). If it is already listed, set it to **Embed & Sign**.

Ensure `StashPay.xcframework` is present in `Assets/Stash.Popup/Plugins/iOS/` before building from Unity so the post-process can add it to the main target’s embed phase.

### [Android] Blank Checkout Card

Ensure internet permission in your AndroidManifest.xml.

### [Android] System browser used instead of in-app Chrome Custom Tabs

When using browser mode, some Unity projects launch Chrome Custom Tabs while others fall back to a system browser window. (This may be due to differences in Android dependencies between Unity versions.) While both flows are valid, Chrome Custom Tabs generally provide a superior experience. If you notice your app is not using Chrome Custom Tabs, you can resolve this by including the [AndroidX Browser library (`androidx.browser:browser`)](https://developer.android.com/jetpack/androidx/releases/browser), which supports [Android Custom Tabs](https://developer.android.com/develop/ui/views/layout/webapps/overview-of-android-custom-tabs).

1. **Enable Custom Gradle Template** in Unity:
   - Go to **Edit > Project Settings > Player**
   - Select **Android** tab
   - Scroll to **Publishing Settings**
   - Check **Custom Main Gradle Template**
   - Unity will create `Assets/Plugins/Android/mainTemplate.gradle`

2. **Add the dependency** to `Assets/Plugins/Android/mainTemplate.gradle`:
   - Open the file and find the `dependencies` block
   - Add: `implementation 'androidx.browser:browser:1.9.0'`

Example:
```gradle
dependencies {
    implementation fileTree(dir: 'libs', include: ['*.jar'])
    implementation 'androidx.browser:browser:1.9.0'
**DEPS**}
```

> **Note:** Stash Popup will automatically detect if Chrome Custom Tabs is available in the Android bundle and fall back gracefully to the default browser if not.



## Documentation

- [Stash Documentation](https://docs.stash.gg) - Full API reference and integration guides.

## Versioning

This package follows [Semantic Versioning](https://semver.org/) (major.minor.patch):

- **Major**: Breaking changes
- **Minor**: New features (backward compatible)
- **Patch**: Bug fixes

## Support

- Documentation: https://docs.stash.gg
- Email: developers@stash.gg
