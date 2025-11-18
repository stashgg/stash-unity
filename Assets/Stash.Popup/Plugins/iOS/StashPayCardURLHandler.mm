#import <Foundation/Foundation.h>
#import <SafariServices/SafariServices.h>
#import <WebKit/WebKit.h>
#import <QuartzCore/QuartzCore.h>
#import <objc/runtime.h>

// Unity callbacks
typedef void (*SafariViewDismissedCallback)();
typedef void (*PaymentSuccessCallback)();
typedef void (*PaymentFailureCallback)();
typedef void (*OptinResponseCallback)(const char* optinType);
typedef void (*PageLoadedCallback)(double loadTimeMs);
SafariViewDismissedCallback _safariViewDismissedCallback = NULL;
PaymentSuccessCallback _paymentSuccessCallback = NULL;
PaymentFailureCallback _paymentFailureCallback = NULL;
OptinResponseCallback _optinResponseCallback = NULL;
PageLoadedCallback _pageLoadedCallback = NULL;

// State flags
BOOL _callbackWasCalled = NO;
BOOL _isCardCurrentlyPresented = NO;
BOOL _paymentSuccessHandled = NO;
BOOL _paymentSuccessCallbackCalled = NO;

// Card configuration
static CGFloat _cardHeightRatio = 0.4;
static CGFloat _cardVerticalPosition = 1.0;
static CGFloat _cardWidthRatio = 1.0;
static CGFloat _originalCardHeightRatio = 0.4;
static CGFloat _originalCardVerticalPosition = 1.0;
static CGFloat _originalCardWidthRatio = 1.0;

// Custom popup size multipliers
// iOS defaults: portrait +10% width/+10% height, landscape +15% width/+15% height
static BOOL _useCustomPopupSize = NO;
static CGFloat _customPortraitWidthMultiplier = 1.0285;
static CGFloat _customPortraitHeightMultiplier = 1.485;
static CGFloat _customLandscapeWidthMultiplier = 1.753635;
static CGFloat _customLandscapeHeightMultiplier = 1.1385;

// Presentation modes
static BOOL _forceSafariViewController = NO;
static BOOL _usePopupPresentation = NO;
static BOOL _isCardExpanded = NO;

#define ENABLE_IPAD_SUPPORT 1

// Define a delegate class to handle Safari View Controller callbacks
@interface StashPayCardSafariDelegate : NSObject <SFSafariViewControllerDelegate, UIGestureRecognizerDelegate, WKScriptMessageHandler>
+ (instancetype)sharedInstance;
@property (nonatomic, copy) void (^safariViewDismissedCallback)(void);
@property (nonatomic, strong) UIViewController *currentPresentedVC;
@property (nonatomic, strong) UIWindow *portraitWindow;  // Separate window for iPhone portrait mode
@property (nonatomic, strong) UIWindow *previousKeyWindow;  // Store previous window to restore
@property (nonatomic, strong) UIPanGestureRecognizer *panGestureRecognizer;
@property (nonatomic, strong) UIView *dragTrayView;
@property (nonatomic, strong) UIView *navigationBarView;
@property (nonatomic, strong) NSURL *initialURL;
@property (nonatomic, assign) CGFloat initialY;
@property (nonatomic, assign) BOOL isObservingKeyboard;
@property (nonatomic, assign) BOOL isNavigationBarVisible;
@property (nonatomic, assign) BOOL isPurchaseProcessing;
- (void)handleDismiss:(UITapGestureRecognizer *)gesture;
- (void)dismissButtonTapped:(UIButton *)button;
- (void)dismissWithAnimation:(void (^)(void))completion;
- (void)handlePanGesture:(UIPanGestureRecognizer *)gesture;
- (void)handleDragTrayPanGesture:(UIPanGestureRecognizer *)gesture;
- (void)callUnityCallbackOnce;
- (void)cleanupCardInstance;
- (void)expandCardToFullScreen;
- (void)collapseCardToOriginal;
- (void)updateCardExpansionProgress:(CGFloat)progress cardView:(UIView *)cardView;
- (void)updateButtonPositionsForProgress:(CGFloat)progress cardView:(UIView *)cardView safeAreaInsets:(UIEdgeInsets)safeAreaInsets;
- (UIView *)createDragTray:(CGFloat)cardWidth;
- (UIView *)createFloatingBackButton;
- (void)showFloatingBackButton:(WKWebView *)webView;
- (void)hideFloatingBackButton:(WKWebView *)webView;
- (void)backButtonTapped:(UIButton *)button;
- (void)startKeyboardObserving;
- (void)stopKeyboardObserving;
- (void)keyboardWillShow:(NSNotification *)notification;
- (void)keyboardWillHide:(NSNotification *)notification;
@end

// WebView navigation delegate to handle loading states
@interface WebViewLoadDelegate : NSObject <WKNavigationDelegate>
@property (nonatomic, weak) WKWebView *webView;
@property (nonatomic, assign) CFAbsoluteTime pageLoadStartTime;
- (instancetype)initWithWebView:(WKWebView*)webView loadingView:(UIView*)loadingView;
@end

// WebView UI delegate to disable context menus and text selection
@interface WebViewUIDelegate : NSObject <WKUIDelegate>
@end

// Forward declarations
BOOL isRunningOniPad();
CGSize calculateiPadCardSize(CGRect screenBounds);

// Custom view controller to maintain custom frame and enforce orientation for window root VC
@interface OrientationLockedViewController : UIViewController
@property (nonatomic, assign) CGRect customFrame; // Custom frame to maintain
@property (nonatomic, assign) BOOL enforcePortrait; // YES to enforce portrait on iPhone
@property (nonatomic, assign) BOOL skipLayoutDuringInitialSetup; // Skip layout updates during initial popup setup
- (void)updateCornerRadiusMask; // Update corner radius mask layer
@end

@implementation OrientationLockedViewController

- (void)viewWillLayoutSubviews {
    [super viewWillLayoutSubviews];
    
    // Skip layout updates during initial setup to prevent interfering with animations
    if (self.skipLayoutDuringInitialSetup) {
        return;
    }
    
    CGRect screenBounds = [UIScreen mainScreen].bounds;
    UIWindow *cardWindow = self.view.window;
    
    // Update window and overlay frames on orientation change
    if (cardWindow && !CGRectEqualToRect(cardWindow.frame, screenBounds)) {
        cardWindow.frame = screenBounds;
    }
    
    UIView *overlayView = objc_getAssociatedObject(self, "overlayView");
    if (overlayView && !CGRectEqualToRect(overlayView.frame, screenBounds)) {
        overlayView.frame = screenBounds;
    }
    
    if (_usePopupPresentation) {
        // Popup mode: unified behavior for iPhone and iPad
        // Only update layout if frame actually changed (orientation change), not during initial setup
        BOOL isLandscape = UIInterfaceOrientationIsLandscape([[UIApplication sharedApplication] statusBarOrientation]);
        
        CGFloat smallerDimension = fmin(screenBounds.size.width, screenBounds.size.height);
        CGFloat percentage = isRunningOniPad() ? 0.5 : 0.75;
        CGFloat baseSize = fmax(
            isRunningOniPad() ? 400.0 : 300.0,
            fmin(isRunningOniPad() ? 500.0 : 500.0, smallerDimension * percentage)
        );
        
        CGFloat portraitWidthMultiplier = _useCustomPopupSize ? _customPortraitWidthMultiplier : 1.0285;
        CGFloat portraitHeightMultiplier = _useCustomPopupSize ? _customPortraitHeightMultiplier : 1.485;
        CGFloat landscapeWidthMultiplier = _useCustomPopupSize ? _customLandscapeWidthMultiplier : 1.753635;
        CGFloat landscapeHeightMultiplier = _useCustomPopupSize ? _customLandscapeHeightMultiplier : 1.1385;
        
        CGFloat popupWidth = baseSize * (isLandscape ? landscapeWidthMultiplier : portraitWidthMultiplier);
        CGFloat popupHeight = baseSize * (isLandscape ? landscapeHeightMultiplier : portraitHeightMultiplier);
        
        CGRect newFrame = CGRectMake(
            (screenBounds.size.width - popupWidth) / 2,
            (screenBounds.size.height - popupHeight) / 2,
            popupWidth,
            popupHeight
        );
        
        // Only animate if frame actually changed (orientation change), and use instant update if frame is close
        if (!CGRectEqualToRect(self.view.frame, newFrame)) {
            // Check if this is a significant change (orientation) or just a minor adjustment
            CGFloat frameDifference = fabs(self.view.frame.origin.x - newFrame.origin.x) + 
                                     fabs(self.view.frame.origin.y - newFrame.origin.y) +
                                     fabs(self.view.frame.size.width - newFrame.size.width) +
                                     fabs(self.view.frame.size.height - newFrame.size.height);
            
            if (frameDifference > 50.0) {
                // Significant change (orientation) - animate smoothly
                [UIView animateWithDuration:0.3 animations:^{
                    self.view.frame = newFrame;
                    self.customFrame = newFrame;
                } completion:^(BOOL finished) {
                    [self updateCornerRadiusMask];
                }];
            } else {
                // Minor adjustment - update instantly without animation to prevent sliding
                [CATransaction begin];
                [CATransaction setDisableActions:YES];
                self.view.frame = newFrame;
                self.customFrame = newFrame;
                [CATransaction commit];
                [self updateCornerRadiusMask];
            }
        } else {
            self.customFrame = newFrame;
            [self updateCornerRadiusMask];
        }
    } else {
        // Card mode: split implementation for iPhone and iPad
        // CRITICAL: Skip layout updates during initial setup to prevent interference with animation
        if (self.skipLayoutDuringInitialSetup) {
            return; // Don't touch anything during animation
        }
        
        // iPhone card mode: also skip if we're in the middle of a gesture
        if (!isRunningOniPad()) {
            // For iPhone, be extra careful - only update if frame actually needs to change
            // Don't interfere with gestures or animations
        }
        
        CGFloat width, height, x, y;
        
        if (isRunningOniPad()) {
            // iPad: phone-like aspect ratio, centered vertically, supports rotation
            CGFloat phoneLikeWidth = fmin(400.0, screenBounds.size.width * 0.9);
            width = phoneLikeWidth;
            height = screenBounds.size.height * _cardHeightRatio;
            x = (screenBounds.size.width - width) / 2;
            y = (screenBounds.size.height - height) / 2;
        } else {
            // iPhone: forced portrait, use ratios, slides up from bottom
            // Ensure portrait orientation (narrower dimension = width)
            if (screenBounds.size.width > screenBounds.size.height) {
                CGFloat temp = screenBounds.size.width;
                screenBounds.size.width = screenBounds.size.height;
                screenBounds.size.height = temp;
            }
            width = screenBounds.size.width * _cardWidthRatio;
            height = screenBounds.size.height * _cardHeightRatio;
            x = (screenBounds.size.width - width) / 2;
            y = screenBounds.size.height * _cardVerticalPosition - height;
            if (y < 0) y = 0;
        }
        
        CGRect newFrame = CGRectMake(x, y, width, height);
        
        if (!CGRectEqualToRect(self.view.frame, newFrame)) {
            // Check if this is a significant change (orientation) or just a minor adjustment
            CGFloat frameDifference = fabs(self.view.frame.origin.x - newFrame.origin.x) + 
                                     fabs(self.view.frame.origin.y - newFrame.origin.y) +
                                     fabs(self.view.frame.size.width - newFrame.size.width) +
                                     fabs(self.view.frame.size.height - newFrame.size.height);
            
            if (frameDifference > 50.0) {
                // Significant change (orientation) - animate smoothly
                [UIView animateWithDuration:0.3 animations:^{
                    self.view.frame = newFrame;
                    self.customFrame = newFrame;
                    
                    // Update drag tray handle position to stay centered during orientation change
                    UIView *dragTray = [self.view viewWithTag:8888];
                    if (dragTray) {
                        dragTray.frame = CGRectMake(0, 0, newFrame.size.width, 44);
                        UIView *handle = [dragTray viewWithTag:8889];
                        if (handle) {
                            CGFloat handleX = (newFrame.size.width / 2.0) - 20.0;
                            handle.frame = CGRectMake(handleX, 12, 40, 5);
                        }
                    }
                } completion:^(BOOL finished) {
                    [self updateCornerRadiusMask];
                }];
            } else {
                // Minor adjustment - update instantly without animation to prevent sliding
                [CATransaction begin];
                [CATransaction setDisableActions:YES];
                self.view.frame = newFrame;
                self.customFrame = newFrame;
                
                // Update drag tray handle position to stay centered
                UIView *dragTray = [self.view viewWithTag:8888];
                if (dragTray) {
                    dragTray.frame = CGRectMake(0, 0, newFrame.size.width, 44);
                    UIView *handle = [dragTray viewWithTag:8889];
                    if (handle) {
                        CGFloat handleX = (newFrame.size.width / 2.0) - 20.0;
                        handle.frame = CGRectMake(handleX, 12, 40, 5);
                    }
                }
                
                [CATransaction commit];
                [self updateCornerRadiusMask];
            }
        } else {
            self.customFrame = newFrame;
            [self updateCornerRadiusMask];
        }
    }
}

- (void)updateCornerRadiusMask {
    CAShapeLayer *maskLayer = (CAShapeLayer *)self.view.layer.mask;
    if (!maskLayer) {
        maskLayer = [[CAShapeLayer alloc] init];
        self.view.layer.mask = maskLayer;
    }
    
    CGRect viewBounds = self.view.bounds;
    UIRectCorner cornersToRound;
    
    // iPad: always round all corners
    if (isRunningOniPad()) {
        cornersToRound = UIRectCornerAllCorners;
    } else if (_usePopupPresentation) {
        // Popup mode: always round all corners
        cornersToRound = UIRectCornerAllCorners;
    } else {
        // iPhone card mode: round based on position
        if (_cardVerticalPosition < 0.1) {
            cornersToRound = UIRectCornerBottomLeft | UIRectCornerBottomRight;
        } else if (_cardVerticalPosition > 0.9) {
            cornersToRound = UIRectCornerTopLeft | UIRectCornerTopRight;
        } else {
            cornersToRound = UIRectCornerAllCorners;
        }
    }
    
    UIBezierPath *maskPath = [UIBezierPath bezierPathWithRoundedRect:viewBounds
                                                  byRoundingCorners:cornersToRound
                                                        cornerRadii:CGSizeMake(12.0, 12.0)];
    maskLayer.frame = viewBounds;
    maskLayer.path = maskPath.CGPath;
}

- (UIInterfaceOrientationMask)supportedInterfaceOrientations {
    if (self.enforcePortrait && !isRunningOniPad()) {
        return UIInterfaceOrientationMaskPortrait;
    }
    
    // Allow all orientations for popup mode or iPad card mode
    if (_usePopupPresentation || isRunningOniPad()) {
        return UIInterfaceOrientationMaskAll;
    }
    
    UIInterfaceOrientation currentOrientation = [[UIApplication sharedApplication] statusBarOrientation];
    return (1 << currentOrientation);
}

- (BOOL)shouldAutorotate {
    // Allow rotation for popup mode or iPad card mode
    return _usePopupPresentation || isRunningOniPad();
}

- (UIInterfaceOrientation)preferredInterfaceOrientationForPresentation {
    if (self.enforcePortrait && !isRunningOniPad()) {
        return UIInterfaceOrientationPortrait;
    }
    return [[UIApplication sharedApplication] statusBarOrientation];
}

@end

@implementation WebViewLoadDelegate {
    __weak WKWebView* _webView;
    UIView* _loadingView;
    NSTimer* _timeoutTimer;
}

- (instancetype)initWithWebView:(WKWebView*)webView loadingView:(UIView*)loadingView {
    self = [super init];
    if (self) {
        _webView = webView;
        self.webView = webView; // Store weak reference for navigation bar functionality
        _loadingView = loadingView;
        
        // Create a fallback timer to handle cases where navigation events aren't fired
        _timeoutTimer = [NSTimer scheduledTimerWithTimeInterval:0.1  // Very fast timeout for first paint
                                                        target:self 
                                                      selector:@selector(handleTimeout:) 
                                                      userInfo:nil 
                                                       repeats:NO];
    }
    return self;
}

