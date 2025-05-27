# Stash.Popup

A Unity plugin that provides a customizable card-style popup for Stash Pay. The plugin uses native WebKit implementation on iOS to provide a native looking
IAP experience using Stash payment rails. Currently available for iOS devices.

## Requirements

- Unity 2019.4 or later
- iOS 9.0 or later (for iOS builds)
- Xcode 11.0 or later (for iOS builds)

## Installation

1. Import the `Stash.Popup` folder into your Unity project.
2. The plugin will be automatically included in your iOS build.

## Usage

### Basic Setup

The plugin provides a singleton `StashPayCard` class that handles all interactions with the popup card. Here's how to use it:

The general flow:

1. Request payment link on the game backend and forward the resulting link to the game client.
2. Show the payment URL using the StashPayCard

```csharp
using StashPopup;

public class YourClass : MonoBehaviour
{  
    void OpenPaymentCard()
    {
        // Open a URL in the card popup
        StashPayCard.Instance.OpenURL("STASH_PAY_URL", OnStashPayDismissed, OnStashPaySuccess);
    }
    
    void OnStashPayDismissed()
    {
        Debug.Log("Card was dismissed.");
        // Do nothing.
    }
    
    void OnStashPaySuccess()
    {
        Debug.Log("Payment was successful.");
        // Verify purchase using Stash API & grant purchase to the user.
    }
}
```

3. If the payment is successfull, validate the purchase using the Stash API on the game backend.

### Events

The plugin provides two main events:

1. `OnSafariViewDismissed`: Triggered when the card is dismissed.
2. `OnPaymentSuccess`: Triggered when a payment is successful.```

### Platform Support

- **iOS**: Full native implementation using WebKit.
- **Unity Editor & Other Platforms**: Falls back to opening URL in the default browser.

