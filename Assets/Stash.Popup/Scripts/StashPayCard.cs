using System;
using UnityEngine;
using System.Runtime.InteropServices;
using System.Collections;
using AOT;

namespace StashPopup
{
    /// <summary>
    /// Configuration for the modal presentation (openModal). Matches the native SDK ModalConfig.
    /// </summary>
    public struct StashPayModalConfig
    {
        public bool showDragBar;
        public bool allowDismiss;
        public float phoneWidthRatioPortrait;
        public float phoneHeightRatioPortrait;
        public float phoneWidthRatioLandscape;
        public float phoneHeightRatioLandscape;
        public float tabletWidthRatioPortrait;
        public float tabletHeightRatioPortrait;
        public float tabletWidthRatioLandscape;
        public float tabletHeightRatioLandscape;

        /// <summary>Default modal config (drag bar and dismiss enabled, default size ratios).</summary>
        public static StashPayModalConfig Default => new StashPayModalConfig
        {
            showDragBar = true,
            allowDismiss = true,
            phoneWidthRatioPortrait = 0.8f,
            phoneHeightRatioPortrait = 0.5f,
            phoneWidthRatioLandscape = 0.5f,
            phoneHeightRatioLandscape = 0.8f,
            tabletWidthRatioPortrait = 0.4f,
            tabletHeightRatioPortrait = 0.3f,
            tabletWidthRatioLandscape = 0.3f,
            tabletHeightRatioLandscape = 0.4f
        };
    }

    /// <summary>
    /// Legacy popup size configuration (multipliers). Use StashPayModalConfig for new code.
    /// </summary>
    public struct PopupSizeConfig
    {
        public float portraitWidthMultiplier;
        public float portraitHeightMultiplier;
        public float landscapeWidthMultiplier;
        public float landscapeHeightMultiplier;
    }

    /// <summary>
    /// Cross-platform wrapper for Stash Pay in-app checkout.
    /// </summary>
    public class StashPayCard : MonoBehaviour
    {
        #region Singleton Implementation
        private static StashPayCard _instance;

        /// <summary>
        /// Gets the singleton instance of StashPayCard.
        /// 
        /// This property provides access to the single instance of StashPayCard in your application.
        /// If no instance exists, one will be automatically created as a persistent GameObject that
        /// survives scene loads.
        /// 
        /// Use this property to access StashPayCard functionality throughout your application:
        /// <code>
        /// StashPayCard.Instance.OpenCheckout(url, onDismiss, onSuccess, onFailure);
        /// </code>
        /// </summary>
        /// <value>The singleton StashPayCard instance.</value>
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
        
        /// <summary>
        /// Unity lifecycle callback invoked when the application is paused or resumed.
        /// On Android, optionally update the activity reference when resuming (handled by bridge on next open).
        /// </summary>
        private void OnApplicationPause(bool pauseStatus)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!pauseStatus)
            {
                try
                {
                    var activity = GetUnityActivity();
                    if (activity != null && androidPluginInstance != null)
                        androidPluginInstance.Call("setActivity", activity);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"StashPayCard: Failed to set activity on resume: {e.Message}");
                }
            }
#endif
        }
        #endregion

        #region Events
        /// <summary>Fired when the checkout or modal dialog is dismissed by the user.</summary>
        public event Action OnSafariViewDismissed;
        /// <summary>Fired when the user completes a payment successfully.</summary>
        public event Action OnPaymentSuccess;
        /// <summary>Fired when a payment fails.</summary>
        public event Action OnPaymentFailure;
        /// <summary>Fired when an opt-in response is received (e.g. payment channel selection).</summary>
        public event Action<string> OnOptinResponse;
        /// <summary>Fired when the page finishes loading, with load time in milliseconds.</summary>
        public event Action<double> OnPageLoaded;
        /// <summary>Fired when the initial page load fails (no connection, 4xx/5xx, timeout). Dialog is auto-dismissed; OnSafariViewDismissed is not called.</summary>
        public event Action OnNetworkError;
        /// <summary>
        /// Event fired when an unhandled exception occurs during native plugin operations.
        /// Subscribe to this event to be notified of exceptions that occur when calling native methods.
        /// 
        /// Platform Notes:
        /// - Android: Java exceptions are catchable and will trigger this event.
        /// - iOS: Objective-C exceptions/crashes are NOT catchable and will crash the app before this event fires.
        ///   This event only fires for exceptions that occur during the P/Invoke marshalling itself.
        /// </summary>
        public event Action<string, Exception> OnNativeException;
        #endregion

        #region Private Fields
        private bool _forcePortraitOnCheckout = false;
        private float _cardHeightRatioPortrait = 0.68f;
        private float _cardWidthRatioLandscape = 0.9f;
        private float _cardHeightRatioLandscape = 0.6f;
        private float _tabletWidthRatioPortrait = 0.4f;
        private float _tabletHeightRatioPortrait = 0.5f;
        private float _tabletWidthRatioLandscape = 0.3f;
        private float _tabletHeightRatioLandscape = 0.6f;
        #endregion

        #region Native Plugin Interface

