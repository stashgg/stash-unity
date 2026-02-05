# Stash.Popup

| <img src="https://github.com/stashgg/stash-unity/blob/main/.github/popup_phone.png?raw=true" width="300px" /> | <img src="https://github.com/stashgg/stash-unity/blob/main/.github/popup_tablet.png?raw=true" width="400px" /> |
|:--:|:--:|
| Phone Presentation | Tablet Presentation |

Unity wrapper for the Stash Pay native SDK (stash-native). It integrates in-app checkout and modal flows on iOS and Android using pre-built native packages (AAR on Android, xcframework on iOS). The wrapper exposes all native features: **OpenCheckout** and **OpenModal** with full configuration (force portrait, card/tablet ratios, modal sizing), **ForceWebBasedCheckout** (SFSafariViewController / Chrome Custom Tabs), and all callbacks including **OnNetworkError** for initial page load failures.

> **Note:** Stash Pay can also be integrated by opening the checkout URL in the user's default browser if you prefer not to use the in-app UI.

## Requirements

- Unity 2019.4+
- iOS 13.0+ / Android 5.0+ (API 21+)

## Installation

1. Import the `Stash.Popup` folder into your Unity project's Assets directory.
2. **Android:** The package includes `StashPay-1.2.4.aar` in `Plugins/Android/`. Ensure your build includes `androidx.appcompat:appcompat:1.6.1` and `androidx.browser:browser:1.7.0` (see [stash-native Android README](https://github.com/stashgg/stash-native/blob/main/Android/README.md)).
3. **iOS:** Add the Stash Pay xcframework **before building from Unity** so in-app checkout and modal work:
   - Download [StashPay-1.2.4.xcframework.zip](https://github.com/stashgg/stash-native/releases) (or latest), unzip.
   - Place the `StashPay.xcframework` folder in `Assets/Stash.Popup/Plugins/iOS/`. Unity will include it in the generated Xcode project.
   - Build for iOS from Unity. If the xcframework is missing, the wrapper falls back to opening the URL in the system browser and logs a warning.

## Native dependency (stash-native)

This wrapper targets **stash-native 1.2.4**. To update:

- **Android:** Replace `Assets/Stash.Popup/Plugins/Android/StashPay-1.2.4.aar` with the new AAR from [releases](https://github.com/stashgg/stash-native/releases). The thin bridge (`StashPayCardUnityBridge.java`) forwards calls to the AAR and sends callbacks to Unity.
- **iOS:** Replace the embedded `StashPay.xcframework` in your Xcode project with the new xcframework from the same releases. The thin bridge (`StashPayCardBridge.mm`) forwards calls to the SDK and implements the delegate to send callbacks to Unity.

## Folder Structure

### ./Editor
- **`StashPopupAndroidPostProcess.cs`** - Adds permissions for the Stash Pay AAR (e.g. foreground service). The AAR declares its own components via manifest merge.
- **`StashPayCardEditor/`** - Editor window for testing checkout and modal in the Unity Editor (Windows and macOS).

### ./Plugins
- **`Plugins/Android/`** - `StashPay-1.2.4.aar` (native SDK) and `StashPayCardUnityBridge.java` (Unity bridge).
- **`Plugins/iOS/`** - `StashPayCardBridge.mm` (Unity bridge). Place `StashPay.xcframework` here before building for iOS so the bridge links to the SDK; ensure the framework is set to **Embed & Sign** in the generated Xcode project (see [Troubleshooting](#ios-app-crashes-with-library-not-loaded-related-to-stashpay)).

### ./Scripts
- **`StashPayCard.cs`** - Singleton API: `OpenCheckout`, `OpenModal`, `OpenPopup`, configuration properties, and events.

### ./Sample
- **`StashPaySample.cs`** / **`StashPaySample.unity`** - Simple demo: Open Checkout, Open Modal, config toggles, Force Web Checkout, and callback status.


## Best Practices

- Implement both in-app dialog and in-app browser checkout flows with Stash.Popup to ensure a reliable fallback if one flow is unavailable.
- Set up deep link handling for Stash Pay even when using in-app dialogs, as some payment methods may require returning to your app from external browser flows.
- Maintain Stash.Popup in its own folder for easy updates. The package is actively developed and may receive frequent patches.

## Basic Usage

Before using Stash.Popup, make sure your game server is set up to create Stash Pay checkout URLs using the Stash API. If you haven't already set up checkout URL generation, see our [integration guide](https://docs.stash.gg/guides/stash-pay/integration) for instructions.

Stash.Popup supports two presentation modes: **in-app card dialog** and **in-app browser**. To see how each presentation mode looks, refer to our documentation:

- [Presentation Options for iOS](https://docs.stash.gg/guides/stash-pay/ios-android-integration/presentation-options-ios)
- [Presentation Options for Android](https://docs.stash.gg/guides/stash-pay/ios-android-integration/presentation-options-android)

We recommend implementing both so you can switch between these modes as you need.

### Using In-app card dialog

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

You can customize the checkout card size (e.g. for webshop or full-page flows) by setting `CardHeightRatioPortrait` (and other ratio properties) before calling `OpenCheckout()`. Restore the previous value in the dismiss callback if you need different sizes elsewhere.

> **iOS Development Note:**  
> The first Stash checkout call may be slow when running under the Xcode debugger (especially if connected wirelessly), due to `WKWebView` processes being heavily instrumented by Xcode. This delay only affects debug sessions on the first call, not production builds.

### Using In-app browser

If you prefer to direct users to an isolated in-app browser window instead, you can enable browser mode for the `OpenCheckout()` method. This will use [SFSafariViewController](https://developer.apple.com/documentation/safariservices/sfsafariviewcontroller) on iOS or [Chrome Custom Tabs](https://developer.android.com/develop/ui/views/layout/webapps/overview-of-android-custom-tabs) on Android.

Even if you primarily use the in-app card mode, we strongly recommend supporting in-app browser mode as well either as a user-selectable option or as an automatic fallback if the dialog encounters unhandled errors.

```csharp
void OpenInBrowserMode(string url)
{
    // Enable browser-based checkout for this operation.
    StashPayCard.Instance.ForceWebBasedCheckout = true;

    // This will open in-app browser window instead of the in-app card.
    // In browser mode only dismiss callback is available. Use deeplinks for success/failure callbacks.
    StashPayCard.Instance.OpenCheckout(url, OnDismiss);
}
```

In browser mode, Stash Pay can't trigger Unity Success/Failure callbacks directly like the in-app dialog can. Instead, the purchase result is sent back via deeplinks. Always implement deeplink handling alongside native callbacks to cover all use-cases. See section below for details.

> **Android Note:** Some Unity projects *may* require the `androidx.browser` dependency to use Chrome Custom Tabs. See [Troubleshooting](#android-browser-fallback-behavior-inconsistency) section for setup instructions.

## Deeplinks

Stash Pay uses deeplinks to return users to your game after in-app browser checkout or some external flows (e.g., 3DS, PayPal). Try to handle deeplinks in your game, as they are required for browser mode and sometimes needed for in-app dialogs as well.

**Deeplink Structure:**  
Stash uses the following deeplink format for successful and failed purchases:
- `<your-app-scheme>://stash/purchaseSuccess` - Purchase completed successfully.
- `<your-app-scheme>://stash/purchaseFailure` - Purchase failed.

> **Note:** You can set your app unique deeplink scheme in Stash Studio.


**Handling deeplinks:**  

Configure your Unity project to handle deeplinks using the standard approach. If you don't use deep linking in your game already, see the official [Unity Deep Linking documentation](https://docs.unity3d.com/6000.2/Documentation/Manual/deep-linking.html).

On iOS you must call `StashPayCard.Instance.DismissSafariViewController()` after the deeplink is received. This will dismiss the **SFSafariViewController** seamlessly, the user is returned to the game, and you can handle the purchase outcome. On Android there is no need to manually dismiss Chrome Custom Tabs as the user is automatically returned to the game.

```csharp
void Awake()
{
    Application.deepLinkActivated += OnDeepLink;
}

void OnDeepLink(string url)
{
    if (url.Contains("stash/purchaseSuccess"))
    {
        StashPayCard.Instance.DismissSafariViewController(success: true); //iOS only
        //Handle purchase success.
    }
    else if (url.Contains("stash/purchaseFailure"))
    {
        StashPayCard.Instance.DismissSafariViewController(success: false); //iOS only
        //Handle purchase failure.
    }
}
```

## Exception Handling

Stash.Popup includes built-in exception handling for native plugin operations on both iOS and Android. This allows you to catch and handle errors that occur during checkout or popup operations inside your Unity game.

### Usage

Subscribe to the `OnNativeException` event to be notified when exceptions occur:

```csharp
using StashPopup;

void Start()
{
    // Subscribe to exception events
    StashPayCard.Instance.OnNativeException += OnStashPayException;
}

void OnStashPayException(string operation, Exception exception)
{
    Debug.LogError($"StashPayCard exception in {operation}: {exception.Message}");
    Debug.LogException(exception);
    
    // Handle the exception - log to analytics, show error to user, etc.
    // Example: Log to crash reporting service
}

void OnDestroy()
{
    if (StashPayCard.Instance != null)
    {
        StashPayCard.Instance.OnNativeException -= OnStashPayException;
    }
}
```
### Automatic Fallback Behavior

When `OpenCheckout()`, `OpenModal()`, or `OpenPopup()` encounters an exception:
- The exception is logged and reported via `OnNativeException` event
- On iOS and Android, the operation falls back to opening the URL in the default browser (`Application.OpenURL()`)
- This ensures users can still complete their purchase even if the native dialog fails.

### Best Practices

- Always subscribe to `OnNativeException` in production builds to monitor errors.
- Log exceptions to your analytics/crash reporting service.
- Consider showing user-friendly error messages when exceptions occur.

## Unity Editor Simulator 

<div align="center">
  <img src="https://github.com/stashgg/stash-unity/blob/main/.github/popup_editor.png?raw=true" width="800px" /><br/>
  <em>Unity Editor Simulation</em>
</div>
</br>
</br>

Stash.Popup package includes a Unity editor extension that allows you to test Stash Pay checkout dialogs directly in the Unity Editor without building to a device.

When you call `OpenCheckout()` in the Editor, the extension automatically intercepts these calls and displays the flow in an "emulator" window within Unity editor. This enables you to interact with the Stash Pay UI, complete purchases, and verify callback events. You can finish both test and production purchases.

> **Note:** Currently **Windows** and **macOS** versions of Unity are supported for editor simulator. Linux versions of editor are not supported.


## Optional Flows

### Opt-in Popup

Stash also provides a customizable opt-in popup that allows users to choose between Native IAP and Stash Pay. This dialog is hosted remotely and can be tailored in Stash Studio, even down to specific players, devices, or player cohorts. Stash handles the visuals and presentation logic, you simply prompt it when needed.

> Note: Opt-in dialog requires unique URL you can obtain from Stash Studio.

Use `OpenPopup()` for dynamic payment channel selection opt-in dialogs controlled by Stash. Handle the `OnOptinResponse` event that returns the player's preferred selection:

```csharp
void ShowPaymentChannelSelection()
{
    // Subscribe to opt-in response
    StashPayCard.Instance.OnOptinResponse += OnChannelSelected;
    
    StashPayCard.Instance.OpenPopup(
        "https://payment-channel-selection-url-from-stash-studio",
        dismissCallback: () => {
            // Unsubscribe when popup closes
            StashPayCard.Instance.OnOptinResponse -= OnChannelSelected;
        }
    );
}

void OnChannelSelected(string channel)
{
    // Receives "NATIVE_IAP" or "STASH_PAY" enum.
    string paymentMethod = channel.ToUpper();
    
    // Save user preference and use it to control the payment channel.
    PlayerPrefs.SetString("PaymentMethod", paymentMethod);
    
    Debug.Log($"User selected: {paymentMethod}");
}
```

### Opt-in Popup Size

By default, `OpenPopup()` sizes itself automatically to fit the device screen. While not recommended for most cases, if you want to override this behavior, you can provide a custom size using the `PopupSizeConfig` class:

```csharp
var customSize = new PopupSizeConfig
{
    portraitWidthMultiplier = 0.9f,      // 90% of base width in portrait
    portraitHeightMultiplier = 1.2f,      // 120% of base height in portrait
    landscapeWidthMultiplier = 1.4f,     // 140% of base width in landscape
    landscapeHeightMultiplier = 0.85f    // 85% of base height in landscape
};

StashPayCard.Instance.OpenPopup(
    url,
    customSize: customSize
);
```



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


## API Reference

### Methods

**`OpenCheckout(string url, Action onDismiss = null, Action onSuccess = null, Action onFailure = null)`**
Opens Stash Pay checkout (sliding card on phone, centered modal on tablet). Respects `ForcePortraitOnCheckout` and card/tablet ratio settings.

**`OpenModal(string url, Action onDismiss = null, Action onSuccess = null, Action onFailure = null, StashPayModalConfig? config = null)`**
Opens a centered modal (e.g. opt-in / channel selection). Use `config` for drag bar, dismiss behavior, and phone/tablet size ratios.

**`OpenPopup(...)`**
Legacy overload; equivalent to `OpenModal` with optional `PopupSizeConfig` for custom size.

**`ResetPresentationState()`** – Dismisses current dialog and resets state.

**`DismissSafariViewController()`** / **`DismissSafariViewController(bool success)`** – For web-based checkout deep link handling (iOS).

### Checkout configuration (before OpenCheckout)

**`ForcePortraitOnCheckout`** (bool) – Portrait-locked activity on phone when true.  
**`CardHeightRatioPortrait`**, **`CardWidthRatioLandscape`**, **`CardHeightRatioLandscape`** – Phone card size (0.1–1.0).  
**`TabletWidthRatioPortrait`**, **`TabletHeightRatioPortrait`**, **`TabletWidthRatioLandscape`**, **`TabletHeightRatioLandscape`** – Tablet card size.

### Other properties

**`ForceWebBasedCheckout`** (bool) – Use SFSafariViewController/Chrome Custom Tabs instead of in-app card.  
**`IsCurrentlyPresented`** (bool, read-only) – Whether a dialog is currently open.

### Events

**`OnPaymentSuccess`**, **`OnPaymentFailure`**, **`OnSafariViewDismissed`** (dialog dismissed), **`OnOptinResponse(string)`**, **`OnPageLoaded(double loadTimeMs)`**, **`OnNetworkError`** (initial page load failed; dialog is auto-dismissed).

**`OnNativeException`** (event `Action<string, Exception>`)
- Fired when an unhandled exception occurs during native plugin operations.
- Parameters:
  - `string operation` - The name of the operation that failed (e.g., "OpenCheckout", "OpenPopup")
  - `Exception exception` - The exception that occurred
- **Platform Notes:**
  - **Android:** Java exceptions are catchable and will trigger this event.
  - **iOS:** Only `NSException` objects caught in `@try/@catch` blocks trigger this event. Crashes and memory violations cannot be caught.

### Types

**`StashPayModalConfig`** (struct) – Modal appearance and size (drag bar, allow dismiss, phone/tablet ratios). Use **`StashPayModalConfig.Default`** for defaults.

**`PopupSizeConfig`** (struct) – Legacy size config for `OpenPopup`; prefer `StashPayModalConfig` with `OpenModal`.

## Device Testing & Issues

Every Stash.Popup release is live tested using the [BrowserStack App Live](https://www.browserstack.com/list-of-browsers-and-platforms/app_live) device cloud. The following iOS and Android devices are included in the test suite (subject to platform availability):


| Platform   | Devices (Sample Coverage)                                                                                                                                                                           | OS Versions                 |
|------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|-----------------------------|
| **iOS**    | iPhone 15 Pro, iPhone 15, iPhone 14 Pro Max, iPhone 14, iPhone 13, iPhone 12 Pro, iPhone 12, iPhone 11, iPhone XR, iPhone SE (3rd Gen), iPad Pro 12.9, iPad Air 5, iPad Mini 6, iPad 10th Gen      | iOS / iPadOS 26, 18, 17 |
| **Android**| Google Pixel 8, Pixel 7 Pro, Pixel 7, Pixel 6a, Samsung Galaxy S24 Ultra, S24+, S23 Ultra, S23, S22+, S22, S21 FE, S21, S20, OnePlus 10 Pro, 9 Pro, Xiaomi Redmi Note 11, 12, Samsung Galaxy Tab S8 | Android 16, 15, 14, 13, 12, 11 |

> **Note:** Device availability on BrowserStack may change. Additional models and OS versions are added for regression coverage and as new releases become available.

Manual and automated test flows validate:
- In-app card dialog and in-app browser (SFSafariViewController, Chrome Custom Tabs)
- Checkout result callback triggers
- Deep link handling for at least one major Android and iOS version per release

For the complete up-to-date device list, see [BrowserStack App Live devices](https://www.browserstack.com/list-of-browsers-and-platforms/app_live).  
If you encounter any device-specific issues, please [file a bug report on GitHub](https://github.com/stashgg/stash-unity/issues).



## Support

- Documentation: https://docs.stash.gg
- Email: developers@stash.gg

---

Copyright © 2025 Stash Interactive. All rights reserved.
