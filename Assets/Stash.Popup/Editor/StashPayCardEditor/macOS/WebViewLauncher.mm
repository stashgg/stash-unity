#import <Cocoa/Cocoa.h>
#import <WebKit/WebKit.h>
#import <string.h>
#import <pthread.h>

// Global callback function pointers - set by C# via P/Invoke
typedef void (*PaymentSuccessCallback)();
typedef void (*PaymentFailureCallback)();
typedef void (*PurchaseProcessingCallback)();
typedef void (*OptinResponseCallback)(const char* optinType);

static PaymentSuccessCallback _paymentSuccessCallback = NULL;
static PaymentFailureCallback _paymentFailureCallback = NULL;
static PurchaseProcessingCallback _purchaseProcessingCallback = NULL;
static OptinResponseCallback _optinResponseCallback = NULL;

// Simple in-memory notification queue (max 10 notifications)
#define MAX_NOTIFICATIONS 10
#define MAX_NOTIFICATION_LENGTH 256

typedef struct {
    char type[64];
    char data[192]; // For optin type or other data
} Notification;

static Notification notificationQueue[MAX_NOTIFICATIONS];
static int notificationQueueHead = 0;
static int notificationQueueTail = 0;
static int notificationQueueCount = 0;
static pthread_mutex_t notificationMutex = PTHREAD_MUTEX_INITIALIZER;

// Script message handler - matches iOS pattern exactly
@interface StashScriptMessageHandler : NSObject <WKScriptMessageHandler>
@end

@implementation StashScriptMessageHandler

- (void)userContentController:(WKUserContentController *)userContentController didReceiveScriptMessage:(WKScriptMessage *)message {
    NSString *name = message.name;
    
    NSLog(@"StashScriptMessageHandler: Received message: %@", name);
    
    // Try callbacks first if registered, otherwise queue notification for C# to poll
    if ([name isEqualToString:@"stashPaymentSuccess"]) {
        NSLog(@"StashScriptMessageHandler: Received payment success message");
        if (_paymentSuccessCallback != NULL) {
            dispatch_async(dispatch_get_main_queue(), ^{
                NSLog(@"StashScriptMessageHandler: Invoking payment success callback");
                _paymentSuccessCallback();
            });
        } else {
            NSLog(@"StashScriptMessageHandler: Callback not registered, queuing notification");
            pthread_mutex_lock(&notificationMutex);
            if (notificationQueueCount < MAX_NOTIFICATIONS) {
                strncpy(notificationQueue[notificationQueueTail].type, "StashPaymentSuccess", sizeof(notificationQueue[notificationQueueTail].type) - 1);
                notificationQueue[notificationQueueTail].type[sizeof(notificationQueue[notificationQueueTail].type) - 1] = '\0';
                notificationQueue[notificationQueueTail].data[0] = '\0';
                notificationQueueTail = (notificationQueueTail + 1) % MAX_NOTIFICATIONS;
                notificationQueueCount++;
            }
            pthread_mutex_unlock(&notificationMutex);
        }
    }
    else if ([name isEqualToString:@"stashPaymentFailure"]) {
        NSLog(@"StashScriptMessageHandler: Received payment failure message");
        if (_paymentFailureCallback != NULL) {
            dispatch_async(dispatch_get_main_queue(), ^{
                NSLog(@"StashScriptMessageHandler: Invoking payment failure callback");
                _paymentFailureCallback();
            });
        } else {
            NSLog(@"StashScriptMessageHandler: Callback not registered, queuing notification");
            pthread_mutex_lock(&notificationMutex);
            if (notificationQueueCount < MAX_NOTIFICATIONS) {
                strncpy(notificationQueue[notificationQueueTail].type, "StashPaymentFailure", sizeof(notificationQueue[notificationQueueTail].type) - 1);
                notificationQueue[notificationQueueTail].type[sizeof(notificationQueue[notificationQueueTail].type) - 1] = '\0';
                notificationQueue[notificationQueueTail].data[0] = '\0';
                notificationQueueTail = (notificationQueueTail + 1) % MAX_NOTIFICATIONS;
                notificationQueueCount++;
            }
            pthread_mutex_unlock(&notificationMutex);
        }
    }
    else if ([name isEqualToString:@"stashPurchaseProcessing"]) {
        NSLog(@"StashScriptMessageHandler: Received purchase processing message");
        if (_purchaseProcessingCallback != NULL) {
            dispatch_async(dispatch_get_main_queue(), ^{
                NSLog(@"StashScriptMessageHandler: Invoking purchase processing callback");
                _purchaseProcessingCallback();
            });
        } else {
            NSLog(@"StashScriptMessageHandler: Callback not registered, queuing notification");
            pthread_mutex_lock(&notificationMutex);
            if (notificationQueueCount < MAX_NOTIFICATIONS) {
                strncpy(notificationQueue[notificationQueueTail].type, "StashPurchaseProcessing", sizeof(notificationQueue[notificationQueueTail].type) - 1);
                notificationQueue[notificationQueueTail].type[sizeof(notificationQueue[notificationQueueTail].type) - 1] = '\0';
                notificationQueue[notificationQueueTail].data[0] = '\0';
                notificationQueueTail = (notificationQueueTail + 1) % MAX_NOTIFICATIONS;
                notificationQueueCount++;
            }
            pthread_mutex_unlock(&notificationMutex);
        }
    }
    else if ([name isEqualToString:@"stashOptin"]) {
        NSString *optinType = message.body;
        if (optinType == nil) {
            optinType = @"";
        }
        NSLog(@"StashScriptMessageHandler: Received optin message with type: %@", optinType);
        if (_optinResponseCallback != NULL) {
            const char *optinTypeCStr = [optinType UTF8String];
            dispatch_async(dispatch_get_main_queue(), ^{
                NSLog(@"StashScriptMessageHandler: Invoking optin callback");
                _optinResponseCallback(optinTypeCStr);
            });
        } else {
            NSLog(@"StashScriptMessageHandler: Callback not registered, queuing notification");
            pthread_mutex_lock(&notificationMutex);
            if (notificationQueueCount < MAX_NOTIFICATIONS) {
                strncpy(notificationQueue[notificationQueueTail].type, "StashOptinResponse", sizeof(notificationQueue[notificationQueueTail].type) - 1);
                notificationQueue[notificationQueueTail].type[sizeof(notificationQueue[notificationQueueTail].type) - 1] = '\0';
                const char *optinTypeCStr = [optinType UTF8String];
                if (optinTypeCStr) {
                    strncpy(notificationQueue[notificationQueueTail].data, optinTypeCStr, sizeof(notificationQueue[notificationQueueTail].data) - 1);
                    notificationQueue[notificationQueueTail].data[sizeof(notificationQueue[notificationQueueTail].data) - 1] = '\0';
                } else {
                    notificationQueue[notificationQueueTail].data[0] = '\0';
                }
                notificationQueueTail = (notificationQueueTail + 1) % MAX_NOTIFICATIONS;
                notificationQueueCount++;
            }
            pthread_mutex_unlock(&notificationMutex);
        }
    }
}

