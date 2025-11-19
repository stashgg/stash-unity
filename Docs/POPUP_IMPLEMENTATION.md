# Stash Pay - Native Checkout Plugins

## Why Plugins 

This SDK uses native platform plugins (iOS/Android) for several reasons:

### Security and Compliance
- **Compliance**: Payment processing occurs entirely in Android/iOS native WebViews, isolating sensitive data from Unity's managed environment. Only very thin messaging layer (for states) is implemented.
- **Platform Security**: Native WebViews leverage OS-level security features (Keychain on iOS, KeyStore on Android) that Unity cannot access directly
- **Certificate Validation**: Platform WebViews handle SSL/TLS validation using system trust stores, ensuring proper certificate pinning.

### Performance
- **Native Rendering**: Platform native WebViews use hardware-accelerated rendering pipelines optimized for each OS.
- **Memory Efficiency**: Native WebViews manage their own memory pools separately from Unity's heap, preventing GC pressure
- **Web Standards**: Native WebViews support latest web APIs (WebGL, CSS Grid, modern JavaScript) that Unity WebView wrappers lag behind

### Portability & Maintanance
- **Unity Independence**: The native plugins are not tied to Unity and can be used directly in standalone iOS or Android apps if needed.
- **Stable Native APIs**: The native APIs utilized are minimal and change infrequently, making the codebase simple to maintain and update for all customers. 
- **No Project-specific Dependencies**: Native plugins have almost no reliance on Unity’s internal APIs, making upgrades painless and robust across Unity versions. Just like Apple Pay and Google Pay dialogs, our plugin leaves actual game code untouched and isolated.

## Why not in Unity directly ?

### Option A: WebView in Unity Game
Unity-embedded WebViews (different solutions) have fundamental issues:
- **Outdated Rendering**: Often based on old Chromium versions with security vulnerabilities.
- **Poor Touch Handling**: Input must pass through Unity's event system, adding latency.
- **No Platform Features**: Cannot access Apple Pay, Google Pay, or biometric authentication.
- **Memory Leaks & Pressure**: Third-party plugins often fail to properly clean up native resources.

### Option B: Purchase wrapped in API
Direct API integration (without WebView) is insufficient because:
- **3D Secure**: Requires interactive browser challenge-response flow
- **Payment Provider UI**: Many providers (PayPal, Apple Pay, Google Pay) require native UI components
- **Dynamic Flows**: Payment flows change based on user region, card type, and risk assessment
- **Regulatory Requirements**


## Stash Popup Overview

The StashPayCard SDK provides **two presentation modes** across iOS, Android, and Unity Editor:
- **Popup**: Dialog-based presentation for opt-in and other dialogs.
- **Checkout**: Card style presentation with device-specific behavior for checkouts.


## General Architecture

### Presentation Strategy
- **iOS**: Uses `UIWindow` and `UIViewController` for both popup and checkout
- **Android Popup**: Uses `Dialog` with `WebView`
- **Android Checkout**: Uses separate `Activity` (`StashPayCardPortraitActivity`)

### Communication Layer
All platforms inject `window.stash_sdk` JavaScript functions into the WebView for Unity callbacks.
This way Stash Pay frontend can communicate directly with the game and the page events get propagated to the game.

- `onPaymentSuccess()`: Payment completed successfully
- `onPaymentFailure()`: Payment failed
- `onPurchaseProcessing()`: Purchase is being processed
- `setPaymentChannel(optinType)`: Payment channel selected (Popup only)

## Unity Wrapper (`StashPayCard.cs`)

Unity interacts with the platform-specific plugins via this interface.

### Platform Abstraction
Unity uses conditional compilation and platform-specific calls to native plugins:

```csharp
#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void openPopup(string url);
    
    [DllImport("__Internal")]
    private static extern void openPopupWithSize(string url, float portraitWidth, float portraitHeight, 
                                                  float landscapeWidth, float landscapeHeight);
#elif UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject plugin;
    plugin = new AndroidJavaClass("com.stash.popup.StashPayCardPlugin").CallStatic<AndroidJavaObject>("getInstance");
    plugin.Call("openPopup", url);
#elif UNITY_EDITOR
    // Reflection-based call to editor window
#endif
```

