using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using StashPopup;
using Stash.Core;
using Stash.Scripts.Core;
using System;
using System.Threading.Tasks;
using System.Collections;
using UnityEngine.Networking;
using Canvas = UnityEngine.Canvas;
using CanvasScaler = UnityEngine.UI.CanvasScaler;
using Button = UnityEngine.UIElements.Button; // Specify we want UI Toolkit buttons
using Stash.Samples;

namespace Stash.Samples
{
    public class StashStoreUIController : MonoBehaviour
{
    [SerializeField] private UIDocument storeUIDocument;
    [SerializeField] private Texture2D[] itemImages;

    [Header("Store Items")]
    [SerializeField] private List<StoreItem> storeItems = new List<StoreItem>();

    [Header("Stash Configuration")]
    private string apiKey = "zyIbbfvO1ZRTaDt1VBZ5CJrwrdzyfDyLgt-VWNT-1uWj-5h42aeB6BNGAl8MGImw"; // Default API key
    private string channelSelectionUrl = "https://store.howlingwoods.shop/pay/channel-selection"; // Default channel selection URL
    [SerializeField] private StashEnvironment environment = StashEnvironment.Test;
    
    [Header("User Information")]
    // User information is now handled by AuthenticationManager
    
    [Header("Shop Configuration")]
    [SerializeField] private string shopHandle = "demo-shop";
    [SerializeField] private string currency = "USD";

    private VisualElement root;
    private List<Button> buyButtons = new List<Button>();
    
    // Safari WebView toggle
    private bool useSafariWebView = false; // Default to false (unchecked)
    
    // Show metrics toggle
    private bool showMetrics = false; // Default to false (disabled)
    
    // Payment method selection (Native IAP or Stash Pay)
    private string paymentMethod = "NATIVE_IAP"; // Default to Native IAP
    
    // Settings popup elements
    private VisualElement settingsPopup;
    private DropdownField paymentMethodDropdown;
    private DropdownField orientationModeDropdown;
    private Label deviceIdLabel;
    private Button settingsButton;
    private Button settingsPopupCloseButton;
    
    // Stash SDK settings popup elements
    private VisualElement stashSdkSettingsPopup;
    private TextField apiKeyInput;
    private DropdownField apiEnvironmentDropdown;
    private TextField channelSelectionUrlInput;
    private Toggle safariWebViewToggle;
    private Toggle showMetricsToggle;
    private TextField popupPortraitWidthInput;
    private TextField popupPortraitHeightInput;
    private TextField popupLandscapeWidthInput;
    private TextField popupLandscapeHeightInput;
    private Button stashSdkSettingsCloseButton;
    private Label stashLogoLabel;
    
    // Popup size configuration (defaults to null to use platform defaults)
    private PopupSizeConfig? customPopupSize = null;
    
    // Orientation mode: "Auto", "Portrait", or "Landscape"
    private string orientationMode = "Portrait";
    
    // Delegate for purchase callbacks
    public delegate void PurchaseCompletedDelegate(string itemId, bool success);
    public event PurchaseCompletedDelegate OnPurchaseCompleted;
    
    // Store the current checkout ID for verification
    private string currentCheckoutId;
    private int currentItemIndex;

    private void Start()
    {
        // Get the root of the UI document
        root = storeUIDocument.rootVisualElement;
        
        // Load orientation mode preference (default: "Portrait")
        // Check for new key first, then migrate from old key if needed
        if (PlayerPrefs.HasKey("OrientationMode"))
        {
            orientationMode = PlayerPrefs.GetString("OrientationMode", "Portrait");
        }
        else if (PlayerPrefs.HasKey("OrientationLocked"))
        {
            // Migrate from old boolean key to new string key
            bool wasLocked = PlayerPrefs.GetInt("OrientationLocked", 1) == 1;
            orientationMode = wasLocked ? "Portrait" : "Auto";
            PlayerPrefs.SetString("OrientationMode", orientationMode);
            PlayerPrefs.DeleteKey("OrientationLocked");
            PlayerPrefs.Save();
        }
        else
        {
            orientationMode = "Portrait"; // Default
        }
        
        // Load payment method preference (default: NATIVE_IAP)
        paymentMethod = PlayerPrefs.GetString("PaymentMethod", "NATIVE_IAP");
        
        // Load API key from PlayerPrefs (use default if not set)
        string savedApiKey = PlayerPrefs.GetString("StashApiKey", "");
        if (!string.IsNullOrEmpty(savedApiKey))
        {
            apiKey = savedApiKey;
        }
        
        // Load environment from PlayerPrefs (use default if not set)
        if (PlayerPrefs.HasKey("StashEnvironment"))
        {
            string savedEnvironment = PlayerPrefs.GetString("StashEnvironment", "Test");
            environment = savedEnvironment == "Production" ? StashEnvironment.Production : StashEnvironment.Test;
        }
        else
        {
            environment = StashEnvironment.Test; // Default to Test
        }
        
        // Load channel selection URL from PlayerPrefs (use default if not set)
        string savedChannelUrl = PlayerPrefs.GetString("ChannelSelectionUrl", "");
        if (!string.IsNullOrEmpty(savedChannelUrl))
        {
            channelSelectionUrl = savedChannelUrl;
        }
        
        // Load show metrics preference (default: false)
        showMetrics = PlayerPrefs.GetInt("ShowMetrics", 0) == 1;
        
        // Load popup size configuration from PlayerPrefs
        LoadPopupSizeConfig();
        
        // Apply orientation setting
        ApplyOrientationSetting();
        
        // Initialize IAP Manager for Apple Pay functionality
        InitializeIAP();
        
        // Setup settings popup
        SetupSettingsPopup();
        
        // Setup Stash SDK settings popup
        SetupStashSdkSettingsPopup();
        
        // Subscribe to page load events
        StashPayCard.Instance.OnPageLoaded += OnPageLoaded;
        
        // Ensure we have the right number of store items defined based on the UI
        ValidateAndInitializeStoreItems();
        
        // Setup store UI elements
        UpdateUIFromStoreItems();
        
        // Setup channel selection button
        SetupChannelSelectionButton();
        
    }
    
    private void SetupSettingsPopup()
    {
        // Get settings button
        settingsButton = root.Q<Button>("settings-button");
        if (settingsButton != null)
        {
            settingsButton.clicked += ShowSettingsPopup;
        }
        else
        {
            Debug.LogWarning("[StoreUI] Could not find settings-button");
        }
        
        // Get settings popup elements
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
            // Setup dropdown choices
            paymentMethodDropdown.choices = new List<string> { "Native IAP", "Stash Pay" };
            
            // Set initial value from PlayerPrefs
            paymentMethodDropdown.value = paymentMethod == "NATIVE_IAP" ? "Native IAP" : "Stash Pay";
            
            // Register callback
            paymentMethodDropdown.RegisterValueChangedCallback(OnPaymentMethodChanged);
        }
        else
        {
            Debug.LogWarning("[StoreUI] Could not find payment-method-dropdown");
        }
        
        if (orientationModeDropdown != null)
        {
            // Setup dropdown choices
            orientationModeDropdown.choices = new List<string> { "Auto", "Portrait", "Landscape" };
            
            // Set initial value from PlayerPrefs
            orientationModeDropdown.value = orientationMode;
            
            // Register callback
            orientationModeDropdown.RegisterValueChangedCallback(OnOrientationModeChanged);
        }
        else
        {
            Debug.LogWarning("[StoreUI] Could not find orientation-mode-dropdown");
        }
        
        if (deviceIdLabel != null)
        {
            // Set device ID
            string deviceId = SystemInfo.deviceUniqueIdentifier;
            deviceIdLabel.text = deviceId;
        }
        else
        {
            Debug.LogWarning("[StoreUI] Could not find device-id-label");
        }
        
        if (settingsPopupCloseButton != null)
        {
            settingsPopupCloseButton.clicked += HideSettingsPopup;
        }
        else
        {
            Debug.LogWarning("[StoreUI] Could not find settings-popup-close-button");
        }
    }
    
