//
//  StashPayCard.h
//  StashPay
//
//  Native iOS SDK for Stash Pay checkout integration.
//

#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>

NS_ASSUME_NONNULL_BEGIN

/**
 * Configuration for custom popup sizing.
 */
@interface StashPayPopupSizeConfig : NSObject

@property (nonatomic, assign) CGFloat portraitWidthMultiplier;
@property (nonatomic, assign) CGFloat portraitHeightMultiplier;
@property (nonatomic, assign) CGFloat landscapeWidthMultiplier;
@property (nonatomic, assign) CGFloat landscapeHeightMultiplier;

/**
 * Creates a default size configuration.
 */
- (instancetype)init;

/**
 * Creates a custom size configuration.
 */
- (instancetype)initWithPortraitWidth:(CGFloat)portraitWidth
                       portraitHeight:(CGFloat)portraitHeight
                       landscapeWidth:(CGFloat)landscapeWidth
                      landscapeHeight:(CGFloat)landscapeHeight;

@end

/**
 * Configuration for modal presentation.
 *
 * Modal always appears centered on screen (unlike checkout which uses cards on phones).
 * Supports independent sizing for phone/tablet and portrait/landscape orientations.
 */
@interface StashPayModalConfig : NSObject

/** Phone width ratio for portrait (0.1-1.0). Default 0.9. */
@property (nonatomic, assign) CGFloat phoneWidthRatioPortrait;
/** Phone height ratio for portrait (0.1-1.0). Default 0.7. */
@property (nonatomic, assign) CGFloat phoneHeightRatioPortrait;
/** Phone width ratio for landscape (0.1-1.0). Default 0.7. */
@property (nonatomic, assign) CGFloat phoneWidthRatioLandscape;
/** Phone height ratio for landscape (0.1-1.0). Default 0.85. */
@property (nonatomic, assign) CGFloat phoneHeightRatioLandscape;
/** Tablet width ratio for portrait (0.1-1.0). Default 0.6. */
@property (nonatomic, assign) CGFloat tabletWidthRatioPortrait;
/** Tablet height ratio for portrait (0.1-1.0). Default 0.7. */
@property (nonatomic, assign) CGFloat tabletHeightRatioPortrait;
/** Tablet width ratio for landscape (0.1-1.0). Default 0.5. */
@property (nonatomic, assign) CGFloat tabletWidthRatioLandscape;
/** Tablet height ratio for landscape (0.1-1.0). Default 0.8. */
@property (nonatomic, assign) CGFloat tabletHeightRatioLandscape;
/** Whether to show drag bar at top of modal. Default YES. */
@property (nonatomic, assign) BOOL showDragBar;
/** Whether tap outside and drag gestures can dismiss the modal. Default YES. */
@property (nonatomic, assign) BOOL allowDismiss;

/**
 * Creates a default modal configuration.
 */
- (instancetype)init;

/**
 * Creates a modal configuration with all sizing and behavior options.
 */
- (instancetype)initWithPhoneWidthPortrait:(CGFloat)phoneWidthPortrait
                         phoneHeightPortrait:(CGFloat)phoneHeightPortrait
                         phoneWidthLandscape:(CGFloat)phoneWidthLandscape
                        phoneHeightLandscape:(CGFloat)phoneHeightLandscape
                        tabletWidthPortrait:(CGFloat)tabletWidthPortrait
                       tabletHeightPortrait:(CGFloat)tabletHeightPortrait
                       tabletWidthLandscape:(CGFloat)tabletWidthLandscape
                      tabletHeightLandscape:(CGFloat)tabletHeightLandscape
                               showDragBar:(BOOL)showDragBar
                              allowDismiss:(BOOL)allowDismiss;

@end

/**
 * Protocol for receiving StashPayCard events.
 */
@protocol StashPayCardDelegate <NSObject>

@optional

/**
 * Called when a payment completes successfully.
 */
- (void)stashPayCardDidCompletePayment;

/**
 * Called when a payment fails.
 */
- (void)stashPayCardDidFailPayment;

/**
 * Called when the checkout dialog is dismissed by the user.
 */
- (void)stashPayCardDidDismiss;

/**
 * Called when an opt-in response is received.
 * @param optinType The type of opt-in response
 */
- (void)stashPayCardDidReceiveOptIn:(NSString *)optinType;

/**
 * Called when the checkout page finishes loading.
 * @param loadTimeMs The page load time in milliseconds
 */
- (void)stashPayCardDidLoadPage:(double)loadTimeMs;

/**
 * Called when a network error occurs during initial page load.
 * This includes: no network connection, page load failure, or timeout (5 seconds).
 * The dialog is automatically dismissed before this callback is invoked.
 */
- (void)stashPayCardDidEncounterNetworkError;

@end

/**
 * StashPayCard - Native iOS SDK for Stash Pay checkout integration.
 *
 * This is the main entry point for integrating Stash Pay checkout into your iOS app.
 * It provides methods to display checkout cards and popups, and handles payment callbacks.
 *
 * @code
 * // Get the shared instance
 * StashPayCard *stashPay = [StashPayCard sharedInstance];
 *
 * // Set the delegate to receive callbacks
 * stashPay.delegate = self;
 *
 * // Open a checkout
 * [stashPay openCheckoutWithURL:@"https://your-checkout-url.com"];
 * @endcode
 */
@interface StashPayCard : NSObject

/**
 * The delegate to receive StashPayCard events.
 */
#if __has_feature(objc_arc)
@property (nonatomic, weak, nullable) id<StashPayCardDelegate> delegate;
#else
@property (nonatomic, assign, nullable) id<StashPayCardDelegate> delegate;
#endif

