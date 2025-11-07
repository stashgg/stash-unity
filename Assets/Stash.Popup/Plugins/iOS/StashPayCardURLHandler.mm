#import <Foundation/Foundation.h>
#import <SafariServices/SafariServices.h>
#import <WebKit/WebKit.h>
#import <objc/runtime.h>

__attribute__((constructor))
static void InitializeWebKit() {
}

// Unity callbacks
typedef void (*SafariViewDismissedCallback)();
typedef void (*PaymentSuccessCallback)();
typedef void (*PaymentFailureCallback)();
SafariViewDismissedCallback _safariViewDismissedCallback = NULL;
PaymentSuccessCallback _paymentSuccessCallback = NULL;
PaymentFailureCallback _paymentFailureCallback = NULL;

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

// Presentation modes
static BOOL _forceSafariViewController = NO;
static BOOL _usePopupPresentation = NO;
static BOOL _isCardExpanded = NO;

// Store original autorotate setting to restore later
static BOOL _originalAutoRotateEnabled = YES;

#define ENABLE_IPAD_SUPPORT 1

// Define a delegate class to handle Safari View Controller callbacks
@interface StashPayCardSafariDelegate : NSObject <SFSafariViewControllerDelegate, UIGestureRecognizerDelegate, WKScriptMessageHandler>
+ (instancetype)sharedInstance;
@property (nonatomic, copy) void (^safariViewDismissedCallback)(void);
@property (nonatomic, strong) UIViewController *currentPresentedVC;
@property (nonatomic, strong) UIPanGestureRecognizer *panGestureRecognizer;
@property (nonatomic, strong) UIView *dragTrayView;
@property (nonatomic, strong) UIView *navigationBarView;
@property (nonatomic, strong) UIView *closeButtonView;
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
- (UIView *)createFloatingCloseButton;
- (void)showFloatingCloseButton;
- (void)hideFloatingCloseButton;
- (void)backButtonTapped:(UIButton *)button;
- (void)closeButtonTapped:(UIButton *)button;
- (void)startKeyboardObserving;
- (void)stopKeyboardObserving;
- (void)keyboardWillShow:(NSNotification *)notification;
- (void)keyboardWillHide:(NSNotification *)notification;
@end

// WebView navigation delegate to handle loading states
@interface WebViewLoadDelegate : NSObject <WKNavigationDelegate>
@property (nonatomic, weak) WKWebView *webView;
- (instancetype)initWithWebView:(WKWebView*)webView loadingView:(UIView*)loadingView;
@end

// WebView UI delegate to disable context menus and text selection
@interface WebViewUIDelegate : NSObject <WKUIDelegate>
@end

// Forward declaration
BOOL isRunningOniPad();

// Custom view controller to control orientation
@interface OrientationLockedViewController : UIViewController
@property (nonatomic, assign) BOOL lockPortrait;
@end

@implementation OrientationLockedViewController

- (UIInterfaceOrientationMask)supportedInterfaceOrientations {
    // Lock to portrait for OpenURL on iPhone
    if (self.lockPortrait && !isRunningOniPad()) {
        return UIInterfaceOrientationMaskPortrait;
    }
    
    // For popup or iPad, lock to current orientation
    UIInterfaceOrientation currentOrientation = [[UIApplication sharedApplication] statusBarOrientation];
    return (1 << currentOrientation);
}

- (BOOL)shouldAutorotate {
    return NO; // Never auto-rotate while dialog is presented
}

- (UIInterfaceOrientation)preferredInterfaceOrientationForPresentation {
    if (self.lockPortrait && !isRunningOniPad()) {
        return UIInterfaceOrientationPortrait;
    }
    return [[UIApplication sharedApplication] statusBarOrientation];
}

@end

@implementation WebViewLoadDelegate {
    __weak WKWebView* _webView;
    UIView* _loadingView;
    NSTimer* _timeoutTimer;
    BOOL _hasStartedRendering;
}

- (instancetype)initWithWebView:(WKWebView*)webView loadingView:(UIView*)loadingView {
    self = [super init];
    if (self) {
        _webView = webView;
        self.webView = webView; // Store weak reference for navigation bar functionality
        _loadingView = loadingView;
        _hasStartedRendering = NO;
        
        // Create a fallback timer to handle cases where navigation events aren't fired
        _timeoutTimer = [NSTimer scheduledTimerWithTimeInterval:0.2  // Fast timeout for immediate content display
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
    
    // Allow all navigation within the WebView to keep users in the payment flow
    // This includes link clicks, form submissions, redirects, etc.
    
    // Check for specific schemes that should never be handled in WebView
    if ([url.scheme isEqualToString:@"tel"] ||
        [url.scheme isEqualToString:@"mailto"] ||
        [url.scheme isEqualToString:@"sms"]) {
        // These should open in system apps
        decisionHandler(WKNavigationActionPolicyCancel);
        [[UIApplication sharedApplication] openURL:url options:@{} completionHandler:nil];
        return;
    }
    
    // Check for app store links that should open externally
    if ([urlString containsString:@"apps.apple.com"] ||
        [urlString containsString:@"itunes.apple.com"]) {
        decisionHandler(WKNavigationActionPolicyCancel);
        [[UIApplication sharedApplication] openURL:url options:@{} completionHandler:nil];
        return;
    }
    
    // For all other navigation (including external domains), allow it in the WebView
    // This keeps payment flows, external authentication, and other links within the card
    decisionHandler(WKNavigationActionPolicyAllow);
}

- (void)handleTimeout:(NSTimer*)timer {
    if (_webView.hidden) {
        // Check if the page is truly ready
        NSString *readyCheck = @"(function() { \
            if (document.readyState !== 'complete') return false; \
            if (document.documentElement.style.display === 'none') return false; \
            if (document.body === null) return false; \
            if (window.getComputedStyle(document.body).display === 'none') return false; \
            return true; \
        })()";
        
        [_webView evaluateJavaScript:readyCheck completionHandler:^(id result, NSError *error) {
            if ([result boolValue]) {
        [self showWebViewAndRemoveLoading];
            } else {
                // If not ready, wait a bit more
                dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.1 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
                    [self handleTimeout:timer];
                });
            }
        }];
    }
}

