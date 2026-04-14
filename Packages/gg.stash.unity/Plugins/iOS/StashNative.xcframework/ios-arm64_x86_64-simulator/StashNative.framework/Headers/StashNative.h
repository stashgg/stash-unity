//
//  StashNative.h
//  StashNative
//
//  Native iOS SDK for Stash Native checkout integration.
//

#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>

//! Project version number for StashNative.
FOUNDATION_EXPORT double StashNativeVersionNumber;

//! Project version string for StashNative.
FOUNDATION_EXPORT const unsigned char StashNativeVersionString[];

#if __has_include(<StashNative/StashNativeCard.h>)
#import <StashNative/StashNativeCard.h>
#else
#import "StashNativeCard.h"
#endif
