using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using StashPopup;
using System;
using System.Threading.Tasks;
using System.Collections;
using Stash.Samples;

namespace Stash.Samples
{
    /// <summary>
    /// Main store UI controller: store list, buy buttons, and channel selection UI.
    /// Checkout and verification are handled by <see cref="Stash.Samples.StoreCheckoutService"/>; this class only wires UI and callbacks.
    /// </summary>
    public class StashStoreUIController : MonoBehaviour
    {
        [SerializeField] private UIDocument storeUIDocument;
        [SerializeField] private Texture2D[] itemImages;

        [Header("Store Items")]
        [SerializeField] private List<StoreItem> storeItems = new List<StoreItem>();

        [Header("Stash Configuration")]
        [SerializeField] private string defaultApiKey = "zyIbbfvO1ZRTaDt1VBZ5CJrwrdzyfDyLgt-VWNT-1uWj-5h42aeB6BNGAl8MGImw";
        [SerializeField] private string defaultChannelSelectionUrl = "https://store.howlingwoods.shop/pay/channel-selection";
        [SerializeField] private StashDemoEnvironment defaultEnvironment = StashDemoEnvironment.Test;
        
        [Header("Shop Configuration")]
        [SerializeField] private string currency = DemoAppConstants.DEFAULT_CURRENCY;

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
            settingsManager = new StoreSettingsManager(root, defaultEnvironment, defaultApiKey, defaultChannelSelectionUrl);
            settingsManager.Initialize();
            StashPayCard.Instance.OnPageLoaded += OnPageLoaded;
            StashPayCard.Instance.OnNetworkError += OnStashPayNetworkError;
            ValidateAndInitializeStoreItems();
            UpdateUIFromStoreItems();
            SetupChannelSelectionButton();
        }

        private void ValidateAndInitializeStoreItems()
        {
            if (storeItems.Count == 0)
                InitializeDefaultStoreItems();
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
    /// Opens the payment channel selection modal.
    /// Displays a centered modal for users to choose their preferred payment method.
    /// </summary>
    public void OpenPaymentChannelSelection()
    {
        try
        {
            Stash.Samples.StoreCheckoutService.OpenChannelSelection(
                settingsManager.ChannelSelectionUrl,
                settingsManager.GetCurrentModalConfig(),
                OnChannelSelectionOptinResponse,
                () => ShowToast("Connection Error", "Check your connection and try again."),
                onDismiss: null);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[StoreUI] Exception opening payment channel selection: {ex.Message}\nStackTrace: {ex.StackTrace}");
        }
    }

    private void OnChannelSelectionOptinResponse(string optinType)
    {
        Debug.Log($"[StoreUI] User selected: {optinType}");
        string normalized = (optinType ?? "").ToUpper();
        if (normalized == "STASH_PAY")
            ShowToast("Stash Pay", "Stash Pay selected.");
        else
            ShowToast("Selection", $"Selected: {optinType}");
    }
    
    private void ShowToast(string title, string message)
    {
        UINotificationSystem.ShowToast(title, message, 3f, root);
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

        // Disable the buy button to prevent multiple checkouts
        SetButtonEnabled(buyButtons[itemIndex], false);
        SetButtonLoadingState(buyButtons[itemIndex], true);
        NavigationBlocker.Instance.BlockNavigation();
        OpenStashCheckout(itemIndex);
    }
    
    private async void OpenStashCheckout(int itemIndex)
    {
        try
        {
            StoreItem item = storeItems[itemIndex];
            string userId;
            string email;
            if (AuthenticationManager.Instance != null && AuthenticationManager.Instance.IsAuthenticated())
            {
                UserData userData = AuthenticationManager.Instance.GetUserData();
                userId = userData.UserId;
                email = userData.Email;
            }
            else
            {
                userId = $"guest_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
                email = $"guest_{Guid.NewGuid().ToString("N").Substring(0, 8)}@example.com";
            }

            var result = await Stash.Samples.StoreCheckoutService.GetCheckoutUrlAsync(
                item.id,
                item.name,
                item.description,
                item.pricePerItem,
                1,
                item.imageUrl,
                userId,
                email,
                currency,
                settingsManager.ApiKey,
                settingsManager.Environment);

            if (result == null)
            {
                SetButtonLoadingState(buyButtons[itemIndex], false);
                HandleFailedPurchase(itemIndex);
                return;
            }

            currentCheckoutId = result.Value.checkoutId;
            currentItemIndex = itemIndex;

            Stash.Samples.StoreCheckoutService.OpenCheckout(
                result.Value.url,
                settingsManager.UseSafariWebView,
                OnPaymentSuccessDetected,
                OnPaymentFailureDetected,
                OnBrowserClosed);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Stash] Error generating checkout URL: {ex.Message}");
            SetButtonLoadingState(buyButtons[itemIndex], false);
            HandleFailedPurchase(itemIndex);
        }
    }

