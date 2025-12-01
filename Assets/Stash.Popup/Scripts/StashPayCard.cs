using System;
using UnityEngine;
using System.Runtime.InteropServices;
using System.Collections;
using UnityEngine.Networking;
using AOT;

namespace StashPopup
{
    /// <summary>
    /// Custom popup size configuration for fine-grained control
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
        private const string FLAGSMITH_API_KEY = "ZmnWzYYR29AHDYwMVXtw68";
        private const string FLAGSMITH_API_URL = "https://edge.api.flagsmith.com/api/v1/identities/";

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

        // Initialize singleton and fetch remote configuration
        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                _flagsmithFetchCoroutine = StartCoroutine(FetchFlagsmithConfiguration());
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }
        #endregion

        #region Events
        public event Action OnSafariViewDismissed;
        public event Action OnPaymentSuccess;
        public event Action OnPaymentFailure;
        public event Action<string> OnOptinResponse;
        public event Action<double> OnPageLoaded;
        #endregion

        #region Private Fields
        private float _cardHeightRatio = 0.6f;
        private float _cardVerticalPosition = 1.0f;
        private float _cardWidthRatio = 1.0f;
        private bool _flagsmithConfigLoaded = false;
        private Coroutine _flagsmithFetchCoroutine = null;
        #endregion

        #region Native Plugin Interface

#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaClass androidPlugin;
        private AndroidJavaObject androidPluginInstance;
        
        // Lazy initialization of Android native plugin
        private void InitializeAndroidPlugin()
        {
            if (androidPlugin == null)
            {
                try
                {
                    androidPlugin = new AndroidJavaClass("com.stash.popup.StashPayCardPlugin");
                    androidPluginInstance = androidPlugin.CallStatic<AndroidJavaObject>("getInstance");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"StashPayCard: Failed to initialize Android plugin: {e.Message}");
                }
            }
        }
        
        /// <summary>
        /// Android native callback invoked when a payment transaction completes successfully.
        /// This method is called by the native Android plugin and triggers the OnPaymentSuccess event.
        /// </summary>
        /// <param name="message">Optional message from the native plugin (currently unused).</param>
        public void OnAndroidPaymentSuccess(string message)
        {
            OnPaymentSuccess?.Invoke();
        }
        
        /// <summary>
        /// Android native callback invoked when a payment transaction fails or encounters an error.
        /// This method is called by the native Android plugin and triggers the OnPaymentFailure event.
        /// </summary>
        /// <param name="message">Optional error message from the native plugin (currently unused).</param>
        public void OnAndroidPaymentFailure(string message)
        {
            OnPaymentFailure?.Invoke();
        }
        
        /// <summary>
        /// Android native callback invoked when the checkout dialog is dismissed by the user.
        /// This method is called by the native Android plugin and triggers the OnSafariViewDismissed event.
        /// </summary>
        /// <param name="message">Optional message from the native plugin (currently unused).</param>
        public void OnAndroidDialogDismissed(string message)
        {
            OnSafariViewDismissed?.Invoke();
        }
        
        /// <summary>
        /// Android native callback invoked when an opt-in response is received from the payment flow.
        /// This method is called by the native Android plugin and triggers the OnOptinResponse event.
        /// </summary>
        /// <param name="optinType">The type of opt-in response received from the payment flow.</param>
        public void OnAndroidOptinResponse(string optinType)
        {
            OnOptinResponse?.Invoke(optinType);
        }
        
        /// <summary>
        /// Android native callback invoked when a page finishes loading in the checkout view.
        /// This method is called by the native Android plugin and triggers the OnPageLoaded event with the load time.
        /// </summary>
        /// <param name="loadTimeMs">The page load time in milliseconds as a string.</param>
        public void OnAndroidPageLoaded(string loadTimeMs)
        {
            if (double.TryParse(loadTimeMs, out double loadTime))
            {
                OnPageLoaded?.Invoke(loadTime);
            }
        }

