#if UNITY_EDITOR_OSX || UNITY_EDITOR_WIN
using System;
using UnityEngine;
using UnityEditor;
using StashPopup;
#if UNITY_EDITOR_OSX
using StashPopup.Editor.macOS;
#elif UNITY_EDITOR_WIN
using StashPopup.Editor.Windows;
#endif

namespace StashPopup.Editor
{
    /// <summary>
    /// Editor window for testing StashPayCard popup and checkout dialogs in Unity Editor (macOS and Windows)
    /// </summary>
    public class StashPayCardEditorWindow : EditorWindow
    {
        private string currentUrl = "";
        private bool isPopupMode = false;
        private WebViewLauncher webViewLauncher;
        
        // Device size presets (width x height in points)
        private enum DeviceSize
        {
            iPhoneSE,           // 375 x 667
            iPhone14,            // 390 x 844
            iPhone14ProMax,     // 430 x 932
            iPhone14Pro,        // 393 x 852
            iPad,               // 810 x 1080
            iPadPro,            // 1024 x 1366
            Custom
        }
        
        private DeviceSize currentDeviceSize = DeviceSize.iPhone14;
        private Vector2 customSize = new Vector2(390, 844);
        
        // Device size definitions
        private static readonly Vector2[] DeviceSizes = new Vector2[]
        {
            new Vector2(375, 667),   // iPhone SE
            new Vector2(390, 844),   // iPhone 14
            new Vector2(430, 932),   // iPhone 14 Pro Max
            new Vector2(393, 852),   // iPhone 14 Pro
            new Vector2(810, 1080),   // iPad
            new Vector2(1024, 1366), // iPad Pro
        };
        
        private static readonly string[] DeviceSizeNames = new string[]
        {
            "iPhone SE (375x667)",
            "iPhone 14 (390x844)",
            "iPhone 14 Pro Max (430x932)",
            "iPhone 14 Pro (393x852)",
            "iPad (810x1080)",
            "iPad Pro (1024x1366)",
            "Custom"
        };
        
        private static StashPayCardEditorWindow instance;
        
        [MenuItem("Window/Stash/StashPayCard Test Window")]
        public static void ShowWindow()
        {
            instance = GetWindow<StashPayCardEditorWindow>("StashPayCard Test");
            instance.minSize = new Vector2(320, 300);
            instance.maxSize = new Vector2(320, 300);
        }
        
        public static void OpenPopup(string url, PopupSizeConfig? customSize = null)
        {
            if (instance == null)
            {
                ShowWindow();
            }
            
            instance.currentUrl = url;
            instance.isPopupMode = true;
            
            // Apply custom size if provided
            if (customSize.HasValue)
            {
                var size = customSize.Value;
                // Use portrait size for initial display
                instance.customSize = new Vector2(
                    size.portraitWidthMultiplier * 390f, // Assume base width of 390 (iPhone 14)
                    size.portraitHeightMultiplier * 844f // Assume base height of 844 (iPhone 14)
                );
                instance.currentDeviceSize = DeviceSize.Custom;
            }
            
            instance.LoadUrl(url);
            instance.ApplyDeviceSize();
        }
        
        public static void OpenCheckout(string url)
        {
            if (instance == null)
            {
                ShowWindow();
            }
            
            instance.currentUrl = url;
            instance.isPopupMode = false;
            instance.LoadUrl(url);
            instance.ApplyDeviceSize();
        }
        
