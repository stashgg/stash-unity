#import <Foundation/Foundation.h>
#import <SafariServices/SafariServices.h>
#import <WebKit/WebKit.h>
#import <objc/runtime.h>

// Tell Xcode to link WebKit framework
#pragma comment(lib, "WebKit.framework")

// Mark WebKit framework as required
__attribute__((constructor))
static void InitializeWebKit() {
}

// Define a Unity callback function typedef
typedef void (*SafariViewDismissedCallback)();
SafariViewDismissedCallback _safariViewDismissedCallback = NULL;

// Define a payment success callback function typedef
typedef void (*PaymentSuccessCallback)();
PaymentSuccessCallback _paymentSuccessCallback = NULL;

// Define a payment failure callback function typedef
typedef void (*PaymentFailureCallback)();
PaymentFailureCallback _paymentFailureCallback = NULL;

// Flag to track if callback was already called
BOOL _callbackWasCalled = NO;

// Flag to track if a card is currently being presented
BOOL _isCardCurrentlyPresented = NO;

// Flag to track if payment success was already handled
BOOL _paymentSuccessHandled = NO;

// Flag to track if payment success callback was already called for this session
BOOL _paymentSuccessCallbackCalled = NO;

// Configuration options for card size and position
static CGFloat _cardHeightRatio = 0.4; // Default to 40% of screen height
static CGFloat _cardVerticalPosition = 1.0; // Default to bottom of screen (1.0)
static CGFloat _cardWidthRatio = 1.0; // Default to 100% of screen width

// Compile-time flag to disable iPad detection for debugging (set to 0 to disable iPad features)
#define ENABLE_IPAD_SUPPORT 1

// Store original configuration for expand/collapse functionality
static CGFloat _originalCardHeightRatio = 0.4;
static CGFloat _originalCardVerticalPosition = 1.0;
static CGFloat _originalCardWidthRatio = 1.0;
static BOOL _isCardExpanded = NO;

// Define a delegate class to handle Safari View Controller callbacks
@interface StashPayCardSafariDelegate : NSObject <SFSafariViewControllerDelegate, UIGestureRecognizerDelegate, WKScriptMessageHandler>
+ (instancetype)sharedInstance;
@property (nonatomic, copy) void (^safariViewDismissedCallback)(void);
@property (nonatomic, strong) UIViewController *currentPresentedVC;
@property (nonatomic, strong) UIPanGestureRecognizer *panGestureRecognizer;
@property (nonatomic, strong) UIView *dragTrayView;
@property (nonatomic, assign) CGFloat initialY;
@property (nonatomic, assign) BOOL isObservingKeyboard;
- (void)handleDismiss:(UITapGestureRecognizer *)gesture;
- (void)dismissButtonTapped:(UIButton *)button;
- (void)handlePanGesture:(UIPanGestureRecognizer *)gesture;
- (void)handleDragTrayPanGesture:(UIPanGestureRecognizer *)gesture;
- (void)fallbackToSafariVC:(NSURL *)url topController:(UIViewController *)topController;
- (void)callUnityCallbackOnce;
- (void)expandCardToFullScreen;
- (void)collapseCardToOriginal;
- (UIView *)createDragTray:(CGFloat)cardWidth;
- (void)startKeyboardObserving;
- (void)stopKeyboardObserving;
- (void)keyboardWillShow:(NSNotification *)notification;
- (void)keyboardWillHide:(NSNotification *)notification;
@end

// WebView navigation delegate to handle loading states
@interface WebViewLoadDelegate : NSObject <WKNavigationDelegate>
- (instancetype)initWithWebView:(WKWebView*)webView loadingView:(UIView*)loadingView;
@end

// WebView UI delegate to disable context menus and text selection
@interface WebViewUIDelegate : NSObject <WKUIDelegate>
@end

@implementation WebViewLoadDelegate {
    WKWebView* _webView;
    UIView* _loadingView;
    NSTimer* _timeoutTimer;
    BOOL _hasStartedRendering;
}

- (instancetype)initWithWebView:(WKWebView*)webView loadingView:(UIView*)loadingView {
    self = [super init];
    if (self) {
        _webView = webView;
        _loadingView = loadingView;
        _hasStartedRendering = NO;
        
        // Create a fallback timer to handle cases where navigation events aren't fired
        _timeoutTimer = [NSTimer scheduledTimerWithTimeInterval:0.3  // Reduced to 0.3 seconds
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
    
    if (_webView.hidden) {
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
            
            // Set the WebView background color before showing
            self->_webView.backgroundColor = backgroundColor;
            self->_webView.scrollView.backgroundColor = backgroundColor;
            self->_webView.scrollView.opaque = YES;
            
            // Force background color one last time
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
            
            // First fade out the loading view
            [UIView animateWithDuration:0.2 animations:^{
                self->_loadingView.alpha = 0.0;
            } completion:^(BOOL finished) {
                // Make absolutely sure the page is ready before showing
                [self->_webView evaluateJavaScript:@"document.body !== null && window.getComputedStyle(document.body).display !== 'none'" completionHandler:^(id result, NSError *error) {
                    if ([result boolValue]) {
                self->_webView.hidden = NO;
                [self->_loadingView removeFromSuperview];
                    } else {
                        // If somehow not ready, restore loading view and try again
                        self->_loadingView.alpha = 1.0;
                        dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.1 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
                            [self showWebViewAndRemoveLoading];
                        });
                    }
                }];
            }];
        }];
    }
}

- (void)webView:(WKWebView *)webView didFinishNavigation:(WKNavigation *)navigation {
    // Don't show the webview immediately after navigation finishes
    // Wait for the first content to be drawn
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
                            // Wait a tiny bit more to ensure styles are applied
                            dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.1 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
    [self showWebViewAndRemoveLoading];
                            });
                        }];
                    } else {
                        dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.1 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
                            [self showWebViewAndRemoveLoading];
                        });
                    }
                } else {
                    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.1 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
                        [self showWebViewAndRemoveLoading];
                    });
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
    // Keep loading view visible
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

- (void)safariViewControllerDidFinish:(SFSafariViewController *)controller {
    _isCardCurrentlyPresented = NO; // Reset the presentation flag
    [self callUnityCallbackOnce];
}

- (void)handleDismiss:(UITapGestureRecognizer *)gesture {
    if (self.currentPresentedVC) {
        [self.currentPresentedVC dismissViewControllerAnimated:YES completion:^{
            _isCardCurrentlyPresented = NO; // Reset the presentation flag
            [self callUnityCallbackOnce];
        }];
    }
}

- (void)dismissButtonTapped:(UIButton *)button {
    if (self.currentPresentedVC) {
        [self.currentPresentedVC dismissViewControllerAnimated:YES completion:^{
            _isCardCurrentlyPresented = NO; // Reset the presentation flag
            [self callUnityCallbackOnce];
        }];
    }
}