### iOS Native Calls
- **Method**: Platform Invoke (P/Invoke) via `[DllImport("__Internal")]`
- **Functions**: Direct C function calls compiled into Unity binary
- **Callbacks**: Unity MonoPInvokeCallback functions called from Objective-C via `UnitySendMessage`

### Android Native Calls
- **Method**: JNI (Java Native Interface) via `AndroidJavaClass` and `AndroidJavaObject`
- **Functions**: `Call()` method invokes Java methods by name
- **Callbacks**: `UnityPlayer.UnitySendMessage()` from Java to Unity

### Unity Callbacks
All platforms use `UnitySendMessage(gameObjectName, methodName, parameter)` to trigger Unity events:
- **GameObject**: `"StashPayCard"` (must exist in scene)
- **Methods**: 
  - `OnAndroidPaymentSuccess()` / `OnIOSPaymentSuccess()`
  - `OnAndroidPaymentFailure()` / `OnIOSPaymentFailure()`
  - `OnAndroidOptinResponse(string)` / `OnIOSOptinResponse(string)`
  - `OnAndroidDialogDismissed()` / `OnIOSDialogDismissed()`

### C# Event Pattern
Unity callbacks are converted to C# events:

```csharp
public static event Action OnPaymentSuccess;
public static event Action OnPaymentFailure;
public static event Action<string> OnOptinResponse;
public static event Action OnSafariViewDismissed;

[MonoPInvokeCallback(typeof(Action))]
private static void OnIOSPaymentSuccess() {
    OnPaymentSuccess?.Invoke();
}
```

## iOS Implementation (`StashPayCardURLHandler.mm`)

