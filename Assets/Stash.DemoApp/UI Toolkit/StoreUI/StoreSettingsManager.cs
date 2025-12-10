using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using System.Collections.Generic;
using StashPopup;
using Stash.Webshop;
using Stash.Samples;

namespace Stash.Samples
{
    /// <summary>
    /// Manages store settings including payment method, orientation, and SDK configuration.
    /// Separated from StashStoreUIController for better organization.
    /// </summary>
    public class StoreSettingsManager
    {
        private VisualElement root;
        private StashEnvironment environment;
        private string apiKey;
        private string channelSelectionUrl;
        private string paymentMethod;
        private string orientationMode;
        private bool useSafariWebView;
        private bool showMetrics;
        private PopupSizeConfig? customPopupSize;

        // UI Elements
        private VisualElement settingsPopup;
        private VisualElement stashSdkSettingsPopup;
        private DropdownField paymentMethodDropdown;
        private DropdownField orientationModeDropdown;
        private DropdownField apiEnvironmentDropdown;
        private TextField apiKeyInput;
        private TextField channelSelectionUrlInput;
        private Toggle safariWebViewToggle;
        private Toggle showMetricsToggle;
        private TextField popupPortraitWidthInput;
        private TextField popupPortraitHeightInput;
        private TextField popupLandscapeWidthInput;
        private TextField popupLandscapeHeightInput;
        private Button settingsButton;
        private Button settingsPopupCloseButton;
        private Button stashSdkSettingsCloseButton;
        private Button openTestCardButton;
        private Button nativeLogsButton;
        private Button loadNextSceneButton;
        private Label deviceIdLabel;
        private Label stashLogoLabel;

        public StoreSettingsManager(VisualElement root, StashEnvironment defaultEnvironment, string defaultApiKey, string defaultChannelUrl)
        {
            this.root = root;
            this.environment = defaultEnvironment;
            this.apiKey = defaultApiKey;
            this.channelSelectionUrl = defaultChannelUrl;
            this.paymentMethod = DemoAppConstants.DEFAULT_PAYMENT_METHOD;
            this.orientationMode = DemoAppConstants.DEFAULT_ORIENTATION_MODE;
        }

        public void Initialize()
        {
            LoadPreferences();
            SetupSettingsPopup();
            SetupStashSdkSettingsPopup();
            ApplyOrientationSetting();
        }

        public string PaymentMethod => paymentMethod;
        public string OrientationMode => orientationMode;
        public StashEnvironment Environment => environment;
        public string ApiKey => apiKey;
        public string ChannelSelectionUrl => channelSelectionUrl;
        public bool UseSafariWebView => useSafariWebView;
        public bool ShowMetrics => showMetrics;
        public PopupSizeConfig? CustomPopupSize => customPopupSize;