- (void)showWebViewAndRemoveLoading {
    if (_timeoutTimer) {
        [_timeoutTimer invalidate];
        _timeoutTimer = nil;
    }
    
    // Check if we've already shown the webview (alpha > 0)
    if (_webView.alpha < 0.01) {
        // Final check to ensure page is truly ready
        [_webView evaluateJavaScript:@"document.readyState === 'complete' && document.body !== null" completionHandler:^(id result, NSError *error) {
            if (![result boolValue]) {
                // If not ready, wait and try again
                dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.1 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
                    [self showWebViewAndRemoveLoading];
                });
                return;
            }
            
            // Get the current system background color
            UIColor *backgroundColor;
            if (@available(iOS 13.0, *)) {
                UIUserInterfaceStyle currentStyle = [UITraitCollection currentTraitCollection].userInterfaceStyle;
                backgroundColor = (currentStyle == UIUserInterfaceStyleDark) ? [UIColor blackColor] : [UIColor systemBackgroundColor];
            } else {
                backgroundColor = [UIColor whiteColor];
            }
            
            // Ensure WebView background is solid and matches loading view
            self->_webView.backgroundColor = backgroundColor;
            self->_webView.scrollView.backgroundColor = backgroundColor;
            self->_webView.scrollView.opaque = YES;
            self->_webView.opaque = YES;
            
            // Force background color in the web content
            if (@available(iOS 13.0, *)) {
                UIUserInterfaceStyle currentStyle = [UITraitCollection currentTraitCollection].userInterfaceStyle;
                if (currentStyle == UIUserInterfaceStyleDark) {
                    NSString *forceColor = @"document.documentElement.style.backgroundColor = 'black'; \
                                          document.body.style.backgroundColor = 'black'; \
                                          var style = document.createElement('style'); \
                                          style.innerHTML = 'body, html { background-color: black !important; }'; \
                                          document.head.appendChild(style);";
                    [self->_webView evaluateJavaScript:forceColor completionHandler:nil];
                }
            }
            
            // Seamless cross-fade: both views visible, just swap opacity
            [UIView animateWithDuration:0.3 delay:0 options:UIViewAnimationOptionCurveEaseInOut animations:^{
                self->_loadingView.alpha = 0.0;
                self->_webView.alpha = 1.0;
            } completion:^(BOOL finished) {
                [self->_loadingView removeFromSuperview];
            }];
        }];
    }
}

- (void)webView:(WKWebView *)webView didFinishNavigation:(WKNavigation *)navigation {
    _hasStartedRendering = NO;
    
    // Function to check if page is truly ready
    void (^checkPageReady)(void) = ^{
        NSString *readyCheck = @"(function() { \
            if (document.readyState !== 'complete') return false; \
            if (document.documentElement.style.display === 'none') return false; \
            if (document.body === null) return false; \
            if (window.getComputedStyle(document.body).display === 'none') return false; \
            return true; \
        })()";
        
        [webView evaluateJavaScript:readyCheck completionHandler:^(id result, NSError *error) {
            if ([result boolValue]) {
                // Page is ready, force background color and show WebView
                if (@available(iOS 13.0, *)) {
                    UIUserInterfaceStyle currentStyle = [UITraitCollection currentTraitCollection].userInterfaceStyle;
                    if (currentStyle == UIUserInterfaceStyleDark) {
                        NSString *forceColor = @"document.documentElement.style.backgroundColor = 'black'; \
                                              document.body.style.backgroundColor = 'black'; \
                                              var style = document.createElement('style'); \
                                              style.innerHTML = 'body, html { background-color: black !important; }'; \
                                              document.head.appendChild(style);";
                        [webView evaluateJavaScript:forceColor completionHandler:^(id result, NSError *error) {
                            // Show immediately after forcing color
                            [self showWebViewAndRemoveLoading];
                        }];
                    } else {
                        // Show immediately for light mode
                        [self showWebViewAndRemoveLoading];
                    }
                } else {
                    // Show immediately if not iOS 13+
                    [self showWebViewAndRemoveLoading];
                }
            } else {
                // Page not ready yet, check again after a short delay
                dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.1 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
                    checkPageReady();
                });
            }
        }];
    };
    
    // Start checking if page is ready
    checkPageReady();
}