#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject androidPluginInstance;

        private static AndroidJavaObject GetUnityActivity()
        {
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    return unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                }
            }
            catch { return null; }
        }

        private void InitializeAndroidPlugin()
        {
            if (androidPluginInstance != null) return;
            try
            {
                var bridgeClass = new AndroidJavaClass("com.stash.popup.StashPayCardUnityBridge");
                androidPluginInstance = bridgeClass.CallStatic<AndroidJavaObject>("getInstance");
                var activity = GetUnityActivity();
                if (activity != null)
                    androidPluginInstance.Call("setActivity", activity);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"StashPayCard: Failed to initialize Android bridge: {e.Message}");
            }
        }

        public void OnAndroidPaymentSuccess(string message) => OnPaymentSuccess?.Invoke();
        public void OnAndroidPaymentFailure(string message) => OnPaymentFailure?.Invoke();
        public void OnAndroidDialogDismissed(string message) => OnSafariViewDismissed?.Invoke();
        public void OnAndroidOptinResponse(string optinType) => OnOptinResponse?.Invoke(optinType ?? "");
        public void OnAndroidNetworkError(string message) => OnNetworkError?.Invoke();

        public void OnAndroidPageLoaded(string loadTimeMs)
        {
            if (double.TryParse(loadTimeMs, out double loadTime))
                OnPageLoaded?.Invoke(loadTime);
        }

#elif UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void _StashPayCardBridgeOpenCheckout(string url);
        [DllImport("__Internal")] private static extern void _StashPayCardBridgeOpenModal(string url);
        [DllImport("__Internal")] private static extern void _StashPayCardBridgeOpenModalWithConfig(string url, bool showDragBar, bool allowDismiss, float phoneWPortrait, float phoneHPortrait, float phoneWLandscape, float phoneHLandscape, float tabletWPortrait, float tabletHPortrait, float tabletWLandscape, float tabletHLandscape);
        [DllImport("__Internal")] private static extern void _StashPayCardBridgeDismiss();
        [DllImport("__Internal")] private static extern void _StashPayCardBridgeResetPresentationState();
        [DllImport("__Internal")] private static extern bool _StashPayCardBridgeIsCurrentlyPresented();
        [DllImport("__Internal")] private static extern bool _StashPayCardBridgeIsPurchaseProcessing();
        [DllImport("__Internal")] private static extern void _StashPayCardBridgeSetForcePortraitOnCheckout(bool force);
        [DllImport("__Internal")] private static extern void _StashPayCardBridgeSetCardHeightRatioPortrait(float value);
        [DllImport("__Internal")] private static extern void _StashPayCardBridgeSetCardWidthRatioLandscape(float value);
        [DllImport("__Internal")] private static extern void _StashPayCardBridgeSetCardHeightRatioLandscape(float value);
        [DllImport("__Internal")] private static extern void _StashPayCardBridgeSetTabletWidthRatioPortrait(float value);
        [DllImport("__Internal")] private static extern void _StashPayCardBridgeSetTabletHeightRatioPortrait(float value);
        [DllImport("__Internal")] private static extern void _StashPayCardBridgeSetTabletWidthRatioLandscape(float value);
        [DllImport("__Internal")] private static extern void _StashPayCardBridgeSetTabletHeightRatioLandscape(float value);
        [DllImport("__Internal")] private static extern void _StashPayCardBridgeSetForceWebBasedCheckout(bool force);
        [DllImport("__Internal")] private static extern bool _StashPayCardBridgeGetForceWebBasedCheckout();
        [DllImport("__Internal")] private static extern void _StashPayCardBridgeDismissSafariViewController();
        [DllImport("__Internal")] private static extern void _StashPayCardBridgeDismissSafariViewControllerWithResult(bool success);

        public void OnIOSPaymentSuccess() => OnPaymentSuccess?.Invoke();
        public void OnIOSPaymentFailure() => OnPaymentFailure?.Invoke();
        public void OnIOSDialogDismissed() => OnSafariViewDismissed?.Invoke();
        public void OnIOSOptinResponse(string optinType) => OnOptinResponse?.Invoke(optinType ?? "");
        public void OnIOSNetworkError() => OnNetworkError?.Invoke();
        public void OnIOSPageLoaded(string loadTimeMsStr)
        {
            if (double.TryParse(loadTimeMsStr, out double loadTime))
                OnPageLoaded?.Invoke(loadTime);
        }