        private void LoadPreferences()
        {
            // Load orientation mode
            if (PlayerPrefs.HasKey(DemoAppConstants.PREF_ORIENTATION_MODE))
            {
                orientationMode = PlayerPrefs.GetString(DemoAppConstants.PREF_ORIENTATION_MODE, DemoAppConstants.DEFAULT_ORIENTATION_MODE);
            }
            else if (PlayerPrefs.HasKey("OrientationLocked"))
            {
                bool wasLocked = PlayerPrefs.GetInt("OrientationLocked", 1) == 1;
                orientationMode = wasLocked ? DemoAppConstants.ORIENTATION_PORTRAIT : DemoAppConstants.ORIENTATION_AUTO;
                PlayerPrefs.SetString(DemoAppConstants.PREF_ORIENTATION_MODE, orientationMode);
                PlayerPrefs.DeleteKey("OrientationLocked");
                PlayerPrefs.Save();
            }

            // Load payment method
            paymentMethod = PlayerPrefs.GetString(DemoAppConstants.PREF_PAYMENT_METHOD, DemoAppConstants.DEFAULT_PAYMENT_METHOD);

            // Load API key
            string savedApiKey = PlayerPrefs.GetString(DemoAppConstants.PREF_STASH_API_KEY, "");
            if (!string.IsNullOrEmpty(savedApiKey))
            {
                apiKey = savedApiKey;
            }

            // Load environment
            if (PlayerPrefs.HasKey(DemoAppConstants.PREF_STASH_ENVIRONMENT))
            {
                string savedEnvironment = PlayerPrefs.GetString(DemoAppConstants.PREF_STASH_ENVIRONMENT, "Test");
                environment = savedEnvironment == "Production" ? StashEnvironment.Production : StashEnvironment.Test;
            }

            // Load channel selection URL
            string savedChannelUrl = PlayerPrefs.GetString(DemoAppConstants.PREF_CHANNEL_SELECTION_URL, "");
            if (!string.IsNullOrEmpty(savedChannelUrl))
            {
                channelSelectionUrl = savedChannelUrl;
            }

            // Load show metrics
            showMetrics = PlayerPrefs.GetInt(DemoAppConstants.PREF_SHOW_METRICS, 0) == 1;

            // Load popup size configuration
            LoadPopupSizeConfig();
        }

        private void SetupSettingsPopup()
        {
            settingsButton = root.Q<Button>("settings-button");
            if (settingsButton != null)
            {
                settingsButton.clicked += ShowSettingsPopup;
            }

            settingsPopup = root.Q<VisualElement>("settings-popup");
            paymentMethodDropdown = root.Q<DropdownField>("payment-method-dropdown");
            orientationModeDropdown = root.Q<DropdownField>("orientation-mode-dropdown");
            deviceIdLabel = root.Q<Label>("device-id-label");
            settingsPopupCloseButton = root.Q<Button>("settings-popup-close-button");

            if (settingsPopup != null)
            {
                settingsPopup.visible = false;
            }

            if (paymentMethodDropdown != null)
            {
                paymentMethodDropdown.choices = new List<string> { "Native IAP", "Stash Pay" };
                paymentMethodDropdown.value = paymentMethod == DemoAppConstants.PAYMENT_METHOD_NATIVE_IAP ? "Native IAP" : "Stash Pay";
                paymentMethodDropdown.RegisterValueChangedCallback(OnPaymentMethodChanged);
            }

            if (orientationModeDropdown != null)
            {
                orientationModeDropdown.choices = new List<string> { "Auto", "Portrait", "Landscape" };
                orientationModeDropdown.value = orientationMode;
                orientationModeDropdown.RegisterValueChangedCallback(OnOrientationModeChanged);
            }

            if (deviceIdLabel != null)
            {
                deviceIdLabel.text = SystemInfo.deviceUniqueIdentifier;
            }

            if (settingsPopupCloseButton != null)
            {
                settingsPopupCloseButton.clicked += HideSettingsPopup;
            }
        }

