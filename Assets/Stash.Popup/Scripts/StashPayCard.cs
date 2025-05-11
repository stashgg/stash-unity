using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

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
        #endregion
        
        #region Private Fields
        // iOS-specific fields only
        #endregion
        
        #region Native Plugin Interface
        
        #if UNITY_IOS && !UNITY_EDITOR
        // Import the native iOS plugin functions
        [DllImport("__Internal")]
        private static extern void _StashPayCardOpenURLInSafariVC(string url);
        
        [DllImport("__Internal")]
        private static extern void _StashPayCardSetSafariViewDismissedCallback(Action callback);
        #endif
        
        #endregion
        
        #region Public Methods
        
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
            
            #if UNITY_IOS && !UNITY_EDITOR
            // Register the callback if provided
            if (callback != null)
            {
                // Clear any existing callback first
                OnSafariViewDismissed = null;
                // Then register the new callback
                OnSafariViewDismissed = callback;
            }
            
            // Register the native callback
            _StashPayCardSetSafariViewDismissedCallback(OnViewDismissed);
            
            // Open the URL in Safari View Controller
            _StashPayCardOpenURLInSafariVC(url);
            #else
            // For Android and other platforms, just open in default browser without callbacks
            Application.OpenURL(url);
            #endif
        }
        
        #endregion
        
        #region Private Methods
        
        // Called from the native iOS plugin when the Safari View is dismissed
        [AOT.MonoPInvokeCallback(typeof(Action))]
        private static void OnViewDismissed()
        {
            Debug.Log("StashPayCard: Safari View was dismissed");
            if (Instance != null && Instance.OnSafariViewDismissed != null)
            {
                // Execute on the main thread since this is called from a native thread
                Instance.ExecuteOnMainThread(() => Instance.OnSafariViewDismissed?.Invoke());
            }
        }
        
        private void ExecuteOnMainThread(Action action)
        {
            if (action == null) return;
            
            // If we're already on the main thread, execute immediately
            if (IsMainThread())
            {
                action();
            }
            else
            {
                // Otherwise queue it for the next frame
                StartCoroutine(ExecuteOnNextFrame(action));
            }
        }
        
        private IEnumerator ExecuteOnNextFrame(Action action)
        {
            yield return null;
            action?.Invoke();
        }
        
        private bool IsMainThread()
        {
            return System.Threading.Thread.CurrentThread.ManagedThreadId == 1;
        }
        
        #endregion
    }
} 