# Stash.Popup

| <img src="https://github.com/stashgg/stash-unity/blob/main/.github/popup_phone.png?raw=true" width="300px" /> | <img src="https://github.com/stashgg/stash-unity/blob/main/.github/popup_tablet.png?raw=true" width="400px" /> |
|:--:|:--:|
| Phone Presentation | Tablet Presentation |

Unity plugin for integrating Stash Pay checkout flows using native WebViews on iOS and Android.
The plugin also provides SFSafariViewController and Chrome Custom Tabs mode as a fallback or alternative flow.

## Requirements

- Unity 2019.4+
- iOS 12.0+ / Android 13.0+

## Installation

Import the `Stash.Popup` folder into your Unity project's Assets directory.

## Folder Contents

### ./Editor
Dependency post-processing scripts and editor purchase simulator tool:
- **`AddWebKitFramework.cs`** - Adds WebKit and SafariServices frameworks to iOS Xcode projects
- **`StashPopupAndroidPostProcess.cs`** - Injects `StashPayCardPortraitActivity` into Android manifest
- **`StashPayCardEditor/`** - Unity extension for testing full checkout flow inside editor.

### ./Plugins
Native platform implementations for Stash Pay purchase card:
- **`Plugins/iOS/`** - Objective-C/Objective-C++ code for the native Stash Dialog
- **`Plugins/Android/`** - Java code for the native Stash Dialog

### ./Sample
Sample scene demonstrating package usage, use as a reference implementation:
- **`StashPaySample.cs`** - Shows `OpenCheckout()` and `OpenOptin()` with web request integration
- **`StashPaySample.unity`** - Sample scene with buttons to try checkout


## Best Practices

- Implement both in-app and browser-based checkout flows with Stash.Popup to ensure a reliable fallback if one flow is unavailable.
- Set up deep link handling for Stash Pay, as some payment methods require returning to your app from external flows.
- Maintain Stash.Popup in its own folder for easy updates. The package is actively developed and may receive frequent patches.

## Basic Usage

### Opening a In-app checkout

> **iOS Development Note:**  
> The first Stash checkout call may be slow when running under the Xcode debugger, due to web view processes being instrumented by Xcode. This delay only affects debug sessions, not production builds.

Use `OpenCheckout()` to display a Stash Pay checkout in a native card dialog:

```csharp
using StashPopup;

public class MyStore : MonoBehaviour
{
    void PurchaseItem(string checkoutUrl)
    {
        // checkoutUrl is a Stash Pay URL generated on your game backend.
        StashPayCard.Instance.OpenCheckout(
            checkoutUrl,
            dismissCallback: OnCheckoutDismissed,
            successCallback: OnPaymentSuccess,
            failureCallback: OnPaymentFailure
        );
    }
    
    void OnCheckoutDismissed()
    {
        // User closed the dialog
        VerifyPurchaseStatus();
    }
    
    void OnPaymentSuccess()
    {
        // Payment completed - verify on backend before granting items
        VerifyAndGrantPurchase();
    }
    
    void OnPaymentFailure()
    {
        // Payment failed - show error to user
        ShowErrorMessage("Payment could not be processed");
    }
}
```

>**Note:** Use the callbacks to update your game client and handle changes in purchase status. However, make sure to verify every purchase on your backend server before granting any items.


## Browser Checkout Mode

By default, Stash Popup shows the checkout flow inside your game as a native in-app card. If you prefer to direct users to a isolated browser window using SFSafariViewController on iOS or Chrome Custom Tabs on Android you can enable browser mode for checkout. 

Implementing browser mode together with in-app card is also strongly recommended, either as an explicit user option or as a fallback when the in-app card isn’t suitable.