- (void)handlePanGesture:(UIPanGestureRecognizer *)gesture {
    if (!self.currentPresentedVC) return;
    
    UIView *view = self.currentPresentedVC.view;
    CGFloat height = view.frame.size.height;
    CGPoint translation = [gesture translationInView:view.superview];
    
    // For iPad, allow both directions since card is centered
    // For iPhone, determine allowed swipe direction based on card position
    BOOL isNearTop = _cardVerticalPosition < 0.1;
    
    // iPad gets more flexible gesture handling
    BOOL allowUpward = isRunningOniPad() || isNearTop;
    BOOL allowDownward = isRunningOniPad() || NO; // iPhone: only allow downward for very specific cases
    
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
            } else if (!isRunningOniPad()) {
                // iPhone: DISABLED for bottom/middle positioned cards
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
                // iPad: allow dismissal in either direction
                if ((velocity.y < -300 || currentY < (self.initialY - dismissThreshold)) && allowUpward) {
                    // Dismiss upward
                    shouldDismiss = YES;
                    finalY = -height;
                } else if ((velocity.y > 300 || currentY > (self.initialY + dismissThreshold)) && allowDownward) {
                    // Dismiss downward
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
                        _isCardCurrentlyPresented = NO; // Reset the presentation flag
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

// For UIGestureRecognizerDelegate
- (BOOL)gestureRecognizer:(UIGestureRecognizer *)gestureRecognizer shouldRecognizeSimultaneouslyWithGestureRecognizer:(UIGestureRecognizer *)otherGestureRecognizer {
    // Allow drag tray gesture to work alongside webview scrolling
    // but prevent the main pan gesture from conflicting with the drag tray
    if ([gestureRecognizer.view isEqual:self.dragTrayView] || [otherGestureRecognizer.view isEqual:self.dragTrayView]) {
        // Drag tray gesture should work independently
        return NO;
    }
    // Allow other gestures to work simultaneously
    return YES;
}

- (BOOL)gestureRecognizerShouldBegin:(UIGestureRecognizer *)gestureRecognizer {
    // Only allow drag tray gesture to begin if it's actually on the drag tray
    if ([gestureRecognizer.view isEqual:self.dragTrayView]) {
        return YES;
    }
    return YES;
}

- (void)fallbackToSafariVC:(NSURL *)url topController:(UIViewController *)topController {
    SFSafariViewController* safariViewController = [[SFSafariViewController alloc] initWithURL:url];
    
    // Use automatic style which works better with Safari View Controller
    safariViewController.modalPresentationStyle = UIModalPresentationOverFullScreen;
    
    // Pre-configure the view before presentation to avoid slide animation
    safariViewController.view.backgroundColor = [UIColor clearColor];
    
    // Apply an Apple Pay style frame to the view before presentation
    CGRect screenBounds = [UIScreen mainScreen].bounds;
    CGFloat width, height, x, finalY;
    
    // Handle iPad with iPhone-like aspect ratio and centering
    if (isRunningOniPad()) {
        CGSize cardSize = calculateiPadCardSize(screenBounds);
        width = cardSize.width;
        height = cardSize.height;
        
        // Center the card on iPad
        x = (screenBounds.size.width - width) / 2;
        finalY = (screenBounds.size.height - height) / 2;
        

    } else {
        // iPhone/standard behavior
        width = screenBounds.size.width * _cardWidthRatio;  // Configurable width
        height = screenBounds.size.height * _cardHeightRatio; // Configurable height
        x = (screenBounds.size.width - width) / 2; // Center horizontally
        
        // Calculate vertical position based on _cardVerticalPosition
        // 0.0 = top of screen, 1.0 = bottom of screen, 0.5 = middle
        finalY = screenBounds.size.height * _cardVerticalPosition - height;
        // Ensure the card doesn't go above the top of the screen
        if (finalY < 0) finalY = 0;
        

    }
    
    // Start position (off-screen)
    CGFloat y = screenBounds.size.height; // Start off-screen at the bottom
    
    safariViewController.view.frame = CGRectMake(x, y, width, height);
    
    // Make the Safari view non-fullscreen by setting its frame before and after presentation
    [topController presentViewController:safariViewController animated:NO completion:^{
        // Immediately after presentation, animate to final position
        
        [UIView animateWithDuration:0.25 animations:^{
            safariViewController.view.frame = CGRectMake(x, finalY, width, height);
            safariViewController.view.superview.backgroundColor = [UIColor colorWithWhite:0.0 alpha:0.4];
        } completion:^(BOOL finished) {
            // Determine which corners to round based on position
            UIRectCorner cornersToRound;
            if (_cardVerticalPosition < 0.1) {
                // Near top: round bottom corners
                cornersToRound = UIRectCornerBottomLeft | UIRectCornerBottomRight;
            } else if (_cardVerticalPosition > 0.9) {
                // Near bottom: round top corners
                cornersToRound = UIRectCornerTopLeft | UIRectCornerTopRight;
            } else {
                // In middle: round all corners
                cornersToRound = UIRectCornerAllCorners;
            }
            
            // For iPad, always round all corners for centered appearance
            if (isRunningOniPad()) {
                cornersToRound = UIRectCornerAllCorners;
            }
            
            // Round the selected corners
            UIBezierPath *maskPath = [UIBezierPath bezierPathWithRoundedRect:safariViewController.view.bounds
                                                          byRoundingCorners:cornersToRound
                                                                cornerRadii:CGSizeMake(12.0, 12.0)];
            
            CAShapeLayer *maskLayer = [[CAShapeLayer alloc] init];
            maskLayer.frame = safariViewController.view.bounds;
            maskLayer.path = maskPath.CGPath;
            safariViewController.view.layer.mask = maskLayer;
            
            // Add a handle indicator based on position (skip for iPad as it's always centered)
            if (!isRunningOniPad() && (_cardVerticalPosition <= 0.1 || _cardVerticalPosition >= 0.9)) {
                // Add handle for top or bottom positions on iPhone
                UIView *handleView = [[UIView alloc] init];
                handleView.backgroundColor = [UIColor colorWithWhite:0.8 alpha:1.0];
                handleView.layer.cornerRadius = 2.5;
                
                if (_cardVerticalPosition >= 0.9) {
                    // Bottom position - handle at top
                    handleView.frame = CGRectMake(width/2 - 20, 6, 40, 5);
                } else {
                    // Top position - handle at bottom
                    handleView.frame = CGRectMake(width/2 - 20, height - 11, 40, 5);
                }
                
                [safariViewController.view addSubview:handleView];
            }
            
            // Add drag tray at the top of the Safari card for drag-to-dismiss functionality
            UIView *dragTray = [[StashPayCardSafariDelegate sharedInstance] createDragTray:width];
            [safariViewController.view addSubview:dragTray];
            [StashPayCardSafariDelegate sharedInstance].dragTrayView = dragTray; // Store reference
            
            // Note: Expand button is now integrated into the drag tray, no separate creation needed
        }];
    }];
    
    // Set the delegate
    safariViewController.delegate = [StashPayCardSafariDelegate sharedInstance];
    
    // Store reference for dismissal
    [StashPayCardSafariDelegate sharedInstance].currentPresentedVC = safariViewController;
    
    // Start observing keyboard notifications for auto-expand
    [[StashPayCardSafariDelegate sharedInstance] startKeyboardObserving];
    
    // Set the callback to be triggered when Safari View is dismissed
    [StashPayCardSafariDelegate sharedInstance].safariViewDismissedCallback = ^{
        // Stop keyboard observing when dismissing
        [[StashPayCardSafariDelegate sharedInstance] stopKeyboardObserving];
        
        if (_safariViewDismissedCallback != NULL) {
            _safariViewDismissedCallback();
        }
    };
}

// Helper function to determine if we should use full-screen Safari
BOOL shouldUseFullScreenSafari() {
    // Consider it full-screen if height is close to 1.0 and width is 1.0
    BOOL isFullScreen = (_cardHeightRatio >= 0.95 && _cardWidthRatio >= 0.95);
    return isFullScreen;
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

// Helper function to calculate iPhone-like card dimensions for iPad
CGSize calculateiPadCardSize(CGRect screenBounds) {
    // Safety checks for valid screen bounds
    if (screenBounds.size.width <= 0 || screenBounds.size.height <= 0) {
        return CGSizeMake(400, 600); // Fallback iPhone-like size
    }
    
    // Define iPhone-like aspect ratio (using iPhone 14 as reference: 390x844)
    CGFloat iPhoneWidth = 390.0;
    CGFloat iPhoneHeight = 844.0;
    CGFloat iPhoneAspectRatio = iPhoneWidth / iPhoneHeight;
    
    // Safety check for aspect ratio
    if (iPhoneAspectRatio <= 0) {
        return CGSizeMake(400, 600);
    }
    
    // Scale to fit nicely on iPad (about 70% of iPad screen width)
    CGFloat maxCardWidth = screenBounds.size.width * 0.7;
    CGFloat maxCardHeight = screenBounds.size.height * 0.8;
    
    // Additional safety checks
    if (maxCardWidth <= 0 || maxCardHeight <= 0) {
        return CGSizeMake(400, 600);
    }
    
    CGFloat cardWidth, cardHeight;
    
    // Calculate dimensions maintaining iPhone aspect ratio
    if (maxCardWidth / iPhoneAspectRatio <= maxCardHeight) {
        // Width-constrained
        cardWidth = maxCardWidth;
        cardHeight = cardWidth / iPhoneAspectRatio;
    } else {
        // Height-constrained
        cardHeight = maxCardHeight;
        cardWidth = cardHeight * iPhoneAspectRatio;
    }
    
    // Final safety checks for reasonable sizes
    if (cardWidth < 100 || cardHeight < 100 || cardWidth > screenBounds.size.width || cardHeight > screenBounds.size.height) {
        return CGSizeMake(400, 600);
    }
    
    return CGSizeMake(cardWidth, cardHeight);
}

// Enhanced fallback method that handles both custom positioning and full-screen Safari
- (void)presentSafariViewController:(NSURL *)url topController:(UIViewController *)topController {
    SFSafariViewController* safariViewController = [[SFSafariViewController alloc] initWithURL:url];
    
    if (shouldUseFullScreenSafari()) {
        // Full-screen native Safari experience
        safariViewController.modalPresentationStyle = UIModalPresentationFullScreen;
        
        // Set the delegate
        safariViewController.delegate = [StashPayCardSafariDelegate sharedInstance];
        
        // Present with animation for full-screen experience
        [topController presentViewController:safariViewController animated:YES completion:^{
        }];
        
        // Store reference for dismissal
        [StashPayCardSafariDelegate sharedInstance].currentPresentedVC = safariViewController;
        
        // Start observing keyboard notifications for auto-expand (even for full-screen)
        [[StashPayCardSafariDelegate sharedInstance] startKeyboardObserving];
        
        // Set the callback
        [StashPayCardSafariDelegate sharedInstance].safariViewDismissedCallback = ^{
            // Stop keyboard observing when dismissing
            [[StashPayCardSafariDelegate sharedInstance] stopKeyboardObserving];
            
            if (_safariViewDismissedCallback != NULL) {
                _safariViewDismissedCallback();
            }
        };
        
        return;
    }
    
    // Custom positioned Safari (existing logic)
    [self fallbackToSafariVC:url topController:topController];
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
    
    // Calculate full screen frame - fill entire screen edge-to-edge when expanded
    CGRect fullScreenFrame = screenBounds; // Fill entire screen
    
    // Calculate safe area dimensions for positioning UI elements
    // CGFloat safeWidth = screenBounds.size.width - safeAreaInsets.left - safeAreaInsets.right; // Unused variable removed
    CGFloat safeTop = safeAreaInsets.top;
    

    
    
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
            
            // Add new constraints that respect safe area when expanded
            if (@available(iOS 11.0, *)) {
                [NSLayoutConstraint activateConstraints:@[
                    [webView.leadingAnchor constraintEqualToAnchor:cardView.safeAreaLayoutGuide.leadingAnchor],
                    [webView.trailingAnchor constraintEqualToAnchor:cardView.safeAreaLayoutGuide.trailingAnchor],
                    [webView.topAnchor constraintEqualToAnchor:cardView.safeAreaLayoutGuide.topAnchor],
                    [webView.bottomAnchor constraintEqualToAnchor:cardView.safeAreaLayoutGuide.bottomAnchor]
                ]];
            } else {
                // Fallback for iOS < 11: use manual safe area calculation
                [NSLayoutConstraint activateConstraints:@[
                    [webView.leadingAnchor constraintEqualToAnchor:cardView.leadingAnchor constant:safeAreaInsets.left],
                    [webView.trailingAnchor constraintEqualToAnchor:cardView.trailingAnchor constant:-safeAreaInsets.right],
                    [webView.topAnchor constraintEqualToAnchor:cardView.topAnchor constant:safeAreaInsets.top],
                    [webView.bottomAnchor constraintEqualToAnchor:cardView.bottomAnchor constant:-safeAreaInsets.bottom]
                ]];
            }
            

            break;
        }
    }
    
    // Update drag tray size for full screen (full width but positioned considering safe area)
    UIView *dragTray = [cardView viewWithTag:8888];
    if (dragTray) {
        dragTray.frame = CGRectMake(0, safeTop, fullScreenFrame.size.width, 44);
        
        // Ensure drag tray stays on top of WebView after constraint changes
        [cardView bringSubviewToFront:dragTray];
        
        // Update gradient layer frame to match new drag tray size
        CAGradientLayer *gradientLayer = (CAGradientLayer*)dragTray.layer.sublayers.firstObject;
        if (gradientLayer) {
            gradientLayer.frame = dragTray.bounds;
        }
        
        // Update handle position and color within the drag tray
        UIView *handle = dragTray.subviews.firstObject;
        if (handle) {
            handle.frame = CGRectMake(fullScreenFrame.size.width/2 - 20, 12, 40, 5);
            
            // Refresh handle color for overlay style
            BOOL isDarkMode = NO;
            if (@available(iOS 13.0, *)) {
                UIUserInterfaceStyle currentStyle = [UITraitCollection currentTraitCollection].userInterfaceStyle;
                isDarkMode = (currentStyle == UIUserInterfaceStyleDark);
            }
            handle.backgroundColor = isDarkMode ? [UIColor colorWithWhite:0.95 alpha:0.9] : [UIColor colorWithWhite:1.0 alpha:0.9];
        }
    }
    
    // Animate to full screen (edge-to-edge)
    [UIView animateWithDuration:0.2 animations:^{  // Reduced from 0.3
        cardView.frame = fullScreenFrame;
        cardView.superview.backgroundColor = [UIColor colorWithWhite:0.0 alpha:0.6]; // Semi-transparent instead of solid black
    } completion:^(BOOL finished) {
        // Remove corner radius mask for full screen (both iPhone and iPad)
        cardView.layer.mask = nil;
    }];
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
            
            // Restore original edge-to-edge constraints for collapsed state
            [NSLayoutConstraint activateConstraints:@[
                [webView.leadingAnchor constraintEqualToAnchor:cardView.leadingAnchor],
                [webView.trailingAnchor constraintEqualToAnchor:cardView.trailingAnchor],
                [webView.topAnchor constraintEqualToAnchor:cardView.topAnchor],
                [webView.bottomAnchor constraintEqualToAnchor:cardView.bottomAnchor]
            ]];
            

            break;
        }
    }
    
    // Update drag tray size for original card size
    UIView *dragTray = [cardView viewWithTag:8888];
    if (dragTray) {
        dragTray.frame = CGRectMake(0, 0, width, 44);
        
        // Ensure drag tray stays on top of WebView after constraint changes
        [cardView bringSubviewToFront:dragTray];
        
        // Update gradient layer frame to match new drag tray size
        CAGradientLayer *gradientLayer = (CAGradientLayer*)dragTray.layer.sublayers.firstObject;
        if (gradientLayer) {
            gradientLayer.frame = dragTray.bounds;
        }
        
        // Update handle position and color within the drag tray
        UIView *handle = dragTray.subviews.firstObject;
        if (handle) {
            handle.frame = CGRectMake(width/2 - 20, 12, 40, 5);
            
            // Refresh handle color for overlay style
            BOOL isDarkMode = NO;
            if (@available(iOS 13.0, *)) {
                UIUserInterfaceStyle currentStyle = [UITraitCollection currentTraitCollection].userInterfaceStyle;
                isDarkMode = (currentStyle == UIUserInterfaceStyleDark);
            }
            handle.backgroundColor = isDarkMode ? [UIColor colorWithWhite:0.95 alpha:0.9] : [UIColor colorWithWhite:1.0 alpha:0.9];
        }
    }
    
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
    // Create a drag tray area at the top of the card for drag-to-expand/collapse/dismiss functionality
    // This now overlays on top of the WebView instead of taking up space
    UIView *dragTrayView = [[UIView alloc] init];
    dragTrayView.frame = CGRectMake(0, 0, cardWidth, 44); // 44pt tall touch area
    dragTrayView.tag = 8888; // Tag to find it later
    
    // Check if we're in dark mode to set appropriate colors
    BOOL isDarkMode = NO;
    if (@available(iOS 13.0, *)) {
        UIUserInterfaceStyle currentStyle = [UITraitCollection currentTraitCollection].userInterfaceStyle;
        isDarkMode = (currentStyle == UIUserInterfaceStyleDark);
    }
    
    // Add a subtle background gradient for better visibility over web content
    CAGradientLayer *gradientLayer = [CAGradientLayer layer];
    gradientLayer.frame = dragTrayView.bounds;
    
    if (isDarkMode) {
        // Dark mode: subtle dark gradient
        gradientLayer.colors = @[
            (id)[UIColor colorWithWhite:0.0 alpha:0.7].CGColor,  // Top: more opaque
            (id)[UIColor colorWithWhite:0.0 alpha:0.3].CGColor,  // Middle
            (id)[UIColor colorWithWhite:0.0 alpha:0.0].CGColor   // Bottom: transparent
        ];
    } else {
        // Light mode: subtle light gradient
        gradientLayer.colors = @[
            (id)[UIColor colorWithWhite:0.0 alpha:0.5].CGColor,  // Top: semi-opaque dark
            (id)[UIColor colorWithWhite:0.0 alpha:0.2].CGColor,  // Middle
            (id)[UIColor colorWithWhite:0.0 alpha:0.0].CGColor   // Bottom: transparent
        ];
    }
    
    gradientLayer.locations = @[@0.0, @0.6, @1.0];
    [dragTrayView.layer addSublayer:gradientLayer];
    
    // Add visual handle indicator with enhanced visibility over web content
    UIView *handleView = [[UIView alloc] init];
    if (isDarkMode) {
        // Dark mode: bright handle for visibility
        handleView.backgroundColor = [UIColor colorWithWhite:0.95 alpha:0.9];
    } else {
        // Light mode: white handle with shadow for visibility over any content
        handleView.backgroundColor = [UIColor colorWithWhite:1.0 alpha:0.9];
    }
    handleView.layer.cornerRadius = 2.5;
    handleView.frame = CGRectMake(cardWidth/2 - 20, 12, 40, 5);
    
    // Add subtle shadow to handle for better visibility
    handleView.layer.shadowColor = [UIColor blackColor].CGColor;
    handleView.layer.shadowOffset = CGSizeMake(0, 1);
    handleView.layer.shadowOpacity = 0.3;
    handleView.layer.shadowRadius = 2.0;
    
    [dragTrayView addSubview:handleView];
    
    // Add pan gesture recognizer for drag-to-expand/collapse/dismiss functionality
    UIPanGestureRecognizer *dragTrayPanGesture = [[UIPanGestureRecognizer alloc] initWithTarget:self action:@selector(handleDragTrayPanGesture:)];
    dragTrayPanGesture.delegate = self;
    [dragTrayView addGestureRecognizer:dragTrayPanGesture];
    
    return dragTrayView;
}

