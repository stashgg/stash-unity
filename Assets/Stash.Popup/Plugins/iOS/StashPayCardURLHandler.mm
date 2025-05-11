#import <Foundation/Foundation.h>
#import <SafariServices/SafariServices.h>
#import <WebKit/WebKit.h>
#import <objc/runtime.h>

// Tell Xcode to link WebKit framework
#pragma comment(lib, "WebKit.framework")

// Mark WebKit framework as required
__attribute__((constructor))
static void InitializeWebKit() {
    NSLog(@"Initializing WebKit framework");
}

// Define a Unity callback function typedef
typedef void (*SafariViewDismissedCallback)();
SafariViewDismissedCallback _safariViewDismissedCallback = NULL;
// Flag to track if callback was already called
BOOL _callbackWasCalled = NO;

// Define a delegate class to handle Safari View Controller callbacks
@interface StashPayCardSafariDelegate : NSObject <SFSafariViewControllerDelegate, UIGestureRecognizerDelegate>
+ (instancetype)sharedInstance;
@property (nonatomic, copy) void (^safariViewDismissedCallback)(void);
@property (nonatomic, strong) UIViewController *currentPresentedVC;
@property (nonatomic, strong) UIPanGestureRecognizer *panGestureRecognizer;
@property (nonatomic, assign) CGFloat initialY;
- (void)handleDismiss:(UITapGestureRecognizer *)gesture;
- (void)dismissButtonTapped:(UIButton *)button;
- (void)handlePanGesture:(UIPanGestureRecognizer *)gesture;
- (void)fallbackToSafariVC:(NSURL *)url topController:(UIViewController *)topController;
- (void)callUnityCallbackOnce;
@end

// WebView navigation delegate to handle loading states
@interface WebViewLoadDelegate : NSObject <WKNavigationDelegate>
- (instancetype)initWithWebView:(WKWebView*)webView loadingView:(UIView*)loadingView;
@end

@implementation WebViewLoadDelegate {
    WKWebView* _webView;
    UIView* _loadingView;
    NSTimer* _timeoutTimer;
}

- (instancetype)initWithWebView:(WKWebView*)webView loadingView:(UIView*)loadingView {
    self = [super init];
    if (self) {
        _webView = webView;
        _loadingView = loadingView;
        
        // Create a fallback timer to handle cases where navigation events aren't fired
        _timeoutTimer = [NSTimer scheduledTimerWithTimeInterval:5.0 
                                                        target:self 
                                                      selector:@selector(handleTimeout:) 
                                                      userInfo:nil 
                                                       repeats:NO];
    }
    return self;
}

// Handle external links and decide when to call Unity callback
- (void)webView:(WKWebView *)webView decidePolicyForNavigationAction:(WKNavigationAction *)navigationAction decisionHandler:(void (^)(WKNavigationActionPolicy))decisionHandler {
    
    NSURL *url = navigationAction.request.URL;
    NSLog(@"WebView navigation requested to: %@", url.absoluteString);
    
    // Check if this is a normal link click (not the initial page load)
    if (navigationAction.navigationType == WKNavigationTypeLinkActivated) {
        NSLog(@"Link was clicked, will call callback and open in external browser");
        
        // Forward to the next section for actual dismissal
        decisionHandler(WKNavigationActionPolicyCancel);
        
        // Open the URL in Safari
        [[UIApplication sharedApplication] openURL:url options:@{} completionHandler:nil];
        
        // We need to find the view controller after declaration to avoid the undeclared identifier error
        dispatch_async(dispatch_get_main_queue(), ^{
            // Use main thread and delay slightly to ensure Safari delegate exists
            if ([StashPayCardSafariDelegate sharedInstance].currentPresentedVC) {
                [[StashPayCardSafariDelegate sharedInstance].currentPresentedVC dismissViewControllerAnimated:YES completion:^{
                    [[StashPayCardSafariDelegate sharedInstance] callUnityCallbackOnce];
                }];
            }
        });
        
        return;
    }
    
    // Allow all other navigations
    decisionHandler(WKNavigationActionPolicyAllow);
}

- (void)handleTimeout:(NSTimer*)timer {
    if (_webView.hidden) {
        NSLog(@"Timeout occurred - showing WebView anyway");
        [self showWebViewAndRemoveLoading];
    }
}

