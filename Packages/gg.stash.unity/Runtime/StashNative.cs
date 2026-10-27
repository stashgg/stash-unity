using System;
using System.Collections;
using UnityEngine;
using System.Runtime.InteropServices;

namespace Stash.Native
{
    /// <summary>Orientation of the checkout card or Safari sheet on phones. Ignored on tablets.</summary>
    public enum StashNativeOrientationPreference
    {
        /// <summary>Follow the host app's current orientation.</summary>
        FollowHost = 0,
        /// <summary>Present portrait checkout even in landscape-only games; the game's orientation is restored afterwards.</summary>
        Portrait = 1
    }

    /// <summary>Reason the QR scanner could not use the camera. See <see cref="StashNative.OnScannerError"/>.</summary>
    public enum StashNativeScannerError
    {
        Unknown = 0,
        /// <summary>User denied camera access. The scanner stays open and offers a Settings button.</summary>
        CameraPermissionDenied,
        /// <summary>Device has no usable camera.</summary>
        CameraUnavailable,
        /// <summary>Camera session setup failed; retry later.</summary>
        CameraConfigurationFailed,
        /// <summary>iOS only: <c>NSCameraUsageDescription</c> is missing. Set Camera Usage Description in Player Settings.</summary>
        MissingCameraUsageDescription,
        /// <summary>Android only: the CAMERA permission was stripped from the merged manifest.</summary>
        CameraPermissionNotDeclared,
        /// <summary>
        /// No scanner can be shown: the optional <c>gg.stash.unity.codelink</c> package is not installed, or on Android its
        /// CameraX/ZXing dependencies are missing from the build. Nothing is presented and no dismissal follows.
        /// </summary>
        ScannerUnavailable
    }

    /// <summary>Android-only: foreground keep-alive during external browser / Custom Tabs (see stash-native docs).</summary>
    public struct StashNativeKeepAliveConfig
    {
        public string notificationTitle;
        public string notificationText;
        /// <summary>Android drawable resource id; use 0 for the library default icon.</summary>
        public int notificationIconResId;
    }

    /// <summary>
    /// Responsive checkout card sizing. Dimensions are points of web content, excluding safe areas.
    /// The card shrinks to fit shorter intrinsic content and is capped by <see cref="maximumContentHeight"/>.
    /// A new instance carries the native defaults, so <c>new StashNativeCardConfig { orientationPreference = ... }</c> is safe.
    /// </summary>
    public class StashNativeCardConfig
    {
        /// <summary>Preferred content width in points. Default 400.</summary>
        public float preferredContentWidth = 400f;
        /// <summary>Preferred (resting) content height in points. Default 560.</summary>
        public float preferredContentHeight = 560f;
        /// <summary>Height cap in points. 0 uses all available height. Default 720.</summary>
        public float maximumContentHeight = 720f;
        /// <summary>Margin between the card and the screen edges in points. Default 16.</summary>
        public float edgeMargin = 16f;
        /// <summary>Whether the user can dismiss the card (swipe, backdrop tap, back button). Default true.</summary>
        public bool allowDismiss = true;
        /// <summary>
        /// When false, the card stays open after the payment success/failure callback until closed by the page, user, or host.
        /// Android note: Unity is paused while the card is on screen, so with <c>autoClose = false</c> the payment callbacks
        /// are delivered only after the card closes. Default true.
        /// </summary>
        public bool autoClose = true;
        /// <summary>Portrait checkout on phones regardless of the game's orientation. Default FollowHost.</summary>
        public StashNativeOrientationPreference orientationPreference = StashNativeOrientationPreference.FollowHost;

        /// <summary>Native defaults. Equivalent to <c>new StashNativeCardConfig()</c>.</summary>
        public static StashNativeCardConfig Default => new StashNativeCardConfig();

        [Obsolete("forcePortrait was removed in 3.0.0. Use orientationPreference = StashNativeOrientationPreference.Portrait.", true)]
        public bool forcePortrait;
        [Obsolete("backgroundColor was removed in 3.0.0. The checkout themes itself via the theme= URL parameter.", true)]
        public string backgroundColor;
        [Obsolete("Ratio sizing was removed in 3.0.0. Use preferredContentWidth / preferredContentHeight / maximumContentHeight / edgeMargin (points).", true)]
        public float cardHeightRatioPortrait, cardWidthRatioLandscape, cardHeightRatioLandscape,
            tabletWidthRatioPortrait, tabletHeightRatioPortrait, tabletWidthRatioLandscape, tabletHeightRatioLandscape;
    }

    /// <summary>Options for <see cref="StashNative.OpenBrowser"/>. iOS only; Android ignores it.</summary>
    public class StashNativeBrowserConfig
    {
        /// <summary>Portrait Safari on iPhone regardless of the game's orientation. Default FollowHost.</summary>
        public StashNativeOrientationPreference orientationPreference = StashNativeOrientationPreference.FollowHost;

        public static StashNativeBrowserConfig Default => new StashNativeBrowserConfig();
    }

    /// <summary>Copy shown by the QR scanner. Null or empty strings keep the built-in wording. A new instance carries the defaults.</summary>
    public class StashNativeScannerConfig
    {
        /// <summary>Default: "Scan QR code".</summary>
        public string headerText;
        /// <summary>Default: "Scan the webshop code".</summary>
        public string calloutTitle;
        /// <summary>Default: "Keep the QR code centered inside the frame."</summary>
        public string calloutSubtitle;
        /// <summary>Default: "Camera unavailable".</summary>
        public string cameraUnavailableText;
        /// <summary>Default: "Connected".</summary>
        public string connectedText;
        /// <summary>False removes the callout and gives its space to the scan frame. Default true.</summary>
        public bool showCallout = true;

        public static StashNativeScannerConfig Default => new StashNativeScannerConfig();
    }