- (void)webView:(WKWebView *)webView didCommitNavigation:(WKNavigation *)navigation {
    // Content has started rendering
    _hasStartedRendering = YES;
    // Don't show the webview here anymore, wait for full load
}

- (void)webView:(WKWebView *)webView didStartProvisionalNavigation:(WKNavigation *)navigation {
    _hasStartedRendering = NO;
}

- (void)webView:(WKWebView *)webView didFailNavigation:(WKNavigation *)navigation withError:(NSError *)error {
    // Even on error, wait a bit before showing to prevent flashing
    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.3 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
    [self showWebViewAndRemoveLoading];
    });
}

- (void)webView:(WKWebView *)webView didFailProvisionalNavigation:(WKNavigation *)navigation withError:(NSError *)error {
    // Even on error, wait a bit before showing to prevent flashing
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
- (void)webView:(WKWebView *)webView runOpenPanelWithParameters:(WKOpenPanelParameters *)parameters initiatedByFrame:(WKFrameInfo *)frame completionHandler:(void (^)(NSArray<NSURL *> *))completionHandler {
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

// Method to ensure callback is only called once
- (void)callUnityCallbackOnce {
    if (!_callbackWasCalled && _safariViewDismissedCallback != NULL) {
        _callbackWasCalled = YES;
        _isCardCurrentlyPresented = NO; // Reset the presentation flag
        dispatch_async(dispatch_get_main_queue(), ^{
            _safariViewDismissedCallback();
        });
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
    
    if (self.closeButtonView) {
        [self.closeButtonView removeFromSuperview];
        self.closeButtonView = nil;
    }
    
    if (self.panGestureRecognizer && self.currentPresentedVC) {
        [self.currentPresentedVC.view removeGestureRecognizer:self.panGestureRecognizer];
        self.panGestureRecognizer.delegate = nil;
        self.panGestureRecognizer = nil;
    }
    
    if (self.currentPresentedVC) {
        for (UIView *subview in self.currentPresentedVC.view.subviews) {
            if ([subview isKindOfClass:NSClassFromString(@"WKWebView")]) {
                WKWebView *webView = (WKWebView *)subview;
                webView.navigationDelegate = nil;
                webView.UIDelegate = nil;
                [webView.configuration.userContentController removeScriptMessageHandlerForName:@"stashPaymentSuccess"];
                [webView.configuration.userContentController removeScriptMessageHandlerForName:@"stashPaymentFailure"];
                [webView.configuration.userContentController removeScriptMessageHandlerForName:@"stashPurchaseProcessing"];
                objc_setAssociatedObject(self.currentPresentedVC, "webViewDelegate", nil, OBJC_ASSOCIATION_RETAIN_NONATOMIC);
                objc_setAssociatedObject(self.currentPresentedVC, "webViewUIDelegate", nil, OBJC_ASSOCIATION_RETAIN_NONATOMIC);
                [webView stopLoading];
                break;
            }
        }
    }
    
    self.currentPresentedVC = nil;
    self.initialURL = nil;
    
    self.isNavigationBarVisible = NO;
    self.isPurchaseProcessing = NO;
    _isCardExpanded = NO;
    _isCardCurrentlyPresented = NO;
    _usePopupPresentation = NO;
    _callbackWasCalled = NO;
    _paymentSuccessHandled = NO;
    _paymentSuccessCallbackCalled = NO;
    
    // Unlock rotation when dialog is dismissed
    UnlockRotation();
}

- (void)safariViewControllerDidFinish:(SFSafariViewController *)controller {
    if (_forceSafariViewController) {
        if (_safariViewDismissedCallback != NULL) {
            dispatch_async(dispatch_get_main_queue(), ^{
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
    
    if (_usePopupPresentation) {
        [UIView animateWithDuration:0.2 animations:^{
            self.currentPresentedVC.view.alpha = 0.0;
            self.currentPresentedVC.view.transform = CGAffineTransformMakeScale(0.9, 0.9);
            self.currentPresentedVC.view.superview.backgroundColor = [UIColor colorWithWhite:0.0 alpha:0.0];
        } completion:^(BOOL finished) {
            [self.currentPresentedVC dismissViewControllerAnimated:NO completion:completion];
        }];
    } else {
        [self.currentPresentedVC dismissViewControllerAnimated:YES completion:completion];
    }
}

- (void)handlePanGesture:(UIPanGestureRecognizer *)gesture {
    if (!self.currentPresentedVC) return;
    if (self.isPurchaseProcessing) return;
    
    UIView *view = self.currentPresentedVC.view;
    CGFloat height = view.frame.size.height;
    CGPoint translation = [gesture translationInView:view.superview];
    
    // For iPhone, determine allowed swipe direction based on card position
    BOOL isNearTop = _cardVerticalPosition < 0.1;
    
    // iPad: only allow downward (dismiss), not upward
    // iPhone: allow upward only for top-positioned cards
    BOOL allowUpward = !isRunningOniPad() && isNearTop;
    BOOL allowDownward = isRunningOniPad() || NO;
    
    switch (gesture.state) {
        case UIGestureRecognizerStateBegan:
            self.initialY = view.frame.origin.y;
            break;
            
        case UIGestureRecognizerStateChanged: {
            // Calculate new Y position based on allowed direction
            CGFloat newY = self.initialY;
            
            if (allowUpward && translation.y < 0) {
                // Allow upward movement
                newY = self.initialY + translation.y;
            } else if (allowDownward && translation.y > 0) {
                // Allow downward movement
                newY = self.initialY + translation.y;
            } else {
                // Movement not allowed in this direction
                return;
            }
            
            view.frame = CGRectMake(view.frame.origin.x, newY, view.frame.size.width, height);
            
            // Adjust background opacity based on position
            CGFloat maxTravel = height;
            CGFloat currentTravel = fabs(newY - self.initialY);
            CGFloat ratio = 1.0 - (currentTravel / maxTravel);
            if (ratio < 0) ratio = 0;
            view.superview.backgroundColor = [UIColor colorWithWhite:0.0 alpha:0.4 * ratio];
            break;
        }
            
        case UIGestureRecognizerStateEnded:
        case UIGestureRecognizerStateCancelled: {
            // Get the velocity of the gesture
            CGPoint velocity = [gesture velocityInView:view.superview];
            CGFloat currentY = view.frame.origin.y;
            CGFloat dismissThreshold = height * 0.3;
            
            // Determine if we should dismiss based on position and velocity
            BOOL shouldDismiss = NO;
            CGFloat finalY = 0;
            
            if (isRunningOniPad()) {
                // iPad: only allow downward dismissal (not upward)
                if ((velocity.y > 300 || currentY > (self.initialY + dismissThreshold)) && allowDownward) {
                    shouldDismiss = YES;
                    finalY = view.superview.bounds.size.height;
                }
            } else {
                // iPhone: original logic for top-positioned cards only
                if (isNearTop) {
                    // Dismiss if swiped up with enough velocity or dragged up far enough
                    shouldDismiss = (velocity.y < -300 || currentY < (self.initialY - dismissThreshold));
                    finalY = -height;
                } else {
                    // DISABLED: No dismissal for bottom/middle positioned cards via gesture
                    shouldDismiss = NO;
                }
            }
            
            if (shouldDismiss) {
                // Animate the rest of the way out, then dismiss
                [UIView animateWithDuration:0.15 animations:^{  // Reduced from 0.2
                    view.frame = CGRectMake(view.frame.origin.x, finalY, view.frame.size.width, height);
                    view.superview.backgroundColor = [UIColor colorWithWhite:0.0 alpha:0.0];
                } completion:^(BOOL finished) {
                    [self.currentPresentedVC dismissViewControllerAnimated:NO completion:^{
                        [self cleanupCardInstance];
                        [self callUnityCallbackOnce];
                    }];
                }];
            } else {
                // Animate back to original position with spring animation
                [UIView animateWithDuration:0.2  // Reduced from 0.3
                                      delay:0 
                     usingSpringWithDamping:0.7 
                      initialSpringVelocity:0 
                                    options:UIViewAnimationOptionCurveEaseOut 
                                 animations:^{
                    view.frame = CGRectMake(view.frame.origin.x, self.initialY, view.frame.size.width, height);
                    CGFloat baseOpacity = _isCardExpanded ? 0.6 : 0.4;
                    view.superview.backgroundColor = [UIColor colorWithWhite:0.0 alpha:baseOpacity];
                } completion:nil];
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

// Helper function to lock device rotation
void LockRotation() {
    // Store original state
    _originalAutoRotateEnabled = [[UIDevice currentDevice] isGeneratingDeviceOrientationNotifications];
    
    // Disable auto-rotation by posting notification
    if (@available(iOS 16.0, *)) {
        // iOS 16+: Use window scene geometry preferences
        UIWindowScene *windowScene = (UIWindowScene *)[[UIApplication sharedApplication].connectedScenes anyObject];
        if (windowScene) {
            UIInterfaceOrientation currentOrientation = windowScene.interfaceOrientation;
            [windowScene requestGeometryUpdateWithPreferences:[[UIWindowSceneGeometryPreferencesIOS alloc] initWithInterfaceOrientations:(1 << currentOrientation)] errorHandler:nil];
        }
    } else {
        // iOS 15 and below: Use setValue approach
        [[UIDevice currentDevice] beginGeneratingDeviceOrientationNotifications];
        UIInterfaceOrientation currentOrientation = [[UIApplication sharedApplication] statusBarOrientation];
        [[UIDevice currentDevice] setValue:@(currentOrientation) forKey:@"orientation"];
        [UIViewController attemptRotationToDeviceOrientation];
    }
}

// Helper function to unlock device rotation
void UnlockRotation() {
    if (@available(iOS 16.0, *)) {
        // iOS 16+: Reset geometry preferences to allow all orientations
        UIWindowScene *windowScene = (UIWindowScene *)[[UIApplication sharedApplication].connectedScenes anyObject];
        if (windowScene) {
            [windowScene requestGeometryUpdateWithPreferences:[[UIWindowSceneGeometryPreferencesIOS alloc] initWithInterfaceOrientations:UIInterfaceOrientationMaskAll] errorHandler:nil];
        }
    } else {
        // iOS 15 and below
        if (_originalAutoRotateEnabled) {
            [[UIDevice currentDevice] endGeneratingDeviceOrientationNotifications];
        }
        [UIViewController attemptRotationToDeviceOrientation];
    }
}

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
        
        // Update handle position for full screen - make it highly visible
        UIView *handle = dragTray.subviews.firstObject;
        if (handle) {
            handle.frame = CGRectMake(fullScreenFrame.size.width/2 - 20, 12, 40, 5);
            
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
        cardView.superview.backgroundColor = [UIColor colorWithWhite:0.0 alpha:0.6]; // Semi-transparent instead of solid black
        
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
    
    // Update drag tray
    UIView *dragTray = [cardView viewWithTag:8888];
    if (dragTray) {
        dragTray.frame = CGRectMake(0, 0, currentWidth, 44);
        
        // Update gradient layer
        CAGradientLayer *gradientLayer = (CAGradientLayer*)dragTray.layer.sublayers.firstObject;
        if (gradientLayer && [gradientLayer isKindOfClass:[CAGradientLayer class]]) {
            gradientLayer.frame = dragTray.bounds;
        }
        
        // Update handle position
        UIView *handle = dragTray.subviews.firstObject;
        if (handle) {
            handle.frame = CGRectMake(currentWidth/2 - 20, 12, 40, 5);
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
    CGFloat safeTop = safeAreaInsets.top;
    
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
    CGFloat currentWidth = collapsedWidth + (expandedWidth - collapsedWidth) * progress;
    
    // Define button positions for collapsed and expanded states
    CGFloat collapsedTopOffset = 16 + 6; // 22px from top in collapsed state
    CGFloat expandedTopOffset = 16; // 16px from top in expanded state
    
    // Interpolate top offset
    CGFloat currentTopOffset = collapsedTopOffset + (expandedTopOffset - collapsedTopOffset) * progress;
    
    // Update close button position if visible
    UIView *closeButton = [cardView viewWithTag:8887];
    if (closeButton && self.closeButtonView) {
        // Calculate right offset for both states
        CGFloat collapsedRightOffset = collapsedWidth - 16 - 40; // 16pt margin from right edge
        CGFloat expandedRightOffset = expandedWidth - safeAreaInsets.right - 16 - 40; // Respect right safe area
        
        // Interpolate right offset
        CGFloat currentRightOffset = collapsedRightOffset + (expandedRightOffset - collapsedRightOffset) * progress;
        
        closeButton.frame = CGRectMake(currentRightOffset, currentTopOffset, 40, 40);
    }
    
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
        
        // Update handle position for collapsed state - maintain high visibility
        UIView *handle = dragTray.subviews.firstObject;
        if (handle) {
            handle.frame = CGRectMake(width/2 - 20, 12, 40, 5);
            
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
        cardView.superview.backgroundColor = [UIColor colorWithWhite:0.0 alpha:0.4];
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
    handleView.frame = CGRectMake(cardWidth/2 - 20, 12, 40, 5);
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

- (UIView *)createFloatingCloseButton {
    UIButton *floatingCloseButton = [UIButton buttonWithType:UIButtonTypeSystem];
    
    CGFloat topSafeArea = 0;
    CGFloat cardWidth = 0;
    if (@available(iOS 11.0, *)) {
        UIView *currentView = self.currentPresentedVC.view;
        if (currentView && [currentView respondsToSelector:@selector(safeAreaInsets)]) {
            topSafeArea = currentView.safeAreaInsets.top;
        }
        cardWidth = currentView.frame.size.width;
    }
    
    if (cardWidth <= 0) {
        CGRect screenBounds = [UIScreen mainScreen].bounds;
        cardWidth = isRunningOniPad() ? calculateiPadCardSize(screenBounds).width : screenBounds.size.width * _cardWidthRatio;
    }
    
    CGFloat topOffset = MAX(16, topSafeArea + 8) + 6;
    CGFloat rightOffset = cardWidth - 56;
    floatingCloseButton.frame = CGRectMake(rightOffset, topOffset, 40, 40);
    floatingCloseButton.tag = 8887;
    
    BOOL isDarkMode = NO;
    if (@available(iOS 13.0, *)) {
        isDarkMode = ([UITraitCollection currentTraitCollection].userInterfaceStyle == UIUserInterfaceStyleDark);
    }
    
    [floatingCloseButton setTitle:@"✕" forState:UIControlStateNormal];
    floatingCloseButton.titleLabel.font = [UIFont systemFontOfSize:16 weight:UIFontWeightSemibold];
    [floatingCloseButton setTitleColor:isDarkMode ? [UIColor whiteColor] : [UIColor blackColor] forState:UIControlStateNormal];
    floatingCloseButton.backgroundColor = isDarkMode ? [UIColor colorWithWhite:0.1 alpha:0.5] : [UIColor colorWithWhite:0.95 alpha:0.5];
    floatingCloseButton.layer.cornerRadius = 20;
    floatingCloseButton.layer.shadowColor = [UIColor blackColor].CGColor;
    floatingCloseButton.layer.shadowOffset = CGSizeMake(0, isRunningOniPad() ? 1 : 2);
    floatingCloseButton.layer.shadowOpacity = isRunningOniPad() ? 0.1 : 0.2;
    floatingCloseButton.layer.shadowRadius = isRunningOniPad() ? 2 : 4;
    floatingCloseButton.layer.borderWidth = 0.5;
    floatingCloseButton.layer.borderColor = isDarkMode ? [UIColor colorWithWhite:0.3 alpha:0.5].CGColor : [UIColor colorWithWhite:0.7 alpha:0.5].CGColor;
    
    [floatingCloseButton addTarget:self action:@selector(closeButtonTapped:) forControlEvents:UIControlEventTouchUpInside];
    
    return floatingCloseButton;
}

- (void)showFloatingCloseButton {
    if (!self.currentPresentedVC) return;
    
    UIView *containerView = self.currentPresentedVC.view;
    
    if (!self.closeButtonView) {
        self.closeButtonView = [self createFloatingCloseButton];
    }
    
    self.closeButtonView.alpha = 0.0;
    self.closeButtonView.transform = CGAffineTransformMakeScale(0.8, 0.8);
    [containerView addSubview:self.closeButtonView];
    [containerView bringSubviewToFront:self.closeButtonView];
    
    [UIView animateWithDuration:0.3 delay:0.1 usingSpringWithDamping:0.7 initialSpringVelocity:0.5 options:UIViewAnimationOptionCurveEaseOut animations:^{
        self.closeButtonView.alpha = 1.0;
        self.closeButtonView.transform = CGAffineTransformIdentity;
    } completion:nil];
}

- (void)hideFloatingCloseButton {
    if (!self.closeButtonView) return;
    
    [UIView animateWithDuration:0.25 delay:0 usingSpringWithDamping:0.8 initialSpringVelocity:0.3 options:UIViewAnimationOptionCurveEaseIn animations:^{
        self.closeButtonView.alpha = 0.0;
        self.closeButtonView.transform = CGAffineTransformMakeScale(0.6, 0.6);
    } completion:^(BOOL finished) {
        [self.closeButtonView removeFromSuperview];
        self.closeButtonView = nil;
    }];
}

- (void)handleDragTrayPanGesture:(UIPanGestureRecognizer *)gesture {
    if (!self.currentPresentedVC) return;
    if (self.isPurchaseProcessing) return;
    
    UIView *cardView = self.currentPresentedVC.view;
    CGFloat height = cardView.frame.size.height;
    CGPoint translation = [gesture translationInView:cardView.superview];
    CGPoint velocity = [gesture velocityInView:cardView.superview];
    
    switch (gesture.state) {
        case UIGestureRecognizerStateBegan:
            self.initialY = cardView.frame.origin.y;
            break;
            
        case UIGestureRecognizerStateChanged: {
            CGFloat currentTravel = translation.y;
            CGFloat screenHeight = cardView.superview.bounds.size.height;
            
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
                        cardView.frame = CGRectMake(currentFrame.origin.x, newY, currentFrame.size.width, currentFrame.size.height);
                    }
                    
                } else {
                    // Normal collapsed position dragging for dismiss - direct linear movement
                    CGFloat newY = self.initialY + currentTravel;
                    CGFloat maxY = screenHeight;
                    newY = MIN(maxY, newY);
                    cardView.frame = CGRectMake(cardView.frame.origin.x, newY, cardView.frame.size.width, height);
                }
                
                // Smooth background opacity change - lighter on iPad
                CGFloat maxTravel = _isCardExpanded ? (height * 0.8) : (height * 0.6);
                CGFloat ratio = 1.0 - (currentTravel / maxTravel);
                ratio = MAX(0.1, MIN(1.0, ratio)); // Clamp between 0.1 and 1.0
                
                CGFloat baseOpacity = _isCardExpanded ? (isRunningOniPad() ? 0.45 : 0.6) : (isRunningOniPad() ? 0.25 : 0.4);
                cardView.superview.backgroundColor = [UIColor colorWithWhite:0.0 alpha:baseOpacity * ratio];
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
                // iOS-native dismiss animation
                CGFloat animationDuration = 0.4;
                if (velocity.y > 1000) {
                    animationDuration = 0.25; // Faster for quick swipes
                }
                
                CGFloat finalY = cardView.superview.bounds.size.height;
                [UIView animateWithDuration:animationDuration 
                                      delay:0 
                     usingSpringWithDamping:0.9 
                      initialSpringVelocity:velocity.y / 1000.0 
                                    options:UIViewAnimationOptionCurveEaseOut 
                                 animations:^{
                    cardView.frame = CGRectMake(cardView.frame.origin.x, finalY, cardView.frame.size.width, cardView.frame.size.height);
                    cardView.superview.backgroundColor = [UIColor colorWithWhite:0.0 alpha:0.0];
                } completion:^(BOOL finished) {
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
                } completion:nil];
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
    if (!_isCardExpanded && self.currentPresentedVC) {
        dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.1 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
            [self expandCardToFullScreen];
        });
    }
}

- (void)keyboardWillHide:(NSNotification *)notification {
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
        [self hideFloatingCloseButton];
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

- (void)closeButtonTapped:(UIButton *)button {
    // Dismiss the entire card
    if (self.currentPresentedVC) {
        [self dismissWithAnimation:^{
            [self cleanupCardInstance];
            [self callUnityCallbackOnce];
        }];
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

    // Opens a URL in Safari View Controller with delegation
    void _StashPayCardOpenURLInSafariVC(const char* urlString) {
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
                
                // Lock rotation while Safari is presented
                LockRotation();
                
                // Present with completely default system behavior - no custom presentation styles
                [topController presentViewController:safariViewController animated:YES completion:nil];
                
                // Set a simple callback for when the native Safari is dismissed
                [StashPayCardSafariDelegate sharedInstance].safariViewDismissedCallback = ^{
                    UnlockRotation();
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
            
            // Reset navigation bar and close button state for new card
            [[StashPayCardSafariDelegate sharedInstance] setIsNavigationBarVisible:NO];
            [[StashPayCardSafariDelegate sharedInstance] setNavigationBarView:nil];
            [[StashPayCardSafariDelegate sharedInstance] setCloseButtonView:nil];
            
            // Store the initial URL for back button functionality
            [[StashPayCardSafariDelegate sharedInstance] setInitialURL:url];
            
            // Create a custom view controller with WKWebView
            OrientationLockedViewController *containerVC = [[OrientationLockedViewController alloc] init];
            containerVC.modalPresentationStyle = UIModalPresentationOverFullScreen;
            
            // Lock to portrait for OpenURL on iPhone, allow all orientations for popup
            containerVC.lockPortrait = !_usePopupPresentation;
            
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
                "})();";
                
                WKUserScript *stashSDKInjection = [[WKUserScript alloc] initWithSource:stashSDKScript 
                                                                     injectionTime:WKUserScriptInjectionTimeAtDocumentStart 
                                                                  forMainFrameOnly:YES];
                [userContentController addUserScript:stashSDKInjection];
                
                // Add the script message handlers
                [userContentController addScriptMessageHandler:[StashPayCardSafariDelegate sharedInstance] name:@"stashPaymentSuccess"];
                [userContentController addScriptMessageHandler:[StashPayCardSafariDelegate sharedInstance] name:@"stashPaymentFailure"];
                [userContentController addScriptMessageHandler:[StashPayCardSafariDelegate sharedInstance] name:@"stashPurchaseProcessing"];
                
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
                loadingView.alpha = 1.0;
                
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
                
                NSMutableURLRequest *request = [NSMutableURLRequest requestWithURL:url 
                                                                    cachePolicy:NSURLRequestReturnCacheDataElseLoad
                                                                timeoutInterval:15.0];
                [request setValue:@"Mozilla/5.0 (iPhone; CPU iPhone OS 15_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/15.0 Mobile/15E148 Safari/604.1" forHTTPHeaderField:@"User-Agent"];
                [request setValue:@"gzip, deflate, br" forHTTPHeaderField:@"Accept-Encoding"];
                
                [webView loadRequest:request];
                
                CGRect screenBounds = [UIScreen mainScreen].bounds;
                
                // For portrait-locked OpenURL on iPhone, ensure we use portrait dimensions
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
                
                // For popup mode, always use the ratios set (don't override with iPad card size)
                // For regular card mode on iPad, use calculateiPadCardSize for better layout
                if (isRunningOniPad() && !_usePopupPresentation) {
                    CGSize cardSize = calculateiPadCardSize(screenBounds);
                    width = cardSize.width;
                    height = cardSize.height;
                    x = (screenBounds.size.width - width) / 2;
                    finalY = (screenBounds.size.height - height) / 2;
                } else {
                    width = screenBounds.size.width * _cardWidthRatio;
                    height = screenBounds.size.height * _cardHeightRatio;
                    x = (screenBounds.size.width - width) / 2;
                    finalY = screenBounds.size.height * _cardVerticalPosition - height;
                    if (finalY < 0) finalY = 0;
                }
                
                CGFloat y = _usePopupPresentation ? finalY : screenBounds.size.height;
                
                [topController presentViewController:containerVC animated:NO completion:^{
                    // Lock rotation while dialog is presented
                    LockRotation();
                    
                    containerVC.view.frame = CGRectMake(x, y, width, height);
                    
                    UIRectCorner cornersToRound;
                    if (_cardVerticalPosition < 0.1) {
                        cornersToRound = UIRectCornerBottomLeft | UIRectCornerBottomRight;
                    } else if (_cardVerticalPosition > 0.9) {
                        cornersToRound = UIRectCornerTopLeft | UIRectCornerTopRight;
                    } else {
                        cornersToRound = UIRectCornerAllCorners;
                    }
                    
                    if (isRunningOniPad()) {
                        cornersToRound = UIRectCornerAllCorners;
                    }
                    
                    UIBezierPath *maskPath = [UIBezierPath bezierPathWithRoundedRect:containerVC.view.bounds
                                                                  byRoundingCorners:cornersToRound
                                                                        cornerRadii:CGSizeMake(12.0, 12.0)];
                    CAShapeLayer *maskLayer = [[CAShapeLayer alloc] init];
                    maskLayer.frame = containerVC.view.bounds;
                    maskLayer.path = maskPath.CGPath;
                    containerVC.view.layer.mask = maskLayer;
                    
                    if (_usePopupPresentation) {
                        containerVC.view.alpha = 0.0;
                        containerVC.view.transform = CGAffineTransformMakeScale(0.9, 0.9);
                    }
                    
                    CGFloat overlayOpacity = isRunningOniPad() ? 0.25 : 0.4;
                    containerVC.view.superview.backgroundColor = [UIColor colorWithWhite:0.0 alpha:0.0];
                    CGFloat animationDuration = _usePopupPresentation ? 0.2 : 0.15;
                    
                    [UIView animateWithDuration:animationDuration animations:^{
                        if (_usePopupPresentation) {
                            containerVC.view.alpha = 1.0;
                            containerVC.view.transform = CGAffineTransformIdentity;
                        } else {
                            containerVC.view.frame = CGRectMake(x, finalY, width, height);
                        }
                        containerVC.view.superview.backgroundColor = [UIColor colorWithWhite:0.0 alpha:overlayOpacity];
                    } completion:^(BOOL finished) {
                        if (!_usePopupPresentation) {
                            UIView *dragTray = [[StashPayCardSafariDelegate sharedInstance] createDragTray:width];
                            [containerVC.view addSubview:dragTray];
                            [StashPayCardSafariDelegate sharedInstance].dragTrayView = dragTray;
                            
                            // Main pan gesture only for iPhone - iPad uses drag tray only
                            if (!isRunningOniPad()) {
                                UIPanGestureRecognizer *panGesture = [[UIPanGestureRecognizer alloc] initWithTarget:[StashPayCardSafariDelegate sharedInstance] action:@selector(handlePanGesture:)];
                                panGesture.delegate = [StashPayCardSafariDelegate sharedInstance];
                                [containerVC.view addGestureRecognizer:panGesture];
                                [StashPayCardSafariDelegate sharedInstance].panGestureRecognizer = panGesture;
                            }
                        }
                        
                        UIView *backgroundView = containerVC.view.superview;
                        
                        if (!_usePopupPresentation) {
                            for (UIGestureRecognizer *recognizer in [backgroundView.gestureRecognizers copy]) {
                                [backgroundView removeGestureRecognizer:recognizer];
                            }
                            
                            UIButton *dismissButton = [UIButton buttonWithType:UIButtonTypeCustom];
                            dismissButton.frame = backgroundView.bounds;
                            dismissButton.backgroundColor = [UIColor clearColor];
                            dismissButton.autoresizingMask = UIViewAutoresizingFlexibleWidth | UIViewAutoresizingFlexibleHeight;
                            [backgroundView addSubview:dismissButton];
                            [backgroundView sendSubviewToBack:dismissButton];
                            [dismissButton addTarget:[StashPayCardSafariDelegate sharedInstance] 
                                             action:@selector(dismissButtonTapped:) 
                                   forControlEvents:UIControlEventTouchUpInside];
                        }
                        
                        [StashPayCardSafariDelegate sharedInstance].currentPresentedVC = containerVC;
                        [[StashPayCardSafariDelegate sharedInstance] showFloatingCloseButton];
                        [[StashPayCardSafariDelegate sharedInstance] startKeyboardObserving];
                        
                        [StashPayCardSafariDelegate sharedInstance].safariViewDismissedCallback = ^{
                            [[StashPayCardSafariDelegate sharedInstance] stopKeyboardObserving];
                            if (_safariViewDismissedCallback != NULL) {
                                _safariViewDismissedCallback();
                            }
                        };
                    }];
                }];
                return;
            }
        }
    }

    // Resets the card presentation state (useful for debugging or force reset)
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

    // Returns whether a card is currently being presented
    bool _StashPayCardIsCurrentlyPresented() {
        return _isCardCurrentlyPresented;
    }

    // Sets whether to force use of SFSafariViewController over WKWebView
    void _StashPayCardSetForceSafariViewController(bool force) {
        _forceSafariViewController = force;
    }

    // Gets whether to force use of SFSafariViewController over WKWebView
    bool _StashPayCardGetForceSafariViewController() {
        return _forceSafariViewController;
    }

    void _StashPayCardOpenPopup(const char* urlString) {
        CGRect screenBounds = [UIScreen mainScreen].bounds;
        CGFloat smallerDimension = fmin(screenBounds.size.width, screenBounds.size.height);
        
        // iPad gets compact popup (50% smaller for modal dialogs)
        CGFloat minSize = isRunningOniPad() ? 400.0 : 300.0;
        CGFloat maxSize = isRunningOniPad() ? 500.0 : 500.0;
        CGFloat percentage = isRunningOniPad() ? 0.5 : 0.75; // iPad uses 50%, iPhone uses 75%
        CGFloat squareSize = fmax(minSize, fmin(maxSize, smallerDimension * percentage));
        
        CGFloat squareRatioWidth = squareSize / screenBounds.size.width;
        CGFloat squareRatioHeight = squareSize / screenBounds.size.height;
        CGFloat centerPosition = 0.5 + (squareRatioHeight / 2.0);
        
        _cardWidthRatio = squareRatioWidth;
        _cardHeightRatio = squareRatioHeight;
        _cardVerticalPosition = centerPosition;
        _originalCardWidthRatio = squareRatioWidth;
        _originalCardHeightRatio = squareRatioHeight;
        _originalCardVerticalPosition = centerPosition;
        _isCardExpanded = NO;
        _usePopupPresentation = YES;
        
        _StashPayCardOpenURLInSafariVC(urlString);
    }
}

