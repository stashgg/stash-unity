using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using StashPopup;
using Stash.Webshop;
using System;
using System.Threading.Tasks;
using System.Collections;
using UnityEngine.Networking;
using Button = UnityEngine.UIElements.Button;
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
        [SerializeField] private string defaultApiKey = "zyIbbfvO1ZRTaDt1VBZ5CJrwrdzyfDyLgt-VWNT-1uWj-5h42aeB6BNGAl8MGImw";
        [SerializeField] private string defaultChannelSelectionUrl = "https://store.howlingwoods.shop/pay/channel-selection";
        [SerializeField] private StashEnvironment defaultEnvironment = StashEnvironment.Test;
        
        [Header("Shop Configuration")]
        [SerializeField] private string currency = "USD";

        private VisualElement root;
        private List<Button> buyButtons = new List<Button>();
        private StoreSettingsManager settingsManager;
        
        // Delegate for purchase callbacks
        public delegate void PurchaseCompletedDelegate(string itemId, bool success);
        public event PurchaseCompletedDelegate OnPurchaseCompleted;
        
        // Store the current checkout ID for verification
        private string currentCheckoutId;
        private int currentItemIndex;

        private void Start()
    {
        root = storeUIDocument.rootVisualElement;
        
        // Initialize settings manager
        settingsManager = new StoreSettingsManager(root, defaultEnvironment, defaultApiKey, defaultChannelSelectionUrl);
        settingsManager.Initialize();
        
        // Initialize IAP Manager
        InitializeIAP();
        
        // Subscribe to page load events
        StashPayCard.Instance.OnPageLoaded += OnPageLoaded;
        
        // Initialize native exception logger
        InitializeNativeExceptionLogger();
        
        // Initialize store items and UI
        ValidateAndInitializeStoreItems();
        UpdateUIFromStoreItems();
        
        // Setup channel selection button
        SetupChannelSelectionButton();
        
        // Setup Galleon checkout button
        SetupGalleonCheckoutButton();
    }
    
    private void InitializeNativeExceptionLogger()
    {
        // Check if logger already exists
        NativeExceptionLogger existingLogger = FindObjectOfType<NativeExceptionLogger>();
        if (existingLogger == null)
        {
            // Create logger GameObject
            GameObject loggerGO = new GameObject("NativeExceptionLogger");
            NativeExceptionLogger logger = loggerGO.AddComponent<NativeExceptionLogger>();
            
            // Set UI document reference
            var loggerUIDocument = loggerGO.AddComponent<UIDocument>();
            loggerUIDocument.panelSettings = storeUIDocument.panelSettings;
            logger.UIDocument = loggerUIDocument;
        }
    }
    private void InitializeIAP()
    {
        if (SimpleIAPManager.Instance == null)
        {
            GameObject iapManager = new GameObject("SimpleIAPManager");
            iapManager.AddComponent<SimpleIAPManager>();
            DontDestroyOnLoad(iapManager);
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
    
    private void SetupGalleonCheckoutButton()
    {
        // Setup Galleon checkout button
        Button galleonCheckoutButton = root.Q<Button>("galleon-checkout-button");

        if (galleonCheckoutButton != null)
        {
            galleonCheckoutButton.clicked += OpenGalleonCheckout;
            Debug.Log("[StoreUI] Galleon checkout button setup complete");
        }
        else
        {
            Debug.LogWarning("[StoreUI] Could not find galleon-checkout-button in UI");
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
            PopupSizeConfig? sizeToUse = settingsManager.GetCurrentPopupSizeFromInputs();
            
            // Register opt-in response callback
            StashPayCard.Instance.OnOptinResponse += OnChannelSelectionOptinResponse;
            
            // Open the payment channel selection URL in a centered popup (using configured URL)
            StashPayCard.Instance.OpenPopup(settingsManager.ChannelSelectionUrl,
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

    private void OnChannelSelectionOptinResponse(string optinType)
    {
        Debug.Log($"[StoreUI] User selected payment method: {optinType}");
        
        // Normalize to uppercase format (handles both "stash_pay" and "STASH_PAY")
        string normalizedType = optinType.ToUpper();
        
        // Update payment method preference based on user selection
            if (normalizedType == DemoAppConstants.PAYMENT_METHOD_STASH_PAY || normalizedType == DemoAppConstants.PAYMENT_METHOD_NATIVE_IAP)
            {
                // Payment method is managed by StoreSettingsManager, update will be handled there
                string displayName = normalizedType == DemoAppConstants.PAYMENT_METHOD_NATIVE_IAP ? "Native IAP" : "Stash Pay";
            ShowToast("Payment Method Selected", $"You selected: {displayName}");
        }
        else
        {
            Debug.LogWarning($"[StoreUI] Unknown payment method selected: {optinType}");
            ShowToast("Unknown Selection", $"Received unknown payment method: {optinType}");
        }
    }
    
    private void ShowToast(string title, string message)
    {
        UINotificationSystem.ShowToast(title, message, 3f, root);
    }

    private void ProcessIAPPurchase(int itemIndex)
    {
        if (storeItems == null || itemIndex < 0 || itemIndex >= storeItems.Count)
        {
            Debug.LogError($"[StoreUI] Invalid item index in ProcessIAPPurchase: {itemIndex}! Store items count: {storeItems?.Count ?? 0}");
            return;
        }

        StoreItem item = storeItems[itemIndex];
        
        // Check if IAP Manager is ready
        if (SimpleIAPManager.Instance == null)
        {
            Debug.LogError("[StoreUI] SimpleIAPManager instance is null");
            ShowIAPErrorMessage("In-app purchases are not available. Please restart the app and try again.");
            OnPurchaseCompleted?.Invoke(item.id, false);
            return;
        }
        
        if (!SimpleIAPManager.Instance.IsReady())
        {
            if (SimpleIAPManager.Instance.IsInitializing())
            {
                ShowIAPErrorMessage("In-app purchases are still loading.\nPlease wait a moment and try again.");
            }
            else
            {
                Debug.LogError("[StoreUI] IAP initialization failed");
                ShowIAPErrorMessage("Native IAP failed to initialize.\nPlease use sandbox account for native purchases.");
            }
            
            OnPurchaseCompleted?.Invoke(item.id, false);
            return;
        }
        
        // Subscribe to purchase events temporarily
        System.Action<string> onSuccess = null;
        System.Action<string, string> onFailure = null;
        
        onSuccess = (productId) => {
            if (productId == item.id)
            {
                UINotificationSystem.ShowPopup("Purchase Successful!", $"You successfully purchased {item.name}!", 3f, root);
                ConfettiEffect.Create(root);
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
        SimpleIAPManager.Instance.BuyProduct(item.id);
    }
    

    
    private void ShowIAPErrorMessage(string errorMessage)
    {
        UINotificationSystem.ShowPopup("Purchase Error", errorMessage, 3f, root);
    }

    private void ProcessPurchase(int itemIndex)
    {
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
        if (settingsManager.PaymentMethod == DemoAppConstants.PAYMENT_METHOD_NATIVE_IAP)
        {
            ProcessIAPPurchase(itemIndex);
        }
        else // STASH_PAY
        {
            
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
            }

            // Build request body matching the working format from StashPaySample.cs
            string platformString = Application.platform == RuntimePlatform.IPhonePlayer ? "IOS" :
                                    Application.platform == RuntimePlatform.Android ? "ANDROID" : "ANDROID";
            
            var requestBody = new CheckoutRequest
            {
                regionCode = "USA", // ISO 3166-1 Alpha-3 format
                currency = currency,
                item = new CheckoutRequestItem
                {
                    id = item.id,
                    pricePerItem = item.pricePerItem,
                    quantity = 1,
                    imageUrl = item.imageUrl,
                    name = item.name,
                    description = item.description
                },
                user = new CheckoutRequestUser
                {
                    id = userId,
                    validatedEmail = email,
                    regionCode = "US", // ISO 3166-1 Alpha-2 format
                    platform = platformString
                }
            };

            string apiUrl = $"{settingsManager.Environment.GetRootUrl()}/sdk/server/checkout_links/generate_quick_pay_url";

            var requestJson = JsonUtility.ToJson(requestBody);

            var headers = new List<Stash.Models.RequestHeader>
            {
                new Stash.Models.RequestHeader
                {
                    Key = "X-Stash-Api-Key",
                    Value = settingsManager.ApiKey
                }
            };

            // Make the API POST request directly using RestClient
            var response = await Stash.Webshop.RestClient.Post(apiUrl, requestJson, headers);

            if (response.StatusCode == 200)
            {
                // Try to parse checkout URL and ID from response
                string url = null;
                string id = null;

                try
                {
                    // Expected response: { "url":"...", "id":"..." }
                    // We'll use a simple wrapper for parsing
                    CheckoutLinkResponse parsed = JsonUtility.FromJson<CheckoutLinkResponse>(response.Data);
                    url = parsed.url;
                    id = parsed.id;
                }
                catch (Exception parseEx)
                {
                    Debug.LogError($"[Stash] Failed to parse checkout response: {parseEx.Message}. Full data: {response.Data}");
                    throw;
                }

                // Store the checkout ID and item index for verification later
                currentCheckoutId = id;
                currentItemIndex = itemIndex;

                // Open the checkout URL in the StashPayCard
                // Apply user's preference for Web View mode
                StashPayCard.Instance.ForceWebBasedCheckout = settingsManager.UseSafariWebView;

                StashPayCard.Instance.OpenCheckout(url, () => OnBrowserClosed(), () => OnPaymentSuccessDetected(), () => OnPaymentFailureDetected());
            }
            else
            {
                string errorDetails = !string.IsNullOrEmpty(response.Error) 
                    ? response.Error 
                    : $"HTTP {response.StatusCode}";
                
                if (!string.IsNullOrEmpty(response.Data))
                {
                    errorDetails += $". Response: {response.Data}";
                }
                
                Debug.LogError($"[Stash] Error generating checkout URL: {errorDetails}");
                SetButtonLoadingState(buyButtons[itemIndex], false);
                HandleFailedPurchase(itemIndex);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Stash] Error generating checkout URL: {ex.Message}");
            // Reset button state on error
            SetButtonLoadingState(buyButtons[itemIndex], false);
            HandleFailedPurchase(itemIndex);
        }
    }

    [Serializable]
    private class CheckoutRequest
    {
        public string regionCode;
        public string currency;
        public CheckoutRequestItem item;
        public CheckoutRequestUser user;
    }
    
    [Serializable]
    private class CheckoutRequestItem
    {
        public string id;
        public string pricePerItem;
        public int quantity;
        public string imageUrl;
        public string name;
        public string description;
    }
    
    [Serializable]
    private class CheckoutRequestUser
    {
        public string id;
        public string validatedEmail;
        public string regionCode;
        public string platform;
    }
    
    [Serializable]
    private class CheckoutLinkResponse
    {
        public string url;
        public string id;
    }
    
    
    
    private void OnBrowserClosed()
    {
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
        
        using (UnityWebRequest request = UnityWebRequest.PostWwwForm(verifyUrl, ""))
        {
            // Add authorization header
            request.SetRequestHeader("X-Stash-Api-Key", settingsManager.ApiKey);
            
            // Send the request
            yield return request.SendWebRequest();
            
            bool isSuccessful = false;
            PurchaseVerificationResponse purchaseResponse = null;
            
            // First check HTTP response
            if (request.result == UnityWebRequest.Result.Success && request.responseCode == 200)
            {
                // Parse the response to get item details
                string responseText = request.downloadHandler.text;
                
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
                HandleSuccessfulPurchase(currentItemIndex);
                
                // Get item name and payment details
                string itemName = purchaseResponse.items[0].name;
                string message = $"Your purchase of {itemName} has been completed successfully!";
                
                if (!string.IsNullOrEmpty(purchaseResponse.paymentSummary.total) && !string.IsNullOrEmpty(purchaseResponse.currency))
                {
                    message += $"\nAmount: {purchaseResponse.paymentSummary.total} {purchaseResponse.currency}";
                }
                
                if (!string.IsNullOrEmpty(purchaseResponse.paymentSummary.tax) && purchaseResponse.paymentSummary.tax != "0")
                {
                    message += $"\nTax: {purchaseResponse.paymentSummary.tax} {purchaseResponse.currency}";
                }
                
                if (!string.IsNullOrEmpty(purchaseResponse.paymentSummary.timeMillis))
                {
                    try
                    {
                        long milliseconds = long.Parse(purchaseResponse.paymentSummary.timeMillis);
                        DateTime dateTime = DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).DateTime.ToLocalTime();
                        message += $"\nDate: {dateTime:g}";
                    }
                    catch { }
                }
                
                UINotificationSystem.ShowPopup("Purchase Successful", message, 3f, root);
                ConfettiEffect.Create(root);
                
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
        string baseUrl = settingsManager.Environment.GetRootUrl();
        return $"{baseUrl}/sdk/checkout_links/order/{checkoutId}";
    }
    
    private void HandleSuccessfulPurchase(int itemIndex)
    {
        if (storeItems == null || itemIndex < 0 || itemIndex >= storeItems.Count)
        {
            Debug.LogError($"[StoreUI] Invalid item index {itemIndex} in HandleSuccessfulPurchase. Store items count: {storeItems?.Count ?? 0}");
            return;
        }
        
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
            
            UINotificationSystem.ShowPopup("Payment Successful!", $"You successfully purchased {currentItem.name}!", 3f, root);
            ConfettiEffect.Create(root);
            
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
            string message = "Your payment could not be processed.\n\nPlease try again or contact support if the problem persists.";
            
            if (storeItems != null && currentItemIndex >= 0 && currentItemIndex < storeItems.Count)
            {
                string itemName = storeItems[currentItemIndex].name;
                message = $"Payment for {itemName} could not be processed.\n\nPlease try again or contact support if the problem persists.";
            }
            
        UINotificationSystem.ShowPopup("Payment Failed", message, 3f, root);
    }
    
        private void OnDestroy()
    {
        if (StashPayCard.Instance != null)
        {
            StashPayCard.Instance.OnPaymentSuccess -= OnPaymentSuccessDetected;
            StashPayCard.Instance.OnPageLoaded -= OnPageLoaded;
        }
    }
    
    private void OnPageLoaded(double loadTimeMs)
    {
        ShowLoadTimeToast(loadTimeMs);
    }
    
    private void ShowLoadTimeToast(double loadTimeMs)
    {
        if (settingsManager.ShowMetrics)
        {
            UINotificationSystem.ShowLoadTimeToast(loadTimeMs, root);
        }
    }
    
    #region Galleon Checkout
    
    private const string GALLEON_API_BASE_URL = "https://sandbox-stash-bridge.galleon.so";
    private const string GALLEON_BEARER_TOKEN = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJhcHBJZCI6Im1lcmdlY3J1aXNlLnNiLmFwcCIsImlhdCI6MTc2NTkyNDM4OH0.rbXMiE-z3-DXESoKuVGw_8u_9wu_KTQeI-rU75KEddw";
    
    /// <summary>
    /// Opens Galleon checkout by authenticating and getting checkout URL
    /// </summary>
    private async void OpenGalleonCheckout()
    {
        try
        {
            UINotificationSystem.ShowToast("Galleon Checkout", "Authenticating...", 2f, root);
            
            // Step 1: Authenticate to get stripeCustomerId
            string stripeCustomerId = await AuthenticateWithGalleon();
            
            if (string.IsNullOrEmpty(stripeCustomerId))
            {
                UINotificationSystem.ShowPopup("Error", "Failed to authenticate with Galleon API", 3f, root);
                return;
            }
            
            Debug.Log($"[Galleon] Authenticated successfully. Stripe Customer ID: {stripeCustomerId}");
            
            // Step 2: Get checkout link by customer ID
            string checkoutUrl = await GetCheckoutLinkByCustomerId(stripeCustomerId);
            
            if (string.IsNullOrEmpty(checkoutUrl))
            {
                UINotificationSystem.ShowPopup("Error", "Failed to get checkout URL from Galleon API", 3f, root);
                return;
            }
            
            Debug.Log($"[Galleon] Checkout URL received: {checkoutUrl}");
            
            // Step 3: Open checkout in StashPayCard
            StashPayCard.Instance.OpenCheckout(
                checkoutUrl,
                dismissCallback: () => {
                    UINotificationSystem.ShowToast("Dismissed", "Galleon checkout was dismissed", 2f, root);
                },
                successCallback: () => {
                    UINotificationSystem.ShowToast("Success", "Galleon checkout successful!", 3f, root);
                },
                failureCallback: () => {
                    UINotificationSystem.ShowToast("Failure", "Galleon checkout failed", 3f, root);
                }
            );
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Galleon] Error in OpenGalleonCheckout: {ex.Message}\n{ex.StackTrace}");
            UINotificationSystem.ShowPopup("Error", $"Galleon checkout error: {ex.Message}", 3f, root);
        }
    }
    
    /// <summary>
    /// Authenticates with Galleon API and returns stripeCustomerId
    /// </summary>
    private async Task<string> AuthenticateWithGalleon()
    {
        string url = $"{GALLEON_API_BASE_URL}/authenticate";
        
        // Using test payload from documentation
        var requestBody = new GalleonAuthenticateRequest
        {
            app_user_id = "user123"
        };
        
        string jsonBody = JsonUtility.ToJson(requestBody);
        
        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {GALLEON_BEARER_TOKEN}");
            
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var response = JsonUtility.FromJson<GalleonAuthenticateResponse>(request.downloadHandler.text);
                    return response.stripeCustomerId;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Galleon] Failed to parse authenticate response: {ex.Message}. Response: {request.downloadHandler.text}");
                    return null;
                }
            }
            else
            {
                Debug.LogError($"[Galleon] Authenticate request failed: {request.error}. Status: {request.responseCode}. Response: {request.downloadHandler?.text}");
                return null;
            }
        }
    }
    
    /// <summary>
    /// Gets checkout link by customer ID from Galleon API
    /// </summary>
    private async Task<string> GetCheckoutLinkByCustomerId(string stripeCustomerId)
    {
        string url = $"{GALLEON_API_BASE_URL}/checkout-link-by-customer-id";
        
        // Using test payload from documentation
        var requestBody = new GalleonCheckoutLinkRequest
        {
            stripe_customer_id = stripeCustomerId,
            amount = 29.99,
            currency = "USD",
            payer_ip = "192.168.1.1",
            session_ttl_seconds = 900,
            metadata = new GalleonCheckoutMetadata
            {
                transactionId = "123123",
                platform = "google_dtc"
            },
            description = "Premium Subscription",
            setup_future_usage = "off_session",
            sku = "SKU-PREMIUM",
            preview = new GalleonCheckoutPreview
            {
                title = "Premium Subscription",
                description = "Unlock all premium features",
                image_url = "https://example.com/premium.png"
            },
            return_deeplink = "myapp://payment-complete",
            back_button_enabled = true,
            country = "US"
        };
        
        string jsonBody = JsonUtility.ToJson(requestBody);
        
        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {GALLEON_BEARER_TOKEN}");
            
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var response = JsonUtility.FromJson<GalleonCheckoutLinkResponse>(request.downloadHandler.text);
                    return response.checkout_url;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Galleon] Failed to parse checkout link response: {ex.Message}. Response: {request.downloadHandler.text}");
                    return null;
                }
            }
            else
            {
                Debug.LogError($"[Galleon] Checkout link request failed: {request.error}. Status: {request.responseCode}. Response: {request.downloadHandler?.text}");
                return null;
            }
        }
    }
    
    // Galleon API request/response classes
    [Serializable]
    private class GalleonAuthenticateRequest
    {
        public string app_user_id;
    }
    
    [Serializable]
    private class GalleonAuthenticateResponse
    {
        public string stripeCustomerId;
    }
    
    [Serializable]
    private class GalleonCheckoutLinkRequest
    {
        public string stripe_customer_id;
        public double amount;
        public string currency;
        public string payer_ip;
        public int session_ttl_seconds;
        public GalleonCheckoutMetadata metadata;
        public string description;
        public string setup_future_usage;
        public string sku;
        public GalleonCheckoutPreview preview;
        public string return_deeplink;
        public bool back_button_enabled;
        public string country;
    }
    
    [Serializable]
    private class GalleonCheckoutMetadata
    {
        public string transactionId;
        public string platform;
    }
    
    [Serializable]
    private class GalleonCheckoutPreview
    {
        public string title;
        public string description;
        public string image_url;
    }
    
    [Serializable]
    private class GalleonCheckoutLinkResponse
    {
        public string checkout_url;
    }
    
    #endregion
    }
}