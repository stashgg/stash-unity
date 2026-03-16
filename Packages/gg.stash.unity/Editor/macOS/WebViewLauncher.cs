using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEditor;
using AOT;

namespace Stash.Editor.macOS
{
    /// <summary>
    /// Native webview launcher using compiled Objective-C code (macOS only)
    /// </summary>
    public class WebViewLauncher
    {
#if UNITY_EDITOR_OSX
        private const string BundleName = "WebViewLauncher.bundle";
        
        // Callback delegates - must be UnmanagedFunctionPointer for P/Invoke
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void PaymentSuccessCallback();
        
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void PaymentFailureCallback();
        
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void PurchaseProcessingCallback();
        
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void OptinResponseCallback(IntPtr optinType);
        
        // Load bundle via dlopen with full path so runtime never does load-by-name (which fails inside Unity project).
        // Delegates for window/poll functions resolved from bundle handle.
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr CreateWebViewWindowDelegate(double x, double y, double width, double height, string url);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void DestroyWebViewWindowDelegate(IntPtr windowPtr);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int PollNotificationDelegate(IntPtr typeBuffer, int typeBufferSize, IntPtr dataBuffer, int dataBufferSize);
        
        private static CreateWebViewWindowDelegate createWebViewWindowFunc;
        private static DestroyWebViewWindowDelegate destroyWebViewWindowFunc;
        private static PollNotificationDelegate pollNotificationFunc;
        
