# Stash.Popup

Unity plugin for integrating Stash Pay checkout flows using native WebViews on iOS and Android.

## Requirements

- Unity 2019.4+
- iOS 12.0+ / Android API 21+

## Installation

Import the `Stash.Popup` folder into your Unity project's Assets directory.

## Plugin Structure

### Editor
Build post-processing scripts that automatically configure platform-specific settings:
- **`AddWebKitFramework.cs`** - Adds WebKit and SafariServices frameworks to iOS Xcode projects
- **`StashPopupAndroidPostProcess.cs`** - Injects `StashPayCardPortraitActivity` into Android manifest

### Plugins
Native platform implementations for WebView integration:
- **`Plugins/iOS/`** - Objective-C/Objective-C++ code for native Stash Dialog.
- **`Plugins/Android/`** - Java code for native Stash Dialog.

### Sample
Start Here, Example implementation demonstrating API usage:
- **`StashPaySample.cs`** - Shows `OpenCheckout()` and `OpenOptin()` with web request integration
- **`StashPaySample.unity`** - Sample scene with buttons to try checkout.

## Usage

### Opening a Checkout

Use `OpenURL()` to display a Stash Pay checkout in a native card dialog:

```csharp
using StashPopup;

public class MyStore : MonoBehaviour
{
    void PurchaseItem(string checkoutUrl)
    {
        // checkoutUrl is a Stash Pay URL generated on your game backend.
        StashPayCard.Instance.OpenURL(
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


### Forcing Web View Mode

Stash Popup can also force Web View Mode (SFSafariViewController on iOS, Chrome Custom Tabs on Android) instead of in-app WebView. You can either force this in code or later remotly via Stash Studio for specific segments.

```csharp
void OpenInBrowser(string url)
{
    // Enable browser mode
    StashPayCard.Instance.ForceWebBasedCheckout = true;
    
    // Opens in Safari/Chrome instead of card
    StashPayCard.Instance.OpenURL(url, OnDismiss, OnSuccess, OnFailure);
    
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

**`OpenURL(string url, Action onDismiss, Action onSuccess, Action onFailure)`**
Opens Stash Pay checkout in a sliding card from the bottom of the screen.

**`OpenPopup(string url, Action onDismiss, Action onSuccess, Action onFailure)`**
Opens Stash opt-in and other remote Stash dialogs in a centered modal popup.

**`ResetPresentationState()`**
Dismisses current dialog and resets state.

### Properties

**`ForceWebBasedCheckout`** (bool)
- `false` - Use Stash Pay native card (default)
- `true` - Use SFSafariViewController/Chrome Custom Tabs for checkout.

**`IsCurrentlyPresented`** (bool, read-only)
- Returns whether a dialog is currently open.

## Support

- Documentation: https://docs.stash.gg
- Email: developers@stash.gg

---

Copyright © 2024 Stash. All rights reserved.
