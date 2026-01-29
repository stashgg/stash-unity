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

/**
 * Card height ratio (0.0 to 1.0) relative to screen height.
 * Default is 0.6 (matching Unity behavior).
 */
@property (nonatomic, assign) CGFloat cardHeightRatio;

/**
 * Vertical position ratio (0.0 = bottom, 1.0 = top).
 * Default is 1.0 (card slides up from bottom).
 */
@property (nonatomic, assign) CGFloat cardVerticalPosition;

/**
 * Card width ratio (0.0 to 1.0) relative to screen width.
 * Default is 1.0 (full width).
 */
@property (nonatomic, assign) CGFloat cardWidthRatio;

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
