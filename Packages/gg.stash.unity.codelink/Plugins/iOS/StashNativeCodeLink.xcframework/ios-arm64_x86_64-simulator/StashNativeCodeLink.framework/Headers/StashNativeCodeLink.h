#import <Foundation/Foundation.h>

// Optional QR scanner module for StashNative. Linking this framework enables
// -[StashNative openScannerFromViewController:config:]; the core finds
// StashNativeScannerViewController at runtime and reports
// StashNativeScannerErrorScannerUnavailable when it is absent. The host app must
// declare NSCameraUsageDescription. There is no host-facing API in this header.
