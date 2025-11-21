# Stash.Popup

| <img src="https://github.com/stashgg/stash-unity/blob/main/.github/popup_phone.png?raw=true" width="400px" /> | <img src="https://github.com/stashgg/stash-unity/blob/main/.github/popup_tablet.png?raw=true" width="400px" /> |
|:--:|:--:|
| Phone Presentation | Tablet Presentation |


Unity plugin for integrating Stash Pay checkout flows using native WebViews on iOS and Android.
The plugin also provides SFSafariViewController and Chrome Custom Tabs mode as a fallback or alternative flow.

## Requirements

- Unity 2019.4+
- iOS 12.0+ / Android 13.0+

## Installation

Import the `Stash.Popup` folder into your Unity project's Assets directory.

## Package Contents

### ./Editor
Build post-processing scripts and editor testing tool:
- **`AddWebKitFramework.cs`** - Adds WebKit and SafariServices frameworks to iOS Xcode projects
- **`StashPopupAndroidPostProcess.cs`** - Injects `StashPayCardPortraitActivity` into Android manifest
- **`StashPayCardEditor/`** - Unity Editor testing extension for testing full checkout without device builds (macOS and Windows)

### ./Plugins
Native platform implementations for Stash Pay integration:
- **`Plugins/iOS/`** - Objective-C/Objective-C++ code for the native Stash Dialog
- **`Plugins/Android/`** - Java code for the native Stash Dialog

### ./Sample
Sample scene demonstrating package usage:
- **`StashPaySample.cs`** - Shows `OpenCheckout()` and `OpenOptin()` with web request integration
- **`StashPaySample.unity`** - Sample scene with buttons to try checkout

## Usage

### Opening a Checkout

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

**Important:** Always verify purchases on your backend. Never trust client-side callbacks alone.


### Opening an Opt-in Popup

Use `OpenPopup()` for payment channel selection opt-in dialogs. Always handle the `OnOptinResponse` event:

```csharp
void ShowPaymentChannelSelection()
{
    // Subscribe to opt-in response
    StashPayCard.Instance.OnOptinResponse += OnChannelSelected;
    
    StashPayCard.Instance.OpenPopup(
        "https://your-site.com/payment-channel-selection",
        dismissCallback: () => {
            // Unsubscribe when popup closes
            StashPayCard.Instance.OnOptinResponse -= OnChannelSelected;
        }
    );
}

void OnChannelSelected(string channel)
{
    // Receives "native_iap" or "stash_pay"
    string paymentMethod = channel.ToUpper();
    
    // Save user preference
    PlayerPrefs.SetString("PaymentMethod", paymentMethod);
    PlayerPrefs.Save();
    
    Debug.Log($"User selected: {paymentMethod}");
}
```

**Use `OpenPopup()` exclusively for:** Payment channel selection opt-in flows.

### Configuring Popup Size

`OpenPopup()` supports optional custom size configuration. By default, it uses platform-specific default sizing. You can customize the size using `PopupSizeConfig`:

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

**Note:** The popup automatically adjusts its size when the device rotates between portrait and landscape orientations. Custom multipliers are applied relative to the calculated base size (which depends on device type and screen dimensions).


### Forcing Web View Mode

Stash Popup can also force Web View mode (SFSafariViewController on iOS, Chrome Custom Tabs on Android) instead of in-app WebView. You can either force this in code or later remotely via Stash Studio for specific segments.

Keep in mind that callbacks do not work for the web view mode, and users are instead redirected back to the game via deeplinks after a successful purchase. 

```csharp
void OpenInBrowser(string url)
{
    // Enable browser mode
    StashPayCard.Instance.ForceWebBasedCheckout = true;
    
    // Opens in Safari/Chrome instead of card
    StashPayCard.Instance.OpenCheckout(url, OnDismiss, OnSuccess, OnFailure);
    
    // Restore default mode
    StashPayCard.Instance.ForceWebBasedCheckout = false;
}
```

## Unity Editor Testing 

| <img src="https://github.com/stashgg/stash-unity/blob/main/.github/popup_editor.png?raw=true" width="500px" /> |
|:--:|
| Unity Editor Simulation |



The plugin includes an Unity editor extension that allows you to test StashPayCard popups and checkout dialogs directly in the Unity Editor without building to a device.

**How it works:** When you call `OpenPopup()` or `OpenCheckout()` in the Editor, the extension automatically intercepts these calls and displays the flow in a window within Unity editor. This enables you to interact with the Stash Pay UI, complete purchases, and verify callback events without leaving the Editor.

Currently **Windows** and **macOS** versions of Unity are supported.


## Troubleshooting

### iOS Build Error in Xcode: Undefined symbol

Add frameworks in Unity Project Settings → iOS → Other Settings → Linked Frameworks:
- `WebKit.framework`
- `SafariServices.framework`

Clean and rebuild Xcode project.

### Android: Blank WebView

Ensure internet permission in your AndroidManifest.xml.


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