// Handle all navigation to keep everything within the card
- (void)webView:(WKWebView *)webView decidePolicyForNavigationAction:(WKNavigationAction *)navigationAction decisionHandler:(void (^)(WKNavigationActionPolicy))decisionHandler {
    
    NSURL *url = navigationAction.request.URL;
    NSString *urlString = url.absoluteString;
    
    // Check if URL contains klarna, paypal, or stripe and show/hide navigation bar accordingly
    BOOL shouldShowNavigationBar = ([urlString.lowercaseString containsString:@"klarna"] || 
                                   [urlString.lowercaseString containsString:@"paypal"] ||
                                   [urlString.lowercaseString containsString:@"payments.stripe.com"]);
    
    if (shouldShowNavigationBar && ![[StashPayCardSafariDelegate sharedInstance] isNavigationBarVisible]) {
        [[StashPayCardSafariDelegate sharedInstance] showFloatingBackButton:webView];
    } else if (!shouldShowNavigationBar && [[StashPayCardSafariDelegate sharedInstance] isNavigationBarVisible]) {
        [[StashPayCardSafariDelegate sharedInstance] hideFloatingBackButton:webView];
    }
    
    if ([url.scheme isEqualToString:@"tel"] ||
        [url.scheme isEqualToString:@"mailto"] ||
        [url.scheme isEqualToString:@"sms"]) {
        decisionHandler(WKNavigationActionPolicyCancel);
        [[UIApplication sharedApplication] openURL:url options:@{} completionHandler:nil];
        return;
    }
    
    if ([urlString containsString:@"apps.apple.com"] ||
        [urlString containsString:@"itunes.apple.com"]) {
        decisionHandler(WKNavigationActionPolicyCancel);
        [[UIApplication sharedApplication] openURL:url options:@{} completionHandler:nil];
        return;
    }
    
    decisionHandler(WKNavigationActionPolicyAllow);
}

- (void)handleTimeout:(NSTimer*)timer {
    [self showWebViewAndRemoveLoading];
}

- (void)showWebViewAndRemoveLoading {
    if (_timeoutTimer) {
        [_timeoutTimer invalidate];
        _timeoutTimer = nil;
    }
    
    if (_webView.alpha < 0.01) {
        UIColor *backgroundColor;
        if (@available(iOS 13.0, *)) {
            UIUserInterfaceStyle currentStyle = [UITraitCollection currentTraitCollection].userInterfaceStyle;
            backgroundColor = (currentStyle == UIUserInterfaceStyleDark) ? [UIColor blackColor] : [UIColor systemBackgroundColor];
        } else {
            backgroundColor = [UIColor whiteColor];
        }
        
        _webView.backgroundColor = backgroundColor;
        _webView.scrollView.backgroundColor = backgroundColor;
        _webView.scrollView.opaque = YES;
        _webView.opaque = YES;
        
        if (@available(iOS 13.0, *)) {
            UIUserInterfaceStyle currentStyle = [UITraitCollection currentTraitCollection].userInterfaceStyle;
            if (currentStyle == UIUserInterfaceStyleDark) {
                NSString *forceColor = @"document.documentElement.style.backgroundColor = 'black'; \
                                      document.body.style.backgroundColor = 'black'; \
                                      var style = document.createElement('style'); \
                                      style.innerHTML = 'body, html { background-color: black !important; }'; \
                                      document.head.appendChild(style);";
                [_webView evaluateJavaScript:forceColor completionHandler:nil];
            }
        }
        
        [UIView animateWithDuration:0.2 delay:0 options:UIViewAnimationOptionCurveEaseInOut animations:^{
            self->_loadingView.alpha = 0.0;
            self->_webView.alpha = 1.0;
        } completion:^(BOOL finished) {
            [self->_loadingView removeFromSuperview];
        }];
    }
}

- (void)webView:(WKWebView *)webView didFinishNavigation:(WKNavigation *)navigation {
    if (self.pageLoadStartTime > 0) {
        CFAbsoluteTime loadEndTime = CFAbsoluteTimeGetCurrent();
        double loadTimeSeconds = loadEndTime - self.pageLoadStartTime;
        double loadTimeMs = loadTimeSeconds * 1000.0;
        
        if (_pageLoadedCallback != NULL) {
            dispatch_async(dispatch_get_main_queue(), ^{
                _pageLoadedCallback(loadTimeMs);
            });
        }
        
        self.pageLoadStartTime = 0;
    }
    
    __weak WebViewLoadDelegate *weakSelf = self;
    __block void (^checkPageReady)(void);
    checkPageReady = ^{
        NSString *readyCheck = @"(function() { \
            if (document.readyState !== 'complete') return false; \
            if (document.documentElement.style.display === 'none') return false; \
            if (document.body === null) return false; \
            if (window.getComputedStyle(document.body).display === 'none') return false; \
            return true; \
        })()";
        
        [webView evaluateJavaScript:readyCheck completionHandler:^(id result, NSError *error) {
            if ([result boolValue]) {
                // Page is ready - show webview and remove loading
                WebViewLoadDelegate *strongSelf = weakSelf;
                if (strongSelf) {
                    if (@available(iOS 13.0, *)) {
                        UIUserInterfaceStyle currentStyle = [UITraitCollection currentTraitCollection].userInterfaceStyle;
                        if (currentStyle == UIUserInterfaceStyleDark) {
                            NSString *forceColor = @"document.documentElement.style.backgroundColor = 'black'; \
                                                  document.body.style.backgroundColor = 'black'; \
                                                  var style = document.createElement('style'); \
                                                  style.innerHTML = 'body, html { background-color: black !important; }'; \
                                                  document.head.appendChild(style);";
                            [webView evaluateJavaScript:forceColor completionHandler:^(id result, NSError *error) {
                                [strongSelf showWebViewAndRemoveLoading];
                            }];
                        } else {
                            [strongSelf showWebViewAndRemoveLoading];
                        }
                    } else {
                        [strongSelf showWebViewAndRemoveLoading];
                    }
                }
            } else {
                __weak void (^weakCheckPageReady)(void) = checkPageReady;
                dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.1 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
                    if (weakCheckPageReady) {
                        weakCheckPageReady();
                    }
                });
            }
        }];
    };
    
    checkPageReady();
}

- (void)webView:(WKWebView *)webView didCommitNavigation:(WKNavigation *)navigation {
    // Show webview immediately for iPhone card mode (not iPad)
    if (!_usePopupPresentation && !isRunningOniPad()) {
        [self showWebViewAndRemoveLoading];
    }
}

- (void)webView:(WKWebView *)webView didFailNavigation:(WKNavigation *)navigation withError:(NSError *)error {
    // Show webview even on error after a brief delay
    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.3 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
        [self showWebViewAndRemoveLoading];
    });
}

- (void)webView:(WKWebView *)webView didFailProvisionalNavigation:(WKNavigation *)navigation withError:(NSError *)error {
    // Show webview even on error after a brief delay
    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.3 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
        [self showWebViewAndRemoveLoading];
    });
}

- (void)dealloc {
    if (_timeoutTimer) {
        [_timeoutTimer invalidate];
        _timeoutTimer = nil;
    }
}

@end

@implementation WebViewUIDelegate

// Disable context menu completely (iOS 13+)
- (void)webView:(WKWebView *)webView contextMenuConfigurationForElement:(WKContextMenuElementInfo *)elementInfo completionHandler:(void (^)(UIContextMenuConfiguration *))completionHandler API_AVAILABLE(ios(13.0)) {
    completionHandler(nil);
}

// Disable context menu commit (iOS 13+)
- (void)webView:(WKWebView *)webView contextMenuForElement:(WKContextMenuElementInfo *)elementInfo willCommitWithAnimator:(id<UIContextMenuInteractionCommitAnimating>)animator API_AVAILABLE(ios(13.0)) {
}

// Block context menu will display (iOS 13+)
- (void)webView:(WKWebView *)webView contextMenuConfigurationForElement:(WKContextMenuElementInfo *)elementInfo willDisplayMenuWithAnimator:(id<UIContextMenuInteractionAnimating>)animator API_AVAILABLE(ios(13.0)) {
}

// Block context menu did end (iOS 13+)
- (void)webView:(WKWebView *)webView contextMenuDidEndForElement:(WKContextMenuElementInfo *)elementInfo API_AVAILABLE(ios(13.0)) {
}

// Disable file upload (prevents image selection dialogs)
- (void)webView:(WKWebView *)webView runOpenPanelWithParameters:(id)parameters initiatedByFrame:(WKFrameInfo *)frame completionHandler:(void (^)(NSArray<NSURL *> *))completionHandler {
    completionHandler(nil);
}

// Handle new window requests by loading them in the same WebView
- (WKWebView *)webView:(WKWebView *)webView createWebViewWithConfiguration:(WKWebViewConfiguration *)configuration forNavigationAction:(WKNavigationAction *)navigationAction windowFeatures:(WKWindowFeatures *)windowFeatures {
    // Instead of creating a new window, load the URL in the existing WebView
    // This prevents target="_blank" links and window.open() from opening new windows
    
    NSURL *url = navigationAction.request.URL;
    NSString *urlString = url.absoluteString;
    
    // Check for specific schemes that should open externally
    if ([url.scheme isEqualToString:@"tel"] ||
        [url.scheme isEqualToString:@"mailto"] ||
        [url.scheme isEqualToString:@"sms"] ||
        [urlString containsString:@"apps.apple.com"] ||
        [urlString containsString:@"itunes.apple.com"]) {
        // Open these externally and return nil to prevent new window
        [[UIApplication sharedApplication] openURL:url options:@{} completionHandler:nil];
        return nil;
    }
    
    // For all other URLs, load them in the existing WebView
    [webView loadRequest:navigationAction.request];
    
    // Return nil to prevent the new window from being created
    return nil;
}

// Disable JavaScript alerts/prompts that might interfere
- (void)webView:(WKWebView *)webView runJavaScriptAlertPanelWithMessage:(NSString *)message initiatedByFrame:(WKFrameInfo *)frame completionHandler:(void (^)(void))completionHandler {
    completionHandler();
}

- (void)webView:(WKWebView *)webView runJavaScriptConfirmPanelWithMessage:(NSString *)message initiatedByFrame:(WKFrameInfo *)frame completionHandler:(void (^)(BOOL))completionHandler {
    completionHandler(NO);
}

- (void)webView:(WKWebView *)webView runJavaScriptTextInputPanelWithPrompt:(NSString *)prompt defaultText:(NSString *)defaultText initiatedByFrame:(WKFrameInfo *)frame completionHandler:(void (^)(NSString *))completionHandler {
    completionHandler(nil);
}

@end

@implementation StashPayCardSafariDelegate

+ (instancetype)sharedInstance {
    static StashPayCardSafariDelegate *sharedInstance = nil;
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        sharedInstance = [[StashPayCardSafariDelegate alloc] init];
    });
    return sharedInstance;
}

// Helper function to get the global callback (avoids instance variable shadowing)
static SafariViewDismissedCallback GetGlobalSafariViewDismissedCallback() {
    extern SafariViewDismissedCallback _safariViewDismissedCallback;
    return _safariViewDismissedCallback;
}

// Method to ensure callback is only called once
- (void)callUnityCallbackOnce {
    if (!_callbackWasCalled) {
        // Get the global callback through helper function to avoid instance variable shadowing
        SafariViewDismissedCallback globalCallback = GetGlobalSafariViewDismissedCallback();
        
        if (globalCallback != NULL) {
            _callbackWasCalled = YES;
            _isCardCurrentlyPresented = NO; // Reset the presentation flag
            
            // Clear the block property to break retain cycles (it wraps the C callback)
            self.safariViewDismissedCallback = nil;
            
            dispatch_async(dispatch_get_main_queue(), ^{
                globalCallback();
            });
        }
    }
}

- (void)cleanupCardInstance {
    [self stopKeyboardObserving];
    
    if (self.dragTrayView) {
        [self.dragTrayView removeFromSuperview];
        self.dragTrayView = nil;
    }
    
    if (self.navigationBarView) {
        [self.navigationBarView removeFromSuperview];
        self.navigationBarView = nil;
    }
    
    if (self.panGestureRecognizer && self.currentPresentedVC) {
        [self.currentPresentedVC.view removeGestureRecognizer:self.panGestureRecognizer];
        self.panGestureRecognizer.delegate = nil;
        self.panGestureRecognizer = nil;
    }
    
    if (self.currentPresentedVC) {
        // Clean up all associated objects first
        objc_setAssociatedObject(self.currentPresentedVC, "webViewDelegate", nil, OBJC_ASSOCIATION_RETAIN_NONATOMIC);
        objc_setAssociatedObject(self.currentPresentedVC, "webViewUIDelegate", nil, OBJC_ASSOCIATION_RETAIN_NONATOMIC);
        objc_setAssociatedObject(self.currentPresentedVC, "overlayView", nil, OBJC_ASSOCIATION_RETAIN_NONATOMIC);
        objc_setAssociatedObject(self.currentPresentedVC, "cardWindow", nil, OBJC_ASSOCIATION_RETAIN_NONATOMIC);
        objc_setAssociatedObject(self.currentPresentedVC, "overlayViewForAnimation", nil, OBJC_ASSOCIATION_RETAIN_NONATOMIC);
        objc_setAssociatedObject(self.currentPresentedVC, "overlayOpacity", nil, OBJC_ASSOCIATION_RETAIN_NONATOMIC);
        objc_setAssociatedObject(self.currentPresentedVC, "setupCompletionBlock", nil, OBJC_ASSOCIATION_COPY_NONATOMIC);
        
        // Clean up WebView and remove from superview
        for (UIView *subview in [self.currentPresentedVC.view.subviews copy]) {
            if ([subview isKindOfClass:NSClassFromString(@"WKWebView")]) {
                WKWebView *webView = (WKWebView *)subview;
                webView.navigationDelegate = nil;
                webView.UIDelegate = nil;
                [webView.configuration.userContentController removeScriptMessageHandlerForName:@"stashPaymentSuccess"];
                [webView.configuration.userContentController removeScriptMessageHandlerForName:@"stashPaymentFailure"];
                [webView.configuration.userContentController removeScriptMessageHandlerForName:@"stashPurchaseProcessing"];
                [webView.configuration.userContentController removeScriptMessageHandlerForName:@"stashOptin"];
                [webView stopLoading];
                [webView removeFromSuperview];
                break;
            }
        }
        
        // Clean up overlay view and any dismiss buttons
        UIView *overlayView = objc_getAssociatedObject(self.currentPresentedVC, "overlayView");
        if (overlayView) {
            for (UIView *subview in [overlayView.subviews copy]) {
                [subview removeFromSuperview];
            }
            [overlayView removeFromSuperview];
        }
    }
    
    // Clean up portrait window if it exists
    if (self.portraitWindow) {
        // Dismiss any presented view controllers first
        if (self.portraitWindow.rootViewController) {
            [self.portraitWindow.rootViewController dismissViewControllerAnimated:NO completion:nil];
        }
        
        self.portraitWindow.hidden = YES;
        self.portraitWindow.rootViewController = nil;
        
        // Restore previous key window
        if (self.previousKeyWindow) {
            [self.previousKeyWindow makeKeyAndVisible];
            self.previousKeyWindow = nil;
        }
        
        self.portraitWindow = nil;
    }
    
    // Don't clear safariViewDismissedCallback here - it's needed for callUnityCallbackOnce
    // It will be cleared after the callback is called
    
    self.currentPresentedVC = nil;
    self.initialURL = nil;
    
    self.isNavigationBarVisible = NO;
    self.isPurchaseProcessing = NO;
    _isCardExpanded = NO;
    _isCardCurrentlyPresented = NO;
    _usePopupPresentation = NO;
    _useCustomPopupSize = NO;
    _callbackWasCalled = NO;
    _paymentSuccessHandled = NO;
    _paymentSuccessCallbackCalled = NO;
}

- (void)safariViewControllerDidFinish:(SFSafariViewController *)controller {
    if (_forceSafariViewController) {
        if (_safariViewDismissedCallback != NULL) {
            dispatch_async(dispatch_get_main_queue(), ^{
                // Explicitly capture self - this is intentional
                (void)self;
                _safariViewDismissedCallback();
            });
        }
    } else {
        [self cleanupCardInstance];
        [self callUnityCallbackOnce];
    }
}

- (void)handleDismiss:(UITapGestureRecognizer *)gesture {
    if (self.currentPresentedVC) {
        [self dismissWithAnimation:^{
            [self cleanupCardInstance];
            [self callUnityCallbackOnce];
        }];
    }
}

- (void)dismissButtonTapped:(UIButton *)button {
    if (self.isPurchaseProcessing) return;
    
    if (self.currentPresentedVC) {
        [self dismissWithAnimation:^{
            [self cleanupCardInstance];
            [self callUnityCallbackOnce];
        }];
    }
}