@end

// Global reference to store WebViewLauncher instance pointer for callbacks
static void* g_webViewLauncherInstance = NULL;

@interface WebViewWindow : NSWindow
@property (strong) WKWebView *webView;
@property (strong) StashScriptMessageHandler *scriptHandler;
- (instancetype)initWithRect:(NSRect)rect url:(NSString *)url;
@end

@implementation WebViewWindow

- (instancetype)initWithRect:(NSRect)rect url:(NSString *)url {
    self = [super initWithContentRect:rect
                             styleMask:NSWindowStyleMaskTitled | NSWindowStyleMaskResizable
                               backing:NSBackingStoreBuffered
                                 defer:NO];
    if (self) {
        self.title = @"StashPayCard Preview";
        self.releasedWhenClosed = NO;
        
        WKWebViewConfiguration *config = [[WKWebViewConfiguration alloc] init];
        WKUserContentController *userContentController = [[WKUserContentController alloc] init];
        
        // Create script message handler
        self.scriptHandler = [[StashScriptMessageHandler alloc] init];
        
        // Add script message handlers
        [userContentController addScriptMessageHandler:self.scriptHandler name:@"stashPaymentSuccess"];
        [userContentController addScriptMessageHandler:self.scriptHandler name:@"stashPaymentFailure"];
        [userContentController addScriptMessageHandler:self.scriptHandler name:@"stashPurchaseProcessing"];
        [userContentController addScriptMessageHandler:self.scriptHandler name:@"stashOptin"];
        
        // Inject JavaScript exactly like iOS - direct messageHandlers calls (no bridge object needed)
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
        
        // Inject at document start (matches iOS exactly)
        WKUserScript *stashSDKInjection = [[WKUserScript alloc] initWithSource:stashSDKScript
                                                           injectionTime:WKUserScriptInjectionTimeAtDocumentStart
                                                        forMainFrameOnly:YES];
        [userContentController addUserScript:stashSDKInjection];
        
        NSLog(@"WebViewWindow: JavaScript stash_sdk functions injected (iOS pattern)");
        
        config.userContentController = userContentController;
        
        self.webView = [[WKWebView alloc] initWithFrame:NSMakeRect(0, 0, rect.size.width, rect.size.height) configuration:config];
        self.webView.autoresizingMask = NSViewWidthSizable | NSViewHeightSizable;
        
        [self.contentView addSubview:self.webView];
        
        NSURL *urlObj = [NSURL URLWithString:url];
        if (urlObj) {
            [self.webView loadRequest:[NSURLRequest requestWithURL:urlObj]];
        }
        
        [self makeKeyAndOrderFront:nil];
    }
    return self;
}

