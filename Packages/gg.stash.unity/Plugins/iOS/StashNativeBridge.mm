/**
 * Forwards Unity C# calls to StashNative (3.x) and delegate callbacks back to Unity.
 * When StashNative.xcframework is not linked, every entry point compiles as a no-op.
 *
 * Threading: Unity's script thread is the main thread on iOS, so calls normally run inline. If a call arrives on another
 * thread it is marshaled to main. Session-scoped callbacks carry the generation the C# side passed to the open, as
 * "<generation>|<payload>", so C# can drop events from a session it has already reset.
 */
#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>

#define STASHNATIVE_AVAILABLE __has_include(<StashNative/StashNative.h>)

#if STASHNATIVE_AVAILABLE
#import <StashNative/StashNative.h>
#endif

extern "C" void UnitySendMessage(const char* obj, const char* method, const char* msg);
extern "C" UIViewController* UnityGetGLViewController(void);

static const char* kUnityObject = "StashNative";
static int s_generation = 0;
static BOOL s_detached = NO;

static void SendToUnity(const char* method, const char* msg) {
    if (s_detached) return;
    if (msg == NULL) msg = "";
    UnitySendMessage(kUnityObject, method, msg);
}

/** Session-scoped event: prefixed with the generation of the open it belongs to. */
static void SendSessionEvent(const char* method, NSString* payload) {
    NSString* tagged = [NSString stringWithFormat:@"%d|%@", s_generation, payload ?: @""];
    SendToUnity(method, [tagged UTF8String]);
}

static void RunOnMain(dispatch_block_t block) {
    if ([NSThread isMainThread]) block();
    else dispatch_async(dispatch_get_main_queue(), block);
}

#if STASHNATIVE_AVAILABLE

@interface StashNativeUnityDelegate : NSObject <StashNativeDelegate>
@end

@implementation StashNativeUnityDelegate

- (void)stashNativeDidCompletePaymentWithOrder:(nullable NSString *)order {
    SendSessionEvent("OnIOSPaymentSuccess", order);
}

- (void)stashNativeDidFailPayment {
    SendSessionEvent("OnIOSPaymentFailure", nil);
}

- (void)stashNativeDidDismissCard {
    SendSessionEvent("OnIOSCardDismissed", nil);
}

- (void)stashNativeDidDismissScanner {
    SendSessionEvent("OnIOSScannerDismissed", nil);
}

- (void)stashNativeDidLoadPage:(double)loadTimeMs {
    SendSessionEvent("OnIOSPageLoaded", [NSString stringWithFormat:@"%f", loadTimeMs]);
}

- (void)stashNativeDidEncounterNetworkError {
    SendSessionEvent("OnIOSNetworkError", nil);
}

- (void)stashNativeDidRequestExternalPaymentWithURL:(NSString *)url {
    SendSessionEvent("OnIOSExternalPayment", url);
}

- (void)stashNativeDidDismissBrowser {
    // Not session-scoped: a browser opened with openBrowser has no card session, and the event must survive a reset.
    SendToUnity("OnIOSBrowserDismissed", "");
}

- (void)stashNativeDidScanQRCode:(NSString *)content {
    SendSessionEvent("OnIOSQrCodeScanned", content);
}

- (void)stashNativeScannerDidEncounterError:(NSError *)error {
    // "<code>;<description>"; codes match StashNativeScannerError (1..5).
    NSString* payload = [NSString stringWithFormat:@"%ld;%@", (long)error.code, error.localizedDescription ?: @""];
    SendSessionEvent("OnIOSScannerError", payload);
}

@end

static StashNativeUnityDelegate* s_delegate;

/** Must run on the main thread (the SDK requires delegate access on main). The SDK holds the delegate weakly; the static keeps it alive. */
static void EnsureDelegate(void) {
    s_detached = NO;
    if (s_delegate == nil) s_delegate = [[StashNativeUnityDelegate alloc] init];
    if ([StashNative sharedInstance].delegate != s_delegate) [StashNative sharedInstance].delegate = s_delegate;
}

static NSString* NSStringFromNullableUTF8(const char* utf8) {
    if (utf8 == NULL || utf8[0] == '\0') return nil;
    return [NSString stringWithUTF8String:utf8];
}

static StashNativeOrientationPreference OrientationFromInt(int value) {
    return value == 1 ? StashNativeOrientationPreferencePortrait : StashNativeOrientationPreferenceFollowHost;
}

static UIViewController* Presenter(void) {
    UIViewController* vc = UnityGetGLViewController();
    // Present from the topmost controller so an open succeeds even when Unity's root already presents something.
    while (vc.presentedViewController != nil && !vc.presentedViewController.isBeingDismissed) {
        vc = vc.presentedViewController;
    }
    return vc;
}

#endif