#elif UNITY_IOS && !UNITY_EDITOR
        private delegate void SafariViewDismissedCallback();
        private delegate void PaymentSuccessCallback();
        private delegate void PaymentFailureCallback();
        private delegate void OptinResponseCallback(string optinType);
        private delegate void PageLoadedCallback(double loadTimeMs);
        
        [DllImport("__Internal")]
        private static extern void _StashPayCardOpenCheckoutInSafariVC(string url);
        
        [DllImport("__Internal")]
        private static extern void _StashPayCardOpenPopup(string url);
        
        [DllImport("__Internal")]
        private static extern void _StashPayCardOpenPopupWithSize(string url, float portraitWidth, float portraitHeight, float landscapeWidth, float landscapeHeight);
        
        [DllImport("__Internal")]
        private static extern void _StashPayCardSetSafariViewDismissedCallback(SafariViewDismissedCallback callback);
        
        [DllImport("__Internal")]
        private static extern void _StashPayCardSetPaymentSuccessCallback(PaymentSuccessCallback callback);
        
        [DllImport("__Internal")]
        private static extern void _StashPayCardSetPaymentFailureCallback(PaymentFailureCallback callback);
        
        [DllImport("__Internal")]
        private static extern void _StashPayCardSetOptinResponseCallback(OptinResponseCallback callback);
        
        [DllImport("__Internal")]
        private static extern void _StashPayCardSetPageLoadedCallback(PageLoadedCallback callback);
        
        [DllImport("__Internal")]
        private static extern void _StashPayCardSetCardConfigurationWithWidth(float heightRatio, float verticalPosition, float widthRatio);
        
        [DllImport("__Internal")]
        private static extern void _StashPayCardResetPresentationState();
        
        [DllImport("__Internal")]
        private static extern bool _StashPayCardIsCurrentlyPresented();

        [DllImport("__Internal")]
        private static extern void _StashPayCardSetForceSafariViewController(bool force);

        [DllImport("__Internal")]
        private static extern bool _StashPayCardGetForceSafariViewController();
        
        [DllImport("__Internal")]
        private static extern void _StashPayCardDismissSafariViewController(bool success);
#endif

        #endregion

        #region Public Methods

        // Applies current card size/position configuration to native plugin
        private void ApplyCardConfiguration()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            androidPluginInstance?.Call("setCardConfiguration", _cardHeightRatio, _cardVerticalPosition, _cardWidthRatio);
#elif UNITY_IOS && !UNITY_EDITOR
            _StashPayCardSetCardConfigurationWithWidth(_cardHeightRatio, _cardVerticalPosition, _cardWidthRatio);
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
            StartCoroutine(OpenURLWithFlagsmithConfig(url, dismissCallback, successCallback, failureCallback, false));
        }
        
        // Internal handler: waits for Flagsmith config then opens URL or popup
        private IEnumerator OpenURLWithFlagsmithConfig(string url, Action dismissCallback, Action successCallback, Action failureCallback, bool isPopup, PopupSizeConfig? customSize = null)
        {
            if (string.IsNullOrEmpty(url)) yield break;

            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                url = "https://" + url;
            }

            // Wait for Flagsmith configuration to be loaded before opening
            if (!_flagsmithConfigLoaded && _flagsmithFetchCoroutine != null)
            {
                yield return _flagsmithFetchCoroutine;
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
                if (isPopup)
                {
                    // Only pass size if custom size is explicitly specified
                    if (customSize.HasValue)
                    {
                        PopupSizeConfig sizeConfig = customSize.Value;
                        Debug.Log($"[StashPayCard] Opening popup with custom size: Portrait({sizeConfig.portraitWidthMultiplier}, {sizeConfig.portraitHeightMultiplier}), Landscape({sizeConfig.landscapeWidthMultiplier}, {sizeConfig.landscapeHeightMultiplier})");
                        androidPluginInstance.Call("openPopupWithSize", 
                            url,
                            sizeConfig.portraitWidthMultiplier,
                            sizeConfig.portraitHeightMultiplier,
                            sizeConfig.landscapeWidthMultiplier,
                            sizeConfig.landscapeHeightMultiplier);
                    }
                    else
                    {
                        Debug.Log("[StashPayCard] Opening popup with platform default size (no custom size specified)");
                        // Use platform default (no custom size specified)
                    androidPluginInstance.Call("openPopup", url);
                    }
                }
                else
                {
                    // Apply card configuration and call openCheckout for card presentation
                    androidPluginInstance.Call("setCardConfiguration", _cardHeightRatio, _cardVerticalPosition, _cardWidthRatio);
                    androidPluginInstance.Call("openCheckout", url);
                }
            }
            else
            {
                Application.OpenURL(url);
            }