        private void SetupStashSdkSettingsPopup()
        {
            stashLogoLabel = root.Q<Label>("app-title");
            if (stashLogoLabel != null)
            {
                stashLogoLabel.RegisterCallback<ClickEvent>(evt => ShowStashSdkSettingsPopup());
            }

            stashSdkSettingsPopup = root.Q<VisualElement>("stash-sdk-settings-popup");
            apiKeyInput = root.Q<TextField>("api-key-input");
            apiEnvironmentDropdown = root.Q<DropdownField>("api-environment-dropdown");
            channelSelectionUrlInput = root.Q<TextField>("channel-selection-url-input");
            safariWebViewToggle = root.Q<Toggle>("safari-webview-toggle");
            showMetricsToggle = root.Q<Toggle>("show-metrics-toggle");
            popupPortraitWidthInput = root.Q<TextField>("popup-portrait-width-input");
            popupPortraitHeightInput = root.Q<TextField>("popup-portrait-height-input");
            popupLandscapeWidthInput = root.Q<TextField>("popup-landscape-width-input");
            popupLandscapeHeightInput = root.Q<TextField>("popup-landscape-height-input");
            stashSdkSettingsCloseButton = root.Q<Button>("stash-sdk-settings-close-button");
            openTestCardButton = root.Q<Button>("open-test-card-button");
            nativeLogsButton = root.Q<Button>("native-logs-button");
            loadNextSceneButton = root.Q<Button>("load-next-scene-button");

            if (stashSdkSettingsPopup != null)
            {
                stashSdkSettingsPopup.visible = false;
            }

            if (apiKeyInput != null)
            {
                apiKeyInput.value = apiKey;
                apiKeyInput.RegisterCallback<FocusOutEvent>(evt => OnApiKeyChanged());
            }

            if (apiEnvironmentDropdown != null)
            {
                apiEnvironmentDropdown.choices = new List<string> { "Test", "Production" };
                apiEnvironmentDropdown.value = environment == StashEnvironment.Production ? "Production" : "Test";
                apiEnvironmentDropdown.RegisterValueChangedCallback(OnApiEnvironmentChanged);
            }

            if (channelSelectionUrlInput != null)
            {
                channelSelectionUrlInput.value = channelSelectionUrl;
                channelSelectionUrlInput.RegisterCallback<FocusOutEvent>(evt => OnChannelSelectionUrlChanged());
            }

            if (safariWebViewToggle != null)
            {
                safariWebViewToggle.value = useSafariWebView;
                safariWebViewToggle.RegisterValueChangedCallback(OnSafariToggleChanged);
            }

            if (showMetricsToggle != null)
            {
                showMetricsToggle.value = showMetrics;
                showMetricsToggle.RegisterValueChangedCallback(OnShowMetricsToggleChanged);
            }

            if (stashSdkSettingsCloseButton != null)
            {
                stashSdkSettingsCloseButton.clicked += HideStashSdkSettingsPopup;
            }

            if (openTestCardButton != null)
            {
                openTestCardButton.clicked += OpenTestCard;
            }

            if (nativeLogsButton != null)
            {
                nativeLogsButton.clicked += OpenNativeLogs;
            }

            if (loadNextSceneButton != null)
            {
                loadNextSceneButton.clicked += LoadNextScene;
            }

            SetupPopupSizeInputs();
        }

        private void SetupPopupSizeInputs()
        {
            if (popupPortraitWidthInput != null)
            {
                popupPortraitWidthInput.value = customPopupSize.HasValue ? customPopupSize.Value.portraitWidthMultiplier.ToString("F3") : "";
                popupPortraitWidthInput.RegisterCallback<FocusOutEvent>(evt => OnPopupSizeChanged());
            }

            if (popupPortraitHeightInput != null)
            {
                popupPortraitHeightInput.value = customPopupSize.HasValue ? customPopupSize.Value.portraitHeightMultiplier.ToString("F3") : "";
                popupPortraitHeightInput.RegisterCallback<FocusOutEvent>(evt => OnPopupSizeChanged());
            }

            if (popupLandscapeWidthInput != null)
            {
                popupLandscapeWidthInput.value = customPopupSize.HasValue ? customPopupSize.Value.landscapeWidthMultiplier.ToString("F3") : "";
                popupLandscapeWidthInput.RegisterCallback<FocusOutEvent>(evt => OnPopupSizeChanged());
            }

            if (popupLandscapeHeightInput != null)
            {
                popupLandscapeHeightInput.value = customPopupSize.HasValue ? customPopupSize.Value.landscapeHeightMultiplier.ToString("F3") : "";
                popupLandscapeHeightInput.RegisterCallback<FocusOutEvent>(evt => OnPopupSizeChanged());
            }
        }

        private void ShowSettingsPopup()
        {
            if (settingsPopup != null)
            {
                settingsPopup.style.display = DisplayStyle.Flex;
                settingsPopup.visible = true;
                settingsPopup.AddToClassList("visible");
            }
        }