    private void SetupStashSdkSettingsPopup()
    {
        // Get Stash logo and make it clickable
        stashLogoLabel = root.Q<Label>("app-title");
        if (stashLogoLabel != null)
        {
            // Make the logo clickable by adding a pointer event handler
            stashLogoLabel.RegisterCallback<ClickEvent>(evt => ShowStashSdkSettingsPopup());
        }
        else
        {
            Debug.LogWarning("[StoreUI] Could not find app-title (Stash logo)");
        }
        
        // Get Stash SDK settings popup elements
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
        
        if (stashSdkSettingsPopup != null)
        {
            stashSdkSettingsPopup.visible = false;
        }
        
        if (apiKeyInput != null)
        {
            // Set initial value
            apiKeyInput.value = apiKey;
            
            // Register callback for when user finishes editing (on blur/enter)
            apiKeyInput.RegisterCallback<FocusOutEvent>(evt => OnApiKeyChanged());
        }
        else
        {
            Debug.LogWarning("[StoreUI] Could not find api-key-input");
        }
        
        if (apiEnvironmentDropdown != null)
        {
            // Setup dropdown choices
            apiEnvironmentDropdown.choices = new List<string> { "Test", "Production" };
            
            // Set initial value from current environment
            apiEnvironmentDropdown.value = environment == StashEnvironment.Production ? "Production" : "Test";
            
            // Register callback
            apiEnvironmentDropdown.RegisterValueChangedCallback(OnApiEnvironmentChanged);
        }
        else
        {
            Debug.LogWarning("[StoreUI] Could not find api-environment-dropdown");
        }
        
        if (channelSelectionUrlInput != null)
        {
            // Set initial value
            channelSelectionUrlInput.value = channelSelectionUrl;
            
            // Register callback for when user finishes editing (on blur/enter)
            channelSelectionUrlInput.RegisterCallback<FocusOutEvent>(evt => OnChannelSelectionUrlChanged());
        }
        else
        {
            Debug.LogWarning("[StoreUI] Could not find channel-selection-url-input");
        }
        
        if (safariWebViewToggle != null)
        {
            safariWebViewToggle.value = useSafariWebView; // Set initial value
            safariWebViewToggle.RegisterValueChangedCallback(OnSafariToggleChanged);
        }
        else
        {
            Debug.LogWarning("[StoreUI] Could not find safari-webview-toggle");
        }
        
        if (showMetricsToggle != null)
        {
            showMetricsToggle.value = showMetrics; // Set initial value from PlayerPrefs
            showMetricsToggle.RegisterValueChangedCallback(OnShowMetricsToggleChanged);
        }
        else
        {
            Debug.LogWarning("[StoreUI] Could not find show-metrics-toggle");
        }
        
        if (stashSdkSettingsCloseButton != null)
        {
            stashSdkSettingsCloseButton.clicked += HideStashSdkSettingsPopup;
        }
        else
        {
            Debug.LogWarning("[StoreUI] Could not find stash-sdk-settings-close-button");
        }
        
        // Setup popup size input fields
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
            Invoke(() => {
                if (settingsPopup != null && !settingsPopup.ClassListContains("visible"))
                {
                    settingsPopup.visible = false;
                    settingsPopup.style.display = DisplayStyle.None;
                }
            }, 0.3f); // Match the CSS transition duration
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
            Invoke(() => {
                if (stashSdkSettingsPopup != null && !stashSdkSettingsPopup.ClassListContains("visible"))
                {
                    stashSdkSettingsPopup.visible = false;
                    stashSdkSettingsPopup.style.display = DisplayStyle.None;
                }
            }, 0.3f); // Match the CSS transition duration
        }
    }
    
    private void OnPaymentMethodChanged(ChangeEvent<string> evt)
    {
        // Convert dropdown value to internal format
        paymentMethod = evt.newValue == "Native IAP" ? "NATIVE_IAP" : "STASH_PAY";
        
        // Save preference to PlayerPrefs
        PlayerPrefs.SetString("PaymentMethod", paymentMethod);
        PlayerPrefs.Save();
        
        Debug.Log($"[StoreUI] Payment method changed to: {paymentMethod}");
    }
    
    private void OnApiKeyChanged()
    {
        if (apiKeyInput == null) return;
        
        string newApiKey = apiKeyInput.value?.Trim() ?? "";
        
        // Only update if the key has actually changed and is not empty
        if (!string.IsNullOrEmpty(newApiKey) && newApiKey != apiKey)
        {
            apiKey = newApiKey;
            
            // Save to PlayerPrefs
            PlayerPrefs.SetString("StashApiKey", apiKey);
            PlayerPrefs.Save();
            
            Debug.Log($"[StoreUI] Stash API Key updated");
        }
    }
    
    private void OnApiEnvironmentChanged(ChangeEvent<string> evt)
    {
        // Convert dropdown value to StashEnvironment enum
        StashEnvironment newEnvironment = evt.newValue == "Production" ? StashEnvironment.Production : StashEnvironment.Test;
        
        // Update environment if it has changed
        if (newEnvironment != environment)
        {
            environment = newEnvironment;
            
            // Save to PlayerPrefs
            PlayerPrefs.SetString("StashEnvironment", environment == StashEnvironment.Production ? "Production" : "Test");
            PlayerPrefs.Save();
            
            Debug.Log($"[StoreUI] API Environment changed to: {environment}");
        }
    }
    
    private void OnChannelSelectionUrlChanged()
    {
        if (channelSelectionUrlInput == null) return;
        
        string newUrl = channelSelectionUrlInput.value?.Trim() ?? "";
        
        // Only update if the URL has actually changed and is not empty
        if (!string.IsNullOrEmpty(newUrl) && newUrl != channelSelectionUrl)
        {
            channelSelectionUrl = newUrl;
            
            // Save to PlayerPrefs
            PlayerPrefs.SetString("ChannelSelectionUrl", channelSelectionUrl);
            PlayerPrefs.Save();
            
            Debug.Log($"[StoreUI] Channel Selection URL updated to: {channelSelectionUrl}");
        }
    }
    
    private void OnSafariToggleChanged(ChangeEvent<bool> evt)
    {
        useSafariWebView = evt.newValue;
        Debug.Log($"[StoreUI] Safari WebView mode changed to: {useSafariWebView}");
    }
    
    private void OnShowMetricsToggleChanged(ChangeEvent<bool> evt)
    {
        showMetrics = evt.newValue;
        
        // Save preference to PlayerPrefs
        PlayerPrefs.SetInt("ShowMetrics", showMetrics ? 1 : 0);
        PlayerPrefs.Save();
        
        Debug.Log($"[StoreUI] Show metrics changed to: {showMetrics}");
    }
    
    private float ParseFloatValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return 0f;
        
        // Remove whitespace and 'f' suffix if present
        value = value.Trim().TrimEnd('f', 'F');
        
        if (float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float result))
        {
            return result;
        }
        
        return float.NaN; // Return NaN to indicate parse failure
    }
    
    private void OnPopupSizeChanged()
    {
        // Try to parse all four values
        bool allValid = true;
        float portraitWidth = 0f;
        float portraitHeight = 0f;
        float landscapeWidth = 0f;
        float landscapeHeight = 0f;
        
        if (popupPortraitWidthInput != null && !string.IsNullOrEmpty(popupPortraitWidthInput.value))
        {
            portraitWidth = ParseFloatValue(popupPortraitWidthInput.value);
            if (float.IsNaN(portraitWidth))
            {
                allValid = false;
            }
        }
        else
        {
            allValid = false;
        }
        
        if (popupPortraitHeightInput != null && !string.IsNullOrEmpty(popupPortraitHeightInput.value))
        {
            portraitHeight = ParseFloatValue(popupPortraitHeightInput.value);
            if (float.IsNaN(portraitHeight))
            {
                allValid = false;
            }
        }
        else
        {
            allValid = false;
        }
        
        if (popupLandscapeWidthInput != null && !string.IsNullOrEmpty(popupLandscapeWidthInput.value))
        {
            landscapeWidth = ParseFloatValue(popupLandscapeWidthInput.value);
            if (float.IsNaN(landscapeWidth))
            {
                allValid = false;
            }
        }
        else
        {
            allValid = false;
        }
        
        if (popupLandscapeHeightInput != null && !string.IsNullOrEmpty(popupLandscapeHeightInput.value))
        {
            landscapeHeight = ParseFloatValue(popupLandscapeHeightInput.value);
            if (float.IsNaN(landscapeHeight))
            {
                allValid = false;
            }
        }
        else
        {
            allValid = false;
        }
        
        if (allValid)
        {
            customPopupSize = new PopupSizeConfig
            {
                portraitWidthMultiplier = portraitWidth,
                portraitHeightMultiplier = portraitHeight,
                landscapeWidthMultiplier = landscapeWidth,
                landscapeHeightMultiplier = landscapeHeight
            };
            
            // Save to PlayerPrefs
            PlayerPrefs.SetFloat("PopupPortraitWidth", portraitWidth);
            PlayerPrefs.SetFloat("PopupPortraitHeight", portraitHeight);
            PlayerPrefs.SetFloat("PopupLandscapeWidth", landscapeWidth);
            PlayerPrefs.SetFloat("PopupLandscapeHeight", landscapeHeight);
            PlayerPrefs.SetInt("UseCustomPopupSize", 1);
            PlayerPrefs.Save();
            
            Debug.Log($"[StoreUI] Popup size updated: Portrait({portraitWidth}, {portraitHeight}), Landscape({landscapeWidth}, {landscapeHeight})");
        }
        else
        {
            // If any field is empty or invalid, clear the custom size (use platform defaults)
            customPopupSize = null;
            PlayerPrefs.SetInt("UseCustomPopupSize", 0);
            PlayerPrefs.Save();
            Debug.Log("[StoreUI] Popup size cleared - using platform defaults");
        }
    }
    
    private void LoadPopupSizeConfig()
    {
        // Check if custom popup size is enabled
        bool useCustomSize = PlayerPrefs.GetInt("UseCustomPopupSize", 0) == 1;
        
        if (useCustomSize)
        {
            float portraitWidth = PlayerPrefs.GetFloat("PopupPortraitWidth", 0.85f);
            float portraitHeight = PlayerPrefs.GetFloat("PopupPortraitHeight", 1.125f);
            float landscapeWidth = PlayerPrefs.GetFloat("PopupLandscapeWidth", 1.27075f);
            float landscapeHeight = PlayerPrefs.GetFloat("PopupLandscapeHeight", 0.9f);
            
            customPopupSize = new PopupSizeConfig
            {
                portraitWidthMultiplier = portraitWidth,
                portraitHeightMultiplier = portraitHeight,
                landscapeWidthMultiplier = landscapeWidth,
                landscapeHeightMultiplier = landscapeHeight
            };
        }
        else
        {
            customPopupSize = null;
        }
    }
    
    private void OnOrientationModeChanged(ChangeEvent<string> evt)
    {
        orientationMode = evt.newValue;
        
        // Save preference to PlayerPrefs
        PlayerPrefs.SetString("OrientationMode", orientationMode);
        PlayerPrefs.Save();
        
        // Apply the orientation setting
        ApplyOrientationSetting();
        
        Debug.Log($"[StoreUI] Orientation mode changed to: {orientationMode}");
    }
    
    private void ApplyOrientationSetting()
    {
        switch (orientationMode)
        {
            case "Portrait":
            // Lock to portrait only
            Screen.orientation = ScreenOrientation.Portrait;
            Screen.autorotateToPortrait = true;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = false;
            Screen.autorotateToLandscapeRight = false;
                break;
                
            case "Landscape":
                // Lock to landscape only
                Screen.orientation = ScreenOrientation.LandscapeLeft;
                Screen.autorotateToPortrait = false;
                Screen.autorotateToPortraitUpsideDown = false;
                Screen.autorotateToLandscapeLeft = true;
                Screen.autorotateToLandscapeRight = true;
                break;
                
            case "Auto":
            default:
            // Allow all orientations
            Screen.orientation = ScreenOrientation.AutoRotation;
            Screen.autorotateToPortrait = true;
            Screen.autorotateToPortraitUpsideDown = true;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
                break;
        }
    }
    
    private void InitializeIAP()
    {
        // Initializing IAP system
        
        // Create SimpleIAPManager if it doesn't exist
        if (SimpleIAPManager.Instance == null)
        {
            GameObject iapManager = new GameObject("SimpleIAPManager");
            iapManager.AddComponent<SimpleIAPManager>();
            DontDestroyOnLoad(iapManager);
            // Created new SimpleIAPManager instance
        }
        else
        {
            // IAP Manager instance found
        }
    }
    
    private void ValidateAndInitializeStoreItems()
    {
        // Check if we have store items defined in the editor
        if (storeItems.Count == 0)
        {
            // Initialize with default items
            InitializeDefaultStoreItems();
        }
        
        // We no longer need to count item elements in the UI
        // since we're creating them dynamically
    }
    
    private void UpdateUIFromStoreItems()
    {
        VisualElement itemsContainer = root.Q<VisualElement>("items-container");
        if (itemsContainer == null)
        {
            Debug.LogError("Could not find 'items-container' in the UI document!");
            return;
        }
        
        // Clear existing UI elements and buttons
        itemsContainer.Clear();
        buyButtons.Clear();
        
        // Create store items dynamically for each item in storeItems
        for (int i = 0; i < storeItems.Count; i++)
        {
            int itemIndex = i; // Capture for lambda
            StoreItem storeItem = storeItems[i];
            
            // Create the store item container
            VisualElement itemContainer = new VisualElement();
            itemContainer.name = $"item-{i+1}";
            itemContainer.AddToClassList("store-item");
            
            // Create the name label
            Label nameLabel = new Label(storeItem.name);
            nameLabel.name = $"item-{i+1}-name";
            nameLabel.AddToClassList("item-name");
            
            // Create the image element
            VisualElement imageElement = new VisualElement();
            imageElement.name = $"item-{i+1}-image";
            imageElement.AddToClassList("item-image");
            
            // Set image if available
            if (itemImages != null && i < itemImages.Length && itemImages[i] != null)
            {
                imageElement.style.backgroundImage = new StyleBackground(itemImages[i]);
            }
            
            // Create buy button
            Button buyButton = new Button();
            buyButton.name = $"buy-button-{i+1}";
            buyButton.text = "$" + storeItem.pricePerItem;
            buyButton.AddToClassList("buy-button");
            
            // Add click handler to directly process purchase with Stash checkout
            buyButton.clicked += () => ProcessPurchase(itemIndex);
            
            // Add to tracking list
            buyButtons.Add(buyButton);
            
            // Add elements to container in the desired order
            itemContainer.Add(nameLabel);
            itemContainer.Add(imageElement);
            itemContainer.Add(buyButton);
            
            // Add to container
            itemsContainer.Add(itemContainer);
        }
    }
    
    private void InitializeDefaultStoreItems()
    {
        // Add default store items only if none were defined in the editor
        storeItems.Add(new StoreItem {
            id = "fistful_of_potions",
            name = "Fistful of Potions",
            description = "A handful of powerful potions",
            pricePerItem = "0.99",
            imageUrl = ""
        });
        
        storeItems.Add(new StoreItem {
            id = "barrel_of_potions",
            name = "Barrel of Potions",
            description = "A whole barrel of potions",
            pricePerItem = "4.99",
            imageUrl = ""
        });
        
        storeItems.Add(new StoreItem {
            id = "battle_pass",
            name = "Battle Pass",
            description = "Premium battle pass with exclusive rewards",
            pricePerItem = "9.99",
            imageUrl = ""
        });
        
        storeItems.Add(new StoreItem {
            id = "small_resource_shipment_1",
            name = "Resource Shipment",
            description = "Small shipment of valuable resources",
            pricePerItem = "2.99",
            imageUrl = ""
        });
    }

    private void SetupChannelSelectionButton()
    {
        // Setup channel selection button
        Button channelSelectionButton = root.Q<Button>("open-channel-selection-button");

        if (channelSelectionButton != null)
        {
            channelSelectionButton.clicked += OpenPaymentChannelSelection;
            Debug.Log("[StoreUI] Channel selection button setup complete");
        }
        else
        {
            Debug.LogWarning("[StoreUI] Could not find open-channel-selection-button in UI");
        }
    }
    
    /// <summary>
    /// Opens the payment channel selection popup.
    /// This displays a centered popup for users to choose their preferred payment method.
    /// </summary>
    public void OpenPaymentChannelSelection()
    {
        try
        {
            // Ensure we have the latest values from input fields before opening
            PopupSizeConfig? sizeToUse = GetCurrentPopupSizeFromInputs();
            
            // Register opt-in response callback
            StashPayCard.Instance.OnOptinResponse += OnChannelSelectionOptinResponse;
            
            // Open the payment channel selection URL in a centered popup (using configured URL)
            StashPayCard.Instance.OpenPopup(channelSelectionUrl,
                dismissCallback: () => {
                    // Show toast when popup is dismissed
                    // ShowToast("Popup dismissed", "The payment channel selection dialog was closed.");
                    // Unregister callback when popup closes
                    StashPayCard.Instance.OnOptinResponse -= OnChannelSelectionOptinResponse;
                },
                customSize: sizeToUse);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[StoreUI] Exception opening payment channel selection: {ex.Message}\nStackTrace: {ex.StackTrace}");
        }
    }
    
    private PopupSizeConfig? GetCurrentPopupSizeFromInputs()
    {
        // Try to read current values from input fields
        string portraitWidthStr = popupPortraitWidthInput?.value ?? "";
        string portraitHeightStr = popupPortraitHeightInput?.value ?? "";
        string landscapeWidthStr = popupLandscapeWidthInput?.value ?? "";
        string landscapeHeightStr = popupLandscapeHeightInput?.value ?? "";
        
        float portraitWidth = ParseFloatValue(portraitWidthStr);
        float portraitHeight = ParseFloatValue(portraitHeightStr);
        float landscapeWidth = ParseFloatValue(landscapeWidthStr);
        float landscapeHeight = ParseFloatValue(landscapeHeightStr);
        
        Debug.Log($"[StoreUI] Reading popup size from inputs: Portrait({portraitWidthStr}->{portraitWidth}), Landscape({landscapeWidthStr}->{landscapeWidth})");
        
        // Check if all values are valid (not NaN and not empty)
        bool allValid = !float.IsNaN(portraitWidth) && !float.IsNaN(portraitHeight) && 
                       !float.IsNaN(landscapeWidth) && !float.IsNaN(landscapeHeight) &&
                       !string.IsNullOrWhiteSpace(portraitWidthStr) &&
                       !string.IsNullOrWhiteSpace(portraitHeightStr) &&
                       !string.IsNullOrWhiteSpace(landscapeWidthStr) &&
                       !string.IsNullOrWhiteSpace(landscapeHeightStr);
        
        if (allValid)
        {
            PopupSizeConfig config = new PopupSizeConfig
            {
                portraitWidthMultiplier = portraitWidth,
                portraitHeightMultiplier = portraitHeight,
                landscapeWidthMultiplier = landscapeWidth,
                landscapeHeightMultiplier = landscapeHeight
            };
            
            Debug.Log($"[StoreUI] Using custom popup size: Portrait({config.portraitWidthMultiplier}, {config.portraitHeightMultiplier}), Landscape({config.landscapeWidthMultiplier}, {config.landscapeHeightMultiplier})");
            return config;
        }
        
        // If inputs are not valid, use the saved customPopupSize (or null for platform defaults)
        Debug.Log($"[StoreUI] Inputs not valid, using saved customPopupSize: {customPopupSize.HasValue}");
        return customPopupSize;
    }

    private void OnChannelSelectionOptinResponse(string optinType)
    {
        Debug.Log($"[StoreUI] User selected payment method: {optinType}");
        
        // Normalize to uppercase format (handles both "stash_pay" and "STASH_PAY")
        string normalizedType = optinType.ToUpper();
        
        // Update payment method preference based on user selection
        if (normalizedType == "STASH_PAY" || normalizedType == "NATIVE_IAP")
        {
            paymentMethod = normalizedType;
            
            // Save preference to PlayerPrefs
            PlayerPrefs.SetString("PaymentMethod", paymentMethod);
            PlayerPrefs.Save();
            
            // Update dropdown to reflect the change
            if (paymentMethodDropdown != null)
            {
                paymentMethodDropdown.value = paymentMethod == "NATIVE_IAP" ? "Native IAP" : "Stash Pay";
            }
            
            // Show toast notification
            string displayName = paymentMethod == "NATIVE_IAP" ? "Native IAP" : "Stash Pay";
            ShowToast("Payment Method Selected", $"You selected: {displayName}");
            
            Debug.Log($"[StoreUI] Payment method updated to: {paymentMethod}");
        }
        else
        {
            Debug.LogWarning($"[StoreUI] Unknown payment method selected: {optinType} (normalized: {normalizedType})");
            ShowToast("Unknown Selection", $"Received unknown payment method: {optinType}");
        }
    }
    
    private void ShowToast(string title, string message)
    {
        try
        {
            // Create toast notification
            var toastGO = new GameObject("PopupCallbackToast");
            var toastScript = toastGO.AddComponent<SimpleToast>();
            
            // Set the root element
            if (root != null)
            {
                toastScript.SetRootElement(root);
            }
            
            toastScript.Show(title, message);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[StoreUI] Error showing toast: {ex.Message}");
        }
    }

    private void ProcessIAPPurchase(int itemIndex)
    {
        // Processing IAP purchase
        
        if (storeItems == null || itemIndex < 0 || itemIndex >= storeItems.Count)
        {
            Debug.LogError($"[StoreUI] Invalid item index in ProcessIAPPurchase: {itemIndex}! Store items count: {storeItems?.Count ?? 0}");
            return;
        }

        StoreItem item = storeItems[itemIndex];
        
        // Starting IAP transaction
        
        // Check if IAP Manager is ready
        if (SimpleIAPManager.Instance == null)
        {
            Debug.LogError("[StoreUI] ❌ SimpleIAPManager instance is null");
            Debug.LogError("[StoreUI] This means the IAP manager was never created or was destroyed");
            ShowIAPErrorMessage("In-app purchases are not available. Please restart the app and try again.");
            OnPurchaseCompleted?.Invoke(item.id, false);
            return;
        }
        
        if (!SimpleIAPManager.Instance.IsReady())
        {
            string status = SimpleIAPManager.Instance.GetInitializationStatus();
            Debug.LogWarning($"[StoreUI] ⚠️ SimpleIAPManager not ready yet. Status: {status}");
            
            if (SimpleIAPManager.Instance.IsInitializing())
            {
                Debug.LogWarning("[StoreUI] IAP is still initializing, please wait...");
                ShowIAPErrorMessage("In-app purchases are still loading.\nPlease wait a moment and try again.");
            }
            else
            {
                Debug.LogError("[StoreUI] IAP initialization failed!");
                ShowIAPErrorMessage("Native IAP failed to initialize.\nPlease use sandbox account for native purchases.");
            }
            
            OnPurchaseCompleted?.Invoke(item.id, false);
            return;
        }
        
                    // IAP Manager ready, proceeding
        
        // Subscribe to purchase events temporarily
        System.Action<string> onSuccess = null;
        System.Action<string, string> onFailure = null;
        
        onSuccess = (productId) => {
            if (productId == item.id)
            {
                Debug.Log($"[StoreUI] ✅ IAP purchase successful for: {productId}");
                ShowSuccessPopupWithConfetti("Purchase Successful!", $"You successfully purchased {item.name}!");
                OnPurchaseCompleted?.Invoke(item.id, true);
                
                // Unsubscribe
                SimpleIAPManager.Instance.OnPurchaseSuccess -= onSuccess;
                SimpleIAPManager.Instance.OnPurchaseFailure -= onFailure;
            }
        };
        
        onFailure = (productId, error) => {
            if (productId == item.id)
            {
                Debug.LogError($"[StoreUI] ❌ IAP purchase failed for: {productId}, error: {error}");
                ShowIAPErrorMessage($"Purchase failed: {error}");
                OnPurchaseCompleted?.Invoke(item.id, false);
                
                // Unsubscribe
                SimpleIAPManager.Instance.OnPurchaseSuccess -= onSuccess;
                SimpleIAPManager.Instance.OnPurchaseFailure -= onFailure;
            }
        };
        
        // Subscribe to events
        SimpleIAPManager.Instance.OnPurchaseSuccess += onSuccess;
        SimpleIAPManager.Instance.OnPurchaseFailure += onFailure;
        
        // Start the purchase
                    // Initiating purchase
        SimpleIAPManager.Instance.BuyProduct(item.id);
    }
    

    
    private void ShowIAPErrorMessage(string errorMessage)
    {
        try
        {
            // Create an error popup
            var errorPopup = new GameObject("IAPErrorPopup");
            var popupScript = errorPopup.AddComponent<SuccessPopup>(); // Reusing SuccessPopup class
            
            // Set the root element directly to avoid search issues
            if (root != null)
            {
                popupScript.SetRootElement(root);
            }
            
            popupScript.Show("Purchase Error", errorMessage);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[StoreUI] Error showing IAP error message: {ex.Message}");
        }
    }

    private void ProcessPurchase(int itemIndex)
    {
        // Processing purchase request
        
        if (storeItems == null)
        {
            Debug.LogError("[StoreUI] storeItems is null in ProcessPurchase!");
            return;
        }
        
        if (itemIndex < 0 || itemIndex >= storeItems.Count)
        {
            Debug.LogError($"[StoreUI] Invalid item index in ProcessPurchase: {itemIndex}! Valid range: 0 to {storeItems.Count - 1}");
            return;
        }

        StoreItem item = storeItems[itemIndex];
        
        // Check which purchase method to use based on settings
        if (paymentMethod == "NATIVE_IAP")
        {
            Debug.Log($"Processing purchase with Native IAP for item: {item.id}");
            ProcessIAPPurchase(itemIndex);
        }
        else // STASH_PAY
        {
            Debug.Log($"Processing purchase with Stash Pay for item: {item.id} at price: {item.pricePerItem}");
            
            // Disable the buy button to prevent multiple checkouts
            SetButtonEnabled(buyButtons[itemIndex], false);
            
            // Display a loading indicator
            SetButtonLoadingState(buyButtons[itemIndex], true);

            // Block navigation during purchase
            NavigationBlocker.Instance.BlockNavigation();
            
            // Open Stash popup for checkout
            OpenStashCheckout(itemIndex);
        }
    }
    
    private async void OpenStashCheckout(int itemIndex)
    {
        try
        {
            StoreItem item = storeItems[itemIndex];
            
            string userId;
            string email;
            
            // Check if user is authenticated
            if (AuthenticationManager.Instance != null && AuthenticationManager.Instance.IsAuthenticated())
            {
                // Use authenticated user data
                UserData userData = AuthenticationManager.Instance.GetUserData();
                userId = userData.UserId;
                email = userData.Email;
            }
            else
            {
                // Generate random credentials for unauthenticated users
                userId = $"guest_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
                email = $"guest_{Guid.NewGuid().ToString("N").Substring(0, 8)}@example.com";
                // Using generated guest credentials
            }
            
            // Create CheckoutItemData from StoreItem
            var checkoutItem = new StashCheckout.CheckoutItemData
            {
                id = item.id,
                pricePerItem = item.pricePerItem,
                quantity = 1,
                imageUrl = item.imageUrl,
                name = item.name,
                description = item.description
            };

            // Generate the checkout URL using the Stash Checkout API
            (string url, string id) = await StashCheckout.CreateCheckoutLink(
                userId,
                email,
                shopHandle,
                checkoutItem,
                apiKey,
                environment
            );
            
            // Store the checkout ID and item index for verification later
            currentCheckoutId = id;
            currentItemIndex = itemIndex;
            
            // Open the checkout URL in the StashPayCard
            // Apply user's preference for Web View mode
            // This respects remote Flagsmith configuration while allowing local override
            StashPayCard.Instance.ForceWebBasedCheckout = useSafariWebView;
            
            StashPayCard.Instance.OpenCheckout(url, () => OnBrowserClosed(), () => OnPaymentSuccessDetected(), () => OnPaymentFailureDetected());
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Stash] Error generating checkout URL: {ex.Message}");
            // Reset button state on error
            SetButtonLoadingState(buyButtons[itemIndex], false);
            HandleFailedPurchase(itemIndex);
        }
    }
    
    
    
    private void OnBrowserClosed()
    {
        // Browser closed, verifying purchase
        
        // Re-enable the buy button since checkout was dismissed
        SetButtonEnabled(buyButtons[currentItemIndex], true);
        
        // Unblock navigation when browser is closed
        NavigationBlocker.Instance.UnblockNavigation();
        
        // Verify the purchase status by calling the Stash API
        StartCoroutine(VerifyPurchase());
    }
    
    private IEnumerator VerifyPurchase()
    {
        if (string.IsNullOrEmpty(currentCheckoutId))
        {
            Debug.LogError("[Stash] Cannot verify purchase: checkout ID is null or empty");
            HandleFailedPurchase(currentItemIndex);
            yield break;
        }
        
        // Create the verification request URL
        string verifyUrl = GetVerificationUrl(currentCheckoutId);
                    // Verifying purchase with backend
        
        using (UnityWebRequest request = UnityWebRequest.PostWwwForm(verifyUrl, ""))
        {
            // Add authorization header
            request.SetRequestHeader("X-Stash-Api-Key", apiKey);
            
            // Send the request
            yield return request.SendWebRequest();
            
            bool isSuccessful = false;
            PurchaseVerificationResponse purchaseResponse = null;
            
            // First check HTTP response
            if (request.result == UnityWebRequest.Result.Success && request.responseCode == 200)
            {
                // Parse the response to get item details
                string responseText = request.downloadHandler.text;
                // Purchase response received
                
                try
                {
                    // Parse the response
                    purchaseResponse = JsonUtility.FromJson<PurchaseVerificationResponse>(responseText);
                    
                    // Strict verification: check all required fields exist
                    isSuccessful = 
                        purchaseResponse != null && 
                        purchaseResponse.paymentSummary != null &&
                        !string.IsNullOrEmpty(purchaseResponse.paymentSummary.total) &&
                        purchaseResponse.items != null && 
                        purchaseResponse.items.Length > 0;
                        
                    // Purchase verification completed
                    
                    // Additional logging to help debug
                    if (purchaseResponse == null)
                    {
                        Debug.LogWarning("[Stash] Purchase response is null");
                    }
                    else if (purchaseResponse.paymentSummary == null)
                    {
                        Debug.LogWarning("[Stash] Payment summary is null");
                    }
                    else if (string.IsNullOrEmpty(purchaseResponse.paymentSummary.total))
                    {
                        Debug.LogWarning("[Stash] Payment total is missing");
                    }
                    else if (purchaseResponse.items == null || purchaseResponse.items.Length == 0)
                    {
                        Debug.LogWarning("[Stash] Items array is null or empty");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Stash] Failed to parse purchase response: {ex.Message}");
                    isSuccessful = false;
                }
            }
            else
            {
                // HTTP error
                Debug.LogWarning($"[Stash] Purchase verification failed with status: {request.responseCode}. Error: {request.error}");
                isSuccessful = false;
            }
            
            // Handle result based on verification
            if (isSuccessful)
            {
                // Purchase was successful
                                    // Purchase verified successfully
                HandleSuccessfulPurchase(currentItemIndex);
                
                // Get item name and payment details
                string itemName = purchaseResponse.items[0].name;
                
                // Show success popup with payment details
                ShowSuccessPopup(
                    itemName,
                    purchaseResponse.currency,
                    purchaseResponse.paymentSummary.total,
                    purchaseResponse.paymentSummary.tax,
                    purchaseResponse.paymentSummary.timeMillis
                );
                
                // Notify listeners
                if (storeItems != null && currentItemIndex >= 0 && currentItemIndex < storeItems.Count)
                {
                    OnPurchaseCompleted?.Invoke(storeItems[currentItemIndex].id, true);
                }
                else
                {
                    Debug.LogError($"[StoreUI] Cannot invoke OnPurchaseCompleted - invalid currentItemIndex: {currentItemIndex}");
                }
            }
            else
            {
                // Purchase failed verification
                Debug.LogWarning("[Stash] Purchase verification failed");
                HandleFailedPurchase(currentItemIndex);
                if (storeItems != null && currentItemIndex >= 0 && currentItemIndex < storeItems.Count)
                {
                    OnPurchaseCompleted?.Invoke(storeItems[currentItemIndex].id, false);
                }
                else
                {
                    Debug.LogError($"[StoreUI] Cannot invoke OnPurchaseCompleted - invalid currentItemIndex: {currentItemIndex}");
                }
            }
            
            // Reset button state
            SetButtonLoadingState(buyButtons[currentItemIndex], false);
        }
    }
    
    private string GetVerificationUrl(string checkoutId)
    {
        // Use the environment's root URL from Stash.Core
        string baseUrl = environment.GetRootUrl();
        // Use the constant for the checkout order endpoint
        return $"{baseUrl}/sdk/checkout_links/order/{checkoutId}";
    }
    
    private void ShowSuccessPopup(string itemName = null, string currency = null, string total = null, string tax = null, string timeStamp = null)
    {
        // Create and show a success popup
        var successPopup = new GameObject("SuccessPopup");
        var popupScript = successPopup.AddComponent<SuccessPopup>();
        
        // Set the root element directly to avoid search issues
        if (root != null)
        {
            popupScript.SetRootElement(root);
        }
        
        // Build the success message with payment details
        string message = "Your purchase";
        
        // Add item name if available
        if (!string.IsNullOrEmpty(itemName))
        {
            message += $" of {itemName}";
        }
        
        message += " has been completed successfully!";
        
        // Add payment details if available
        string paymentDetails = "";
        if (!string.IsNullOrEmpty(total) && !string.IsNullOrEmpty(currency))
        {
            paymentDetails += $"\nAmount: {total} {currency}";
        }
        
        if (!string.IsNullOrEmpty(tax) && tax != "0" && tax != "")
        {
            paymentDetails += $"\nTax: {tax} {currency}";
        }
        
        if (!string.IsNullOrEmpty(timeStamp))
        {
            // Convert timestamp to readable date if possible
            try
            {
                long milliseconds = long.Parse(timeStamp);
                DateTime dateTime = DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).DateTime.ToLocalTime();
                paymentDetails += $"\nDate: {dateTime:g}";
            }
            catch
            {
                // If timestamp conversion fails, just show the raw value
                paymentDetails += $"\nTimestamp: {timeStamp}";
            }
        }
        
        // Show the popup with all available information
        popupScript.Show("Purchase Successful", message + paymentDetails);
    }
    
    private void HandleSuccessfulPurchase(int itemIndex)
    {
        if (storeItems == null || itemIndex < 0 || itemIndex >= storeItems.Count)
        {
            Debug.LogError($"[StoreUI] Invalid item index {itemIndex} in HandleSuccessfulPurchase. Store items count: {storeItems?.Count ?? 0}");
            return;
        }
        
        // Purchase completed for item
        
        // Re-enable the buy button after successful purchase
        if (buyButtons != null && itemIndex < buyButtons.Count)
        {
            SetButtonEnabled(buyButtons[itemIndex], true);
        }
        
        // Unblock navigation after successful purchase
        NavigationBlocker.Instance.UnblockNavigation();
        
        // Implement your purchase success logic
        // Could include adding the item to inventory, showing success message, etc.
    }
    
    private void HandleFailedPurchase(int itemIndex)
    {
        if (storeItems == null || itemIndex < 0 || itemIndex >= storeItems.Count)
        {
            Debug.LogError($"[StoreUI] Invalid item index {itemIndex} in HandleFailedPurchase. Store items count: {storeItems?.Count ?? 0}");
            return;
        }
        
        Debug.LogError("Purchase failed for selected item");
        
        // Re-enable the buy button after failed purchase
        if (buyButtons != null && itemIndex < buyButtons.Count)
        {
            SetButtonEnabled(buyButtons[itemIndex], true);
            
            // Show the purchase failed state on the button
            Button button = buyButtons[itemIndex];
            button.AddToClassList("purchase-failed");
            
            // Remove the failed state after a short delay
            Invoke(() => {
                button.RemoveFromClassList("purchase-failed");
            }, 2f);
        }
        
        // Unblock navigation after failed purchase
        NavigationBlocker.Instance.UnblockNavigation();
    }
    
    private void SetButtonLoadingState(Button button, bool isLoading)
    {
        if (isLoading)
        {
            button.AddToClassList("button-loading");
            button.text = "...";
        }
        else
        {
            try
            {
                // Get the item index from the button name
                string buttonName = button.name;
                int itemIndex = int.Parse(buttonName.Split('-')[2]) - 1;
                
                if (storeItems == null || itemIndex < 0 || itemIndex >= storeItems.Count)
                {
                    Debug.LogError($"[StoreUI] Invalid item index {itemIndex} in SetButtonLoadingState. Store items count: {storeItems?.Count ?? 0}");
                    button.text = "BUY";
                    return;
                }
                
                button.text = "$" + storeItems[itemIndex].pricePerItem;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[StoreUI] Exception in SetButtonLoadingState: {ex.Message}");
                button.text = "BUY";
            }
        }
    }
    
    private void SetButtonEnabled(Button button, bool enabled)
    {
        button.SetEnabled(enabled);
        if (!enabled)
        {
            button.AddToClassList("button-disabled");
        }
        else
        {
            button.RemoveFromClassList("button-disabled");
        }
    }
    
    private void Invoke(Action action, float delay)
    {
        StartCoroutine(InvokeCoroutine(action, delay));
    }
    
    private System.Collections.IEnumerator InvokeCoroutine(Action action, float delay)
    {
        yield return new WaitForSeconds(delay);
        action?.Invoke();
    }
    
    // Show login required message
    private void ShowLoginRequiredMessage()
    {
        Debug.Log("Showing login required message");
        
        // Create a login required popup
        var loginPopup = new GameObject("LoginRequiredPopup");
        var popupScript = loginPopup.AddComponent<SuccessPopup>(); // Reusing the SuccessPopup class
        
        string message = "You need to log in before making a purchase.\n\nPlease go to the User tab and log in.";
        popupScript.Show("Login Required", message);
        
        // Also switch to user tab to make it easier for the user
        TabController tabController = FindObjectOfType<TabController>();
        if (tabController != null)
        {
            tabController.SelectTab("user");
        }
    }
    
    // Store item data structure
    [System.Serializable]
    public class StoreItem
    {
        public string id = "item_id";
        public string name = "Item Name";
        public string description = "Item Description";
        public string pricePerItem = "0.99";
        [Tooltip("URL for the item image in the Stash checkout page")]
        public string imageUrl = "";
    }

    // Purchase response data classes
    [Serializable]
    private class PurchaseVerificationResponse
    {
        public string currency;
        public PurchaseItem[] items;
        public PaymentSummary paymentSummary;
    }
    
    [Serializable]
    private class PurchaseItem
    {
        public string id;
        public string pricePerItem;
        public int quantity;
        public string imageUrl;
        public string name;
        public string description;
    }
    
    [Serializable]
    private class PaymentSummary
    {
        public string timeMillis;
        public string total;
        public string tax;
    }

    private void OnPaymentSuccessDetected()
    {
        Debug.Log("[Stash] Payment success detected from iOS");
        
        try
        {
            // Validate currentItemIndex before accessing storeItems
            if (storeItems == null || currentItemIndex < 0 || currentItemIndex >= storeItems.Count)
            {
                Debug.LogError($"[StoreUI] Invalid currentItemIndex {currentItemIndex}. Store items count: {storeItems?.Count ?? 0}");
                return;
            }
            
            // Get the current item details
            StoreItem currentItem = storeItems[currentItemIndex];
            
            // Show success popup with confetti
            ShowSuccessPopupWithConfetti("Payment Successful!", $"You successfully purchased {currentItem.name}!");
            
            // Re-enable UI after successful payment
            if (buyButtons != null && currentItemIndex >= 0 && currentItemIndex < buyButtons.Count)
            {
                SetButtonEnabled(buyButtons[currentItemIndex], true);
                SetButtonLoadingState(buyButtons[currentItemIndex], false);
            }
            
            // Unblock navigation after successful payment
            NavigationBlocker.Instance.UnblockNavigation();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Stash] Error handling payment success: {ex.Message}");
        }
    }

    private void OnPaymentFailureDetected()
    {
        Debug.Log("[Stash] Payment failure detected from iOS");
        
        try
        {
            // Validate currentItemIndex before accessing storeItems
            if (storeItems == null || currentItemIndex < 0 || currentItemIndex >= storeItems.Count)
            {
                Debug.LogError($"[StoreUI] Invalid currentItemIndex {currentItemIndex}. Store items count: {storeItems?.Count ?? 0}");
                return;
            }
            
            // Handle the failed purchase
            HandleFailedPurchase(currentItemIndex);
            
            // Re-enable UI after failed payment (same as success handler)
            if (buyButtons != null && currentItemIndex >= 0 && currentItemIndex < buyButtons.Count)
            {
                SetButtonEnabled(buyButtons[currentItemIndex], true);
                SetButtonLoadingState(buyButtons[currentItemIndex], false);
            }
            
            // Show failure message to user
            ShowPaymentFailureMessage();
            
            // Notify listeners
            OnPurchaseCompleted?.Invoke(storeItems[currentItemIndex].id, false);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Stash] Error handling payment failure: {ex.Message}");
        }
    }
    
    private void ShowPaymentFailureMessage()
    {
        try
        {
            // Create a failure popup
            var failurePopup = new GameObject("PaymentFailurePopup");
            var popupScript = failurePopup.AddComponent<SuccessPopup>(); // Reusing the SuccessPopup class
            
            // Set the root element directly to avoid search issues
            if (root != null)
            {
                popupScript.SetRootElement(root);
            }
            
            string message = "Your payment could not be processed.\n\nPlease try again or contact support if the problem persists.";
            
            // Add the current item name if available
            if (storeItems != null && currentItemIndex >= 0 && currentItemIndex < storeItems.Count)
            {
                string itemName = storeItems[currentItemIndex].name;
                message = $"Payment for {itemName} could not be processed.\n\nPlease try again or contact support if the problem persists.";
            }
            
            popupScript.Show("Payment Failed", message);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Stash] Error showing payment failure message: {ex.Message}");
        }
    }
    
    private void ShowSuccessPopupWithConfetti(string title, string message)
    {
        try
        {
            // Create success popup
            var successPopup = new GameObject("SuccessPopup");
            var popupScript = successPopup.AddComponent<SuccessPopup>();
            
            // Set the root element directly
            if (root != null)
            {
                popupScript.SetRootElement(root);
            }
            
            popupScript.Show(title, message);
            
            // Create confetti effect
            CreateConfettiEffect();
            
            Debug.Log($"[Stash] Success popup shown with confetti: {title} - {message}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Stash] Error showing success popup: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Creates a simple confetti effect using UI Toolkit VisualElements
    /// </summary>
    private void CreateConfettiEffect()
    {
        try
        {
            // Find the root visual element from UI Document
            VisualElement rootElement = null;
            if (storeUIDocument != null && storeUIDocument.rootVisualElement != null)
            {
                rootElement = storeUIDocument.rootVisualElement;
            }
            else
            {
                var uiDoc = FindObjectOfType<UIDocument>();
                if (uiDoc != null && uiDoc.rootVisualElement != null)
                {
                    rootElement = uiDoc.rootVisualElement;
                }
            }
            
            if (rootElement == null)
            {
                Debug.LogError("[Stash] Could not find root visual element for confetti");
                return;
            }
            
            // Create confetti container that covers the entire screen
            VisualElement confettiContainer = new VisualElement();
            confettiContainer.name = "ConfettiContainer";
            confettiContainer.style.position = Position.Absolute;
            confettiContainer.style.top = 0;
            confettiContainer.style.left = 0;
            confettiContainer.style.width = Length.Percent(100);
            confettiContainer.style.height = Length.Percent(100);
            confettiContainer.pickingMode = PickingMode.Ignore;
            rootElement.Add(confettiContainer);
            
            // Create a simple confetti animator component
            GameObject confettiAnimatorGO = new GameObject("ConfettiAnimator");
            ConfettiAnimator animator = confettiAnimatorGO.AddComponent<ConfettiAnimator>();
            animator.Initialize(confettiContainer, rootElement);
            
            // Auto-destroy after animation completes (account for delays)
            Destroy(confettiAnimatorGO, 6f);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Stash] Error creating confetti effect: {ex.Message}");
        }
    }
    
        private void OnDestroy()
    {
        Debug.Log($"[StoreUI] OnDestroy called for StashStoreUIController instance: {GetInstanceID()}");
        // Unsubscribe from payment success events
        if (StashPayCard.Instance != null)
        {
            Debug.Log($"[StoreUI] Unsubscribing OnPaymentSuccessDetected callback for instance: {GetInstanceID()}");
            StashPayCard.Instance.OnPaymentSuccess -= OnPaymentSuccessDetected;
            StashPayCard.Instance.OnPageLoaded -= OnPageLoaded;
        }
    }
    
    private void OnPageLoaded(double loadTimeMs)
    {
        Debug.Log($"[StoreUI] Page loaded in {loadTimeMs:F0} ms");
        ShowLoadTimeToast(loadTimeMs);
    }
    
    private void ShowLoadTimeToast(double loadTimeMs)
    {
        // Only show toast if metrics are enabled
        if (!showMetrics)
        {
            return;
        }
        
        try
        {
            // Create toast notification
            var toastGO = new GameObject("LoadTimeToast");
            var toastScript = toastGO.AddComponent<LoadTimeToast>();
            
            // Set the root element
            if (root != null)
            {
                toastScript.SetRootElement(root);
            }
            
            toastScript.Show(loadTimeMs);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[StoreUI] Error showing load time toast: {ex.Message}");
        }
    }
}

// Modern UI Toolkit-based popup class for showing purchase messages
public class SuccessPopup : MonoBehaviour
{
    private VisualElement popupContainer;
    private VisualElement popupCard;
    private float showDuration = 3f;
    private VisualElement overrideRootElement;
    
    public void SetRootElement(VisualElement rootElement)
    {
        overrideRootElement = rootElement;
    }
    
    public void Show(string title, string message)
    {
        CreateUIToolkitPopup(title, message);
        
        // Close after duration
        Destroy(gameObject, showDuration);
    }
    
    private void CreateUIToolkitPopup(string title, string message)
    {
        // Try multiple approaches to find the root visual element
        VisualElement rootElement = null;
        
        // First check if we have an override root element set directly
        if (overrideRootElement != null)
        {
            rootElement = overrideRootElement;
            Debug.Log("[SuccessPopup] Using override root element");
        }
        
        // First try to get from current GameObject (unlikely to work for dynamically created objects)
        if (rootElement == null)
        {
            var currentUIDocument = GetComponent<UIDocument>();
            if (currentUIDocument != null)
            {
                rootElement = currentUIDocument.rootVisualElement;
                Debug.Log("[SuccessPopup] Found UIDocument on current GameObject");
            }
        }
        
        // Try to find StashStoreUIController and get its UIDocument
        if (rootElement == null)
        {
            var storeUIController = FindObjectOfType<StashStoreUIController>();
            if (storeUIController != null)
            {
                Debug.Log("[SuccessPopup] Found StashStoreUIController");
                
                // Get the UIDocument component
                var uiDocument = storeUIController.GetComponent<UIDocument>();
                if (uiDocument != null)
                {
                    rootElement = uiDocument.rootVisualElement;
                    Debug.Log("[SuccessPopup] Using UIDocument component on StashStoreUIController");
                }
                else
                {
                    Debug.LogWarning("[SuccessPopup] StashStoreUIController found but no UIDocument component");
                }
            }
            else
            {
                Debug.LogError("[SuccessPopup] Could not find StashStoreUIController in scene");
            }
        }
        
        // Try to find any UIDocument in the scene as last resort
        if (rootElement == null)
        {
            var allUIDocuments = FindObjectsOfType<UIDocument>();
            Debug.Log($"[SuccessPopup] Found {allUIDocuments.Length} UIDocument(s) in scene");
            
            foreach (var doc in allUIDocuments)
            {
                if (doc.rootVisualElement != null)
                {
                    rootElement = doc.rootVisualElement;
                    Debug.Log($"[SuccessPopup] Using UIDocument from GameObject: {doc.gameObject.name}");
                    break;
                }
            }
        }
        
        if (rootElement == null) 
        {
            Debug.LogError("[SuccessPopup] Could not find root visual element for popup - no UIDocument found in scene");
            return;
        }
        
        // Calculate responsive sizes based on screen - much smaller for success/failure popups
        float screenWidth = Screen.width;
        float screenHeight = Screen.height;
        float popupWidth = Mathf.Clamp(screenWidth * 0.5f, 150f, 200f);
        float popupHeight = Mathf.Clamp(screenHeight * 0.2f, 120f, 180f);
        
        // Create popup container (full screen overlay)
        popupContainer = new VisualElement();
        popupContainer.style.position = Position.Absolute;
        popupContainer.style.top = 0;
        popupContainer.style.left = 0;
        popupContainer.style.width = Length.Percent(100);
        popupContainer.style.height = Length.Percent(100);
        popupContainer.style.backgroundColor = new Color(0, 0, 0, 0.75f);
        popupContainer.style.alignItems = Align.Center;
        popupContainer.style.justifyContent = Justify.Center;
        popupContainer.style.flexDirection = FlexDirection.Column;
        
        // Create popup card with responsive sizing
        popupCard = new VisualElement();
        popupCard.style.backgroundColor = new Color(0.26f, 0.26f, 0.29f, 0.95f);
        popupCard.style.borderTopLeftRadius = 12;
        popupCard.style.borderTopRightRadius = 12;
        popupCard.style.borderBottomLeftRadius = 12;
        popupCard.style.borderBottomRightRadius = 12;
        popupCard.style.borderLeftWidth = 3;
        popupCard.style.borderRightWidth = 3;
        popupCard.style.borderTopWidth = 3;
        popupCard.style.borderBottomWidth = 3;
        popupCard.style.borderLeftColor = new Color(0.18f, 0.18f, 0.18f, 0.96f);
        popupCard.style.borderRightColor = new Color(0.18f, 0.18f, 0.18f, 0.96f);
        popupCard.style.borderTopColor = new Color(0.18f, 0.18f, 0.18f, 0.96f);
        popupCard.style.borderBottomColor = new Color(0.18f, 0.18f, 0.18f, 0.96f);
        popupCard.style.paddingLeft = 16;
        popupCard.style.paddingRight = 16;
        popupCard.style.paddingTop = 14;
        popupCard.style.paddingBottom = 14;
        popupCard.style.width = popupWidth;
        popupCard.style.maxHeight = popupHeight;
        popupCard.style.flexShrink = 0;
        
        // Create title label with smaller font size for compact popups
        var titleLabel = new Label(title);
        titleLabel.style.fontSize = Mathf.Clamp(screenWidth * 0.03f, 14f, 16f);
        titleLabel.style.color = new Color(1f, 0.84f, 0f, 1f);
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        titleLabel.style.marginBottom = 8;
        titleLabel.style.whiteSpace = WhiteSpace.Normal;
        titleLabel.style.flexWrap = Wrap.Wrap;
        
        // Create message label with smaller font size for compact popups
        var messageLabel = new Label(message);
        messageLabel.style.fontSize = Mathf.Clamp(screenWidth * 0.025f, 11f, 13f);
        messageLabel.style.color = Color.white;
        messageLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        messageLabel.style.whiteSpace = WhiteSpace.Normal;
        messageLabel.style.marginBottom = 6;
        messageLabel.style.flexWrap = Wrap.Wrap;
        
        // Assemble the popup
        popupCard.Add(titleLabel);
        popupCard.Add(messageLabel);
        popupContainer.Add(popupCard);
        rootElement.Add(popupContainer);
        
        Debug.Log($"Popup created with size: {popupWidth}x{popupHeight} on screen: {screenWidth}x{screenHeight}");
        
        // Start with small scale and animate in
        popupCard.style.scale = new Vector2(0.8f, 0.8f);
        popupContainer.style.opacity = 0;
        
        // Animate in immediately
        AnimatePopupIn();
        
        // Schedule close animation
        StartCoroutine(CloseAfterDelay());
    }
    
    private void AnimatePopupIn()
    {
        if (popupContainer == null || popupCard == null) return;
        
        // Animate opacity from 0 to 1
        StartCoroutine(AnimateFloat(0f, 1f, 0.2f, (value) => {
            if (popupContainer != null)
                popupContainer.style.opacity = value;
        }));
        
        // Animate scale from 0.8 to 1
        StartCoroutine(AnimateFloat(0.8f, 1f, 0.3f, (value) => {
            if (popupCard != null)
                popupCard.style.scale = new Vector2(value, value);
        }));
    }
    
    private void AnimatePopupOut()
    {
        if (popupContainer == null || popupCard == null) return;
        
        // Animate opacity from 1 to 0
        StartCoroutine(AnimateFloat(1f, 0f, 0.2f, (value) => {
            if (popupContainer != null)
                popupContainer.style.opacity = value;
        }));
        
        // Animate scale from 1 to 0.8
        StartCoroutine(AnimateFloat(1f, 0.8f, 0.2f, (value) => {
            if (popupCard != null)
                popupCard.style.scale = new Vector2(value, value);
        }, () => {
            // Animation completed callback
            if (popupContainer != null && popupContainer.parent != null)
            {
                popupContainer.parent.Remove(popupContainer);
            }
        }));
    }
    
    private System.Collections.IEnumerator AnimateFloat(float from, float to, float duration, System.Action<float> onUpdate, System.Action onComplete = null)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            // Ease out back approximation
            t = t < 0.5f ? 2f * t * t : -1f + (4f - 2f * t) * t;
            float value = Mathf.Lerp(from, to, t);
            onUpdate?.Invoke(value);
            yield return null;
        }
        onUpdate?.Invoke(to);
        onComplete?.Invoke();
    }
    
    private System.Collections.IEnumerator CloseAfterDelay()
    {
        yield return new WaitForSeconds(showDuration - 0.5f); // Start closing animation 0.5s before destroy
        AnimatePopupOut();
    }
}