> **Android Note:** Some Unity projects may require the `androidx.browser` dependency to use Chrome Custom Tabs. See [Troubleshooting](#android-browser-fallback-behavior-inconsistency) for setup instructions.

```csharp
void OpenInBrowserMode(string url)
{
    // Enable browser-based checkout for this operation.
    StashPayCard.Instance.ForceWebBasedCheckout = true;

    // This will open isolated browser window instead of the in-app card.
    StashPayCard.Instance.OpenCheckout(url, OnDismiss, OnSuccess, OnFailure);
}
```

When you enable browser mode, the purchase flow occurs in the isolated browser process so Stash Pay can't call your Unity callbacks directly. Instead, Stash Pay returns the purchase result using deeplinks, which your game can optionally listen for to determine the outcome.

## Deeplinks

Stash Pay uses the following deeplinks during browser-based checkout, as well as in situations where an external browser flow is required (For example 3DS verifications, Google Pay etc.). Regardless of whether you use browser mode, it is highly recommended to implement deeplink handling in your game to ensure reliable purchase result reception.

**Deeplink Structure:**  
Stash uses the following deeplink format for both iOS and Android:
- `<your-scheme>://stash/purchaseSuccess` - Purchase completed successfully
- `<your-scheme>://stash/purchaseFailure` - Purchase failed or was cancelled

> **Note:** You can set your deeplink scheme in Stash Studio.

**Handling Deeplinks:**  
On Android, OpenCheckout() callbacks like OnSuccess/OnFailure are *not* triggered when using browser checkout, users complete checkout and then are redirected back to your game with a deeplink. It is up to you to handle the rest of the purchase logic after the deeplink is received.

On iOS, however, you can still leverage all the usual callbacks (dismiss, success, failure) *if* you handle the incoming deeplinks and manually notify StashPayCard via `DismissSafariViewController(success: true/false)`. This allows you to propagate results all the way back to your original Unity callback handlers.

**Handling deep links on iOS:**

On iOS, listen for deep link activations in your Unity code. When you detect one of the Stash-specific result URLs, call `DismissSafariViewController()` with the appropriate success value. StashPayCard will then trigger your original OnSuccess/OnFailure callback from the OpenCheckout call.

```csharp
void Awake()
{
    Application.deepLinkActivated += OnDeepLink;
}

void OnDeepLink(string url)
{
    if (url.Contains("stash/purchaseSuccess"))
        StashPayCard.Instance.DismissSafariViewController(success: true);  // Triggers OnSuccess callback!
    else if (url.Contains("stash/purchaseFailure"))
        StashPayCard.Instance.DismissSafariViewController(success: false); // Triggers OnFailure callback!
}
```

You may also use `DismissSafariViewController()` without passing a success parameter to simply close the view controller. In this case, only the dismiss callback is triggered—no success or failure events will be called, giving you full control over how to handle the purchase result in web view mode.

## Unity Editor Simulator 

<div align="center">
  <img src="https://github.com/stashgg/stash-unity/blob/main/.github/popup_editor.png?raw=true" width="800px" /><br/>
  <em>Unity Editor Simulation</em>
</div>
</br>
</br>

Stash.Popup plugin includes a Unity editor extension that allows you to test StashPayCard popups and checkout dialogs directly in the Unity Editor without building to a device.

When you call `OpenCheckout()` in the Editor, the extension automatically intercepts these calls and displays the flow in a window within Unity editor. This enables you to interact with the Stash Pay UI, complete purchases, and verify callback events without leaving the Editor.

> **Note:** Currently **Windows** and **macOS** versions of Unity are supported for editor simulator. Linux versions will come soon.



## Optional Flows

### Opening an Opt-in Popup

> Note: Opt-in dialog requires unique URL you can obtain from Stash Studio.

Use `OpenPopup()` for dynamic payment channel selection opt-in dialogs controlled by Stash. Always handle the `OnOptinResponse` event:

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
    // Receives "native_iap" or "stash_pay" enum.
    string paymentMethod = channel.ToUpper();
    
    // Save user preference and use it to control the payment channel.
    PlayerPrefs.SetString("PaymentMethod", paymentMethod);
    PlayerPrefs.Save();
    
    Debug.Log($"User selected: {paymentMethod}");
}
```

### Configuring Opt-in Popup Size

By default, `OpenPopup()` sizes itself to fit the device screen constraints. While not recommended for most cases, if you want to override this behavior, you can provide a custom size using the `PopupSizeConfig` class:

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

While highly unlikely, if this happens add frameworks in Unity Project Settings → iOS → Other Settings → Linked Frameworks:
- `WebKit.framework`
- `SafariServices.framework`

Clean and rebuild Xcode project.

### [Android] Blank Checkout Card

Ensure internet permission in your AndroidManifest.xml.

### [Android] Full browser used instead of Chrome Custom Tabs

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

3. **Rebuild your Android project** - the dependency will now be included automatically.

> **Note:** Stash Popup will automatically detect if Chrome Custom Tabs is available and fall back gracefully to the default browser if not.


## API Reference

### Methods

**`OpenCheckout(string url, Action onDismiss, Action onSuccess, Action onFailure)`**
Opens Stash Pay checkout in a sliding card from the bottom of the screen.

**`OpenPopup(string url, Action onDismiss = null, Action onSuccess = null, Action onFailure = null, PopupSizeConfig? customSize = null)`**
Opens Stash opt-in and other remote Stash dialogs in a centered modal popup. Size can be customized using `PopupSizeConfig`. If not provided, uses platform-specific default sizing.

**`ResetPresentationState()`**
Dismisses current dialog and resets state.

### Properties

**`ForceWebBasedCheckout`** (bool)
- `false` - Use Stash Pay native card (default)
- `true` - Use SFSafariViewController/Chrome Custom Tabs for checkout.

**`IsCurrentlyPresented`** (bool, read-only)
- Returns whether a dialog is currently open.

### Types

**`PopupSizeConfig`** (struct)
- `portraitWidthMultiplier` (float) - Width multiplier for portrait orientation
- `portraitHeightMultiplier` (float) - Height multiplier for portrait orientation
- `landscapeWidthMultiplier` (float) - Width multiplier for landscape orientation
- `landscapeHeightMultiplier` (float) - Height multiplier for landscape orientation

**Note:** Each platform (iOS and Android) has its own default sizing. When `customSize` is not provided, the platform-specific defaults are used.

## Support

- Documentation: https://docs.stash.gg
- Email: developers@stash.gg

---

Copyright © 2025 Stash Interactive. All rights reserved.
