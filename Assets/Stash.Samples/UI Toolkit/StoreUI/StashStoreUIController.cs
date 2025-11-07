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
    [SerializeField] private string apiKey = "your-api-key-here";
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
    
    // Settings popup elements
    private VisualElement settingsPopup;
    private Toggle safariWebViewToggle;
    private Button settingsButton;
    private Button settingsPopupCloseButton;
    
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
        
        // Initialize IAP Manager for Apple Pay functionality
        InitializeIAP();
        
        // Setup settings popup
        SetupSettingsPopup();
        
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
        safariWebViewToggle = root.Q<Toggle>("safari-webview-toggle");
        settingsPopupCloseButton = root.Q<Button>("settings-popup-close-button");
        
        if (settingsPopup != null)
        {
            settingsPopup.visible = false;
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
        
        if (settingsPopupCloseButton != null)
        {
            settingsPopupCloseButton.clicked += HideSettingsPopup;
        }
        else
        {
            Debug.LogWarning("[StoreUI] Could not find settings-popup-close-button");
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
    
    private void OnSafariToggleChanged(ChangeEvent<bool> evt)
    {
        useSafariWebView = evt.newValue;
        Debug.Log($"[StoreUI] Safari WebView mode changed to: {useSafariWebView}");
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
        // Payment channel selection popup opening
        
        try
        {
            // Open the payment channel selection URL in a centered popup
            StashPayCard.Instance.OpenPopup("https://store.howlingwoods.shop/pay/channel-selection");
            Debug.Log("[StoreUI] Payment channel selection popup opened");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[StoreUI] Exception opening payment channel selection: {ex.Message}\nStackTrace: {ex.StackTrace}");
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
            Debug.LogWarning("[StoreUI] ⚠️ SimpleIAPManager not ready yet");
            Debug.LogWarning("[StoreUI] This usually means Unity IAP is still initializing or failed to initialize");
            ShowIAPErrorMessage("In-app purchases are still loading. Please wait a moment and try again.");
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
        
        Debug.Log($"Processing purchase with Stash for item: {item.id} at price: {item.pricePerItem}");
        
        // Disable the buy button to prevent multiple checkouts
        SetButtonEnabled(buyButtons[itemIndex], false);
        
        // Display a loading indicator
        SetButtonLoadingState(buyButtons[itemIndex], true);

        // Block navigation during purchase
        NavigationBlocker.Instance.BlockNavigation();
        
        // Open Stash popup for checkout
        OpenStashCheckout(itemIndex);
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
            // Only override ForceWebBasedCheckout if user has explicitly enabled it
            // This respects remote Flagsmith configuration while allowing local override
            if (useSafariWebView)
            {
                StashPayCard.Instance.ForceWebBasedCheckout = true;
            }
            
            StashPayCard.Instance.OpenURL(url, () => OnBrowserClosed(), () => OnPaymentSuccessDetected(), () => OnPaymentFailureDetected());
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
            
            // Show enhanced success popup with confetti
            ShowSuccessPopup(
                currentItem.name,
                currency,
                currentItem.pricePerItem,
                "0", // No tax info available at this point
                DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString()
            );
            
            // Also show confetti effect
            ShowSuccessPopupWithConfetti("Payment successful!", "Your purchase has been completed successfully!");
            
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
            // Create a simple success popup without confetti to avoid particle system issues
            var successPopup = new GameObject("SuccessPopup");
            var popupScript = successPopup.AddComponent<SuccessPopup>();
            
            // Set the root element directly
            if (root != null)
            {
                popupScript.SetRootElement(root);
            }
            
            popupScript.Show(title, message);
            
            Debug.Log($"[Stash] Success popup shown: {title} - {message}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Stash] Error showing success popup: {ex.Message}");
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
}