    /// <summary>Removed in 3.0.0. The centered modal has no native counterpart; use <see cref="StashNativeCardConfig"/> with <see cref="StashNative.OpenCard"/>.</summary>
    [Obsolete("StashNativeModalConfig was removed in Stash for Unity 3.0.0. Use StashNativeCardConfig with OpenCard.", true)]
    public struct StashNativeModalConfig { }

    /// <summary>
    /// Unity entry point for the Stash Native SDK. All methods must be called from the Unity main thread.
    /// Native callbacks are delivered to the GameObject named <c>StashNative</c>; the component enforces that name.
    /// </summary>
    public class StashNative : MonoBehaviour
    {
        /// <summary>GameObject name both native bridges send callbacks to.</summary>
        public const string ReceiverName = "StashNative";

        #region Singleton

        private static StashNative _instance;

        /// <summary>The singleton. Created on first access and persisted across scenes.</summary>
        public static StashNative Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject(ReceiverName);
                    _instance = go.AddComponent<StashNative>();
                    if (Application.isPlaying) DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        /// <summary>The singleton if it exists, otherwise null. Never creates one; safe to call from Editor code outside Play mode.</summary>
        public static StashNative InstanceOrNull => _instance;

        private void Awake()
        {
            if (_instance == null || _instance == this)
            {
                _instance = this;
                if (gameObject.name != ReceiverName)
                {
                    Debug.LogWarning("StashNative: renaming GameObject '" + gameObject.name + "' to '" + ReceiverName + "' so native callbacks can reach it.");
                    gameObject.name = ReceiverName;
                }
                if (transform.parent != null)
                {
                    Debug.LogWarning("StashNative: component must live on a root GameObject to survive scene loads; detaching from parent.");
                    transform.SetParent(null, true);
                }
                if (Application.isPlaying) DontDestroyOnLoad(gameObject);
            }
            else
            {
                Debug.LogWarning("StashNative: duplicate instance on '" + gameObject.name + "' removed; use StashNative.Instance.");
                Destroy(this); // only the component, never the host object
            }
        }

        private void OnDestroy()
        {
            if (_instance != this) return;
            _instance = null;
            ClearCardCallbacks();
            ClearScannerCallbacks();
            _generation++; // anything still queued from native is dropped
            // Detach so native stops targeting a receiver that no longer exists.
#if UNITY_ANDROID && !UNITY_EDITOR
            try { androidPluginInstance?.Call("detach"); } catch (Exception) { }
#elif UNITY_IOS && !UNITY_EDITOR
            try { _StashNativeBridgeDetach(); } catch (Exception) { }
#endif
        }

        #endregion

        #region Events

        /// <summary>
        /// Fired after the checkout card is dismissed by the user, by the page (<c>window.close()</c>), by <see cref="Dismiss"/>, or by the host.
        /// Not fired after a payment auto-close, a network error, or an external-payment browser handoff; those end the card through their own events.
        /// </summary>
        public event Action OnCardDismissed;
        /// <summary>Fired when payment succeeds. With <c>autoClose</c> on, the card has already closed, so a new card can be opened from this callback. Argument: order payload from the checkout page, or an empty string when the page sent none (never null).</summary>
        public event Action<string> OnPaymentSuccess;
        /// <summary>Fired when payment fails. With <c>autoClose</c> on, the card has already closed.</summary>
        public event Action OnPaymentFailure;
        /// <summary>Fired when the checkout page finishes loading (argument: load time in ms). Android: delivered after the card closes, because Unity is paused while the card is on screen.</summary>
        public event Action<double> OnPageLoaded;
        /// <summary>Fired when checkout ends because of a network error. The card is already dismissed and <see cref="OnCardDismissed"/> does not fire.</summary>
        public event Action OnNetworkError;
        /// <summary>
        /// Fired when the checkout page sends the payment to the browser (e.g. Klarna, Cash App). Informational; do not open another
        /// card from this callback. By default the card closes without <see cref="OnCardDismissed"/> and the presentation ends with
        /// <see cref="OnBrowserDismissed"/> (also when the browser cannot open). When the page passes <c>keepOpen</c>, the browser opens
        /// over the card, no browser event follows, and the card ends later with its own events.
        /// </summary>
        public event Action<string> OnExternalPayment;
        /// <summary>
        /// Fired when the browser (Chrome Custom Tabs / SFSafariViewController) closes, or cannot open, after <see cref="OpenBrowser"/>
        /// or an external payment that closed the card. Not fired for a <c>keepOpen</c> browser, after <see cref="DismissBrowser"/>,
        /// or after <see cref="ResetPresentationState"/>.
        /// </summary>
        public event Action OnBrowserDismissed;
        /// <summary>Fired once with the raw QR payload after the scanner card has closed. Requires <c>gg.stash.unity.codelink</c>.</summary>
        public event Action<string> OnQrCodeScanned;
        /// <summary>Fired when the scanner closes without a scan result.</summary>
        public event Action OnScannerDismissed;
        /// <summary>Fired when the scanner cannot use the camera. Except for <see cref="StashNativeScannerError.ScannerUnavailable"/>, the scanner stays open. See <see cref="LastScannerErrorMessage"/> for the platform's description.</summary>
        public event Action<StashNativeScannerError> OnScannerError;
        /// <summary>Fired when a native call or a user callback throws. Arguments: operation name, exception.</summary>
        public event Action<string, Exception> OnNativeException;

        /// <summary>Platform description of the most recent <see cref="OnScannerError"/> (iOS NSError text or Android error name). Empty if none.</summary>
        public string LastScannerErrorMessage { get; private set; } = "";

        [Obsolete("Renamed to OnCardDismissed in 3.0.0. Note: payment auto-close and network errors no longer emit a dismiss event.", true)]
        public event Action OnDialogDismissed { add { } remove { } }
        [Obsolete("Renamed to OnBrowserDismissed in 3.0.0.", true)]
        public event Action OnBrowserClosed { add { } remove { } }
        [Obsolete("Opt-in responses were removed from Stash Native 3.0.0; the checkout page handles channel selection itself.", true)]
        public event Action<string> OnOptinResponse { add { } remove { } }

