using System;
using UnityEngine;
using System.Runtime.InteropServices;
using System.Collections;

namespace Stash.Native
{
    public struct StashNativeModalConfig
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

        public static StashNativeModalConfig Default => new StashNativeModalConfig
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

    public struct StashNativeCardConfig
    {
        public bool forcePortrait;
        public float cardHeightRatioPortrait;
        public float cardWidthRatioLandscape;
        public float cardHeightRatioLandscape;
        public float tabletWidthRatioPortrait;
        public float tabletHeightRatioPortrait;
        public float tabletWidthRatioLandscape;
        public float tabletHeightRatioLandscape;

        public static StashNativeCardConfig Default => new StashNativeCardConfig
        {
            forcePortrait = false,
            cardHeightRatioPortrait = 0.68f,
            cardWidthRatioLandscape = 0.9f,
            cardHeightRatioLandscape = 0.6f,
            tabletWidthRatioPortrait = 0.4f,
            tabletHeightRatioPortrait = 0.5f,
            tabletWidthRatioLandscape = 0.3f,
            tabletHeightRatioLandscape = 0.6f
        };
    }

    public class StashNative : MonoBehaviour
    {
        #region Singleton

        private static StashNative _instance;

        public static StashNative Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("StashNative");
                    _instance = go.AddComponent<StashNative>();
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
                catch (Exception e)
                {
                    Debug.LogWarning("StashNative: Failed to set activity on resume: " + e.Message);
                }
            }
#endif
        }

        #endregion

        #region Events

        /// <summary>Fired when the card or modal is dismissed. Prefer per-call callbacks for a specific open; use this for global listeners (e.g. analytics).</summary>
        public event Action OnDialogDismissed;
        /// <summary>Fired when payment succeeds. Prefer per-call callbacks for a specific open; use this for global listeners.</summary>
        public event Action OnPaymentSuccess;
        /// <summary>Fired when payment fails. Prefer per-call callbacks for a specific open; use this for global listeners.</summary>
        public event Action OnPaymentFailure;
        public event Action<string> OnOptinResponse;
        public event Action<double> OnPageLoaded;
        public event Action OnNetworkError;
        public event Action<string, Exception> OnNativeException;

        private Action _currentDismissCallback;
        private Action _currentSuccessCallback;
        private Action _currentFailureCallback;

        #endregion

        #region Native Plugin

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
                var bridgeClass = new AndroidJavaClass("com.stash.popup.StashNativeCardUnityBridge");
                androidPluginInstance = bridgeClass.CallStatic<AndroidJavaObject>("getInstance");
                var activity = GetUnityActivity();
                if (activity != null)
                    androidPluginInstance.Call("setActivity", activity);
            }
            catch (Exception e)
            {
                Debug.LogError("StashNative: Failed to initialize Android bridge: " + e.Message);
            }
        }

        public void OnAndroidPaymentSuccess(string message) { _currentSuccessCallback?.Invoke(); OnPaymentSuccess?.Invoke(); _currentSuccessCallback = null; }
        public void OnAndroidPaymentFailure(string message) { _currentFailureCallback?.Invoke(); OnPaymentFailure?.Invoke(); _currentFailureCallback = null; }
        public void OnAndroidDialogDismissed(string message) { _currentDismissCallback?.Invoke(); OnDialogDismissed?.Invoke(); _currentDismissCallback = null; }
        public void OnAndroidOptinResponse(string optinType) => OnOptinResponse?.Invoke(optinType ?? "");
        public void OnAndroidNetworkError(string message) => OnNetworkError?.Invoke();

        public void OnAndroidPageLoaded(string loadTimeMs)
        {
            if (double.TryParse(loadTimeMs, out double loadTime))
                OnPageLoaded?.Invoke(loadTime);
        }