#endif

        #endregion

        #region Public Methods

        private void SyncForceWebBasedCheckoutToNative()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (androidPluginInstance != null)
                androidPluginInstance.Call("setForceWebBasedCheckout", ForceWebBasedCheckout);
#elif UNITY_IOS && !UNITY_EDITOR
            _StashPayCardBridgeSetForceWebBasedCheckout(ForceWebBasedCheckout);
#endif
        }

        private void ApplyCheckoutConfigToNative()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (androidPluginInstance == null) return;
            try
            {
                androidPluginInstance.Call("setForcePortraitOnCheckout", _forcePortraitOnCheckout);
                androidPluginInstance.Call("setCardHeightRatioPortrait", _cardHeightRatioPortrait);
                androidPluginInstance.Call("setCardWidthRatioLandscape", _cardWidthRatioLandscape);
                androidPluginInstance.Call("setCardHeightRatioLandscape", _cardHeightRatioLandscape);
                androidPluginInstance.Call("setTabletWidthRatioPortrait", _tabletWidthRatioPortrait);
                androidPluginInstance.Call("setTabletHeightRatioPortrait", _tabletHeightRatioPortrait);
                androidPluginInstance.Call("setTabletWidthRatioLandscape", _tabletWidthRatioLandscape);
                androidPluginInstance.Call("setTabletHeightRatioLandscape", _tabletHeightRatioLandscape);
            }
            catch (System.Exception e) { HandleNativeException("ApplyCheckoutConfigToNative", e); }
#elif UNITY_IOS && !UNITY_EDITOR
            try
            {
                _StashPayCardBridgeSetForcePortraitOnCheckout(_forcePortraitOnCheckout);
                _StashPayCardBridgeSetCardHeightRatioPortrait(_cardHeightRatioPortrait);
                _StashPayCardBridgeSetCardWidthRatioLandscape(_cardWidthRatioLandscape);
                _StashPayCardBridgeSetCardHeightRatioLandscape(_cardHeightRatioLandscape);
                _StashPayCardBridgeSetTabletWidthRatioPortrait(_tabletWidthRatioPortrait);
                _StashPayCardBridgeSetTabletHeightRatioPortrait(_tabletHeightRatioPortrait);
                _StashPayCardBridgeSetTabletWidthRatioLandscape(_tabletWidthRatioLandscape);
                _StashPayCardBridgeSetTabletHeightRatioLandscape(_tabletHeightRatioLandscape);
            }
            catch (System.Exception e) { HandleNativeException("ApplyCheckoutConfigToNative", e); }