// Enhanced success popup class with confetti support
public class EnhancedSuccessPopup : SuccessPopup
{
    private bool showConfetti = false;
    private ParticleSystem confettiSystem;
    
    public void Show(string title, string message, bool withConfetti = false)
    {
        showConfetti = withConfetti;
        
        // Note: UI confetti disabled in favor of world-space confetti effect
        // The main confetti system in ShowConfettiEffect() handles the celebration
        
        // Call base class show method (not self!)
        base.Show(title, message);
    }
    
    private void CreateUIConfetti()
    {
        // This method is now disabled to prevent UI rendering issues
        // The main world-space confetti system provides a better visual effect
        Debug.Log("[Stash] UI confetti creation skipped - using world-space confetti instead");
    }
    
    private void OnDestroy()
    {
        if (confettiSystem != null)
        {
            Destroy(confettiSystem.gameObject);
        }
    }
}

/// <summary>
/// Simple confetti animator that draws confetti directly on UI Toolkit
/// </summary>
public class ConfettiAnimator : MonoBehaviour
{
    private VisualElement container;
    private VisualElement rootElement;
    private List<ConfettiPiece> pieces = new List<ConfettiPiece>();
    private float elapsed = 0f;
    
    private class ConfettiPiece
    {
        public VisualElement element;
        public float startX;
        public float startY;
        public float driftX;
        public float rotationSpeed;
        public float duration;
        public float elapsed;
        public float startDelay;
    }
    