- (void)dismissWithAnimation:(void (^)(void))completion {
    if (!self.currentPresentedVC) {
        if (completion) completion();
        return;
    }
    
    // All presentations now use window-based approach
    CGRect screenBounds = [UIScreen mainScreen].bounds;
    CGFloat dismissY = screenBounds.size.height;
    
    OrientationLockedViewController *containerVC = (OrientationLockedViewController *)self.currentPresentedVC;
    UIView *overlayView = objc_getAssociatedObject(containerVC, "overlayView");
    
    // Disable layout updates during dismissal to prevent glitching
    containerVC.skipLayoutDuringInitialSetup = YES;
    
    CGFloat animationDuration = _usePopupPresentation ? 0.2 : 0.3;
    
    [UIView animateWithDuration:animationDuration delay:0 options:UIViewAnimationOptionCurveEaseInOut animations:^{
        if (_usePopupPresentation) {
            // Popup: fade out and scale down
            containerVC.view.alpha = 0.0;
            containerVC.view.transform = CGAffineTransformMakeScale(0.9, 0.9);
        } else {
            // Card: slide down off screen
            CGRect frame = containerVC.view.frame;
            frame.origin.y = dismissY;
            containerVC.customFrame = frame;
            containerVC.view.frame = frame;
        }
        
        // Fade out overlay
        if (overlayView) {
            overlayView.backgroundColor = [UIColor colorWithWhite:0.0 alpha:0.0];
        }
    } completion:^(BOOL finished) {
        // Re-enable layout updates after dismissal completes
        containerVC.skipLayoutDuringInitialSetup = NO;
        if (completion) completion();
    }];
}

- (void)handlePanGesture:(UIPanGestureRecognizer *)gesture {
    if (!self.currentPresentedVC) return;
    if (self.isPurchaseProcessing) return;
    
    // iPhone only - iPad doesn't use pan gesture on card
    if (isRunningOniPad()) return;
    
    UIView *view = self.currentPresentedVC.view;
    CGFloat height = view.frame.size.height;
    
    // Use window as reference view for accurate gesture tracking
    UIView *referenceView = self.portraitWindow ? self.portraitWindow : view.superview;
    CGPoint translation = [gesture translationInView:referenceView];
    CGPoint velocity = [gesture velocityInView:referenceView];
    
    // iPhone dismiss gesture: slide-down to dismiss, slide-up if near top
    BOOL isNearTop = _cardVerticalPosition < 0.1;
    BOOL allowUpward = isNearTop;
    BOOL allowDownward = YES;
    
    switch (gesture.state) {
        case UIGestureRecognizerStateBegan: {
            // CRITICAL: Store initial position and disable layout updates
            self.initialY = view.frame.origin.y;
            if ([self.currentPresentedVC isKindOfClass:[OrientationLockedViewController class]]) {
                OrientationLockedViewController *containerVC = (OrientationLockedViewController *)self.currentPresentedVC;
                containerVC.skipLayoutDuringInitialSetup = YES;
            }
            break;
        }
            
        case UIGestureRecognizerStateChanged: {
            // Calculate new Y position based on allowed direction
            CGFloat newY = self.initialY;
            
            if (allowUpward && translation.y < 0) {
                // Allow upward movement
                newY = self.initialY + translation.y;
            } else if (allowDownward && translation.y > 0) {
                // Allow downward movement
                newY = self.initialY + translation.y;
                // Prevent going below screen bottom
                CGFloat maxY = referenceView.bounds.size.height;
                if (newY > maxY) newY = maxY;
            } else {
                // Movement not allowed in this direction
                return;
            }
            
            // CRITICAL: Update frame directly with CATransaction to prevent layout interference
            [CATransaction begin];
            [CATransaction setDisableActions:YES];
            view.frame = CGRectMake(view.frame.origin.x, newY, view.frame.size.width, height);
            [CATransaction commit];
            
            // Update customFrame to match
            if ([self.currentPresentedVC isKindOfClass:[OrientationLockedViewController class]]) {
                OrientationLockedViewController *containerVC = (OrientationLockedViewController *)self.currentPresentedVC;
                containerVC.customFrame = view.frame;
            }
            
            // Adjust background opacity based on position
            CGFloat maxTravel = height;
            CGFloat currentTravel = fabs(newY - self.initialY);
            CGFloat ratio = 1.0 - (currentTravel / maxTravel);
            if (ratio < 0) ratio = 0;
            
            // Update overlay opacity
            UIView *overlayView = objc_getAssociatedObject(self.currentPresentedVC, "overlayView");
            if (overlayView) {
                CGFloat baseOpacity = isRunningOniPad() ? 0.25 : 0.4;
                overlayView.backgroundColor = [UIColor colorWithWhite:0.0 alpha:baseOpacity * ratio];
            }
            break;
        }
            
        case UIGestureRecognizerStateEnded:
        case UIGestureRecognizerStateCancelled: {
            // Get the velocity of the gesture (already retrieved above)
            CGFloat currentY = view.frame.origin.y;
            CGFloat dismissThreshold = height * 0.3;
            CGFloat screenHeight = referenceView.bounds.size.height;
            
            // Determine if we should dismiss based on position and velocity
            BOOL shouldDismiss = NO;
            CGFloat finalY = 0;
            
            // Check for downward dismissal
            if (allowDownward && (velocity.y > 300 || currentY > (self.initialY + dismissThreshold))) {
                shouldDismiss = YES;
                finalY = screenHeight;
            }
            // Check for upward dismissal (iPhone top-positioned cards only)
            else if (allowUpward && (velocity.y < -300 || currentY < (self.initialY - dismissThreshold))) {
                shouldDismiss = YES;
                finalY = -height;
            }
            
            if (shouldDismiss) {
                // Animate the rest of the way out, then dismiss
                [UIView animateWithDuration:0.15 animations:^{
                    view.frame = CGRectMake(view.frame.origin.x, finalY, view.frame.size.width, height);
                    
                    // Update customFrame
                    if ([self.currentPresentedVC isKindOfClass:[OrientationLockedViewController class]]) {
                        OrientationLockedViewController *containerVC = (OrientationLockedViewController *)self.currentPresentedVC;
                        containerVC.customFrame = view.frame;
                    }
                    
                    // Fade out overlay
                    UIView *overlayView = objc_getAssociatedObject(self.currentPresentedVC, "overlayView");
                    if (overlayView) {
                        overlayView.backgroundColor = [UIColor colorWithWhite:0.0 alpha:0.0];
                    }
                } completion:^(BOOL finished) {
                    // Re-enable layout updates
                    if ([self.currentPresentedVC isKindOfClass:[OrientationLockedViewController class]]) {
                        OrientationLockedViewController *containerVC = (OrientationLockedViewController *)self.currentPresentedVC;
                        containerVC.skipLayoutDuringInitialSetup = NO;
                    }
                    
                    [self.currentPresentedVC dismissViewControllerAnimated:NO completion:^{
                        [self cleanupCardInstance];
                        [self callUnityCallbackOnce];
                    }];
                }];
            } else {
                // Animate back to original position with spring animation
                [UIView animateWithDuration:0.2
                                     delay:0
                        usingSpringWithDamping:0.7
                         initialSpringVelocity:0
                                       options:UIViewAnimationOptionCurveEaseOut
                                    animations:^{
                    view.frame = CGRectMake(view.frame.origin.x, self.initialY, view.frame.size.width, height);
                    
                    // Update customFrame
                    if ([self.currentPresentedVC isKindOfClass:[OrientationLockedViewController class]]) {
                        OrientationLockedViewController *containerVC = (OrientationLockedViewController *)self.currentPresentedVC;
                        containerVC.customFrame = view.frame;
                    }
                    
                    CGFloat baseOpacity = _isCardExpanded ? 0.6 : 0.4;
                    
                    // Update overlay opacity
                    UIView *overlayView = objc_getAssociatedObject(self.currentPresentedVC, "overlayView");
                    if (overlayView) {
                        overlayView.backgroundColor = [UIColor colorWithWhite:0.0 alpha:baseOpacity];
                    }
                } completion:^(BOOL finished) {
                    // Re-enable layout updates after gesture completes
                    if ([self.currentPresentedVC isKindOfClass:[OrientationLockedViewController class]]) {
                        OrientationLockedViewController *containerVC = (OrientationLockedViewController *)self.currentPresentedVC;
                        containerVC.skipLayoutDuringInitialSetup = NO;
                    }
                }];
            }
            break;
        }
            
        default:
            break;
    }
}

- (BOOL)gestureRecognizer:(UIGestureRecognizer *)gestureRecognizer shouldRecognizeSimultaneouslyWithGestureRecognizer:(UIGestureRecognizer *)otherGestureRecognizer {
    if ([gestureRecognizer.view isEqual:self.dragTrayView] || [otherGestureRecognizer.view isEqual:self.dragTrayView]) {
        return NO;
    }
    return YES;
}

- (BOOL)gestureRecognizerShouldBegin:(UIGestureRecognizer *)gestureRecognizer {
    return YES;
}

// Note: Rotation locking is now naturally handled by the window's rootViewController
// No explicit lock/unlock functions needed

// Helper function to detect if we're running on iPad
BOOL isRunningOniPad() {
    // Check compile-time flag first
    #if !ENABLE_IPAD_SUPPORT
    return NO;
    #endif
    
    // Safety check: ensure we're in a UI context and UIDevice is available
    if (![NSThread isMainThread]) {
        // If not on main thread, dispatch to main thread synchronously
        __block BOOL result = NO;
        dispatch_sync(dispatch_get_main_queue(), ^{
            result = isRunningOniPad();
        });
        return result;
    }
    
    // Additional safety check for UIDevice availability
    Class UIDeviceClass = NSClassFromString(@"UIDevice");
    if (!UIDeviceClass) {
        return NO;
    }
    
    // Safe access to current device
    UIDevice *currentDevice = [UIDevice currentDevice];
    if (!currentDevice) {
        return NO;
    }
    
    BOOL isPad = (currentDevice.userInterfaceIdiom == UIUserInterfaceIdiomPad);
    return isPad;
}

// Helper function to calculate card dimensions for iPad (more squared for better UX)
CGSize calculateiPadCardSize(CGRect screenBounds) {
    if (screenBounds.size.width <= 0 || screenBounds.size.height <= 0) {
        return CGSizeMake(600, 700);
    }
    
    // Use a more squared aspect ratio for iPad (closer to 4:5 instead of iPhone's narrow 9:19.5)
    CGFloat targetAspectRatio = 0.75; // 3:4 ratio (more squared)
    
    // Scale to fit nicely on iPad (80% of screen width, 75% of height)
    CGFloat maxCardWidth = screenBounds.size.width * 0.8;
    CGFloat maxCardHeight = screenBounds.size.height * 0.75;
    
    if (maxCardWidth <= 0 || maxCardHeight <= 0) {
        return CGSizeMake(600, 700);
    }
    
    CGFloat cardWidth, cardHeight;
    
    // Calculate dimensions maintaining squared aspect ratio
    if (maxCardWidth / targetAspectRatio <= maxCardHeight) {
        // Width-constrained
        cardWidth = maxCardWidth;
        cardHeight = cardWidth / targetAspectRatio;
    } else {
        // Height-constrained
        cardHeight = maxCardHeight;
        cardWidth = cardHeight * targetAspectRatio;
    }
    
    // Ensure reasonable sizes
    if (cardWidth < 400 || cardHeight < 500 || cardWidth > screenBounds.size.width || cardHeight > screenBounds.size.height) {
        return CGSizeMake(600, 700);
    }
    
    return CGSizeMake(cardWidth, cardHeight);
}

- (void)expandCardToFullScreen {
    if (!self.currentPresentedVC) return;
    
    _isCardExpanded = YES;
    
    UIView *cardView = self.currentPresentedVC.view;
    CGRect screenBounds = [UIScreen mainScreen].bounds;
    
    // Get safe area insets to respect notch and other system UI
    UIEdgeInsets safeAreaInsets = UIEdgeInsetsZero;
    if (@available(iOS 11.0, *)) {
        // Use the parent view's safe area insets
        UIView *parentView = cardView.superview;
        if (parentView && [parentView respondsToSelector:@selector(safeAreaInsets)]) {
            safeAreaInsets = parentView.safeAreaInsets;
        }
    }
    
    // Calculate safe area dimensions for positioning UI elements
    CGFloat safeTop = safeAreaInsets.top;
    
    // Calculate full screen frame - respect safe area at top
    CGRect fullScreenFrame = CGRectMake(0, safeTop, screenBounds.size.width, screenBounds.size.height - safeTop);
    

    
    
    // Update WebView constraints to respect safe area when expanded
    // Find the WebView and update its constraints without disrupting the view hierarchy
    for (UIView *subview in cardView.subviews) {
        if ([subview isKindOfClass:NSClassFromString(@"WKWebView")]) {
            WKWebView *webView = (WKWebView *)subview;
            
            // Find and deactivate existing constraints that involve this webView
            NSMutableArray *constraintsToRemove = [NSMutableArray array];
            for (NSLayoutConstraint *constraint in cardView.constraints) {
                if (constraint.firstItem == webView || constraint.secondItem == webView) {
                    [constraintsToRemove addObject:constraint];
                }
            }
            [NSLayoutConstraint deactivateConstraints:constraintsToRemove];
            webView.translatesAutoresizingMaskIntoConstraints = NO;
            
            // Add new constraints for full screen - WebView fills entire card (drag tray overlays on top)
            [NSLayoutConstraint activateConstraints:@[
                [webView.leadingAnchor constraintEqualToAnchor:cardView.leadingAnchor],
                [webView.trailingAnchor constraintEqualToAnchor:cardView.trailingAnchor],
                [webView.topAnchor constraintEqualToAnchor:cardView.topAnchor], // No offset - fill entire card
                [webView.bottomAnchor constraintEqualToAnchor:cardView.bottomAnchor]
            ]];
            
            // Force WebView background to match system background during expansion
            if (@available(iOS 13.0, *)) {
                UIUserInterfaceStyle currentStyle = [UITraitCollection currentTraitCollection].userInterfaceStyle;
                UIColor *backgroundColor = (currentStyle == UIUserInterfaceStyleDark) ? [UIColor blackColor] : [UIColor systemBackgroundColor];
                webView.backgroundColor = backgroundColor;
                webView.scrollView.backgroundColor = backgroundColor;
            } else {
                webView.backgroundColor = [UIColor whiteColor];
                webView.scrollView.backgroundColor = [UIColor whiteColor];
            }
            
            // Configure WebView for full screen - prevent black bars with proper scroll behavior
            if (@available(iOS 11.0, *)) {
                webView.scrollView.contentInsetAdjustmentBehavior = UIScrollViewContentInsetAdjustmentNever;
            }
            webView.scrollView.contentInset = UIEdgeInsetsZero;
            webView.scrollView.scrollIndicatorInsets = UIEdgeInsetsZero;
            
            // Prevent overscroll bounce to avoid black bars in expanded state
            webView.scrollView.bounces = NO;
            webView.scrollView.alwaysBounceVertical = NO;
            webView.scrollView.alwaysBounceHorizontal = NO;
            
            // Ensure scroll view fills content properly
            webView.scrollView.showsVerticalScrollIndicator = YES;
            webView.scrollView.showsHorizontalScrollIndicator = NO;
            
            // Inject CSS to prevent web-level overscroll behaviors
            NSString *preventOverscrollScript = @"(function() {"
                "var style = document.createElement('style');"
                "style.innerHTML = '"
                    "body, html { "
                        "overscroll-behavior: none !important; "
                        "overflow-x: hidden !important; "
                        "-webkit-overflow-scrolling: touch !important; "
                    "} "
                    "* { "
                        "overscroll-behavior: none !important; "
                    "}"
                "';"
                "document.head.appendChild(style);"
            "})();";
            
            [webView evaluateJavaScript:preventOverscrollScript completionHandler:nil];
            
            break;
        }
    }
    
    // Update drag tray size for full screen (full width at card top, overlaying WebView)
    UIView *dragTray = [cardView viewWithTag:8888];
    if (dragTray) {
        dragTray.frame = CGRectMake(0, 0, fullScreenFrame.size.width, 44); // Position at top of card (card already below safe area)
        
        // Ensure drag tray overlays on top of WebView (critical for eliminating black bar)
        [cardView bringSubviewToFront:dragTray];
        
        // Update gradient layer frame for full screen
        CAGradientLayer *gradientLayer = (CAGradientLayer*)dragTray.layer.sublayers.firstObject;
        if (gradientLayer && [gradientLayer isKindOfClass:[CAGradientLayer class]]) {
            gradientLayer.frame = dragTray.bounds;
        }
        dragTray.backgroundColor = [UIColor clearColor];
        
        // Update handle position for full screen - always centered
        UIView *handle = [dragTray viewWithTag:8889];
        if (handle) {
            CGFloat handleX = (fullScreenFrame.size.width / 2.0) - 20.0;
            handle.frame = CGRectMake(handleX, 12, 40, 5);
            
            // Use high-contrast handle for overlay on web content
            handle.backgroundColor = [UIColor colorWithWhite:1.0 alpha:0.95];
            handle.layer.shadowOpacity = 0.8; // Strong shadow for visibility
        }
    }
    
    // Update button positions for full screen using consistent positioning logic
    [self updateButtonPositionsForProgress:1.0 cardView:cardView safeAreaInsets:safeAreaInsets];
    
    // Animate to full screen (edge-to-edge)
    [UIView animateWithDuration:0.2 animations:^{  // Reduced from 0.3
        cardView.frame = fullScreenFrame;
        
        // Update customFrame (all presentations now use window approach)
        if ([self.currentPresentedVC isKindOfClass:[OrientationLockedViewController class]]) {
            OrientationLockedViewController *containerVC = (OrientationLockedViewController *)self.currentPresentedVC;
            containerVC.customFrame = cardView.frame;
        }
        
        // Update overlay opacity
        UIView *overlayView = objc_getAssociatedObject(self.currentPresentedVC, "overlayView");
        if (overlayView) {
            overlayView.backgroundColor = [UIColor colorWithWhite:0.0 alpha:0.6];
        }
        
        // Ensure card background matches system background
        if (@available(iOS 13.0, *)) {
            UIUserInterfaceStyle currentStyle = [UITraitCollection currentTraitCollection].userInterfaceStyle;
            cardView.backgroundColor = (currentStyle == UIUserInterfaceStyleDark) ? [UIColor blackColor] : [UIColor systemBackgroundColor];
        } else {
            cardView.backgroundColor = [UIColor whiteColor];
        }
    } completion:^(BOOL finished) {
        // Add iOS-style rounded corners at top for full screen
        UIBezierPath *maskPath = [UIBezierPath bezierPathWithRoundedRect:cardView.bounds
                                                      byRoundingCorners:UIRectCornerTopLeft | UIRectCornerTopRight
                                                            cornerRadii:CGSizeMake(16.0, 16.0)]; // iOS standard corner radius
        
        CAShapeLayer *maskLayer = [[CAShapeLayer alloc] init];
        maskLayer.frame = cardView.bounds;
        maskLayer.path = maskPath.CGPath;
        cardView.layer.mask = maskLayer;
    }];
}