#endif
        }

        /// <summary>
        /// Opens a Stash Pay checkout URL in a in-game dialog card.
        ///
        /// This method displays a Stash Pay checkout page using the native presentation for your platform.
        /// - On iOS and Android devices, this shows the checkout UI as an animated card sliding up from the bottom.
        /// - On desktop in the Unity Editor (when using the StashPayCardEditor package), this opens a simulated test window.
        ///
        /// <param name="url">The Stash Pay checkout URL to load. (Must be HTTPS)</param>
        /// <param name="dismissCallback">Called when the checkout card is dismissed/closed.</param>
        /// <param name="successCallback">Called if the purchase completes successfully.</param>
        /// <param name="failureCallback">Called if the purchase fails or errors.</param>
        /// 
        /// Usage example:
        /// <code>
        /// StashPayCard.Instance.OpenCheckout(
        ///     "https://your-stash-pay-checkout-link.com",
        ///     () => Debug.Log("Dismissed"),
        ///     () => Debug.Log("Success"),
        ///     () => Debug.Log("Failed")
        /// );
        /// </code>
        /// </summary>
        public void OpenCheckout(string url, Action dismissCallback = null, Action successCallback = null, Action failureCallback = null)
        {
            StartCoroutine(OpenURLInternal(url, dismissCallback, successCallback, failureCallback, false));
        }
        
        // Internal handler: opens URL as checkout or modal/popup
        private IEnumerator OpenURLInternal(string url, Action dismissCallback, Action successCallback, Action failureCallback, bool isPopup, PopupSizeConfig? customSize = null, StashPayModalConfig? modalConfig = null)
        {
            if (string.IsNullOrEmpty(url)) yield break;

            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                url = "https://" + url;
            }

            OnSafariViewDismissed = dismissCallback;
            OnPaymentSuccess = successCallback;
            OnPaymentFailure = failureCallback;

            // Popup mode always uses custom implementation - temporarily disable ForceWebBasedCheckout
            bool originalForceSafari = false;
            if (isPopup)
            {
                originalForceSafari = ForceWebBasedCheckout;
                if (originalForceSafari)
            {
                    ForceWebBasedCheckout = false;
                }
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            InitializeAndroidPlugin();
            if (androidPluginInstance != null)
            {
                try
                {
                    SyncForceWebBasedCheckoutToNative();
                    if (isPopup)
                    {
                        if (modalConfig.HasValue)
                        {
                            var c = modalConfig.Value;
                            androidPluginInstance.Call("openModalWithConfig", url, c.showDragBar, c.allowDismiss,
                                c.phoneWidthRatioPortrait, c.phoneHeightRatioPortrait, c.phoneWidthRatioLandscape, c.phoneHeightRatioLandscape,
                                c.tabletWidthRatioPortrait, c.tabletHeightRatioPortrait, c.tabletWidthRatioLandscape, c.tabletHeightRatioLandscape);
                        }
                        else if (customSize.HasValue)
                        {
                            var s = customSize.Value;
                            androidPluginInstance.Call("openModalWithConfig", url, true, true,
                                Mathf.Clamp(s.portraitWidthMultiplier, 0.1f, 1f), Mathf.Clamp(s.portraitHeightMultiplier, 0.1f, 1f),
                                Mathf.Clamp(s.landscapeWidthMultiplier, 0.1f, 1f), Mathf.Clamp(s.landscapeHeightMultiplier, 0.1f, 1f),
                                0.4f, 0.3f, 0.3f, 0.4f);
                        }
                        else
                            androidPluginInstance.Call("openModal", url);
                    }
                    else
                    {
                        ApplyCheckoutConfigToNative();
                        androidPluginInstance.Call("openCheckout", url);
                    }
                }
                catch (System.Exception e)
                {
                    HandleNativeException(isPopup ? "OpenModal" : "OpenCheckout", e);
                    Application.OpenURL(url);
                }
            }
            else
                Application.OpenURL(url);
#elif UNITY_IOS && !UNITY_EDITOR
            try
            {
                SyncForceWebBasedCheckoutToNative();
                if (isPopup)
                {
                    if (modalConfig.HasValue)
                    {
                        var c = modalConfig.Value;
                        _StashPayCardBridgeOpenModalWithConfig(url, c.showDragBar, c.allowDismiss,
                            c.phoneWidthRatioPortrait, c.phoneHeightRatioPortrait, c.phoneWidthRatioLandscape, c.phoneHeightRatioLandscape,
                            c.tabletWidthRatioPortrait, c.tabletHeightRatioPortrait, c.tabletWidthRatioLandscape, c.tabletHeightRatioLandscape);
                    }
                    else if (customSize.HasValue)
                    {
                        var s = customSize.Value;
                        _StashPayCardBridgeOpenModalWithConfig(url, true, true,
                            Mathf.Clamp(s.portraitWidthMultiplier, 0.1f, 1f), Mathf.Clamp(s.portraitHeightMultiplier, 0.1f, 1f),
                            Mathf.Clamp(s.landscapeWidthMultiplier, 0.1f, 1f), Mathf.Clamp(s.landscapeHeightMultiplier, 0.1f, 1f),
                            0.4f, 0.3f, 0.3f, 0.4f);
                    }
                    else
                        _StashPayCardBridgeOpenModal(url);
                }
                else
                {
                    ApplyCheckoutConfigToNative();
                    _StashPayCardBridgeOpenCheckout(url);
                }
            }
            catch (System.Exception e)
            {
                HandleNativeException(isPopup ? "OpenModal" : "OpenCheckout", e);
                Application.OpenURL(url);
            }
#elif UNITY_EDITOR
            // Use Editor window for testing in Unity Editor
            OpenEditorTestWindow(url, isPopup, customSize);
#else
            Application.OpenURL(url);
#endif

            // Restore original ForceWebBasedCheckout setting after popup call
            if (isPopup && originalForceSafari)
            {
                ForceWebBasedCheckout = originalForceSafari;
            }
        }

        /// <summary>
        /// Opens a URL in a centered modal popup window.
        /// 
        /// This method displays a Stash Pay popup using platform-specific native presentation.
        /// - On iOS and Android devices, this shows a modal popup with platform-specific default sizing.
        /// - On desktop in the Unity Editor, this opens a simulated test window.
        /// 
        /// If no custom size is provided, the popup uses platform-specific default dimensions.
        /// </summary>
        /// <param name="url">The Stash Pay URL to load in the popup. Get your opt-in URL from Stash Studio.</param>
        /// <param name="dismissCallback">Optional callback invoked when the popup is dismissed by the user.</param>
        /// <param name="successCallback">Optional callback invoked when a payment transaction completes successfully.</param>
        /// <param name="failureCallback">Optional callback invoked when a payment transaction fails or encounters an error.</param>
        /// <param name="customSize">Optional custom size configuration for portrait and landscape orientations. If null, uses platform-specific defaults.</param>
        /// 
        /// <example>
        /// <code>
        /// var customSize = new PopupSizeConfig
        /// {
        ///     portraitWidthMultiplier = 0.9f,
        ///     portraitHeightMultiplier = 0.8f,
        ///     landscapeWidthMultiplier = 0.85f,
        ///     landscapeHeightMultiplier = 0.75f
        /// };
        /// 
        /// StashPayCard.Instance.OpenPopup(
        ///     "https://your-stash-pay-popup-link.com",
        ///     () => Debug.Log("Popup dismissed"),
        ///     () => Debug.Log("Payment succeeded"),
        ///     () => Debug.Log("Payment failed"),
        ///     customSize
        /// );
        /// </code>
        /// </example>
        /// <summary>Opens a URL in a centered modal. Use for opt-in flows (e.g. payment channel selection). Legacy overload with PopupSizeConfig.</summary>
        public void OpenPopup(string url, Action dismissCallback = null, Action successCallback = null, Action failureCallback = null, PopupSizeConfig? customSize = null)
        {
            StartCoroutine(OpenURLInternal(url, dismissCallback, successCallback, failureCallback, true, customSize, null));
        }

        /// <summary>Opens a URL in a centered modal with optional config (drag bar, dismiss, sizing). Use for opt-in flows.</summary>
        public void OpenModal(string url, Action dismissCallback = null, Action successCallback = null, Action failureCallback = null, StashPayModalConfig? config = null)
        {
            StartCoroutine(OpenURLInternal(url, dismissCallback, successCallback, failureCallback, true, null, config));
        }

        /// <summary>
        /// Resets and dismisses any currently presented checkout card or popup.
        /// 
        /// This method programmatically closes any active Stash Pay checkout UI that is currently
        /// displayed on screen. It is useful for cleanup scenarios or when you need to force-dismiss
        /// the checkout interface.
        /// 
        /// Note: This method only has effect on iOS and Android devices. In the Unity Editor,
        /// it has no effect.
        /// </summary>
        public void ResetPresentationState()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                InitializeAndroidPlugin();
                androidPluginInstance?.Call("resetPresentationState");
            }
            catch (System.Exception e) { HandleNativeException("ResetPresentationState", e); }
