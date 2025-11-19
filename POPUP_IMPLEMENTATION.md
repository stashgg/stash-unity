# Popup Implementation Documentation

## Overview

The StashPayCard SDK provides two presentation modes across iOS, Android, and Unity Editor:
- **Popup**: Dialog-based presentation with rotation support
- **Checkout**: Full-screen card presentation with device-specific behavior

## Architecture

### Presentation Strategy
- **iOS**: Uses `UIWindow` and `UIViewController` for both popup and checkout
- **Android Popup**: Uses `Dialog` with `WebView`
- **Android Checkout**: Uses separate `Activity` (`StashPayCardPortraitActivity`)
- **Unity Editor**: Uses native macOS `WKWebView` in separate window with IPC queue

### Communication Layer
All platforms inject `window.stash_sdk` JavaScript functions into the WebView for Unity callbacks:
- `onPaymentSuccess()`: Payment completed successfully
- `onPaymentFailure()`: Payment failed
- `setPaymentChannel(optinType)`: Payment channel selected (optin response)
- `onPurchaseProcessing()`: Purchase is being processed

## Unity Integration (`StashPayCard.cs`)

### Platform Abstraction
Unity uses conditional compilation and platform-specific P/Invoke to call native plugins:

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
- **Method**: P/Invoke via `[DllImport("__Internal")]`
- **Functions**: Direct C function calls compiled into Unity binary
- **Callbacks**: Unity MonoPInvokeCallback functions called from Objective-C via `UnitySendMessage`

### Android Native Calls
- **Method**: JNI via `AndroidJavaClass` and `AndroidJavaObject`
- **Plugin Access**: Singleton instance retrieved via `getInstance()`
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

### Editor Integration
- **Conditional Compilation**: `#elif UNITY_EDITOR` block
- **Reflection**: Uses `Type.GetType()` and `MethodInfo.Invoke()` to call editor window
- **Callbacks**: Editor window directly invokes public static methods like `OnEditorPaymentSuccess()`

## iOS Implementation (`StashPayCardURLHandler.mm`)