- (void)updateCardExpansionProgress:(CGFloat)progress cardView:(UIView *)cardView {
    if (!cardView) return;
    
    // Clamp progress between 0.0 and 1.0
    progress = MAX(0.0, MIN(1.0, progress));
    
    CGRect screenBounds = [UIScreen mainScreen].bounds;
    UIEdgeInsets safeAreaInsets = UIEdgeInsetsZero;
    if (@available(iOS 11.0, *)) {
        UIView *parentView = cardView.superview;
        if (parentView && [parentView respondsToSelector:@selector(safeAreaInsets)]) {
            safeAreaInsets = parentView.safeAreaInsets;
        }
    }
    CGFloat safeTop = safeAreaInsets.top;
    
    // Calculate collapsed dimensions
    CGFloat collapsedWidth, collapsedHeight, collapsedX, collapsedY;
    if (isRunningOniPad()) {
        CGSize cardSize = calculateiPadCardSize(screenBounds);
        collapsedWidth = cardSize.width;
        collapsedHeight = cardSize.height;
        collapsedX = (screenBounds.size.width - collapsedWidth) / 2;
        collapsedY = (screenBounds.size.height - collapsedHeight) / 2;
    } else {
        collapsedWidth = screenBounds.size.width * _originalCardWidthRatio;
        collapsedHeight = screenBounds.size.height * _originalCardHeightRatio;
        collapsedX = (screenBounds.size.width - collapsedWidth) / 2;
        collapsedY = screenBounds.size.height * _originalCardVerticalPosition - collapsedHeight;
        if (collapsedY < 0) collapsedY = 0;
    }
    
    // Calculate expanded dimensions
    CGFloat expandedWidth = screenBounds.size.width;
    CGFloat expandedHeight = screenBounds.size.height - safeTop;
    CGFloat expandedX = 0;
    CGFloat expandedY = safeTop;
    
    // Interpolate between collapsed and expanded
    CGFloat currentWidth = collapsedWidth + (expandedWidth - collapsedWidth) * progress;
    CGFloat currentHeight = collapsedHeight + (expandedHeight - collapsedHeight) * progress;
    CGFloat currentX = collapsedX + (expandedX - collapsedX) * progress;
    CGFloat currentY = collapsedY + (expandedY - collapsedY) * progress;
    
    // Update card frame
    cardView.frame = CGRectMake(currentX, currentY, currentWidth, currentHeight);
    
    // Update customFrame (all presentations now use window approach)
    if ([self.currentPresentedVC isKindOfClass:[OrientationLockedViewController class]]) {
        OrientationLockedViewController *containerVC = (OrientationLockedViewController *)self.currentPresentedVC;
        containerVC.customFrame = cardView.frame;
    }
    
    // Update drag tray
    UIView *dragTray = [cardView viewWithTag:8888];
    if (dragTray) {
        dragTray.frame = CGRectMake(0, 0, currentWidth, 44);
        
        // Update gradient layer
        CAGradientLayer *gradientLayer = (CAGradientLayer*)dragTray.layer.sublayers.firstObject;
        if (gradientLayer && [gradientLayer isKindOfClass:[CAGradientLayer class]]) {
            gradientLayer.frame = dragTray.bounds;
        }
        
        // Update handle position to always be centered
        UIView *handle = [dragTray viewWithTag:8889];
        if (handle) {
            // Always center the handle based on current drag tray width
            CGFloat handleX = (currentWidth / 2.0) - 20.0;
            handle.frame = CGRectMake(handleX, 12, 40, 5);
        }
    }
    
    // Update button positions with smooth interpolation
    [self updateButtonPositionsForProgress:progress cardView:cardView safeAreaInsets:safeAreaInsets];
    
    // Update corner radius based on progress
    if (progress > 0.8) {
        // Near full screen - iOS style top corners only
        UIBezierPath *maskPath = [UIBezierPath bezierPathWithRoundedRect:cardView.bounds
                                                      byRoundingCorners:UIRectCornerTopLeft | UIRectCornerTopRight
                                                            cornerRadii:CGSizeMake(16.0, 16.0)];
        CAShapeLayer *maskLayer = [[CAShapeLayer alloc] init];
        maskLayer.frame = cardView.bounds;
        maskLayer.path = maskPath.CGPath;
        cardView.layer.mask = maskLayer;
    } else {
        // Collapsed - original corner style
        UIRectCorner cornersToRound;
        if (isRunningOniPad()) {
            cornersToRound = UIRectCornerAllCorners;
        } else {
            if (_originalCardVerticalPosition < 0.1) {
                cornersToRound = UIRectCornerBottomLeft | UIRectCornerBottomRight;
            } else if (_originalCardVerticalPosition > 0.9) {
                cornersToRound = UIRectCornerTopLeft | UIRectCornerTopRight;
            } else {
                cornersToRound = UIRectCornerAllCorners;
            }
        }
        
        UIBezierPath *maskPath = [UIBezierPath bezierPathWithRoundedRect:cardView.bounds
                                                      byRoundingCorners:cornersToRound
                                                            cornerRadii:CGSizeMake(12.0, 12.0)];
        CAShapeLayer *maskLayer = [[CAShapeLayer alloc] init];
        maskLayer.frame = cardView.bounds;
        maskLayer.path = maskPath.CGPath;
        cardView.layer.mask = maskLayer;
    }
    
    // Update background opacity - lighter on iPad for cleaner appearance
    CGFloat minOpacity = isRunningOniPad() ? 0.25 : 0.4; // Lighter base on iPad
    CGFloat maxOpacity = isRunningOniPad() ? 0.45 : 0.6; // Lighter max on iPad
    CGFloat baseOpacity = minOpacity + ((maxOpacity - minOpacity) * progress);
    cardView.superview.backgroundColor = [UIColor colorWithWhite:0.0 alpha:baseOpacity];
}

- (void)updateButtonPositionsForProgress:(CGFloat)progress cardView:(UIView *)cardView safeAreaInsets:(UIEdgeInsets)safeAreaInsets {
    CGRect screenBounds = [UIScreen mainScreen].bounds;
    // CGFloat safeTop = safeAreaInsets.top;  // Unused in this method
    
    // Calculate collapsed dimensions
    CGFloat collapsedWidth, expandedWidth;
    if (isRunningOniPad()) {
        CGSize cardSize = calculateiPadCardSize(screenBounds);
        collapsedWidth = cardSize.width;
    } else {
        collapsedWidth = screenBounds.size.width * _originalCardWidthRatio;
    }
    expandedWidth = screenBounds.size.width;
    
    // Interpolate current width
    // CGFloat currentWidth = collapsedWidth + (expandedWidth - collapsedWidth) * progress;  // Unused in this method
    
    // Define button positions for collapsed and expanded states
    CGFloat collapsedTopOffset = 16 + 6; // 22px from top in collapsed state
    CGFloat expandedTopOffset = 16; // 16px from top in expanded state
    
    // Interpolate top offset
    CGFloat currentTopOffset = collapsedTopOffset + (expandedTopOffset - collapsedTopOffset) * progress;
    
    // Update back button position if visible
    UIView *backButton = [cardView viewWithTag:9999];
    if (backButton && self.navigationBarView) {
        // Calculate left offset for both states
        CGFloat collapsedLeftOffset = 16; // 16pt margin from left edge
        CGFloat expandedLeftOffset = safeAreaInsets.left + 16; // Respect left safe area + 16pt margin
        
        // Interpolate left offset
        CGFloat currentLeftOffset = collapsedLeftOffset + (expandedLeftOffset - collapsedLeftOffset) * progress;
        
        backButton.frame = CGRectMake(currentLeftOffset, currentTopOffset, 40, 40);
    }
}

- (void)collapseCardToOriginal {
    if (!self.currentPresentedVC) return;
    
    _isCardExpanded = NO;
    
    UIView *cardView = self.currentPresentedVC.view;
    CGRect screenBounds = [UIScreen mainScreen].bounds;
    
    // Calculate original position based on device type
    CGFloat width, height, x, finalY;
    
    if (isRunningOniPad()) {
        // iPad: return to centered iPhone-like card
        CGSize cardSize = calculateiPadCardSize(screenBounds);
        width = cardSize.width;
        height = cardSize.height;
        x = (screenBounds.size.width - width) / 2;
        finalY = (screenBounds.size.height - height) / 2;

    } else {
        // iPhone: return to original configured position
        width = screenBounds.size.width * _originalCardWidthRatio;
        height = screenBounds.size.height * _originalCardHeightRatio;
        x = (screenBounds.size.width - width) / 2;
        finalY = screenBounds.size.height * _originalCardVerticalPosition - height;
        if (finalY < 0) finalY = 0;

    }
    
    
    // Restore WebView constraints to edge-to-edge when collapsed  
    // Find the WebView and restore its original constraints without disrupting the view hierarchy
    for (UIView *subview in cardView.subviews) {
        if ([subview isKindOfClass:NSClassFromString(@"WKWebView")]) {
            WKWebView *webView = (WKWebView *)subview;
            
            // Find and deactivate existing constraints that involve this webView
            NSMutableArray *constraintsToRemove = [NSMutableArray array];
            for (NSLayoutConstraint *constraint in cardView.constraints) {
                if (constraint.firstItem == webView || constraint.secondItem == webView) {
                    [constraintsToRemove addObject:constraint];
                }
            }
            [NSLayoutConstraint deactivateConstraints:constraintsToRemove];
            webView.translatesAutoresizingMaskIntoConstraints = NO;
            
            // Restore edge-to-edge constraints for collapsed state (drag tray overlays)
            [NSLayoutConstraint activateConstraints:@[
                [webView.leadingAnchor constraintEqualToAnchor:cardView.leadingAnchor],
                [webView.trailingAnchor constraintEqualToAnchor:cardView.trailingAnchor],
                [webView.topAnchor constraintEqualToAnchor:cardView.topAnchor], // No offset - drag tray overlays
                [webView.bottomAnchor constraintEqualToAnchor:cardView.bottomAnchor]
            ]];
            
            // Configure WebView for collapsed state - maintain consistent settings to prevent overscroll
            if (@available(iOS 11.0, *)) {
                webView.scrollView.contentInsetAdjustmentBehavior = UIScrollViewContentInsetAdjustmentNever;
            }
            webView.scrollView.contentInset = UIEdgeInsetsZero;
            webView.scrollView.scrollIndicatorInsets = UIEdgeInsetsZero;
            
            // Prevent overscroll bounce to avoid black bars (keep consistent with initial setup)
            webView.scrollView.bounces = NO;
            webView.scrollView.alwaysBounceVertical = NO;
            webView.scrollView.alwaysBounceHorizontal = NO;
            
            // Inject CSS to prevent web-level overscroll behaviors in collapsed state too
            NSString *preventOverscrollScript = @"(function() {"
                "var style = document.createElement('style');"
                "style.innerHTML = '"
                    "body, html { "
                        "overscroll-behavior: none !important; "
                        "overflow-x: hidden !important; "
                        "-webkit-overflow-scrolling: touch !important; "
                    "} "
                    "* { "
                        "overscroll-behavior: none !important; "
                    "}"
                "';"
                "document.head.appendChild(style);"
            "})();";
            
            [webView evaluateJavaScript:preventOverscrollScript completionHandler:nil];
            
            break;
        }
    }
    
    // Update drag tray size for original card size - keep transparent
    UIView *dragTray = [cardView viewWithTag:8888];
    if (dragTray) {
        dragTray.frame = CGRectMake(0, 0, width, 44);
        
        // Ensure drag tray stays on top of WebView after constraint changes
        [cardView bringSubviewToFront:dragTray];
        
        // Update gradient layer frame for collapsed state
        CAGradientLayer *gradientLayer = (CAGradientLayer*)dragTray.layer.sublayers.firstObject;
        if (gradientLayer && [gradientLayer isKindOfClass:[CAGradientLayer class]]) {
            gradientLayer.frame = dragTray.bounds;
        }
        dragTray.backgroundColor = [UIColor clearColor];
        
        // Update handle position for collapsed state - always centered
        UIView *handle = [dragTray viewWithTag:8889];
        if (handle) {
            CGFloat handleX = (width / 2.0) - 20.0;
            handle.frame = CGRectMake(handleX, 12, 40, 5);
            
            // Use high-contrast handle consistently
            handle.backgroundColor = [UIColor colorWithWhite:1.0 alpha:0.95];
            handle.layer.shadowOpacity = 0.8; // Strong shadow for visibility
        }
    }
    
    // Update button positions for collapsed state using consistent positioning logic
    UIEdgeInsets safeAreaInsets = UIEdgeInsetsZero;
    if (@available(iOS 11.0, *)) {
        UIView *parentView = cardView.superview;
        if (parentView && [parentView respondsToSelector:@selector(safeAreaInsets)]) {
            safeAreaInsets = parentView.safeAreaInsets;
        }
    }
    [self updateButtonPositionsForProgress:0.0 cardView:cardView safeAreaInsets:safeAreaInsets];
    
    // Animate back to original size
    [UIView animateWithDuration:0.2 animations:^{  // Reduced from 0.3
        cardView.frame = CGRectMake(x, finalY, width, height);
        
        // Update customFrame (all presentations now use window approach)
        if ([self.currentPresentedVC isKindOfClass:[OrientationLockedViewController class]]) {
            OrientationLockedViewController *containerVC = (OrientationLockedViewController *)self.currentPresentedVC;
            containerVC.customFrame = cardView.frame;
        }
        
        // Update overlay opacity
        UIView *overlayView = objc_getAssociatedObject(self.currentPresentedVC, "overlayView");
        if (overlayView) {
            overlayView.backgroundColor = [UIColor colorWithWhite:0.0 alpha:0.4];
        }
    } completion:^(BOOL finished) {
        // Restore corner radius mask (always apply for consistency)
        UIRectCorner cornersToRound;
        
        if (isRunningOniPad()) {
            // iPad: always round all corners for aesthetic appeal
            cornersToRound = UIRectCornerAllCorners;
        } else {
            // iPhone: round corners based on original position
            if (_originalCardVerticalPosition < 0.1) {
                cornersToRound = UIRectCornerBottomLeft | UIRectCornerBottomRight;
            } else if (_originalCardVerticalPosition > 0.9) {
                cornersToRound = UIRectCornerTopLeft | UIRectCornerTopRight;
            } else {
                cornersToRound = UIRectCornerAllCorners;
            }
        }
        
        UIBezierPath *maskPath = [UIBezierPath bezierPathWithRoundedRect:cardView.bounds
                                                      byRoundingCorners:cornersToRound
                                                            cornerRadii:CGSizeMake(12.0, 12.0)];
        
        CAShapeLayer *maskLayer = [[CAShapeLayer alloc] init];
        maskLayer.frame = cardView.bounds;
        maskLayer.path = maskPath.CGPath;
        cardView.layer.mask = maskLayer;
        
    }];
}


