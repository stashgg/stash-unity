using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using StashPopup;
using Stash.Samples;

namespace Stash.Samples
{
    /// <summary>
    /// Manages store settings including orientation and SDK configuration (Stash Pay only).
    /// Separated from StashStoreUIController for better organization.
    /// </summary>
    public class StoreSettingsManager
    {
        private VisualElement root;
        private StashDemoEnvironment environment;
        private string apiKey;
        private string channelSelectionUrl;
        private string orientationMode;
        private bool useSafariWebView;
        private bool showMetrics;

        // Checkout config state
        private bool checkoutForcePortrait;
        private float checkoutCardHeightPortrait, checkoutCardWidthLandscape, checkoutCardHeightLandscape;
        private float checkoutTabletWPortrait, checkoutTabletHPortrait, checkoutTabletWLandscape, checkoutTabletHLandscape;

        // Modal config state
        private bool modalShowDragBar, modalAllowDismiss;
        private float modalPhoneWPortrait, modalPhoneHPortrait, modalPhoneWLandscape, modalPhoneHLandscape;
        private float modalTabletWPortrait, modalTabletHPortrait, modalTabletWLandscape, modalTabletHLandscape;

        // UI Elements (unified Settings modal)
        private VisualElement settingsPopup;
        private DropdownField orientationModeDropdown;
        private DropdownField apiEnvironmentDropdown;
        private TextField apiKeyInput;
        private TextField channelSelectionUrlInput;
        private Toggle safariWebViewToggle;
        private Toggle showMetricsToggle;
        private Button settingsButton;
        private Button settingsPopupCloseButton;
        private Button openTestCardButton;
        private Label deviceIdLabel;
        private Label stashLogoLabel;

        // Checkout UI
        private Toggle checkoutForcePortraitToggle;
        private TextField checkoutPhoneHeightPortrait, checkoutPhoneWidthLandscape, checkoutPhoneHeightLandscape;
        private TextField checkoutTabletWidthPortrait, checkoutTabletHeightPortrait, checkoutTabletWidthLandscape, checkoutTabletHeightLandscape;

        // Modal UI
        private Toggle modalShowDragBarToggle, modalAllowDismissToggle;
        private TextField modalPhoneWidthPortrait, modalPhoneHeightPortrait, modalPhoneWidthLandscape, modalPhoneHeightLandscape;
        private TextField modalTabletWidthPortrait, modalTabletHeightPortrait, modalTabletWidthLandscape, modalTabletHeightLandscape;

        // Accordion: only one foldout open at a time
        private Foldout foldoutApi, foldoutCheckout, foldoutModal;
        private bool foldoutSyncInProgress;

        public StoreSettingsManager(VisualElement root, StashDemoEnvironment defaultEnvironment, string defaultApiKey, string defaultChannelUrl)
        {
            this.root = root;
            this.environment = defaultEnvironment;
            this.apiKey = defaultApiKey;
            this.channelSelectionUrl = defaultChannelUrl;
            this.orientationMode = DemoAppConstants.DEFAULT_ORIENTATION_MODE;
        }

        public void Initialize()
        {
            LoadPreferences();
            SetupSettingsPopup();
            ApplyOrientationSetting();
        }

        public string OrientationMode => orientationMode;
        public StashDemoEnvironment Environment => environment;
        public string ApiKey => apiKey;
        public string ChannelSelectionUrl => channelSelectionUrl;
        public bool UseSafariWebView => useSafariWebView;
        public bool ShowMetrics => showMetrics;

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
                environment = savedEnvironment == "Production" ? StashDemoEnvironment.Production : StashDemoEnvironment.Test;
            }

            // Load channel selection URL
            string savedChannelUrl = PlayerPrefs.GetString(DemoAppConstants.PREF_CHANNEL_SELECTION_URL, "");
            if (!string.IsNullOrEmpty(savedChannelUrl))
            {
                channelSelectionUrl = savedChannelUrl;
            }

            // Load show metrics
            showMetrics = PlayerPrefs.GetInt(DemoAppConstants.PREF_SHOW_METRICS, 0) == 1;

            useSafariWebView = PlayerPrefs.GetInt(DemoAppConstants.PREF_USE_SAFARI_WEBVIEW, 0) == 1;