- (void)showWebViewAndRemoveLoading {
    if (_timeoutTimer) {
        [_timeoutTimer invalidate];
        _timeoutTimer = nil;
    }
    
    if (_webView.hidden) {
        [UIView animateWithDuration:0.3 animations:^{
            _loadingView.alpha = 0.0;
        } completion:^(BOOL finished) {
            _webView.hidden = NO;
            [_loadingView removeFromSuperview];
        }];
    }
}

- (void)webView:(WKWebView *)webView didFinishNavigation:(WKNavigation *)navigation {
    NSLog(@"WebView did finish navigation");
    [self showWebViewAndRemoveLoading];
}

- (void)webView:(WKWebView *)webView didCommitNavigation:(WKNavigation *)navigation {
    NSLog(@"WebView did commit navigation");
}

- (void)webView:(WKWebView *)webView didStartProvisionalNavigation:(WKNavigation *)navigation {
    NSLog(@"WebView started provisional navigation");
}

- (void)webView:(WKWebView *)webView didFailNavigation:(WKNavigation *)navigation withError:(NSError *)error {
    NSLog(@"WebView navigation failed with error: %@", error);
    // Still remove loading view on error
    [self showWebViewAndRemoveLoading];
}

- (void)webView:(WKWebView *)webView didFailProvisionalNavigation:(WKNavigation *)navigation withError:(NSError *)error {
    NSLog(@"WebView provisional navigation failed with error: %@", error);
    // Still remove loading view on provisional error
    [self showWebViewAndRemoveLoading];
}

- (void)dealloc {
    if (_timeoutTimer) {
        [_timeoutTimer invalidate];
        _timeoutTimer = nil;
    }
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
        NSLog(@"Calling Unity callback once");
        _callbackWasCalled = YES;
        dispatch_async(dispatch_get_main_queue(), ^{
            _safariViewDismissedCallback();
        });
    }
}

- (void)safariViewControllerDidFinish:(SFSafariViewController *)controller {
    NSLog(@"Safari View Controller did finish");
    [self callUnityCallbackOnce];
}

- (void)handleDismiss:(UITapGestureRecognizer *)gesture {
    if (self.currentPresentedVC) {
        [self.currentPresentedVC dismissViewControllerAnimated:YES completion:^{
            [self callUnityCallbackOnce];
        }];
    }
}

- (void)dismissButtonTapped:(UIButton *)button {
    NSLog(@"Dismiss button tapped");
    if (self.currentPresentedVC) {
        [self.currentPresentedVC dismissViewControllerAnimated:YES completion:^{
            NSLog(@"View controller dismissed via button tap");
            [self callUnityCallbackOnce];
        }];
    }
}

- (void)handlePanGesture:(UIPanGestureRecognizer *)gesture {
    if (!self.currentPresentedVC) return;
    
    UIView *view = self.currentPresentedVC.view;
    CGFloat height = view.frame.size.height;
    CGPoint translation = [gesture translationInView:view.superview];
    
    switch (gesture.state) {
        case UIGestureRecognizerStateBegan:
            self.initialY = view.frame.origin.y;
            break;
            
        case UIGestureRecognizerStateChanged: {
            // Only allow downward movement
            CGFloat newY = self.initialY + translation.y;
            if (newY < self.initialY) newY = self.initialY;
            
            view.frame = CGRectMake(view.frame.origin.x, newY, view.frame.size.width, height);
            
            // Adjust background opacity based on position
            CGFloat maxTravel = height;
            CGFloat currentTravel = newY - self.initialY;
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
            
            // If swiped down with enough velocity or dragged down far enough, dismiss
            if (velocity.y > 300 || currentY > (self.initialY + height * 0.3)) {
                // Animate the rest of the way out, then dismiss
                [UIView animateWithDuration:0.2 animations:^{
                    view.frame = CGRectMake(view.frame.origin.x, view.superview.bounds.size.height, view.frame.size.width, height);
                    view.superview.backgroundColor = [UIColor colorWithWhite:0.0 alpha:0.0];
                } completion:^(BOOL finished) {
                    [self.currentPresentedVC dismissViewControllerAnimated:NO completion:^{
                        NSLog(@"View controller dismissed via swipe gesture");
                        [self callUnityCallbackOnce];
                    }];
                }];
            } else {
                // Animate back to original position
                [UIView animateWithDuration:0.2 animations:^{
                    view.frame = CGRectMake(view.frame.origin.x, self.initialY, view.frame.size.width, height);
                    view.superview.backgroundColor = [UIColor colorWithWhite:0.0 alpha:0.4];
                }];
            }
            break;
        }
            
        default:
            break;
    }
}