        private void HideSettingsPopup()
        {
            if (settingsPopup != null)
            {
                settingsPopup.RemoveFromClassList("visible");
                settingsPopup.schedule.Execute(() =>
                {
                    if (settingsPopup != null && !settingsPopup.ClassListContains("visible"))
                    {
                        settingsPopup.visible = false;
                        settingsPopup.style.display = DisplayStyle.None;
                    }
                }).StartingIn(300);
            }
        }

        private void ShowStashSdkSettingsPopup()
        {
            if (stashSdkSettingsPopup != null)
            {
                stashSdkSettingsPopup.style.display = DisplayStyle.Flex;
                stashSdkSettingsPopup.visible = true;
                stashSdkSettingsPopup.AddToClassList("visible");
            }
        }

        private void HideStashSdkSettingsPopup()
        {
            if (stashSdkSettingsPopup != null)
            {
                stashSdkSettingsPopup.RemoveFromClassList("visible");
                stashSdkSettingsPopup.schedule.Execute(() =>
                {
                    if (stashSdkSettingsPopup != null && !stashSdkSettingsPopup.ClassListContains("visible"))
                    {
                        stashSdkSettingsPopup.visible = false;
                        stashSdkSettingsPopup.style.display = DisplayStyle.None;
                    }
                }).StartingIn(300);
            }
        }

        private void OnPaymentMethodChanged(ChangeEvent<string> evt)
        {
            paymentMethod = evt.newValue == "Native IAP" ? DemoAppConstants.PAYMENT_METHOD_NATIVE_IAP : DemoAppConstants.PAYMENT_METHOD_STASH_PAY;
            PlayerPrefs.SetString(DemoAppConstants.PREF_PAYMENT_METHOD, paymentMethod);
            PlayerPrefs.Save();
        }

        private void OnOrientationModeChanged(ChangeEvent<string> evt)
        {
            orientationMode = evt.newValue;
            PlayerPrefs.SetString(DemoAppConstants.PREF_ORIENTATION_MODE, orientationMode);
            PlayerPrefs.Save();
            ApplyOrientationSetting();
        }

        private void OnApiKeyChanged()
        {
            if (apiKeyInput == null) return;
            string newApiKey = apiKeyInput.value?.Trim() ?? "";
            if (!string.IsNullOrEmpty(newApiKey) && newApiKey != apiKey)
            {
                apiKey = newApiKey;
                PlayerPrefs.SetString(DemoAppConstants.PREF_STASH_API_KEY, apiKey);
                PlayerPrefs.Save();
            }
        }

        private void OnApiEnvironmentChanged(ChangeEvent<string> evt)
        {
            StashEnvironment newEnvironment = evt.newValue == "Production" ? StashEnvironment.Production : StashEnvironment.Test;
            if (newEnvironment != environment)
            {
                environment = newEnvironment;
                PlayerPrefs.SetString(DemoAppConstants.PREF_STASH_ENVIRONMENT, environment == StashEnvironment.Production ? "Production" : "Test");
                PlayerPrefs.Save();
            }
        }

        private void OnChannelSelectionUrlChanged()
        {
            if (channelSelectionUrlInput == null) return;
            string newUrl = channelSelectionUrlInput.value?.Trim() ?? "";
            if (!string.IsNullOrEmpty(newUrl) && newUrl != channelSelectionUrl)
            {
                channelSelectionUrl = newUrl;
                PlayerPrefs.SetString(DemoAppConstants.PREF_CHANNEL_SELECTION_URL, channelSelectionUrl);
                PlayerPrefs.Save();
            }
        }

        private void OnSafariToggleChanged(ChangeEvent<bool> evt)
        {
            useSafariWebView = evt.newValue;
        }

