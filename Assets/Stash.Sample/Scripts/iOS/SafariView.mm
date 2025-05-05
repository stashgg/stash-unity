#import <Foundation/Foundation.h>
#import <SafariServices/SafariServices.h>

// Unity bridge to open URLs in Safari View Controller
extern "C" {
    void _OpenURLInSafariVC(const char* urlStr) {
        if (urlStr == NULL) {
            NSLog(@"Error: URL is null");
            return;
        }
        
        NSString* nsUrlStr = [NSString stringWithUTF8String:urlStr];
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
        
        // Create and present the Safari View Controller
        if (@available(iOS 9.0, *)) {
            SFSafariViewController* safariViewController = [[SFSafariViewController alloc] initWithURL:url];
            safariViewController.modalPresentationStyle = UIModalPresentationPageSheet;
            [topController presentViewController:safariViewController animated:YES completion:nil];
        } else {
            // Fallback to opening in regular Safari for older iOS versions
            [[UIApplication sharedApplication] openURL:url];
        }
    }
} 