// For UIGestureRecognizerDelegate
- (BOOL)gestureRecognizer:(UIGestureRecognizer *)gestureRecognizer shouldRecognizeSimultaneouslyWithGestureRecognizer:(UIGestureRecognizer *)otherGestureRecognizer {
    // Allow this gesture to work alongside others
    return YES;
}

- (void)fallbackToSafariVC:(NSURL *)url topController:(UIViewController *)topController {
    NSLog(@"Falling back to SFSafariViewController");
    
    SFSafariViewController* safariViewController = [[SFSafariViewController alloc] initWithURL:url];
    
    // Use automatic style which works better with Safari View Controller
    safariViewController.modalPresentationStyle = UIModalPresentationOverFullScreen;
    
    // Pre-configure the view before presentation to avoid slide animation
    safariViewController.view.backgroundColor = [UIColor clearColor];
    
    // Apply an Apple Pay style frame to the view before presentation
    CGRect screenBounds = [UIScreen mainScreen].bounds;
    CGFloat width = screenBounds.size.width;  // Full width
    CGFloat height = screenBounds.size.height * 0.4; // 40% of screen height
    CGFloat x = 0;
    CGFloat y = screenBounds.size.height; // Start off-screen at the bottom
    
    safariViewController.view.frame = CGRectMake(x, y, width, height);
    
    // Make the Safari view non-fullscreen by setting its frame before and after presentation
    [topController presentViewController:safariViewController animated:NO completion:^{
        // Immediately after presentation, animate to final position
        CGFloat finalY = screenBounds.size.height - height;
        
        [UIView animateWithDuration:0.25 animations:^{
            safariViewController.view.frame = CGRectMake(x, finalY, width, height);
            safariViewController.view.superview.backgroundColor = [UIColor colorWithWhite:0.0 alpha:0.4];
        } completion:^(BOOL finished) {
            // Round only the top corners
            UIBezierPath *maskPath = [UIBezierPath bezierPathWithRoundedRect:safariViewController.view.bounds
                                                          byRoundingCorners:(UIRectCornerTopLeft | UIRectCornerTopRight)
                                                                cornerRadii:CGSizeMake(12.0, 12.0)];
            
            CAShapeLayer *maskLayer = [[CAShapeLayer alloc] init];
            maskLayer.frame = safariViewController.view.bounds;
            maskLayer.path = maskPath.CGPath;
            safariViewController.view.layer.mask = maskLayer;
            
            // Add a subtle handle at the top like Apple Pay sheet
            UIView *handleView = [[UIView alloc] initWithFrame:CGRectMake(width/2 - 20, 6, 40, 5)];
            handleView.backgroundColor = [UIColor colorWithWhite:0.8 alpha:1.0];
            handleView.layer.cornerRadius = 2.5;
            [safariViewController.view addSubview:handleView];
        }];
    }];
    
    // Set the delegate
    safariViewController.delegate = [StashPayCardSafariDelegate sharedInstance];
    
    // Set the callback to be triggered when Safari View is dismissed
    [StashPayCardSafariDelegate sharedInstance].safariViewDismissedCallback = ^{
        if (_safariViewDismissedCallback != NULL) {
            NSLog(@"Calling _safariViewDismissedCallback from safariViewDismissedCallback");
            _safariViewDismissedCallback();
        }
    };
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
        NSLog(@"Current UI style: %@", isDarkMode ? @"Dark" : @"Light");
    }
    
    // Set the background color based on mode
    loadingView.backgroundColor = isDarkMode ? [UIColor blackColor] : [UIColor whiteColor];
    
    // Choose logo color based on mode
    UIColor *logoColor = isDarkMode ? [UIColor whiteColor] : [UIColor blackColor];
    
    // Calculate the reduced size (70% of original)
    float originalWidth = 295.0;
    float originalHeight = 53.0;
    float scaleFactor = 0.7; // 70% of original size (30% reduction)
    float newWidth = originalWidth * scaleFactor;
    float newHeight = originalHeight * scaleFactor;
    
    NSLog(@"Rendering logo at reduced size: %.0f x %.0f", newWidth, newHeight);
    
    // Create a container for the logo with the reduced size
    UIView* logoContainer = [[UIView alloc] initWithFrame:CGRectMake(0, 0, newWidth, newHeight)];
    logoContainer.backgroundColor = loadingView.backgroundColor;
    logoContainer.translatesAutoresizingMaskIntoConstraints = NO;
    
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
    
    // Remove all default margins/padding in the webview and scale the content to fit
    NSString *htmlTemplate = @"<!DOCTYPE html><html><head><meta name='viewport' content='width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no'><style>body{margin:0;padding:0;background-color:%@;display:flex;justify-content:center;align-items:center;width:100%%;height:100%%;overflow:hidden;}svg{width:100%%;height:auto;}</style></head><body>%@</body></html>";
    
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
    
    // Center the logo container in the loading view
    [NSLayoutConstraint activateConstraints:@[
        [logoContainer.centerXAnchor constraintEqualToAnchor:loadingView.centerXAnchor],
        [logoContainer.centerYAnchor constraintEqualToAnchor:loadingView.centerYAnchor],
        [logoContainer.widthAnchor constraintEqualToConstant:newWidth],
        [logoContainer.heightAnchor constraintEqualToConstant:newHeight]
    ]];
    
    // Add a loading indicator below the logo
    UIActivityIndicatorView* activityIndicator;
    if (@available(iOS 13.0, *)) {
        activityIndicator = [[UIActivityIndicatorView alloc] initWithActivityIndicatorStyle:UIActivityIndicatorViewStyleMedium];
    } else {
        activityIndicator = [[UIActivityIndicatorView alloc] initWithActivityIndicatorStyle:UIActivityIndicatorViewStyleWhite];
    }
    
    // Set the indicator color based on the mode
    activityIndicator.color = logoColor;
    activityIndicator.translatesAutoresizingMaskIntoConstraints = NO;
    [loadingView addSubview:activityIndicator];
    
    [NSLayoutConstraint activateConstraints:@[
        [activityIndicator.centerXAnchor constraintEqualToAnchor:loadingView.centerXAnchor],
        [activityIndicator.topAnchor constraintEqualToAnchor:logoContainer.bottomAnchor constant:20]
    ]];
    
    [activityIndicator startAnimating];
    
    return loadingView;
}

