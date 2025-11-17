using System;
using UnityEngine;
using System.Runtime.InteropServices;
using System.Collections;
using UnityEngine.Networking;
using AOT;

namespace StashPopup
{
    /// <summary>
    /// Popup size presets for OpenPopup
    /// </summary>
    public enum PopupSize
    {
        Small,      // 70% width, 100% height (portrait)
        Medium,     // 85% width, 112.5% height (portrait) - default
        Large       // 100% width, 125% height (portrait)
    }

    /// <summary>
    /// Custom popup size configuration for fine-grained control
    /// </summary>
    public struct PopupSizeConfig
    {
        public float portraitWidthMultiplier;   // Default: 0.85f
        public float portraitHeightMultiplier;  // Default: 1.125f
        public float landscapeWidthMultiplier;  // Default: 1.27075f
        public float landscapeHeightMultiplier; // Default: 0.9f

        public static PopupSizeConfig Default => new PopupSizeConfig
        {
            portraitWidthMultiplier = 0.85f,
            portraitHeightMultiplier = 1.125f,
            landscapeWidthMultiplier = 1.27075f,
            landscapeHeightMultiplier = 0.9f
        };
    }

    /// <summary>
    /// Cross-platform wrapper for Stash Pay checkout.
    /// </summary>
    public class StashPayCard : MonoBehaviour
    {
        #region Singleton Implementation
        private static StashPayCard _instance;
        private const string FLAGSMITH_API_KEY = "ZmnWzYYR29AHDYwMVXtw68";
        private const string FLAGSMITH_API_URL = "https://edge.api.flagsmith.com/api/v1/identities/";

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
        
        // Android callback: payment succeeded
        public void OnAndroidPaymentSuccess(string message)
        {
            OnPaymentSuccess?.Invoke();
        }
        
        // Android callback: payment failed
        public void OnAndroidPaymentFailure(string message)
        {
            OnPaymentFailure?.Invoke();
        }
        
        // Android callback: dialog dismissed
        public void OnAndroidDialogDismissed(string message)
        {
            OnSafariViewDismissed?.Invoke();
        }
        
        // Android callback: opt-in response
        public void OnAndroidOptinResponse(string optinType)
        {
            OnOptinResponse?.Invoke(optinType);
        }
        
        // Android callback: page loaded
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
        private static extern void _StashPayCardSetCardConfiguration(float heightRatio, float verticalPosition);
        
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
        /// Opens a Stash Pay checkout URL in a sliding card view from the bottom of the screen.
        /// </summary>
        public void OpenCheckout(string url, Action dismissCallback = null, Action successCallback = null, Action failureCallback = null)
        {
            StartCoroutine(OpenURLWithFlagsmithConfig(url, dismissCallback, successCallback, failureCallback, false));
        }
        
        // Internal handler: waits for Flagsmith config then opens URL or popup
        private IEnumerator OpenURLWithFlagsmithConfig(string url, Action dismissCallback, Action successCallback, Action failureCallback, bool isPopup, PopupSize? size = null, PopupSizeConfig? customSize = null)
        {
            if (string.IsNullOrEmpty(url)) yield break;

            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                url = "https://" + url;
            }

