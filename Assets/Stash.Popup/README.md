# Stash.Popup

A Unity plugin for iOS that provides a customizable card-style popup for web content, with a focus on payment flows and secure web interactions.

## Features

- **Card UI**: Present web content in a card-style popup with optimized default settings
- **Responsive Design**: Automatically adapts to different screen sizes and orientations
- **iPad Support**: Special handling for iPad with iPhone-like aspect ratio and centering
- **Interactive Gestures**: 
  - Drag to expand/collapse
  - Swipe to dismiss
  - Tap outside to dismiss
- **Keyboard Awareness**: Automatically expands when keyboard appears
- **Security Features**:
  - Disabled text selection
  - Disabled image saving
  - Disabled context menus
  - Secure web content handling
- **Loading Animation**: Custom loading screen with animated logo
- **Payment Flow Support**: Special handling for payment success URLs
- **Dark Mode Support**: Automatically adapts to system dark/light mode

## Requirements

- Unity 2019.4 or later
- iOS 9.0 or later
- Xcode 11.0 or later

## Installation

1. Import the `Stash.Popup` folder into your Unity project's `Assets` folder
2. The plugin will be automatically included in your iOS build

## Usage

### Basic Setup

```csharp
using Stash.Popup;

public class YourClass : MonoBehaviour
{
    void Start()
    {
        // Set up callbacks
        StashPayCard.SetSafariViewDismissedCallback(OnSafariViewDismissed);
        StashPayCard.SetPaymentSuccessCallback(OnPaymentSuccess);
    }
    
    void OpenPaymentCard()
    {
        // Open a URL in the card popup
        StashPayCard.OpenURLInSafariVC("STASH_PAY_URL");
    }
    
    void OnSafariViewDismissed()
    {
        Debug.Log("Card was dismissed");
    }
    
    void OnPaymentSuccess()
    {
        Debug.Log("Payment was successful");
    }
}
```

### Callbacks

The plugin provides two main callbacks:

1. `SetSafariViewDismissedCallback`: Called when the card is dismissed
2. `SetPaymentSuccessCallback`: Called when a payment is successful

### iPad Behavior

On iPad devices, the card will:
- Use an iPhone-like aspect ratio
- Be centered on screen
- Have rounded corners on all sides

## Security Notes

The plugin implements several security measures:
- Disables text selection to prevent copying sensitive information
- Disables image saving to prevent capturing payment details
- Disables context menus to prevent unwanted actions
- Uses secure web content handling for payment flows

## Support

For support or feature requests, please contact the Stash team.

## License

This plugin is proprietary software. Unauthorized copying, distribution, or use is strictly prohibited. 