// Unity bridge to open URLs in Safari View Controller
extern "C" {
    // Sets the callback function to be called when Safari View is dismissed
    void _StashPayCardSetSafariViewDismissedCallback(SafariViewDismissedCallback callback) {
        _safariViewDismissedCallback = callback;
        _callbackWasCalled = NO; // Reset flag when setting a new callback
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
            // Try to create a custom view controller with WKWebView
            UIViewController *containerVC = [[UIViewController alloc] init];
            containerVC.modalPresentationStyle = UIModalPresentationOverFullScreen;
            
            // Try to create the web view
            Class webViewClass = NSClassFromString(@"WKWebView");
            Class configClass = NSClassFromString(@"WKWebViewConfiguration");
            
            if (webViewClass && configClass) {
                // WebKit is available, use WKWebView with proper configuration
                WKWebViewConfiguration *config = [[WKWebViewConfiguration alloc] init];
                config.allowsInlineMediaPlayback = YES;
                
                // Create preferences
                WKPreferences *preferences = [[WKPreferences alloc] init];
                preferences.javaScriptEnabled = YES;
                preferences.javaScriptCanOpenWindowsAutomatically = YES;
                config.preferences = preferences;
                
                // Setup a web view controller
                WKWebView *webView = [[WKWebView alloc] initWithFrame:CGRectZero configuration:config];
                webView.backgroundColor = [UIColor blackColor];
                webView.opaque = YES;
                webView.translatesAutoresizingMaskIntoConstraints = NO;
                webView.hidden = YES; // Hide webview initially until loaded
                webView.scrollView.bounces = NO; // Prevent bouncing effect
                
                // Create an explicit URL request
                NSMutableURLRequest *request = [NSMutableURLRequest requestWithURL:url 
                                                                       cachePolicy:NSURLRequestUseProtocolCachePolicy
                                                                   timeoutInterval:30.0];
                
                // Add standard headers
                [request setValue:@"Mozilla/5.0 (iPhone; CPU iPhone OS 15_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/15.0 Mobile/15E148 Safari/604.1" forHTTPHeaderField:@"User-Agent"];
            
                // Log the URL we're trying to load
                NSLog(@"Loading URL in WebView: %@", url.absoluteString);
                
                // Load the request
                [webView loadRequest:request];
                
                // Create loading view with logo
                UIView* loadingView = CreateLoadingView(CGRectZero);
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
            
                // Set up constraints for the web view (leaving space at top for handle)
                [NSLayoutConstraint activateConstraints:@[
                    [webView.leadingAnchor constraintEqualToAnchor:containerVC.view.leadingAnchor],
                    [webView.trailingAnchor constraintEqualToAnchor:containerVC.view.trailingAnchor],
                    [webView.topAnchor constraintEqualToAnchor:containerVC.view.topAnchor constant:20],
                    [webView.bottomAnchor constraintEqualToAnchor:containerVC.view.bottomAnchor]
                ]];
                
                // Add navigation delegate to detect when loading finishes
                WebViewLoadDelegate *delegate = [[WebViewLoadDelegate alloc] initWithWebView:webView loadingView:loadingView];
                webView.navigationDelegate = delegate;
                // Store the delegate with the view controller to prevent deallocation
                objc_setAssociatedObject(containerVC, "webViewDelegate", delegate, OBJC_ASSOCIATION_RETAIN_NONATOMIC);
                
                // Pre-configure the view with transparent background
                containerVC.view.backgroundColor = [UIColor clearColor];
                
                // Apply initial frame off-screen
                CGRect screenBounds = [UIScreen mainScreen].bounds;
                CGFloat width = screenBounds.size.width;  // Full width
                CGFloat height = screenBounds.size.height * 0.4; // 40% of screen height
                CGFloat x = 0;
                CGFloat y = screenBounds.size.height; // Start off-screen at the bottom
                
                // Present without animation and animate to position after
                [topController presentViewController:containerVC animated:NO completion:^{
                    // Set initial position off-screen
                    containerVC.view.frame = CGRectMake(x, y, width, height);
                    
                    // Animate into position
                    [UIView animateWithDuration:0.25 animations:^{
                        CGFloat finalY = screenBounds.size.height - height;
                        containerVC.view.frame = CGRectMake(x, finalY, width, height);
                        containerVC.view.backgroundColor = [UIColor blackColor];
                        containerVC.view.superview.backgroundColor = [UIColor colorWithWhite:0.0 alpha:0.4];
                    } completion:^(BOOL finished) {
                        // Round only the top corners
                        UIBezierPath *maskPath = [UIBezierPath bezierPathWithRoundedRect:containerVC.view.bounds
                                                                      byRoundingCorners:(UIRectCornerTopLeft | UIRectCornerTopRight)
                                                                            cornerRadii:CGSizeMake(12.0, 12.0)];
                        
                        CAShapeLayer *maskLayer = [[CAShapeLayer alloc] init];
                        maskLayer.frame = containerVC.view.bounds;
                        maskLayer.path = maskPath.CGPath;
                        containerVC.view.layer.mask = maskLayer;
                        
                        // Add a subtle handle at the top like Apple Pay sheet
                        UIView *handleView = [[UIView alloc] initWithFrame:CGRectMake(width/2 - 20, 6, 40, 5)];
                        handleView.backgroundColor = [UIColor colorWithWhite:0.8 alpha:1.0];
                        handleView.layer.cornerRadius = 2.5;
                        [containerVC.view addSubview:handleView];
                        
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
                        UIButton *dismissButton = [UIButton buttonWithType:UIButtonTypeCustom];
                        dismissButton.frame = backgroundView.bounds;
                        dismissButton.backgroundColor = [UIColor clearColor];
                        dismissButton.autoresizingMask = UIViewAutoresizingFlexibleWidth | UIViewAutoresizingFlexibleHeight;
                        
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
                        
                        // Set the callback to be triggered when Safari View is dismissed
                        [StashPayCardSafariDelegate sharedInstance].safariViewDismissedCallback = ^{
                            if (_safariViewDismissedCallback != NULL) {
                                NSLog(@"Calling _safariViewDismissedCallback from safariViewDismissedCallback");
                                _safariViewDismissedCallback();
                            }
                        };
                        
                        // GUARANTEED FALLBACK: Show the WebView after 3 seconds no matter what
                        dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(3 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
                            if (webView.hidden) {
                                NSLog(@"Forcing webView display after timeout");
                                webView.hidden = NO;
                                loadingView.hidden = YES;
                            }
                        });
                    }];
                }];
                return;
            }
            
            // If we get here, WKWebView failed - fall back to SFSafariViewController
            [[StashPayCardSafariDelegate sharedInstance] fallbackToSafariVC:url topController:topController];
        } else {
            // Fallback to opening in regular Safari for older iOS versions
            [[UIApplication sharedApplication] openURL:url];
        }
    }
} 