extern "C" {

bool _StashNativeBridgeIsSDKAvailable(void) {
#if STASHNATIVE_AVAILABLE
    return true;
#else
    return false;
#endif
}

/** True when the optional StashNativeCodeLink framework is linked (the core resolves this class by name). */
bool _StashNativeBridgeIsScannerAvailable(void) {
#if STASHNATIVE_AVAILABLE
    return NSClassFromString(@"StashNativeScannerViewController") != nil;
#else
    return false;
#endif
}

/** Returned buffer is strdup'd; IL2CPP's generated P/Invoke wrapper frees it after marshaling. */
const char* _StashNativeBridgeSdkVersion(void) {
#if STASHNATIVE_AVAILABLE
    NSString* v = [StashNative sdkVersion];
    return strdup(v ? [v UTF8String] : "");
#else
    return strdup("");
#endif
}

/** hasConfig == false opens with native defaults; the remaining parameters are ignored. */
void _StashNativeBridgeOpenCard(int generation, const char* url, bool hasConfig,
    float preferredContentWidth, float preferredContentHeight, float maximumContentHeight, float edgeMargin,
    bool allowDismiss, bool autoClose, int orientationPreference) {
#if STASHNATIVE_AVAILABLE
    if (!url) return;
    NSString* nsUrl = [NSString stringWithUTF8String:url];
    StashNativeCardConfig* config = nil;
    if (hasConfig) {
        config = [[StashNativeCardConfig alloc] init];
        config.preferredContentWidth = preferredContentWidth;
        config.preferredContentHeight = preferredContentHeight;
        config.maximumContentHeight = maximumContentHeight;
        config.edgeMargin = edgeMargin;
        config.allowDismiss = allowDismiss;
        config.autoClose = autoClose;
        config.orientationPreference = OrientationFromInt(orientationPreference);
    }
    RunOnMain(^{
        s_generation = generation;
        EnsureDelegate();
        [[StashNative sharedInstance] openCardWithURL:nsUrl fromViewController:Presenter() config:config];
    });
#endif
}

/** orientationPreference < 0 opens without a config. */
void _StashNativeBridgeOpenBrowser(const char* url, int orientationPreference) {
#if STASHNATIVE_AVAILABLE
    if (!url) return;
    NSString* nsUrl = [NSString stringWithUTF8String:url];
    StashNativeBrowserConfig* config = nil;
    if (orientationPreference >= 0) {
        config = [[StashNativeBrowserConfig alloc] init];
        config.orientationPreference = OrientationFromInt(orientationPreference);
    }
    RunOnMain(^{
        EnsureDelegate();
        [[StashNative sharedInstance] openBrowserWithURL:nsUrl fromViewController:Presenter() config:config];
    });
#endif
}

/** hasConfig == false opens with built-in copy. Empty strings keep the native default for that field. */
void _StashNativeBridgeOpenScanner(int generation, bool hasConfig,
    const char* headerText, const char* calloutTitle, const char* calloutSubtitle,
    const char* cameraUnavailableText, const char* connectedText, bool showCallout) {
#if STASHNATIVE_AVAILABLE
    StashNativeScannerConfig* config = nil;
    if (hasConfig) {
        config = [[StashNativeScannerConfig alloc] init];
        config.headerText = NSStringFromNullableUTF8(headerText);
        config.calloutTitle = NSStringFromNullableUTF8(calloutTitle);
        config.calloutSubtitle = NSStringFromNullableUTF8(calloutSubtitle);
        config.cameraUnavailableText = NSStringFromNullableUTF8(cameraUnavailableText);
        config.connectedText = NSStringFromNullableUTF8(connectedText);
        config.showCallout = showCallout;
    }
    RunOnMain(^{
        s_generation = generation;
        EnsureDelegate();
        [[StashNative sharedInstance] openScannerFromViewController:Presenter() config:config];
    });
#endif
}

void _StashNativeBridgeCloseBrowser(void) {
#if STASHNATIVE_AVAILABLE
    RunOnMain(^{ [[StashNative sharedInstance] closeBrowser]; });
#endif
}

/** Closes Safari and reports the deeplink payment result; the SDK then raises payment success/failure and browser dismissed. */
void _StashNativeBridgeDismissBrowser(bool success) {
#if STASHNATIVE_AVAILABLE
    RunOnMain(^{ [[StashNative sharedInstance] dismissSafariViewControllerWithResult:success]; });
#else
    (void)success;
#endif
}

void _StashNativeBridgeDismiss(void) {
#if STASHNATIVE_AVAILABLE
    RunOnMain(^{ [[StashNative sharedInstance] dismiss]; });
#endif
}

void _StashNativeBridgeResetPresentationState(void) {
#if STASHNATIVE_AVAILABLE
    RunOnMain(^{ [[StashNative sharedInstance] resetPresentationState]; });
#endif
}

/** Stops forwarding to Unity and clears the SDK delegate; called when the receiving GameObject is destroyed. */
void _StashNativeBridgeDetach(void) {
#if STASHNATIVE_AVAILABLE
    s_detached = YES;
    RunOnMain(^{
        if ([StashNative sharedInstance].delegate == s_delegate) [StashNative sharedInstance].delegate = nil;
    });
#endif
}

bool _StashNativeBridgeIsCurrentlyPresented(void) {
#if STASHNATIVE_AVAILABLE
    return [[StashNative sharedInstance] isCurrentlyPresented];
#else
    return false;
#endif
}

bool _StashNativeBridgeIsPurchaseProcessing(void) {
#if STASHNATIVE_AVAILABLE
    return [[StashNative sharedInstance] isPurchaseProcessing];
#else
    return false;
#endif
}

void _StashNativeBridgeSetInspectableWebViewsEnabled(bool enabled) {
#if STASHNATIVE_AVAILABLE
    [StashNative setInspectableWebViewsEnabled:enabled];
#else
    (void)enabled;
#endif
}

bool _StashNativeBridgeIsInspectableWebViewsEnabled(void) {
#if STASHNATIVE_AVAILABLE
    return [StashNative isInspectableWebViewsEnabled];
#else
    return false;
#endif
}

}
