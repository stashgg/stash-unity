#if UNITY_EDITOR_OSX || UNITY_EDITOR_WIN
using System;
using UnityEngine;
using UnityEditor;
using Stash.Native;
using Stash.Native.Desktop;

namespace Stash.Editor
{
    /// <summary>
    /// Editor simulator for StashNative card and modal flows (macOS and Windows). Presents the checkout through the
    /// stash-native desktop host in window mode, sized to a device preset, and routes its events into StashNative
    /// so per-call callbacks and global events fire as on a device. Simulate buttons fire the callbacks directly.
    /// </summary>
    public class StashEditorPluginWindow : EditorWindow
    {
        private string currentUrl = "";
        private bool isPopupMode = false;
        private StashNativeCardConfig? currentCardConfig;
        private StashNativeModalConfig? currentModalConfig;

        // Device size presets (width x height in points)
        private enum DeviceSize
        {
            iPhoneSE,           // 375 x 667
            iPhone14,            // 390 x 844
            iPhone14ProMax,     // 430 x 932
            iPhone14Pro,        // 393 x 852
            iPad,               // 810 x 1080
            iPadPro,            // 1024 x 1366
            Desktop,            // 480 x 720, the desktop card
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
            new Vector2(810, 1080),  // iPad
            new Vector2(1024, 1366), // iPad Pro
            new Vector2(480, 720),   // Desktop card
        };

        private static readonly string[] DeviceSizeNames = new string[]
        {
            "iPhone SE (375x667)",
            "iPhone 14 (390x844)",
            "iPhone 14 Pro Max (430x932)",
            "iPhone 14 Pro (393x852)",
            "iPad (810x1080)",
            "iPad Pro (1024x1366)",
            "Desktop card (480x720)",
            "Custom"
        };

        private static StashEditorPluginWindow instance;
        private bool subscribed;

        [MenuItem("Window/Stash/Stash Native - Test Window")]
        public static void ShowWindow()
        {
            instance = GetWindow<StashEditorPluginWindow>("Stash Native - Test Window");
            instance.minSize = new Vector2(320, 320);
            instance.maxSize = new Vector2(320, 320);
        }

        /// <summary>Called by StashNative.OpenModal in play mode (reflection contract, keep the signature).</summary>
        public static void OpenModal(string url, StashNativeModalConfig? config = null)
        {
            if (instance == null) ShowWindow();
            instance.currentUrl = url;
            instance.isPopupMode = true;
            instance.currentModalConfig = config;
            instance.currentCardConfig = null;
            instance.Present();
        }