        private void OnShowMetricsToggleChanged(ChangeEvent<bool> evt)
        {
            showMetrics = evt.newValue;
            PlayerPrefs.SetInt(DemoAppConstants.PREF_SHOW_METRICS, showMetrics ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void OnPopupSizeChanged()
        {
            float portraitWidth = ParseFloatValue(popupPortraitWidthInput?.value ?? "");
            float portraitHeight = ParseFloatValue(popupPortraitHeightInput?.value ?? "");
            float landscapeWidth = ParseFloatValue(popupLandscapeWidthInput?.value ?? "");
            float landscapeHeight = ParseFloatValue(popupLandscapeHeightInput?.value ?? "");

            bool allValid = !float.IsNaN(portraitWidth) && !float.IsNaN(portraitHeight) &&
                           !float.IsNaN(landscapeWidth) && !float.IsNaN(landscapeHeight) &&
                           !string.IsNullOrWhiteSpace(popupPortraitWidthInput?.value) &&
                           !string.IsNullOrWhiteSpace(popupPortraitHeightInput?.value) &&
                           !string.IsNullOrWhiteSpace(popupLandscapeWidthInput?.value) &&
                           !string.IsNullOrWhiteSpace(popupLandscapeHeightInput?.value);

            if (allValid)
            {
                customPopupSize = new PopupSizeConfig
                {
                    portraitWidthMultiplier = portraitWidth,
                    portraitHeightMultiplier = portraitHeight,
                    landscapeWidthMultiplier = landscapeWidth,
                    landscapeHeightMultiplier = landscapeHeight
                };

                PlayerPrefs.SetFloat(DemoAppConstants.PREF_POPUP_PORTRAIT_WIDTH, portraitWidth);
                PlayerPrefs.SetFloat(DemoAppConstants.PREF_POPUP_PORTRAIT_HEIGHT, portraitHeight);
                PlayerPrefs.SetFloat(DemoAppConstants.PREF_POPUP_LANDSCAPE_WIDTH, landscapeWidth);
                PlayerPrefs.SetFloat(DemoAppConstants.PREF_POPUP_LANDSCAPE_HEIGHT, landscapeHeight);
                PlayerPrefs.SetInt(DemoAppConstants.PREF_USE_CUSTOM_POPUP_SIZE, 1);
                PlayerPrefs.Save();
            }
            else
            {
                customPopupSize = null;
                PlayerPrefs.SetInt(DemoAppConstants.PREF_USE_CUSTOM_POPUP_SIZE, 0);
                PlayerPrefs.Save();
            }
        }

        private float ParseFloatValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return 0f;

            value = value.Trim().TrimEnd('f', 'F');
            if (float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float result))
            {
                return result;
            }

            return float.NaN;
        }

        private void LoadPopupSizeConfig()
        {
            bool useCustomSize = PlayerPrefs.GetInt(DemoAppConstants.PREF_USE_CUSTOM_POPUP_SIZE, 0) == 1;

            if (useCustomSize)
            {
                customPopupSize = new PopupSizeConfig
                {
                    portraitWidthMultiplier = PlayerPrefs.GetFloat(DemoAppConstants.PREF_POPUP_PORTRAIT_WIDTH, 0.85f),
                    portraitHeightMultiplier = PlayerPrefs.GetFloat(DemoAppConstants.PREF_POPUP_PORTRAIT_HEIGHT, 1.125f),
                    landscapeWidthMultiplier = PlayerPrefs.GetFloat(DemoAppConstants.PREF_POPUP_LANDSCAPE_WIDTH, 1.27075f),
                    landscapeHeightMultiplier = PlayerPrefs.GetFloat(DemoAppConstants.PREF_POPUP_LANDSCAPE_HEIGHT, 0.9f)
                };
            }
            else
            {
                customPopupSize = null;
            }
        }

