#if UNITY_EDITOR_OSX || UNITY_EDITOR_WIN
using System;
using UnityEngine;
using UnityEditor;
using Stash.Native;
#if UNITY_EDITOR_OSX
using Stash.Editor.macOS;
#elif UNITY_EDITOR_WIN
using Stash.Editor.Windows;
#endif

namespace Stash.Editor
{
    /// <summary>
    /// Editor window for testing the StashNative checkout card and QR scanner in the Unity Editor (macOS and Windows).
    /// The card opens in a native WebView window; the scanner is simulated with a text field.
    /// Mirrors the device lifecycle: payment with autoClose closes the card before the callback and emits no dismiss;
    /// with autoClose off the window stays open until dismissed; Dismiss / ResetPresentationState / IsCurrentlyPresented
    /// from the runtime API are routed here.
    /// </summary>
    public class StashEditorPluginWindow : EditorWindow
    {
        private enum Mode { Card, Scanner }

        private string currentUrl = "";
        private Mode mode = Mode.Card;
        private bool autoClose = true;
        private bool allowDismiss = true;
        private bool sessionActive;
        private string simulatedQrPayload = "";
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

        private static StashEditorPluginWindow instance;

        [MenuItem("Window/Stash/Stash Native - Test Window")]
        public static void ShowWindow()
        {
            instance = GetWindow<StashEditorPluginWindow>("Stash Native - Test Window");
            instance.minSize = new Vector2(320, 320);
            instance.maxSize = new Vector2(320, 320);
        }

        // ---- entry points used by StashNative (via reflection) --------------------------------

        /// <summary>Called by StashNative.OpenCard in Play mode.</summary>
        public static void OpenCard(string url, bool autoClose, bool allowDismiss)
        {
            if (instance == null) ShowWindow();
            instance.EndCurrentSession(closeIfIdle: false); // switching flows ends the previous one like a user dismissal
            instance.mode = Mode.Card;
            instance.autoClose = autoClose;
            instance.allowDismiss = allowDismiss;
            instance.currentUrl = url;
            instance.sessionActive = true;
            instance.LoadUrl(url);
            instance.Repaint();
        }

        /// <summary>Called by StashNative.OpenScanner in Play mode. No camera; type a payload and press Scan.</summary>
        public static void OpenScanner()
        {
            if (instance == null) ShowWindow();
            instance.EndCurrentSession(closeIfIdle: false);
            instance.DisposeWebView();
            instance.mode = Mode.Scanner;
            instance.currentUrl = "";
            instance.sessionActive = true;
            instance.Repaint();
        }

        /// <summary>StashNative.Dismiss(): ends the current card or scanner with its dismissal callback.</summary>
        public static void Dismiss()
        {
            if (instance == null) return;
            instance.EndCurrentSession();
            instance.Repaint();
        }

        /// <summary>StashNative.ResetPresentationState(): tears the simulation down without callbacks.</summary>
        public static void Reset()
        {
            if (instance == null) return;
            instance.sessionActive = false;
            instance.DisposeWebView();
            instance.Repaint();
        }

        /// <summary>StashNative.IsCurrentlyPresented in the Editor.</summary>
        public static bool IsPresented() => instance != null && instance.sessionActive;

        // ---- GUI --------------------------------------------------------------------------------

        private void OnGUI()
        {
            EditorGUILayout.Space(5);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play mode and call StashNative.Instance.OpenCard / OpenScanner to simulate the native flows here.", MessageType.Info);
                return;
            }

            if (mode == Mode.Scanner)
            {
                DrawScannerGUI();
                return;
            }

            EditorGUILayout.HelpBox("You can test Stash Pay purchases in the preview window. Callbacks will be triggered as if you were running on a device. You can also simulate callback events using the buttons below.", MessageType.Info);
            EditorGUILayout.Space(5);

