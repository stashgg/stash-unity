#import <UIKit/UIKit.h>
#import "StashNative.h"

// SPI for StashNativeCodeLink; not a host API. The core resolves the scanner class
// "StashNativeScannerViewController" by name and checks StashNativeScannerContent conformance.

NS_ASSUME_NONNULL_BEGIN

/** The active scanner card session. All calls happen on the main thread. */
@protocol StashNativeScannerHost <NSObject>
/** Copy supplied by the host app; nil uses the built-in wording. */
@property (nonatomic, readonly, copy, nullable) StashNativeScannerConfig *scannerConfig;
/** YES once a code was accepted; scanning and camera errors stop. */
@property (nonatomic, readonly) BOOL scanCompleted;
/** NO after the session started closing or was replaced. */
- (BOOL)scannerSessionIsActive;
/** Accepts the first framed code. Empty, duplicate, or late content is ignored. */
- (void)completeScan:(NSString *)content;
/** Reports a StashNativeScannerErrorDomain error to the delegate; the card stays open. */
- (void)scannerDidFailWithError:(NSError *)error;
/** Close control tapped; honored only while user dismissal is allowed. */
- (void)scannerRequestedDismissal;
@end

/** Implemented by the scanner view controller hosted inside the card. */
@protocol StashNativeScannerContent <NSObject>
#if __has_feature(objc_arc)
@property (nonatomic, weak, nullable) id<StashNativeScannerHost> scannerHost;
#else
@property (nonatomic, assign, nullable) id<StashNativeScannerHost> scannerHost;
#endif
/** Shows the Connected confirmation, then calls completion unless disposed. */
- (void)showConnectedWithCompletion:(void (^ _Nullable)(void))completion;
/** Pauses capture; the card may still be visible. */
- (void)stopScanning;
/** Releases the camera; the controller is not reused afterwards. */
- (void)dispose;
@end

NS_ASSUME_NONNULL_END