### Popup Mode
- **Presentation**: Creates dedicated [`UIWindow`](https://developer.apple.com/documentation/uikit/uiwindow) with transparent background, centered [`UIViewController`](https://developer.apple.com/documentation/uikit/uiviewcontroller)
- **Rotation**: Full rotation support ([`UIInterfaceOrientationMaskAll`](https://developer.apple.com/documentation/uikit/uiinterfaceorientationmask))
- **Sizing**: Dynamically calculated based on screen dimensions and orientation
  - Portrait: 85% width, 112.5% height of base size
  - Landscape: 127% width, 90% height of base size
- **Animation**: Fade-in with scale (0.9 → 1.0) over 200ms using [`UIView.animate`](https://developer.apple.com/documentation/uikit/uiview/1622418-animate)
- **Dismissal**: Fade-out animation, window and view controller cleanup
- **Layout Updates**: [`viewWillLayoutSubviews`](https://developer.apple.com/documentation/uikit/uiviewcontroller/1621437-viewwilllayoutsubviews) handles orientation changes

### Checkout Mode (Card Presentation)
- **Presentation**: Uses dedicated [`UIWindow`](https://developer.apple.com/documentation/uikit/uiwindow) with card-style view controller
- **iPhone**: 
  - Forced portrait orientation ([`UIInterfaceOrientationMaskPortrait`](https://developer.apple.com/documentation/uikit/uiinterfaceorientationmask))
  - Bottom-aligned card (68% screen height)
  - Slide-up animation from bottom using [`CGAffineTransform`](https://developer.apple.com/documentation/corefoundation/cgaffinetransform)
  - Drag-to-dismiss and drag-to-expand gestures
- **iPad**:
  - Rotation support enabled
  - Centered card (70% width, max 600pt, 68% height)
  - Slide-up animation to center
  - Drag-to-dismiss only (no expand gesture)
  - Seamless rotation with frame recalculation in [`viewWillLayoutSubviews`](https://developer.apple.com/documentation/uikit/uiviewcontroller/1621437-viewwilllayoutsubviews)
- **Rounded Corners**: Top corners only (25pt radius) using [`CAShapeLayer`](https://developer.apple.com/documentation/quartzcore/cashapelayer) and [`UIBezierPath`](https://developer.apple.com/documentation/uikit/uibezierpath)
- **Gestures**: [`UIPanGestureRecognizer`](https://developer.apple.com/documentation/uikit/uipangesturerecognizer) with custom [`gestureRecognizerShouldBegin`](https://developer.apple.com/documentation/uikit/uigesturerecognizerdelegate/1624213-gesturerecognizershouldbegin) for iPad gesture blocking

### WebView Setup
- **WebView**: [`WKWebView`](https://developer.apple.com/documentation/webkit/wkwebview) with JavaScript enabled via [`WKWebViewConfiguration`](https://developer.apple.com/documentation/webkit/wkwebviewconfiguration)
- **Script Injection**: [`WKUserScript`](https://developer.apple.com/documentation/webkit/wkuserscript) injected at document start for `window.stash_sdk`
- **Message Handlers**: [`WKScriptMessageHandler`](https://developer.apple.com/documentation/webkit/wkscriptmessagehandler) via [`WKUserContentController`](https://developer.apple.com/documentation/webkit/wkusercontentcontroller) for callback interception
- **Scrolling**: Disabled for popup mode via JavaScript injection

### Memory Management
- **Cleanup**: `cleanupCardInstance()` removes all [`objc_setAssociatedObject`](https://developer.apple.com/documentation/objectivec/1418509-objc_setassociatedobject) references, delegates, and views
- **Callback Pattern**: `callUnityCallbackOnce` block pattern with `__weak` references to prevent retain cycles
- **View Hierarchy**: Explicit [`removeFromSuperview`](https://developer.apple.com/documentation/uikit/uiview/1622421-removefromsuperview) and `nil` assignments

## Android Implementation

### Popup Mode (`StashPayCardPlugin.java`)
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

### Checkout Mode (`StashPayCardPortraitActivity.java`)
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
- **Scrolling**: Disabled for popup mode via JavaScript injection
- **Dark Mode**: [`setForceDark()`](https://developer.android.com/reference/android/webkit/WebSettings#setForceDark(int)) based on system theme (API 29+)

### Memory Management
- **Activity Lifecycle**: [`onDestroy()`](https://developer.android.com/reference/android/app/Activity#onDestroy()) calls [`webView.destroy()`](https://developer.android.com/reference/android/webkit/WebView#destroy()) and nullifies references
- **Dialog Cleanup**: `cleanupAllViews()` removes all views, listeners, and WebView
- **Listener Removal**: [`removeOnGlobalLayoutListener()`](https://developer.android.com/reference/android/view/ViewTreeObserver#removeOnGlobalLayoutListener(android.view.ViewTreeObserver.OnGlobalLayoutListener)) for orientation listener

## Unity Editor Implementation (`StashPayCardEditorWindow.cs` + macOS Bundle)

### Architecture
- **C# Editor Window**: Unity [`EditorWindow`](https://docs.unity3d.com/ScriptReference/EditorWindow.html) with simulation buttons
- **Native WebView**: Objective-C++ bundle (`WebViewLauncher.bundle`) loaded via [`dlopen`](https://developer.apple.com/library/archive/documentation/System/Conceptual/ManPages_iPhoneOS/man3/dlopen.3.html)
- **IPC**: In-memory notification queue with [`pthread_mutex`](https://developer.apple.com/library/archive/documentation/System/Conceptual/ManPages_iPhoneOS/man3/pthread_mutex_lock.3.html) protected access
  - C++ side: `QueueNotification()` adds to queue
  - C# side: `PollNotification()` polls queue in [`EditorApplication.update`](https://docs.unity3d.com/ScriptReference/EditorApplication-update.html)

### WebView Window
- **Window**: [`NSWindow`](https://developer.apple.com/documentation/appkit/nswindow) with [`WKWebView`](https://developer.apple.com/documentation/webkit/wkwebview) created via Objective-C runtime
- **Script Injection**: Same `window.stash_sdk` functions as iOS/Android
- **Message Handlers**: Posts to notification queue instead of Unity messages
- **Lifecycle**: Explicit cleanup via `DestroyWebViewWindow()`

### Callback Simulation
Editor window provides buttons to trigger callbacks directly:
- Payment Success
- Payment Failure
- Set Payment Channel
- Dismiss Catalog

All callbacks close the editor window and native WebView.

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

### Unity Editor
- **Platform Limitation**: macOS only (uses [AppKit](https://developer.apple.com/documentation/appkit) and [WebKit](https://developer.apple.com/documentation/webkit) APIs)
- **IPC Requirement**: Native bundle runs out-of-process, requires queue-based communication
- **Bundle Loading**: Manual [`dlopen`](https://developer.apple.com/library/archive/documentation/System/Conceptual/ManPages_iPhoneOS/man3/dlopen.3.html)/[`dlsym`](https://developer.apple.com/library/archive/documentation/System/Conceptual/ManPages_iPhoneOS/man3/dlsym.3.html) required, P/Invoke insufficient for complex Objective-C