#elif UNITY_IOS && !UNITY_EDITOR
            if (!isPopup)
            {
            ApplyCardConfiguration();
            }
            _StashPayCardSetSafariViewDismissedCallback(OnIOSSafariViewDismissed);
            _StashPayCardSetPaymentSuccessCallback(OnIOSPaymentSuccess);
            _StashPayCardSetPaymentFailureCallback(OnIOSPaymentFailure);
            _StashPayCardSetOptinResponseCallback(OnIOSOptinResponse);
            _StashPayCardSetPageLoadedCallback(OnIOSPageLoaded);
            
            if (isPopup)
            {
                // Only pass size if custom size is explicitly specified
                if (customSize.HasValue)
                {
                    PopupSizeConfig sizeConfig = customSize.Value;
                    Debug.Log($"[StashPayCard] Opening popup with custom size: Portrait({sizeConfig.portraitWidthMultiplier}, {sizeConfig.portraitHeightMultiplier}), Landscape({sizeConfig.landscapeWidthMultiplier}, {sizeConfig.landscapeHeightMultiplier})");
                    _StashPayCardOpenPopupWithSize(
                        url,
                        sizeConfig.portraitWidthMultiplier,
                        sizeConfig.portraitHeightMultiplier,
                        sizeConfig.landscapeWidthMultiplier,
                        sizeConfig.landscapeHeightMultiplier);
                }
                else
                {
                    Debug.Log("[StashPayCard] Opening popup with platform default size (no custom size specified)");
                    // Use iOS default (no custom size specified)
                _StashPayCardOpenPopup(url);
                }
            }
            else
            {
            _StashPayCardOpenCheckoutInSafariVC(url);
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
        public void OpenPopup(string url, Action dismissCallback = null, Action successCallback = null, Action failureCallback = null, PopupSizeConfig? customSize = null)
        {
            StartCoroutine(OpenURLWithFlagsmithConfig(url, dismissCallback, successCallback, failureCallback, true, customSize));
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
            androidPluginInstance?.Call("resetPresentationState");
#elif UNITY_IOS && !UNITY_EDITOR
            _StashPayCardResetPresentationState();
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
            _StashPayCardDismissSafariViewController(success);
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
                return androidPluginInstance?.Call<bool>("isCurrentlyPresented") ?? false;
#elif UNITY_IOS && !UNITY_EDITOR
                return _StashPayCardIsCurrentlyPresented();
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
                return androidPluginInstance?.Call<bool>("getForceSafariViewController") ?? false;
#elif UNITY_IOS && !UNITY_EDITOR
                return _StashPayCardGetForceSafariViewController();
#else
                return false;
#endif
            }
            set
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                androidPluginInstance?.Call("setForceSafariViewController", value);
#elif UNITY_IOS && !UNITY_EDITOR
                _StashPayCardSetForceSafariViewController(value);
#endif
            }
        }

        /// <summary>
        /// Gets or sets the height ratio of the checkout card relative to the screen height.
        /// 
        /// This property controls how tall the sliding checkout card appears when using
        /// the custom card UI presentation (not applicable when ForceWebBasedCheckout is true).
        /// 
        /// The value is clamped between 0.0 and 1.0, where:
        /// - 0.0 = Minimum height (card barely visible)
        /// - 1.0 = Full screen height
        /// - Default: 0.6 (60% of screen height)
        /// 
        /// Changes to this property are immediately applied to any currently displayed card
        /// and will affect future card presentations.
        /// </summary>
        /// <value>The height ratio between 0.0 and 1.0. Default is 0.6.</value>
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
        /// Gets or sets the vertical position of the checkout card's anchor point.
        /// 
        /// This property controls where the checkout card is positioned vertically on the screen
        /// when using the custom card UI presentation (not applicable when ForceWebBasedCheckout is true).
        /// 
        /// The value is clamped between 0.0 and 1.0, where:
        /// - 0.0 = Bottom of the screen
        /// - 1.0 = Top of the screen
        /// - Default: 1.0 (card slides up from bottom)
        /// 
        /// Changes to this property are immediately applied to any currently displayed card
        /// and will affect future card presentations.
        /// </summary>
        /// <value>The vertical position ratio between 0.0 and 1.0. Default is 1.0.</value>
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
        /// Gets or sets the width ratio of the checkout card relative to the screen width.
        /// 
        /// This property controls how wide the sliding checkout card appears when using
        /// the custom card UI presentation (not applicable when ForceWebBasedCheckout is true).
        /// 
        /// The value is clamped between 0.0 and 1.0, where:
        /// - 0.0 = Minimum width (card barely visible)
        /// - 1.0 = Full screen width
        /// - Default: 1.0 (full width)
        /// 
        /// Changes to this property are immediately applied to any currently displayed card
        /// and will affect future card presentations.
        /// </summary>
        /// <value>The width ratio between 0.0 and 1.0. Default is 1.0.</value>
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

        // Fetches remote feature flags from Flagsmith API with device traits
        private IEnumerator FetchFlagsmithConfiguration()
        {
            string deviceId = SystemInfo.deviceUniqueIdentifier;
            string bundleId = Application.identifier;
            string deviceModel = SystemInfo.deviceModel;
            string osVersion = SystemInfo.operatingSystem;
            
            FlagsmithIdentityRequest identityRequest = new FlagsmithIdentityRequest
            {
                identifier = deviceId,
                traits = new FlagsmithTrait[]
                {
                    new FlagsmithTrait { trait_key = "bundle_id", trait_value = bundleId },
                    new FlagsmithTrait { trait_key = "device_model", trait_value = deviceModel },
                    new FlagsmithTrait { trait_key = "os_version", trait_value = osVersion },
                }
            };
            
            string jsonPayload = JsonUtility.ToJson(identityRequest);
            
            using (UnityWebRequest request = new UnityWebRequest(FLAGSMITH_API_URL, "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("X-Environment-Key", FLAGSMITH_API_KEY);
                request.timeout = 5;
                
                yield return request.SendWebRequest();
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        FlagsmithIdentityResponse response = JsonUtility.FromJson<FlagsmithIdentityResponse>(request.downloadHandler.text);
                        ApplyFlagsmithConfiguration(response);
                    }
                    catch
                    {
                        Debug.LogWarning("StashPayCard: Failed to parse Flagsmith response - using defaults");
                    }
                }
            }
            
            _flagsmithConfigLoaded = true;
        }

        // Applies feature flags from Flagsmith response
        private void ApplyFlagsmithConfiguration(FlagsmithIdentityResponse response)
        {
            if (response?.flags == null) return;

            foreach (var flag in response.flags)
            {
                if (flag.feature.name == "force_sfsafariviewcontroller" && flag.enabled)
                {
#if UNITY_IOS && !UNITY_EDITOR
                    ForceWebBasedCheckout = true;
#endif
                }
                else if (flag.feature.name == "force_chromecustomtab" && flag.enabled)
                {
#if UNITY_ANDROID && !UNITY_EDITOR
                    ForceWebBasedCheckout = true;
#endif
                }
            }
        }