/**
 * Gets whether web-based checkout (SFSafariViewController) is forced.
 * When enabled, checkout URLs open in SFSafariViewController instead of the in-app card UI.
 */
@property (nonatomic, assign) BOOL forceWebBasedCheckout;

/**
 * Checks if a checkout card or popup is currently displayed.
 */
@property (nonatomic, readonly) BOOL isCurrentlyPresented;

/**
 * Checks if a purchase is currently being processed.
 * When YES, the checkout dialog cannot be dismissed by the user.
 */
@property (nonatomic, readonly) BOOL isPurchaseProcessing;

// ============================================================================
// Checkout Orientation and Phone Card Size Configuration
// ============================================================================

/**
 * When YES (default), phone checkout forces portrait orientation.
 * When NO, phone checkout is shown in current orientation (slide from bottom); in landscape
 * uses cardWidthRatioLandscape and cardHeightRatioLandscape.
 */
@property (nonatomic, assign) BOOL forcePortraitOnCheckout;

/**
 * Phone card height ratio in portrait (0.0 to 1.0). Portrait phone card is full screen width.
 * Default is 0.68 (68% of screen height).
 */
@property (nonatomic, assign) CGFloat cardHeightRatioPortrait;

/**
 * Phone card width ratio in landscape (0.1 to 1.0). Used when forcePortraitOnCheckout is NO.
 * Default is 0.9 (90% of screen width).
 */
@property (nonatomic, assign) CGFloat cardWidthRatioLandscape;

/**
 * Phone card height ratio in landscape (0.1 to 1.0). Used when forcePortraitOnCheckout is NO.
 * Default is 0.6 (60% of screen height).
 */
@property (nonatomic, assign) CGFloat cardHeightRatioLandscape;

// ============================================================================
// Orientation-Specific Tablet (iPad) Card Size Configuration
// ============================================================================

/**
 * Tablet width ratio in portrait orientation (0.1 to 1.0).
 * Default is 0.6 (60% of screen width).
 */
@property (nonatomic, assign) CGFloat tabletWidthRatioPortrait;

/**
 * Tablet height ratio in portrait orientation (0.1 to 1.0).
 * Default is 0.8 (80% of screen height).
 */
@property (nonatomic, assign) CGFloat tabletHeightRatioPortrait;

/**
 * Tablet width ratio in landscape orientation (0.1 to 1.0).
 * Default is 0.8 (80% of screen width).
 */
@property (nonatomic, assign) CGFloat tabletWidthRatioLandscape;

/**
 * Tablet height ratio in landscape orientation (0.1 to 1.0).
 * Default is 0.65 (65% of screen height).
 */
@property (nonatomic, assign) CGFloat tabletHeightRatioLandscape;

/**
 * Gets the shared singleton instance of StashPayCard.
 */
+ (instancetype)sharedInstance;

/**
 * Opens a Stash Pay checkout URL in a sliding card UI.
 *
 * The card slides up from the bottom of the screen and displays the checkout page.
 * On iPads, the card appears centered on screen.
 *
 * @param url The Stash Pay checkout URL to load
 */
- (void)openCheckoutWithURL:(NSString *)url;

/**
 * Opens a Stash Pay URL in a centered popup dialog.
 *
 * The popup appears centered on screen with a semi-transparent background.
 * Uses default sizing appropriate for the device.
 *
 * @param url The Stash Pay URL to load
 */
- (void)openPopupWithURL:(NSString *)url;

/**
 * Opens a Stash Pay URL in a centered popup dialog with custom sizing.
 *
 * @param url The Stash Pay URL to load
 * @param sizeConfig Custom size configuration for portrait and landscape orientations
 */
- (void)openPopupWithURL:(NSString *)url sizeConfig:(nullable StashPayPopupSizeConfig *)sizeConfig;

/**
 * Opens a URL in a centered modal dialog with default configuration.
 *
 * Unlike openCheckoutWithURL which uses different presentations on phones vs iPads,
 * openModalWithURL always shows a centered modal on all devices. The modal resizes
 * seamlessly when the device rotates.
 *
 * Uses default sizing ratios and shows drag bar with dismiss enabled.
 *
 * @param url The URL to load in the modal
 */
- (void)openModalWithURL:(NSString *)url;

/**
 * Opens a URL in a centered modal dialog with custom configuration.
 *
 * Unlike openCheckoutWithURL which uses different presentations on phones vs iPads,
 * openModalWithURL always shows a centered modal on all devices. The modal resizes
 * seamlessly when the device rotates.
 *
 * @param url The URL to load in the modal
 * @param config Configuration for sizing, drag bar, and dismiss behavior (nil for defaults)
 */
- (void)openModalWithURL:(NSString *)url config:(nullable StashPayModalConfig *)config;

/**
 * Dismisses any currently displayed checkout dialog.
 */
- (void)dismiss;

/**
 * Resets the presentation state and dismisses any displayed dialog.
 */
- (void)resetPresentationState;

/**
 * Dismisses the currently open SFSafariViewController if one is presented.
 * Only effective when forceWebBasedCheckout is YES.
 */
- (void)dismissSafariViewController;

/**
 * Dismisses the currently open SFSafariViewController and fires appropriate callbacks.
 * Useful for handling deeplink callbacks.
 *
 * @param success YES to fire payment success callback, NO to fire payment failure callback
 */
- (void)dismissSafariViewControllerWithResult:(BOOL)success;

@end

NS_ASSUME_NONNULL_END