### Popup 
- **Presentation**: Creates dedicated [`UIWindow`](https://developer.apple.com/documentation/uikit/uiwindow) with transparent background, centered [`UIViewController`](https://developer.apple.com/documentation/uikit/uiviewcontroller)
- **Rotation**: Full rotation support ([`UIInterfaceOrientationMaskAll`](https://developer.apple.com/documentation/uikit/uiinterfaceorientationmask))
- **Sizing**: Dynamically calculated based on screen dimensions and orientation
  - Portrait: 85% width, 112.5% height of base size
  - Landscape: 127% width, 90% height of base size
- **Animation**: Fade-in with scale (0.9 → 1.0) over 200ms using [`UIView.animate`](https://developer.apple.com/documentation/uikit/uiview/1622418-animate)
- **Dismissal**: Fade-out animation, window and view controller cleanup
- **Layout Updates**: [`viewWillLayoutSubviews`](https://developer.apple.com/documentation/uikit/uiviewcontroller/1621437-viewwilllayoutsubviews) handles orientation changes

### Checkout Card
- **Presentation**: Uses dedicated [`UIWindow`](https://developer.apple.com/documentation/uikit/uiwindow) with card-style view controller
- **iPhone**: 
  - Enforce portrait orientation ([`UIInterfaceOrientationMaskPortrait`](https://developer.apple.com/documentation/uikit/uiinterfaceorientationmask))
  - Bottom-aligned card (68% screen height)
  - Slide-up animation from bottom using [`CGAffineTransform`](https://developer.apple.com/documentation/corefoundation/cgaffinetransform)
  - Drag-to-dismiss and drag-to-expand gestures
- **iPad**:
  - Rotation support enabled
  - Centered card (70% width, max 600pt, 68% height)
  - Slide-up animation to center
  - Drag-to-dismiss only (no expand gesture)
  - Seamless rotation with frame recalculation in [`viewWillLayoutSubviews`](https://developer.apple.com/documentation/uikit/uiviewcontroller/1621437-viewwilllayoutsubviews)

### WebView Setup
- **WebView**: [`WKWebView`](https://developer.apple.com/documentation/webkit/wkwebview) with JavaScript enabled via [`WKWebViewConfiguration`](https://developer.apple.com/documentation/webkit/wkwebviewconfiguration)
- **Script Injection**: [`WKUserScript`](https://developer.apple.com/documentation/webkit/wkuserscript) injected at document start for `window.stash_sdk`
- **Message Handlers**: [`WKScriptMessageHandler`](https://developer.apple.com/documentation/webkit/wkscriptmessagehandler) via [`WKUserContentController`](https://developer.apple.com/documentation/webkit/wkusercontentcontroller) for callback interception
- **Scrolling**: Enabled for card, Disabled for popup mode via JavaScript injection

### Memory Management
- **Cleanup**: `cleanupCardInstance()` removes all [`objc_setAssociatedObject`](https://developer.apple.com/documentation/objectivec/1418509-objc_setassociatedobject) references, delegates, and views
- **Callback Pattern**: `callUnityCallbackOnce` block pattern with `__weak` references to prevent retain cycles
- **View Hierarchy**: Explicit [`removeFromSuperview`](https://developer.apple.com/documentation/uikit/uiview/1622421-removefromsuperview) and `nil` assignments


## Android Implementation

### Popup (`StashPayCardPlugin.java`)
- **Presentation**: [`Dialog`](https://developer.android.com/reference/android/app/Dialog) with `Theme_Translucent_NoTitleBar_Fullscreen`
- **Container**: [`FrameLayout`](https://developer.android.com/reference/android/widget/FrameLayout) centered with [`Gravity.CENTER`](https://developer.android.com/reference/android/view/Gravity)
- **Rotation**: Full rotation support via [`ViewTreeObserver.OnGlobalLayoutListener`](https://developer.android.com/reference/android/view/ViewTreeObserver.OnGlobalLayoutListener)
- **Sizing**: Dynamically calculated based on screen dimensions and orientation
  - Same multipliers as iOS (portrait: 85%/112.5%, landscape: 127%/90%)
  - Base size: 300dp (phone), 400dp (tablet)
- **Animation**: Fade-in with scale transformation (0.9 → 1.0) over 200ms using [`View.animate()`](https://developer.android.com/reference/android/view/View#animate())
- **Rotation Animation**: Scale down to 0.95, resize, scale back to 1.0
- **Dismissal**: Fade-out animation, cleanup via `cleanupAllViews()`
- **Rounded Corners**: 20dp radius with [`GradientDrawable`](https://developer.android.com/reference/android/graphics/drawable/GradientDrawable) and [`ViewOutlineProvider`](https://developer.android.com/reference/android/view/ViewOutlineProvider)

### Checkout (`StashPayCardPortraitActivity.java`)
- **Presentation**: Separate transparent [`Activity`](https://developer.android.com/reference/android/app/Activity) with `Theme.Translucent.NoTitleBar.Fullscreen`
- **Phone**:
  - Forced portrait orientation ([`SCREEN_ORIENTATION_PORTRAIT`](https://developer.android.com/reference/android/content/pm/ActivityInfo#SCREEN_ORIENTATION_PORTRAIT))
  - Bottom-aligned card (`MATCH_PARENT` width, 68% height)
  - Slide-up animation from bottom
  - Drag-to-dismiss and drag-to-expand gestures via [`OnTouchListener`](https://developer.android.com/reference/android/view/View.OnTouchListener)
- **Tablet**:
  - Full sensor rotation ([`SCREEN_ORIENTATION_FULL_SENSOR`](https://developer.android.com/reference/android/content/pm/ActivityInfo#SCREEN_ORIENTATION_FULL_SENSOR))
  - Centered card (70% width, max 600dp, 68% height)
  - Fade-in animation
  - Drag-to-dismiss only (no expand)
  - UI recreation in [`onConfigurationChanged`](https://developer.android.com/reference/android/app/Activity#onConfigurationChanged(android.content.res.Configuration)) for seamless rotation
- **Rounded Corners**: 
  - Phone: Top corners only (25dp radius)
  - Tablet: All corners (25dp radius)
- **Window Flags**: [`FLAG_NOT_TOUCH_MODAL`](https://developer.android.com/reference/android/view/WindowManager.LayoutParams#FLAG_NOT_TOUCH_MODAL), [`FLAG_WATCH_OUTSIDE_TOUCH`](https://developer.android.com/reference/android/view/WindowManager.LayoutParams#FLAG_WATCH_OUTSIDE_TOUCH), [`FLAG_DIM_BEHIND`](https://developer.android.com/reference/android/view/WindowManager.LayoutParams#FLAG_DIM_BEHIND) to keep Unity running

### WebView Setup
- **WebView**: Android [`WebView`](https://developer.android.com/reference/android/webkit/WebView) with JavaScript and DOM storage enabled
- **Settings**: [`WebSettings`](https://developer.android.com/reference/android/webkit/WebSettings) with `setLoadWithOverviewMode(true)`, `setUseWideViewPort(true)` for proper rendering
- **JavaScript Interface**: [`addJavascriptInterface`](https://developer.android.com/reference/android/webkit/WebView#addJavascriptInterface(java.lang.Object,%20java.lang.String))`(new StashJavaScriptInterface(), "StashAndroid")`
- **Script Injection**: [`evaluateJavascript()`](https://developer.android.com/reference/android/webkit/WebView#evaluateJavascript(java.lang.String,%20android.webkit.ValueCallback%3Cjava.lang.String%3E)) to inject `window.stash_sdk` functions
- **Scrolling**: Enabled for card, disabled for popup mode via JavaScript injection

### Memory Management
- **Activity Lifecycle**: [`onDestroy()`](https://developer.android.com/reference/android/app/Activity#onDestroy()) calls [`webView.destroy()`](https://developer.android.com/reference/android/webkit/WebView#destroy()) and nullifies references
- **Dialog Cleanup**: `cleanupAllViews()` removes all views, listeners, and WebView
- **Listener Removal**: [`removeOnGlobalLayoutListener()`](https://developer.android.com/reference/android/view/ViewTreeObserver#removeOnGlobalLayoutListener(android.view.ViewTreeObserver.OnGlobalLayoutListener)) for orientation listener


## Key Platform Differences

### Rotation Handling
- **iOS Popup**: `viewWillLayoutSubviews` with frame recalculation
- **iOS Checkout iPad**: `viewWillLayoutSubviews` with conditional logic
- **Android Popup**: `ViewTreeObserver.OnGlobalLayoutListener` with animated resize
- **Android Checkout Tablet**: `onConfigurationChanged` with UI recreation
- **Unity Editor**: Window remains fixed size

### Gesture Handling
- **iOS**: `UIPanGestureRecognizer` with custom `gestureRecognizerShouldBegin`
- **Android**: `OnTouchListener` with `MotionEvent` handling
- **Unity Editor**: N/A (simulation buttons only)

### Dismissal
- **iOS**: Animates view controller, calls Unity callback once, destroys window
- **Android Popup**: Animates dialog container, dismisses dialog, triggers `OnDismissListener`
- **Android Checkout**: Activity finish with animation, `onDestroy` triggers Unity callback
- **Unity Editor**: Destroys native window, triggers C# callback via notification queue

## Technical Constraints

### Android
- **Activity Approach**: Required for checkout to handle complex gestures and rotation reliably
- **Dialog Approach**: Used for popup to avoid blocking app rotation
- **WebView Context**: Must use [`Activity`](https://developer.android.com/reference/android/app/Activity) context, not `Application` context
- **Manifest Entry**: `StashPayCardPortraitActivity` requires [`configChanges`](https://developer.android.com/guide/topics/manifest/activity-element#config)`="orientation|screenSize|keyboardHidden"`

### iOS
- **Window Management**: Separate [`UIWindow`](https://developer.apple.com/documentation/uikit/uiwindow) required to overlay Unity view
- **Gesture Blocking**: iPad requires explicit gesture blocking in [`gestureRecognizerShouldBegin`](https://developer.apple.com/documentation/uikit/uigesturerecognizerdelegate/1624213-gesturerecognizershouldbegin)
- **Animation Timing**: [`CATransaction`](https://developer.apple.com/documentation/quartzcore/catransaction) with [`setDisableActions:YES`](https://developer.apple.com/documentation/quartzcore/catransaction/1448255-setdisableactions) to prevent unwanted animations
- **Memory**: ARC requires careful weak/strong self pattern in blocks to prevent retain cycles