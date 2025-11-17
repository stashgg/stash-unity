# Stash.Popup

Unity plugin for integrating Stash Pay checkout flows using native WebViews on iOS and Android.
The plugin also provides SFSafariViewController and Chrome Custom Tabs mode.

## Requirements

- Unity 2019.4+
- iOS 12.0+ / Android API 21+

## Installation

Import the `Stash.Popup` folder into your Unity project's Assets directory.

## Folder Structure

### Editor
Build post-processing scripts that automatically configure platform-specific settings:
- **`AddWebKitFramework.cs`** - Adds WebKit and SafariServices frameworks to iOS Xcode projects
- **`StashPopupAndroidPostProcess.cs`** - Injects `StashPayCardPortraitActivity` into Android manifest

### Plugins
Native platform implementations for WebView integration:
- **`Plugins/iOS/`** - Objective-C/Objective-C++ code for the native Stash Dialog
- **`Plugins/Android/`** - Java code for the native Stash Dialog

### Sample
Start here: Example implementation demonstrating API usage:
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

`OpenPopup()` supports optional size configuration. By default, it uses the Medium preset. You can customize the size using presets or custom multipliers:

#### Using Size Presets

```csharp
// Small popup (70% width, 100% height in portrait)
StashPayCard.Instance.OpenPopup(
    url,
    size: PopupSize.Small
);

// Medium popup - default (85% width, 112.5% height in portrait)
StashPayCard.Instance.OpenPopup(
    url,
    size: PopupSize.Medium  // or omit for default
);

// Large popup (100% width, 125% height in portrait)
StashPayCard.Instance.OpenPopup(
    url,
    size: PopupSize.Large
);
```

#### Using Custom Size Configuration

For fine-grained control, use `PopupSizeConfig` to specify exact multipliers:

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

## Troubleshooting

### iOS Build Error: Undefined symbol

Add frameworks in Unity Project Settings → iOS → Other Settings → Linked Frameworks:
- `WebKit.framework`
- `SafariServices.framework`

Clean and rebuild Xcode project.

### Android: Blank WebView

1. Ensure internet permission in AndroidManifest.xml
2. Enable hardware acceleration

### Callbacks Not Firing

- Test on real device (callbacks don't work in Unity Editor)
- Check native logs (Xcode Console / Android Logcat)
- Verify your web page calls the correct JavaScript functions


## API Reference

### Methods

**`OpenCheckout(string url, Action onDismiss, Action onSuccess, Action onFailure)`**
Opens Stash Pay checkout in a sliding card from the bottom of the screen.

**`OpenPopup(string url, Action onDismiss = null, Action onSuccess = null, Action onFailure = null, PopupSize? size = null, PopupSizeConfig? customSize = null)`**
Opens Stash opt-in and other remote Stash dialogs in a centered modal popup. Size can be customized using presets (`PopupSize.Small`, `Medium`, `Large`) or custom multipliers (`PopupSizeConfig`). Defaults to `Medium` size if not specified.

**`ResetPresentationState()`**
Dismisses current dialog and resets state.

### Properties

**`ForceWebBasedCheckout`** (bool)
- `false` - Use Stash Pay native card (default)
- `true` - Use SFSafariViewController/Chrome Custom Tabs for checkout.

**`IsCurrentlyPresented`** (bool, read-only)
- Returns whether a dialog is currently open.

### Types

**`PopupSize`** (enum)
- `Small` - 70% width, 100% height (portrait)
- `Medium` - 85% width, 112.5% height (portrait) - default
- `Large` - 100% width, 125% height (portrait)

**`PopupSizeConfig`** (struct)
- `portraitWidthMultiplier` (float) - Width multiplier for portrait orientation (default: 0.85)
- `portraitHeightMultiplier` (float) - Height multiplier for portrait orientation (default: 1.125)
- `landscapeWidthMultiplier` (float) - Width multiplier for landscape orientation (default: 1.27075)
- `landscapeHeightMultiplier` (float) - Height multiplier for landscape orientation (default: 0.9)
- `Default` (static property) - Returns the default Medium size configuration

## Support

- Documentation: https://docs.stash.gg
- Email: developers@stash.gg

---

Copyright © 2024 Stash. All rights reserved.