#elif UNITY_IOS && !UNITY_EDITOR
            try { _StashPayCardBridgeResetPresentationState(); }
            catch (System.Exception e) { HandleNativeException("ResetPresentationState", e); }
#endif
        }

        /// <summary>
        /// Dismisses the currently open SFSafariViewController if one is presented.
        /// 
        /// This method dismisses the SFSafariViewController and fires only the OnSafariViewDismissed callback.
        /// Use this when you want to dismiss the view controller without triggering success/failure callbacks.
        /// 
        /// Note: This method only has effect on iOS devices when ForceWebBasedCheckout is true
        /// and an SFSafariViewController is currently presented. In the Unity Editor or on
        /// other platforms, it has no effect.
        /// </summary>
        public void DismissSafariViewController()
        {
#if UNITY_IOS && !UNITY_EDITOR
            try { _StashPayCardBridgeDismissSafariViewController(); }
            catch (System.Exception e) { HandleNativeException("DismissSafariViewController", e); }
#endif
        }

        /// <summary>
        /// Dismisses the currently open SFSafariViewController if one is presented and fires the appropriate callbacks.
        /// 
        /// This method is useful when handling deeplink callbacks from SFSafariViewController.
        /// When a deeplink is received (e.g., via Application.deepLinkActivated), you can call
        /// this method to programmatically dismiss the SFSafariViewController, fire the appropriate
        /// success or failure callback, and return control to the Unity game.
        /// 
        /// Note: This method only has effect on iOS devices when ForceWebBasedCheckout is true
        /// and an SFSafariViewController is currently presented. In the Unity Editor or on
        /// other platforms, it has no effect.
        /// 
        /// Usage example:
        /// <code>
        /// Application.deepLinkActivated += (url) => {
        ///     if (url.Contains("stash/purchaseSuccess")) {
        ///         StashPayCard.Instance.DismissSafariViewController(success: true);
        ///     } else if (url.Contains("stash/purchaseFailure")) {
        ///         StashPayCard.Instance.DismissSafariViewController(success: false);
        ///     }
        /// };
        /// </code>
        /// </summary>
        /// <param name="success">True to fire OnPaymentSuccess callback, false to fire OnPaymentFailure callback.</param>
        public void DismissSafariViewController(bool success)
        {
#if UNITY_IOS && !UNITY_EDITOR
            try { _StashPayCardBridgeDismissSafariViewControllerWithResult(success); }
            catch (System.Exception e) { HandleNativeException("DismissSafariViewController", e); }
#endif
        }

        /// <summary>
        /// Gets a value indicating whether a Stash Pay checkout card or popup is currently displayed.
        /// 
        /// This property allows you to check if there is an active checkout UI presentation
        /// before attempting to open a new one, which can help prevent multiple overlapping
        /// checkout interfaces.
        /// </summary>
        /// <value>True if a checkout card or popup is currently visible; otherwise, false.</value>
        public bool IsCurrentlyPresented
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                try { InitializeAndroidPlugin(); return androidPluginInstance?.Call<bool>("isCurrentlyPresented") ?? false; }
                catch (System.Exception e) { HandleNativeException("IsCurrentlyPresented", e); return false; }