- (UIView *)createDragTray:(CGFloat)cardWidth {
    UIView *dragTrayView = [[UIView alloc] init];
    dragTrayView.frame = CGRectMake(0, 0, cardWidth, 44);
    dragTrayView.tag = 8888;
    
    // Add black gradient fade for visual separation
    CAGradientLayer *gradientLayer = [CAGradientLayer layer];
    gradientLayer.frame = dragTrayView.bounds;
    
    if (isRunningOniPad()) {
        gradientLayer.colors = @[
            (id)[UIColor colorWithWhite:0.0 alpha:0.25].CGColor,
            (id)[UIColor colorWithWhite:0.0 alpha:0.15].CGColor,
            (id)[UIColor colorWithWhite:0.0 alpha:0.0].CGColor
        ];
    } else {
        gradientLayer.colors = @[
            (id)[UIColor colorWithWhite:0.0 alpha:0.35].CGColor,
            (id)[UIColor colorWithWhite:0.0 alpha:0.20].CGColor,
            (id)[UIColor colorWithWhite:0.0 alpha:0.0].CGColor
        ];
    }
    gradientLayer.locations = @[@0.0, @0.5, @1.0];
    [dragTrayView.layer addSublayer:gradientLayer];
    
    dragTrayView.backgroundColor = [UIColor clearColor];
    
    UIView *handleView = [[UIView alloc] init];
    handleView.backgroundColor = [UIColor colorWithWhite:1.0 alpha:0.95];
    handleView.layer.cornerRadius = 2.5;
    handleView.tag = 8889; // Tag for easy access
    // Handle is always centered - will be updated when drag tray resizes
    handleView.frame = CGRectMake(cardWidth/2 - 20, 12, 40, 5);
    handleView.autoresizingMask = UIViewAutoresizingFlexibleLeftMargin | UIViewAutoresizingFlexibleRightMargin; // Keep centered
    handleView.layer.shadowColor = [UIColor blackColor].CGColor;
    handleView.layer.shadowOffset = CGSizeMake(0, 2);
    handleView.layer.shadowOpacity = isRunningOniPad() ? 0.3 : 0.8;
    handleView.layer.shadowRadius = isRunningOniPad() ? 2.0 : 4.0;
    [dragTrayView addSubview:handleView];
    
    UIPanGestureRecognizer *dragTrayPanGesture = [[UIPanGestureRecognizer alloc] initWithTarget:self action:@selector(handleDragTrayPanGesture:)];
    dragTrayPanGesture.delegate = self;
    [dragTrayView addGestureRecognizer:dragTrayPanGesture];
    
    return dragTrayView;
}

- (UIView *)createFloatingBackButton {
    UIButton *floatingBackButton = [UIButton buttonWithType:UIButtonTypeSystem];
    
    CGFloat topSafeArea = 0;
    if (@available(iOS 11.0, *)) {
        UIView *currentView = self.currentPresentedVC.view;
        if (currentView && [currentView respondsToSelector:@selector(safeAreaInsets)]) {
            topSafeArea = currentView.safeAreaInsets.top;
        }
    }
    
    CGFloat topOffset = MAX(16, topSafeArea + 8) + 6;
    floatingBackButton.frame = CGRectMake(16, topOffset, 40, 40);
    floatingBackButton.tag = 9999;
    
    BOOL isDarkMode = NO;
    if (@available(iOS 13.0, *)) {
        isDarkMode = ([UITraitCollection currentTraitCollection].userInterfaceStyle == UIUserInterfaceStyleDark);
    }
    
    [floatingBackButton setTitle:@"←" forState:UIControlStateNormal];
    floatingBackButton.titleLabel.font = [UIFont systemFontOfSize:18 weight:UIFontWeightSemibold];
    [floatingBackButton setTitleColor:isDarkMode ? [UIColor whiteColor] : [UIColor blackColor] forState:UIControlStateNormal];
    floatingBackButton.backgroundColor = isDarkMode ? [UIColor colorWithWhite:0.1 alpha:0.5] : [UIColor colorWithWhite:0.95 alpha:0.5];
    floatingBackButton.layer.cornerRadius = 20;
    floatingBackButton.layer.shadowColor = [UIColor blackColor].CGColor;
    floatingBackButton.layer.shadowOffset = CGSizeMake(0, isRunningOniPad() ? 1 : 2);
    floatingBackButton.layer.shadowOpacity = isRunningOniPad() ? 0.1 : 0.2;
    floatingBackButton.layer.shadowRadius = isRunningOniPad() ? 2 : 4;
    floatingBackButton.layer.borderWidth = 0.5;
    floatingBackButton.layer.borderColor = isDarkMode ? [UIColor colorWithWhite:0.3 alpha:0.5].CGColor : [UIColor colorWithWhite:0.7 alpha:0.5].CGColor;
    
    [floatingBackButton addTarget:self action:@selector(backButtonTapped:) forControlEvents:UIControlEventTouchUpInside];
    
    return floatingBackButton;
}

- (void)handleDragTrayPanGesture:(UIPanGestureRecognizer *)gesture {
    if (!self.currentPresentedVC) return;
    if (self.isPurchaseProcessing) return;
    
    UIView *cardView = self.currentPresentedVC.view;
    CGFloat height = cardView.frame.size.height;
    
    // Get reference view for gestures (window or superview)
    UIView *referenceView = self.portraitWindow ? self.portraitWindow : cardView.superview;
    CGPoint translation = [gesture translationInView:referenceView];
    CGPoint velocity = [gesture velocityInView:referenceView];
    
    // Get overlay view for window approach
    UIView *overlayView = self.portraitWindow ? objc_getAssociatedObject(self.currentPresentedVC, "overlayView") : nil;
    
    switch (gesture.state) {
        case UIGestureRecognizerStateBegan: {
            self.initialY = cardView.frame.origin.y;
            // iPhone: Disable layout updates during gesture to prevent interference
            if (!isRunningOniPad() && [self.currentPresentedVC isKindOfClass:[OrientationLockedViewController class]]) {
                OrientationLockedViewController *containerVC = (OrientationLockedViewController *)self.currentPresentedVC;
                containerVC.skipLayoutDuringInitialSetup = YES;
            }
            break;
        }
            
        case UIGestureRecognizerStateChanged: {
            CGFloat currentTravel = translation.y;
            CGFloat screenHeight = self.portraitWindow ? self.portraitWindow.bounds.size.height : cardView.superview.bounds.size.height;
            
            if (currentTravel < 0 && !_isCardExpanded && !isRunningOniPad()) {
                // Dragging up to expand - linear progress that directly follows finger (disabled on iPad)
                CGFloat expandDistance = height * 0.4;
                CGFloat expandProgress = MIN(1.0, fabs(currentTravel) / expandDistance);
                
                [self updateCardExpansionProgress:expandProgress cardView:cardView];
                
            } else if (currentTravel > 0) {
                // Dragging down - handle dismiss or collapse with direct linear following
                
                if (_isCardExpanded) {
                    // When expanded, collapse follows finger linearly with no easing
                    CGFloat collapseDistance = height * 0.5;
                    CGFloat collapseProgress = MIN(1.0, currentTravel / collapseDistance);
                    
                    // Direct linear progress - no easing during drag for iOS-native feel
                    [self updateCardExpansionProgress:1.0 - collapseProgress cardView:cardView];
                    
                    // If dragged beyond collapse distance, continue with dismiss motion
                    if (collapseProgress >= 1.0) {
                        // Calculate how much we've dragged past the collapse point
                        CGFloat extraTravel = currentTravel - collapseDistance;
                        
                        // Get the card's current collapsed position
                        CGRect screenBounds = [UIScreen mainScreen].bounds;
                        CGFloat collapsedY;
                        if (isRunningOniPad()) {
                            CGSize cardSize = calculateiPadCardSize(screenBounds);
                            collapsedY = (screenBounds.size.height - cardSize.height) / 2;
                        } else {
                            CGFloat collapsedHeight = screenBounds.size.height * _originalCardHeightRatio;
                            collapsedY = screenBounds.size.height * _originalCardVerticalPosition - collapsedHeight;
                            if (collapsedY < 0) collapsedY = 0;
                        }
                        
                        // Add the extra travel with some resistance for dismiss hint
                        CGFloat newY = collapsedY + extraTravel * 0.6; // Less damping for more direct feel
                        CGFloat maxY = screenHeight;
                        newY = MIN(maxY, newY);
                        
                        // Directly update frame for smooth dismiss hint
                        CGRect currentFrame = cardView.frame;
                        // iPhone: Use CATransaction to prevent layout interference
                        if (!isRunningOniPad()) {
                            [CATransaction begin];
                            [CATransaction setDisableActions:YES];
                        }
                        cardView.frame = CGRectMake(currentFrame.origin.x, newY, currentFrame.size.width, currentFrame.size.height);
                        if (!isRunningOniPad()) {
                            [CATransaction commit];
                        }
                        
                        // Update customFrame (all presentations now use window approach)
                        if ([self.currentPresentedVC isKindOfClass:[OrientationLockedViewController class]]) {
                            OrientationLockedViewController *containerVC = (OrientationLockedViewController *)self.currentPresentedVC;
                            containerVC.customFrame = cardView.frame;
                        }
                    }
                    
                } else {
                    // Normal collapsed position dragging for dismiss - direct linear movement
                    CGFloat newY = self.initialY + currentTravel;
                    CGFloat maxY = screenHeight;
                    newY = MIN(maxY, newY);
                    // iPhone: Use CATransaction to prevent layout interference
                    if (!isRunningOniPad()) {
                        [CATransaction begin];
                        [CATransaction setDisableActions:YES];
                    }
                    cardView.frame = CGRectMake(cardView.frame.origin.x, newY, cardView.frame.size.width, height);
                    if (!isRunningOniPad()) {
                        [CATransaction commit];
                    }
                    
                    // Update customFrame (all presentations now use window approach)
                    if ([self.currentPresentedVC isKindOfClass:[OrientationLockedViewController class]]) {
                        OrientationLockedViewController *containerVC = (OrientationLockedViewController *)self.currentPresentedVC;
                        containerVC.customFrame = cardView.frame;
                    }
                }
                
                // Smooth background opacity change - lighter on iPad
                CGFloat maxTravel = _isCardExpanded ? (height * 0.8) : (height * 0.6);
                CGFloat ratio = 1.0 - (currentTravel / maxTravel);
                ratio = MAX(0.1, MIN(1.0, ratio)); // Clamp between 0.1 and 1.0
                
                CGFloat baseOpacity = _isCardExpanded ? (isRunningOniPad() ? 0.45 : 0.6) : (isRunningOniPad() ? 0.25 : 0.4);
                
                // Update overlay opacity
                if (overlayView) {
                    overlayView.backgroundColor = [UIColor colorWithWhite:0.0 alpha:baseOpacity * ratio];
                }
            }
            break;
        }
            
        case UIGestureRecognizerStateEnded:
        case UIGestureRecognizerStateCancelled: {
            CGFloat currentTravel = translation.y;
            
            // iOS-native thresholds - more generous and intuitive
            CGFloat expandThreshold = height * 0.15; // 15% upward drag to expand
            CGFloat collapseThreshold = height * 0.25; // 25% downward drag to collapse  
            CGFloat dismissThreshold = height * 0.3; // 30% downward drag to dismiss
            
            // Velocity thresholds for quick gestures (similar to iOS system gestures)
            CGFloat expandVelocityThreshold = -300; // Lower threshold for upward swipes
            CGFloat collapseVelocityThreshold = 300; // Lower threshold for downward swipes
            CGFloat dismissVelocityThreshold = 500; // Moderate threshold for dismiss
            
            // Determine action based on distance and velocity
            BOOL shouldExpand = NO;
            BOOL shouldCollapse = NO;
            BOOL shouldDismiss = NO;
            
            if (currentTravel < -expandThreshold || velocity.y < expandVelocityThreshold) {
                // Dragged up sufficiently or fast upward velocity (disabled on iPad)
                if (!_isCardExpanded && !isRunningOniPad()) {
                    shouldExpand = YES;
                }
            } else if (currentTravel > 0) {
                // Downward movement - determine collapse vs dismiss
                if (_isCardExpanded) {
                    // For expanded cards, prioritize collapse over dismiss
                    if (currentTravel > collapseThreshold || velocity.y > collapseVelocityThreshold) {
                        shouldCollapse = YES;
                        
                        // Only dismiss if dragged much further or with very high velocity
                        if (currentTravel > dismissThreshold * 1.5 && velocity.y > dismissVelocityThreshold * 1.5) {
                            shouldDismiss = YES;
                            shouldCollapse = NO; // Override collapse with dismiss
                        }
                    }
                } else {
                    // For collapsed cards, dismiss if threshold met
                    if (currentTravel > dismissThreshold || velocity.y > dismissVelocityThreshold) {
                        shouldDismiss = YES;
                    }
                }
            }
            
            if (shouldExpand) {
                // iOS-native expansion animation
                [UIView animateWithDuration:0.5 
                                      delay:0 
                     usingSpringWithDamping:0.85 
                      initialSpringVelocity:fabs(velocity.y) / 1000.0 
                                    options:UIViewAnimationOptionCurveEaseOut 
                                 animations:^{
                    [self updateCardExpansionProgress:1.0 cardView:cardView];
                } completion:^(BOOL finished) {
                    _isCardExpanded = YES;
                    [self expandCardToFullScreen]; // Finalize WebView constraints
                }];
            } else if (shouldCollapse) {
                // iOS-native collapse animation with responsive timing
                CGFloat animationDuration = 0.45;
                CGFloat springDamping = 0.85;
                CGFloat springVelocity = velocity.y / 1000.0;
                
                // Faster animation for quick gestures
                if (velocity.y > 600) {
                    animationDuration = 0.35;
                    springVelocity = velocity.y / 800.0;
                }
                
                [UIView animateWithDuration:animationDuration 
                                      delay:0 
                     usingSpringWithDamping:springDamping 
                      initialSpringVelocity:springVelocity 
                                    options:UIViewAnimationOptionCurveEaseOut 
                                 animations:^{
                    [self updateCardExpansionProgress:0.0 cardView:cardView];
                } completion:^(BOOL finished) {
                    _isCardExpanded = NO;
                    [self collapseCardToOriginal]; // Finalize WebView constraints
                }];
            } else if (shouldDismiss) {
                // Disable layout updates during dismissal to prevent glitching
                if ([self.currentPresentedVC isKindOfClass:[OrientationLockedViewController class]]) {
                    OrientationLockedViewController *containerVC = (OrientationLockedViewController *)self.currentPresentedVC;
                    containerVC.skipLayoutDuringInitialSetup = YES;
                }
                
                // iOS-native dismiss animation
                CGFloat animationDuration = 0.4;
                if (velocity.y > 1000) {
                    animationDuration = 0.25; // Faster for quick swipes
                }
                
                CGFloat finalY = self.portraitWindow ? self.portraitWindow.bounds.size.height : cardView.superview.bounds.size.height;
                [UIView animateWithDuration:animationDuration 
                                      delay:0 
                     usingSpringWithDamping:0.9 
                      initialSpringVelocity:velocity.y / 1000.0 
                                    options:UIViewAnimationOptionCurveEaseOut 
                                 animations:^{
                    cardView.frame = CGRectMake(cardView.frame.origin.x, finalY, cardView.frame.size.width, cardView.frame.size.height);
                    
                    // Update customFrame (all presentations now use window approach)
                    if ([self.currentPresentedVC isKindOfClass:[OrientationLockedViewController class]]) {
                        OrientationLockedViewController *containerVC = (OrientationLockedViewController *)self.currentPresentedVC;
                        containerVC.customFrame = cardView.frame;
                    }
                    
                    // Fade out overlay
                    if (overlayView) {
                        overlayView.backgroundColor = [UIColor colorWithWhite:0.0 alpha:0.0];
                    }
                } completion:^(BOOL finished) {
                    // Re-enable layout updates after dismissal completes
                    if ([self.currentPresentedVC isKindOfClass:[OrientationLockedViewController class]]) {
                        OrientationLockedViewController *containerVC = (OrientationLockedViewController *)self.currentPresentedVC;
                        containerVC.skipLayoutDuringInitialSetup = NO;
                    }
                    
                    [self.currentPresentedVC dismissViewControllerAnimated:NO completion:^{
                        [self cleanupCardInstance];
                        [self callUnityCallbackOnce];
                    }];
                }];
            } else {
                // Return to current state with iOS-native spring animation
                CGFloat targetProgress = _isCardExpanded ? 1.0 : 0.0;
                [UIView animateWithDuration:0.4 
                                      delay:0 
                     usingSpringWithDamping:0.8 
                      initialSpringVelocity:fabs(velocity.y) / 1000.0 
                                    options:UIViewAnimationOptionCurveEaseOut 
                                 animations:^{
                    [self updateCardExpansionProgress:targetProgress cardView:cardView];
                } completion:^(BOOL finished) {
                    // iPhone: Re-enable layout after gesture completes
                    if (!isRunningOniPad() && [self.currentPresentedVC isKindOfClass:[OrientationLockedViewController class]]) {
                        OrientationLockedViewController *containerVC = (OrientationLockedViewController *)self.currentPresentedVC;
                        containerVC.skipLayoutDuringInitialSetup = NO;
                    }
                }];
            }
            break;
        }
            
        default:
            break;
    }
}

