#import <Foundation/Foundation.h>
#import <WebKit/WebKit.h>

// This empty class ensures WebKit framework gets linked
@interface StashPayCardWebKitLink : NSObject
@end

@implementation StashPayCardWebKitLink
// Force WebKit to be linked with the app
+ (void)load {
    // Create and immediately release a WKWebViewConfiguration to ensure linking
    WKWebViewConfiguration* config = [[WKWebViewConfiguration alloc] init];
    (void)config; // avoid unused variable warning
}
@end 