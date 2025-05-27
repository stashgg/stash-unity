using System;
using UnityEngine;
using System.Runtime.InteropServices;
using AOT;

namespace StashPopup
{
    /// <summary>
    /// Provides a cross-platform wrapper for opening Stash Pay URLs.
    /// On iOS, opens a specialized StashPayCard with callback support.
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
        #endregion

        #region Private Fields
        // Card configuration values with defaults. 
        private float _cardHeightRatio = 0.6f; // Default: 60% of screen height
        private float _cardVerticalPosition = 1.0f; // Default: bottom of screen
        private float _cardWidthRatio = 1.0f; // Default: 100% of screen width
        #endregion

        #region Native Plugin Interface

#if UNITY_IOS && !UNITY_EDITOR
        // Delegate types for iOS callbacks
        private delegate void SafariViewDismissedCallback();
        private delegate void PaymentSuccessCallback();
        
        // Import the native iOS plugin functions
        [DllImport("__Internal")]
        private static extern void _StashPayCardOpenURLInSafariVC(string url);
        
        [DllImport("__Internal")]
        private static extern void _StashPayCardSetSafariViewDismissedCallback(SafariViewDismissedCallback callback);
        
        [DllImport("__Internal")]
        private static extern void _StashPayCardSetPaymentSuccessCallback(PaymentSuccessCallback callback);
        
        [DllImport("__Internal")]
        private static extern void _StashPayCardSetCardConfiguration(float heightRatio, float verticalPosition);
        
        [DllImport("__Internal")]
        private static extern void _StashPayCardSetCardConfigurationWithWidth(float heightRatio, float verticalPosition, float widthRatio);
#endif

        #endregion

        #region Public Methods

        /// <summary>
        /// Applies the current card configuration settings to the native plugin.
        /// Called automatically when settings are changed.
        /// </summary>
        private void ApplyCardConfiguration()
        {
#if UNITY_IOS && !UNITY_EDITOR
            _StashPayCardSetCardConfigurationWithWidth(_cardHeightRatio, _cardVerticalPosition, _cardWidthRatio);
#endif
        }

        /// <summary>
        /// Opens a URL in the appropriate platform-specific browser view.
        /// On iOS, opens the URL in StashPayCard and supports callback.
        /// On other platforms and editor, simply opens the URL in the default browser as a fallback.
        /// </summary>
        /// <param name="url">Stash Pay URL to open</param>
        /// <param name="dismissCallback">Callback triggered when the browser view is dismissed / payment fails.</param>
        /// <param name="successCallback">Callback triggered when payment succeeds.</param>
        public void OpenURL(string url, Action dismissCallback = null, Action successCallback = null)
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

            if (dismissCallback != null)
            {
                OnSafariViewDismissed += dismissCallback;
            }

            if (successCallback != null)
            {
                OnPaymentSuccess += successCallback;
            }

#if UNITY_IOS && !UNITY_EDITOR
                      
            ApplyCardConfiguration();

            // Set the native iOS callbacks
            _StashPayCardSetSafariViewDismissedCallback(OnIOSSafariViewDismissed);
            _StashPayCardSetPaymentSuccessCallback(OnIOSPaymentSuccess);
            
            // Open the URL using the native iOS plugin
            _StashPayCardOpenURLInSafariVC(url);
#else
            // For Android and other platforms, just open in default browser without callbacks
            Application.OpenURL(url);
#endif
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
            if (Instance != null)
            {
                Instance.OnPaymentSuccess?.Invoke();
            }
        }
#endif

        #endregion
    }
}