    public void Initialize(VisualElement confettiContainer, VisualElement root)
    {
        container = confettiContainer;
        rootElement = root;
        
        // Create confetti pieces
        Color[] colors = new Color[] 
        { 
            Color.red, Color.blue, Color.green, Color.yellow, 
            Color.magenta, Color.cyan, new Color(1f, 0.5f, 0f) // Orange
        };
        
        int confettiCount = 250;
        for (int i = 0; i < confettiCount; i++)
        {
            VisualElement piece = new VisualElement();
            float size = UnityEngine.Random.Range(4f, 10f);
            piece.style.width = size;
            piece.style.height = size;
            piece.style.backgroundColor = colors[UnityEngine.Random.Range(0, colors.Length)];
            piece.style.position = Position.Absolute;
            
            float startX = UnityEngine.Random.Range(0f, 100f);
            piece.style.left = Length.Percent(startX);
            piece.style.top = 0f;
            
            float rotation = UnityEngine.Random.Range(0f, 360f);
            piece.style.rotate = new Rotate(rotation);
            
            // Initially hide the piece until it starts falling
            piece.style.opacity = 0f;
            
            container.Add(piece);
            
            ConfettiPiece confettiPiece = new ConfettiPiece
            {
                element = piece,
                startX = startX,
                startY = 0f,
                driftX = UnityEngine.Random.Range(-120f, 120f),
                rotationSpeed = UnityEngine.Random.Range(180f, 540f),
                duration = UnityEngine.Random.Range(2.5f, 4f),
                elapsed = 0f,
                startDelay = UnityEngine.Random.Range(0f, 1.5f) // Random delay up to 1.5 seconds
            };
            
            pieces.Add(confettiPiece);
        }
    }
    
