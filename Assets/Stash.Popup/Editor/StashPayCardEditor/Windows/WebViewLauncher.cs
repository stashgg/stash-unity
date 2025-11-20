using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEditor;
using AOT;

namespace StashPopup.Editor.Windows
{
    /// <summary>
    /// Native webview launcher using compiled C++ code with WebView2 (Windows only)
    /// </summary>
    public class WebViewLauncher
    {
#if UNITY_EDITOR_WIN
        private const string DllName = "WebViewLauncher";

        // Callback delegates - must be UnmanagedFunctionPointer for P/Invoke
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void PaymentSuccessCallback();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void PaymentFailureCallback();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void PurchaseProcessingCallback();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void OptinResponseCallback(IntPtr optinType);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr CreateWebViewWindow(double x, double y, double width, double height, string url);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void DestroyWebViewWindow(IntPtr windowPtr);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int PollNotification(System.Text.StringBuilder typeBuffer, int typeBufferSize, System.Text.StringBuilder dataBuffer, int dataBufferSize);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void SetPaymentSuccessCallback(IntPtr callbackPtr);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void SetPaymentFailureCallback(IntPtr callbackPtr);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void SetPurchaseProcessingCallback(IntPtr callbackPtr);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void SetOptinResponseCallback(IntPtr callbackPtr);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void PumpMessages();

        // Callback instances (kept alive to prevent GC)
        private PaymentSuccessCallback paymentSuccessCallback;
        private PaymentFailureCallback paymentFailureCallback;
        private PurchaseProcessingCallback purchaseProcessingCallback;
        private OptinResponseCallback optinResponseCallback;

        // Event handlers
        public event Action OnPaymentSuccess;
        public event Action OnPaymentFailure;
        public event Action OnPurchaseProcessing;
        public event Action<string> OnOptinResponse;

        // Static instance reference for callbacks
        private static WebViewLauncher currentInstance;

        private IntPtr windowPtr = IntPtr.Zero;
        private static bool messagePumpActive = false;

        public bool CreateWindow(Rect rect, string url)
        {
            try
            {
                if (windowPtr != IntPtr.Zero)
                {
                    DestroyWindow();
                }

                // Set current instance for callbacks
                currentInstance = this;
                Debug.Log("[WebViewLauncher] Set currentInstance for callbacks");

                // Set up callbacks
                paymentSuccessCallback = OnPaymentSuccessInternal;
                paymentFailureCallback = OnPaymentFailureInternal;
                purchaseProcessingCallback = OnPurchaseProcessingInternal;
                optinResponseCallback = OnOptinResponseInternal;

                // Register callbacks BEFORE creating window so they're ready when JavaScript messages arrive
                Debug.Log("[WebViewLauncher] Registering callbacks with native code");
                try
                {
                    // Convert callback delegates to function pointers
                    IntPtr successPtr = Marshal.GetFunctionPointerForDelegate(paymentSuccessCallback);
                    IntPtr failurePtr = Marshal.GetFunctionPointerForDelegate(paymentFailureCallback);
                    IntPtr processingPtr = Marshal.GetFunctionPointerForDelegate(purchaseProcessingCallback);
                    IntPtr optinPtr = Marshal.GetFunctionPointerForDelegate(optinResponseCallback);

                    Debug.Log($"[WebViewLauncher] Callback function pointers: success={successPtr}, failure={failurePtr}, processing={processingPtr}, optin={optinPtr}");

                    // Register each callback
                    SetPaymentSuccessCallback(successPtr);
                    Debug.Log("[WebViewLauncher] ✓ PaymentSuccess callback registered");

                    SetPaymentFailureCallback(failurePtr);
                    Debug.Log("[WebViewLauncher] ✓ PaymentFailure callback registered");

                    SetPurchaseProcessingCallback(processingPtr);
                    Debug.Log("[WebViewLauncher] ✓ PurchaseProcessing callback registered");

                    SetOptinResponseCallback(optinPtr);
                    Debug.Log("[WebViewLauncher] ✓ OptinResponse callback registered");
                }
                catch (Exception callbackEx)
                {
                    Debug.LogError($"[WebViewLauncher] ❌ Failed to register callbacks: {callbackEx.Message}\n{callbackEx.StackTrace}");
                }

                // Set up notification polling as fallback
                SetupNotificationCenterListener();

                // Create window AFTER callbacks are registered
                Debug.Log("[WebViewLauncher] ========== CREATING WEBVIEW WINDOW ==========");
                Debug.Log($"[WebViewLauncher] URL: {url}");
                Debug.Log($"[WebViewLauncher] Rect: x={rect.x}, y={rect.y}, width={rect.width}, height={rect.height}");
                windowPtr = CreateWebViewWindow(rect.x, rect.y, rect.width, rect.height, url);

                if (windowPtr == IntPtr.Zero)
                {
                    Debug.LogError("[WebViewLauncher] ✗✗✗ CreateWebViewWindow returned null pointer ✗✗✗");
                    return false;
                }

                Debug.Log($"[WebViewLauncher] ✓✓✓ WebView window created successfully, handle={windowPtr} ✓✓✓");
                Debug.Log("[WebViewLauncher] ============================================");
                Debug.Log("[WebViewLauncher] NOTE: Check DebugView (dbgview.exe) for detailed C++ debug output");
                Debug.Log("[WebViewLauncher] Download DebugView from: https://docs.microsoft.com/en-us/sysinternals/downloads/debugview");
                
                return true;
            }
            catch (DllNotFoundException dllEx)
            {
                Debug.LogError($"WebViewLauncher: DLL not found: {dllEx.Message}");
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"WebViewLauncher: Failed to create window: {e.Message}\n{e.StackTrace}");
                return false;
            }
        }