        // Per-call callbacks for the current card. Cleared when the card's session ends: user/page dismiss, payment result
        // with autoClose on, network error, or, after an external-payment handoff, the browser closing (default handoff) or a
        // silent DismissBrowser(). A keepOpen handoff leaves the card open, so its callbacks stay until the card's own ending.
        private Action _cardDismissCallback;
        private Action<string> _cardSuccessCallback;
        private Action _cardFailureCallback;
        private Action _cardNetworkErrorCallback;
        private Action<string> _cardExternalPaymentCallback;
        private bool _cardAutoClose = true;
        private bool _cardSessionActive;
        // Set by an external-payment handoff. Native does not say whether the page passed keepOpen, so the next signal decides:
        // a card event means the card stayed open; OnBrowserDismissed means it had closed and the session is over.
        private bool _cardHandoffPending;

        // Per-call callbacks for the current scanner. Cleared on scan, dismiss, or ScannerUnavailable.
        private Action<string> _scannerScannedCallback;
        private Action _scannerDismissCallback;
        private Action<StashNativeScannerError> _scannerErrorCallback;
        private bool _scannerSessionActive;

        // Session generation. Every accepted open and every reset bumps it; the bridges echo it in each session-scoped
        // callback, and events from an older generation are dropped. This stops a message queued by a card that was just
        // reset from completing the card opened after it.
        private int _generation;

        private void ClearCardCallbacks()
        {
            _cardDismissCallback = null;
            _cardSuccessCallback = null;
            _cardFailureCallback = null;
            _cardNetworkErrorCallback = null;
            _cardExternalPaymentCallback = null;
            _cardSessionActive = false;
            _cardHandoffPending = false;
        }

        private void ClearScannerCallbacks()
        {
            _scannerScannedCallback = null;
            _scannerDismissCallback = null;
            _scannerErrorCallback = null;
            _scannerSessionActive = false;
        }

        /// <summary>Runs a user callback so one throwing handler cannot suppress the others; failures surface on <see cref="OnNativeException"/>.</summary>
        private void Invoke(string what, Action a)
        {
            if (a == null) return;
            try { a(); }
            catch (Exception e) { ReportCallbackException(what, e); }
        }

        private void Invoke<T>(string what, Action<T> a, T arg)
        {
            if (a == null) return;
            try { a(arg); }
            catch (Exception e) { ReportCallbackException(what, e); }
        }

        private void ReportCallbackException(string what, Exception e)
        {
            Debug.LogError("[StashNative] Exception in " + what + " handler: " + e.Message);
            Debug.LogException(e);
            try { OnNativeException?.Invoke(what, e); } catch (Exception) { }
        }

        /// <summary>Multicast events are invoked subscriber by subscriber so one throwing listener does not stop the rest.</summary>
        private void Raise(string what, Delegate multicast, params object[] args)
        {
            if (multicast == null) return;
            foreach (var d in multicast.GetInvocationList())
            {
                try { d.DynamicInvoke(args); }
                catch (Exception e) { ReportCallbackException(what, e.InnerException ?? e); }
            }
        }

        #endregion

        #region Native callbacks (shared handlers)

        /// <summary>Splits "generation|payload" and returns false when the event belongs to an earlier session.</summary>
        private bool Accept(string message, out string payload)
        {
            payload = message ?? "";
            int bar = payload.IndexOf('|');
            if (bar < 0) return true; // untagged (editor / legacy)
            bool ok = int.TryParse(payload.Substring(0, bar), out int gen) && gen == _generation;
            payload = payload.Substring(bar + 1);
            if (!ok) Debug.Log("[StashNative] Dropped a native event from an earlier session (generation mismatch).");
            return ok;
        }

        private void HandlePaymentSuccess(string order)
        {
            var o = order ?? "";
            var cb = _cardSuccessCallback;
            if (_cardAutoClose) ClearCardCallbacks();
            Invoke("successCallback", cb, o);
            Raise("OnPaymentSuccess", OnPaymentSuccess, o);
        }

        private void HandlePaymentFailure()
        {
            var cb = _cardFailureCallback;
            if (_cardAutoClose) ClearCardCallbacks();
            Invoke("failureCallback", cb);
            Raise("OnPaymentFailure", OnPaymentFailure);
        }

        private void HandleCardDismissed()
        {
            var cb = _cardDismissCallback;
            ClearCardCallbacks();
            Invoke("dismissCallback", cb);
            Raise("OnCardDismissed", OnCardDismissed);
        }

        private void HandleNetworkError()
        {
            var cb = _cardNetworkErrorCallback;
            ClearCardCallbacks();
            Invoke("networkErrorCallback", cb);
            Raise("OnNetworkError", OnNetworkError);
        }

        private void HandleExternalPayment(string url)
        {
            var u = url ?? "";
            // Not an ending: with keepOpen the card stays and ends later with its own events. See _cardHandoffPending.
            if (_cardSessionActive) _cardHandoffPending = true;
            Invoke("externalPaymentCallback", _cardExternalPaymentCallback, u);
            Raise("OnExternalPayment", OnExternalPayment, u);
        }

        private void HandlePageLoaded(string loadTimeMs)
        {
            if (double.TryParse(loadTimeMs, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double loadTime))
                Raise("OnPageLoaded", OnPageLoaded, loadTime);
        }

        private void HandleBrowserDismissed()
        {
            // After a default handoff the card already closed; the browser closing ends that presentation.
            if (_cardHandoffPending) ClearCardCallbacks();
            Raise("OnBrowserDismissed", OnBrowserDismissed);
        }