    private async void OnBrowserClosed()
    {
        SetButtonEnabled(buyButtons[currentItemIndex], true);
        NavigationBlocker.Instance.UnblockNavigation();

        if (string.IsNullOrEmpty(currentCheckoutId))
        {
            HandleFailedPurchase(currentItemIndex);
            SetButtonLoadingState(buyButtons[currentItemIndex], false);
            return;
        }

        var verification = await Stash.Samples.StoreCheckoutService.VerifyPurchaseAsync(
            currentCheckoutId,
            settingsManager.ApiKey,
            settingsManager.Environment);

        if (verification == null)
        {
            HandleFailedPurchase(currentItemIndex);
            SetButtonLoadingState(buyButtons[currentItemIndex], false);
            return;
        }

        var v = verification.Value;
        if (v.Success)
        {
            HandleSuccessfulPurchase(currentItemIndex);
            string message = $"Your purchase of {v.ItemName} has been completed successfully!";
            if (!string.IsNullOrEmpty(v.Total) && !string.IsNullOrEmpty(v.Currency))
                message += $"\nAmount: {v.Total} {v.Currency}";
            if (!string.IsNullOrEmpty(v.Tax) && v.Tax != "0")
                message += $"\nTax: {v.Tax} {v.Currency}";
            if (!string.IsNullOrEmpty(v.TimeMillis))
            {
                try
                {
                    long ms = long.Parse(v.TimeMillis);
                    message += $"\nDate: {DateTimeOffset.FromUnixTimeMilliseconds(ms).DateTime.ToLocalTime():g}";
                }
                catch { }
            }
            UINotificationSystem.ShowPopup("Purchase Successful", message, 3f, root);
            if (storeItems != null && currentItemIndex >= 0 && currentItemIndex < storeItems.Count)
                OnPurchaseCompleted?.Invoke(storeItems[currentItemIndex].id, true);
        }
        else
        {
            HandleFailedPurchase(currentItemIndex);
            if (storeItems != null && currentItemIndex >= 0 && currentItemIndex < storeItems.Count)
                OnPurchaseCompleted?.Invoke(storeItems[currentItemIndex].id, false);
        }

        SetButtonLoadingState(buyButtons[currentItemIndex], false);
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
            if (StashPayCard.Instance == null) return;
            StashPayCard.Instance.OnPageLoaded -= OnPageLoaded;
            StashPayCard.Instance.OnNetworkError -= OnStashPayNetworkError;
        }
    
    private void OnStashPayNetworkError()
    {
        if (currentItemIndex >= 0 && buyButtons != null && currentItemIndex < buyButtons.Count)
        {
            SetButtonLoadingState(buyButtons[currentItemIndex], false);
            SetButtonEnabled(buyButtons[currentItemIndex], true);
            NavigationBlocker.Instance?.UnblockNavigation();
        }
        ShowToast("Connection Error", "Check your connection and try again.");
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
    
    }
}