    private void Update()
    {
        if (container == null || container.parent == null)
        {
            Destroy(gameObject);
            return;
        }
        
        elapsed += Time.deltaTime;
        
        // Update all confetti pieces
        for (int i = pieces.Count - 1; i >= 0; i--)
        {
            ConfettiPiece piece = pieces[i];
            
            if (piece.element == null || piece.element.parent == null)
            {
                pieces.RemoveAt(i);
                continue;
            }
            
            // Check if this piece should start falling yet
            if (elapsed < piece.startDelay)
            {
                // Still waiting to start, keep it hidden
                continue;
            }
            
            // Show the piece when it starts
            if (piece.elapsed == 0f)
            {
                piece.element.style.opacity = 1f;
            }
            
            // Calculate elapsed time since the piece started (not since creation)
            float animationTime = elapsed - piece.startDelay;
            piece.elapsed = animationTime;
            float t = piece.elapsed / piece.duration;
            
            if (t >= 1f)
            {
                // Remove finished pieces
                if (piece.element.parent != null)
                {
                    piece.element.RemoveFromHierarchy();
                }
                pieces.RemoveAt(i);
                continue;
            }
            
            // Calculate position
            float easedT = 1f - Mathf.Pow(1f - t, 2f); // Ease out
            float currentX = piece.startX + (piece.driftX * easedT);
            float currentY = easedT * 100f; // Fall from top to bottom
            
            piece.element.style.left = Length.Percent(currentX);
            piece.element.style.top = Length.Percent(currentY);
            
            // Update rotation
            float currentRotation = piece.elapsed * piece.rotationSpeed;
            piece.element.style.rotate = new Rotate(currentRotation);
            
            // Fade out near the end
            if (t > 0.7f)
            {
                float fadeT = (t - 0.7f) / 0.3f;
                piece.element.style.opacity = 1f - fadeT;
            }
        }
        
        // Clean up if all pieces are done (account for max delay + duration)
        if (pieces.Count == 0 && elapsed > 6f)
        {
            if (container != null && container.parent != null)
            {
                container.RemoveFromHierarchy();
            }
            Destroy(gameObject);
        }
    }
    