        [MonoPInvokeCallback(typeof(PaymentSuccessCallback))]
        private static void OnPaymentSuccessInternal()
        {
            Debug.Log("[WebViewLauncher] OnPaymentSuccessInternal called");
            if (currentInstance != null)
            {
                Debug.Log("[WebViewLauncher] currentInstance found, dispatching to Unity main thread");
                // Dispatch to Unity main thread
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    Debug.Log("[WebViewLauncher] Invoking OnPaymentSuccess event");
                    currentInstance.OnPaymentSuccess?.Invoke();
                };
            }
            else
            {
                Debug.LogError("[WebViewLauncher] ERROR - currentInstance is null!");
            }
        }

        [MonoPInvokeCallback(typeof(PaymentFailureCallback))]
        private static void OnPaymentFailureInternal()
        {
            Debug.Log("[WebViewLauncher] OnPaymentFailureInternal called");
            if (currentInstance != null)
            {
                Debug.Log("[WebViewLauncher] currentInstance found, dispatching to Unity main thread");
                // Dispatch to Unity main thread
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    Debug.Log("[WebViewLauncher] Invoking OnPaymentFailure event");
                    currentInstance.OnPaymentFailure?.Invoke();
                };
            }
            else
            {
                Debug.LogError("[WebViewLauncher] ERROR - currentInstance is null!");
            }
        }

        [MonoPInvokeCallback(typeof(PurchaseProcessingCallback))]
        private static void OnPurchaseProcessingInternal()
        {
            Debug.Log("[WebViewLauncher] OnPurchaseProcessingInternal called");
            if (currentInstance != null)
            {
                Debug.Log("[WebViewLauncher] currentInstance found, dispatching to Unity main thread");
                // Dispatch to Unity main thread
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    Debug.Log("[WebViewLauncher] Invoking OnPurchaseProcessing event");
                    currentInstance.OnPurchaseProcessing?.Invoke();
                };
            }
            else
            {
                Debug.LogError("[WebViewLauncher] ERROR - currentInstance is null!");
            }
        }

        [MonoPInvokeCallback(typeof(OptinResponseCallback))]
        private static void OnOptinResponseInternal(IntPtr optinTypePtr)
        {
            Debug.Log("[WebViewLauncher] OnOptinResponseInternal called");
            if (currentInstance != null)
            {
                string optinType = "";
                if (optinTypePtr != IntPtr.Zero)
                {
                    optinType = Marshal.PtrToStringAnsi(optinTypePtr);
                }
                string finalOptinType = optinType ?? "";
                Debug.Log($"[WebViewLauncher] Optin type: {finalOptinType}, dispatching to Unity main thread");

                // Dispatch to Unity main thread
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    Debug.Log($"[WebViewLauncher] Invoking OnOptinResponse event with: {finalOptinType}");
                    currentInstance.OnOptinResponse?.Invoke(finalOptinType);
                };
            }
            else
            {
                Debug.LogError("[WebViewLauncher] ERROR - currentInstance is null!");
            }
        }

        private void SetupNotificationCenterListener()
        {
            try
            {
                // Use EditorApplication.update to poll for notifications and pump Windows messages
                if (!messagePumpActive)
                {
                    UnityEditor.EditorApplication.update += PollForNotifications;
                    messagePumpActive = true;
                    Debug.Log("[WebViewLauncher] Set up notification polling via EditorApplication.update");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WebViewLauncher] Failed to set up notification listener: {ex.Message}");
            }
        }

        private static void PollForNotifications()
        {
            // Pump Windows messages for WebView2 - CRITICAL for WebView2 to work
            // WebView2 requires continuous message pumping to function
            // Without this, the WebView will appear but won't render content
            try
            {
                // Pump messages multiple times per frame to ensure WebView2 gets enough processing time
                // WebView2 is very sensitive to message pump frequency
                for (int i = 0; i < 5; i++)
                {
                    PumpMessages();
                }
            }
            catch { }

            // Poll in-memory notification queue from native code
            if (currentInstance == null) return;

            try
            {
                System.Text.StringBuilder typeBuffer = new System.Text.StringBuilder(64);
                System.Text.StringBuilder dataBuffer = new System.Text.StringBuilder(192);

                // Poll until queue is empty
                while (PollNotification(typeBuffer, 64, dataBuffer, 192) != 0)
                {
                    string notificationType = typeBuffer.ToString();
                    string notificationData = dataBuffer.ToString();

                    UnityEditor.EditorApplication.delayCall += () =>
                    {
                        switch (notificationType)
                        {
                            case "StashPaymentSuccess":
                                currentInstance.OnPaymentSuccess?.Invoke();
                                break;
                            case "StashPaymentFailure":
                                currentInstance.OnPaymentFailure?.Invoke();
                                break;
                            case "StashPurchaseProcessing":
                                currentInstance.OnPurchaseProcessing?.Invoke();
                                break;
                            case "StashOptinResponse":
                                currentInstance.OnOptinResponse?.Invoke(notificationData);
                                break;
                        }
                    };

                    // Clear buffers for next iteration
                    typeBuffer.Clear();
                    dataBuffer.Clear();
                }
            }
            catch (System.Exception ex)
            {
                // Silently ignore errors
            }
        }

        public void DestroyWindow()
        {
            // Remove notification listener
            if (messagePumpActive)
            {
                UnityEditor.EditorApplication.update -= PollForNotifications;
                messagePumpActive = false;
            }

            if (windowPtr != IntPtr.Zero)
            {
                try
                {
                    DestroyWebViewWindow(windowPtr);
                }
                catch { }
                windowPtr = IntPtr.Zero;
            }

            // Clear instance reference
            if (currentInstance == this)
            {
                currentInstance = null;
            }
        }

        public void Dispose()
        {
            DestroyWindow();
        }
#else
        public bool CreateWindow(Rect rect, string url) { return false; }
        public void DestroyWindow() { }
        public void Dispose() { }
#endif
    }
}

