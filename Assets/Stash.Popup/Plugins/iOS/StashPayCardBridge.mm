/**
 * Unity bridge for the Stash Pay iOS SDK (StashPay.xcframework).
 * Forwards Unity C# calls to StashPayCard and sends delegate callbacks to Unity via UnitySendMessage.
 *
 * IMPORTANT: StashPay.xcframework must be present in this folder (Plugins/iOS/) when you build
 * from Unity. Otherwise __has_include(<StashPay/...>) fails and the bridge compiles as no-ops.
 * Add the xcframework before Build → iOS so Unity embeds it in the Xcode project.
 */
#import <Foundation/Foundation.h>

#define STASHPAY_AVAILABLE __has_include(<StashPay/StashPay-Swift.h>) || __has_include(<StashPay/StashPay.h>)

#if STASHPAY_AVAILABLE
#if __has_include(<StashPay/StashPay-Swift.h>)
#import <StashPay/StashPay-Swift.h>
#else
#import <StashPay/StashPay.h>
#endif
#endif

extern "C" void UnitySendMessage(const char* obj, const char* method, const char* msg);

static const char* kUnityObject = "StashPayCard";

static void SendToUnity(const char* method, const char* msg) {
    if (msg == NULL) msg = "";
    UnitySendMessage(kUnityObject, method, msg);
}

#if STASHPAY_AVAILABLE

@interface StashPayUnityDelegate : NSObject <StashPayCardDelegate>
@end

@implementation StashPayUnityDelegate

- (void)stashPayCardDidCompletePayment {
    SendToUnity("OnIOSPaymentSuccess", "");
}

- (void)stashPayCardDidFailPayment {
    SendToUnity("OnIOSPaymentFailure", "");
}

- (void)stashPayCardDidDismiss {
    SendToUnity("OnIOSDialogDismissed", "");
}

- (void)stashPayCardDidReceiveOptIn:(NSString *)optinType {
    SendToUnity("OnIOSOptinResponse", optinType ? [optinType UTF8String] : "");
}

- (void)stashPayCardDidLoadPage:(double)loadTimeMs {
    SendToUnity("OnIOSPageLoaded", [[NSString stringWithFormat:@"%f", loadTimeMs] UTF8String]);
}

- (void)stashPayCardDidEncounterNetworkError {
    SendToUnity("OnIOSNetworkError", "");
}

@end

static StashPayUnityDelegate* s_delegate;
static BOOL s_delegateSet;

static void EnsureDelegate(void) {
    if (s_delegateSet) return;
    s_delegate = [[StashPayUnityDelegate alloc] init];
    [StashPayCard sharedInstance].delegate = s_delegate;
    s_delegateSet = YES;
}

#endif // STASHPAY_AVAILABLE

