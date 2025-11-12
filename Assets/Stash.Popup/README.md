# Stash.Popup

Unity plugin for integrating Stash Pay checkout flows using native WebViews on iOS and Android.

## Requirements

- Unity 2019.4+
- iOS 12.0+ / Android API 21+

## Installation

Import the `Stash.Popup` folder into your Unity project's Assets directory.

## Usage

### Opening a Checkout

Use `OpenURL()` to display a Stash Pay checkout in a native card dialog:

```csharp
using StashPopup;

public class MyStore : MonoBehaviour
{
    void PurchaseItem(string checkoutUrl)
    {
        StashPayCard.Instance.OpenURL(
            checkoutUrl,
            dismissCallback: OnCheckoutDismissed,
            successCallback: OnPaymentSuccess,
            failureCallback: OnPaymentFailure
        );
    }
    
    void OnCheckoutDismissed()
    {
        // User closed the dialog - verify purchase status on backend
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

### Opening a Popup

Use `OpenPopup()` for centered modal dialogs (channel selection, settings, etc.):

```csharp
void ShowPaymentOptions()
{
    StashPayCard.Instance.OpenPopup(
        "https://your-site.com/payment-selection",
        dismissCallback: () => Debug.Log("Popup closed"),
        successCallback: null,  // Optional
        failureCallback: null   // Optional
    );
}
```

**Use popup for:** Non-payment flows like payment method selection or account settings.

### Forcing Web View Mode

Force native browser (Safari on iOS, Chrome Custom Tabs on Android) instead of in-app WebView:

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

**Use web view mode when:**
- Testing third-party payment integrations
- Debugging payment flows
- User preference for external browser

## Events

Subscribe to events for advanced integration:

```csharp
void OnEnable()
{
    StashPayCard.Instance.OnPaymentSuccess += HandleSuccess;
    StashPayCard.Instance.OnOptinResponse += HandlePaymentMethodSelection;
    StashPayCard.Instance.OnPageLoaded += HandlePageLoad;
}

void OnDisable()
{
    if (StashPayCard.Instance != null)
    {
        StashPayCard.Instance.OnPaymentSuccess -= HandleSuccess;
        StashPayCard.Instance.OnOptinResponse -= HandlePaymentMethodSelection;
        StashPayCard.Instance.OnPageLoaded -= HandlePageLoad;
    }
}

void HandlePaymentMethodSelection(string method)
{
    // Receives "native_iap" or "stash_pay"
    PlayerPrefs.SetString("PaymentMethod", method.ToUpper());
}

void HandlePageLoad(double loadTimeMs)
{
    Debug.Log($"Page rendered in {loadTimeMs}ms");
}
```

### Available Events

| Event | Type | Description |
|-------|------|-------------|
| `OnSafariViewDismissed` | `Action` | Card dismissed |
| `OnPaymentSuccess` | `Action` | Payment completed |
| `OnPaymentFailure` | `Action` | Payment failed |
| `OnOptinResponse` | `Action<string>` | Payment method selected ("native_iap" or "stash_pay") |
| `OnPageLoaded` | `Action<double>` | Page loaded (ms) |

## JavaScript Bridge

Your web pages can communicate with Unity via these injected functions:

```javascript
// Notify payment success
window.stash_sdk.onPaymentSuccess({});

// Notify payment failure
window.stash_sdk.onPaymentFailure({});

// Send payment method selection
window.stash_sdk.setPaymentChannel("native_iap"); // or "stash_pay"
```

## Complete Example

```csharp
using UnityEngine;
using StashPopup;
using Stash.Core;

public class StoreController : MonoBehaviour
{
    private string apiKey = "your-stash-api-key";
    private string currentCheckoutId;
    
    void Start()
    {
        StashPayCard.Instance.OnPaymentSuccess += OnPaymentSuccess;
    }
    
    void OnDestroy()
    {
        if (StashPayCard.Instance != null)
        {
            StashPayCard.Instance.OnPaymentSuccess -= OnPaymentSuccess;
        }
    }
    
