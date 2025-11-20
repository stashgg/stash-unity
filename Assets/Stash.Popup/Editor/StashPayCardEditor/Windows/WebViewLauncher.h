#pragma once

#ifdef WEBVIEWLAUNCHER_EXPORTS
#define WEBVIEWLAUNCHER_API __declspec(dllexport)
#else
#define WEBVIEWLAUNCHER_API __declspec(dllimport)
#endif

extern "C" {
    WEBVIEWLAUNCHER_API void* CreateWebViewWindow(double x, double y, double width, double height, const char* url);
    WEBVIEWLAUNCHER_API void DestroyWebViewWindow(void* windowPtr);
    WEBVIEWLAUNCHER_API void SetPaymentSuccessCallback(void* callbackPtr);
    WEBVIEWLAUNCHER_API void SetPaymentFailureCallback(void* callbackPtr);
    WEBVIEWLAUNCHER_API void SetPurchaseProcessingCallback(void* callbackPtr);
    WEBVIEWLAUNCHER_API void SetOptinResponseCallback(void* callbackPtr);
    WEBVIEWLAUNCHER_API int PollNotification(char* typeBuffer, int typeBufferSize, char* dataBuffer, int dataBufferSize);
    WEBVIEWLAUNCHER_API void PumpMessages();
}