#elif UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern bool _StashNativeCardBridgeIsSDKAvailable();
        [DllImport("__Internal")] private static extern void _StashNativeCardBridgeOpenCard(string url);
        [DllImport("__Internal")] private static extern void _StashNativeCardBridgeOpenCardWithConfig(string url, bool forcePortrait, float cardHeightRatioPortrait, float cardWidthRatioLandscape, float cardHeightRatioLandscape, float tabletWidthRatioPortrait, float tabletHeightRatioPortrait, float tabletWidthRatioLandscape, float tabletHeightRatioLandscape);
        [DllImport("__Internal")] private static extern void _StashNativeCardBridgeOpenModal(string url);
        [DllImport("__Internal")] private static extern void _StashNativeCardBridgeOpenModalWithConfig(string url, bool showDragBar, bool allowDismiss, float phoneWPortrait, float phoneHPortrait, float phoneWLandscape, float phoneHLandscape, float tabletWPortrait, float tabletHPortrait, float tabletWLandscape, float tabletHLandscape);
        [DllImport("__Internal")] private static extern void _StashNativeCardBridgeOpenBrowser(string url);
        [DllImport("__Internal")] private static extern void _StashNativeCardBridgeCloseBrowser();
        [DllImport("__Internal")] private static extern void _StashNativeCardBridgeDismiss();
        [DllImport("__Internal")] private static extern void _StashNativeCardBridgeResetPresentationState();
        [DllImport("__Internal")] private static extern bool _StashNativeCardBridgeIsCurrentlyPresented();
        [DllImport("__Internal")] private static extern bool _StashNativeCardBridgeIsPurchaseProcessing();

        public void OnIOSPaymentSuccess() { _currentSuccessCallback?.Invoke(); OnPaymentSuccess?.Invoke(); _currentSuccessCallback = null; }
        public void OnIOSPaymentFailure() { _currentFailureCallback?.Invoke(); OnPaymentFailure?.Invoke(); _currentFailureCallback = null; }
        public void OnIOSDialogDismissed() { _currentDismissCallback?.Invoke(); OnDialogDismissed?.Invoke(); _currentDismissCallback = null; }
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

        public void OpenCard(string url, Action dismissCallback = null, Action successCallback = null, Action failureCallback = null)
        {
            OpenCard(url, dismissCallback, successCallback, failureCallback, null);
        }

        public void OpenCard(string url, Action dismissCallback = null, Action successCallback = null, Action failureCallback = null, StashNativeCardConfig? config = null)
        {
            StartCoroutine(OpenCardOrModalInternal(url, dismissCallback, successCallback, failureCallback, isModal: false, cardConfig: config, modalConfig: null));
        }

        public void OpenModal(string url, Action dismissCallback = null, Action successCallback = null, Action failureCallback = null, StashNativeModalConfig? config = null)
        {
            StartCoroutine(OpenCardOrModalInternal(url, dismissCallback, successCallback, failureCallback, isModal: true, cardConfig: null, modalConfig: config));
        }

        public void OpenBrowser(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                url = "https://" + url;
#if UNITY_ANDROID && !UNITY_EDITOR
            InitializeAndroidPlugin();
            if (androidPluginInstance != null)
            {
                try { androidPluginInstance.Call("openBrowser", url); }
                catch (Exception e) { HandleNativeException("OpenBrowser", e); }
            }
            else
                Application.OpenURL(url);
#elif UNITY_IOS && !UNITY_EDITOR
            try
            {
                if (_StashNativeCardBridgeIsSDKAvailable())
                    _StashNativeCardBridgeOpenBrowser(url);
                else
                    Application.OpenURL(url);
            }
            catch (Exception e) { HandleNativeException("OpenBrowser", e); }
#else
            Application.OpenURL(url);
#endif
        }

        public void CloseBrowser()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { androidPluginInstance?.Call("closeBrowser"); }
            catch (Exception e) { HandleNativeException("CloseBrowser", e); }
#elif UNITY_IOS && !UNITY_EDITOR
            try { _StashNativeCardBridgeCloseBrowser(); }
            catch (Exception e) { HandleNativeException("CloseBrowser", e); }
#endif
        }

        public void Dismiss()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { InitializeAndroidPlugin(); androidPluginInstance?.Call("dismiss"); }
            catch (Exception e) { HandleNativeException("Dismiss", e); }
