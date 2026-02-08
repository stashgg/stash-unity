# Stash for Unity [![Lint](https://github.com/stashgg/stash-unity/actions/workflows/lint.yml/badge.svg)](https://github.com/stashgg/stash-unity/actions/workflows/lint.yml) ![buildtest](https://github.com/stashgg/stash-unity/actions/workflows/main.yml/badge.svg) 

<p align="left">
  <img src="https://github.com/stashgg/stash-native/raw/main/.github/assets/stash_unity.png" width="128" height="128" alt="Stash Unity Logo"/>
</p>

Unity package wrapper for [stash-native](https://github.com/stashgg/stash-native), enabling native-feeling Stash Pay IAP checkout and webshop presentation directly inside your Unity game (Android/iOS).

## Requirements

- Unity 2019.4+ (LTS recommended)
- iOS 12.0+ / Android API 21+

## Sample Scene

Try the sample scene in the **./Sample** folder, or try our demo in the Appetize online simulator:

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

# Quick Start

##  Folder Structure

#### ./Plugins
- **`Plugins/Android/`** - Contains the StashNative AAR and Unity bridge for Android.
- **`Plugins/iOS/`** - Contains the Unity bridge and native framework for iOS.

#### ./Scripts
- **`StashNative.cs`** - Singleton API that wraps and provides calls to the native iOS and Android libraries: use `OpenCard`, `OpenModal`, `OpenBrowser`, `CloseBrowser`, and listen to events for integration.

#### ./Editor
- **`StashEditorPlugin/`** - **(Optional)** Editor window for testing Stash card and modal flows directly in the Unity Editor (Windows and macOS). Lets you simulate UI, trigger events, and test callback handling within the editor without building. The editor plugin is under active development and may not be fully stable.

#### ./Sample
- **`StashSample.cs`** / **`StashSample.unity`** - **(Optional)** Reference implementation and demo scene: Open Card, Open Modal, Open Browser, and callback status.


##  Usage

Stash-native is used to handle Stash links - either Stash Pay links for IAP purchases or pre-authenticated links for embedded webshops. These links should always be generated securely on your backend server to ensure proper authentication and permissions.

Before you start integrating the library, we strongly recommend reading our [integration guide](https://docs.stash.gg/guides/stash-pay/integration) for best practices and detailed instructions on generating checkout URLs and Stash links correctly.

> **iOS Development Note:**
> The first OpenCard or OpenModal call may be slow when running under the Xcode debugger (especially if connected wirelessly), due to `WKWebView` processes being heavily instrumented by Xcode. This delay only affects debug sessions on the first call, not production builds.


### OpenCard()

Drawer-style card: slides up from the bottom on phones, centered on tablets. Suited for Stash Pay payment links, pre-authenticated webshop URLs or payment opt-in dialogs.

```csharp
using Stash.Native;

public class MyStore : MonoBehaviour
{
    void PurchaseItem(string checkoutUrl)
    {
        StashNative.Instance.OpenCard(
            STASH_URL_TO_OPEN,
            dismissCallback: OnDialogDismissed,
            successCallback: OnPaymentSuccess,
            failureCallback: OnPaymentFailure
        );
    }

    void OnDialogDismissed() => VerifyPurchaseStatus();
    void OnPaymentSuccess() => VerifyPurchaseStatus();
    void OnPaymentFailure() => ShowErrorMessage("Payment could not be processed");
}
```

All callbacks and config are optional; you can pass only the ones you need or use the global events instead. See the API reference below for more details about configuration options.


### OpenModal()

Centered modal dialog on all devices. Same layout on phone and tablet; resizes on rotation. Suited for channel selection or as an alternative checkout style.

```csharp
using Stash.Native;

StashNative.Instance.OpenModal(
    STASH_URL_TO_OPEN,
    dismissCallback: () => RefreshShopState(),
    successCallback: OnPurchaseComplete,
    failureCallback: OnPurchaseFailed
);
// All callbacks and config are optional
```

All callbacks and config are optional; you can pass only the ones you need or use the global events instead. See the API reference below for more details about configuration options.


### OpenBrowser()

Opens the URL in the platform browser (Chrome Custom Tabs on Android, SFSafariViewController on iOS). Use when you need an alternative lightweight, system-native browser view.

On iOS, `CloseBrowser()` dismisses the Safari view when your app regains focus; on Android it is a no-op as Chrome Custom tabs cant be dismissed by the app.

```csharp
StashNative.Instance.OpenBrowser(STASH_URL_TO_OPEN);
// Later, on iOS only:
StashNative.Instance.CloseBrowser();
```

---

## Full API Reference

All public API lives on the **`StashNative`** singleton. Access it via **`StashNative.Instance`**.

---

### Singleton

| Member | Description |
|--------|-------------|
| **`StashNative Instance`** | Static read-only. The single instance. Created on first access and persisted across scenes (`DontDestroyOnLoad`). |

---

### Methods

| Signature | Description |
|-----------|-------------|
| **`void OpenCard(string url, Action dismissCallback, Action successCallback, Action failureCallback, StashNativeCardConfig config)`** | Opens the URL in the native card (drawer). All callbacks and config are optional. |
| **`void OpenModal(string url, Action dismissCallback, Action successCallback, Action failureCallback, StashNativeModalConfig config)`** | Opens the URL in a centered modal. All callbacks and config are optional. |
| **`void OpenBrowser(string url)`** | Opens the URL in the platform browser (Chrome Custom Tabs on Android, SFSafariViewController on iOS). |
| **`void CloseBrowser()`** | **iOS Only:** Dismisses the Safari view programatically. |
| **`void Dismiss()`** | Dismisses the current card or modal. |
| **`bool IsCurrentlyPresented`** | True if a card or modal is currently visible. |
| **`bool IsPurchaseProcessing`** | True when a purchase is in progress and the dialog cannot be dismissed manually. |

---

### Global Events

Subscribe on `StashNative.Instance`.

| Event | When it fires |
|-------|----------------|
| **`OnDialogDismissed`** | Card or modal was dismissed by the user. |
| **`OnPaymentSuccess`** | Payment completed successfully in the in-app UI. |
| **`OnPaymentFailure`** | Payment failed in the in-app UI. |
| **`OnOptinResponse`** | Opt-in / channel selection response (e.g. `"stash_pay"`, `"native_iap"`). |
| **`OnPageLoaded`** | Page finished loading (argument: load time in ms). |
| **`OnNetworkError`** | Page load failed (no connection, HTTP error, timeout). |
| **`OnNativeException`** | Exception during a native call (operation name, exception, missing library). |

**Callbacks / events:** You can pass **per-call callbacks** to `OpenCard` / `OpenModal` (dismiss, success, failure) and/or subscribe to the **events** above. Per-call callbacks are ideal for handling the result of a specific open (e.g. refresh inventory after this purchase via Stash Pay/Webshop). Events are ideal for global listeners (e.g. analytics, logging, opt-in dialogs) that should run for every card/modal result. Both are invoked when a result occurs.

---

### Config types

#### `StashNativeCardConfig` (struct)

Optional per-call config for **`OpenCard`**. **`StashNativeCardConfig.Default`** for defaults.

| Field | Default | Description |
|-------|---------|-------------|
| **`forcePortrait`** | `false` | Portrait-locked on phone when true. |
| **`cardHeightRatioPortrait`** | `0.68f` | Card height ratio portrait (0.1–1.0). |
| **`cardWidthRatioLandscape`** | `0.9f` | Card width ratio landscape. |
| **`cardHeightRatioLandscape`** | `0.6f` | Card height ratio landscape. |
| **`tabletWidthRatioPortrait`** | `0.4f` | Tablet width portrait. |
| **`tabletHeightRatioPortrait`** | `0.5f` | Tablet height portrait. |
| **`tabletWidthRatioLandscape`** | `0.3f` | Tablet width landscape. |
| **`tabletHeightRatioLandscape`** | `0.6f` | Tablet height landscape. |

#### `StashNativeModalConfig` (struct)

Optional per-call config for **`OpenModal`**. **`StashNativeModalConfig.Default`** for defaults.

| Field | Default | Description |
|-------|---------|-------------|
| **`showDragBar`** | `true` | Show drag bar. |
| **`allowDismiss`** | `true` | User can dismiss. |
| **`phoneWidthRatioPortrait`** … **`tabletHeightRatioLandscape`** | (see struct) | Size ratios 0.1–1.0. |


## Unity Editor Simulator

<div align="center">
  <img src="https://github.com/stashgg/stash-unity/blob/main/.github/popup_editor.png?raw=true" width="800px" /><br/>
  <em>Unity Editor Simulation</em>
</div>
</br>
</br>

Package includes a Unity editor extension that allows you to test Stash URLs directly in the Unity Editor without building to a device.

When you call `OpenCard()` or `OpenModal()` in the Editor, the extension intercepts and displays the flow in a preview window. You can interact with the UI and verify callback events. `OpenBrowser()` is not simulated; it opens the system browser.

> **Note:** Currently **Windows** and **macOS** versions of Unity are supported for editor simulator. Linux versions of editor are not supported.


## Troubleshooting

### [iOS] Build Error in Xcode: Undefined symbol

While highly unlikely, however if this happens add frameworks in Unity Project Settings → iOS → Other Settings → Linked Frameworks:
- `WebKit.framework`
- `SafariServices.framework`

Clean and rebuild Xcode project.

### [iOS] App crashes with "Library not loaded related to StashNative"

The app is linked with StashNative, but the framework has not been embedded in the app bundle.
In the Unity Editor, select `StashNative.xcframework` file and make sure "Add to embedded binaries" is enabled in Inspector panel.

Or fix it in Xcode project:
1. Open the Unity-generated Xcode project (e.g. after **File → Build Settings → iOS → Build**).
2. Select the **Unity-iPhone** (main app) target in the project navigator.
3. Open the **General** tab and scroll to **Frameworks, Libraries, and Embedded Content**.
4. If **StashNative.framework** is missing, click **+** and add it from the project (it should appear under Frameworks or Plugins/iOS). If it is already listed, set it to **Embed & Sign**.

Ensure `StashNative.xcframework` is present in `Assets/Stash.Popup/Plugins/iOS/` before building from Unity so the post-process can add it to the main target’s embed phase.

### [Android] Bridge does not compile

The Unity bridge expects the StashNative AAR to expose `StashNative` and related classes. If your AAR uses a different Java package than `com.stash.stashnative`, update the fully qualified class names in `StashNativeCardUnityBridge.java` to match the AAR.

### [Android] Blank card

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
