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
        /// Opens a URL in a centered modal popup.
        /// Uses platform-specific default sizing if no custom size is provided.
        /// Modal behavior: close button only, no drag gestures or tap-outside-to-dismiss.
        /// </summary>
        /// <param name="url">The URL to open in the popup</param>
        /// <param name="dismissCallback">Called when the popup is dismissed</param>
        /// <param name="successCallback">Called when payment succeeds (if applicable)</param>
        /// <param name="failureCallback">Called when payment fails (if applicable)</param>
        /// <param name="customSize">Optional custom size configuration. If not provided, uses platform-specific defaults.</param>
        public void OpenPopup(string url, Action dismissCallback = null, Action successCallback = null, Action failureCallback = null, PopupSizeConfig? customSize = null)
        {
            StartCoroutine(OpenURLWithFlagsmithConfig(url, dismissCallback, successCallback, failureCallback, true, customSize));
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
        // Editor-only callback methods for testing
        public void OnEditorPaymentSuccess()
        {
            OnPaymentSuccess?.Invoke();
        }
        
        public void OnEditorPaymentFailure()
        {
            OnPaymentFailure?.Invoke();
        }
        
        public void OnEditorOptinResponse(string optinType)
        {
            OnOptinResponse?.Invoke(optinType);
        }
        
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