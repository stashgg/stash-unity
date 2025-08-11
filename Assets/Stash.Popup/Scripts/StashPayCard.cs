using System;
using UnityEngine;
using System.Runtime.InteropServices;
using AOT;

namespace StashPopup
{
    /// <summary>
    /// Provides a cross-platform wrapper for opening Stash Pay URLs.
    /// On iOS, opens a specialized StashPayCard with callback support.
    /// On Android, opens a custom WebView dialog with payment callbacks.
    /// On other platforms, falls back to opening the URL in the default browser.
    /// </summary>
    public class StashPayCard : MonoBehaviour
    {
        #region Singleton Implementation
        private static StashPayCard _instance;

        public static StashPayCard Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("StashPayCard");
                    _instance = go.AddComponent<StashPayCard>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }
        #endregion

        #region Events
        /// <summary>
        /// Event triggered when the Safari view is dismissed.
        /// </summary>
        public event Action OnSafariViewDismissed;

        /// <summary>
        /// Event triggered when a payment succeeds.
        /// </summary>
        public event Action OnPaymentSuccess;

        /// <summary>
        /// Event triggered when a payment fails.
        /// </summary>
        public event Action OnPaymentFailure;
        #endregion

        #region Private Fields
        // Card configuration values with defaults. 
        private float _cardHeightRatio = 0.6f; // Default: 60% of screen height
        private float _cardVerticalPosition = 1.0f; // Default: bottom of screen
        private float _cardWidthRatio = 1.0f; // Default: 100% of screen width
        #endregion

        #region Native Plugin Interface

#if UNITY_ANDROID && !UNITY_EDITOR
        // Android plugin interface
        private AndroidJavaClass androidPlugin;
        private AndroidJavaObject androidPluginInstance;
        
        private void InitializeAndroidPlugin()
        {
            if (androidPlugin == null)
            {
                try
                {
                    androidPlugin = new AndroidJavaClass("com.stash.popup.StashPayCardPlugin");
                    androidPluginInstance = androidPlugin.CallStatic<AndroidJavaObject>("getInstance");
                    Debug.Log("StashPayCard: Android plugin initialized");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"StashPayCard: Failed to initialize Android plugin: {e.Message}");
                }
            }
        }
        
        // Android callback methods (called by Unity message system)
        public void OnAndroidPaymentSuccess(string message)
        {
            Debug.Log("[StashPayCard] Android payment success callback received");
            OnPaymentSuccess?.Invoke();
        }
        
        public void OnAndroidPaymentFailure(string message)
        {
            Debug.Log("[StashPayCard] Android payment failure callback received");
            OnPaymentFailure?.Invoke();
        }
        
        public void OnAndroidDialogDismissed(string message)
        {
            Debug.Log("[StashPayCard] Android dialog dismissed callback received");
            OnSafariViewDismissed?.Invoke();
        }

#elif UNITY_IOS && !UNITY_EDITOR
        // Delegate types for iOS callbacks
        private delegate void SafariViewDismissedCallback();
        private delegate void PaymentSuccessCallback();
        private delegate void PaymentFailureCallback();
        
        // Import the native iOS plugin functions
        [DllImport("__Internal")]
        private static extern void _StashPayCardOpenURLInSafariVC(string url);
        
        [DllImport("__Internal")]
        private static extern void _StashPayCardSetSafariViewDismissedCallback(SafariViewDismissedCallback callback);
        
        [DllImport("__Internal")]
        private static extern void _StashPayCardSetPaymentSuccessCallback(PaymentSuccessCallback callback);
        
        [DllImport("__Internal")]
        private static extern void _StashPayCardSetPaymentFailureCallback(PaymentFailureCallback callback);
        
        [DllImport("__Internal")]
        private static extern void _StashPayCardSetCardConfiguration(float heightRatio, float verticalPosition);
        
        [DllImport("__Internal")]
        private static extern void _StashPayCardSetCardConfigurationWithWidth(float heightRatio, float verticalPosition, float widthRatio);
        
        [DllImport("__Internal")]
        private static extern void _StashPayCardResetPresentationState();
        
        [DllImport("__Internal")]
        private static extern bool _StashPayCardIsCurrentlyPresented();

        // Force the use of native SFSafariViewController
        [DllImport("__Internal")]
        private static extern void _StashPayCardSetForceSafariViewController(bool force);

        // Check current setting
        [DllImport("__Internal")]
        private static extern bool _StashPayCardGetForceSafariViewController();
#endif

        #endregion

        #region Public Methods

        /// <summary>
        /// Applies the current card configuration settings to the native plugin.
        /// Called automatically when settings are changed.
        /// </summary>
        private void ApplyCardConfiguration()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (androidPluginInstance != null)
            {
                try
                {
                    androidPluginInstance.Call("setCardConfiguration", _cardHeightRatio, _cardVerticalPosition, _cardWidthRatio);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"StashPayCard: Android config error: {e.Message}");
                }
            }
