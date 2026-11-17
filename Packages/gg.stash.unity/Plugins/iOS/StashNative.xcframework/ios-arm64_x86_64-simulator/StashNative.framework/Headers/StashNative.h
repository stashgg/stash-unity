#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>

NS_ASSUME_NONNULL_BEGIN

FOUNDATION_EXPORT NSErrorDomain const StashNativeScannerErrorDomain;
typedef NS_ERROR_ENUM(StashNativeScannerErrorDomain, StashNativeScannerError) {
    StashNativeScannerErrorCameraPermissionDenied = 1,
    StashNativeScannerErrorCameraUnavailable,
    StashNativeScannerErrorCameraConfigurationFailed,
    StashNativeScannerErrorMissingCameraUsageDescription,
    /** The optional StashNativeCodeLink module is not linked; nothing was presented. */
    StashNativeScannerErrorScannerUnavailable
};

typedef NS_ENUM(NSInteger, StashNativeOrientationPreference) {
    StashNativeOrientationPreferenceFollowHost = 0,
    StashNativeOrientationPreferencePortrait = 1
};

/** Responsive card. Defaults: 400 x 560 content, 16 margin, 720 maximum content height.
 * Dimensions are points of web content, excluding safe areas.
 * Preferred height is the resting height, limited by the maximum and available space.
 */
@interface StashNativeCardConfig : NSObject <NSCopying>
@property (nonatomic) CGFloat preferredContentWidth;
@property (nonatomic) CGFloat preferredContentHeight;
/** Zero uses all available height. */
@property (nonatomic) CGFloat maximumContentHeight;
@property (nonatomic) CGFloat edgeMargin;
/** Allows user dismissal. Page close, payment callbacks, and native dismiss are independent. */
@property (nonatomic) BOOL allowDismiss;
@property (nonatomic) BOOL autoClose;
/** On iPhone, presents portrait checkout in an SDK window, including landscape-only hosts. Ignored on iPad. */
@property (nonatomic) StashNativeOrientationPreference orientationPreference;
@end

/** Safari presentation. Defaults to following the host orientation; copied when opened. */
@interface StashNativeBrowserConfig : NSObject <NSCopying>
/** Requests portrait Safari on iPhone, including landscape-only hosts. Ignored on iPad. */
@property (nonatomic) StashNativeOrientationPreference orientationPreference;
@end

/** Scanner copy. Nil or empty text uses the built-in wording. Copied when the scanner opens. */
@interface StashNativeScannerConfig : NSObject <NSCopying>
/** Default: "Scan QR code". */
@property (nonatomic, copy, nullable) NSString *headerText;
/** Default: "Scan the webshop code". */
@property (nonatomic, copy, nullable) NSString *calloutTitle;
/** Default: "Keep the QR code centered inside the frame." */
@property (nonatomic, copy, nullable) NSString *calloutSubtitle;
/** Default: "Camera unavailable". */
@property (nonatomic, copy, nullable) NSString *cameraUnavailableText;
/** Default: "Connected". */
@property (nonatomic, copy, nullable) NSString *connectedText;
/** Defaults to YES. NO removes the callout and gives its space to the scan frame. */
@property (nonatomic) BOOL showCallout;
@end

@protocol StashNativeDelegate <NSObject>

@optional

/**
 * Called when a payment completes successfully. With autoClose on, the card closes before this
 * callback is invoked, so a new presentation can open from it.
 *
 * @param order Optional string from \c window.stash_sdk.onPaymentSuccess(order) (plain or JSON
 *     string). \c nil when the page omits the argument or passes an empty string.
 */
- (void)stashNativeDidCompletePaymentWithOrder:(nullable NSString *)order
    NS_SWIFT_NAME(stashNativeDidCompletePayment(withOrder:));

/**
 * Called when a payment fails. With autoClose on, the card closes before this callback is invoked.
 */
- (void)stashNativeDidFailPayment;

/**
 * Called after checkout card dismissal by the user, page window.close() outside processing,
 * dismiss, or the host dismissing the presenting view controller. Payment auto-close, network
 * error, and browser handoff do not emit it.
 */
- (void)stashNativeDidDismissCard;

/** Called after the scanner is dismissed without a scan result. */
- (void)stashNativeDidDismissScanner;

/**
 * Called when the checkout page finishes loading.
 * @param loadTimeMs The page load time in milliseconds
 */
- (void)stashNativeDidLoadPage:(double)loadTimeMs;

/**
 * Called when checkout ends because of a network error: no connection, a failed initial response,
 * the 15-second foreground response deadline, or a connection failure after the page loaded
 * (including during purchase processing). The card is dismissed before this callback is invoked
 * and \c stashNativeDidDismissCard is not emitted.
 */
- (void)stashNativeDidEncounterNetworkError;

/**
 * Called when the checkout page calls \c window.stash_sdk.externalBrowser(url, {keepOpen}). The \c url
 * string includes the theme query parameter when applicable.
 * By default the SDK closes the checkout without invoking \c stashNativeDidDismissCard, then opens the URL
 * in \c SFSafariViewController. Closing it emits \c stashNativeDidDismissBrowser. An iPhone portrait
 * checkout keeps its portrait window through a native Safari sheet and restores the host orientation
 * when the browser closes.
 * With \c keepOpen, Safari opens over the card, which stays open. This callback is informational:
 * closing Safari returns to the card and emits no browser event, and the card ends with its own events.
 */
