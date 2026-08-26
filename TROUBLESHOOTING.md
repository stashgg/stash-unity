# Stash for Unity — Troubleshooting

Build and integration fixes for the [Stash for Unity](README.md) package. For setup and API usage, see the main README.

### [iOS] Build Error in Xcode: Undefined symbol

While highly unlikely, however if this happens add frameworks in Unity Project Settings → iOS → Other Settings → Linked Frameworks:
- `WebKit.framework`
- `SafariServices.framework`

Clean and rebuild Xcode project.

### [iOS] App crashes with "Library not loaded related to StashNative"

The app is linked with StashNative, but the framework has not been embedded in the app bundle.
In the Unity Editor, select `StashNative.xcframework` file and make sure "Add to embedded binaries" is enabled in Inspector panel.

Or fix it in Xcode project:
1. Open the Unity-generated Xcode project (e.g. after **File → Build Settings → iOS → Build**).
2. Select the **Unity-iPhone** (main app) target in the project navigator.
3. Open the **General** tab and scroll to **Frameworks, Libraries, and Embedded Content**.
4. If **StashNative.framework** is missing, click **+** and add it from the project (it should appear under Frameworks or Plugins/iOS). If it is already listed, set it to **Embed & Sign**.

Ensure `StashNative.xcframework` is present in `Packages/gg.stash.unity/Plugins/iOS/` before building from Unity so the post-process can add it to the main target’s embed phase.

### [Windows] Checkout does not open, "WebView2 Runtime is not installed" in the log

The Windows host needs the WebView2 Evergreen runtime on the player's machine. It is preinstalled on Windows 11 and on updated Windows 10; on a machine without it, `OpenCard` / `OpenModal` report `OnNetworkError` (and an `error` line in the log). Ship the [Evergreen bootstrapper](https://developer.microsoft.com/microsoft-edge/webview2/) with your installer, or fall back to `OpenBrowser`.

### [Windows] "StashNativeDesktop.dll not found" in the editor or player

The DLL lives in `Packages/gg.stash.unity/Plugins/Windows/x86_64/` and must be enabled for Standalone Win64 and the Windows editor in its import settings (it is by default). In a player it is copied next to the executable under `<Game>_Data/Plugins/x86_64/`. If a custom build step strips plugins, add it back.

### [Windows] Editor cannot rebuild or update the package: "StashNativeDesktop.dll is being used by another process"

The editor holds the DLL once loaded. Close the Unity editor before replacing the file (for example when running `Tools/sync-desktop-binaries.sh`).

### [macOS] "StashNativeDesktop.bundle not found" in a built player

Unity copies native plugins to `<App>.app/Contents/PlugIns/`. The bundle's import settings must enable Standalone OSX (they do by default). If a post-build step re-signs or re-packages the app, keep `Contents/PlugIns/StashNativeDesktop.bundle` in place.

### [macOS] Gatekeeper or notarization complaints about the bundle

The bundle inside the package is unsigned; signing applies to the hosting app. When you sign and notarize your player, the bundle in `Contents/PlugIns` is signed as part of the app (`codesign --deep` or your own per-file signing) and passes notarization. Ad-hoc signed development builds run locally without extra steps.

### [Desktop] The card stays dark / the page never loads in the editor

The editor presents the checkout through the same host as the players in a standalone window. A `networkError` after about 15 seconds means the page could not be reached; `http://` URLs are refused (checkout must be `https://`). Use the Simulate buttons in the test window to exercise callbacks without a page.

### [Android] Bridge does not compile

The Unity bridge expects the StashNative AAR to expose `StashNative` and related classes. If your AAR uses a different Java package than `com.stash.stashnative`, update the fully qualified class names in `StashNativeCardUnityBridge.java` to match the AAR.

### [Android] Blank card

Ensure internet permission in your AndroidManifest.xml.

### [Android] System browser used instead of in-app Chrome Custom Tabs

When using browser mode, some Unity projects launch Chrome Custom Tabs while others fall back to a system browser window. (This may be due to differences in Android dependencies between Unity versions.) While both flows are valid, Chrome Custom Tabs generally provide a superior experience. If you notice your app is not using Chrome Custom Tabs, you can resolve this by including the [AndroidX Browser library (`androidx.browser:browser`)](https://developer.android.com/jetpack/androidx/releases/browser), which supports [Android Custom Tabs](https://developer.android.com/develop/ui/views/layout/webapps/overview-of-android-custom-tabs).

1. **Enable Custom Gradle Template** in Unity:
   - Go to **Edit > Project Settings > Player**
   - Select **Android** tab
   - Scroll to **Publishing Settings**
   - Check **Custom Main Gradle Template**
   - Unity will create `Assets/Plugins/Android/mainTemplate.gradle`

2. **Add the dependency** to `Assets/Plugins/Android/mainTemplate.gradle`:
   - Open the file and find the `dependencies` block
   - Add: `implementation 'androidx.browser:browser:1.7.0'` (or newer, e.g. `1.9.0`)

Example:

```gradle
dependencies {
    implementation fileTree(dir: 'libs', include: ['*.jar'])
    implementation 'androidx.browser:browser:1.7.0'
**DEPS**}
```

> **Note:** Stash Popup will automatically detect if Chrome Custom Tabs is available in the Android bundle and fall back gracefully to the default browser if not.