- (void)startKeyboardObserving {
    if (!self.isObservingKeyboard) {
        [[NSNotificationCenter defaultCenter] addObserver:self selector:@selector(keyboardWillShow:) name:UIKeyboardWillShowNotification object:nil];
        [[NSNotificationCenter defaultCenter] addObserver:self selector:@selector(keyboardWillHide:) name:UIKeyboardWillHideNotification object:nil];
        self.isObservingKeyboard = YES;
    }
}

- (void)stopKeyboardObserving {
    if (self.isObservingKeyboard) {
        [[NSNotificationCenter defaultCenter] removeObserver:self name:UIKeyboardWillShowNotification object:nil];
        [[NSNotificationCenter defaultCenter] removeObserver:self name:UIKeyboardWillHideNotification object:nil];
        self.isObservingKeyboard = NO;
    }
}

- (void)keyboardWillShow:(NSNotification *)notification {
    // iPad: don't adjust card when keyboard appears
    if (isRunningOniPad()) {
        return;
    }
    
    // iPhone: expand card when keyboard appears
    if (!_isCardExpanded && self.currentPresentedVC) {
        dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.1 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
            [self expandCardToFullScreen];
        });
    }
}

- (void)keyboardWillHide:(NSNotification *)notification {
    // iPad: don't adjust card when keyboard hides
    if (isRunningOniPad()) {
        return;
    }
    
    // iPhone: collapse card when keyboard hides
    if (_isCardExpanded && self.currentPresentedVC) {
        WKWebView *webView = nil;
        for (UIView *subview in self.currentPresentedVC.view.subviews) {
            if ([subview isKindOfClass:NSClassFromString(@"WKWebView")]) {
                webView = (WKWebView *)subview;
                break;
            }
        }
        
        if (webView) {
            [webView evaluateJavaScript:@"document.activeElement.tagName === 'SELECT'" completionHandler:^(id result, NSError *error) {
                BOOL hasActiveSelect = [result boolValue];
                
                if (!hasActiveSelect) {
                    dispatch_async(dispatch_get_main_queue(), ^{
                        [self collapseCardToOriginal];
                    });
                }
            }];
        } else {
            [self collapseCardToOriginal];
        }
    }
}

// WKScriptMessageHandler implementation for handling JavaScript calls
- (void)userContentController:(WKUserContentController *)userContentController didReceiveScriptMessage:(WKScriptMessage *)message {
    if ([message.name isEqualToString:@"stashPaymentSuccess"]) {
        self.isPurchaseProcessing = NO;
        
        if (_paymentSuccessHandled) {
            return;
        }
        _paymentSuccessHandled = YES;
        
        if (_paymentSuccessCallbackCalled) {
            return;
        }
        _paymentSuccessCallbackCalled = YES;
        
        if (_paymentSuccessCallback != NULL) {
            dispatch_async(dispatch_get_main_queue(), ^{
                _paymentSuccessCallback();
            });
        }
        
        // Automatically close the Stash Pay Card without calling the regular dismissal callback
        if (self.currentPresentedVC) {
            [self dismissWithAnimation:^{
                [self cleanupCardInstance];
                // Don't call callUnityCallbackOnce here since we already handled the success callback
            }];
        }
    }
    else if ([message.name isEqualToString:@"stashPaymentFailure"]) {
        self.isPurchaseProcessing = NO;
        
        if (_paymentFailureCallback != NULL) {
            dispatch_async(dispatch_get_main_queue(), ^{
                _paymentFailureCallback();
            });
        }
        
        // Automatically close the Stash Pay Card without calling the regular dismissal callback
        if (self.currentPresentedVC) {
            [self dismissWithAnimation:^{
                [self cleanupCardInstance];
                // Don't call callUnityCallbackOnce here since we already handled the failure callback
            }];
        }
    }
    else if ([message.name isEqualToString:@"stashPurchaseProcessing"]) {
        self.isPurchaseProcessing = YES;
    }
    else if ([message.name isEqualToString:@"stashOptin"]) {
        NSString *optinType = [message.body isKindOfClass:[NSString class]] ? (NSString *)message.body : @"";
        
        if (_optinResponseCallback != NULL) {
            // Copy the string to ensure it remains valid in the async block
            NSString *optinTypeCopy = [optinType copy];
            dispatch_async(dispatch_get_main_queue(), ^{
                const char *optinTypeCStr = [optinTypeCopy UTF8String];
                _optinResponseCallback(optinTypeCStr);
            });
        }
        
        // Automatically close the popup after opt-in selection
        if (self.currentPresentedVC) {
            [self dismissWithAnimation:^{
                [self cleanupCardInstance];
            }];
        }
    }
}

- (void)showFloatingBackButton:(WKWebView *)webView {
    if (self.isNavigationBarVisible || !self.currentPresentedVC) return;
    
    UIView *containerView = self.currentPresentedVC.view;
    
    // Create floating back button if it doesn't exist
    if (!self.navigationBarView) {
        self.navigationBarView = [self createFloatingBackButton];
    }
    
    // Position floating button in top-left corner
    self.navigationBarView.alpha = 0.0;
    self.navigationBarView.transform = CGAffineTransformMakeScale(0.8, 0.8); // Start slightly smaller
    
    // Add to container on top of everything
    [containerView addSubview:self.navigationBarView];
    [containerView bringSubviewToFront:self.navigationBarView];
    
    [UIView animateWithDuration:0.3 delay:0 usingSpringWithDamping:0.7 initialSpringVelocity:0.5 options:UIViewAnimationOptionCurveEaseOut animations:^{
        self.navigationBarView.alpha = 1.0;
        self.navigationBarView.transform = CGAffineTransformIdentity; // Scale to normal size
    } completion:^(BOOL finished) {
        self.isNavigationBarVisible = YES;
    }];
}

- (void)hideFloatingBackButton:(WKWebView *)webView {
    if (!self.isNavigationBarVisible || !self.navigationBarView) return;
    
    // Animate the floating button out of view with scale and fade
    [UIView animateWithDuration:0.25 delay:0 usingSpringWithDamping:0.8 initialSpringVelocity:0.3 options:UIViewAnimationOptionCurveEaseIn animations:^{
        self.navigationBarView.alpha = 0.0;
        self.navigationBarView.transform = CGAffineTransformMakeScale(0.6, 0.6); // Scale down while fading
    } completion:^(BOOL finished) {
        [self.navigationBarView removeFromSuperview];
        self.navigationBarView = nil;
        self.isNavigationBarVisible = NO;
    }];
}



- (void)backButtonTapped:(UIButton *)button {
    if (!self.currentPresentedVC || !self.initialURL) return;
    
    for (UIView *subview in self.currentPresentedVC.view.subviews) {
        if ([subview isKindOfClass:NSClassFromString(@"WKWebView")]) {
            WKWebView *webView = (WKWebView *)subview;
            NSURLRequest *request = [NSURLRequest requestWithURL:self.initialURL];
            [webView loadRequest:request];
            break;
        }
    }
}

@end

// Helper function to create loading view with a simple spinner
UIView* CreateLoadingView(CGRect frame) {
    // Create container view
    UIView* loadingView = [[UIView alloc] initWithFrame:frame];
    
    // Check if we're in dark mode
    BOOL isDarkMode = NO;
    if (@available(iOS 13.0, *)) {
        UIUserInterfaceStyle currentStyle = [UITraitCollection currentTraitCollection].userInterfaceStyle;
        isDarkMode = (currentStyle == UIUserInterfaceStyleDark);
    }
    
    // Set SOLID background color that matches webview - CRITICAL for preventing black flash
    UIColor *backgroundColor = isDarkMode ? [UIColor blackColor] : [UIColor whiteColor];
    loadingView.backgroundColor = backgroundColor;
    loadingView.opaque = YES; // Ensure completely opaque
    
    // Create a native iOS activity indicator (spinner)
    UIActivityIndicatorView *spinner;
    if (@available(iOS 13.0, *)) {
        spinner = [[UIActivityIndicatorView alloc] initWithActivityIndicatorStyle:UIActivityIndicatorViewStyleLarge];
        spinner.color = isDarkMode ? [UIColor whiteColor] : [UIColor darkGrayColor];
    } else {
        spinner = [[UIActivityIndicatorView alloc] initWithActivityIndicatorStyle:UIActivityIndicatorViewStyleWhiteLarge];
        if (!isDarkMode) {
            spinner.color = [UIColor darkGrayColor];
        }
    }
    
    spinner.translatesAutoresizingMaskIntoConstraints = NO;
    spinner.hidesWhenStopped = NO; // Keep visible even when stopped for smoother transition
    [spinner startAnimating]; // Start the spinner animation
    
    // Add spinner to loading view
    [loadingView addSubview:spinner];
    
    // Center the spinner in the loading view
    [NSLayoutConstraint activateConstraints:@[
        [spinner.centerXAnchor constraintEqualToAnchor:loadingView.centerXAnchor],
        [spinner.centerYAnchor constraintEqualToAnchor:loadingView.centerYAnchor]
    ]];
    
    return loadingView;
}