        private void HandleQrCodeScanned(string content)
        {
            var c = content ?? "";
            var cb = _scannerScannedCallback;
            ClearScannerCallbacks();
            Invoke("scannedCallback", cb, c);
            Raise("OnQrCodeScanned", OnQrCodeScanned, c);
        }

        private void HandleScannerDismissed()
        {
            var cb = _scannerDismissCallback;
            ClearScannerCallbacks();
            Invoke("scannerDismissCallback", cb);
            Raise("OnScannerDismissed", OnScannerDismissed);
        }

        private void HandleScannerError(StashNativeScannerError error, string message)
        {
            LastScannerErrorMessage = message ?? "";
            var cb = _scannerErrorCallback;
            if (error == StashNativeScannerError.ScannerUnavailable) ClearScannerCallbacks();
            Invoke("scannerErrorCallback", cb, error);
            Raise("OnScannerError", OnScannerError, error);
        }

        /// <summary>Native reports ScannerUnavailable asynchronously; fallback paths match that so callers never see the error inside OpenScanner.</summary>
        private IEnumerator ScannerUnavailableNextFrame(int generation)
        {
            yield return null;
            if (generation == _generation) HandleScannerError(StashNativeScannerError.ScannerUnavailable, "scanner module unavailable");
        }

        #endregion

        #region Native Plugin

#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject androidPluginInstance;
        private bool androidInitFailed;

        private void InitializeAndroidPlugin()
        {
            if (androidPluginInstance != null || androidInitFailed) return;
            try
            {
                var bridgeClass = new AndroidJavaClass("com.stash.unity.StashNativeUnityBridge");
                androidPluginInstance = bridgeClass.CallStatic<AndroidJavaObject>("getInstance");
            }
            catch (Exception e)
            {
                androidInitFailed = true;
                Debug.LogError("StashNative: Android bridge com.stash.unity.StashNativeUnityBridge not found. If Minify is enabled, keep the class (the package annotates it with @Keep; check your ProGuard/R8 rules). " + e.Message);
                OnNativeException?.Invoke("InitializeAndroidPlugin", e);
            }
        }

        private static StashNativeScannerError ParseAndroidScannerError(string name)
        {
            switch (name)
            {
                case "CAMERA_PERMISSION_DENIED": return StashNativeScannerError.CameraPermissionDenied;
                case "CAMERA_UNAVAILABLE": return StashNativeScannerError.CameraUnavailable;
                case "CAMERA_CONFIGURATION_FAILED": return StashNativeScannerError.CameraConfigurationFailed;
                case "CAMERA_PERMISSION_NOT_DECLARED": return StashNativeScannerError.CameraPermissionNotDeclared;
                case "SCANNER_UNAVAILABLE": return StashNativeScannerError.ScannerUnavailable;
                default: return StashNativeScannerError.Unknown;
            }
        }

        // Called by StashNativeUnityBridge via UnitySendMessage. Private is fine: SendMessage reaches private methods,
        // and the linker keeps all members of a MonoBehaviour once the type is marked.
        private void OnAndroidPaymentSuccess(string m) { if (Accept(m, out var p)) HandlePaymentSuccess(p); }
        private void OnAndroidPaymentFailure(string m) { if (Accept(m, out _)) HandlePaymentFailure(); }
        private void OnAndroidCardDismissed(string m) { if (Accept(m, out _)) HandleCardDismissed(); }
        private void OnAndroidScannerDismissed(string m) { if (Accept(m, out _)) HandleScannerDismissed(); }
        private void OnAndroidNetworkError(string m) { if (Accept(m, out _)) HandleNetworkError(); }
        private void OnAndroidExternalPayment(string m) { if (Accept(m, out var p)) HandleExternalPayment(p); }
        private void OnAndroidPageLoaded(string m) { if (Accept(m, out var p)) HandlePageLoaded(p); }
        private void OnAndroidBrowserDismissed(string m) => HandleBrowserDismissed();
        private void OnAndroidQrCodeScanned(string m) { if (Accept(m, out var p)) HandleQrCodeScanned(p); }
        private void OnAndroidScannerError(string m) { if (Accept(m, out var p)) HandleScannerError(ParseAndroidScannerError(p), p); }

#elif UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern bool _StashNativeBridgeIsSDKAvailable();
        [DllImport("__Internal")] private static extern bool _StashNativeBridgeIsScannerAvailable();
        [DllImport("__Internal")] private static extern string _StashNativeBridgeSdkVersion();
        [DllImport("__Internal")] private static extern void _StashNativeBridgeOpenCard(int generation, string url, bool hasConfig, float preferredContentWidth, float preferredContentHeight, float maximumContentHeight, float edgeMargin, bool allowDismiss, bool autoClose, int orientationPreference);
        [DllImport("__Internal")] private static extern void _StashNativeBridgeOpenBrowser(string url, int orientationPreference);
        [DllImport("__Internal")] private static extern void _StashNativeBridgeOpenScanner(int generation, bool hasConfig, string headerText, string calloutTitle, string calloutSubtitle, string cameraUnavailableText, string connectedText, bool showCallout);
        [DllImport("__Internal")] private static extern void _StashNativeBridgeCloseBrowser();
        [DllImport("__Internal")] private static extern void _StashNativeBridgeDismissBrowser();
        [DllImport("__Internal")] private static extern void _StashNativeBridgeDismiss();
        [DllImport("__Internal")] private static extern void _StashNativeBridgeResetPresentationState();
        [DllImport("__Internal")] private static extern void _StashNativeBridgeDetach();
        [DllImport("__Internal")] private static extern bool _StashNativeBridgeIsCurrentlyPresented();
        [DllImport("__Internal")] private static extern bool _StashNativeBridgeIsPurchaseProcessing();
        [DllImport("__Internal")] private static extern void _StashNativeBridgeSetInspectableWebViewsEnabled(bool enabled);
        [DllImport("__Internal")] private static extern bool _StashNativeBridgeIsInspectableWebViewsEnabled();