        private void OnGUI()
        {
            EditorGUILayout.Space(5);
            
            // Mode indicator
            EditorGUILayout.LabelField($"Mode: {(isPopupMode ? "Popup" : "Checkout")}", EditorStyles.boldLabel);
            
            // URL display
            EditorGUILayout.LabelField("URL:", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(currentUrl, EditorStyles.textField, GUILayout.Height(18));
            
            EditorGUILayout.Space(5);
            
            // Device size selector
            EditorGUILayout.LabelField("Device Size:", EditorStyles.boldLabel);
            int newDeviceSize = EditorGUILayout.Popup((int)currentDeviceSize, DeviceSizeNames);
            if (newDeviceSize != (int)currentDeviceSize)
            {
                currentDeviceSize = (DeviceSize)newDeviceSize;
                ApplyDeviceSize();
            }
            
            // Custom size input
            if (currentDeviceSize == DeviceSize.Custom)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Width:", GUILayout.Width(50));
                customSize.x = EditorGUILayout.FloatField(customSize.x);
                EditorGUILayout.LabelField("Height:", GUILayout.Width(50));
                customSize.y = EditorGUILayout.FloatField(customSize.y);
                if (GUILayout.Button("Apply", GUILayout.Width(50)))
                {
                    ApplyDeviceSize();
                }
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.Space(5);
            
            // Action buttons
            if (GUILayout.Button("Reload", GUILayout.Height(25)))
            {
                if (!string.IsNullOrEmpty(currentUrl))
                {
                    LoadUrl(currentUrl);
                }
            }
            
            EditorGUILayout.Space(5);
            
            // Simulate callback buttons for testing
            EditorGUILayout.LabelField("Simulate Callbacks:", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Payment Success", GUILayout.Height(22)))
            {
                OnWebViewPaymentSuccess();
            }
            
            if (GUILayout.Button("Payment Failure", GUILayout.Height(22)))
            {
                OnWebViewPaymentFailure();
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Opt-in Response", GUILayout.Height(22)))
            {
                OnWebViewOptinResponse("test_payment_method");
            }
            
            if (GUILayout.Button("Dismiss Catalog", GUILayout.Height(22)))
            {
                OnWebViewDismissCatalog();
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        private void ApplyDeviceSize()
        {
            // Window size is fixed to fit content
            this.minSize = new Vector2(320, 300);
            this.maxSize = new Vector2(320, 300);
        }
        
        private void LoadUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return;
            
            Repaint();
            
            // Create native webview window immediately
            CreateWebViewWindow();
            
            Repaint();
        }
        
        private void CreateWebViewWindow()
        {
            if (string.IsNullOrEmpty(currentUrl)) return;
            
            DisposeWebView();
            
            Vector2 deviceSize = currentDeviceSize == DeviceSize.Custom ? customSize : DeviceSizes[(int)currentDeviceSize];
            Rect editorWindowRect = this.position;
            Rect webviewRect = new Rect(
                editorWindowRect.x + editorWindowRect.width + 10,
                editorWindowRect.y + 100,
                deviceSize.x,
                deviceSize.y
            );
            
            webViewLauncher = new WebViewLauncher();
            
            // Subscribe to callbacks
            webViewLauncher.OnPaymentSuccess += OnWebViewPaymentSuccess;
            webViewLauncher.OnPaymentFailure += OnWebViewPaymentFailure;
            webViewLauncher.OnOptinResponse += OnWebViewOptinResponse;
            
            // Set up polling for notifications (macOS uses NSNotificationCenter, Windows uses message queue)
            StartNotificationPolling();
            
            if (webViewLauncher.CreateWindow(webviewRect, currentUrl))
            {
                Debug.Log("WebViewLauncher: Created webview window successfully");
            }
            else
            {
                Debug.LogWarning("WebViewLauncher: Failed to create window, falling back to browser");
                Application.OpenURL(currentUrl);
                webViewLauncher.Dispose();
                webViewLauncher = null;
            }
        }
        
        private void StartNotificationPolling()
        {
            // Poll for notifications using EditorApplication.update
            UnityEditor.EditorApplication.update += PollForNotifications;
        }
        
        private void StopNotificationPolling()
        {
            UnityEditor.EditorApplication.update -= PollForNotifications;
        }
        
        private void PollForNotifications()
        {
            // Polling is handled by the WebViewLauncher class
            // macOS uses NSNotificationCenter, Windows uses message queue
        }
        
        private void OnWebViewPaymentSuccess()
        {
            Debug.Log("[StashPayCard Editor] Payment Success callback received");
            if (StashPayCard.Instance != null)
            {
                StashPayCard.Instance.OnEditorPaymentSuccess();
            }
            CloseEditorWindow();
        }
        
        private void OnWebViewPaymentFailure()
        {
            Debug.Log("[StashPayCard Editor] Payment Failure callback received");
            if (StashPayCard.Instance != null)
            {
                StashPayCard.Instance.OnEditorPaymentFailure();
            }
            CloseEditorWindow();
        }
        
        private void OnWebViewOptinResponse(string optinType)
        {
            Debug.Log($"[StashPayCard Editor] Optin Response callback received: {optinType}");
            if (StashPayCard.Instance != null)
            {
                StashPayCard.Instance.OnEditorOptinResponse(optinType);
            }
            CloseEditorWindow();
        }
        
        private void OnWebViewDismissCatalog()
        {
            Debug.Log("[StashPayCard Editor] Dismiss Catalog callback received");
            if (StashPayCard.Instance != null)
            {
                StashPayCard.Instance.OnEditorDismissCatalog();
            }
            CloseEditorWindow();
        }
        
        private void CloseEditorWindow()
        {
            DisposeWebView();
            this.Close();
        }
        
        private void DisposeWebView()
        {
            if (webViewLauncher != null)
            {
                // Unsubscribe from callbacks
                webViewLauncher.OnPaymentSuccess -= OnWebViewPaymentSuccess;
                webViewLauncher.OnPaymentFailure -= OnWebViewPaymentFailure;
                webViewLauncher.OnOptinResponse -= OnWebViewOptinResponse;
                
                webViewLauncher.Dispose();
                webViewLauncher = null;
            }
        }
        
        private void OnDestroy()
        {
            // Fire dismiss callback when window is closed (X button or programmatically)
            if (StashPayCard.Instance != null)
            {
                StashPayCard.Instance.OnEditorDismissCatalog();
            }
            DisposeWebView();
        }
        
        private void OnDisable()
        {
            DisposeWebView();
        }
    }
}
#endif // UNITY_EDITOR_OSX || UNITY_EDITOR_WIN