#elif UNITY_IOS && !UNITY_EDITOR
                try { return _StashPayCardBridgeIsCurrentlyPresented(); }
                catch (System.Exception e) { HandleNativeException("IsCurrentlyPresented", e); return false; }
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Gets or sets a value that forces the use of native browser-based checkout instead of the custom card UI.
        /// 
        /// When set to true, checkout URLs will open in the platform's native browser interface:
        /// - On iOS: Opens in Safari View Controller (SFSafariViewController)
        /// - On Android: Opens in Chrome Custom Tabs
        /// 
        /// When set to false (default), checkout URLs use the in-app checkout card presentation.
        /// 
        /// </summary>
        /// <value>True to force native browser checkout; false to use custom card UI (default).</value>
        public bool ForceWebBasedCheckout
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                try { InitializeAndroidPlugin(); return androidPluginInstance?.Call<bool>("getForceWebBasedCheckout") ?? false; }
                catch (System.Exception e) { HandleNativeException("ForceWebBasedCheckout.get", e); return false; }
#elif UNITY_IOS && !UNITY_EDITOR
                try { return _StashPayCardBridgeGetForceWebBasedCheckout(); }
                catch (System.Exception e) { HandleNativeException("ForceWebBasedCheckout.get", e); return false; }
#else
                return false;