        private static StashNativeScannerError ParseIOSScannerError(string code)
        {
            // Matches StashNativeScannerError in StashNative.h.
            switch (code)
            {
                case "1": return StashNativeScannerError.CameraPermissionDenied;
                case "2": return StashNativeScannerError.CameraUnavailable;
                case "3": return StashNativeScannerError.CameraConfigurationFailed;
                case "4": return StashNativeScannerError.MissingCameraUsageDescription;
                case "5": return StashNativeScannerError.ScannerUnavailable;
                default: return StashNativeScannerError.Unknown;
            }
        }

        // Called by StashNativeBridge.mm via UnitySendMessage (see Android note above).
        private void OnIOSPaymentSuccess(string m) { if (Accept(m, out var p)) HandlePaymentSuccess(p); }
        private void OnIOSPaymentFailure(string m) { if (Accept(m, out _)) HandlePaymentFailure(); }
        private void OnIOSCardDismissed(string m) { if (Accept(m, out _)) HandleCardDismissed(); }
        private void OnIOSScannerDismissed(string m) { if (Accept(m, out _)) HandleScannerDismissed(); }
        private void OnIOSNetworkError(string m) { if (Accept(m, out _)) HandleNetworkError(); }
        private void OnIOSExternalPayment(string m) { if (Accept(m, out var p)) HandleExternalPayment(p); }
        private void OnIOSPageLoaded(string m) { if (Accept(m, out var p)) HandlePageLoaded(p); }
        private void OnIOSBrowserDismissed(string m) => HandleBrowserDismissed();
        private void OnIOSQrCodeScanned(string m) { if (Accept(m, out var p)) HandleQrCodeScanned(p); }
        private void OnIOSScannerError(string m)
        {
            // payload is "code;description"
            if (!Accept(m, out var p)) return;
            int semi = p.IndexOf(';');
            string code = semi < 0 ? p : p.Substring(0, semi);
            string desc = semi < 0 ? "" : p.Substring(semi + 1);
            HandleScannerError(ParseIOSScannerError(code), desc);
        }
#endif

        #endregion

        #region Public Methods

        /// <summary>Version of the embedded Stash Native SDK (e.g. "3.0.0"). Empty in the Editor or when the native library is missing.</summary>
        public string SdkVersion
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                try { InitializeAndroidPlugin(); return androidPluginInstance != null ? (androidPluginInstance.Call<string>("getVersion") ?? "") : ""; }
                catch (Exception e) { HandleNativeException("SdkVersion", e); return ""; }
#elif UNITY_IOS && !UNITY_EDITOR
                try { return _StashNativeBridgeIsSDKAvailable() ? (_StashNativeBridgeSdkVersion() ?? "") : ""; }
                catch (Exception e) { HandleNativeException("SdkVersion", e); return ""; }
#else
                return "";
#endif
            }
        }

        /// <summary>True when the QR scanner module (<c>gg.stash.unity.codelink</c>) is present in this build, so a "Link account" button can be shown or hidden up front. Always true in the Editor, where the scanner is simulated.</summary>
        public bool IsScannerAvailable
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                try { InitializeAndroidPlugin(); return androidPluginInstance != null && androidPluginInstance.Call<bool>("isScannerAvailable"); }
                catch (Exception e) { HandleNativeException("IsScannerAvailable", e); return false; }
#elif UNITY_IOS && !UNITY_EDITOR
                try { return _StashNativeBridgeIsSDKAvailable() && _StashNativeBridgeIsScannerAvailable(); }
                catch (Exception e) { HandleNativeException("IsScannerAvailable", e); return false; }
#elif UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>Android only. Enables the short foreground keep-alive service during external browser flows.</summary>
        public void SetKeepAliveEnabled(bool enabled)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                InitializeAndroidPlugin();
                androidPluginInstance?.Call("setKeepAliveEnabled", enabled);
            }
            catch (Exception e) { HandleNativeException("SetKeepAliveEnabled", e); }
#endif
        }

        /// <summary>Android only. Whether the keep-alive service is enabled. False elsewhere.</summary>
        public bool IsKeepAliveEnabled
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                try { InitializeAndroidPlugin(); return androidPluginInstance != null && androidPluginInstance.Call<bool>("isKeepAliveEnabled"); }
                catch (Exception e) { HandleNativeException("IsKeepAliveEnabled", e); return false; }
#else
                return false;
#endif
            }
        }

        /// <summary>Android only. Notification text/icon for keep-alive (call after <see cref="SetKeepAliveEnabled"/> if customizing). Android 13+ shows the notification only if the app holds POST_NOTIFICATIONS.</summary>
        public void SetKeepAliveConfig(StashNativeKeepAliveConfig config)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                InitializeAndroidPlugin();
                androidPluginInstance?.Call("setKeepAliveConfig",
                    config.notificationTitle ?? "",
                    config.notificationText ?? "",
                    config.notificationIconResId);
            }
            catch (Exception e) { HandleNativeException("SetKeepAliveConfig", e); }
#endif
        }

        /// <summary>
        /// Android only. Image (JPEG or PNG bytes) shown full-screen behind the dim overlay while a portrait card is presented over a
        /// landscape game, hiding the black or distorted Unity surface during the orientation switch. Call right before <see cref="OpenCard"/>;
        /// the SDK consumes it on the next accepted open. Decoding happens on the calling thread, so keep captures small. Pass null to clear
        /// an image that was set for an open that never happened. No-op on other platforms.
        /// </summary>
        public void SetBackdropBytes(byte[] imageBytes)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                InitializeAndroidPlugin();
                if (androidPluginInstance == null) return;
                if (imageBytes == null)
                {
                    androidPluginInstance.Call("clearBackdrop");
                    return;
                }
                // sbyte[] maps to Java byte[] without Unity's obsolete Byte[] JNI conversion warning.
                var signed = new sbyte[imageBytes.Length];
                Buffer.BlockCopy(imageBytes, 0, signed, 0, imageBytes.Length);
                androidPluginInstance.Call("setBackdropBytes", (object)signed);
            }
            catch (Exception e) { HandleNativeException("SetBackdropBytes", e); }