        private void ApplyOrientationSetting()
        {
            switch (orientationMode)
            {
                case "Portrait":
                    Screen.orientation = ScreenOrientation.Portrait;
                    Screen.autorotateToPortrait = true;
                    Screen.autorotateToPortraitUpsideDown = false;
                    Screen.autorotateToLandscapeLeft = false;
                    Screen.autorotateToLandscapeRight = false;
                    break;

                case "Landscape":
                    Screen.orientation = ScreenOrientation.LandscapeLeft;
                    Screen.autorotateToPortrait = false;
                    Screen.autorotateToPortraitUpsideDown = false;
                    Screen.autorotateToLandscapeLeft = true;
                    Screen.autorotateToLandscapeRight = true;
                    break;

                case "Auto":
                default:
                    Screen.orientation = ScreenOrientation.AutoRotation;
                    Screen.autorotateToPortrait = true;
                    Screen.autorotateToPortraitUpsideDown = true;
                    Screen.autorotateToLandscapeLeft = true;
                    Screen.autorotateToLandscapeRight = true;
                    break;
            }
        }

        public PopupSizeConfig? GetCurrentPopupSizeFromInputs()
        {
            string portraitWidthStr = popupPortraitWidthInput?.value ?? "";
            string portraitHeightStr = popupPortraitHeightInput?.value ?? "";
            string landscapeWidthStr = popupLandscapeWidthInput?.value ?? "";
            string landscapeHeightStr = popupLandscapeHeightInput?.value ?? "";

            float portraitWidth = ParseFloatValue(portraitWidthStr);
            float portraitHeight = ParseFloatValue(portraitHeightStr);
            float landscapeWidth = ParseFloatValue(landscapeWidthStr);
            float landscapeHeight = ParseFloatValue(landscapeHeightStr);

            bool allValid = !float.IsNaN(portraitWidth) && !float.IsNaN(portraitHeight) &&
                           !float.IsNaN(landscapeWidth) && !float.IsNaN(landscapeHeight) &&
                           !string.IsNullOrWhiteSpace(portraitWidthStr) &&
                           !string.IsNullOrWhiteSpace(portraitHeightStr) &&
                           !string.IsNullOrWhiteSpace(landscapeWidthStr) &&
                           !string.IsNullOrWhiteSpace(landscapeHeightStr);

            if (allValid)
            {
                return new PopupSizeConfig
                {
                    portraitWidthMultiplier = portraitWidth,
                    portraitHeightMultiplier = portraitHeight,
                    landscapeWidthMultiplier = landscapeWidth,
                    landscapeHeightMultiplier = landscapeHeight
                };
            }

            return customPopupSize;
        }

        private void OpenTestCard()
        {
            const string testUrl = "https://htmlpreview.github.io/?https://raw.githubusercontent.com/stashgg/stash-unity/refs/heads/main/.github/Stash.Popup.Test/index.html";
            
            UINotificationSystem.ShowToast("Test Card", "Opening test card...", 2f, root);
            
            StashPayCard.Instance.OpenCheckout(
                testUrl,
                dismissCallback: () => {
                    UINotificationSystem.ShowToast("Dismissed", "Card was dismissed", 2f, root);
                },
                successCallback: () => {
                    UINotificationSystem.ShowToast("Success", "Payment successful!", 3f, root);
                },
                failureCallback: () => {
                    UINotificationSystem.ShowToast("Failure", "Payment failed", 3f, root);
                }
            );
        }

        private void OpenNativeLogs()
        {
            NativeExceptionLogger logger = Object.FindObjectOfType<NativeExceptionLogger>();
            if (logger != null)
            {
                logger.ShowLogPanel();
            }
            else
            {
                UINotificationSystem.ShowToast("Error", "Native Exception Logger not found", 2f, root);
            }
        }

        private void LoadNextScene()
        {
            int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
            int totalScenes = SceneManager.sceneCountInBuildSettings;
            
            if (currentSceneIndex + 1 < totalScenes)
            {
                SceneManager.LoadScene(currentSceneIndex + 1);
            }
            else
            {
                UINotificationSystem.ShowToast("Info", "Already on the last scene", 2f, root);
            }
        }
    }
}