- (void)handleDragTrayPanGesture:(UIPanGestureRecognizer *)gesture {
    if (!self.currentPresentedVC) return;
    
    UIView *cardView = self.currentPresentedVC.view;
    CGFloat height = cardView.frame.size.height;
    CGPoint translation = [gesture translationInView:cardView.superview];
    CGPoint velocity = [gesture velocityInView:cardView.superview];
    
    switch (gesture.state) {
        case UIGestureRecognizerStateBegan:
            self.initialY = cardView.frame.origin.y;
            break;
            
        case UIGestureRecognizerStateChanged: {
            // Allow both upward and downward movement for different actions
            CGFloat newY = self.initialY + translation.y;
            
            // Limit movement to prevent going too far off screen
            CGFloat screenHeight = cardView.superview.bounds.size.height;
            CGFloat minY = -height * 0.3; // Can go up 30% off screen for expand gesture
            CGFloat maxY = screenHeight; // Can go down to bottom for dismiss
            
            newY = MAX(minY, MIN(maxY, newY));
            cardView.frame = CGRectMake(cardView.frame.origin.x, newY, cardView.frame.size.width, height);
            
            // Adjust background opacity based on drag distance and direction
            CGFloat currentTravel = newY - self.initialY;
            CGFloat maxTravel = height * 0.5;
            CGFloat ratio;
            
            if (currentTravel > 0) {
                // Dragging down - reduce opacity for dismiss hint
                ratio = 1.0 - (currentTravel / maxTravel);
                if (ratio < 0.2) ratio = 0.2; // Minimum opacity
            } else {
                // Dragging up - slightly increase opacity for expand hint
                ratio = 1.0 + (abs(currentTravel) / maxTravel) * 0.2;
                if (ratio > 1.0) ratio = 1.0; // Maximum opacity
            }
            
            CGFloat baseOpacity = _isCardExpanded ? 0.6 : 0.4;
            cardView.superview.backgroundColor = [UIColor colorWithWhite:0.0 alpha:baseOpacity * ratio];
            break;
        }
            
        case UIGestureRecognizerStateEnded:
        case UIGestureRecognizerStateCancelled: {
            // CGFloat currentY = cardView.frame.origin.y; // Unused variable removed
            CGFloat expandThreshold = height * 0.15; // 15% of card height upward
            CGFloat dismissThreshold = height * 0.3; // 30% of card height downward
            CGFloat expandVelocityThreshold = -600; // Upward velocity threshold
            CGFloat dismissVelocityThreshold = 800; // Downward velocity threshold
            
            // Determine action based on distance and velocity
            BOOL shouldExpand = NO;
            BOOL shouldCollapse = NO;
            BOOL shouldDismiss = NO;
            
            if (translation.y < -expandThreshold || velocity.y < expandVelocityThreshold) {
                // Dragged up sufficiently or fast upward velocity
                if (!_isCardExpanded) {
                    shouldExpand = YES;
                } else {
                    // Already expanded, treat as small adjustment - return to position
                }
            } else if (translation.y > dismissThreshold || velocity.y > dismissVelocityThreshold) {
                // Dragged down sufficiently or fast downward velocity
                if (_isCardExpanded) {
                    // If expanded, first collapse instead of dismissing
                    shouldCollapse = YES;
                } else {
                    // If not expanded, dismiss
                    shouldDismiss = YES;
                }
            }
            
            if (shouldExpand) {
                // Expand to full screen
                [self expandCardToFullScreen];
            } else if (shouldCollapse) {
                // Collapse to original size
                [self collapseCardToOriginal];
            } else if (shouldDismiss) {
                // Dismiss the card
                CGFloat animationDuration = 0.25;
                if (velocity.y > 1000) {
                    animationDuration = 0.15; // Faster animation for quick swipes
                }
                
                CGFloat finalY = cardView.superview.bounds.size.height;
                [UIView animateWithDuration:animationDuration 
                                      delay:0 
                     usingSpringWithDamping:0.8 
                      initialSpringVelocity:velocity.y / 1000.0 
                                    options:UIViewAnimationOptionCurveEaseOut 
                                 animations:^{
                    cardView.frame = CGRectMake(cardView.frame.origin.x, finalY, cardView.frame.size.width, height);
                    cardView.superview.backgroundColor = [UIColor colorWithWhite:0.0 alpha:0.0];
                } completion:^(BOOL finished) {
                    [self.currentPresentedVC dismissViewControllerAnimated:NO completion:^{
                        _isCardCurrentlyPresented = NO; // Reset the presentation flag
                        [self callUnityCallbackOnce];
                    }];
                }];
            } else {
                // Return to original position with spring animation
                [UIView animateWithDuration:0.3 
                                      delay:0 
                     usingSpringWithDamping:0.7 
                      initialSpringVelocity:0 
                                    options:UIViewAnimationOptionCurveEaseOut 
                                 animations:^{
                    cardView.frame = CGRectMake(cardView.frame.origin.x, self.initialY, cardView.frame.size.width, height);
                    CGFloat baseOpacity = _isCardExpanded ? 0.6 : 0.4;
                    cardView.superview.backgroundColor = [UIColor colorWithWhite:0.0 alpha:baseOpacity];
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
    if ([message.name isEqualToString:@"windowClose"]) {
        if (self.currentPresentedVC) {
            [self.currentPresentedVC dismissViewControllerAnimated:YES completion:^{
                _isCardCurrentlyPresented = NO; // Reset the presentation flag
                [self callUnityCallbackOnce];
            }];
        }
    }
    else if ([message.name isEqualToString:@"stashPaymentSuccess"]) {
        NSLog(@"Payment success received from JavaScript");
        
        // Prevent multiple payment success handling
        if (_paymentSuccessHandled) {
            NSLog(@"Payment success already handled, ignoring duplicate");
            return;
        }
        _paymentSuccessHandled = YES;
        
        // Prevent multiple callback executions for this session
        if (_paymentSuccessCallbackCalled) {
            NSLog(@"Payment success callback already called for this session, ignoring");
            return;
        }
        _paymentSuccessCallbackCalled = YES;
        
        // Call the payment success callback if available
        if (_paymentSuccessCallback != NULL) {
            dispatch_async(dispatch_get_main_queue(), ^{
                _paymentSuccessCallback();
            });
        } else {
            NSLog(@"Payment success callback is NULL, cannot call Unity");
        }
        
        // Automatically close the Stash Pay Card without calling the regular dismissal callback
        if (self.currentPresentedVC) {
            [self.currentPresentedVC dismissViewControllerAnimated:YES completion:^{
                _isCardCurrentlyPresented = NO; // Reset the presentation flag
                _callbackWasCalled = YES; // Mark callback as called to prevent dismissal callback
                // Don't call callUnityCallbackOnce here since we already handled the success callback
            }];
        }
    }
    else if ([message.name isEqualToString:@"stashPaymentFailure"]) {
        NSLog(@"Payment failure received from JavaScript");
        
        // Call the payment failure callback if available
        if (_paymentFailureCallback != NULL) {
            dispatch_async(dispatch_get_main_queue(), ^{
                _paymentFailureCallback();
            });
        } else {
            NSLog(@"Payment failure callback is NULL, cannot call Unity");
        }
        
        // Automatically close the Stash Pay Card without calling the regular dismissal callback
        if (self.currentPresentedVC) {
            [self.currentPresentedVC dismissViewControllerAnimated:YES completion:^{
                _isCardCurrentlyPresented = NO; // Reset the presentation flag
                _callbackWasCalled = YES; // Mark callback as called to prevent dismissal callback
                // Don't call callUnityCallbackOnce here since we already handled the failure callback
            }];
        }
    }
}

@end

// Hard-coded SVG content - white logo for dark mode, black for light mode
static NSString* const kLogoSVGDark = @"<svg width=\"346\" height=\"55\" viewBox=\"0 0 346 55\" fill=\"none\" xmlns=\"http://www.w3.org/2000/svg\"><g clip-path=\"url(#clip0_290_729)\"><path d=\"M18 0C8.06 0 0 8.06 0 18C0 18.34 0 18.68 0.03 19.01C0.61 29.05 10.96 35.85 21.04 33.26L36.83 29.21C38.98 28.66 41.2 29.91 41.5 32.03C41.8 34.17 40.09 36 37.93 36H0V54H36C45.94 54 54 45.94 54 36C54 35.66 54 35.32 53.97 34.99C53.39 24.95 43.04 18.15 32.96 20.74L17.17 24.79C15.02 25.34 12.8 24.09 12.5 21.97C12.2 19.83 13.91 18 16.07 18H52.16L49.31 0H18Z\" fill=\"white\"/><path d=\"M327.99 0L328 22H316V0H298V54H316V26H328V54H346L345.99 0H327.99Z\" fill=\"white\"/><path d=\"M172.03 54.25C172.03 49.06 170.49 41.43 166 34.86C168.73 28.45 170.42 20.61 170.87 11.6C171.02 8.60004 169.92 5.63004 167.85 3.46004C165.78 1.29004 162.88 0.0400391 159.88 0.0400391H51.05C51.05 0.0400391 53.58 6.10004 55.39 11.25C56.28 13.79 56.98 16.09 57.51 18.02H82.77L75.41 54.03H98.01C98.01 54.03 98.01 48.99 97.47 43.5C96.62 34.88 93.64 24.85 91.33 18.03H152.09C151.8 19.76 151.42 21.64 150.91 23.51C148.38 22.68 145.57 22.1 142.46 21.85C133.49 21.12 126.37 22.93 121.86 27.08C118.75 29.95 117.03 33.84 117.03 38.03C117.03 43.38 119.87 48.07 124.82 50.91C128.68 53.12 133.66 54.2 140.06 54.2C145.02 54.2 149.53 52.91 153.46 50.48C153.89 52.2 154 53.55 154.03 54.03H172.03V54.24H172.04L172.03 54.25ZM149.17 39.13C148.4 41.19 143.76 41.35 138.8 39.5C133.84 37.64 130.45 34.47 131.22 32.41C131.99 30.35 136.63 30.19 141.59 32.04C146.55 33.9 149.94 37.07 149.17 39.13Z\" fill=\"white\"/><path d=\"M215.6 4.59C219.6 0.99 225.32 0.84 229.46 3.81C230.26 4.39 231.01 5.07 231.67 5.88C237.95 13.59 246.99 23.09 256.68 24.39C266.07 25.66 294 26 294 26V0H184.47C178.66 0 173.96 5.15 173.96 11.13V12.39C173.96 18.01 178.15 23.54 183.56 24.28C199.39 26.46 230.6 28.12 246.26 29.6C247.47 29.71 248.65 30.1 249.77 30.64C251.61 31.54 253.26 32.96 254.47 34.92C257.25 39.43 256.63 44.99 252.99 48.79C248.98 52.97 242.86 53.31 238.5 50.18C237.7 49.6 236.95 48.92 236.29 48.11C230.01 40.4 220.97 30.89 211.28 29.59C203.26 28.51 176.09 28.11 168.3 28.01C173.46 32.29 179.21 38.78 182.62 48.07C183.29 49.91 183.85 51.91 184.31 53.99H283.49C289.3 53.99 294 48.84 294 42.86V41.61C294 35.99 289.81 30.4 284.4 29.72C268.84 27.76 237.36 25.98 221.7 24.49C220.49 24.37 219.31 23.99 218.19 23.45C216.58 22.66 215.11 21.48 213.96 19.88C210.5 15.08 211.22 8.52 215.6 4.59Z\" fill=\"white\"/></g><defs><clipPath id=\"clip0_290_729\"><rect width=\"346\" height=\"54.25\" fill=\"white\"/></clipPath></defs></svg>";

static NSString* const kLogoSVGLight = @"<svg width=\"346\" height=\"55\" viewBox=\"0 0 346 55\" fill=\"none\" xmlns=\"http://www.w3.org/2000/svg\"><g clip-path=\"url(#clip0_290_729)\"><path d=\"M18 0C8.06 0 0 8.06 0 18C0 18.34 0 18.68 0.03 19.01C0.61 29.05 10.96 35.85 21.04 33.26L36.83 29.21C38.98 28.66 41.2 29.91 41.5 32.03C41.8 34.17 40.09 36 37.93 36H0V54H36C45.94 54 54 45.94 54 36C54 35.66 54 35.32 53.97 34.99C53.39 24.95 43.04 18.15 32.96 20.74L17.17 24.79C15.02 25.34 12.8 24.09 12.5 21.97C12.2 19.83 13.91 18 16.07 18H52.16L49.31 0H18Z\" fill=\"black\"/><path d=\"M327.99 0L328 22H316V0H298V54H316V26H328V54H346L345.99 0H327.99Z\" fill=\"black\"/><path d=\"M172.03 54.25C172.03 49.06 170.49 41.43 166 34.86C168.73 28.45 170.42 20.61 170.87 11.6C171.02 8.60004 169.92 5.63004 167.85 3.46004C165.78 1.29004 162.88 0.0400391 159.88 0.0400391H51.05C51.05 0.0400391 53.58 6.10004 55.39 11.25C56.28 13.79 56.98 16.09 57.51 18.02H82.77L75.41 54.03H98.01C98.01 54.03 98.01 48.99 97.47 43.5C96.62 34.88 93.64 24.85 91.33 18.03H152.09C151.8 19.76 151.42 21.64 150.91 23.51C148.38 22.68 145.57 22.1 142.46 21.85C133.49 21.12 126.37 22.93 121.86 27.08C118.75 29.95 117.03 33.84 117.03 38.03C117.03 43.38 119.87 48.07 124.82 50.91C128.68 53.12 133.66 54.2 140.06 54.2C145.02 54.2 149.53 52.91 153.46 50.48C153.89 52.2 154 53.55 154.03 54.03H172.03V54.24H172.04L172.03 54.25ZM149.17 39.13C148.4 41.19 143.76 41.35 138.8 39.5C133.84 37.64 130.45 34.47 131.22 32.41C131.99 30.35 136.63 30.19 141.59 32.04C146.55 33.9 149.94 37.07 149.17 39.13Z\" fill=\"black\"/><path d=\"M215.6 4.59C219.6 0.99 225.32 0.84 229.46 3.81C230.26 4.39 231.01 5.07 231.67 5.88C237.95 13.59 246.99 23.09 256.68 24.39C266.07 25.66 294 26 294 26V0H184.47C178.66 0 173.96 5.15 173.96 11.13V12.39C173.96 18.01 178.15 23.54 183.56 24.28C199.39 26.46 230.6 28.12 246.26 29.6C247.47 29.71 248.65 30.1 249.77 30.64C251.61 31.54 253.26 32.96 254.47 34.92C257.25 39.43 256.63 44.99 252.99 48.79C248.98 52.97 242.86 53.31 238.5 50.18C237.7 49.6 236.95 48.92 236.29 48.11C230.01 40.4 220.97 30.89 211.28 29.59C203.26 28.51 176.09 28.11 168.3 28.01C173.46 32.29 179.21 38.78 182.62 48.07C183.29 49.91 183.85 51.91 184.31 53.99H283.49C289.3 53.99 294 48.84 294 42.86V41.61C294 35.99 289.81 30.4 284.4 29.72C268.84 27.76 237.36 25.98 221.7 24.49C220.49 24.37 219.31 23.99 218.19 23.45C216.58 22.66 215.11 21.48 213.96 19.88C210.5 15.08 211.22 8.52 215.6 4.59Z\" fill=\"black\"/></g><defs><clipPath id=\"clip0_290_729\"><rect width=\"346\" height=\"54.25\" fill=\"white\"/></clipPath></defs></svg>";

// Helper function to create loading view with logo using WKWebView
UIView* CreateLoadingView(CGRect frame) {
    // Create container view
    UIView* loadingView = [[UIView alloc] initWithFrame:frame];
    
    // Check if we're in dark mode
    BOOL isDarkMode = NO;
    if (@available(iOS 13.0, *)) {
        UIUserInterfaceStyle currentStyle = [UITraitCollection currentTraitCollection].userInterfaceStyle;
        isDarkMode = (currentStyle == UIUserInterfaceStyleDark);
    }
    
    // Set the background color based on mode - use pure black in dark mode
    loadingView.backgroundColor = isDarkMode ? [UIColor blackColor] : [UIColor whiteColor];
    
    // Calculate the reduced size (70% of original) with padding for animation
    float originalWidth = 295.0;
    float originalHeight = 53.0;
    float scaleFactor = 0.7; // 70% of original size (30% reduction)
    float newWidth = originalWidth * scaleFactor;
    float newHeight = originalHeight * scaleFactor;
    
    // Add padding to accommodate the 105% scale animation (5% extra on each side = 10% total)
    float paddingFactor = 1.15; // 115% to safely accommodate 105% scale + some margin
    float containerWidth = newWidth * paddingFactor;
    float containerHeight = newHeight * paddingFactor;
    

    
    // Create a container for the logo with expanded size to accommodate animation
    UIView* logoContainer = [[UIView alloc] initWithFrame:CGRectMake(0, 0, containerWidth, containerHeight)];
    logoContainer.backgroundColor = loadingView.backgroundColor;
    logoContainer.translatesAutoresizingMaskIntoConstraints = NO;
    logoContainer.clipsToBounds = NO; // Allow content to extend beyond bounds for glow effects
    
    // Choose the appropriate SVG content based on dark/light mode
    NSString* svgContent = isDarkMode ? kLogoSVGDark : kLogoSVGLight;
    
    // Create a WKWebView to render the SVG
    WKWebViewConfiguration *config = [[WKWebViewConfiguration alloc] init];
    config.allowsInlineMediaPlayback = YES;
    
    WKWebView *webView = [[WKWebView alloc] initWithFrame:logoContainer.bounds configuration:config];
    webView.backgroundColor = loadingView.backgroundColor;
    webView.opaque = NO;
    webView.scrollView.scrollEnabled = NO;
    webView.scrollView.bounces = NO;
    
    // Enhanced HTML template with better overflow handling for animations
    NSString *htmlTemplate = @"<!DOCTYPE html><html><head><meta name='viewport' content='width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no'><style>body{margin:0;padding:0;background-color:%@;display:flex;justify-content:center;align-items:center;width:100%%;height:100%%;overflow:visible;}svg{width:85%%;height:auto;animation:logoAnimation 2s ease-in-out infinite;transform-origin:center center;}@keyframes logoAnimation{0%%{transform:scale(1);opacity:0.7;}50%%{transform:scale(1.05);opacity:1;}100%%{transform:scale(1);opacity:0.7;}}svg path{animation:pathGlow 3s ease-in-out infinite;filter:drop-shadow(0 0 8px rgba(255,255,255,0.3));}@keyframes pathGlow{0%%{filter:drop-shadow(0 0 8px rgba(255,255,255,0.2));}50%%{filter:drop-shadow(0 0 16px rgba(255,255,255,0.6));}100%%{filter:drop-shadow(0 0 8px rgba(255,255,255,0.2));}}@media (prefers-color-scheme: dark){svg path{filter:drop-shadow(0 0 8px rgba(255,255,255,0.4));}@keyframes pathGlow{0%%{filter:drop-shadow(0 0 8px rgba(255,255,255,0.3));}50%%{filter:drop-shadow(0 0 16px rgba(255,255,255,0.7));}100%%{filter:drop-shadow(0 0 8px rgba(255,255,255,0.3));}}}</style></head><body>%@</body></html>";
    
    NSString *backgroundColor = isDarkMode ? @"black" : @"white";
    NSString *fullHTML = [NSString stringWithFormat:htmlTemplate, backgroundColor, svgContent];
    
    [webView loadHTMLString:fullHTML baseURL:nil];
    
    [logoContainer addSubview:webView];
    
    // Set constraints for the webview to fill the logo container
    webView.translatesAutoresizingMaskIntoConstraints = NO;
    [NSLayoutConstraint activateConstraints:@[
        [webView.leadingAnchor constraintEqualToAnchor:logoContainer.leadingAnchor],
        [webView.trailingAnchor constraintEqualToAnchor:logoContainer.trailingAnchor],
        [webView.topAnchor constraintEqualToAnchor:logoContainer.topAnchor],
        [webView.bottomAnchor constraintEqualToAnchor:logoContainer.bottomAnchor]
    ]];
    
    // Add the logo container to the loading view
    [loadingView addSubview:logoContainer];
    
    // Center the logo container in the loading view with updated dimensions
    [NSLayoutConstraint activateConstraints:@[
        [logoContainer.centerXAnchor constraintEqualToAnchor:loadingView.centerXAnchor],
        [logoContainer.centerYAnchor constraintEqualToAnchor:loadingView.centerYAnchor],
        [logoContainer.widthAnchor constraintEqualToConstant:containerWidth],
        [logoContainer.heightAnchor constraintEqualToConstant:containerHeight]
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

    // Sets the card configuration - height ratio and vertical position
    // NOTE: On iPad, these settings are overridden to maintain iPhone-like aspect ratio centered on screen
    void _StashPayCardSetCardConfiguration(float heightRatio, float verticalPosition) {
        // Validate and clamp height ratio between 0.1 and 0.9
        _cardHeightRatio = heightRatio < 0.1 ? 0.1 : (heightRatio > 0.9 ? 0.9 : heightRatio);
        
        // Validate and clamp vertical position between 0.0 (top) and 1.0 (bottom)
        _cardVerticalPosition = verticalPosition < 0.0 ? 0.0 : (verticalPosition > 1.0 ? 1.0 : verticalPosition);
        
        // Store original values for expand/collapse functionality
        _originalCardHeightRatio = _cardHeightRatio;
        _originalCardVerticalPosition = _cardVerticalPosition;
        _originalCardWidthRatio = _cardWidthRatio; // Keep current width ratio
        _isCardExpanded = NO; // Reset expansion state
    }

    // Sets the card configuration with width support
    // NOTE: On iPad, these settings are overridden to maintain iPhone-like aspect ratio centered on screen
    void _StashPayCardSetCardConfigurationWithWidth(float heightRatio, float verticalPosition, float widthRatio) {
        // Validate and clamp height ratio between 0.1 and 1.0 (allow fullscreen)
        _cardHeightRatio = heightRatio < 0.1 ? 0.1 : (heightRatio > 1.0 ? 1.0 : heightRatio);
        
        // Validate and clamp vertical position between 0.0 (top) and 1.0 (bottom)
        _cardVerticalPosition = verticalPosition < 0.0 ? 0.0 : (verticalPosition > 1.0 ? 1.0 : verticalPosition);
        
        // Validate and clamp width ratio between 0.1 and 1.0
        _cardWidthRatio = widthRatio < 0.1 ? 0.1 : (widthRatio > 1.0 ? 1.0 : widthRatio);
        
        // Store original values for expand/collapse functionality
        _originalCardHeightRatio = _cardHeightRatio;
        _originalCardVerticalPosition = _cardVerticalPosition;
        _originalCardWidthRatio = _cardWidthRatio;
        _isCardExpanded = NO; // Reset expansion state
    }

    // Opens a URL in Safari View Controller with delegation
    void _StashPayCardOpenURLInSafariVC(const char* urlString) {
        if (urlString == NULL) {
            NSLog(@"Error: URL is null");
            return;
        }
        
        // Check if a card is already being presented
        if (_isCardCurrentlyPresented) {
            NSLog(@"Warning: Card is already being presented. Ignoring new request.");
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
            // Check if we should use full-screen Safari directly
            if (shouldUseFullScreenSafari()) {
                [[StashPayCardSafariDelegate sharedInstance] presentSafariViewController:url topController:topController];
                return;
            }
            
            // For non-full-screen modes, try to create a custom view controller with WKWebView
            UIViewController *containerVC = [[UIViewController alloc] init];
            containerVC.modalPresentationStyle = UIModalPresentationOverFullScreen;
            
            // Try to create the web view
            Class webViewClass = NSClassFromString(@"WKWebView");
            Class configClass = NSClassFromString(@"WKWebViewConfiguration");
            
            if (webViewClass && configClass) {
                // WebKit is available, use WKWebView with proper configuration
                WKWebViewConfiguration *config = [[WKWebViewConfiguration alloc] init];
                config.allowsInlineMediaPlayback = YES;
                config.allowsAirPlayForMediaPlayback = YES;
                config.allowsPictureInPictureMediaPlayback = YES;
                
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
                
                // JavaScript code to intercept window.close() calls
                NSString *windowCloseScript = @"(function() {"
                    "var originalClose = window.close;"
                    "window.close = function() {"
                        "window.webkit.messageHandlers.windowClose.postMessage('close');"
                        "originalClose.call(window);"
                    "};"
                "})();";
                
                WKUserScript *windowScript = [[WKUserScript alloc] initWithSource:windowCloseScript 
                                                              injectionTime:WKUserScriptInjectionTimeAtDocumentStart 
                                                           forMainFrameOnly:YES];
                [userContentController addUserScript:windowScript];
                
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
                "})();";
                
                WKUserScript *stashSDKInjection = [[WKUserScript alloc] initWithSource:stashSDKScript 
                                                                     injectionTime:WKUserScriptInjectionTimeAtDocumentStart 
                                                                  forMainFrameOnly:YES];
                [userContentController addUserScript:stashSDKInjection];
                
                // Add the script message handlers
                [userContentController addScriptMessageHandler:[StashPayCardSafariDelegate sharedInstance] name:@"windowClose"];
                [userContentController addScriptMessageHandler:[StashPayCardSafariDelegate sharedInstance] name:@"stashPaymentSuccess"];
                [userContentController addScriptMessageHandler:[StashPayCardSafariDelegate sharedInstance] name:@"stashPaymentFailure"];
                
                config.userContentController = userContentController;
                
                // Setup a web view controller with transparent background
                WKWebView *webView = [[WKWebView alloc] initWithFrame:CGRectZero configuration:config];
                webView.opaque = NO;
                webView.backgroundColor = [UIColor clearColor];
                webView.scrollView.backgroundColor = [UIColor clearColor];
                webView.hidden = YES;
                webView.translatesAutoresizingMaskIntoConstraints = NO;
                
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
                
                // Create loading view with logo
                UIView* loadingView = CreateLoadingView(CGRectZero);
                // Use the appropriate background color for the loading view
                if (@available(iOS 13.0, *)) {
                    UIUserInterfaceStyle currentStyle = [UITraitCollection currentTraitCollection].userInterfaceStyle;
                    loadingView.backgroundColor = (currentStyle == UIUserInterfaceStyleDark) ? [UIColor blackColor] : [UIColor systemBackgroundColor];
                } else {
                    loadingView.backgroundColor = [UIColor whiteColor];
                }
                loadingView.translatesAutoresizingMaskIntoConstraints = NO;
                
                [containerVC.view addSubview:loadingView];
                [containerVC.view addSubview:webView];
                
                // Set up constraints for the loading view (full container)
                [NSLayoutConstraint activateConstraints:@[
                    [loadingView.leadingAnchor constraintEqualToAnchor:containerVC.view.leadingAnchor],
                    [loadingView.trailingAnchor constraintEqualToAnchor:containerVC.view.trailingAnchor],
                    [loadingView.topAnchor constraintEqualToAnchor:containerVC.view.topAnchor],
                    [loadingView.bottomAnchor constraintEqualToAnchor:containerVC.view.bottomAnchor]
                ]];
            
                // Set up constraints for the web view (edge-to-edge filling the entire card)
                [NSLayoutConstraint activateConstraints:@[
                    [webView.leadingAnchor constraintEqualToAnchor:containerVC.view.leadingAnchor],
                    [webView.trailingAnchor constraintEqualToAnchor:containerVC.view.trailingAnchor],
                    [webView.topAnchor constraintEqualToAnchor:containerVC.view.topAnchor],
                    [webView.bottomAnchor constraintEqualToAnchor:containerVC.view.bottomAnchor]
                ]];
                
                // Add navigation delegate to detect when loading finishes
                WebViewLoadDelegate *delegate = [[WebViewLoadDelegate alloc] initWithWebView:webView loadingView:loadingView];
                webView.navigationDelegate = delegate;
                
                // Add UI delegate to disable context menus and text selection
                WebViewUIDelegate *uiDelegate = [[WebViewUIDelegate alloc] init];
                webView.UIDelegate = uiDelegate;
                
                // Store the delegates with the view controller to prevent deallocation
                objc_setAssociatedObject(containerVC, "webViewDelegate", delegate, OBJC_ASSOCIATION_RETAIN_NONATOMIC);
                objc_setAssociatedObject(containerVC, "webViewUIDelegate", uiDelegate, OBJC_ASSOCIATION_RETAIN_NONATOMIC);
                
                // Create an explicit URL request with aggressive caching
                NSMutableURLRequest *request = [NSMutableURLRequest requestWithURL:url 
                                                                    cachePolicy:NSURLRequestReturnCacheDataElseLoad
                                                                timeoutInterval:30.0];
                
                // Add standard headers
                [request setValue:@"Mozilla/5.0 (iPhone; CPU iPhone OS 15_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/15.0 Mobile/15E148 Safari/604.1" forHTTPHeaderField:@"User-Agent"];
                
                // Start loading the WebView immediately
                [webView loadRequest:request];
                
                // Calculate card dimensions
                CGRect screenBounds = [UIScreen mainScreen].bounds;
                CGFloat width, height, x, finalY;
                
                // Handle iPad with iPhone-like aspect ratio and centering
                if (isRunningOniPad()) {
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
                
                // Start position (off-screen)
                CGFloat y = screenBounds.size.height;
                
                // Present without animation and animate to position after
                [topController presentViewController:containerVC animated:NO completion:^{
                    // Set initial position off-screen
                    containerVC.view.frame = CGRectMake(x, y, width, height);
                    
                    // Set background overlay color immediately before animation
                    containerVC.view.superview.backgroundColor = [UIColor colorWithWhite:0.0 alpha:0.4];
                    
                    // Animate into position with faster animation
                    [UIView animateWithDuration:0.15 animations:^{
                        containerVC.view.frame = CGRectMake(x, finalY, width, height);
                        // Set the container view background color
                        if (@available(iOS 13.0, *)) {
                            UIUserInterfaceStyle currentStyle = [UITraitCollection currentTraitCollection].userInterfaceStyle;
                            containerVC.view.backgroundColor = (currentStyle == UIUserInterfaceStyleDark) ? [UIColor blackColor] : [UIColor systemBackgroundColor];
                        } else {
                            containerVC.view.backgroundColor = [UIColor whiteColor];
                        }
                    } completion:^(BOOL finished) {
                        // Determine which corners to round based on position
                        UIRectCorner cornersToRound;
                        if (_cardVerticalPosition < 0.1) {
                            // Near top: round bottom corners
                            cornersToRound = UIRectCornerBottomLeft | UIRectCornerBottomRight;
                        } else if (_cardVerticalPosition > 0.9) {
                            // Near bottom: round top corners
                            cornersToRound = UIRectCornerTopLeft | UIRectCornerTopRight;
                        } else {
                            // In middle: round all corners
                            cornersToRound = UIRectCornerAllCorners;
                        }
                        
                        // For iPad, always round all corners for centered appearance
                        if (isRunningOniPad()) {
                            cornersToRound = UIRectCornerAllCorners;
                        }
                        
                        // Round the selected corners
                        UIBezierPath *maskPath = [UIBezierPath bezierPathWithRoundedRect:containerVC.view.bounds
                                                                      byRoundingCorners:cornersToRound
                                                                            cornerRadii:CGSizeMake(12.0, 12.0)];
                        
                        CAShapeLayer *maskLayer = [[CAShapeLayer alloc] init];
                        maskLayer.frame = containerVC.view.bounds;
                        maskLayer.path = maskPath.CGPath;
                        containerVC.view.layer.mask = maskLayer;
                        
                        // Add drag tray at the top of the Safari card for drag-to-dismiss functionality
                        UIView *dragTray = [[StashPayCardSafariDelegate sharedInstance] createDragTray:width];
                        [containerVC.view addSubview:dragTray];
                        [StashPayCardSafariDelegate sharedInstance].dragTrayView = dragTray; // Store reference
                        
                        // Add pan gesture recognizer for swipe-to-dismiss
                        UIPanGestureRecognizer *panGesture = [[UIPanGestureRecognizer alloc] initWithTarget:[StashPayCardSafariDelegate sharedInstance] action:@selector(handlePanGesture:)];
                        panGesture.delegate = [StashPayCardSafariDelegate sharedInstance];
                        [containerVC.view addGestureRecognizer:panGesture];
                        [StashPayCardSafariDelegate sharedInstance].panGestureRecognizer = panGesture; // Store reference
                        
                        // Add dismissal gesture recognizer to the background overlay
                        UIView *backgroundView = containerVC.view.superview;
                        
                        // Remove existing gesture recognizers to avoid conflicts
                        for (UIGestureRecognizer *recognizer in [backgroundView.gestureRecognizers copy]) {
                            [backgroundView removeGestureRecognizer:recognizer];
                        }
                        
                        // Create a new transparent button to cover the background area
                        // For middle-positioned cards, use a different tap behavior
                        BOOL isMiddlePositioned = _cardVerticalPosition > 0.1 && _cardVerticalPosition < 0.9;
                        
                        UIButton *dismissButton = [UIButton buttonWithType:UIButtonTypeCustom];
                        dismissButton.frame = backgroundView.bounds;
                        dismissButton.backgroundColor = [UIColor clearColor];
                        dismissButton.autoresizingMask = UIViewAutoresizingFlexibleWidth | UIViewAutoresizingFlexibleHeight;
                        
                        // If we're in the middle, make the background slightly darker for better visibility
                        if (isMiddlePositioned) {
                            backgroundView.backgroundColor = [UIColor colorWithWhite:0.0 alpha:0.5];
                        }
                        
                        // Add the button to the background view
                        [backgroundView addSubview:dismissButton];
                        // Make sure it's behind the container view
                        [backgroundView sendSubviewToBack:dismissButton];
                        
                        // Add touch handler
                        [dismissButton addTarget:[StashPayCardSafariDelegate sharedInstance] 
                                         action:@selector(dismissButtonTapped:) 
                               forControlEvents:UIControlEventTouchUpInside];
                        
                        // Store the view controller reference for later dismissal
                        [StashPayCardSafariDelegate sharedInstance].currentPresentedVC = containerVC;
                        
                        // Start observing keyboard notifications for auto-expand
                        [[StashPayCardSafariDelegate sharedInstance] startKeyboardObserving];
                        
                        // Set the callback to be triggered when Safari View is dismissed
                        __weak UIViewController *weakContainerVC = containerVC; // Weak reference to avoid retain cycle
                        [StashPayCardSafariDelegate sharedInstance].safariViewDismissedCallback = ^{
                            // Stop keyboard observing when dismissing
                            [[StashPayCardSafariDelegate sharedInstance] stopKeyboardObserving];
                            
                            if (_safariViewDismissedCallback != NULL) {
                                _safariViewDismissedCallback();
                            }
                        };
                    }];
                }];
                return;
            }
            
            // If we get here, WKWebView failed - fall back to SFSafariViewController
            [[StashPayCardSafariDelegate sharedInstance] presentSafariViewController:url topController:topController];
        } else {
            // Fallback to opening in regular Safari for older iOS versions
            if (@available(iOS 10.0, *)) {
                [[UIApplication sharedApplication] openURL:url options:@{} completionHandler:nil];
            } else {
                [[UIApplication sharedApplication] openURL:url];
            }
        }
    }

    // Resets the card presentation state (useful for debugging or force reset)
    void _StashPayCardResetPresentationState() {
        _isCardCurrentlyPresented = NO;
        _callbackWasCalled = NO;
        _paymentSuccessHandled = NO;
        _paymentSuccessCallbackCalled = NO;
        if ([StashPayCardSafariDelegate sharedInstance].currentPresentedVC) {
            [[StashPayCardSafariDelegate sharedInstance].currentPresentedVC dismissViewControllerAnimated:NO completion:nil];
            [StashPayCardSafariDelegate sharedInstance].currentPresentedVC = nil;
        }
    }

    // Returns whether a card is currently being presented
    bool _StashPayCardIsCurrentlyPresented() {
        return _isCardCurrentlyPresented;
    }
}

