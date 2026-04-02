/**
 * Forwards Unity C# to StashNativeCard and delegate callbacks to Unity.
 * When StashNative is not linked, bridge compiles as no-ops.
 */
#import <Foundation/Foundation.h>

#define STASHNATIVE_AVAILABLE __has_include(<StashNative/StashNative-Swift.h>) || __has_include(<StashNative/StashNative.h>)

#if STASHNATIVE_AVAILABLE
#if __has_include(<StashNative/StashNative-Swift.h>)
#import <StashNative/StashNative-Swift.h>
#else
#import <StashNative/StashNative.h>
#endif
#endif

extern "C" void UnitySendMessage(const char* obj, const char* method, const char* msg);

static const char* kUnityObject = "StashNative";

static void SendToUnity(const char* method, const char* msg) {
    if (msg == NULL) msg = "";
    UnitySendMessage(kUnityObject, method, msg);
}

#if STASHNATIVE_AVAILABLE

@interface StashNativeUnityDelegate : NSObject <StashNativeCardDelegate>
@end

@implementation StashNativeUnityDelegate

- (void)stashNativeCardDidCompletePaymentWithOrder:(NSString *)order {
    SendToUnity("OnIOSPaymentSuccess", order ? [order UTF8String] : "");
}

- (void)stashNativeCardDidFailPayment {
    SendToUnity("OnIOSPaymentFailure", "");
}

- (void)stashNativeCardDidDismiss {
    SendToUnity("OnIOSDialogDismissed", "");
}

- (void)stashNativeCardDidReceiveOptIn:(NSString *)optinType {
    SendToUnity("OnIOSOptinResponse", optinType ? [optinType UTF8String] : "");
}

- (void)stashNativeCardDidLoadPage:(double)loadTimeMs {
    SendToUnity("OnIOSPageLoaded", [[NSString stringWithFormat:@"%f", loadTimeMs] UTF8String]);
}

- (void)stashNativeCardDidEncounterNetworkError {
    SendToUnity("OnIOSNetworkError", "");
}

- (void)stashNativeCardDidRequestExternalPaymentWithURL:(NSString *)url {
    SendToUnity("OnIOSExternalPayment", url ? [url UTF8String] : "");
}

@end

static StashNativeUnityDelegate* s_delegate;
static BOOL s_delegateSet;

static void EnsureDelegate(void) {
    if (s_delegateSet) return;
    s_delegate = [[StashNativeUnityDelegate alloc] init];
    [StashNativeCard sharedInstance].delegate = s_delegate;
    s_delegateSet = YES;
}

static NSString* NSStringFromNullableUTF8(const char* utf8) {
    if (utf8 == NULL || utf8[0] == '\0') return nil;
    return [NSString stringWithUTF8String:utf8];
}

#endif