    private void OnDestroy()
    {
        if (container != null && container.parent != null)
        {
            container.RemoveFromHierarchy();
        }
    }
}

/// <summary>
/// Simple toast notification that shows page load time at the top of the screen
/// </summary>
public class LoadTimeToast : MonoBehaviour
{
    private VisualElement toastContainer;
    private VisualElement rootElement;
    private float showDuration = 2.5f;
    
    public void SetRootElement(VisualElement root)
    {
        rootElement = root;
    }
    
    public void Show(double loadTimeMs)
    {
        if (rootElement == null)
        {
            Debug.LogError("[LoadTimeToast] No root element set");
            Destroy(gameObject);
            return;
        }
        
        CreateToast(loadTimeMs);
        
        // Auto destroy after duration
        Destroy(gameObject, showDuration);
    }
    
    private void CreateToast(double loadTimeMs)
    {
        // Create toast container at the top of screen
        toastContainer = new VisualElement();
        toastContainer.name = "LoadTimeToast";
        toastContainer.style.position = Position.Absolute;
        toastContainer.style.top = 60; // Below header bar
        toastContainer.style.left = Length.Percent(50);
        toastContainer.style.translate = new Translate(Length.Percent(-50), 0);
        toastContainer.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.95f);
        toastContainer.style.borderTopLeftRadius = 8;
        toastContainer.style.borderTopRightRadius = 8;
        toastContainer.style.borderBottomLeftRadius = 8;
        toastContainer.style.borderBottomRightRadius = 8;
        toastContainer.style.paddingLeft = 16;
        toastContainer.style.paddingRight = 16;
        toastContainer.style.paddingTop = 8;
        toastContainer.style.paddingBottom = 8;
        toastContainer.style.minWidth = 150;
        toastContainer.style.alignItems = Align.Center;
        toastContainer.pickingMode = PickingMode.Ignore;
        