#elif UNITY_IOS && !UNITY_EDITOR
            _StashPayCardSetCardConfigurationWithWidth(_cardHeightRatio, _cardVerticalPosition, _cardWidthRatio);
#endif
        }

        /// <summary>
        /// Opens a URL in the appropriate platform-specific browser view.
        /// On iOS, opens the URL in StashPayCard with Safari/WebView and supports callbacks.
        /// On Android, opens the URL in a custom WebView dialog with payment callbacks.
        /// On other platforms and editor, simply opens the URL in the default browser as a fallback.
        /// </summary>
        /// <param name="url">Stash Pay URL to open</param>
        /// <param name="dismissCallback">Callback triggered when the browser view is dismissed.</param>
        /// <param name="successCallback">Callback triggered when payment succeeds.</param>
        /// <param name="failureCallback">Callback triggered when payment fails.</param>
        public void OpenURL(string url, Action dismissCallback = null, Action successCallback = null, Action failureCallback = null)
        {
            if (string.IsNullOrEmpty(url))
            {
                Debug.LogError("StashPayCard: URL is null or empty");
                return;
            }

            // Ensure URL is properly formatted
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                url = "https://" + url;
            }

            Debug.Log($"StashPayCard: Opening URL {url}");

            // Clear previous callbacks to prevent multiple registrations
            OnSafariViewDismissed = null;
            OnPaymentSuccess = null;
            OnPaymentFailure = null;
            
            if (dismissCallback != null)
            {
                OnSafariViewDismissed += dismissCallback;
            }

            if (successCallback != null)
            {
                OnPaymentSuccess += successCallback;
            }

            if (failureCallback != null)
            {
                OnPaymentFailure += failureCallback;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            // Android implementation using WebView dialog
            InitializeAndroidPlugin();
            
            if (androidPluginInstance != null)
            {
                try
                {
                    // Apply card configuration
                    androidPluginInstance.Call("setCardConfiguration", _cardHeightRatio, _cardVerticalPosition, _cardWidthRatio);
                    
                    // Open URL in Android WebView dialog
                    androidPluginInstance.Call("openURL", url);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"StashPayCard: Android plugin error: {e.Message}");
                    // Fallback to default browser
                    Application.OpenURL(url);
                }
            }
            else
            {
                Debug.LogWarning("StashPayCard: Android plugin not available, falling back to default browser");
                Application.OpenURL(url);
            }
#elif UNITY_IOS && !UNITY_EDITOR
                      
            ApplyCardConfiguration();

            // Always re-register the native iOS callbacks to ensure they're properly hooked up
            // This is safe to do multiple times and ensures callbacks work on every call
            _StashPayCardSetSafariViewDismissedCallback(OnIOSSafariViewDismissed);
            _StashPayCardSetPaymentSuccessCallback(OnIOSPaymentSuccess);
            _StashPayCardSetPaymentFailureCallback(OnIOSPaymentFailure);

            // Open the URL using the native iOS plugin
            // The decision to use SFSafariViewController vs WKWebView is now controlled by the ForceSafariViewController property
            _StashPayCardOpenURLInSafariVC(url);
#else
            // For other platforms (Editor, etc.), just open in default browser without callbacks
            Application.OpenURL(url);
#endif
        }

        /// <summary>
        /// Resets the card presentation state. Useful for debugging or force resetting the card state.
        /// This will dismiss any currently presented card and reset internal flags.
        /// </summary>
        public void ResetPresentationState()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (androidPluginInstance != null)
            {
                try
                {
                    androidPluginInstance.Call("resetPresentationState");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"StashPayCard: Android reset error: {e.Message}");
                }
            }
#elif UNITY_IOS && !UNITY_EDITOR
            _StashPayCardResetPresentationState();
#endif
        }

        /// <summary>
        /// Gets whether a card is currently being presented.
        /// </summary>
        public bool IsCurrentlyPresented
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                if (androidPluginInstance != null)
                {
                    try
                    {
                        return androidPluginInstance.Call<bool>("isCurrentlyPresented");
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"StashPayCard: Android isPresented error: {e.Message}");
                        return false;
                    }
                }
                return false;
