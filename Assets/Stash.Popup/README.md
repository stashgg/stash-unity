
<p align="center">
  <img src="https://i.ibb.co/RRfh8df/2025-05-27-13-33-27-High-Res-Screenshot-portrait.png" width="70%" />
</p>

# Stash Pay Popup

A Unity plugin that provides a customizable card-style popup for Stash Pay. The plugin uses native WebKit implementation on iOS to provide a native looking
IAP experience using Stash payment rails. Currently available for iOS devices, with a browser fallback on other platforms.

## Requirements

- Unity 2019.4 or later
- iOS 12.0 or later (for iOS builds)
- Xcode 11.0 or later (for iOS builds)

Note: On legacy devices (Below iOS 12) the popup falls back to the system browser.

## Installation

1. Import the `Stash.Popup` folder into your Unity project.
2. The plugin will be automatically included in your iOS build.

## Usage

### Basic Setup

For detailed instructions on creating Stash Pay checkout links, consult the Stash documentation.

1. Request payment link on the game backend and send the resulting link to the game client.
2. The plugin provides a singleton `StashPayCard` class that handles all interactions with the popup card. Here's how to use it:

```csharp
using StashPopup;

public class YourClass : MonoBehaviour
{  
    void OpenPaymentCard()
    {
        // Open the Stash Pay URL coming from the backend in the card.
        // Card offers two callbacks - on dismiss and on successful payment.
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

3. If the payment is successful, validate the purchase using the Stash API on the game backend.

### Platform Support

- **iOS**: Full native implementation using WebKit.
- **Unity Editor & Other Platforms**: Falls back to opening URL in the default browser.

Android support coming soon.

## Troubleshooting

### WebKit Build Issues

If you encounter build errors related to WebKit, ensure that:
1. WebKit.framework is included in your Unity project's iOS build settings
2. The framework is properly linked in your Xcode project

The plugin includes an editor script that automatically adds WebKit.framework to your iOS build settings. If you're experiencing issues:
1. Check if the Stash.Popup folder is properly imported in your project
2. Verify that the editor script is not being stripped from your build
3. If issues persist, manually add WebKit.framework in Unity's iOS build settings