        // Add border
        toastContainer.style.borderLeftWidth = 2;
        toastContainer.style.borderRightWidth = 2;
        toastContainer.style.borderTopWidth = 2;
        toastContainer.style.borderBottomWidth = 2;
        toastContainer.style.borderLeftColor = new Color(0.3f, 0.7f, 1f, 0.6f);
        toastContainer.style.borderRightColor = new Color(0.3f, 0.7f, 1f, 0.6f);
        toastContainer.style.borderTopColor = new Color(0.3f, 0.7f, 1f, 0.6f);
        toastContainer.style.borderBottomColor = new Color(0.3f, 0.7f, 1f, 0.6f);
        
        // Create label with load time
        string message = $"Rendered in {loadTimeMs:F0}ms";
        Label toastLabel = new Label(message);
        toastLabel.style.color = Color.white;
        toastLabel.style.fontSize = 13;
        toastLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        toastLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        
        toastContainer.Add(toastLabel);
        rootElement.Add(toastContainer);
        
        // Start with slight offset and fade in
        toastContainer.style.opacity = 0;
        toastContainer.style.top = 50;
        
        // Animate in
        StartCoroutine(AnimateToastIn());
    }
    
    private System.Collections.IEnumerator AnimateToastIn()
    {
        float elapsed = 0f;
        float duration = 0.3f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            if (toastContainer != null)
            {
                toastContainer.style.opacity = t;
                toastContainer.style.top = Mathf.Lerp(50, 60, t);
            }
            
            yield return null;
        }
        
        if (toastContainer != null)
        {
            toastContainer.style.opacity = 1;
            toastContainer.style.top = 60;
        }
        
        // Hold for a moment, then fade out
        yield return new WaitForSeconds(showDuration - 0.6f);
        StartCoroutine(AnimateToastOut());
    }
    
    private System.Collections.IEnumerator AnimateToastOut()
    {
        float elapsed = 0f;
        float duration = 0.3f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            if (toastContainer != null)
            {
                toastContainer.style.opacity = 1 - t;
                toastContainer.style.top = Mathf.Lerp(60, 50, t);
            }
            
            yield return null;
        }
        
        if (toastContainer != null && toastContainer.parent != null)
        {
            toastContainer.RemoveFromHierarchy();
        }
    }
    
    private void OnDestroy()
    {
        if (toastContainer != null && toastContainer.parent != null)
        {
            toastContainer.RemoveFromHierarchy();
        }
    }
}

