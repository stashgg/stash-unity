using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;
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
        /// Event triggered when a payment succeeds (redirect_status=succeeded detected).
        /// </summary>
        public event Action OnPaymentSuccess;
        #endregion
        
        #region Private Fields
        // Card configuration values with defaults
        private float _cardHeightRatio = 0.4f; // Default: 40% of screen height
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
        /// Sets the card height as a ratio of the screen height (0.1 to 1.0).
        /// </summary>
        /// <param name="heightRatio">Height ratio (0.1 to 1.0), where 0.4 is 40% of screen height and 1.0 is full screen</param>
        public void SetCardHeightRatio(float heightRatio)
        {
            // Validate and clamp the height ratio (allow up to 1.0 for full-screen)
            _cardHeightRatio = Mathf.Clamp(heightRatio, 0.1f, 1.0f);
            
            // Apply configuration
            ApplyCardConfiguration();
        }
        
        /// <summary>
        /// Set the vertical position of the card (0.0 = top, 0.5 = middle, 1.0 = bottom).
        /// Used for custom positioning.
        /// </summary>
        /// <param name="verticalPosition">Vertical position ratio (0.0 to 1.0)</param>
        public void SetCardVerticalPosition(float verticalPosition)
        {
            _cardVerticalPosition = Mathf.Clamp(verticalPosition, 0.0f, 1.0f);
            ApplyCardConfiguration();
        }
        
        /// <summary>
        /// Set the width ratio of the card (0.1 = 10% width, 1.0 = 100% width).
        /// Used for custom sizing.
        /// </summary>
        /// <param name="widthRatio">Width ratio (0.1 to 1.0)</param>
        public void SetCardWidthRatio(float widthRatio)
        {
            _cardWidthRatio = Mathf.Clamp(widthRatio, 0.1f, 1.0f);
            ApplyCardConfiguration();
        }
        
        /// <summary>
        /// Configure the card to appear as a bottom sheet.
        /// This is a convenience method for the common bottom sheet pattern.
        /// </summary>
        /// <param name="heightRatio">Height ratio (0.1 to 1.0)</param>
        public void ConfigureAsBottomSheet(float heightRatio = 0.4f)
        {
            _cardHeightRatio = Mathf.Clamp(heightRatio, 0.1f, 1.0f);
            _cardVerticalPosition = 1.0f; // Bottom of screen
            _cardWidthRatio = 1.0f; // Full width
            
            ApplyCardConfiguration();
        }
        
        /// <summary>
        /// Configure the card to appear in the middle of the screen.
        /// This is a convenience method for the common dialog pattern.
        /// </summary>
        /// <param name="heightRatio">Height ratio (0.1 to 1.0)</param>
        public void ConfigureAsDialog(float heightRatio = 0.4f)
        {
            _cardHeightRatio = Mathf.Clamp(heightRatio, 0.1f, 1.0f);
            _cardVerticalPosition = 0.5f; // Middle of screen
            _cardWidthRatio = 0.9f; // 90% width for dialog appearance
            
            ApplyCardConfiguration();
        }
        
        /// <summary>
        /// Configure the card to appear as a fullscreen overlay.
        /// This covers the entire screen like a traditional webview and uses native Safari on iOS.
        /// </summary>
        /// <param name="heightRatio">Height ratio (1.0 for true fullscreen experience)</param>
        public void ConfigureAsFullScreen(float heightRatio = 1.0f)
        {
            // For full-screen, we want to trigger native Safari, so ensure values are high enough
            _cardHeightRatio = Mathf.Clamp(heightRatio, 0.95f, 1.0f); // Ensure it's >= 0.95 to trigger native Safari
            _cardVerticalPosition = 0.5f; // Center vertically (though not used in native Safari)
            _cardWidthRatio = 1.0f; // Full width to trigger native Safari
            
            ApplyCardConfiguration();
        }
        
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
        /// <param name="url">The URL to open</param>
        /// <param name="callback">Optional callback triggered when the browser view is dismissed (iOS only)</param>
        public void OpenURL(string url, Action callback = null)
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
            
            if (callback != null)
            {
                OnSafariViewDismissed += callback;
            }
            
            #if UNITY_IOS && !UNITY_EDITOR
            // Apply current card configuration
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