extern "C" {

void _StashNativeCardBridgeOpenCard(const char* url) {
#if STASHNATIVE_AVAILABLE
    if (!url) return;
    EnsureDelegate();
    NSString* nsUrl = [NSString stringWithUTF8String:url];
    [[StashNativeCard sharedInstance] openCardWithURL:nsUrl config:nil];
#endif
}

void _StashNativeCardBridgeOpenCardWithConfig(const char* url,
    bool forcePortrait,
    float cardHeightRatioPortrait, float cardWidthRatioLandscape, float cardHeightRatioLandscape,
    float tabletWidthRatioPortrait, float tabletHeightRatioPortrait,
    float tabletWidthRatioLandscape, float tabletHeightRatioLandscape,
    const char* backgroundColorHex) {
#if STASHNATIVE_AVAILABLE
    if (!url) return;
    EnsureDelegate();
    NSString* nsUrl = [NSString stringWithUTF8String:url];
    StashNativeCardConfig* config = [[StashNativeCardConfig alloc] init];
    config.forcePortrait = forcePortrait;
    config.cardHeightRatioPortrait = cardHeightRatioPortrait;
    config.cardWidthRatioLandscape = cardWidthRatioLandscape;
    config.cardHeightRatioLandscape = cardHeightRatioLandscape;
    config.tabletWidthRatioPortrait = tabletWidthRatioPortrait;
    config.tabletHeightRatioPortrait = tabletHeightRatioPortrait;
    config.tabletWidthRatioLandscape = tabletWidthRatioLandscape;
    config.tabletHeightRatioLandscape = tabletHeightRatioLandscape;
    config.backgroundColor = NSStringFromNullableUTF8(backgroundColorHex);
    [[StashNativeCard sharedInstance] openCardWithURL:nsUrl config:config];
#endif
}

void _StashNativeCardBridgeOpenModal(const char* url) {
#if STASHNATIVE_AVAILABLE
    if (!url) return;
    EnsureDelegate();
    NSString* nsUrl = [NSString stringWithUTF8String:url];
    [[StashNativeCard sharedInstance] openModalWithURL:nsUrl config:nil];
#endif
}

void _StashNativeCardBridgeOpenModalWithConfig(const char* url,
    bool allowDismiss,
    float phoneWPortrait, float phoneHPortrait, float phoneWLandscape, float phoneHLandscape,
    float tabletWPortrait, float tabletHPortrait, float tabletWLandscape, float tabletHLandscape,
    const char* backgroundColorHex) {
#if STASHNATIVE_AVAILABLE
    if (!url) return;
    EnsureDelegate();
    NSString* nsUrl = [NSString stringWithUTF8String:url];
    StashNativeModalConfig* config = [[StashNativeModalConfig alloc] init];
    config.allowDismiss = allowDismiss;
    config.phoneWidthRatioPortrait = phoneWPortrait;
    config.phoneHeightRatioPortrait = phoneHPortrait;
    config.phoneWidthRatioLandscape = phoneWLandscape;
    config.phoneHeightRatioLandscape = phoneHLandscape;
    config.tabletWidthRatioPortrait = tabletWPortrait;
    config.tabletHeightRatioPortrait = tabletHPortrait;
    config.tabletWidthRatioLandscape = tabletWLandscape;
    config.tabletHeightRatioLandscape = tabletHLandscape;
    config.backgroundColor = NSStringFromNullableUTF8(backgroundColorHex);
    [[StashNativeCard sharedInstance] openModalWithURL:nsUrl config:config];
#endif
}

void _StashNativeCardBridgeOpenBrowser(const char* url) {
#if STASHNATIVE_AVAILABLE
    if (!url) return;
    EnsureDelegate();
    NSString* nsUrl = [NSString stringWithUTF8String:url];
    [[StashNativeCard sharedInstance] openBrowserWithURL:nsUrl];
#endif
}

void _StashNativeCardBridgeCloseBrowser(void) {
#if STASHNATIVE_AVAILABLE
    [[StashNativeCard sharedInstance] closeBrowser];
#endif
}

void _StashNativeCardBridgeDismiss(void) {
#if STASHNATIVE_AVAILABLE
    [[StashNativeCard sharedInstance] dismiss];
#endif
}

void _StashNativeCardBridgeResetPresentationState(void) {
#if STASHNATIVE_AVAILABLE
    [[StashNativeCard sharedInstance] resetPresentationState];
#endif
}

bool _StashNativeCardBridgeIsCurrentlyPresented(void) {
#if STASHNATIVE_AVAILABLE
    return [[StashNativeCard sharedInstance] isCurrentlyPresented];
#else
    return false;
#endif
}

bool _StashNativeCardBridgeIsPurchaseProcessing(void) {
#if STASHNATIVE_AVAILABLE
    return [[StashNativeCard sharedInstance] isPurchaseProcessing];
#else
    return false;
#endif
}

bool _StashNativeCardBridgeIsSDKAvailable(void) {
#if STASHNATIVE_AVAILABLE
    return true;
#else
    return false;
#endif
}

}