        /// <summary>Called by StashNative.OpenCard in play mode (reflection contract, keep the signature).</summary>
        public static void OpenCard(string url, StashNativeCardConfig? config = null)
        {
            if (instance == null) ShowWindow();
            instance.currentUrl = url;
            instance.isPopupMode = false;
            instance.currentCardConfig = config;
            instance.currentModalConfig = null;
            instance.Present();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox("You can test Stash Pay purchases in the preview window. Callbacks will be triggered as if you were running on a device. You can also simulate callback events using the buttons below.", MessageType.Info);
            EditorGUILayout.Space(5);

            EditorGUILayout.LabelField($"Mode: {(isPopupMode ? "Modal" : "Card")}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("URL:", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(currentUrl, EditorStyles.textField, GUILayout.Height(18));
            EditorGUILayout.Space(5);

            EditorGUILayout.LabelField("Preview Size:", EditorStyles.boldLabel);
            int newDeviceSize = EditorGUILayout.Popup((int)currentDeviceSize, DeviceSizeNames);
            if (newDeviceSize != (int)currentDeviceSize)
            {
                currentDeviceSize = (DeviceSize)newDeviceSize;
                Present();
            }

            if (currentDeviceSize == DeviceSize.Custom)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Width:", GUILayout.Width(50));
                customSize.x = EditorGUILayout.FloatField(customSize.x);
                EditorGUILayout.LabelField("Height:", GUILayout.Width(50));
                customSize.y = EditorGUILayout.FloatField(customSize.y);
                if (GUILayout.Button("Apply", GUILayout.Width(50)))
                {
                    Present();
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(5);
            if (GUILayout.Button("Reload", GUILayout.Height(25)))
            {
                Present();
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Simulate Callbacks:", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Payment Success", GUILayout.Height(22)))
            {
                SimulateSuccess();
            }
            if (GUILayout.Button("Payment Failure", GUILayout.Height(22)))
            {
                SimulateFailure();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Dismiss Dialog", GUILayout.Height(22)))
            {
                SimulateDismiss();
            }
            EditorGUILayout.EndHorizontal();
        }

        private Vector2 PresetSize()
        {
            return currentDeviceSize == DeviceSize.Custom ? customSize : DeviceSizes[(int)currentDeviceSize];
        }

        /// <summary>Opens (or re-opens) the checkout in the desktop host's window presentation at the preset size.</summary>
        private void Present()
        {
            if (string.IsNullOrEmpty(currentUrl)) return;
            Repaint();
            Subscribe();
            try
            {
                if (StashNativeDesktopBridge.IsCurrentlyPresented)
                    StashNativeDesktopBridge.ResetPresentationState();
                Vector2 size = PresetSize();
                string json = isPopupMode
                    ? JsonUtility.ToJson(StashDesktopModalConfigDto.From(currentModalConfig ?? StashNativeModalConfig.Default, StashNativeDesktopBridge.PresentationWindow, size, false))
                    : JsonUtility.ToJson(StashDesktopCardConfigDto.From(currentCardConfig ?? StashNativeCardConfig.Default, StashNativeDesktopBridge.PresentationWindow, size, false));
                if (isPopupMode)
                    StashNativeDesktopBridge.OpenModal(currentUrl, json);
                else
                    StashNativeDesktopBridge.OpenCard(currentUrl, json);
            }
            catch (Exception e)
            {
                Debug.LogWarning("StashNative Editor: desktop host unavailable, falling back to the system browser: " + e.Message);
                Application.OpenURL(currentUrl);
            }
        }

        // The play-mode StashNative instance drains the native events and raises its events; the panel only
        // follows the presentation state.
        private void Subscribe()
        {
            if (subscribed || StashNative.Instance == null) return;
            subscribed = true;
            StashNative.Instance.OnDialogDismissed += OnPresentationEnded;
            StashNative.Instance.OnPaymentSuccess += _ => OnPresentationEnded();
            StashNative.Instance.OnPaymentFailure += OnPresentationEnded;
            StashNative.Instance.OnNetworkError += OnPresentationEnded;
            StashNative.Instance.OnExternalPayment += _ => OnPresentationEnded();
        }

        private void OnPresentationEnded()
        {
            EditorApplication.delayCall += () =>
            {
                if (this != null && !StashNativeDesktopBridge.IsCurrentlyPresented)
                    Close();
            };
        }

        private void SimulateSuccess()
        {
            Debug.Log("[StashNative Editor] Payment Success callback simulated");
            StashNativeDesktopBridge.ResetPresentationState();
            if (StashNative.Instance != null)
                StashNative.Instance.OnEditorPaymentSuccess("");
            Close();
        }

        private void SimulateFailure()
        {
            Debug.Log("[StashNative Editor] Payment Failure callback simulated");
            StashNativeDesktopBridge.ResetPresentationState();
            if (StashNative.Instance != null)
                StashNative.Instance.OnEditorPaymentFailure();
            Close();
        }

        private void SimulateDismiss()
        {
            Debug.Log("[StashNative Editor] Dismiss Dialog callback simulated");
            if (StashNativeDesktopBridge.IsCurrentlyPresented)
            {
                // The host emits dialogDismissed; StashNative routes it like a device dismissal.
                StashNativeDesktopBridge.Dismiss();
            }
            else if (StashNative.Instance != null)
            {
                StashNative.Instance.OnEditorDismissCatalog();
            }
            Close();
        }

        private void OnDestroy()
        {
            // Closing the panel closes the checkout; a live presentation reports dialogDismissed through the host.
            if (StashNativeDesktopBridge.IsCurrentlyPresented)
                StashNativeDesktopBridge.Dismiss();
            if (instance == this) instance = null;
        }
    }
}
#endif // UNITY_EDITOR_OSX || UNITY_EDITOR_WIN