            LoadCheckoutConfig();
            LoadModalConfig();
        }

        private void SetupSettingsPopup()
        {
            settingsButton = root.Q<Button>("settings-button");
            settingsPopup = root.Q<VisualElement>("settings-popup");
            orientationModeDropdown = root.Q<DropdownField>("orientation-mode-dropdown");
            deviceIdLabel = root.Q<Label>("device-id-label");
            settingsPopupCloseButton = root.Q<Button>("settings-popup-close-button");

            if (settingsButton != null)
                settingsButton.clicked += ShowSettingsPopup;
            if (settingsPopup != null)
                settingsPopup.visible = false;
            if (orientationModeDropdown != null)
            {
                orientationModeDropdown.choices = new List<string> { "Auto", "Portrait", "Landscape" };
                orientationModeDropdown.value = orientationMode;
                orientationModeDropdown.RegisterValueChangedCallback(OnOrientationModeChanged);
            }
            if (deviceIdLabel != null)
                deviceIdLabel.text = SystemInfo.deviceUniqueIdentifier;
            if (settingsPopupCloseButton != null)
                settingsPopupCloseButton.clicked += HideSettingsPopup;

            stashLogoLabel = root.Q<Label>("app-title");
            if (stashLogoLabel != null)
                stashLogoLabel.RegisterCallback<ClickEvent>(evt => ShowSettingsPopup());
            apiKeyInput = root.Q<TextField>("api-key-input");
            apiEnvironmentDropdown = root.Q<DropdownField>("api-environment-dropdown");
            channelSelectionUrlInput = root.Q<TextField>("channel-selection-url-input");
            safariWebViewToggle = root.Q<Toggle>("safari-webview-toggle");
            showMetricsToggle = root.Q<Toggle>("show-metrics-toggle");
            openTestCardButton = root.Q<Button>("open-test-card-button");

            checkoutForcePortraitToggle = root.Q<Toggle>("checkout-force-portrait-toggle");
            checkoutPhoneHeightPortrait = root.Q<TextField>("checkout-phone-height-portrait");
            checkoutPhoneWidthLandscape = root.Q<TextField>("checkout-phone-width-landscape");
            checkoutPhoneHeightLandscape = root.Q<TextField>("checkout-phone-height-landscape");
            checkoutTabletWidthPortrait = root.Q<TextField>("checkout-tablet-width-portrait");
            checkoutTabletHeightPortrait = root.Q<TextField>("checkout-tablet-height-portrait");
            checkoutTabletWidthLandscape = root.Q<TextField>("checkout-tablet-width-landscape");
            checkoutTabletHeightLandscape = root.Q<TextField>("checkout-tablet-height-landscape");

            modalShowDragBarToggle = root.Q<Toggle>("modal-show-dragbar-toggle");
            modalAllowDismissToggle = root.Q<Toggle>("modal-allow-dismiss-toggle");
            modalPhoneWidthPortrait = root.Q<TextField>("modal-phone-width-portrait");
            modalPhoneHeightPortrait = root.Q<TextField>("modal-phone-height-portrait");
            modalPhoneWidthLandscape = root.Q<TextField>("modal-phone-width-landscape");
            modalPhoneHeightLandscape = root.Q<TextField>("modal-phone-height-landscape");
            modalTabletWidthPortrait = root.Q<TextField>("modal-tablet-width-portrait");
            modalTabletHeightPortrait = root.Q<TextField>("modal-tablet-height-portrait");
            modalTabletWidthLandscape = root.Q<TextField>("modal-tablet-width-landscape");
            modalTabletHeightLandscape = root.Q<TextField>("modal-tablet-height-landscape");

            foldoutApi = root.Q<Foldout>("foldout-api");
            foldoutCheckout = root.Q<Foldout>("foldout-checkout");
            foldoutModal = root.Q<Foldout>("foldout-modal");
            SetupFoldoutAccordion();

            if (apiKeyInput != null) { apiKeyInput.value = apiKey; apiKeyInput.RegisterCallback<FocusOutEvent>(evt => OnApiKeyChanged()); }
            if (apiEnvironmentDropdown != null)
            {
                apiEnvironmentDropdown.choices = new List<string> { "Test", "Production" };
                apiEnvironmentDropdown.value = environment == StashDemoEnvironment.Production ? "Production" : "Test";
                apiEnvironmentDropdown.RegisterValueChangedCallback(OnApiEnvironmentChanged);
            }
            if (channelSelectionUrlInput != null) { channelSelectionUrlInput.value = channelSelectionUrl; channelSelectionUrlInput.RegisterCallback<FocusOutEvent>(evt => OnChannelSelectionUrlChanged()); }
            if (safariWebViewToggle != null) { safariWebViewToggle.value = useSafariWebView; safariWebViewToggle.RegisterValueChangedCallback(OnSafariToggleChanged); }
            if (showMetricsToggle != null) { showMetricsToggle.value = showMetrics; showMetricsToggle.RegisterValueChangedCallback(OnShowMetricsToggleChanged); }
            if (openTestCardButton != null) openTestCardButton.clicked += OpenTestCard;

            SetupCheckoutInputs();
            SetupModalInputs();
        }

        private void SetupFoldoutAccordion()
        {
            if (foldoutApi != null) foldoutApi.RegisterValueChangedCallback(OnFoldoutValueChanged);
            if (foldoutCheckout != null) foldoutCheckout.RegisterValueChangedCallback(OnFoldoutValueChanged);
            if (foldoutModal != null) foldoutModal.RegisterValueChangedCallback(OnFoldoutValueChanged);
        }

        private void OnFoldoutValueChanged(ChangeEvent<bool> evt)
        {
            if (foldoutSyncInProgress || !evt.newValue) return;
            foldoutSyncInProgress = true;
            try
            {
                var source = evt.target as Foldout;
                if (source == foldoutApi && foldoutCheckout != null) foldoutCheckout.value = false;
                if (source == foldoutApi && foldoutModal != null) foldoutModal.value = false;
                if (source == foldoutCheckout && foldoutApi != null) foldoutApi.value = false;
                if (source == foldoutCheckout && foldoutModal != null) foldoutModal.value = false;
                if (source == foldoutModal && foldoutApi != null) foldoutApi.value = false;
                if (source == foldoutModal && foldoutCheckout != null) foldoutCheckout.value = false;
            }
            finally { foldoutSyncInProgress = false; }
        }

        private void SetupCheckoutInputs()
        {
            if (checkoutForcePortraitToggle != null) { checkoutForcePortraitToggle.value = checkoutForcePortrait; checkoutForcePortraitToggle.RegisterValueChangedCallback(OnCheckoutForcePortraitChanged); }
            SetCheckoutTextField(checkoutPhoneHeightPortrait, checkoutCardHeightPortrait);
            SetCheckoutTextField(checkoutPhoneWidthLandscape, checkoutCardWidthLandscape);
            SetCheckoutTextField(checkoutPhoneHeightLandscape, checkoutCardHeightLandscape);
            SetCheckoutTextField(checkoutTabletWidthPortrait, checkoutTabletWPortrait);
            SetCheckoutTextField(checkoutTabletHeightPortrait, checkoutTabletHPortrait);
            SetCheckoutTextField(checkoutTabletWidthLandscape, checkoutTabletWLandscape);
            SetCheckoutTextField(checkoutTabletHeightLandscape, checkoutTabletHLandscape);
            ApplyCheckoutConfigToStashPayCard();
        }

        private void SetCheckoutTextField(TextField field, float value)
        {
            if (field == null) return;
            field.value = value.ToString("F3");
            field.RegisterCallback<FocusOutEvent>(_ => { ReadCheckoutFromInputs(); SaveCheckoutConfig(); ApplyCheckoutConfigToStashPayCard(); });
        }

        private void SetupModalInputs()
        {
            if (modalShowDragBarToggle != null) { modalShowDragBarToggle.value = modalShowDragBar; modalShowDragBarToggle.RegisterValueChangedCallback(OnModalShowDragBarChanged); }
            if (modalAllowDismissToggle != null) { modalAllowDismissToggle.value = modalAllowDismiss; modalAllowDismissToggle.RegisterValueChangedCallback(OnModalAllowDismissChanged); }
            SetModalTextField(modalPhoneWidthPortrait, modalPhoneWPortrait);
            SetModalTextField(modalPhoneHeightPortrait, modalPhoneHPortrait);
            SetModalTextField(modalPhoneWidthLandscape, modalPhoneWLandscape);
            SetModalTextField(modalPhoneHeightLandscape, modalPhoneHLandscape);
            SetModalTextField(modalTabletWidthPortrait, modalTabletWPortrait);
            SetModalTextField(modalTabletHeightPortrait, modalTabletHPortrait);
            SetModalTextField(modalTabletWidthLandscape, modalTabletWLandscape);
            SetModalTextField(modalTabletHeightLandscape, modalTabletHLandscape);
        }

        private void SetModalTextField(TextField field, float value)
        {
            if (field == null) return;
            field.value = value.ToString("F3");
            field.RegisterCallback<FocusOutEvent>(_ => { ReadModalFromInputs(); SaveModalConfig(); });
        }

        private void OnCheckoutForcePortraitChanged(ChangeEvent<bool> evt) { checkoutForcePortrait = evt.newValue; SaveCheckoutConfig(); ApplyCheckoutConfigToStashPayCard(); }
        private void OnModalShowDragBarChanged(ChangeEvent<bool> evt) { modalShowDragBar = evt.newValue; SaveModalConfig(); }
        private void OnModalAllowDismissChanged(ChangeEvent<bool> evt) { modalAllowDismiss = evt.newValue; SaveModalConfig(); }

        private void LoadCheckoutConfig()
        {
            checkoutForcePortrait = PlayerPrefs.GetInt(DemoAppConstants.PREF_CHECKOUT_FORCE_PORTRAIT, 0) == 1;
            checkoutCardHeightPortrait = PlayerPrefs.GetFloat(DemoAppConstants.PREF_CHECKOUT_CARD_HEIGHT_PORTRAIT, 0.68f);
            checkoutCardWidthLandscape = PlayerPrefs.GetFloat(DemoAppConstants.PREF_CHECKOUT_CARD_WIDTH_LANDSCAPE, 0.9f);
            checkoutCardHeightLandscape = PlayerPrefs.GetFloat(DemoAppConstants.PREF_CHECKOUT_CARD_HEIGHT_LANDSCAPE, 0.6f);
            checkoutTabletWPortrait = PlayerPrefs.GetFloat(DemoAppConstants.PREF_CHECKOUT_TABLET_W_PORTRAIT, 0.4f);
            checkoutTabletHPortrait = PlayerPrefs.GetFloat(DemoAppConstants.PREF_CHECKOUT_TABLET_H_PORTRAIT, 0.5f);
            checkoutTabletWLandscape = PlayerPrefs.GetFloat(DemoAppConstants.PREF_CHECKOUT_TABLET_W_LANDSCAPE, 0.3f);
            checkoutTabletHLandscape = PlayerPrefs.GetFloat(DemoAppConstants.PREF_CHECKOUT_TABLET_H_LANDSCAPE, 0.6f);
        }

        private void LoadModalConfig()
        {
            modalShowDragBar = PlayerPrefs.GetInt(DemoAppConstants.PREF_MODAL_SHOW_DRAG_BAR, 1) == 1;
            modalAllowDismiss = PlayerPrefs.GetInt(DemoAppConstants.PREF_MODAL_ALLOW_DISMISS, 1) == 1;
            modalPhoneWPortrait = PlayerPrefs.GetFloat(DemoAppConstants.PREF_MODAL_PHONE_W_PORTRAIT, 0.8f);
            modalPhoneHPortrait = PlayerPrefs.GetFloat(DemoAppConstants.PREF_MODAL_PHONE_H_PORTRAIT, 0.5f);
            modalPhoneWLandscape = PlayerPrefs.GetFloat(DemoAppConstants.PREF_MODAL_PHONE_W_LANDSCAPE, 0.5f);
            modalPhoneHLandscape = PlayerPrefs.GetFloat(DemoAppConstants.PREF_MODAL_PHONE_H_LANDSCAPE, 0.8f);
            modalTabletWPortrait = PlayerPrefs.GetFloat(DemoAppConstants.PREF_MODAL_TABLET_W_PORTRAIT, 0.4f);
            modalTabletHPortrait = PlayerPrefs.GetFloat(DemoAppConstants.PREF_MODAL_TABLET_H_PORTRAIT, 0.3f);
            modalTabletWLandscape = PlayerPrefs.GetFloat(DemoAppConstants.PREF_MODAL_TABLET_W_LANDSCAPE, 0.3f);
            modalTabletHLandscape = PlayerPrefs.GetFloat(DemoAppConstants.PREF_MODAL_TABLET_H_LANDSCAPE, 0.4f);
        }

        private void ReadCheckoutFromInputs()
        {
            checkoutCardHeightPortrait = ParseFloatClamped(checkoutPhoneHeightPortrait?.value ?? "", 0.68f);
            checkoutCardWidthLandscape = ParseFloatClamped(checkoutPhoneWidthLandscape?.value ?? "", 0.9f);
            checkoutCardHeightLandscape = ParseFloatClamped(checkoutPhoneHeightLandscape?.value ?? "", 0.6f);
            checkoutTabletWPortrait = ParseFloatClamped(checkoutTabletWidthPortrait?.value ?? "", 0.4f);
            checkoutTabletHPortrait = ParseFloatClamped(checkoutTabletHeightPortrait?.value ?? "", 0.5f);
            checkoutTabletWLandscape = ParseFloatClamped(checkoutTabletWidthLandscape?.value ?? "", 0.3f);
            checkoutTabletHLandscape = ParseFloatClamped(checkoutTabletHeightLandscape?.value ?? "", 0.6f);
        }

        private void ReadModalFromInputs()
        {
            modalPhoneWPortrait = ParseFloatClamped(modalPhoneWidthPortrait?.value ?? "", 0.8f);
            modalPhoneHPortrait = ParseFloatClamped(modalPhoneHeightPortrait?.value ?? "", 0.5f);
            modalPhoneWLandscape = ParseFloatClamped(modalPhoneWidthLandscape?.value ?? "", 0.5f);
            modalPhoneHLandscape = ParseFloatClamped(modalPhoneHeightLandscape?.value ?? "", 0.8f);
            modalTabletWPortrait = ParseFloatClamped(modalTabletWidthPortrait?.value ?? "", 0.4f);
            modalTabletHPortrait = ParseFloatClamped(modalTabletHeightPortrait?.value ?? "", 0.3f);
            modalTabletWLandscape = ParseFloatClamped(modalTabletWidthLandscape?.value ?? "", 0.3f);
            modalTabletHLandscape = ParseFloatClamped(modalTabletHeightLandscape?.value ?? "", 0.4f);
        }

        private float ParseFloatClamped(string value, float defaultValue)
        {
            float v = ParseFloatValue(value);
            return float.IsNaN(v) ? defaultValue : Mathf.Clamp(v, 0.1f, 1f);
        }

        private void SaveCheckoutConfig()
        {
            PlayerPrefs.SetInt(DemoAppConstants.PREF_CHECKOUT_FORCE_PORTRAIT, checkoutForcePortrait ? 1 : 0);
            PlayerPrefs.SetFloat(DemoAppConstants.PREF_CHECKOUT_CARD_HEIGHT_PORTRAIT, checkoutCardHeightPortrait);
            PlayerPrefs.SetFloat(DemoAppConstants.PREF_CHECKOUT_CARD_WIDTH_LANDSCAPE, checkoutCardWidthLandscape);
            PlayerPrefs.SetFloat(DemoAppConstants.PREF_CHECKOUT_CARD_HEIGHT_LANDSCAPE, checkoutCardHeightLandscape);
            PlayerPrefs.SetFloat(DemoAppConstants.PREF_CHECKOUT_TABLET_W_PORTRAIT, checkoutTabletWPortrait);
            PlayerPrefs.SetFloat(DemoAppConstants.PREF_CHECKOUT_TABLET_H_PORTRAIT, checkoutTabletHPortrait);
            PlayerPrefs.SetFloat(DemoAppConstants.PREF_CHECKOUT_TABLET_W_LANDSCAPE, checkoutTabletWLandscape);
            PlayerPrefs.SetFloat(DemoAppConstants.PREF_CHECKOUT_TABLET_H_LANDSCAPE, checkoutTabletHLandscape);
            PlayerPrefs.Save();
        }

        private void SaveModalConfig()
        {
            PlayerPrefs.SetInt(DemoAppConstants.PREF_MODAL_SHOW_DRAG_BAR, modalShowDragBar ? 1 : 0);
            PlayerPrefs.SetInt(DemoAppConstants.PREF_MODAL_ALLOW_DISMISS, modalAllowDismiss ? 1 : 0);
            PlayerPrefs.SetFloat(DemoAppConstants.PREF_MODAL_PHONE_W_PORTRAIT, modalPhoneWPortrait);
            PlayerPrefs.SetFloat(DemoAppConstants.PREF_MODAL_PHONE_H_PORTRAIT, modalPhoneHPortrait);
            PlayerPrefs.SetFloat(DemoAppConstants.PREF_MODAL_PHONE_W_LANDSCAPE, modalPhoneWLandscape);
            PlayerPrefs.SetFloat(DemoAppConstants.PREF_MODAL_PHONE_H_LANDSCAPE, modalPhoneHLandscape);
            PlayerPrefs.SetFloat(DemoAppConstants.PREF_MODAL_TABLET_W_PORTRAIT, modalTabletWPortrait);
            PlayerPrefs.SetFloat(DemoAppConstants.PREF_MODAL_TABLET_H_PORTRAIT, modalTabletHPortrait);
            PlayerPrefs.SetFloat(DemoAppConstants.PREF_MODAL_TABLET_W_LANDSCAPE, modalTabletWLandscape);
            PlayerPrefs.SetFloat(DemoAppConstants.PREF_MODAL_TABLET_H_LANDSCAPE, modalTabletHLandscape);
            PlayerPrefs.Save();
        }

        private void ApplyCheckoutConfigToStashPayCard()
        {
            if (StashPayCard.Instance == null) return;
            StashPayCard.Instance.ForcePortraitOnCheckout = checkoutForcePortrait;
            StashPayCard.Instance.CardHeightRatioPortrait = checkoutCardHeightPortrait;
            StashPayCard.Instance.CardWidthRatioLandscape = checkoutCardWidthLandscape;
            StashPayCard.Instance.CardHeightRatioLandscape = checkoutCardHeightLandscape;
            StashPayCard.Instance.TabletWidthRatioPortrait = checkoutTabletWPortrait;
            StashPayCard.Instance.TabletHeightRatioPortrait = checkoutTabletHPortrait;
            StashPayCard.Instance.TabletWidthRatioLandscape = checkoutTabletWLandscape;
            StashPayCard.Instance.TabletHeightRatioLandscape = checkoutTabletHLandscape;
        }

        /// <summary>Returns the current modal config from settings (used when opening channel selection etc.).</summary>
        public StashPayModalConfig? GetCurrentModalConfig()
        {
            ReadModalFromInputs();
            return new StashPayModalConfig
            {
                showDragBar = modalShowDragBar,
                allowDismiss = modalAllowDismiss,
                phoneWidthRatioPortrait = modalPhoneWPortrait,
                phoneHeightRatioPortrait = modalPhoneHPortrait,
                phoneWidthRatioLandscape = modalPhoneWLandscape,
                phoneHeightRatioLandscape = modalPhoneHLandscape,
                tabletWidthRatioPortrait = modalTabletWPortrait,
                tabletHeightRatioPortrait = modalTabletHPortrait,
                tabletWidthRatioLandscape = modalTabletWLandscape,
                tabletHeightRatioLandscape = modalTabletHLandscape
            };
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
            StashDemoEnvironment newEnvironment = evt.newValue == "Production" ? StashDemoEnvironment.Production : StashDemoEnvironment.Test;
            if (newEnvironment != environment)
            {
                environment = newEnvironment;
                PlayerPrefs.SetString(DemoAppConstants.PREF_STASH_ENVIRONMENT, environment == StashDemoEnvironment.Production ? "Production" : "Test");
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
            PlayerPrefs.SetInt(DemoAppConstants.PREF_USE_SAFARI_WEBVIEW, useSafariWebView ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void OnShowMetricsToggleChanged(ChangeEvent<bool> evt)
        {
            showMetrics = evt.newValue;
            PlayerPrefs.SetInt(DemoAppConstants.PREF_SHOW_METRICS, showMetrics ? 1 : 0);
            PlayerPrefs.Save();
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

    }
}

