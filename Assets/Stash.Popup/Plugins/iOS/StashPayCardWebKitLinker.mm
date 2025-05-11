#import <WebKit/WebKit.h>

// External framework linkage flag
extern "C" {
    // This function is never called, but forces the linker to include WKWebView symbols
    void _StashPayCardForceWebKitLinkage() {
        // Create WebKit classes to ensure linkage
        WKWebViewConfiguration* config = [[WKWebViewConfiguration alloc] init];
        WKWebView* webView = [[WKWebView alloc] initWithFrame:CGRectZero configuration:config];
        (void)webView; // Prevent unused variable warnings
    }
} 