#elif UNITY_IOS && !UNITY_EDITOR
                return _StashPayCardIsCurrentlyPresented();
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Gets or sets whether to force the use of native browser components.
        /// On iOS: Uses SFSafariViewController over WKWebView for full-screen browser experience.
        /// On Android: Uses Chrome Custom Tabs over WebView dialog for native browser experience.
        /// When false (default), uses custom card UI implementations.
        /// </summary>
        public bool ForceSafariViewController
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                if (androidPluginInstance != null)
                {
                    try
                    {
                        return androidPluginInstance.Call<bool>("getForceSafariViewController");
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"StashPayCard: Android getForceSafariViewController error: {e.Message}");
                        return false;
                    }
                }
                return false;
#elif UNITY_IOS && !UNITY_EDITOR
                return _StashPayCardGetForceSafariViewController();
#else
                return false;
#endif
            }
            set
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                if (androidPluginInstance != null)
                {
                    try
                    {
                        androidPluginInstance.Call("setForceSafariViewController", value);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"StashPayCard: Android setForceSafariViewController error: {e.Message}");
                    }
                }
#elif UNITY_IOS && !UNITY_EDITOR
                _StashPayCardSetForceSafariViewController(value);
#endif
            }
        }

        /// <summary>
        /// Gets or sets whether to use the card drawer experience on Android (iOS-style sliding card).
        /// When true, displays a card that slides up from the bottom with drag-to-expand/collapse.
        /// When false, uses the traditional dialog experience.
        /// Default: true (card drawer enabled) - requires Material Components library
        /// Android only - ignored on other platforms.
        /// Note: Automatically falls back to traditional dialog if Material Components is not available.
        /// </summary>
        public bool UseCardDrawer
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                if (androidPluginInstance != null)
                {
                    try
                    {
                        return androidPluginInstance.Call<bool>("getUseCardDrawer");
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"StashPayCard: Android getUseCardDrawer error: {e.Message}");
                        return true; // Default to card drawer
                    }
                }
                return true; // Default to card drawer
#else
                return false; // Not applicable on other platforms
#endif
            }
            set
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                if (androidPluginInstance != null)
                {
                    try
                    {
                        androidPluginInstance.Call("setUseCardDrawer", value);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"StashPayCard: Android setUseCardDrawer error: {e.Message}");
                    }
                }
#endif
            }
        }

        /// <summary>
        /// Sets the card height as a ratio of screen height (0.0 to 1.0).
        /// Default is 0.6 (60% of screen height).
        /// </summary>
        public float CardHeightRatio
        {
            get { return _cardHeightRatio; }
            set 
            { 
                _cardHeightRatio = Mathf.Clamp01(value);
                ApplyCardConfiguration();
            }
        }

        /// <summary>
        /// Sets the card vertical position (0.0 = top, 1.0 = bottom, 0.5 = center).
        /// Default is 1.0 (bottom of screen).
        /// </summary>
        public float CardVerticalPosition
        {
            get { return _cardVerticalPosition; }
            set 
            { 
                _cardVerticalPosition = Mathf.Clamp01(value);
                ApplyCardConfiguration();
            }
        }

        /// <summary>
        /// Sets the card width as a ratio of screen width (0.0 to 1.0).
        /// Default is 1.0 (100% of screen width).
        /// </summary>
        public float CardWidthRatio
        {
            get { return _cardWidthRatio; }
            set 
            { 
                _cardWidthRatio = Mathf.Clamp01(value);
                ApplyCardConfiguration();
            }
        }

        #endregion

        #region Private Methods

#if UNITY_IOS && !UNITY_EDITOR
        /// <summary>
        /// Unity callback method called from iOS when Safari view is dismissed.
        /// </summary>
        [MonoPInvokeCallback(typeof(SafariViewDismissedCallback))]
        private static void OnIOSSafariViewDismissed()
        {
            if (Instance != null)
            {
                Instance.OnSafariViewDismissed?.Invoke();
            }
        }
        
        /// <summary>
        /// Unity callback method called from iOS when payment succeeds.
        /// </summary>
        [MonoPInvokeCallback(typeof(PaymentSuccessCallback))]
        private static void OnIOSPaymentSuccess()
        {
            Debug.Log("[StashPayCard] Payment success callback received from iOS");
            
            if (Instance != null)
            {
                Instance.OnPaymentSuccess?.Invoke();
            }
            else
            {
                Debug.LogWarning("[StashPayCard] Payment success callback received but Instance is null");
            }
        }

        /// <summary>
        /// Unity callback method called from iOS when payment fails.
        /// </summary>
        [MonoPInvokeCallback(typeof(PaymentFailureCallback))]
        private static void OnIOSPaymentFailure()
        {
            Debug.Log("[StashPayCard] Payment failure callback received from iOS");
            
            if (Instance != null)
            {
                Instance.OnPaymentFailure?.Invoke();
            }
            else
            {
                Debug.LogWarning("[StashPayCard] Payment failure callback received but Instance is null");
            }
        }
#endif

        #endregion
    }
}