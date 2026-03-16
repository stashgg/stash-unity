//
//  StashNativeCard.h
//  StashNative
//
//  Native iOS SDK for Stash Native checkout integration.
//

#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>

NS_ASSUME_NONNULL_BEGIN

/**
 * Configuration for custom popup sizing.
 */
@interface StashNativePopupSizeConfig : NSObject

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
@interface StashNativeModalConfig : NSObject

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
 * Configuration for card presentation (openCard).
 *
 * Card slides up from bottom on phones; centered on tablets.
 * Supports independent sizing for phone/tablet and portrait/landscape orientations.
 */
@interface StashNativeCardConfig : NSObject

/** When YES, phone card forces portrait orientation. Default NO. */
@property (nonatomic, assign) BOOL forcePortrait;
/** Phone card height ratio in portrait (0.1-1.0). Default 0.68. */
@property (nonatomic, assign) CGFloat cardHeightRatioPortrait;
/** Phone card width ratio in landscape (0.1-1.0). Default 0.9. */
@property (nonatomic, assign) CGFloat cardWidthRatioLandscape;
/** Phone card height ratio in landscape (0.1-1.0). Default 0.6. */
@property (nonatomic, assign) CGFloat cardHeightRatioLandscape;
/** Tablet width ratio in portrait (0.1-1.0). Default 0.4. */
@property (nonatomic, assign) CGFloat tabletWidthRatioPortrait;
/** Tablet height ratio in portrait (0.1-1.0). Default 0.5. */
@property (nonatomic, assign) CGFloat tabletHeightRatioPortrait;
/** Tablet width ratio in landscape (0.1-1.0). Default 0.3. */
@property (nonatomic, assign) CGFloat tabletWidthRatioLandscape;
/** Tablet height ratio in landscape (0.1-1.0). Default 0.6. */
@property (nonatomic, assign) CGFloat tabletHeightRatioLandscape;

/**
 * Creates a default card configuration.
 */
- (instancetype)init;

@end

/**
 * Protocol for receiving StashNativeCard events.
 */
@protocol StashNativeCardDelegate <NSObject>

@optional

/**
 * Called when a payment completes successfully.
 */
- (void)stashNativeCardDidCompletePayment;

/**
 * Called when a payment fails.
 */
- (void)stashNativeCardDidFailPayment;

/**
 * Called when the checkout dialog is dismissed by the user.
 */
- (void)stashNativeCardDidDismiss;

/**
 * Called when an opt-in response is received.
 * @param optinType The type of opt-in response
 */
- (void)stashNativeCardDidReceiveOptIn:(NSString *)optinType;

/**
 * Called when the checkout page finishes loading.
 * @param loadTimeMs The page load time in milliseconds
 */
- (void)stashNativeCardDidLoadPage:(double)loadTimeMs;

/**
 * Called when a network error occurs during initial page load.
 * This includes: no network connection, page load failure, or timeout (5 seconds).
 * The dialog is automatically dismissed before this callback is invoked.
 */
- (void)stashNativeCardDidEncounterNetworkError;

@end

/**
 * StashNativeCard - Native iOS SDK for Stash Native checkout integration.
 *
 * This is the main entry point for integrating Stash Native checkout into your iOS app.
 * It provides methods to display checkout cards and popups, and handles payment callbacks.
 *
 * @code
 * // Get the shared instance
 * StashNativeCard *stashNative = [StashNativeCard sharedInstance];
 *
 * // Set the delegate to receive callbacks
 * stashNative.delegate = self;
 *
 * // Open a card with default config
 * [stashNative openCardWithURL:@"https://your-checkout-url.com" config:nil];
 * @endcode
 */
@interface StashNativeCard : NSObject

/**
 * The delegate to receive StashNativeCard events.
 */
#if __has_feature(objc_arc)
@property (nonatomic, weak, nullable) id<StashNativeCardDelegate> delegate;
#else
@property (nonatomic, assign, nullable) id<StashNativeCardDelegate> delegate;
#endif

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
 * Gets the shared singleton instance of StashNativeCard.
 */
+ (instancetype)sharedInstance;

/**
 * Opens a URL in a sliding card UI.
 *
 * The card slides up from the bottom of the screen. On iPads, the card appears centered.
 * Pass nil for config to use default sizing and behavior.
 *
 * @param url The URL to load in the card
 * @param config Card sizing and orientation configuration (nil for defaults)
 */
- (void)openCardWithURL:(NSString *)url config:(nullable StashNativeCardConfig *)config NS_SWIFT_NAME(openCard(withURL:config:));

/**
 * Opens a Stash Native URL in a centered popup dialog.
 *
 * The popup appears centered on screen with a semi-transparent background.
 * Uses default sizing appropriate for the device.
 *
 * @param url The Stash Native URL to load
 */
- (void)openPopupWithURL:(NSString *)url;

/**
 * Opens a Stash Native URL in a centered popup dialog with custom sizing.
 *
 * @param url The Stash Native URL to load
 * @param sizeConfig Custom size configuration for portrait and landscape orientations
 */
- (void)openPopupWithURL:(NSString *)url sizeConfig:(nullable StashNativePopupSizeConfig *)sizeConfig;

/**
 * Opens a URL in a centered modal dialog with default configuration.
 *
 * Unlike openCardWithURL:config: which uses different presentations on phones vs iPads,
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
 * Unlike openCardWithURL:config: which uses different presentations on phones vs iPads,
 * openModalWithURL always shows a centered modal on all devices. The modal resizes
 * seamlessly when the device rotates.
 *
 * @param url The URL to load in the modal
 * @param config Configuration for sizing, drag bar, and dismiss behavior (nil for defaults)
 */
- (void)openModalWithURL:(NSString *)url config:(nullable StashNativeModalConfig *)config;

/**
 * Dismisses any currently displayed checkout dialog.
 */
- (void)dismiss;

/**
 * Resets the presentation state and dismisses any displayed dialog.
 */
- (void)resetPresentationState;

/**
 * Opens a URL in SFSafariViewController (platform browser).
 * No callbacks or configuration - simple browser presentation.
 *
 * @param url The URL to open in the browser
 */
- (void)openBrowserWithURL:(NSString *)url;

/**
 * Dismisses the currently presented SFSafariViewController.
 * iOS-only: has no effect on Android (Chrome Custom Tabs cannot be closed programmatically).
 */
- (void)closeBrowser;

/**
 * Dismisses the currently open SFSafariViewController and fires appropriate callbacks.
 * Useful for handling deeplink callbacks.
 *
 * @param success YES to fire payment success callback, NO to fire payment failure callback
 */
- (void)dismissSafariViewControllerWithResult:(BOOL)success;

@end

NS_ASSUME_NONNULL_END