    public async void BuyItem(string itemId, decimal price)
    {
        var item = new StashCheckout.CheckoutItemData
        {
            id = itemId,
            pricePerItem = price.ToString("F2"),
            quantity = 1
        };
        
        var (url, checkoutId) = await StashCheckout.CreateCheckoutLink(
            userId: GetUserId(),
            email: GetUserEmail(),
            shopHandle: "your-shop",
            item: item,
            apiKey: apiKey,
            environment: StashEnvironment.Production
        );
        
        currentCheckoutId = checkoutId;
        
        StashPayCard.Instance.OpenURL(
            url,
            dismissCallback: () => VerifyPurchase(checkoutId),
            successCallback: () => VerifyPurchase(checkoutId),
            failureCallback: () => ShowError("Purchase failed")
        );
    }
    
    void OnPaymentSuccess()
    {
        Debug.Log("Payment completed");
    }
    
    void VerifyPurchase(string checkoutId)
    {
        // Always verify on backend before granting items
        StartCoroutine(VerifyOnBackend(checkoutId));
    }
    
    IEnumerator VerifyOnBackend(string checkoutId)
    {
        string url = $"https://api.stash.gg/sdk/checkout_links/order/{checkoutId}";
        
        using (var request = UnityWebRequest.PostWwwForm(url, ""))
        {
            request.SetRequestHeader("X-Stash-Api-Key", apiKey);
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                // Parse response and grant items
                ProcessPurchase(request.downloadHandler.text);
            }
        }
    }
    
    void ProcessPurchase(string response) { /* Your logic */ }
    void ShowError(string msg) { /* Your logic */ }
    string GetUserId() { return "user123"; }
    string GetUserEmail() { return "user@example.com"; }
}
```

## Platform Behavior

| Platform | Default | ForceWebBasedCheckout = true |
|----------|---------|------------------------------|
| iOS | WKWebView card | SFSafariViewController |
| Android | WebView card | Chrome Custom Tabs |
| Editor | System browser | System browser |

## Best Practices

### Always Verify Purchases

```csharp
void OnPaymentSuccess()
{
    // ✅ CORRECT: Verify before granting
    VerifyPurchaseOnBackend(checkoutId);
    
    // ❌ WRONG: Never grant without verification
    // GrantItemsImmediately();
}
```

### Handle All Cases

```csharp
StashPayCard.Instance.OpenURL(
    url,
    dismissCallback: () => {
        // User might have paid and closed - verify anyway
        VerifyPurchaseStatus();
    },
    successCallback: () => {
        // Success detected - still verify on backend
        VerifyAndGrant();
    },
    failureCallback: () => {
        // Definite failure - show error
        ShowErrorMessage();
    }
);
```

### Unsubscribe from Events

```csharp
void OnDestroy()
{
    if (StashPayCard.Instance != null)
    {
        StashPayCard.Instance.OnPaymentSuccess -= YourHandler;
    }
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
3. Test on real device (not emulator)

### Callbacks Not Firing

- Test on real device (callbacks don't work in Unity Editor)
- Check native logs (Xcode Console / Android Logcat)
- Verify your web page calls the correct JavaScript functions

## API Reference

### Methods

**`OpenURL(string url, Action onDismiss, Action onSuccess, Action onFailure)`**
Opens URL in a sliding card from the bottom of the screen.

**`OpenPopup(string url, Action onDismiss, Action onSuccess, Action onFailure)`**
Opens URL in a centered modal popup.

**`ResetPresentationState()`**
Dismisses current dialog and resets state.

### Properties

**`ForceWebBasedCheckout`** (bool)
- `false` - Use native card WebView (default)
- `true` - Use Safari/Chrome Custom Tabs

**`IsCurrentlyPresented`** (bool, read-only)
- Returns whether a dialog is currently open

## Support

- Documentation: https://docs.stash.gg
- Email: support@stash.gg

---

Copyright © 2024 Stash. All rights reserved.