// Unity bridge to open URLs in Safari View Controller
extern "C" {
    // Sets the callback function to be called when Safari View is dismissed
    void _StashPayCardSetSafariViewDismissedCallback(SafariViewDismissedCallback callback) {
        _safariViewDismissedCallback = callback;
        _callbackWasCalled = NO; // Reset flag when setting a new callback
        _paymentSuccessHandled = NO; // Reset payment success flag when setting new callback
        _paymentSuccessCallbackCalled = NO; // Reset payment success callback flag when setting new callback
    }

    // Sets the callback function to be called when payment succeeds
    void _StashPayCardSetPaymentSuccessCallback(PaymentSuccessCallback callback) {
        _paymentSuccessCallback = callback;
    }

    // Sets the callback function to be called when payment fails
    void _StashPayCardSetPaymentFailureCallback(PaymentFailureCallback callback) {
        _paymentFailureCallback = callback;
    }

    // Sets the callback function to be called when user opts in
    void _StashPayCardSetOptinResponseCallback(OptinResponseCallback callback) {
        _optinResponseCallback = callback;
    }
    
    void _StashPayCardSetPageLoadedCallback(PageLoadedCallback callback) {
        _pageLoadedCallback = callback;
    }

    void _StashPayCardSetCardConfiguration(float heightRatio, float verticalPosition) {
        _cardHeightRatio = heightRatio < 0.1 ? 0.1 : (heightRatio > 0.9 ? 0.9 : heightRatio);
        _cardVerticalPosition = verticalPosition < 0.0 ? 0.0 : (verticalPosition > 1.0 ? 1.0 : verticalPosition);
        _originalCardHeightRatio = _cardHeightRatio;
        _originalCardVerticalPosition = _cardVerticalPosition;
        _originalCardWidthRatio = _cardWidthRatio;
        _isCardExpanded = NO;
    }

    void _StashPayCardSetCardConfigurationWithWidth(float heightRatio, float verticalPosition, float widthRatio) {
        _cardHeightRatio = heightRatio < 0.1 ? 0.1 : (heightRatio > 1.0 ? 1.0 : heightRatio);
        _cardVerticalPosition = verticalPosition < 0.0 ? 0.0 : (verticalPosition > 1.0 ? 1.0 : verticalPosition);
        _cardWidthRatio = widthRatio < 0.1 ? 0.1 : (widthRatio > 1.0 ? 1.0 : widthRatio);
        _originalCardHeightRatio = _cardHeightRatio;
        _originalCardVerticalPosition = _cardVerticalPosition;
        _originalCardWidthRatio = _cardWidthRatio;
        _isCardExpanded = NO;
    }

    // Opens a checkout URL in Safari View Controller with delegation
    void _StashPayCardOpenCheckoutInSafariVC(const char* urlString) {
        if (urlString == NULL) {
            NSLog(@"Error: URL is null");
            return;
        }
        
        NSString* nsUrlStr = [NSString stringWithUTF8String:urlString];
        NSURL* url = [NSURL URLWithString:nsUrlStr];
        
        if (url == nil) {
            NSLog(@"Error: Invalid URL format");
            return;
        }
        
        // Get the top view controller to present from
        UIViewController* rootController = [UIApplication sharedApplication].keyWindow.rootViewController;
        UIViewController* topController = rootController;
        
        while (topController.presentedViewController) {
            topController = topController.presentedViewController;
        }
        
        if (@available(iOS 9.0, *)) {
            // Handle native Safari controller first (completely separate path)
            if (_forceSafariViewController) {
                // Use completely native SFSafariViewController with absolutely NO modifications
                SFSafariViewController* safariViewController = [[SFSafariViewController alloc] initWithURL:url];
                
                // Set delegate ONLY for basic dismissal callback - no other modifications
                safariViewController.delegate = [StashPayCardSafariDelegate sharedInstance];
                
                // Present with completely default system behavior - no custom presentation styles
                [topController presentViewController:safariViewController animated:YES completion:nil];
                
                // Set a simple callback for when the native Safari is dismissed
                [StashPayCardSafariDelegate sharedInstance].safariViewDismissedCallback = ^{
                    if (_safariViewDismissedCallback != NULL) {
                        _safariViewDismissedCallback();
                    }
                };
                
                return;
            }
            
            // Check if a card is already being presented
            if (_isCardCurrentlyPresented) {
                return;
            }
            
            // Reset all callback flags for new payment session to ensure callbacks work properly
            _paymentSuccessHandled = NO;
            _paymentSuccessCallbackCalled = NO;
            _callbackWasCalled = NO;
            
            // Set the presentation flag
            _isCardCurrentlyPresented = YES;
            
            // Reset expansion state for new card
            _isCardExpanded = NO;
            
            // Reset navigation bar state for new card
            [[StashPayCardSafariDelegate sharedInstance] setIsNavigationBarVisible:NO];
            [[StashPayCardSafariDelegate sharedInstance] setNavigationBarView:nil];
            
            // Store the initial URL for back button functionality
            [[StashPayCardSafariDelegate sharedInstance] setInitialURL:url];
            
            // Create a custom view controller with WKWebView
            OrientationLockedViewController *containerVC = [[OrientationLockedViewController alloc] init];
            containerVC.modalPresentationStyle = UIModalPresentationOverFullScreen;
            
            // Enforce portrait for OpenCheckout on iPhone (not popup)
            containerVC.enforcePortrait = !_usePopupPresentation;
            
            // Set container background immediately to prevent black flash
            if (@available(iOS 13.0, *)) {
                UIUserInterfaceStyle currentStyle = [UITraitCollection currentTraitCollection].userInterfaceStyle;
                containerVC.view.backgroundColor = (currentStyle == UIUserInterfaceStyleDark) ? [UIColor blackColor] : [UIColor systemBackgroundColor];
            } else {
                containerVC.view.backgroundColor = [UIColor whiteColor];
            }
            
            // Try to create the web view
            Class webViewClass = NSClassFromString(@"WKWebView");
            Class configClass = NSClassFromString(@"WKWebViewConfiguration");
            
            if (webViewClass && configClass) {
                // WebKit is available, use WKWebView with optimized configuration
                WKWebViewConfiguration *config = [[WKWebViewConfiguration alloc] init];
                config.allowsInlineMediaPlayback = YES;
                config.allowsAirPlayForMediaPlayback = YES;
                config.allowsPictureInPictureMediaPlayback = YES;
                
                // Enable performance optimizations
                if (@available(iOS 14.0, *)) {
                    config.limitsNavigationsToAppBoundDomains = NO; // Allow all domains for payment flows
                }
                
                // Use optimized process pool for faster loading
                config.processPool = [WKProcessPool new];
                
                // Enable autofill and form features for payment data
                if (@available(iOS 11.0, *)) {
                    config.dataDetectorTypes = WKDataDetectorTypeAll;
                }
                
                // Create preferences with enhanced settings
                WKPreferences *preferences = [[WKPreferences alloc] init];
                preferences.javaScriptEnabled = YES;
                preferences.javaScriptCanOpenWindowsAutomatically = YES;
                
                // Enable additional WebKit features for autofill (iOS 14+)
                if (@available(iOS 14.0, *)) {
                    preferences.fraudulentWebsiteWarningEnabled = YES;
                }
                
                config.preferences = preferences;
                
                // Configure web view for better form handling
                if (@available(iOS 14.0, *)) {
                    config.defaultWebpagePreferences.allowsContentJavaScript = YES;
                }
                if (@available(iOS 13.0, *)) {
                    config.defaultWebpagePreferences.preferredContentMode = WKContentModeRecommended;
                }
                
                // Add JavaScript handler for window.close() interception
                WKUserContentController *userContentController = [[WKUserContentController alloc] init];
                
                // Add viewport meta tag and transparent background styles
                NSString *viewportScript = @"var meta = document.createElement('meta'); \
                    meta.name = 'viewport'; \
                    meta.content = 'width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no, viewport-fit=cover'; \
                    document.head.appendChild(meta); \
                    document.documentElement.style.backgroundColor = 'transparent'; \
                    document.body.style.backgroundColor = 'transparent';";
                
                WKUserScript *viewportInjection = [[WKUserScript alloc] initWithSource:viewportScript
                                                                       injectionTime:WKUserScriptInjectionTimeAtDocumentEnd
                                                                    forMainFrameOnly:YES];
                [userContentController addUserScript:viewportInjection];
                
                // Add transparent background styles
                NSString *transparencyStyles = @"body, html { background-color: transparent !important; } \
                    :root { background-color: transparent !important; }";
                WKUserScript *styleInjection = [[WKUserScript alloc] initWithSource:[NSString stringWithFormat:@"var style = document.createElement('style'); \
                    style.innerHTML = '%@'; \
                    document.head.appendChild(style);", transparencyStyles]
                                                                    injectionTime:WKUserScriptInjectionTimeAtDocumentStart
                                                                 forMainFrameOnly:YES];
                [userContentController addUserScript:styleInjection];
                

                
                // JavaScript code to override window.open() to prevent new windows
                NSString *windowOpenOverrideScript = @"(function() {"
                    "var originalOpen = window.open;"
                    "window.open = function(url, name, features) {"
                        "// Instead of opening a new window, navigate in the current window"
                        "if (url) {"
                            "window.location.href = url;"
                        "}"
                        "return window;"
                    "};"
                "})();";
                
                WKUserScript *windowOpenInjection = [[WKUserScript alloc] initWithSource:windowOpenOverrideScript 
                                                                       injectionTime:WKUserScriptInjectionTimeAtDocumentStart 
                                                                    forMainFrameOnly:YES];
                [userContentController addUserScript:windowOpenInjection];
                
                // JavaScript code to handle target="_blank" links and force them to open in same window
                NSString *targetBlankOverrideScript = @"(function() {"
                    "function removeTargetBlank() {"
                        "var links = document.querySelectorAll('a[target=\"_blank\"]');"
                        "for (var i = 0; i < links.length; i++) {"
                            "links[i].removeAttribute('target');"
                        "}"
                    "}"
                    ""
                    "// Remove target=\"_blank\" from existing links"
                    "if (document.readyState === 'loading') {"
                        "document.addEventListener('DOMContentLoaded', removeTargetBlank);"
                    "} else {"
                        "removeTargetBlank();"
                    "}"
                    ""
                    "// Monitor for dynamically added links"
                    "var observer = new MutationObserver(function(mutations) {"
                        "mutations.forEach(function(mutation) {"
                            "if (mutation.type === 'childList') {"
                                "mutation.addedNodes.forEach(function(node) {"
                                    "if (node.nodeType === 1) {"
                                        "if (node.tagName === 'A' && node.getAttribute('target') === '_blank') {"
                                            "node.removeAttribute('target');"
                                        "}"
                                        "var blankLinks = node.querySelectorAll && node.querySelectorAll('a[target=\"_blank\"]');"
                                        "if (blankLinks) {"
                                            "for (var i = 0; i < blankLinks.length; i++) {"
                                                "blankLinks[i].removeAttribute('target');"
                                            "}"
                                        "}"
                                    "}"
                                "});"
                            "}"
                        "});"
                    "});"
                    ""
                    "observer.observe(document.body || document.documentElement, {"
                        "childList: true,"
                        "subtree: true"
                    "});"
                "})();";
                
                WKUserScript *targetBlankInjection = [[WKUserScript alloc] initWithSource:targetBlankOverrideScript 
                                                                        injectionTime:WKUserScriptInjectionTimeAtDocumentEnd 
                                                                     forMainFrameOnly:YES];
                [userContentController addUserScript:targetBlankInjection];
                
                // JavaScript code to set up Stash SDK functions
                NSString *stashSDKScript = @"(function() {"
                    "window.stash_sdk = window.stash_sdk || {};"
                    "window.stash_sdk.onPaymentSuccess = function(data) {"
                        "window.webkit.messageHandlers.stashPaymentSuccess.postMessage(data || {});"
                    "};"
                    "window.stash_sdk.onPaymentFailure = function(data) {"
                        "window.webkit.messageHandlers.stashPaymentFailure.postMessage(data || {});"
                    "};"
                    "window.stash_sdk.onPurchaseProcessing = function(data) {"
                        "window.webkit.messageHandlers.stashPurchaseProcessing.postMessage(data || {});"
                    "};"
                    "window.stash_sdk.setPaymentChannel = function(optinType) {"
                        "window.webkit.messageHandlers.stashOptin.postMessage(optinType || '');"
                    "};"
                "})();";
                
                WKUserScript *stashSDKInjection = [[WKUserScript alloc] initWithSource:stashSDKScript 
                                                                     injectionTime:WKUserScriptInjectionTimeAtDocumentStart 
                                                                  forMainFrameOnly:YES];
                [userContentController addUserScript:stashSDKInjection];
                
                // Add the script message handlers
                [userContentController addScriptMessageHandler:[StashPayCardSafariDelegate sharedInstance] name:@"stashPaymentSuccess"];
                [userContentController addScriptMessageHandler:[StashPayCardSafariDelegate sharedInstance] name:@"stashPaymentFailure"];
                [userContentController addScriptMessageHandler:[StashPayCardSafariDelegate sharedInstance] name:@"stashPurchaseProcessing"];
                [userContentController addScriptMessageHandler:[StashPayCardSafariDelegate sharedInstance] name:@"stashOptin"];
                
                config.userContentController = userContentController;
                
                // Setup web view with proper background color from the start
                WKWebView *webView = [[WKWebView alloc] initWithFrame:CGRectZero configuration:config];
                
                // Set background to match system background immediately to prevent black flash
                UIColor *systemBackgroundColor;
                if (@available(iOS 13.0, *)) {
                    UIUserInterfaceStyle currentStyle = [UITraitCollection currentTraitCollection].userInterfaceStyle;
                    systemBackgroundColor = (currentStyle == UIUserInterfaceStyleDark) ? [UIColor blackColor] : [UIColor systemBackgroundColor];
                } else {
                    systemBackgroundColor = [UIColor whiteColor];
                }
                
                webView.opaque = YES;
                webView.backgroundColor = systemBackgroundColor;
                webView.scrollView.backgroundColor = systemBackgroundColor;
                webView.scrollView.opaque = YES;
                webView.hidden = NO; // Keep visible but at 0 alpha for immediate cross-fade
                webView.alpha = 0.0; // Start at 0 opacity for seamless cross-fade
                webView.translatesAutoresizingMaskIntoConstraints = NO;
                
                // Force all subviews to have solid background
                for (UIView *subview in webView.subviews) {
                    subview.backgroundColor = systemBackgroundColor;
                    subview.opaque = YES;
                }
                for (UIView *subview in webView.scrollView.subviews) {
                    subview.backgroundColor = systemBackgroundColor;
                    subview.opaque = YES;
                }
                
                // Disable scroll bounce and content insets
                webView.scrollView.bounces = NO;
                webView.scrollView.alwaysBounceVertical = NO;
                webView.scrollView.contentInsetAdjustmentBehavior = UIScrollViewContentInsetAdjustmentNever;
                
                // Remove any automatic margins or safe area insets
                if (@available(iOS 11.0, *)) {
                    webView.scrollView.contentInsetAdjustmentBehavior = UIScrollViewContentInsetAdjustmentNever;
                    webView.scrollView.contentInset = UIEdgeInsetsZero;
                    webView.scrollView.scrollIndicatorInsets = UIEdgeInsetsZero;
                }
                
                // Enable scrolling for popup mode
                webView.scrollView.scrollEnabled = YES;
                webView.scrollView.showsVerticalScrollIndicator = YES;
                webView.scrollView.showsHorizontalScrollIndicator = NO;
                
                // Use the proper public API for iOS 9.0+
                if ([webView respondsToSelector:@selector(setOpaque:)]) {
                    [webView setOpaque:NO];
                }
                
                // Additional transparency settings for the scrollView
                webView.scrollView.opaque = NO;
                for (UIView *subview in webView.scrollView.subviews) {
                    subview.backgroundColor = [UIColor clearColor];
                }
                
                // Add viewport meta tag to prevent any margins
                NSString *noMarginsScript = @"var style = document.createElement('style'); \
                    style.innerHTML = 'body { margin: 0 !important; padding: 0 !important; min-height: 100% !important; } \
                    html { margin: 0 !important; padding: 0 !important; height: 100% !important; }'; \
                    document.head.appendChild(style);";
                
                WKUserScript *noMarginsInjection = [[WKUserScript alloc] initWithSource:noMarginsScript
                                                                         injectionTime:WKUserScriptInjectionTimeAtDocumentStart
                                                                      forMainFrameOnly:YES];
                [config.userContentController addUserScript:noMarginsInjection];
                
                UIView* loadingView = CreateLoadingView(CGRectZero);
                loadingView.backgroundColor = systemBackgroundColor;
                loadingView.translatesAutoresizingMaskIntoConstraints = NO;
                loadingView.opaque = YES;
                // For popup mode, start loading view at alpha 0 and fade in with container
                // For card mode, start at alpha 1.0 (visible immediately)
                loadingView.alpha = _usePopupPresentation ? 0.0 : 1.0;
                
                [containerVC.view addSubview:webView];
                [containerVC.view addSubview:loadingView];
                
                [NSLayoutConstraint activateConstraints:@[
                    [loadingView.leadingAnchor constraintEqualToAnchor:containerVC.view.leadingAnchor],
                    [loadingView.trailingAnchor constraintEqualToAnchor:containerVC.view.trailingAnchor],
                    [loadingView.topAnchor constraintEqualToAnchor:containerVC.view.topAnchor],
                    [loadingView.bottomAnchor constraintEqualToAnchor:containerVC.view.bottomAnchor]
                ]];
                
                [NSLayoutConstraint activateConstraints:@[
                    [webView.leadingAnchor constraintEqualToAnchor:containerVC.view.leadingAnchor],
                    [webView.trailingAnchor constraintEqualToAnchor:containerVC.view.trailingAnchor],
                    [webView.topAnchor constraintEqualToAnchor:containerVC.view.topAnchor],
                    [webView.bottomAnchor constraintEqualToAnchor:containerVC.view.bottomAnchor]
                ]];
                
                WebViewLoadDelegate *delegate = [[WebViewLoadDelegate alloc] initWithWebView:webView loadingView:loadingView];
                webView.navigationDelegate = delegate;
                
                WebViewUIDelegate *uiDelegate = [[WebViewUIDelegate alloc] init];
                webView.UIDelegate = uiDelegate;
                
                objc_setAssociatedObject(containerVC, "webViewDelegate", delegate, OBJC_ASSOCIATION_RETAIN_NONATOMIC);
                objc_setAssociatedObject(containerVC, "webViewUIDelegate", uiDelegate, OBJC_ASSOCIATION_RETAIN_NONATOMIC);
                // Store loading view reference for popup animation
                if (_usePopupPresentation) {
                    objc_setAssociatedObject(containerVC, "loadingView", loadingView, OBJC_ASSOCIATION_RETAIN_NONATOMIC);
                }
                
                NSMutableURLRequest *request = [NSMutableURLRequest requestWithURL:url 
                                                                    cachePolicy:NSURLRequestReturnCacheDataElseLoad
                                                                timeoutInterval:15.0];
                [request setValue:@"Mozilla/5.0 (iPhone; CPU iPhone OS 15_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/15.0 Mobile/15E148 Safari/604.1" forHTTPHeaderField:@"User-Agent"];
                [request setValue:@"gzip, deflate, br" forHTTPHeaderField:@"Accept-Encoding"];
                
                // Record page load start time in the delegate
                delegate.pageLoadStartTime = CFAbsoluteTimeGetCurrent();
                
                [webView loadRequest:request];
                
                CGRect screenBounds = [UIScreen mainScreen].bounds;
                
                // For portrait-locked OpenCheckout on iPhone, ensure we use portrait dimensions
                // even if device is currently in landscape
                if (!_usePopupPresentation && !isRunningOniPad()) {
                    // Ensure portrait orientation for sizing (narrower dimension = width)
                    if (screenBounds.size.width > screenBounds.size.height) {
                        // Currently landscape, swap to get portrait dimensions
                        CGFloat temp = screenBounds.size.width;
                        screenBounds.size.width = screenBounds.size.height;
                        screenBounds.size.height = temp;
                    }
                }
                
                CGFloat width, height, x, finalY;
                
                if (_usePopupPresentation) {
                    // Popup mode: use ratios and center vertically
                    width = screenBounds.size.width * _cardWidthRatio;
                    height = screenBounds.size.height * _cardHeightRatio;
                    x = (screenBounds.size.width - width) / 2;
                    finalY = (screenBounds.size.height - height) / 2;
                } else {
                    // Card mode: split implementation for iPhone and iPad
                    if (isRunningOniPad()) {
                        // iPad: phone-like aspect ratio, centered vertically, supports rotation
                        CGFloat phoneLikeWidth = fmin(400.0, screenBounds.size.width * 0.9);
                        width = phoneLikeWidth;
                        height = screenBounds.size.height * _cardHeightRatio;
                        x = (screenBounds.size.width - width) / 2;
                        finalY = (screenBounds.size.height - height) / 2;
                    } else {
                        // iPhone: forced portrait, slides up from bottom
                        // Ensure portrait orientation for sizing (narrower dimension = width)
                        if (screenBounds.size.width > screenBounds.size.height) {
                            CGFloat temp = screenBounds.size.width;
                            screenBounds.size.width = screenBounds.size.height;
                            screenBounds.size.height = temp;
                        }
                        width = screenBounds.size.width * _cardWidthRatio;
                        height = screenBounds.size.height * _cardHeightRatio;
                        x = (screenBounds.size.width - width) / 2;
                        finalY = screenBounds.size.height * _cardVerticalPosition - height;
                        if (finalY < 0) finalY = 0;
                    }
                }
                
                // Use window-based presentation for all cases (iPhone, iPad, popup)
                [[StashPayCardSafariDelegate sharedInstance] setPreviousKeyWindow:[UIApplication sharedApplication].keyWindow];
                
                // iPhone card mode: Start frame BELOW screen, then animate to finalY
                // iPad and popup: Start at final position
                CGFloat initialY;
                if (!_usePopupPresentation && !isRunningOniPad()) {
                    // iPhone: Start frame below screen (off-screen)
                    // Use a larger offset to ensure smooth entry (like Apple Pay)
                    CGFloat screenHeight = screenBounds.size.height;
                    initialY = screenHeight + height; // Start with entire card below screen
                } else {
                    // iPad and popup: Start at final position
                    initialY = finalY;
                }
                
                // Set frame BEFORE creating window
                containerVC.customFrame = CGRectMake(x, initialY, width, height);
                
                // Configure view BEFORE it's added to window hierarchy
                containerVC.view.autoresizingMask = UIViewAutoresizingNone;
                containerVC.view.frame = containerVC.customFrame;
                containerVC.view.alpha = 0.0;
                containerVC.view.transform = CGAffineTransformIdentity; // No transform initially
                
                UIWindow *cardWindow = [[UIWindow alloc] initWithFrame:screenBounds];
                cardWindow.windowLevel = UIWindowLevelAlert;
                cardWindow.backgroundColor = [UIColor clearColor];
                cardWindow.hidden = YES;
                [[StashPayCardSafariDelegate sharedInstance] setPortraitWindow:cardWindow];
                [[StashPayCardSafariDelegate sharedInstance] setCurrentPresentedVC:containerVC];
                
                // Disable layout updates during initial setup
                containerVC.skipLayoutDuringInitialSetup = YES;
                
                // Set rootViewController
                cardWindow.rootViewController = containerVC;
                
                [CATransaction begin];
                [CATransaction setDisableActions:YES];
                [UIView setAnimationsEnabled:NO];
                
                // CRITICAL: For iPhone card mode, ensure frame is BELOW screen
                // For iPad and popup, ensure frame is at final position
                if (!_usePopupPresentation && !isRunningOniPad()) {
                    // iPhone: Frame must be below screen (entire card off-screen)
                    CGFloat screenHeight = screenBounds.size.height;
                    CGFloat belowScreenY = screenHeight + height; // Entire card below screen
                    containerVC.view.frame = CGRectMake(x, belowScreenY, width, height);
                    containerVC.customFrame = CGRectMake(x, belowScreenY, width, height);
                } else {
                    // iPad and popup: Frame at final position
                    containerVC.view.frame = containerVC.customFrame;
                }
                containerVC.view.autoresizingMask = UIViewAutoresizingNone;
                // iPhone card: Start slightly visible to avoid harsh pop-in
                // iPad/popup: Start invisible for fade-in effect
                if (!_usePopupPresentation && !isRunningOniPad()) {
                    containerVC.view.alpha = 0.95; // Almost opaque to avoid pop-in
                } else {
                    containerVC.view.alpha = 0.0;
                }
                containerVC.view.transform = CGAffineTransformIdentity; // No transform - frame position handles it
                
                [CATransaction commit];
                [UIView setAnimationsEnabled:YES];
                
                // iPad and popup: Force layout to ensure frame is applied
                if (isRunningOniPad() || _usePopupPresentation) {
                    [containerVC.view setNeedsLayout];
                    [containerVC.view layoutIfNeeded];
                    
                    // Verify and fix frame for iPad and popup
                    if (!CGRectEqualToRect(containerVC.view.frame, containerVC.customFrame)) {
                        [CATransaction begin];
                        [CATransaction setDisableActions:YES];
                        containerVC.view.frame = containerVC.customFrame;
                        containerVC.view.bounds = CGRectMake(0, 0, containerVC.customFrame.size.width, containerVC.customFrame.size.height);
                        [CATransaction commit];
                    }
                    
                    // Ensure bounds match frame (critical for mask layer, especially on iPad)
                    if (!CGSizeEqualToSize(containerVC.view.bounds.size, containerVC.customFrame.size)) {
                        [CATransaction begin];
                        [CATransaction setDisableActions:YES];
                        containerVC.view.bounds = CGRectMake(0, 0, containerVC.customFrame.size.width, containerVC.customFrame.size.height);
                        [CATransaction commit];
                    }
                }
                // iPhone card mode: NO layout calls - frame is already set below screen
                
                // CRITICAL: Keep skipLayout enabled during animation for ALL modes
                // This prevents viewWillLayoutSubviews from interfering with the animation
                
                // iPhone card mode: Verify frame is below screen before showing window
                if (!_usePopupPresentation && !isRunningOniPad()) {
                    CGFloat screenHeight = screenBounds.size.height;
                    CGFloat currentY = containerVC.view.frame.origin.y;
                    if (currentY < screenHeight) {
                        // Frame is not below screen, fix it
                        CGFloat belowScreenY = screenHeight + height; // Entire card below screen
                        [CATransaction begin];
                        [CATransaction setDisableActions:YES];
                        containerVC.view.frame = CGRectMake(x, belowScreenY, width, height);
                        containerVC.customFrame = CGRectMake(x, belowScreenY, width, height);
                        [CATransaction commit];
                    }
                }
                
                cardWindow.hidden = NO;
                [cardWindow makeKeyAndVisible];
                
                // Final frame check for iPad and popup mode only
                if (isRunningOniPad() || _usePopupPresentation) {
                    dispatch_async(dispatch_get_main_queue(), ^{
                        if (!CGRectEqualToRect(containerVC.view.frame, containerVC.customFrame)) {
                            [CATransaction begin];
                            [CATransaction setDisableActions:YES];
                            containerVC.view.frame = containerVC.customFrame;
                            containerVC.view.bounds = CGRectMake(0, 0, containerVC.customFrame.size.width, containerVC.customFrame.size.height);
                            [CATransaction commit];
                        }
                        
                        // Also verify bounds match (especially important on iPad)
                        if (!CGSizeEqualToSize(containerVC.view.bounds.size, containerVC.customFrame.size)) {
                            [CATransaction begin];
                            [CATransaction setDisableActions:YES];
                            containerVC.view.bounds = CGRectMake(0, 0, containerVC.customFrame.size.width, containerVC.customFrame.size.height);
                            [CATransaction commit];
                        }
                    });
                } else {
                    // iPhone card mode: Verify frame is still below screen after window appears
                    dispatch_async(dispatch_get_main_queue(), ^{
                        CGFloat screenHeight = screenBounds.size.height;
                        CGFloat currentY = containerVC.view.frame.origin.y;
                        if (currentY < screenHeight) {
                            // Frame was reset to top, fix it to below screen
                            CGFloat belowScreenY = screenHeight + height; // Entire card below screen
                            [CATransaction begin];
                            [CATransaction setDisableActions:YES];
                            containerVC.view.frame = CGRectMake(x, belowScreenY, width, height);
                            containerVC.customFrame = CGRectMake(x, belowScreenY, width, height);
                            [CATransaction commit];
                        }
                    });
                }
                
                // Create dark overlay UNDER the card view
                UIView *overlayView = [[UIView alloc] initWithFrame:screenBounds];
                overlayView.backgroundColor = [UIColor colorWithWhite:0.0 alpha:0.0];
                overlayView.userInteractionEnabled = YES; // Enable tap-to-dismiss
                [cardWindow insertSubview:overlayView atIndex:0];
                
                // Store overlay reference for gesture handlers
                objc_setAssociatedObject(containerVC, "overlayView", overlayView, OBJC_ASSOCIATION_RETAIN_NONATOMIC);
                
                // Apply corner radius - split logic for iPhone and iPad
                UIRectCorner cornersToRound;
                if (isRunningOniPad()) {
                    // iPad: always all corners rounded
                    cornersToRound = UIRectCornerAllCorners;
                } else {
                    // iPhone: based on vertical position
                    if (_cardVerticalPosition < 0.1) {
                        cornersToRound = UIRectCornerBottomLeft | UIRectCornerBottomRight;
                    } else if (_cardVerticalPosition > 0.9) {
                        cornersToRound = UIRectCornerTopLeft | UIRectCornerTopRight;
                    } else {
                        cornersToRound = UIRectCornerAllCorners;
                    }
                }
                
                // Apply corner radius mask (use customFrame size to ensure correct bounds)
                CGRect maskBounds = CGRectMake(0, 0, containerVC.customFrame.size.width, containerVC.customFrame.size.height);
                UIBezierPath *maskPath = [UIBezierPath bezierPathWithRoundedRect:maskBounds
                                                              byRoundingCorners:cornersToRound
                                                                    cornerRadii:CGSizeMake(12.0, 12.0)];
                CAShapeLayer *maskLayer = [[CAShapeLayer alloc] init];
                maskLayer.frame = maskBounds;
                maskLayer.path = maskPath.CGPath;
                containerVC.view.layer.mask = maskLayer;
                
                CGFloat overlayOpacity = isRunningOniPad() ? 0.25 : 0.4;
                CGFloat animationDuration = _usePopupPresentation ? 0.2 : 0.3;
                
                // Show and animate immediately for all modes
                if (_usePopupPresentation) {
                    // Popup mode: SIMPLE fade-in at center - NOTHING ELSE
                    // Frame is already set correctly, transform is identity, just fade in
                    // Also fade in loading view with container (no sliding)
                    UIView *loadingView = objc_getAssociatedObject(containerVC, "loadingView");
                    [UIView animateWithDuration:animationDuration 
                                          delay:0 
                                        options:UIViewAnimationOptionCurveEaseOut 
                                     animations:^{
                        containerVC.view.alpha = 1.0;
                        overlayView.backgroundColor = [UIColor colorWithWhite:0.0 alpha:overlayOpacity];
                        // Fade in loading view with container (no sliding)
                        if (loadingView) {
                            loadingView.alpha = 1.0;
                        }
                    } completion:^(BOOL finished) {
                        // NOW allow layout updates after fade-in completes
                        containerVC.skipLayoutDuringInitialSetup = NO;
                        
                        // Keyboard handling: iPhone only (iPad doesn't need keyboard adjustments)
                        if (!isRunningOniPad()) {
                            [[StashPayCardSafariDelegate sharedInstance] startKeyboardObserving];
                        }
                        
                        [StashPayCardSafariDelegate sharedInstance].safariViewDismissedCallback = ^{
                            // Stop keyboard observing only if it was started (iPhone only)
                            if (!isRunningOniPad()) {
                                [[StashPayCardSafariDelegate sharedInstance] stopKeyboardObserving];
                            }
                            if (_safariViewDismissedCallback != NULL) {
                                _safariViewDismissedCallback();
                            }
                        };
                    }];
                } else {
                    // Card mode: split implementation for iPhone and iPad
                    if (isRunningOniPad()) {
                        // iPad card mode: slide-up animation, supports rotation
                        [UIView animateWithDuration:animationDuration 
                                              delay:0 
                             usingSpringWithDamping:0.85 
                              initialSpringVelocity:0.5 
                                            options:UIViewAnimationOptionCurveEaseOut 
                                         animations:^{
                            containerVC.view.alpha = 1.0;
                            containerVC.view.transform = CGAffineTransformIdentity;
                            overlayView.backgroundColor = [UIColor colorWithWhite:0.0 alpha:overlayOpacity];
                        } completion:^(BOOL finished) {
                            // Re-enable layout updates after animation completes
                            containerVC.skipLayoutDuringInitialSetup = NO;
                            
                            // Add drag tray (iPad: only drag tray gesture, no pan on card)
                            UIView *dragTray = [[StashPayCardSafariDelegate sharedInstance] createDragTray:width];
                            [containerVC.view addSubview:dragTray];
                            [StashPayCardSafariDelegate sharedInstance].dragTrayView = dragTray;
                            
                            // iPad: NO keyboard observing, NO tap-to-dismiss, NO pan gesture on card
                            
                            [StashPayCardSafariDelegate sharedInstance].safariViewDismissedCallback = ^{
                                // No keyboard cleanup needed for iPad
                                if (_safariViewDismissedCallback != NULL) {
                                    _safariViewDismissedCallback();
                                }
                            };
                        }];
                    } else {
                        // iPhone card mode: smooth slide-up animation from bottom (like Apple Pay)
                        // Use native iOS animation timing for smooth, natural feel
                        // Animate overlay fade-in slightly before card slide for smoother effect
                        [UIView animateWithDuration:0.15 animations:^{
                            overlayView.backgroundColor = [UIColor colorWithWhite:0.0 alpha:overlayOpacity];
                        }];
                        
                        // Main card slide animation with refined spring physics
                        [UIView animateWithDuration:0.6 
                                              delay:0.05 
                             usingSpringWithDamping:0.75 
                              initialSpringVelocity:0.0 
                                            options:UIViewAnimationOptionCurveEaseOut 
                                         animations:^{
                            // Animate frame from below screen to final position
                            containerVC.view.frame = CGRectMake(x, finalY, width, height);
                            containerVC.customFrame = CGRectMake(x, finalY, width, height);
                            // Ensure alpha is fully opaque (in case it was slightly transparent)
                            containerVC.view.alpha = 1.0;
                        } completion:^(BOOL finished) {
                            // Re-enable layout updates after animation completes
                            containerVC.skipLayoutDuringInitialSetup = NO;
                            
                            // Add drag tray (already has pan gesture for dismiss)
                            UIView *dragTray = [[StashPayCardSafariDelegate sharedInstance] createDragTray:width];
                            [containerVC.view addSubview:dragTray];
                            [StashPayCardSafariDelegate sharedInstance].dragTrayView = dragTray;
                            
                            // iPhone: tap-to-dismiss on overlay
                            UIButton *dismissButton = [UIButton buttonWithType:UIButtonTypeCustom];
                            dismissButton.frame = overlayView.bounds;
                            dismissButton.backgroundColor = [UIColor clearColor];
                            dismissButton.autoresizingMask = UIViewAutoresizingFlexibleWidth | UIViewAutoresizingFlexibleHeight;
                            [overlayView addSubview:dismissButton];
                            [dismissButton addTarget:[StashPayCardSafariDelegate sharedInstance]
                                             action:@selector(dismissButtonTapped:)
                                   forControlEvents:UIControlEventTouchUpInside];
                            
                            // iPhone: start keyboard observing
                            [[StashPayCardSafariDelegate sharedInstance] startKeyboardObserving];
                            
                            [StashPayCardSafariDelegate sharedInstance].safariViewDismissedCallback = ^{
                                // iPhone: stop keyboard observing
                                [[StashPayCardSafariDelegate sharedInstance] stopKeyboardObserving];
                                if (_safariViewDismissedCallback != NULL) {
                                    _safariViewDismissedCallback();
                                }
                            };
                        }];
                    }
                }
            }
        }
    }

    // Resets the card presentation state
    void _StashPayCardResetPresentationState() {
        if ([StashPayCardSafariDelegate sharedInstance].currentPresentedVC) {
            [[StashPayCardSafariDelegate sharedInstance].currentPresentedVC dismissViewControllerAnimated:NO completion:^{
                [[StashPayCardSafariDelegate sharedInstance] cleanupCardInstance];
            }];
        } else {
            // If no view controller is presented, just clean up the state
            [[StashPayCardSafariDelegate sharedInstance] cleanupCardInstance];
        }
    }

    bool _StashPayCardIsCurrentlyPresented() {
        return _isCardCurrentlyPresented;
    }

    void _StashPayCardSetForceSafariViewController(bool force) {
        _forceSafariViewController = force;
    }

    bool _StashPayCardGetForceSafariViewController() {
        return _forceSafariViewController;
    }

    // Forward declaration
    void _StashPayCardOpenPopupWithSize(const char* urlString, float portraitWidth, float portraitHeight, float landscapeWidth, float landscapeHeight);
    
    // Helper function to open popup with given multipliers
    static void OpenPopupWithMultipliers(const char* urlString, BOOL useCustomSize, float portraitWidth, float portraitHeight, float landscapeWidth, float landscapeHeight) {
        if (useCustomSize) {
            _useCustomPopupSize = YES;
            _customPortraitWidthMultiplier = portraitWidth;
            _customPortraitHeightMultiplier = portraitHeight;
            _customLandscapeWidthMultiplier = landscapeWidth;
            _customLandscapeHeightMultiplier = landscapeHeight;
        } else {
            _useCustomPopupSize = NO;
        }
        
        CGRect screenBounds = [UIScreen mainScreen].bounds;
        BOOL isLandscape = UIInterfaceOrientationIsLandscape([[UIApplication sharedApplication] statusBarOrientation]);
        
        // Calculate base size
        CGFloat smallerDimension = fmin(screenBounds.size.width, screenBounds.size.height);
        CGFloat percentage = isRunningOniPad() ? 0.5 : 0.75;
        CGFloat baseSize = fmax(
            isRunningOniPad() ? 400.0 : 300.0,
            fmin(isRunningOniPad() ? 500.0 : 500.0, smallerDimension * percentage)
        );
        
        // Use multipliers based on whether custom size is set
        CGFloat portraitWidthMultiplier, portraitHeightMultiplier, landscapeWidthMultiplier, landscapeHeightMultiplier;
        if (_useCustomPopupSize) {
            portraitWidthMultiplier = _customPortraitWidthMultiplier;
            portraitHeightMultiplier = _customPortraitHeightMultiplier;
            landscapeWidthMultiplier = _customLandscapeWidthMultiplier;
            landscapeHeightMultiplier = _customLandscapeHeightMultiplier;
        } else {
            // iOS default multipliers
            portraitWidthMultiplier = 1.0285;
            portraitHeightMultiplier = 1.485;
            landscapeWidthMultiplier = 1.753635;
            landscapeHeightMultiplier = 1.1385;
        }
        
        CGFloat popupWidth = baseSize * (isLandscape ? landscapeWidthMultiplier : portraitWidthMultiplier);
        CGFloat popupHeight = baseSize * (isLandscape ? landscapeHeightMultiplier : portraitHeightMultiplier);
        
        _cardWidthRatio = popupWidth / screenBounds.size.width;
        _cardHeightRatio = popupHeight / screenBounds.size.height;
        _cardVerticalPosition = 0.5 + (_cardHeightRatio / 2.0);
        
        _originalCardWidthRatio = _cardWidthRatio;
        _originalCardHeightRatio = _cardHeightRatio;
        _originalCardVerticalPosition = _cardVerticalPosition;
        _isCardExpanded = NO;
        _usePopupPresentation = YES;
        
        _StashPayCardOpenCheckoutInSafariVC(urlString);
    }
    
    void _StashPayCardOpenPopup(const char* urlString) {
        OpenPopupWithMultipliers(urlString, NO, 0, 0, 0, 0);
    }
    
    void _StashPayCardOpenPopupWithSize(const char* urlString, float portraitWidth, float portraitHeight, float landscapeWidth, float landscapeHeight) {
        OpenPopupWithMultipliers(urlString, YES, portraitWidth, portraitHeight, landscapeWidth, landscapeHeight);
    }
}