extern "C" {

void _StashPayCardBridgeOpenCheckout(const char* url) {
#if STASHPAY_AVAILABLE
    if (!url) return;
    EnsureDelegate();
    NSString* nsUrl = [NSString stringWithUTF8String:url];
    [[StashPayCard sharedInstance] openCheckoutWithURL:nsUrl];
#endif
}

void _StashPayCardBridgeOpenModal(const char* url) {
#if STASHPAY_AVAILABLE
    if (!url) return;
    EnsureDelegate();
    NSString* nsUrl = [NSString stringWithUTF8String:url];
    [[StashPayCard sharedInstance] openModalWithURL:nsUrl];
#endif
}

void _StashPayCardBridgeOpenModalWithConfig(const char* url,
    bool showDragBar, bool allowDismiss,
    float phoneWPortrait, float phoneHPortrait, float phoneWLandscape, float phoneHLandscape,
    float tabletWPortrait, float tabletHPortrait, float tabletWLandscape, float tabletHLandscape) {
#if STASHPAY_AVAILABLE
    if (!url) return;
    EnsureDelegate();
    NSString* nsUrl = [NSString stringWithUTF8String:url];
    StashPayModalConfig* config = [[StashPayModalConfig alloc] init];
    config.showDragBar = showDragBar;
    config.allowDismiss = allowDismiss;
    config.phoneWidthRatioPortrait = phoneWPortrait;
    config.phoneHeightRatioPortrait = phoneHPortrait;
    config.phoneWidthRatioLandscape = phoneWLandscape;
    config.phoneHeightRatioLandscape = phoneHLandscape;
    config.tabletWidthRatioPortrait = tabletWPortrait;
    config.tabletHeightRatioPortrait = tabletHPortrait;
    config.tabletWidthRatioLandscape = tabletWLandscape;
    config.tabletHeightRatioLandscape = tabletHLandscape;
    [[StashPayCard sharedInstance] openModalWithURL:nsUrl config:config];
#endif
}

void _StashPayCardBridgeDismiss(void) {
#if STASHPAY_AVAILABLE
    [[StashPayCard sharedInstance] dismiss];
#endif
}

void _StashPayCardBridgeResetPresentationState(void) {
#if STASHPAY_AVAILABLE
    [[StashPayCard sharedInstance] resetPresentationState];
#endif
}

bool _StashPayCardBridgeIsCurrentlyPresented(void) {
#if STASHPAY_AVAILABLE
    return [[StashPayCard sharedInstance] isCurrentlyPresented];
#else
    return false;
#endif
}

bool _StashPayCardBridgeIsPurchaseProcessing(void) {
#if STASHPAY_AVAILABLE
    return [[StashPayCard sharedInstance] isPurchaseProcessing];
#else
    return false;
#endif
}

void _StashPayCardBridgeSetForcePortraitOnCheckout(bool force) {
#if STASHPAY_AVAILABLE
    [StashPayCard sharedInstance].forcePortraitOnCheckout = force;
#endif
}

void _StashPayCardBridgeSetCardHeightRatioPortrait(float value) {
#if STASHPAY_AVAILABLE
    [StashPayCard sharedInstance].cardHeightRatioPortrait = value;
#endif
}

void _StashPayCardBridgeSetCardWidthRatioLandscape(float value) {
#if STASHPAY_AVAILABLE
    [StashPayCard sharedInstance].cardWidthRatioLandscape = value;
#endif
}

void _StashPayCardBridgeSetCardHeightRatioLandscape(float value) {
#if STASHPAY_AVAILABLE
    [StashPayCard sharedInstance].cardHeightRatioLandscape = value;
#endif
}

void _StashPayCardBridgeSetTabletWidthRatioPortrait(float value) {
#if STASHPAY_AVAILABLE
    [StashPayCard sharedInstance].tabletWidthRatioPortrait = value;
#endif
}

void _StashPayCardBridgeSetTabletHeightRatioPortrait(float value) {
#if STASHPAY_AVAILABLE
    [StashPayCard sharedInstance].tabletHeightRatioPortrait = value;
#endif
}

void _StashPayCardBridgeSetTabletWidthRatioLandscape(float value) {
#if STASHPAY_AVAILABLE
    [StashPayCard sharedInstance].tabletWidthRatioLandscape = value;
#endif
}

void _StashPayCardBridgeSetTabletHeightRatioLandscape(float value) {
#if STASHPAY_AVAILABLE
    [StashPayCard sharedInstance].tabletHeightRatioLandscape = value;
#endif
}

void _StashPayCardBridgeSetForceWebBasedCheckout(bool force) {
#if STASHPAY_AVAILABLE
    [StashPayCard sharedInstance].forceWebBasedCheckout = force;
#endif
}

bool _StashPayCardBridgeGetForceWebBasedCheckout(void) {
#if STASHPAY_AVAILABLE
    return [StashPayCard sharedInstance].forceWebBasedCheckout;
#else
    return false;
#endif
}

void _StashPayCardBridgeDismissSafariViewController(void) {
#if STASHPAY_AVAILABLE
    [[StashPayCard sharedInstance] dismissSafariViewController];
#endif
}

void _StashPayCardBridgeDismissSafariViewControllerWithResult(bool success) {
#if STASHPAY_AVAILABLE
    [[StashPayCard sharedInstance] dismissSafariViewControllerWithResult:success];
#endif
}

bool _StashPayCardBridgeIsSDKAvailable(void) {
#if STASHPAY_AVAILABLE
    return true;
#else
    return false;
#endif
}

} // extern "C"
