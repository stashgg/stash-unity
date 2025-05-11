# Stash Checkout Demo

This demo shows how to use the Stash Checkout system to create checkout links and open them in a browser.

## Scene Setup

The demo includes the following components:

1. A UI Document that displays a form for checkout information
2. A controller script that handles the form submission and API calls
3. Styling for the UI elements
4. Runtime setup scripts to handle UI initialization

## Known Issues and Workarounds

This demo uses a runtime-based approach to handle UI Toolkit setup instead of relying on serialized YAML files, which can sometimes have issues with parsing in certain Unity environments. This approach is more robust across different Unity versions and editor setups.

## Usage

1. Open the `StashCheckoutDemo` scene
2. The UI panel settings will be generated at runtime via the `StashCheckoutSetup` script
3. Enter the required information in the form:
   - User Information (ID, Email, Display Name, Avatar URL, Profile URL)
   - Shop Information (Shop Handle, Currency, API Key)
   - Item Information (ID, Price, Quantity, Image URL, Name, Description)
4. Click the "Create Checkout Link" button to generate a checkout link
5. The checkout URL and ID will be displayed in the response section
6. Click the "Open in Browser" button to open the checkout URL in your default browser

## Technical Details

The demo uses Unity's UI Toolkit (UI Builder) for the user interface and the `StashCheckout` class to handle the checkout functionality. The controller script `StashCheckoutController.cs` connects the UI to the checkout system.

### Files

- **UI Files**:
  - `StashCheckoutUI.uxml`: UI layout
  - `StashCheckoutUI.uss`: UI styling

- **Script Files**:
  - `StashCheckoutController.cs`: Controller script to handle the UI interactions
  - `StashCheckoutSetup.cs`: Runtime setup for UI panel settings
  - `StashCheckoutBootstrap.cs`: Runtime scene setup script

## Troubleshooting

If you encounter issues:

1. **UI Not Showing**:
   - Check the console for errors
   - Make sure the UIDocument component has proper references
   - The setup scripts should generate panel settings at runtime

2. **API Issues**:
   - Check that the API key is valid
   - Verify that all required fields are filled out
   - Look at the console for detailed error messages

3. **Customization**:
   - You can edit the UXML file in the UI Builder
   - The controller script can be extended to handle multiple items or additional checkout options 