        // Use dlopen/dlsym to manually load functions from bundle (Unity DllImport doesn't work well with bundles)
        [DllImport("libdl.dylib", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr dlopen(string filename, int flag);
        
        [DllImport("libdl.dylib", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr dlsym(IntPtr handle, string symbol);
        
        [DllImport("libdl.dylib", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr dlerror();
        
        [DllImport("libdl.dylib", CallingConvention = CallingConvention.Cdecl)]
        private static extern int dlclose(IntPtr handle);
        
        // On macOS, RTLD_DEFAULT = (void*)-2: search all loaded images for symbol
        private static readonly IntPtr RTLD_DEFAULT = new IntPtr(-2);
        
        private delegate void SetPaymentSuccessCallbackDelegate(IntPtr callbackPtr);
        private delegate void SetPaymentFailureCallbackDelegate(IntPtr callbackPtr);
        private delegate void SetPurchaseProcessingCallbackDelegate(IntPtr callbackPtr);
        private delegate void SetOptinResponseCallbackDelegate(IntPtr callbackPtr);
        
        private static IntPtr bundleHandle = IntPtr.Zero;
        private static SetPaymentSuccessCallbackDelegate setPaymentSuccessCallbackFunc;
        private static SetPaymentFailureCallbackDelegate setPaymentFailureCallbackFunc;
        private static SetPurchaseProcessingCallbackDelegate setPurchaseProcessingCallbackFunc;
        private static SetOptinResponseCallbackDelegate setOptinResponseCallbackFunc;
        
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
        
        // Thread-safe queue: native callbacks run on macOS/WebKit thread; we must not touch Unity APIs there.
        // Enqueue here and drain on Unity main thread in EditorApplication.update.
        private enum PendingCallbackKind { PaymentSuccess, PaymentFailure, PurchaseProcessing, OptinResponse }
        private struct PendingCallback
        {
            public PendingCallbackKind Kind;
            public string OptinType;
        }
        private static readonly System.Collections.Generic.Queue<PendingCallback> pendingCallbacks = new System.Collections.Generic.Queue<PendingCallback>();
        private static readonly object pendingCallbacksLock = new object();
        private static bool drainHandlerRegistered = false;
        
        private IntPtr windowPtr = IntPtr.Zero;
        
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
                bool callbacksRegistered = false;
                try
                {
                    // Resolve bundle path: use AssetDatabase so we load the same file Unity uses (works with packages/symlinks)
                    string bundlePath = null;
                    string[] guids = AssetDatabase.FindAssets(System.IO.Path.GetFileNameWithoutExtension(BundleName));
                    foreach (string guid in guids)
                    {
                        string p = AssetDatabase.GUIDToAssetPath(guid);
                        if (p != null && p.EndsWith(BundleName, StringComparison.OrdinalIgnoreCase))
                        {
                            bundlePath = System.IO.Path.Combine(Application.dataPath, "..", p);
                            bundlePath = System.IO.Path.GetFullPath(bundlePath);
                            break;
                        }
                    }
                    if (string.IsNullOrEmpty(bundlePath))
                        bundlePath = System.IO.Path.Combine(Application.dataPath, "Stash.Popup", "Editor", "StashEditorPlugin", "macOS", BundleName);
                    Debug.Log($"[WebViewLauncher] Loading bundle from: {bundlePath}");
                    
                    // Try loading the bundle with RTLD_LAZY | RTLD_GLOBAL to make symbols visible
                    // RTLD_LAZY = 1, RTLD_GLOBAL = 8, so 1 | 8 = 9
                    bundleHandle = dlopen(bundlePath, 9); // RTLD_LAZY | RTLD_GLOBAL
                    
                    if (bundleHandle == IntPtr.Zero)
                    {
                        // Try with RTLD_NOW | RTLD_GLOBAL
                        bundleHandle = dlopen(bundlePath, 10); // RTLD_NOW | RTLD_GLOBAL
                        if (bundleHandle == IntPtr.Zero)
                        {
                            Debug.LogError($"[WebViewLauncher] ❌ Failed to load bundle: {bundlePath}");
                            IntPtr errorPtr = dlerror();
                            if (errorPtr != IntPtr.Zero)
                            {
                                string errorMsg = Marshal.PtrToStringAnsi(errorPtr);
                                Debug.LogError($"[WebViewLauncher] dlopen error: {errorMsg}");
                            }
                        }
                    }
                    
                    // Prefer the bundle we just loaded (bundleHandle); its symbols are guaranteed visible there.
                    // RTLD_DEFAULT may not see the bundle if Unity loaded it with RTLD_LOCAL.
                    IntPtr setSuccessPtr = IntPtr.Zero;
                    IntPtr setFailurePtr = IntPtr.Zero;
                    IntPtr setProcessingPtr = IntPtr.Zero;
                    IntPtr setOptinPtr = IntPtr.Zero;
                    IntPtr searchHandle = bundleHandle != IntPtr.Zero ? bundleHandle : RTLD_DEFAULT;
                    
                    // Try bundle handle first (macOS C symbols use leading underscore)
                    if (bundleHandle != IntPtr.Zero)
                    {
                        dlerror(); // clear previous error
                        Debug.Log($"[WebViewLauncher] Looking up symbols in bundle handle {bundleHandle}...");
                        setSuccessPtr = dlsym(bundleHandle, "_SetPaymentSuccessCallback");
                        setFailurePtr = dlsym(bundleHandle, "_SetPaymentFailureCallback");
                        setProcessingPtr = dlsym(bundleHandle, "_SetPurchaseProcessingCallback");
                        setOptinPtr = dlsym(bundleHandle, "_SetOptinResponseCallback");
                    }
                    
                    // Fallback: RTLD_DEFAULT (all loaded images) in case bundle was loaded by Unity elsewhere
                    if ((setSuccessPtr == IntPtr.Zero || setFailurePtr == IntPtr.Zero || setProcessingPtr == IntPtr.Zero || setOptinPtr == IntPtr.Zero))
                    {
                        dlerror();
                        Debug.Log("[WebViewLauncher] Fallback: trying RTLD_DEFAULT for callback symbols...");
                        if (setSuccessPtr == IntPtr.Zero) setSuccessPtr = dlsym(RTLD_DEFAULT, "_SetPaymentSuccessCallback");
                        if (setFailurePtr == IntPtr.Zero) setFailurePtr = dlsym(RTLD_DEFAULT, "_SetPaymentFailureCallback");
                        if (setProcessingPtr == IntPtr.Zero) setProcessingPtr = dlsym(RTLD_DEFAULT, "_SetPurchaseProcessingCallback");
                        if (setOptinPtr == IntPtr.Zero) setOptinPtr = dlsym(RTLD_DEFAULT, "_SetOptinResponseCallback");
                    }
                    
                    // Last resort: symbol names without leading underscore
                    if ((setSuccessPtr == IntPtr.Zero || setFailurePtr == IntPtr.Zero || setProcessingPtr == IntPtr.Zero || setOptinPtr == IntPtr.Zero))
                    {
                        dlerror();
                        Debug.Log("[WebViewLauncher] Fallback: trying symbol names without leading underscore...");
                        if (setSuccessPtr == IntPtr.Zero) setSuccessPtr = dlsym(searchHandle, "SetPaymentSuccessCallback");
                        if (setFailurePtr == IntPtr.Zero) setFailurePtr = dlsym(searchHandle, "SetPaymentFailureCallback");
                        if (setProcessingPtr == IntPtr.Zero) setProcessingPtr = dlsym(searchHandle, "SetPurchaseProcessingCallback");
                        if (setOptinPtr == IntPtr.Zero) setOptinPtr = dlsym(searchHandle, "SetOptinResponseCallback");
                    }
                    
                    // Resolve window/poll functions from bundle (must use same handle so we don't trigger load-by-name)
                    if (bundleHandle != IntPtr.Zero)
                    {
                        dlerror();
                        IntPtr createPtr = dlsym(bundleHandle, "_CreateWebViewWindow");
                        if (createPtr == IntPtr.Zero) createPtr = dlsym(bundleHandle, "CreateWebViewWindow");
                        IntPtr destroyPtr = dlsym(bundleHandle, "_DestroyWebViewWindow");
                        if (destroyPtr == IntPtr.Zero) destroyPtr = dlsym(bundleHandle, "DestroyWebViewWindow");
                        IntPtr pollPtr = dlsym(bundleHandle, "_PollNotification");
                        if (pollPtr == IntPtr.Zero) pollPtr = dlsym(bundleHandle, "PollNotification");
                        if (createPtr != IntPtr.Zero && destroyPtr != IntPtr.Zero && pollPtr != IntPtr.Zero)
                        {
                            createWebViewWindowFunc = Marshal.GetDelegateForFunctionPointer<CreateWebViewWindowDelegate>(createPtr);
                            destroyWebViewWindowFunc = Marshal.GetDelegateForFunctionPointer<DestroyWebViewWindowDelegate>(destroyPtr);
                            pollNotificationFunc = Marshal.GetDelegateForFunctionPointer<PollNotificationDelegate>(pollPtr);
                            Debug.Log("[WebViewLauncher] ✓ Window/poll functions resolved from bundle");
                        }
                        else
                            Debug.LogError("[WebViewLauncher] Failed to resolve CreateWebViewWindow/DestroyWebViewWindow/PollNotification from bundle");
                        Debug.Log($"[WebViewLauncher] ✓ Bundle loaded successfully, handle={bundleHandle}");
                    }
                    
                    Debug.Log($"[WebViewLauncher] Function pointers from dlsym: success={setSuccessPtr}, failure={setFailurePtr}, processing={setProcessingPtr}, optin={setOptinPtr}");
                    
                    if (setSuccessPtr != IntPtr.Zero && setFailurePtr != IntPtr.Zero && setProcessingPtr != IntPtr.Zero && setOptinPtr != IntPtr.Zero)
                    {
                        // Convert to delegates
                        setPaymentSuccessCallbackFunc = Marshal.GetDelegateForFunctionPointer<SetPaymentSuccessCallbackDelegate>(setSuccessPtr);
                        setPaymentFailureCallbackFunc = Marshal.GetDelegateForFunctionPointer<SetPaymentFailureCallbackDelegate>(setFailurePtr);
                        setPurchaseProcessingCallbackFunc = Marshal.GetDelegateForFunctionPointer<SetPurchaseProcessingCallbackDelegate>(setProcessingPtr);
                        setOptinResponseCallbackFunc = Marshal.GetDelegateForFunctionPointer<SetOptinResponseCallbackDelegate>(setOptinPtr);
                        
                        // Convert callback delegates to function pointers
                        IntPtr successPtr = Marshal.GetFunctionPointerForDelegate(paymentSuccessCallback);
                        IntPtr failurePtr = Marshal.GetFunctionPointerForDelegate(paymentFailureCallback);
                        IntPtr processingPtr = Marshal.GetFunctionPointerForDelegate(purchaseProcessingCallback);
                        IntPtr optinPtr = Marshal.GetFunctionPointerForDelegate(optinResponseCallback);
                        
                        Debug.Log($"[WebViewLauncher] Callback function pointers: success={successPtr}, failure={failurePtr}, processing={processingPtr}, optin={optinPtr}");
                        
                        // Register each callback
                        setPaymentSuccessCallbackFunc(successPtr);
                        Debug.Log("[WebViewLauncher] ✓ PaymentSuccess callback registered");
                        
                        setPaymentFailureCallbackFunc(failurePtr);
                        Debug.Log("[WebViewLauncher] ✓ PaymentFailure callback registered");
                        
                        setPurchaseProcessingCallbackFunc(processingPtr);
                        Debug.Log("[WebViewLauncher] ✓ PurchaseProcessing callback registered");
                        
                        setOptinResponseCallbackFunc(optinPtr);
                        Debug.Log("[WebViewLauncher] ✓ OptinResponse callback registered");
                        
                        callbacksRegistered = true;
                        Debug.Log("[WebViewLauncher] ✓ All callbacks registered successfully");
                    }
                    else
                    {
                        Debug.LogError("[WebViewLauncher] ❌ Failed to find callback functions in bundle");
                        IntPtr errorPtr = dlerror();
                        if (errorPtr != IntPtr.Zero)
                        {
                            string errorMsg = Marshal.PtrToStringAnsi(errorPtr);
                            Debug.LogError($"[WebViewLauncher] dlsym error: {errorMsg}");
                        }
                        else
                            Debug.LogError("[WebViewLauncher] Ensure WebViewLauncher.bundle was built from WebViewLauncher.mm (run build_webview.sh in the macOS folder).");
                    }
                }
                catch (Exception callbackEx)
                {
                    Debug.LogError($"[WebViewLauncher] ❌ Failed to register callbacks: {callbackEx.Message}\n{callbackEx.StackTrace}");
                }
                
                if (!callbacksRegistered)
                {
                    Debug.LogWarning("[WebViewLauncher] ⚠️ P/Invoke callbacks not registered - using NSNotificationCenter fallback");
                    // Set up NSNotificationCenter listener as fallback
                    SetupNotificationCenterListener();
                }
                
                // Create window AFTER callbacks are registered (use dlsym delegate so we never trigger DllImport load-by-name)
                if (createWebViewWindowFunc == null)
                {
                    Debug.LogError("WebViewLauncher: Bundle loaded but CreateWebViewWindow symbol not found. Rebuild WebViewLauncher.bundle (run build_webview.sh).");
                    return false;
                }
                Debug.Log("[WebViewLauncher] Creating webview window...");
                windowPtr = createWebViewWindowFunc(rect.x, rect.y, rect.width, rect.height, url);
                
                if (windowPtr == IntPtr.Zero)
                {
                    Debug.LogError("WebViewLauncher: CreateWebViewWindow returned null pointer");
                    return false;
                }
                
                if (!drainHandlerRegistered)
                {
                    UnityEditor.EditorApplication.update += DrainPendingCallbacks;
                    drainHandlerRegistered = true;
                }
                
                return true;
            }
            catch (DllNotFoundException dllEx)
            {
                Debug.LogError($"WebViewLauncher: Bundle not found: {dllEx.Message}");
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"WebViewLauncher: Failed to create window: {e.Message}\n{e.StackTrace}");
                return false;
            }
        }
        
        /// <summary>Called from native (macOS/WebKit thread). Do not use Unity/Editor APIs here — only enqueue for main-thread drain.</summary>
        [MonoPInvokeCallback(typeof(PaymentSuccessCallback))]
        private static void OnPaymentSuccessInternal()
        {
            lock (pendingCallbacksLock)
            {
                pendingCallbacks.Enqueue(new PendingCallback { Kind = PendingCallbackKind.PaymentSuccess, OptinType = null });
            }
        }
        
        [MonoPInvokeCallback(typeof(PaymentFailureCallback))]
        private static void OnPaymentFailureInternal()
        {
            lock (pendingCallbacksLock)
            {
                pendingCallbacks.Enqueue(new PendingCallback { Kind = PendingCallbackKind.PaymentFailure, OptinType = null });
            }
        }
        
        [MonoPInvokeCallback(typeof(PurchaseProcessingCallback))]
        private static void OnPurchaseProcessingInternal()
        {
            lock (pendingCallbacksLock)
            {
                pendingCallbacks.Enqueue(new PendingCallback { Kind = PendingCallbackKind.PurchaseProcessing, OptinType = null });
            }
        }
        
        /// <summary>Called from native. optinTypePtr is valid only for this call (native must pass a copied string).</summary>
        [MonoPInvokeCallback(typeof(OptinResponseCallback))]
        private static void OnOptinResponseInternal(IntPtr optinTypePtr)
        {
            string optinType = (optinTypePtr != IntPtr.Zero) ? (Marshal.PtrToStringAnsi(optinTypePtr) ?? "") : "";
            lock (pendingCallbacksLock)
            {
                pendingCallbacks.Enqueue(new PendingCallback { Kind = PendingCallbackKind.OptinResponse, OptinType = optinType });
            }
        }
        
        /// <summary>Runs on Unity main thread (EditorApplication.update). Drains pending callbacks and invokes events.</summary>
        private static void DrainPendingCallbacks()
        {
            if (currentInstance == null) return;
            PendingCallback[] batch = null;
            lock (pendingCallbacksLock)
            {
                if (pendingCallbacks.Count == 0) return;
                batch = pendingCallbacks.ToArray();
                pendingCallbacks.Clear();
            }
            foreach (var cb in batch)
            {
                var inst = currentInstance;
                if (inst == null) continue;
                try
                {
                    switch (cb.Kind)
                    {
                        case PendingCallbackKind.PaymentSuccess:
                            inst.OnPaymentSuccess?.Invoke();
                            break;
                        case PendingCallbackKind.PaymentFailure:
                            inst.OnPaymentFailure?.Invoke();
                            break;
                        case PendingCallbackKind.PurchaseProcessing:
                            inst.OnPurchaseProcessing?.Invoke();
                            break;
                        case PendingCallbackKind.OptinResponse:
                            inst.OnOptinResponse?.Invoke(cb.OptinType ?? "");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }
        
        private static System.Collections.Generic.Queue<string> pendingNotifications = new System.Collections.Generic.Queue<string>();
        private static System.Collections.Generic.Queue<string> pendingOptinTypes = new System.Collections.Generic.Queue<string>();
        private static bool notificationPollingActive = false;
        
        private void SetupNotificationCenterListener()
        {
            try
            {
                // Use EditorApplication.update to poll for notifications
                if (!notificationPollingActive)
                {
                    UnityEditor.EditorApplication.update += PollForNotifications;
                    notificationPollingActive = true;
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
            // Poll in-memory notification queue from native code
            if (currentInstance == null || pollNotificationFunc == null) return;
            
            try
            {
                IntPtr typeBuf = Marshal.AllocHGlobal(64);
                IntPtr dataBuf = Marshal.AllocHGlobal(192);
                try
                {
                    while (pollNotificationFunc(typeBuf, 64, dataBuf, 192) != 0)
                    {
                        string notificationType = Marshal.PtrToStringAnsi(typeBuf) ?? "";
                        string notificationData = Marshal.PtrToStringAnsi(dataBuf) ?? "";
                        lock (pendingCallbacksLock)
                        {
                            pendingCallbacks.Enqueue(new PendingCallback
                            {
                                Kind = notificationType == "StashPaymentSuccess" ? PendingCallbackKind.PaymentSuccess
                                    : notificationType == "StashPaymentFailure" ? PendingCallbackKind.PaymentFailure
                                    : notificationType == "StashPurchaseProcessing" ? PendingCallbackKind.PurchaseProcessing
                                    : PendingCallbackKind.OptinResponse,
                                OptinType = notificationType == "StashOptinResponse" ? notificationData : null
                            });
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(typeBuf);
                    Marshal.FreeHGlobal(dataBuf);
                }
            }
            catch (System.Exception)
            {
                // Silently ignore errors
            }
        }
        
        // Public method to be called from native code via a simpler mechanism
        public static void HandleNotification(string notificationName, string optinType = null)
        {
            lock (pendingNotifications)
            {
                pendingNotifications.Enqueue(notificationName);
                if (optinType != null)
                {
                    lock (pendingOptinTypes)
                    {
                        pendingOptinTypes.Enqueue(optinType);
                    }
                }
            }
        }
        
        public void DestroyWindow()
        {
            if (drainHandlerRegistered)
            {
                UnityEditor.EditorApplication.update -= DrainPendingCallbacks;
                drainHandlerRegistered = false;
            }
            
            // Remove notification listener
            if (notificationPollingActive)
            {
                UnityEditor.EditorApplication.update -= PollForNotifications;
                notificationPollingActive = false;
            }
            
            if (windowPtr != IntPtr.Zero)
            {
                try
                {
                    if (destroyWebViewWindowFunc != null)
                        destroyWebViewWindowFunc(windowPtr);
                }
                catch { }
                windowPtr = IntPtr.Zero;
            }
            
            // Clear instance reference
            if (currentInstance == this)
            {
                currentInstance = null;
            }
            
            // Do not dlclose the bundle: native may still invoke callbacks (e.g. late dispatch_async).
            // Unloading would cause use-after-unload and crash. Bundle handle is left open for session lifetime.
            bundleHandle = IntPtr.Zero;
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

