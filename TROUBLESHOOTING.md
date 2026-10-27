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

Ensure `StashNative.xcframework` is present in `Packages/gg.stash.unity/Plugins/iOS/` before building from Unity so the post-process can add it to the main target’s embed phase. The same applies to `StashNativeCodeLink.xcframework` in `Packages/gg.stash.unity.codelink/Plugins/iOS/` when that package is installed.

### [Android] Bridge does not compile

The Unity bridge (`StashNativeUnityBridge.java`) expects `com.stash.stashnative.StashNative` from `StashNative-3.0.0.aar`. A leftover 2.x AAR (`StashNative-2.x.aar`, which exposes `StashNativeCard`) in `Assets/Plugins/Android` or in the package folder causes duplicate-class or missing-class errors; remove it.

### [Android] Release build with Minify: checkout opens in Chrome, no callbacks, or `OpenScanner` reports `ScannerUnavailable`

R8 removed the Unity bridge class. The package annotates `com.stash.unity.StashNativeUnityBridge` with `@androidx.annotation.Keep`; if your ProGuard configuration strips or ignores annotations, add `-keep class com.stash.unity.** { *; }` to your user ProGuard file. Unity logs `StashNative: Android bridge ... not found` and raises `OnNativeException("InitializeAndroidPlugin")` when this happens.

### [Android] Callbacks arrive late, `OnApplicationPause` fires when a card opens

Expected. The card runs in its own activity, which pauses the Unity player. Events native raises while the card is up are queued and delivered in order after it closes. Use `autoClose = true` if you need the payment result immediately; see the README note under OpenCard.

### [Android] `NoClassDefFoundError: androidx/window/...` or `androidx/webkit/...` at runtime

Stash Native 3.0.0 requires `androidx.window:window`, `androidx.window:window-java`, `androidx.webkit:webkit` and `androidx.core:core`. Add them via EDM4U (the package ships `Editor/StashNativeDependencies.xml`) or in `mainTemplate.gradle`; see the README Installation section.

### [Android] Gradle fails in `mergeExtDexRelease` with `D8: java.lang.NullPointerException` on `camera-*-1.4.2`

CameraX 1.4.x is built with a Kotlin toolchain that the D8 dexer bundled with Unity 2022.3's Android Gradle Plugin 7.4.2 cannot read; the build also reports a `StackOverflowError`. Pin CameraX to 1.3.4 (`camera-camera2`, `camera-lifecycle`, `camera-view`), which is what the package's dependency manifest and README specify. The QR scanner uses no CameraX API newer than 1.3.0. On Unity 6 with AGP 8.x, 1.4.2 works.

### [Android/iOS] `OpenScanner` reports `ScannerUnavailable`

The optional `gg.stash.unity.codelink` package is not installed, or on Android its CameraX/ZXing dependencies are missing from the Gradle classpath. Install the package and add the four libraries listed in its README. On iOS also confirm `StashNativeCodeLink.xcframework` is embedded (same fix as for `StashNative.xcframework` above).

### [iOS] Scanner reports `MissingCameraUsageDescription`

Set **Camera Usage Description** in Player Settings → iOS → Other Settings. Unity writes it to `NSCameraUsageDescription`.

### [iOS] Build fails with deployment target errors

Stash Native 3.0.0 requires iOS 15.0. Set **Target minimum iOS Version** to 15.0 or higher in Player Settings.

### [Android/iOS] `OnCardDismissed` never fires after a successful payment

That is the 3.0 contract: with `autoClose = true` the card closes and only the success/failure callback fires. A network error ends the card with `OnNetworkError`. An external-payment handoff ends with `OnBrowserDismissed` when the browser closes, or, if the page passed `keepOpen`, with the card's own events later. Pass the per-call callbacks to `OpenCard` so every ending reaches the same cleanup; see [Callbacks](https://stashgg.github.io/stash-native/usage/callbacks.html).

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

> **Note:** Stash Native will automatically detect if Chrome Custom Tabs is available in the Android bundle and fall back gracefully to the default browser if not.