- (void)stashNativeDidRequestExternalPaymentWithURL:(NSString *)url
    NS_SWIFT_NAME(stashNativeDidRequestExternalPayment(with:));

/**
 * Called after a browser opened with openBrowser or checkout's externalBrowser is dismissed.
 * Covers user closure and closeBrowser; no card dismissal callback is emitted. Not emitted for
 * Safari opened with \c keepOpen, which returns to the open card, or after dismissSafariViewController
 * or resetPresentationState, which end silently.
 */
- (void)stashNativeDidDismissBrowser;

/** The scanner returns the unmodified QR payload once, after the scanner card has closed.
 * No URL is opened and no payment callback or user-dismiss callback is emitted on success.
 */
- (void)stashNativeDidScanQRCode:(NSString *)content
    NS_SWIFT_NAME(stashNativeDidScanQRCode(_:));

/** The scanner cannot use the camera. Delivered once per scanner session; the card stays
 * open so the user can close it or grant access in Settings and return to scanning.
 * \c StashNativeScannerErrorScannerUnavailable is different: the StashNativeCodeLink module is
 * not linked, nothing is presented, \c isCurrentlyPresented stays NO, and no dismissal follows.
 */
- (void)stashNativeScannerDidEncounterError:(NSError *)error
    NS_SWIFT_NAME(stashNativeScannerDidEncounterError(_:));

@end

/** One active checkout, scanner, or browser per instance. Callbacks use the main thread.
 * Opening calls may run on any thread and are marshaled to main.
 */
@interface StashNative : NSObject
/** Register, inspect, replace, or clear the delegate on main; tear down the delegate on main too.
 * Each event uses the live delegate once when delivery begins. Clearing affects subsequent events,
 * not a callback already executing. Events are not replayed. The SDK does not retain this registration.
 * Under non-ARC, clear the delegate before releasing it.
 */
#if __has_feature(objc_arc)
@property (nonatomic, weak, nullable) id<StashNativeDelegate> delegate;
#else
@property (nonatomic, assign, nullable) id<StashNativeDelegate> delegate;
#endif
/** Query presentation and processing state on the main thread; the state is owned there. */
@property (nonatomic, readonly) BOOL isCurrentlyPresented;
@property (nonatomic, readonly) BOOL isPurchaseProcessing;
+ (instancetype)sharedInstance;
/** The SDK version string, for example "3.0.0". Matches Android's StashNative.getVersion(). */
+ (NSString *)getVersion;
+ (NSString *)sdkVersion DEPRECATED_MSG_ATTRIBUTE("Use getVersion.");
+ (void)setInspectableWebViewsEnabled:(BOOL)enabled;
+ (BOOL)isInspectableWebViewsEnabled;
/** The presenter must belong to the window that initiated checkout. Invalid/busy opens are ignored. */
- (void)openCardWithURL:(NSString *)url fromViewController:(UIViewController *)presenter
               config:(nullable StashNativeCardConfig *)config NS_SWIFT_NAME(openCard(withURL:from:config:));
- (void)openBrowserWithURL:(NSString *)url fromViewController:(UIViewController *)presenter
    NS_SWIFT_NAME(openBrowser(withURL:from:));
/** Opens Safari with an optional orientation preference. */
- (void)openBrowserWithURL:(NSString *)url fromViewController:(UIViewController *)presenter
                  config:(nullable StashNativeBrowserConfig *)config NS_SWIFT_NAME(openBrowser(withURL:from:config:));
/** Opens a QR camera in the responsive native card, following the host app's orientation.
 * Requires the StashNativeCodeLink module; without it the delegate receives
 * \c StashNativeScannerErrorScannerUnavailable asynchronously and nothing is presented.
 * The host must provide NSCameraUsageDescription. Requests camera access when opened;
 * uses no microphone and saves no images. The first framed QR code shows a brief Connected
 * confirmation, then closes the card before delivering the scan callback.
 */
- (void)openScannerFromViewController:(UIViewController *)presenter NS_SWIFT_NAME(openScanner(from:));
/** Opens the scanner with optional custom copy and callout visibility. */
- (void)openScannerFromViewController:(UIViewController *)presenter config:(nullable StashNativeScannerConfig *)config
    NS_SWIFT_NAME(openScanner(from:config:));
/** Programmatic dismissal remains available while purchase processing prevents user dismissal. */
- (void)dismiss;
/** Immediately tears down the session without delegate callbacks; keeps the delegate registration. */
- (void)resetPresentationState;
/** Closes Safari opened by openBrowser or externalBrowser, emitting \c stashNativeDidDismissBrowser.
 * Safari opened with \c keepOpen closes back to the open card without a browser event.
 */
- (void)closeBrowser;
/** Closes the SDK's Safari without any delegate callback, for example when the payment deep link
 * arrives and the game handles the result itself. Safari opened by openBrowser or externalBrowser ends
 * its presentation silently. Safari opened with \c keepOpen closes back to the open card, whose page
 * still reports its own events.
 */
- (void)dismissSafariViewController;
@end

NS_ASSUME_NONNULL_END

// Scanner module contract; keeps the umbrella complete for framework module builds.
#import "StashNativeScannerSupport.h"