@end

extern "C" {
    void* CreateWebViewWindow(double x, double y, double width, double height, const char* url) {
        @autoreleasepool {
            NSScreen *mainScreen = [NSScreen mainScreen];
            double screenHeight = mainScreen.frame.size.height;
            
            NSRect rect = NSMakeRect(x, screenHeight - y - height, width, height);
            WebViewWindow *window = [[WebViewWindow alloc] initWithRect:rect url:[NSString stringWithUTF8String:url]];
            [window retain]; // Manual retain for non-ARC
            return (void*)window;
        }
    }
    
    void DestroyWebViewWindow(void* windowPtr) {
        if (windowPtr) {
            @autoreleasepool {
                WebViewWindow *window = (WebViewWindow*)windowPtr;
                // Remove script message handlers before closing
                if (window.scriptHandler) {
                    [window.webView.configuration.userContentController removeScriptMessageHandlerForName:@"stashPaymentSuccess"];
                    [window.webView.configuration.userContentController removeScriptMessageHandlerForName:@"stashPaymentFailure"];
                    [window.webView.configuration.userContentController removeScriptMessageHandlerForName:@"stashPurchaseProcessing"];
                    [window.webView.configuration.userContentController removeScriptMessageHandlerForName:@"stashOptin"];
                }
                [window close];
                [window release]; // Manual release for non-ARC
            }
        }
    }
    
    // Set callback functions - accept void* and cast to function pointer (for Unity P/Invoke)
    // These MUST be exported with extern "C" to be visible to dlsym
    extern "C" void SetPaymentSuccessCallback(void* callbackPtr) {
        _paymentSuccessCallback = (PaymentSuccessCallback)callbackPtr;
        NSLog(@"SetPaymentSuccessCallback: callback set to %p", callbackPtr);
    }
    
    extern "C" void SetPaymentFailureCallback(void* callbackPtr) {
        _paymentFailureCallback = (PaymentFailureCallback)callbackPtr;
        NSLog(@"SetPaymentFailureCallback: callback set to %p", callbackPtr);
    }
    
    extern "C" void SetPurchaseProcessingCallback(void* callbackPtr) {
        _purchaseProcessingCallback = (PurchaseProcessingCallback)callbackPtr;
        NSLog(@"SetPurchaseProcessingCallback: callback set to %p", callbackPtr);
    }
    
    extern "C" void SetOptinResponseCallback(void* callbackPtr) {
        _optinResponseCallback = (OptinResponseCallback)callbackPtr;
        NSLog(@"SetOptinResponseCallback: callback set to %p", callbackPtr);
    }
    
    // Poll for notifications - returns 1 if notification found, 0 if none
    // Fills type and data buffers if notification found
    extern "C" int PollNotification(char* typeBuffer, int typeBufferSize, char* dataBuffer, int dataBufferSize) {
        pthread_mutex_lock(&notificationMutex);
        int hasNotification = 0;
        if (notificationQueueCount > 0) {
            Notification* notif = &notificationQueue[notificationQueueHead];
            strncpy(typeBuffer, notif->type, typeBufferSize - 1);
            typeBuffer[typeBufferSize - 1] = '\0';
            strncpy(dataBuffer, notif->data, dataBufferSize - 1);
            dataBuffer[dataBufferSize - 1] = '\0';
            notificationQueueHead = (notificationQueueHead + 1) % MAX_NOTIFICATIONS;
            notificationQueueCount--;
            hasNotification = 1;
        }
        pthread_mutex_unlock(&notificationMutex);
        return hasNotification;
    }
}