#endif
        }

        /// <summary>Debug/QA only. Makes the SDK's checkout WebViews inspectable (Safari Web Inspector on iOS 16.4+, chrome://inspect on Android). Off by default. Do NOT enable in production builds; call before opening any checkout.</summary>
        public void SetInspectableWebViewsEnabled(bool enabled)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                InitializeAndroidPlugin();
                androidPluginInstance?.Call("setInspectableWebViewsEnabled", enabled);
            }
            catch (Exception e) { HandleNativeException("SetInspectableWebViewsEnabled", e); }
#elif UNITY_IOS && !UNITY_EDITOR
            try { _StashNativeBridgeSetInspectableWebViewsEnabled(enabled); }
            catch (Exception e) { HandleNativeException("SetInspectableWebViewsEnabled", e); }
#endif
        }

        /// <summary>Whether checkout WebView inspection is enabled. Default false.</summary>
        public bool IsInspectableWebViewsEnabled
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                try { InitializeAndroidPlugin(); return androidPluginInstance != null && androidPluginInstance.Call<bool>("isInspectableWebViewsEnabled"); }
                catch (Exception e) { HandleNativeException("IsInspectableWebViewsEnabled", e); return false; }
#elif UNITY_IOS && !UNITY_EDITOR
                try { return _StashNativeBridgeIsInspectableWebViewsEnabled(); }
                catch (Exception e) { HandleNativeException("IsInspectableWebViewsEnabled", e); return false; }
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Opens the URL in the responsive native checkout card. Returns false, without touching the current session, if a card or
        /// scanner is already open or the URL is empty; the native SDK ignores overlapping opens, so the wrapper refuses them up front.
        /// Exactly one of the per-call callbacks ends the card: <paramref name="dismissCallback"/> (user, page, host or <see cref="Dismiss"/>),
        /// <paramref name="successCallback"/> / <paramref name="failureCallback"/> (with <c>autoClose</c>; otherwise the card stays open and
        /// <paramref name="dismissCallback"/> follows later), <paramref name="networkErrorCallback"/>, or <paramref name="externalPaymentCallback"/>.
        /// Android: Unity is paused while the card is on screen, so callbacks that native raises mid-card are delivered after it closes.
        /// </summary>
        public bool OpenCard(string url, Action dismissCallback = null, Action<string> successCallback = null, Action failureCallback = null,
            StashNativeCardConfig config = null, Action networkErrorCallback = null, Action<string> externalPaymentCallback = null)
        {
            if (string.IsNullOrWhiteSpace(url)) { Debug.LogWarning("StashNative.OpenCard: empty URL."); return false; }
            if (IsSessionActive) { Debug.LogWarning("StashNative.OpenCard ignored: a card or scanner is already presented. Wait for it to end or call Dismiss()."); return false; }
            url = url.Trim();

            _generation++;
            _cardDismissCallback = dismissCallback;
            _cardSuccessCallback = successCallback;
            _cardFailureCallback = failureCallback;
            _cardNetworkErrorCallback = networkErrorCallback;
            _cardExternalPaymentCallback = externalPaymentCallback;
            _cardAutoClose = config == null || config.autoClose;
            _cardSessionActive = true;

#if UNITY_ANDROID && !UNITY_EDITOR
            InitializeAndroidPlugin();
            if (androidPluginInstance == null) return FallbackToSystemBrowser(url);
            try
            {
                if (config != null)
                    androidPluginInstance.Call("openCardWithConfig", _generation, url,
                        config.preferredContentWidth, config.preferredContentHeight, config.maximumContentHeight, config.edgeMargin,
                        config.allowDismiss, config.autoClose, (int)config.orientationPreference);
                else
                    androidPluginInstance.Call("openCard", _generation, url);
                return true;
            }
            catch (Exception e)
            {
                HandleNativeException("OpenCard", e);
                return FallbackToSystemBrowser(url);
            }
#elif UNITY_IOS && !UNITY_EDITOR
            try
            {
                if (!_StashNativeBridgeIsSDKAvailable())
                {
                    Debug.LogWarning("StashNative: iOS StashNative.xcframework not linked. Add StashNative.xcframework to Plugins/iOS and set Embed & Sign. Falling back to system browser.");
                    return FallbackToSystemBrowser(url);
                }
                if (config != null)
                    _StashNativeBridgeOpenCard(_generation, url, true,
                        config.preferredContentWidth, config.preferredContentHeight, config.maximumContentHeight, config.edgeMargin,
                        config.allowDismiss, config.autoClose, (int)config.orientationPreference);
                else
                    _StashNativeBridgeOpenCard(_generation, url, false, 0f, 0f, 0f, 0f, true, true, 0);
                return true;
            }
            catch (Exception e)
            {
                HandleNativeException("OpenCard", e);
                return FallbackToSystemBrowser(url);
            }
#elif UNITY_EDITOR
            return OpenEditorTestWindow(url, config ?? new StashNativeCardConfig());
#else
            return FallbackToSystemBrowser(url);
#endif
        }

        /// <summary>No native card: open the URL in the system browser and forget the per-call callbacks, which could never fire.</summary>
        private bool FallbackToSystemBrowser(string url)
        {
            ClearCardCallbacks();
            Application.OpenURL(url);
            return false;
        }

        [Obsolete("OpenModal was removed in Stash for Unity 3.0.0 (no native counterpart in Stash Native 3.x). Use OpenCard with StashNativeCardConfig.", true)]
        public void OpenModal(string url, Action dismissCallback = null, Action<string> successCallback = null, Action failureCallback = null, object config = null) { }

        /// <summary>
        /// Opens the QR scanner in the native card for webshop account linking. Requires the <c>gg.stash.unity.codelink</c> package
        /// (check <see cref="IsScannerAvailable"/>); without it <paramref name="errorCallback"/> / <see cref="OnScannerError"/> receive
        /// <see cref="StashNativeScannerError.ScannerUnavailable"/> on the next frame and nothing is presented.
        /// Returns false, without touching the current session, if a card or scanner is already open.
        /// iOS needs Camera Usage Description in Player Settings. The scanner follows the game's orientation; <see cref="Dismiss"/> closes it.
        /// </summary>
        public bool OpenScanner(Action<string> scannedCallback = null, Action dismissCallback = null, Action<StashNativeScannerError> errorCallback = null, StashNativeScannerConfig config = null)
        {
            if (IsSessionActive) { Debug.LogWarning("StashNative.OpenScanner ignored: a card or scanner is already presented."); return false; }

            _generation++;
            _scannerScannedCallback = scannedCallback;
            _scannerDismissCallback = dismissCallback;
            _scannerErrorCallback = errorCallback;
            _scannerSessionActive = true;

#if UNITY_ANDROID && !UNITY_EDITOR
            InitializeAndroidPlugin();
            if (androidPluginInstance == null) { StartCoroutine(ScannerUnavailableNextFrame(_generation)); return true; }
            try
            {
                if (config != null)
                    androidPluginInstance.Call("openScannerWithConfig", _generation,
                        config.headerText ?? "", config.calloutTitle ?? "", config.calloutSubtitle ?? "",
                        config.cameraUnavailableText ?? "", config.connectedText ?? "", config.showCallout);
                else
                    androidPluginInstance.Call("openScanner", _generation);
                return true;
            }
            catch (Exception e) { HandleNativeException("OpenScanner", e); ClearScannerCallbacks(); return false; }
#elif UNITY_IOS && !UNITY_EDITOR
            try
            {
                if (!_StashNativeBridgeIsSDKAvailable()) { StartCoroutine(ScannerUnavailableNextFrame(_generation)); return true; }
                if (config != null)
                    _StashNativeBridgeOpenScanner(_generation, true,
                        config.headerText ?? "", config.calloutTitle ?? "", config.calloutSubtitle ?? "",
                        config.cameraUnavailableText ?? "", config.connectedText ?? "", config.showCallout);
                else
                    _StashNativeBridgeOpenScanner(_generation, false, "", "", "", "", "", true);
                return true;
            }
            catch (Exception e) { HandleNativeException("OpenScanner", e); ClearScannerCallbacks(); return false; }
#elif UNITY_EDITOR
            return OpenEditorScannerWindow();
#else
            StartCoroutine(ScannerUnavailableNextFrame(_generation));
            return true;
#endif
        }

        /// <summary>Config-only overload so <c>OpenScanner(config)</c> compiles; results arrive on the global events.</summary>
        public bool OpenScanner(StashNativeScannerConfig config) => OpenScanner(null, null, null, config);

        /// <summary>Opens the URL in the platform browser (Chrome Custom Tabs on Android, SFSafariViewController on iOS). <paramref name="config"/> is iOS only. Falls back to the system browser if the native SDK is unavailable.</summary>
        public void OpenBrowser(string url, StashNativeBrowserConfig config = null)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            url = url.Trim();
