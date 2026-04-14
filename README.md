# Stash for Unity [![Lint](https://github.com/stashgg/stash-unity/actions/workflows/lint.yml/badge.svg)](https://github.com/stashgg/stash-unity/actions/workflows/lint.yml) ![buildtest](https://github.com/stashgg/stash-unity/actions/workflows/main.yml/badge.svg)

<p align="left">
  <img src="https://github.com/stashgg/stash-native/raw/main/.github/assets/stash_unity.png" width="128" height="128" alt="Stash Unity Logo"/>
</p>

Unity package wrapper for [stash-native](https://github.com/stashgg/stash-native) (embedded **Stash Native 2.1.1**), enabling native-feeling Stash Pay IAP checkout and webshop presentation directly inside your Unity game (Android/iOS).

## Requirements

- Unity 2021.3+ (LTS recommended)
- iOS 13.0+ / Android API 21+

## Installation (UPM)

This package is distributed via the Unity Package Manager (UPM).

### Add from Git URL (recommended)

1. Open **Window > Package Manager**
2. Click **+** → **Add package from git URL**
3. Enter: `https://github.com/stashgg/stash-unity.git?path=Packages/gg.stash.unity`

### Add via manifest.json

Add to your project's `Packages/manifest.json` under `dependencies`:

```json
"gg.stash.unity": "https://github.com/stashgg/stash-unity.git?path=Packages/gg.stash.unity"
```

### Android: Optional Gradle dependencies

Use your package manager or custom project Gradle file to add the following dependencies.

| Dependency | Why add it |
|------------|------------|
| **`androidx.browser:browser`** (1.7.0 and up) | **`OpenBrowser()`** and some external browser flows use Chrome Custom Tabs when this is on the classpath; otherwise Android may open the plain system browser. |
| **`androidx.core:core`** (1.12.0 and up) — **recommended** | Aligns Jetpack with current Stash Native behavior (broadcast receiver registration, foreground service helpers, and other `androidx.core` APIs). Unity projects that still resolve **very old** `androidx.core` (e.g. 1.2.x from legacy EDM trees) can hit **`NoSuchMethodError`** or subtle incompatibilities with other plugins. Pinning **`androidx.core:core`** to **1.12.0+** is not required by this package but is **recommended** for the most reliable Android experience. |

**Steps (Custom Gradle Template):**

1. **Edit → Project Settings → Player → Android → Publishing Settings**
2. Enable **Custom Main Gradle Template** (Unity creates or uses `Assets/Plugins/Android/mainTemplate.gradle`).
3. Open `mainTemplate.gradle`, find the `dependencies { }` block, and **before** the `**DEPS**` line Unity injects, add:

```gradle
    implementation 'androidx.browser:browser:1.7.0'
    implementation 'androidx.core:core:1.12.0'
```

If other plugins pull an older `androidx.core`, you can add a resolution strategy so the newer version wins (example only—adjust to match your Gradle setup):

```gradle
configurations.all {
    resolutionStrategy {
        force 'androidx.core:core:1.12.0'
    }
}
```

### Import sample (optional)

After adding the package, you can import the sample scene and scripts:

1. Open **Window > Package Manager**
2. Select **Stash for Unity** in the list
3. Expand **Samples** and click **Import** next to **Stash Integration Sample**
4. Input your test API key in `StashLinkGenerator.cs` (in the imported sample)

Do not expect to browse the sample under **Packages** in the Project window: folders named **`Samples~`** are intentionally ignored by Unity’s importer (only **Import** copies them into your **Assets** folder).

## Sample / Demo

- **iOS:** [Open in Appetize.io](https://appetize.io/app/b_eyszozcrmyt2zifoh5bjyiifha)
- **Android:** [Open in Appetize.io](https://appetize.io/app/b_e7zfxgltohxm2rd5aw4zplzmwq?device=pixel7&osVersion=13.0&toolbar=true)

# Quick Start

## Package structure

When installed via UPM, the package lives under `Packages/gg.stash.unity/` with this layout:

| Path | Description |
|------|-------------|
| **Runtime/** | **`StashNative.cs`** – Singleton API: `OpenCard`, `OpenModal`, `OpenBrowser`, `CloseBrowser`, Android keep-alive helpers, and events. |
| **Editor/** | **(Optional)** Editor window for testing card/modal flows in the Unity Editor (Windows and macOS). |
| **Plugins/Android/** | StashNative AAR and Unity bridge for Android. |
| **Plugins/iOS/** | Unity bridge and StashNative.xcframework for iOS. |
| **Samples~/StashSample/** | Optional sample (on disk only). Unity **does not list** files under `Samples~` in the Project window (the `~` path is excluded from import). Use **Package Manager → Samples → Import** to copy them into `Assets/`. |


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
        var config = StashNativeCardConfig.Default;
        // If your game is locked to landscape orientation, force portrait for checkout:
        // config.forcePortrait = true;

        StashNative.Instance.OpenCard(
            STASH_URL_TO_OPEN,
            dismissCallback: OnDialogDismissed,
            successCallback: OnPaymentSuccess,
            failureCallback: OnPaymentFailure,
            config: config
        );
    }

    void OnDialogDismissed() => VerifyPurchaseStatus();
    void OnPaymentSuccess(string order) => VerifyPurchaseStatus();
    void OnPaymentFailure() => ShowErrorMessage("Payment could not be processed");
}
```

### Force portrait card in landscape games

Use **`StashNativeCardConfig.forcePortrait = true`** when your game is locked to landscape but you want the Stash card in portrait. For best experience, we recommend following adjustments:

**Lock Unity screen orientation while the potrait card is presented.** We recommend temporarily locking rotation in the Unity player for the duration of the card, then restoring every autorotation flag and the previous **`Screen.orientation`** in your dismiss, success, and failure paths. That way the game does not keep autorotating behind the potrait card, which can sometime cause keyboard focus or layout glitches.

```csharp
// To lock the orientation while the card is open, save current orientation settings, apply lock, and restore after:
// Example:
var savedOrientation = Screen.orientation;
// ...save all autorotate states as needed...

// Lock orientation to rotation used at the time card will be presented.
Screen.orientation = ScreenOrientation.LandscapeLeft;

// Restore all previous orientation/autorotate settings inside each callback:
StashNative.Instance.OpenCard(
    checkoutUrl,
    dismissCallback: () => { /* Restore orientation here */ },
    successCallback: _ => { /* Restore orientation here */ },
    failureCallback: () => { /* Restore orientation here */ },
    config: config);
```

**Android: backdrop for landscape-only builds.** With **`forcePortrait`**, checkout runs in a portrait activity while Unity may still be landscape-only; the Unity surface can look black or distorted during the transition (This is Android OS behaviour). You can, optionally capture the current frame and pass it to the SDK **before** **`OpenCard`** so it is shown as a full-screen backdrop behind the dim overlay (JPEG bytes). The SDK consumes the buffer; you do not need to free it on the native side.

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
yield return new WaitForEndOfFrame();
var snap = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
snap.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
snap.Apply();
byte[] imageBytes = snap.EncodeToPNG(); // or ImageConversion.EncodeToJPG(snap, 75)
Destroy(snap);

using (var stashCard = new AndroidJavaClass("com.stash.stashnative.StashNativeCard"))
{
    stashCard.CallStatic("setBackdropBytes", (object)imageBytes);
}
#endif

StashNative.Instance.OpenCard(/* … */);
```

After the flow ends, call **`setBackdropBytes(null)`** on Android if you use a backdrop, so a stale image is not reused. If Unity logs JNI warnings about **`byte[]`**, convert to **`sbyte[]`** when calling **`setBackdropBytes`** (see **`Assets/StashSample.cs`** in this repo).

### OpenModal()

Centered modal dialog on all devices. Same layout on phone and tablet; resizes on rotation. Suited for channel selection or as an alternative checkout style.

```csharp
using Stash.Native;

StashNative.Instance.OpenModal(
    STASH_URL_TO_OPEN,
    dismissCallback: () => RefreshShopState(),
    successCallback: order => OnPurchaseComplete(order),
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

### Android keep-alive (optional)

Android may kill or suspend your game during external payments in Chrome Custom Tabs or the browser, especially on low-memory devices. To prevent this, you can enable Stash's optional keep-alive foreground service (off by default). This service will automaticaly spawn when browser opens and unload when user returns to the game. See the [stash-native README](https://github.com/stashgg/stash-native/blob/main/README.md) for manifest and permission details. Enable it like this:

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
| **`void OpenCard(string url, Action dismissCallback, Action<string> successCallback, Action failureCallback, StashNativeCardConfig? config)`** | Opens the URL in the native card (drawer). `successCallback` receives an optional order payload (may be empty). All callbacks and config are optional. |
| **`void OpenModal(string url, Action dismissCallback, Action<string> successCallback, Action failureCallback, StashNativeModalConfig? config)`** | Opens the URL in a centered modal. Same success signature as `OpenCard`. |
| **`void OpenBrowser(string url)`** | Opens the URL in the platform browser (Chrome Custom Tabs when `androidx.browser` is on the classpath, otherwise the system browser on Android; `SFSafariViewController` on iOS). |
| **`void CloseBrowser()`** | **iOS only:** dismisses the Safari view programmatically. No-op on Android. |
| **`void SetKeepAliveEnabled(bool enabled)`** | **Android only:** opt in to the SDK keep-alive foreground service during external browser flows. |
| **`void SetKeepAliveConfig(StashNativeKeepAliveConfig config)`** | **Android only:** notification title, text, and optional icon resource id (`0` = library default). |
| **`void Dismiss()`** | Dismisses the current card or modal. |
| **`bool IsCurrentlyPresented`** | True if a card or modal is currently visible. |
| **`bool IsPurchaseProcessing`** | True when a purchase is in progress and the dialog cannot be dismissed manually. |

---

### Global Events

Subscribe on `StashNative.Instance`.

| Event | When it fires |
|-------|----------------|
| **`OnDialogDismissed`** | Card or modal was dismissed by the user. |
| **`OnPaymentSuccess`** | Payment completed successfully in the in-app UI. Argument: optional order string from checkout (may be empty). |
| **`OnPaymentFailure`** | Payment failed in the in-app UI. |
| **`OnExternalPayment`** | Checkout opened an external URL (e.g. GPay, Klarna, crypto). Finalize via deeplink; same flow as described in [stash-native callbacks](https://github.com/stashgg/stash-native/blob/main/README.md). |
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
| **`forcePortrait`** | `false` | Portrait-locked on phone when true. **Required for landscape-locked games:** Set to `true` if your Unity game is locked to landscape orientation to ensure checkout displays in portrait. Portrait orientation must be enabled in Unity Player Settings (iOS: `allowedAutorotateToPortrait: 1`). |
| **`cardHeightRatioPortrait`** | `0.68f` | Card height ratio portrait (0.1–1.0). |
| **`cardWidthRatioLandscape`** | `0.9f` | Card width ratio landscape. |
| **`cardHeightRatioLandscape`** | `0.6f` | Card height ratio landscape. |
| **`tabletWidthRatioPortrait`** | `0.4f` | Tablet width portrait. |
| **`tabletHeightRatioPortrait`** | `0.5f` | Tablet height portrait. |
| **`tabletWidthRatioLandscape`** | `0.3f` | Tablet width landscape. |
| **`tabletHeightRatioLandscape`** | `0.6f` | Tablet height landscape. |
| **`backgroundColor`** | `null` | Optional shell color (`#RGB`, `#RRGGBB`, `#AARRGGBB`). Omit for the default Stash light/dark theme. |

#### `StashNativeModalConfig` (struct)

Optional per-call config for **`OpenModal`**. **`StashNativeModalConfig.Default`** for defaults.

| Field | Default | Description |
|-------|---------|-------------|
| **`allowDismiss`** | `true` | User can dismiss (tap outside / gestures per native SDK). |
| **`phoneWidthRatioPortrait`** … **`tabletHeightRatioLandscape`** | (see struct) | Size ratios 0.1–1.0. |
| **`backgroundColor`** | `null` | Optional shell color; omit for default Stash theme. |

#### `StashNativeKeepAliveConfig` (struct)

Used with **`SetKeepAliveConfig`** on **Android** only.

| Field | Description |
|-------|-------------|
| **`notificationTitle`** | Notification title. |
| **`notificationText`** | Notification body. |
| **`notificationIconResId`** | Android drawable resource id, or **`0`** for the library default icon. |


## Unity Editor Simulator

<div align="center">
  <img src="https://storage.googleapis.com/stash_sdk_demo/popup_editor.png" width="800px" /><br/>
  <em>Unity Editor Simulation</em>
</div>
</br>
</br>

Package includes a Unity editor extension that allows you to test Stash URLs directly in the Unity Editor without building to a device.

When you call `OpenCard()` or `OpenModal()` in the Editor, the extension intercepts and displays the flow in a preview window. You can interact with the UI and verify callback events. `OpenBrowser()` is not simulated; it opens the system browser.

> **Note:** Currently **Windows** and **macOS** versions of Unity are supported for editor simulator. Linux versions of editor are not supported.

## Troubleshooting

See **[TROUBLESHOOTING.md](TROUBLESHOOTING.md)** for iOS/Android build issues (Xcode symbols, framework embedding, Gradle, Custom Tabs, keep-alive, and related fixes).

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
