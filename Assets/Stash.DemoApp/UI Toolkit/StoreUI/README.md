# Mobile-Responsive In-Game Store UI with Real Currency

A simple and customizable in-game store interface built with Unity UI Toolkit, designed to be responsive for mobile screens and use real currency (USD) for purchases.

## Features

- Clean, modern UI design
- Mobile-responsive layout
- 4 customizable store items with real currency prices
- Simulated purchase flow with success/failure states
- Visual feedback for purchase states (loading, success, failure)
- Easy to integrate with platform-specific IAP systems
- Fully customizable through USS styles

## Setup Instructions

1. **Add UI Document**:
   - Create a new GameObject in your scene
   - Add a `UIDocument` component to it
   - Set the Source Asset to `StoreUI.uxml`
   - Set the Panel Settings reference (use existing or create new)

2. **Add Controller**:
   - Add the `StoreUIController` script to the same GameObject
   - Assign the UIDocument reference
   - Add item images (optional)

3. **Implementation**:
   - Use the `StoreExample` script as a reference for implementing the store in your game
   - Subscribe to the `OnPurchaseCompleted` event to handle purchases
   - Integrate with your platform's IAP system (e.g., Unity IAP)

## Integration with IAP Systems

The `StoreUIController` includes a simulated purchase flow that you can replace with actual IAP implementation:

1. Modify the `ProcessPurchase` method to call your IAP manager
2. Update the item IDs in the `itemIds` list to match your IAP product IDs
3. Implement purchase verification and item granting in your game

## Customization

### Modifying Items

To modify store items, edit the `StoreUI.uxml` file. Each item follows this structure:

```xml
<VisualElement name="item-1" class="store-item">
    <VisualElement name="item-1-image" class="item-image" />
    <VisualElement class="item-details">
        <Label text="Item Name" class="item-name" />
        <Label text="Item Description" class="item-description" />
        <VisualElement class="price-container">
            <Label text="$X.XX" class="price-value" />
        </VisualElement>
    </VisualElement>
    <Button text="BUY" name="buy-button-1" class="buy-button" />
</VisualElement>
```

### Styling

Modify the `StoreUI.uss` file to change colors, sizes, fonts, and other visual properties.

### Functionality

Extend the `StoreUIController.cs` script to add additional functionality:
- Connect to your preferred payment provider or IAP system
- Implement product verification and receipt validation
- Add animations, sounds, or other feedback
- Integrate with your game's inventory system

## Mobile Responsiveness

The UI automatically adjusts for smaller screens (below 600px width):
- Header adapts to smaller size
- Items stack vertically
- Button sizes increase for better touch interaction

## Requirements

- Unity 2020.1 or newer
- UI Toolkit package
- For actual purchases: Unity IAP or platform-specific IAP integration 