#if UNITY_ANDROID && !UNITY_EDITOR
            InitializeAndroidPlugin();
            if (androidPluginInstance == null) { Application.OpenURL(url); return; }
            try { androidPluginInstance.Call("openBrowser", url); }
            catch (Exception e) { HandleNativeException("OpenBrowser", e); Application.OpenURL(url); }
#elif UNITY_IOS && !UNITY_EDITOR
            try
            {
                if (_StashNativeBridgeIsSDKAvailable())
                    _StashNativeBridgeOpenBrowser(url, config != null ? (int)config.orientationPreference : -1);
                else
                    Application.OpenURL(url);
            }
            catch (Exception e) { HandleNativeException("OpenBrowser", e); Application.OpenURL(url); }
#else
            Application.OpenURL(url);
#endif
        }

        /// <summary>
        /// Closes the SDK's browser and raises <see cref="OnBrowserDismissed"/>. On Android, call it when your payment deep link
        /// arrives while <see cref="IsCurrentlyPresented"/> is true, from an activity that is not <c>singleTask</c> (see the native
        /// browser docs). A <c>keepOpen</c> browser closes back to its card without a browser event.
        /// </summary>
        public void CloseBrowser()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { InitializeAndroidPlugin(); androidPluginInstance?.Call("closeBrowser"); }
            catch (Exception e) { HandleNativeException("CloseBrowser", e); }
#elif UNITY_IOS && !UNITY_EDITOR
            try { _StashNativeBridgeCloseBrowser(); }
            catch (Exception e) { HandleNativeException("CloseBrowser", e); }
#endif
        }

        /// <summary>
        /// iOS only: closes Safari silently, without <see cref="OnBrowserDismissed"/> or payment events, for when your payment deep
        /// link has arrived and the game handles the result itself. Ends an external-payment handoff's session. A <c>keepOpen</c>
        /// Safari closes back to its card, which still reports its own events. No-op on Android.
        /// </summary>
        public void DismissBrowser()
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (_cardHandoffPending) ClearCardCallbacks();
            try { _StashNativeBridgeDismissBrowser(); }
            catch (Exception e) { HandleNativeException("DismissBrowser", e); }
#endif
        }

        /// <summary>Dismisses the current card or scanner, even while purchase processing blocks user dismissal. On iOS this also closes an open Safari view (raising <see cref="OnBrowserDismissed"/>); on Android it never touches Custom Tabs.</summary>
        public void Dismiss()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { InitializeAndroidPlugin(); androidPluginInstance?.Call("dismiss"); }
            catch (Exception e) { HandleNativeException("Dismiss", e); }