#elif UNITY_IOS && !UNITY_EDITOR
            try { _StashNativeCardBridgeDismiss(); }
            catch (Exception e) { HandleNativeException("Dismiss", e); }
#endif
        }

        public void ResetPresentationState()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { InitializeAndroidPlugin(); androidPluginInstance?.Call("resetPresentationState"); }
            catch (Exception e) { HandleNativeException("ResetPresentationState", e); }
#elif UNITY_IOS && !UNITY_EDITOR
            try { _StashNativeCardBridgeResetPresentationState(); }
            catch (Exception e) { HandleNativeException("ResetPresentationState", e); }
#endif
        }

        public bool IsCurrentlyPresented
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                try { InitializeAndroidPlugin(); return androidPluginInstance != null && androidPluginInstance.Call<bool>("isCurrentlyPresented"); }
                catch (Exception e) { HandleNativeException("IsCurrentlyPresented", e); return false; }
#elif UNITY_IOS && !UNITY_EDITOR
                try { return _StashNativeCardBridgeIsCurrentlyPresented(); }
                catch (Exception e) { HandleNativeException("IsCurrentlyPresented", e); return false; }
#else
                return false;
#endif
            }
        }

        public bool IsPurchaseProcessing
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                try { InitializeAndroidPlugin(); return androidPluginInstance != null && androidPluginInstance.Call<bool>("isPurchaseProcessing"); }
                catch (Exception e) { HandleNativeException("IsPurchaseProcessing", e); return false; }
#elif UNITY_IOS && !UNITY_EDITOR
                try { return _StashNativeCardBridgeIsPurchaseProcessing(); }
                catch (Exception e) { HandleNativeException("IsPurchaseProcessing", e); return false; }
#else
                return false;