            EditorGUILayout.LabelField($"Mode: Card   autoClose: {autoClose}   allowDismiss: {allowDismiss}   presented: {sessionActive}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("URL:", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(currentUrl, EditorStyles.textField, GUILayout.Height(18));

            EditorGUILayout.Space(5);

            EditorGUILayout.LabelField("Preview Size:", EditorStyles.boldLabel);
            int newDeviceSize = EditorGUILayout.Popup((int)currentDeviceSize, DeviceSizeNames);
            if (newDeviceSize != (int)currentDeviceSize)
                currentDeviceSize = (DeviceSize)newDeviceSize;

            if (currentDeviceSize == DeviceSize.Custom)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Width:", GUILayout.Width(50));
                customSize.x = EditorGUILayout.FloatField(customSize.x);
                EditorGUILayout.LabelField("Height:", GUILayout.Width(50));
                customSize.y = EditorGUILayout.FloatField(customSize.y);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(5);

            using (new EditorGUI.DisabledScope(!sessionActive))
            {
                if (GUILayout.Button("Reload", GUILayout.Height(25)) && !string.IsNullOrEmpty(currentUrl))
                    LoadUrl(currentUrl);

                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Simulate Callbacks:", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Payment Success", GUILayout.Height(22))) OnWebViewPaymentSuccess();
                if (GUILayout.Button("Payment Failure", GUILayout.Height(22))) OnWebViewPaymentFailure();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                using (new EditorGUI.DisabledScope(!allowDismiss))
                {
                    if (GUILayout.Button(allowDismiss ? "Dismiss Card (user)" : "Dismiss Card (allowDismiss = false)", GUILayout.Height(22)))
                        EndCurrentSession();
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawScannerGUI()
        {
            EditorGUILayout.HelpBox("QR scanner simulation. There is no camera in the Editor: paste the QR payload below and press Scan to fire OnQrCodeScanned, or Dismiss to fire OnScannerDismissed.", MessageType.Info);
            EditorGUILayout.Space(5);

            EditorGUILayout.LabelField($"Mode: Scanner   presented: {sessionActive}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("QR payload:", EditorStyles.boldLabel);
            simulatedQrPayload = EditorGUILayout.TextField(simulatedQrPayload);

            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(!sessionActive || string.IsNullOrEmpty(simulatedQrPayload)))
            {
                if (GUILayout.Button("Scan", GUILayout.Height(22)))
                {
                    var payload = simulatedQrPayload;
                    // Native closes the scanner card before delivering the scan.
                    sessionActive = false;
                    Debug.Log("[StashNative Editor] QR scanned: " + payload);
                    StashNative.InstanceOrNull?.OnEditorQrCodeScanned(payload);
                    CloseWindowIfIdle();
                    return;
                }
            }
            using (new EditorGUI.DisabledScope(!sessionActive))
            {
                if (GUILayout.Button("Dismiss Scanner", GUILayout.Height(22)))
                {
                    EndCurrentSession();
                    return;
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        // ---- lifecycle ----------------------------------------------------------------------------

        /// <summary>Ends the active session the way a user dismissal would: state is cleared first, then the callback runs, so a
        /// callback that opens the next card is not closed by this one.</summary>
        private void EndCurrentSession(bool closeIfIdle = true)
        {
            if (!sessionActive) return;
            sessionActive = false;
            var endedMode = mode;
            DisposeWebView();
            if (endedMode == Mode.Scanner)
            {
                Debug.Log("[StashNative Editor] Scanner dismissed");
                StashNative.InstanceOrNull?.OnEditorScannerDismissed();
            }
            else
            {
                Debug.Log("[StashNative Editor] Card dismissed");
                StashNative.InstanceOrNull?.OnEditorCardDismissed();
            }
            if (closeIfIdle) CloseWindowIfIdle();
        }

        private void OnWebViewPaymentSuccess()
        {
            Debug.Log("[StashNative Editor] Payment Success callback received");
            if (autoClose)
            {
                // Device order: card closes, then the callback; no dismiss follows.
                sessionActive = false;
                DisposeWebView();
            }
            StashNative.InstanceOrNull?.OnEditorPaymentSuccess("");
            CloseWindowIfIdle();
        }

        private void OnWebViewPaymentFailure()
        {
            Debug.Log("[StashNative Editor] Payment Failure callback received");
            if (autoClose)
            {
                sessionActive = false;
                DisposeWebView();
            }
            StashNative.InstanceOrNull?.OnEditorPaymentFailure();
            CloseWindowIfIdle();
        }

        /// <summary>Closes the window only if the callback that just ran did not start a new session.</summary>
        private void CloseWindowIfIdle()
        {
            if (sessionActive) { Repaint(); return; }
            Close();
        }

        private void LoadUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
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
            // Opt-in responses were removed in Stash Native 3.0 and are no longer forwarded.
            webViewLauncher.OnPaymentSuccess += OnWebViewPaymentSuccess;
            webViewLauncher.OnPaymentFailure += OnWebViewPaymentFailure;

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

        private void DisposeWebView()
        {
            if (webViewLauncher != null)
            {
                webViewLauncher.OnPaymentSuccess -= OnWebViewPaymentSuccess;
                webViewLauncher.OnPaymentFailure -= OnWebViewPaymentFailure;
                webViewLauncher.Dispose();
                webViewLauncher = null;
            }
        }

        private void OnDestroy()
        {
            // Closing the window by hand counts as the user dismissing the card or scanner. Never creates the runtime
            // singleton (InstanceOrNull), so closing the window outside Play mode leaves the scene untouched.
            if (sessionActive && Application.isPlaying)
                EndCurrentSession();
            sessionActive = false;
            DisposeWebView();
            if (instance == this) instance = null;
        }

        private void OnDisable()
        {
            DisposeWebView();
        }
    }
}
#endif // UNITY_EDITOR_OSX || UNITY_EDITOR_WIN