#if UNITY_IOS && !UNITY_EDITOR
        // iOS callback: card dismissed
        [MonoPInvokeCallback(typeof(SafariViewDismissedCallback))]
        private static void OnIOSSafariViewDismissed()
        {
            Instance?.OnSafariViewDismissed?.Invoke();
        }
        
        // iOS callback: payment succeeded
        [MonoPInvokeCallback(typeof(PaymentSuccessCallback))]
        private static void OnIOSPaymentSuccess()
        {
            Instance?.OnPaymentSuccess?.Invoke();
            }

        // iOS callback: payment failed
        [MonoPInvokeCallback(typeof(PaymentFailureCallback))]
        private static void OnIOSPaymentFailure()
        {
            Instance?.OnPaymentFailure?.Invoke();
            }
        
        // iOS callback: opt-in response
        [MonoPInvokeCallback(typeof(OptinResponseCallback))]
        private static void OnIOSOptinResponse(string optinType)
        {
            Instance?.OnOptinResponse?.Invoke(optinType);
        }
        
        // iOS callback: page loaded
        [MonoPInvokeCallback(typeof(PageLoadedCallback))]
        private static void OnIOSPageLoaded(double loadTimeMs)
        {
            Instance?.OnPageLoaded?.Invoke(loadTimeMs);
        }
#endif

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

        #region Flagsmith Data Structures
        
        [Serializable]
        private class FlagsmithIdentityRequest
        {
            public string identifier;
            public FlagsmithTrait[] traits;
        }
        
        [Serializable]
        private class FlagsmithTrait
        {
            public string trait_key;
            public string trait_value;
        }
        
        [Serializable]
        private class FlagsmithIdentityResponse
        {
            public FlagsmithFlag[] flags;
        }

        [Serializable]
        private class FlagsmithFlag
        {
            public bool enabled;
            public FlagsmithFeature feature;
        }

        [Serializable]
        private class FlagsmithFeature
        {
            public string name;
        }

        #endregion
    }
}