#elif UNITY_IOS && !UNITY_EDITOR
            try { _StashNativeBridgeDismiss(); }
            catch (Exception e) { HandleNativeException("Dismiss", e); }
#elif UNITY_EDITOR
            EditorCall("Dismiss");
#endif
        }

        /// <summary>Tears down the native session without callbacks (queued on the native main thread). Per-call callbacks are cleared and any event still in flight from the old session is dropped.</summary>
        public void ResetPresentationState()
        {
            _generation++;
            ClearCardCallbacks();
            ClearScannerCallbacks();
#if UNITY_ANDROID && !UNITY_EDITOR
            try { InitializeAndroidPlugin(); androidPluginInstance?.Call("resetPresentationState"); }
            catch (Exception e) { HandleNativeException("ResetPresentationState", e); }
#elif UNITY_IOS && !UNITY_EDITOR
            try { _StashNativeBridgeResetPresentationState(); }
            catch (Exception e) { HandleNativeException("ResetPresentationState", e); }
#elif UNITY_EDITOR
            EditorCall("Reset");
#endif
        }

        /// <summary>
        /// True while a card or scanner is presented or an open is pending, including while a browser is open over it. A standalone
        /// browser counts on iOS but not on Android. In the Editor it reflects the test window.
        /// </summary>
        public bool IsCurrentlyPresented
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                try { InitializeAndroidPlugin(); return androidPluginInstance != null && androidPluginInstance.Call<bool>("isCurrentlyPresented"); }
                catch (Exception e) { HandleNativeException("IsCurrentlyPresented", e); return false; }
#elif UNITY_IOS && !UNITY_EDITOR
                try { return _StashNativeBridgeIsCurrentlyPresented(); }
                catch (Exception e) { HandleNativeException("IsCurrentlyPresented", e); return false; }
#elif UNITY_EDITOR
                return EditorIsPresented();
#else
                return false;
#endif
            }
        }

        /// <summary>True when a purchase is in progress and the card cannot be dismissed by the user. Android: not pollable while the card is up, because Unity is paused.</summary>
        public bool IsPurchaseProcessing
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                try { InitializeAndroidPlugin(); return androidPluginInstance != null && androidPluginInstance.Call<bool>("isPurchaseProcessing"); }
                catch (Exception e) { HandleNativeException("IsPurchaseProcessing", e); return false; }
#elif UNITY_IOS && !UNITY_EDITOR
                try { return _StashNativeBridgeIsPurchaseProcessing(); }
                catch (Exception e) { HandleNativeException("IsPurchaseProcessing", e); return false; }
#else
                return false;
#endif
            }
        }

        /// <summary>A card or scanner session is active from the wrapper's point of view, or native reports one presented.</summary>
        private bool IsSessionActive => _cardSessionActive || _scannerSessionActive || IsCurrentlyPresented;

        #endregion

        #region Internal

        private void HandleNativeException(string operation, Exception exception)
        {
            Debug.LogError("[StashNative] Exception in " + operation + ": " + exception.Message);
            Debug.LogException(exception);
            try { OnNativeException?.Invoke(operation, exception); } catch (Exception) { }
        }

#if UNITY_EDITOR
        // Called by the Editor test window (Stash.Unity.Editor) to simulate native callbacks.
        public void OnEditorPaymentSuccess(string order = null) => HandlePaymentSuccess(order);
        public void OnEditorPaymentFailure() => HandlePaymentFailure();
        public void OnEditorCardDismissed() => HandleCardDismissed();
        public void OnEditorQrCodeScanned(string content) => HandleQrCodeScanned(content);
        public void OnEditorScannerDismissed() => HandleScannerDismissed();

        private static Type EditorWindowType()
        {
            return Type.GetType("Stash.Editor.StashEditorPluginWindow, Stash.Unity.Editor") ?? Type.GetType("Stash.Editor.StashEditorPluginWindow");
        }

        private bool OpenEditorTestWindow(string url, StashNativeCardConfig config)
        {
            try
            {
                var editorWindowType = EditorWindowType();
                var openCard = editorWindowType?.GetMethod("OpenCard", new[] { typeof(string), typeof(bool), typeof(bool) });
                if (openCard == null) { Debug.LogWarning("StashNative: Editor test window unavailable on this platform; opening the system browser."); return FallbackToSystemBrowser(url); }
                openCard.Invoke(null, new object[] { url, config.autoClose, config.allowDismiss });
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("StashNative: Could not open Editor test window: " + e.Message);
                return FallbackToSystemBrowser(url);
            }
        }

        private bool OpenEditorScannerWindow()
        {
            try
            {
                var openScanner = EditorWindowType()?.GetMethod("OpenScanner", Type.EmptyTypes);
                if (openScanner == null)
                {
                    Debug.LogWarning("StashNative: Editor scanner simulation unavailable on this platform.");
                    StartCoroutine(ScannerUnavailableNextFrame(_generation));
                    return true;
                }
                openScanner.Invoke(null, null);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("StashNative: Could not open Editor scanner window: " + e.Message);
                ClearScannerCallbacks();
                return false;
            }
        }

        private static void EditorCall(string method)
        {
            try { EditorWindowType()?.GetMethod(method, Type.EmptyTypes)?.Invoke(null, null); }
            catch (Exception e) { Debug.LogWarning("StashNative: Editor " + method + " failed: " + e.Message); }
        }

        private static bool EditorIsPresented()
        {
            try { return (bool)(EditorWindowType()?.GetMethod("IsPresented", Type.EmptyTypes)?.Invoke(null, null) ?? false); }
            catch (Exception) { return false; }
        }
#endif

        #endregion
    }
}