/// <summary>
/// Simple toast notification for showing popup callback messages
/// </summary>
public class SimpleToast : MonoBehaviour
{
    private VisualElement toastContainer;
    private VisualElement rootElement;
    private float showDuration = 3f;
    
    public void SetRootElement(VisualElement root)
    {
        rootElement = root;
    }
    
    public void Show(string title, string message)
    {
        if (rootElement == null)
        {
            // Try to find root element
            var storeUIController = FindObjectOfType<StashStoreUIController>();
            if (storeUIController != null)
            {
                var uiDocument = storeUIController.GetComponent<UIDocument>();
                if (uiDocument != null)
                {
                    rootElement = uiDocument.rootVisualElement;
                }
            }
        }
        
        if (rootElement == null)
        {
            Debug.LogError("[SimpleToast] No root element set");
            Destroy(gameObject);
            return;
        }
        
        CreateToast(title, message);
        
        // Auto destroy after duration
        Destroy(gameObject, showDuration);
    }
    
    private void CreateToast(string title, string message)
    {
        // Create toast container at the bottom center of screen
        toastContainer = new VisualElement();
        toastContainer.name = "SimpleToast";
        toastContainer.style.position = Position.Absolute;
        toastContainer.style.bottom = 100;
        toastContainer.style.left = Length.Percent(50);
        toastContainer.style.translate = new Translate(Length.Percent(-50), 0);
        toastContainer.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.95f);
        toastContainer.style.borderTopLeftRadius = 8;
        toastContainer.style.borderTopRightRadius = 8;
        toastContainer.style.borderBottomLeftRadius = 8;
        toastContainer.style.borderBottomRightRadius = 8;
        toastContainer.style.paddingLeft = 16;
        toastContainer.style.paddingRight = 16;
        toastContainer.style.paddingTop = 12;
        toastContainer.style.paddingBottom = 12;
        toastContainer.style.minWidth = 200;
        toastContainer.style.maxWidth = 300;
        toastContainer.style.alignItems = Align.Center;
        toastContainer.style.flexDirection = FlexDirection.Column;
        toastContainer.pickingMode = PickingMode.Ignore;
        
        // Add border
        toastContainer.style.borderLeftWidth = 2;
        toastContainer.style.borderRightWidth = 2;
        toastContainer.style.borderTopWidth = 2;
        toastContainer.style.borderBottomWidth = 2;
        Color grayBorderColor = new Color(0.6f, 0.6f, 0.6f, 0.8f);
        toastContainer.style.borderLeftColor = grayBorderColor;
        toastContainer.style.borderRightColor = grayBorderColor;
        toastContainer.style.borderTopColor = grayBorderColor;
        toastContainer.style.borderBottomColor = grayBorderColor;
        
        // Create title label
        Label titleLabel = new Label(title);
        titleLabel.style.color = new Color(0.8f, 0.8f, 0.8f, 1f);
        titleLabel.style.fontSize = 14;
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        titleLabel.style.marginBottom = 4;
        
        // Create message label
        Label messageLabel = new Label(message);
        messageLabel.style.color = Color.white;
        messageLabel.style.fontSize = 12;
        messageLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        messageLabel.style.whiteSpace = WhiteSpace.Normal;
        messageLabel.style.flexWrap = Wrap.Wrap;
        
        toastContainer.Add(titleLabel);
        toastContainer.Add(messageLabel);
        rootElement.Add(toastContainer);
        
        // Start with slight offset and fade in
        toastContainer.style.opacity = 0;
        toastContainer.style.bottom = 80;
        
        // Animate in
        StartCoroutine(AnimateToastIn());
    }
    
    private System.Collections.IEnumerator AnimateToastIn()
    {
        float elapsed = 0f;
        float duration = 0.3f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            if (toastContainer != null)
            {
                toastContainer.style.opacity = t;
                toastContainer.style.bottom = Mathf.Lerp(80, 100, t);
            }
            
            yield return null;
        }
        
        if (toastContainer != null)
        {
            toastContainer.style.opacity = 1;
            toastContainer.style.bottom = 100;
        }
        
        // Hold for a moment, then fade out
        yield return new WaitForSeconds(showDuration - 0.6f);
        StartCoroutine(AnimateToastOut());
    }
    
    private System.Collections.IEnumerator AnimateToastOut()
    {
        float elapsed = 0f;
        float duration = 0.3f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            if (toastContainer != null)
            {
                toastContainer.style.opacity = 1 - t;
                toastContainer.style.bottom = Mathf.Lerp(100, 80, t);
            }
            
            yield return null;
        }
        
        if (toastContainer != null && toastContainer.parent != null)
        {
            toastContainer.RemoveFromHierarchy();
        }
    }
    
    private void OnDestroy()
    {
        if (toastContainer != null && toastContainer.parent != null)
        {
            toastContainer.RemoveFromHierarchy();
        }
    }
}
}