#endif
            }
        }

        #endregion

        #region Internal

        private IEnumerator OpenCardOrModalInternal(string url, Action dismissCallback, Action successCallback, Action failureCallback, bool isModal, StashNativeCardConfig? cardConfig, StashNativeModalConfig? modalConfig)
        {
            if (string.IsNullOrEmpty(url)) yield break;
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                url = "https://" + url;

            _currentDismissCallback = dismissCallback;
            _currentSuccessCallback = successCallback;
            _currentFailureCallback = failureCallback;

#if UNITY_ANDROID && !UNITY_EDITOR
            InitializeAndroidPlugin();
            if (androidPluginInstance != null)
            {
                try
                {
                    if (isModal)
                    {
                        if (modalConfig.HasValue)
                        {
                            var c = modalConfig.Value;
                            androidPluginInstance.Call("openModalWithConfig", url, c.showDragBar, c.allowDismiss,
                                c.phoneWidthRatioPortrait, c.phoneHeightRatioPortrait, c.phoneWidthRatioLandscape, c.phoneHeightRatioLandscape,
                                c.tabletWidthRatioPortrait, c.tabletHeightRatioPortrait, c.tabletWidthRatioLandscape, c.tabletHeightRatioLandscape);
                        }
                        else
                            androidPluginInstance.Call("openModal", url);
                    }
                    else
                    {
                        if (cardConfig.HasValue)
                        {
                            var c = cardConfig.Value;
                            androidPluginInstance.Call("openCardWithConfig", url, c.forcePortrait,
                                Mathf.Clamp(c.cardHeightRatioPortrait, 0.1f, 1f), Mathf.Clamp(c.cardWidthRatioLandscape, 0.1f, 1f), Mathf.Clamp(c.cardHeightRatioLandscape, 0.1f, 1f),
                                Mathf.Clamp(c.tabletWidthRatioPortrait, 0.1f, 1f), Mathf.Clamp(c.tabletHeightRatioPortrait, 0.1f, 1f),
                                Mathf.Clamp(c.tabletWidthRatioLandscape, 0.1f, 1f), Mathf.Clamp(c.tabletHeightRatioLandscape, 0.1f, 1f));
                        }
                        else
                            androidPluginInstance.Call("openCard", url);
                    }
                }
                catch (Exception e)
                {
                    HandleNativeException(isModal ? "OpenModal" : "OpenCard", e);
                    Application.OpenURL(url);
                }
            }
            else
                Application.OpenURL(url);
#elif UNITY_IOS && !UNITY_EDITOR
            try
            {
                if (!_StashNativeCardBridgeIsSDKAvailable())
                {
                    Debug.LogWarning("StashNative: iOS StashNative.xcframework not linked. Add StashNative.xcframework to Plugins/iOS and set Embed & Sign. Falling back to system browser.");
                    Application.OpenURL(url);
                    yield break;
                }
                if (isModal)
                {
                    if (modalConfig.HasValue)
                    {
                        var c = modalConfig.Value;
                        _StashNativeCardBridgeOpenModalWithConfig(url, c.showDragBar, c.allowDismiss,
                            c.phoneWidthRatioPortrait, c.phoneHeightRatioPortrait, c.phoneWidthRatioLandscape, c.phoneHeightRatioLandscape,
                            c.tabletWidthRatioPortrait, c.tabletHeightRatioPortrait, c.tabletWidthRatioLandscape, c.tabletHeightRatioLandscape);
                    }
                    else
                        _StashNativeCardBridgeOpenModal(url);
                }
                else
                {
                    if (cardConfig.HasValue)
                    {
                        var c = cardConfig.Value;
                        _StashNativeCardBridgeOpenCardWithConfig(url, c.forcePortrait,
                            Mathf.Clamp(c.cardHeightRatioPortrait, 0.1f, 1f), Mathf.Clamp(c.cardWidthRatioLandscape, 0.1f, 1f), Mathf.Clamp(c.cardHeightRatioLandscape, 0.1f, 1f),
                            Mathf.Clamp(c.tabletWidthRatioPortrait, 0.1f, 1f), Mathf.Clamp(c.tabletHeightRatioPortrait, 0.1f, 1f),
                            Mathf.Clamp(c.tabletWidthRatioLandscape, 0.1f, 1f), Mathf.Clamp(c.tabletHeightRatioLandscape, 0.1f, 1f));
                    }
                    else
                        _StashNativeCardBridgeOpenCard(url);
                }
            }
            catch (Exception e)
            {
                HandleNativeException(isModal ? "OpenModal" : "OpenCard", e);
                Application.OpenURL(url);
            }
#elif UNITY_EDITOR
            OpenEditorTestWindow(url, isModal);
#else
            Application.OpenURL(url);
#endif
        }

        private void HandleNativeException(string operation, Exception exception)
        {
            Debug.LogError("[StashNative] Exception in " + operation + ": " + exception.Message);
            Debug.LogException(exception);
            OnNativeException?.Invoke(operation, exception);
        }

#if UNITY_EDITOR
        public void OnEditorPaymentSuccess() { _currentSuccessCallback?.Invoke(); OnPaymentSuccess?.Invoke(); _currentSuccessCallback = null; }
        public void OnEditorPaymentFailure() { _currentFailureCallback?.Invoke(); OnPaymentFailure?.Invoke(); _currentFailureCallback = null; }
        public void OnEditorOptinResponse(string optinType) => OnOptinResponse?.Invoke(optinType ?? "");
        public void OnEditorDismissCatalog() { _currentDismissCallback?.Invoke(); OnDialogDismissed?.Invoke(); _currentDismissCallback = null; }

        private void OpenEditorTestWindow(string url, bool isModal)
        {
            try
            {
                var editorWindowType = Type.GetType("Stash.Editor.StashEditorPluginWindow, Assembly-CSharp-Editor") ?? Type.GetType("Stash.Editor.StashEditorPluginWindow");
                if (editorWindowType == null) return;
                if (isModal)
                {
                    var openModal = editorWindowType.GetMethod("OpenModal", new[] { typeof(string), typeof(StashNativeModalConfig?) });
                    if (openModal != null) openModal.Invoke(null, new object[] { url, null });
                }
                else
                {
                    var openCard = editorWindowType.GetMethod("OpenCard", new[] { typeof(string) });
                    if (openCard != null) openCard.Invoke(null, new object[] { url });
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("StashNative: Could not open Editor test window: " + e.Message);
            }
        }
#endif

        #endregion
    }
}
