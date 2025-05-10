// Stash Purchase Dialog for iOS

#import <Foundation/Foundation.h>
#import <SafariServices/SafariServices.h>
#import <WebKit/WebKit.h>

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

// Define a delegate class to handle Safari View Controller callbacks
@interface StashSafariDelegate : NSObject <SFSafariViewControllerDelegate>
+ (instancetype)sharedInstance;
@property (nonatomic, copy) void (^safariViewDismissedCallback)(void);
@property (nonatomic, strong) UIViewController *currentPresentedVC;
- (void)handleDismiss:(UITapGestureRecognizer *)gesture;
- (void)fallbackToSafariVC:(NSURL *)url topController:(UIViewController *)topController;
@end

@implementation StashSafariDelegate

+ (instancetype)sharedInstance {
    static StashSafariDelegate *sharedInstance = nil;
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        sharedInstance = [[StashSafariDelegate alloc] init];
    });
    return sharedInstance;
}

- (void)safariViewControllerDidFinish:(SFSafariViewController *)controller {
    NSLog(@"Safari View Controller did finish");
    if (self.safariViewDismissedCallback) {
        self.safariViewDismissedCallback();
    }
}

- (void)handleDismiss:(UITapGestureRecognizer *)gesture {
    if (self.currentPresentedVC) {
        [self.currentPresentedVC dismissViewControllerAnimated:YES completion:^{
            if (self.safariViewDismissedCallback) {
                self.safariViewDismissedCallback();
            }
        }];
    }
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
    safariViewController.delegate = [StashSafariDelegate sharedInstance];
    
    // Set the callback to be triggered when Safari View is dismissed
    [StashSafariDelegate sharedInstance].safariViewDismissedCallback = ^{
        if (_safariViewDismissedCallback != NULL) {
            _safariViewDismissedCallback();
        }
    };
}

@end

// Unity bridge to open URLs in Safari View Controller
extern "C" {
    // Sets the callback function to be called when Safari View is dismissed
    void _SetSafariViewDismissedCallback(SafariViewDismissedCallback callback) {
        _safariViewDismissedCallback = callback;
    }

    // Opens a URL in Safari View Controller with delegation
    void _OpenURLInSafariVC(const char* urlString) {
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
                // WebKit is available, use WKWebView
                WKWebViewConfiguration *config = [[WKWebViewConfiguration alloc] init];
                WKWebView *webView = [[WKWebView alloc] initWithFrame:CGRectZero configuration:config];
                webView.backgroundColor = [UIColor blackColor];
                webView.translatesAutoresizingMaskIntoConstraints = NO;
                [webView loadRequest:[NSURLRequest requestWithURL:url]];
                
                [containerVC.view addSubview:webView];
                
                // Set up constraints for the web view (leaving space at top for handle)
                [NSLayoutConstraint activateConstraints:@[
                    [webView.leadingAnchor constraintEqualToAnchor:containerVC.view.leadingAnchor],
                    [webView.trailingAnchor constraintEqualToAnchor:containerVC.view.trailingAnchor],
                    [webView.topAnchor constraintEqualToAnchor:containerVC.view.topAnchor constant:20],
                    [webView.bottomAnchor constraintEqualToAnchor:containerVC.view.bottomAnchor]
                ]];
                
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
                        
                        // Add dismissal gesture recognizer
                        UITapGestureRecognizer *tapGesture = [[UITapGestureRecognizer alloc] initWithTarget:[StashSafariDelegate sharedInstance] action:@selector(handleDismiss:)];
                        tapGesture.numberOfTapsRequired = 1;
                        [containerVC.view.superview addGestureRecognizer:tapGesture];
                        
                        // Store the view controller reference for later dismissal
                        [StashSafariDelegate sharedInstance].currentPresentedVC = containerVC;
                    }];
                }];
                return;
            }
            
            // If we get here, WKWebView failed - fall back to SFSafariViewController
            [[StashSafariDelegate sharedInstance] fallbackToSafariVC:url topController:topController];
        } else {
            // Fallback to opening in regular Safari for older iOS versions
            [[UIApplication sharedApplication] openURL:url];
        }
    }
} 