            // Add enable_immediate_fetch query parameter for faster loading on webview and popup.
            // Check if URL already has query parameters to use & instead of ?
            if (!url.Contains("enable_immediate_fetch"))
            {
                url += url.Contains("?") ? "&enable_immediate_fetch=true" : "?enable_immediate_fetch=true";
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
                    // Get size multipliers based on preset or custom config
                    PopupSizeConfig sizeConfig = GetPopupSizeConfig(size, customSize);
                    
                    // Call openPopup with size configuration
                    androidPluginInstance.Call("openPopupWithSize", 
                        url,
                        sizeConfig.portraitWidthMultiplier,
                        sizeConfig.portraitHeightMultiplier,
                        sizeConfig.landscapeWidthMultiplier,
                        sizeConfig.landscapeHeightMultiplier);
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
                // Get size multipliers based on preset or custom config
                PopupSizeConfig sizeConfig = GetPopupSizeConfig(size, customSize);
                
                // Call openPopup with size configuration
                _StashPayCardOpenPopupWithSize(
                    url,
                    sizeConfig.portraitWidthMultiplier,
                    sizeConfig.portraitHeightMultiplier,
                    sizeConfig.landscapeWidthMultiplier,
                    sizeConfig.landscapeHeightMultiplier);
            }
            else
            {
            _StashPayCardOpenCheckoutInSafariVC(url);
            }
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
        /// Opens a URL in a centered modal popup.
        /// By default uses Medium size (85% width, 112.5% height in portrait).
        /// Modal behavior: close button only, no drag gestures or tap-outside-to-dismiss.
        /// </summary>
        /// <param name="url">The URL to open in the popup</param>
        /// <param name="dismissCallback">Called when the popup is dismissed</param>
        /// <param name="successCallback">Called when payment succeeds (if applicable)</param>
        /// <param name="failureCallback">Called when payment fails (if applicable)</param>
        /// <param name="size">Popup size preset (Small, Medium, Large). Defaults to Medium.</param>
        /// <param name="customSize">Custom size configuration. Only used if size is not specified or you want to override preset values.</param>
        public void OpenPopup(string url, Action dismissCallback = null, Action successCallback = null, Action failureCallback = null, PopupSize? size = null, PopupSizeConfig? customSize = null)
        {
            StartCoroutine(OpenURLWithFlagsmithConfig(url, dismissCallback, successCallback, failureCallback, true, size, customSize));
        }

        // Resets and dismisses any currently presented card
        public void ResetPresentationState()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            androidPluginInstance?.Call("resetPresentationState");
#elif UNITY_IOS && !UNITY_EDITOR
            _StashPayCardResetPresentationState();
#endif
        }

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
        /// Forces native browser instead of custom card UI (Safari on iOS, Chrome Custom Tabs on Android).
        /// </summary>
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

        public float CardHeightRatio
        {
            get { return _cardHeightRatio; }
            set 
            { 
                _cardHeightRatio = Mathf.Clamp01(value);
                ApplyCardConfiguration();
            }
        }

        public float CardVerticalPosition
        {
            get { return _cardVerticalPosition; }
            set 
            { 
                _cardVerticalPosition = Mathf.Clamp01(value);
                ApplyCardConfiguration();
            }
        }

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

        /// <summary>
        /// Gets popup size configuration based on preset or custom values
        /// </summary>
        private PopupSizeConfig GetPopupSizeConfig(PopupSize? size, PopupSizeConfig? customSize)
        {
            // If custom size is provided, use it
            if (customSize.HasValue)
            {
                return customSize.Value;
            }

            // Otherwise, use preset or default (Medium)
            PopupSize preset = size ?? PopupSize.Medium;

            switch (preset)
            {
                case PopupSize.Small:
                    return new PopupSizeConfig
                    {
                        portraitWidthMultiplier = 0.7f,
                        portraitHeightMultiplier = 1.0f,
                        landscapeWidthMultiplier = 1.07f, // 0.7 * 1.3 * 1.15
                        landscapeHeightMultiplier = 0.8f  // 1.0 * 0.8
                    };
                case PopupSize.Medium:
                    return PopupSizeConfig.Default;
                case PopupSize.Large:
                    return new PopupSizeConfig
                    {
                        portraitWidthMultiplier = 1.0f,
                        portraitHeightMultiplier = 1.25f,
                        landscapeWidthMultiplier = 1.495f, // 1.0 * 1.3 * 1.15
                        landscapeHeightMultiplier = 1.0f   // 1.25 * 0.8
                    };
                default:
                    return PopupSizeConfig.Default;
            }
        }

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