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

### [Android] Bridge does not compile

The Unity bridge expects the StashNative AAR to expose `StashNative` and related classes. If your AAR uses a different Java package than `com.stash.stashnative`, update the fully qualified class names in `StashNativeCardUnityBridge.java` to match the AAR.

### [Android] Blank card

Ensure internet permission in your AndroidManifest.xml.

### [Android] Crash when enabling keep-alive: `NoSuchMethodError` on `ServiceCompat.startForeground`

The Stash Native foreground service uses **`ServiceCompat.startForeground(Service, int, Notification, int)`** (foreground service type). That overload exists only in **newer AndroidX Core** artifacts. Unity builds often end up with **`androidx.core:core:1.2.0`** (or similar) from old transitive dependencies, so the class loads but the method is missing and the app crashes on the main thread.

**Fix:** Force a modern Core into the app (pick **1.12.0** or newer, e.g. **1.13.1**):

1. Enable **Custom Main Gradle Template** (same as in the Custom Tabs section below) so you have `Assets/Plugins/Android/mainTemplate.gradle`.
2. Inside the `dependencies { }` block, **before** the `**DEPS**` line, add:

```gradle
    implementation 'androidx.core:core:1.13.1'
```

3. If you use **External Dependency Manager (EDM)**, add `androidx.core:core:1.13.1` to your Android dependencies (or a `*Dependencies.xml` file) and run **Assets → External Dependency Manager → Android Resolver → Force Resolve** so the old `androidx.core.core-1.2.0.aar` in `Assets/Plugins/Android` is replaced.

Rebuild the APK and confirm the merged libraries no longer ship an ancient `core` only.

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

Example (Custom Tabs + recommended Core for keep-alive):

```gradle
dependencies {
    implementation fileTree(dir: 'libs', include: ['*.jar'])
    implementation 'androidx.core:core:1.13.1'
    implementation 'androidx.browser:browser:1.9.0'
**DEPS**}
```

> **Note:** Stash Popup will automatically detect if Chrome Custom Tabs is available in the Android bundle and fall back gracefully to the default browser if not.