#endif
            }
            set
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                try { InitializeAndroidPlugin(); androidPluginInstance?.Call("setForceWebBasedCheckout", value); }
                catch (System.Exception e) { HandleNativeException("ForceWebBasedCheckout.set", e); }
#elif UNITY_IOS && !UNITY_EDITOR
                try { _StashPayCardBridgeSetForceWebBasedCheckout(value); }
                catch (System.Exception e) { HandleNativeException("ForceWebBasedCheckout.set", e); }
#endif
            }
        }

        /// <summary>When true, checkout opens in a portrait-locked activity (phone); when false, overlay in current orientation. Default: false.</summary>
        public bool ForcePortraitOnCheckout { get { return _forcePortraitOnCheckout; } set { _forcePortraitOnCheckout = value; ApplyCheckoutConfigToNative(); } }

        /// <summary>Card height as ratio of screen height in portrait (0.1-1.0). Default: 0.68.</summary>
        public float CardHeightRatioPortrait { get { return _cardHeightRatioPortrait; } set { _cardHeightRatioPortrait = Mathf.Clamp(value, 0.1f, 1f); ApplyCheckoutConfigToNative(); } }

        /// <summary>Card width as ratio of screen width in landscape. Default: 0.9.</summary>
        public float CardWidthRatioLandscape { get { return _cardWidthRatioLandscape; } set { _cardWidthRatioLandscape = Mathf.Clamp(value, 0.1f, 1f); ApplyCheckoutConfigToNative(); } }

        /// <summary>Card height as ratio of screen height in landscape. Default: 0.6.</summary>
        public float CardHeightRatioLandscape { get { return _cardHeightRatioLandscape; } set { _cardHeightRatioLandscape = Mathf.Clamp(value, 0.1f, 1f); ApplyCheckoutConfigToNative(); } }

        /// <summary>Tablet card width in portrait (0.1-1.0). Default: 0.4.</summary>
        public float TabletWidthRatioPortrait { get { return _tabletWidthRatioPortrait; } set { _tabletWidthRatioPortrait = Mathf.Clamp(value, 0.1f, 1f); ApplyCheckoutConfigToNative(); } }

        /// <summary>Tablet card height in portrait (0.1-1.0). Default: 0.5.</summary>
        public float TabletHeightRatioPortrait { get { return _tabletHeightRatioPortrait; } set { _tabletHeightRatioPortrait = Mathf.Clamp(value, 0.1f, 1f); ApplyCheckoutConfigToNative(); } }

        /// <summary>Tablet card width in landscape (0.1-1.0). Default: 0.3.</summary>
        public float TabletWidthRatioLandscape { get { return _tabletWidthRatioLandscape; } set { _tabletWidthRatioLandscape = Mathf.Clamp(value, 0.1f, 1f); ApplyCheckoutConfigToNative(); } }

        /// <summary>Tablet card height in landscape (0.1-1.0). Default: 0.6.</summary>
        public float TabletHeightRatioLandscape { get { return _tabletHeightRatioLandscape; } set { _tabletHeightRatioLandscape = Mathf.Clamp(value, 0.1f, 1f); ApplyCheckoutConfigToNative(); } }

        /// <summary>Legacy: card height ratio in portrait. Use CardHeightRatioPortrait.</summary>
        public float CardHeightRatio { get { return _cardHeightRatioPortrait; } set { CardHeightRatioPortrait = value; } }

        #endregion

        #region Private Methods

        /// <summary>
        /// Handles exceptions that occur during native plugin operations.
        /// Logs the exception and fires the OnNativeException event for subscribers.
        /// 
        /// Note on exception handling:
        /// - Android: Java exceptions thrown in native code ARE catchable and will be wrapped as AndroidJavaException.
        /// - iOS: Objective-C exceptions (NSException) and crashes are NOT catchable via C# try-catch.
        ///   iOS crashes will terminate the app. Only errors that occur during the P/Invoke call itself
        ///   (like invalid function signatures) would be catchable.
        /// </summary>
        /// <param name="operation">The name of the operation that failed (e.g., "OpenCheckout", "OpenPopup")</param>
        /// <param name="exception">The exception that occurred</param>
        private void HandleNativeException(string operation, System.Exception exception)
        {
            string errorMessage = $"[StashPayCard] Exception in {operation}: {exception.Message}";
            Debug.LogError(errorMessage);
            Debug.LogException(exception);
            
            // Fire event for subscribers to handle
            OnNativeException?.Invoke(operation, exception);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only callback method invoked when a payment transaction completes successfully in the test window.
        /// This method is called by the StashPayCardEditorWindow and triggers the OnPaymentSuccess event.
        /// </summary>
        public void OnEditorPaymentSuccess()
        {
            OnPaymentSuccess?.Invoke();
        }
        
        /// <summary>
        /// Editor-only callback method invoked when a payment transaction fails in the test window.
        /// This method is called by the StashPayCardEditorWindow and triggers the OnPaymentFailure event.
        /// </summary>
        public void OnEditorPaymentFailure()
        {
            OnPaymentFailure?.Invoke();
        }
        
        /// <summary>
        /// Editor-only callback method invoked when an opt-in response is received in the test window.
        /// This method is called by the StashPayCardEditorWindow and triggers the OnOptinResponse event.
        /// </summary>
        /// <param name="optinType">The type of opt-in response received from the payment flow.</param>
        public void OnEditorOptinResponse(string optinType)
        {
            OnOptinResponse?.Invoke(optinType);
        }
        
        /// <summary>
        /// Editor-only callback method invoked when the checkout dialog is dismissed in the test window.
        /// This method is called by the StashPayCardEditorWindow and triggers the OnSafariViewDismissed event.
        /// </summary>
        public void OnEditorDismissCatalog()
        {
            OnSafariViewDismissed?.Invoke();
        }
        
        // Editor-only method to open test window using reflection
        private void OpenEditorTestWindow(string url, bool isPopup, PopupSizeConfig? customSize)
        {
            try
            {
                // Use reflection to avoid direct assembly reference
                System.Type editorWindowType = System.Type.GetType("StashPopup.Editor.StashPayCardEditorWindow, Assembly-CSharp-Editor");
                if (editorWindowType == null)
                {
                    // Try alternative assembly name
                    editorWindowType = System.Type.GetType("StashPopup.Editor.StashPayCardEditorWindow");
                }
                
                if (editorWindowType != null)
                {
                    if (isPopup)
                    {
                        System.Reflection.MethodInfo openPopupMethod = editorWindowType.GetMethod("OpenPopup", 
                            new System.Type[] { typeof(string), typeof(PopupSizeConfig?) });
                        if (openPopupMethod != null)
                        {
                            openPopupMethod.Invoke(null, new object[] { url, customSize });
                        }
                    }
                    else
                    {
                        System.Reflection.MethodInfo openCheckoutMethod = editorWindowType.GetMethod("OpenCheckout", 
                            new System.Type[] { typeof(string) });
                        if (openCheckoutMethod != null)
                        {
                            openCheckoutMethod.Invoke(null, new object[] { url });
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"StashPayCard: Could not open Editor test window: {e.Message}");
            }
        }
#endif

        #endregion
    }
}