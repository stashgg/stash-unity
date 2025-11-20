# StashPayCard Editor Testing Plugin 

This Unity Editor extension enables you to fully test StashPayCard popups and checkout dialogs directly in the Unity Editor—no device build or deployment required.

When you invoke `StashPayCard.Instance.OpenPopup` or `StashPayCard.Instance.OpenCheckout` (per the usual integration approach), this editor tool automatically intercepts those calls and displays your flow in a native WebView within the Unity Editor. This allows you to interact with the Stash Pay UI, complete test purchases, and simulate callback events without leaving the Editor.

## Usage

Simply import this Editor package. Whenever your game code calls `OpenPopup` or `OpenCheckout` and this tool is present, those interactions are caught and displayed in the Editor—mirroring the device experience for end-to-end testing.

## Included Components

- **StashPayCardEditorWindow.cs** – Main editor test window (for macOS and Windows)
- **macOS/** – Contains native WKWebView integration for Mac
- **Windows/** – Contains native WebView2 integration for Windows

The `macOS` and `Windows` folders include native build scripts and pre-built plugin binaries. You do not need to run or modify these scripts—everything required is already included for immediate use in the Editor.

## Platform Support

**Note for Windows users:** This editor extension requires the Microsoft Edge WebView2 Runtime to function. Most modern Windows systems already have it installed, but if you encounter problems, you can download and install it directly from the [Microsoft Edge WebView2 official download site](https://developer.microsoft.com/en-us/microsoft-edge/webview2/?form=MA13LH).

- **macOS:** Utilizes native WKWebView for rendering
- **Windows:** Utilizes Microsoft Edge WebView2
- **Other platforms:** Not supported (for Linux or other OS, use an actual device to test)

