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
    [SerializeField] private string externalUserId = "user123";
    [SerializeField] private string userEmail = "example@example.com";
    [SerializeField] private string displayName = "Example User";
    [SerializeField] private string avatarIconUrl = "";
    [SerializeField] private string profileUrl = "";
    
    [Header("Shop Configuration")]
    [SerializeField] private string shopHandle = "demo-shop";
    [SerializeField] private string currency = "USD";

    private VisualElement root;
    private List<Button> buyButtons = new List<Button>();
    
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
        
        // Ensure we have the right number of store items defined based on the UI
        ValidateAndInitializeStoreItems();
        
        // Setup store UI elements
        UpdateUIFromStoreItems();
        
        // Make sure StashPayCard exists in the scene
        StashPayCard.Instance.gameObject.name = "StashPayCard";
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
            
            // Create the image element
            VisualElement imageElement = new VisualElement();
            imageElement.name = $"item-{i+1}-image";
            imageElement.AddToClassList("item-image");
            
            // Set image if available
            if (itemImages != null && i < itemImages.Length && itemImages[i] != null)
            {
                imageElement.style.backgroundImage = new StyleBackground(itemImages[i]);
            }
            
            // Create the footer
            VisualElement footerElement = new VisualElement();
            footerElement.name = $"item-{i+1}-footer";
            footerElement.AddToClassList("item-footer");
            
            // Create the name label
            Label nameLabel = new Label(storeItem.name);
            nameLabel.name = $"item-{i+1}-name";
            nameLabel.AddToClassList("item-name");
            
            // Create the price label
            Label priceLabel = new Label("$" + storeItem.pricePerItem);
            priceLabel.name = $"item-{i+1}-price";
            priceLabel.AddToClassList("item-price");
            
            // Create buy button
            Button buyButton = new Button();
            buyButton.name = $"buy-button-{i+1}";
            buyButton.text = "BUY";
            buyButton.AddToClassList("buy-button");
            
            // Add click handler
            buyButton.clicked += () => ProcessPurchase(itemIndex);
            
            // Add to tracking list
            buyButtons.Add(buyButton);
            
            // Assemble the item
            footerElement.Add(nameLabel);
            footerElement.Add(priceLabel);
            footerElement.Add(buyButton);
            
            itemContainer.Add(imageElement);
            itemContainer.Add(footerElement);
            
            // Add to container
            itemsContainer.Add(itemContainer);
        }
    }
    
    private void InitializeDefaultStoreItems()
    {
        // Add default store items only if none were defined in the editor
        storeItems.Add(new StoreItem {
            id = "premium_sword",
            name = "Item 1",
            description = "Description 1",
            pricePerItem = "4.99",
            imageUrl = ""
        });
        
        storeItems.Add(new StoreItem {
            id = "health_potion",
            name = "Item 2",
            description = "Description 2",
            pricePerItem = "0.99",
            imageUrl = ""
        });
        
        storeItems.Add(new StoreItem {
            id = "magic_shield",
            name = "Item 3",
            description = "Description 3",
            pricePerItem = "2.99",
            imageUrl = ""
        });
        
        storeItems.Add(new StoreItem {
            id = "xp_booster",
            name = "Item 4",
            description = "Description 4",
            pricePerItem = "5.99",
            imageUrl = ""
        });
    }

    private void ProcessPurchase(int itemIndex)
    {
        if (itemIndex < 0 || itemIndex >= storeItems.Count)
        {
            Debug.LogError("Invalid item index!");
            return;
        }

        StoreItem item = storeItems[itemIndex];
        
        Debug.Log($"Processing purchase with Stash for item: {item.id} at price: {item.pricePerItem}");
        
        // Display a loading indicator
        SetButtonLoadingState(buyButtons[itemIndex], true);
        
        // Open Stash popup for checkout
        OpenStashCheckout(itemIndex);
    }
    
    private async void OpenStashCheckout(int itemIndex)
    {
        // Check if user is authenticated
        if (AuthenticationManager.Instance != null && !AuthenticationManager.Instance.IsAuthenticated())
        {
            Debug.LogWarning("User not authenticated. Please login before purchasing.");
            
            // Show login dialog instead of highlighting login button
            ShowLoginRequiredMessage();
            
            // Reset button state
            SetButtonLoadingState(buyButtons[itemIndex], false);
            return;
        }
        
        try
        {
            StoreItem item = storeItems[itemIndex];
            
            // Create a checkout item with the specified product information
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
            var (url, id) = await StashCheckout.CreateCheckoutLinkWithItems(
                externalUserId,
                userEmail,
                displayName,
                avatarIconUrl,
                profileUrl,
                shopHandle,
                currency,
                new StashCheckout.CheckoutItemData[] { checkoutItem },
                apiKey,
                environment
            );
            
            // Store the checkout ID and item index for verification later
            currentCheckoutId = id;
            currentItemIndex = itemIndex;
            
            Debug.Log($"[Stash] Generated checkout URL: {url} with ID: {id}");
            
            // Open the checkout URL in the StashPayCard
            StashPayCard.Instance.OpenURL(url, () => OnBrowserClosed());
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
        Debug.Log($"[Stash] Browser closed for checkout ID: {currentCheckoutId}");
        
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
        Debug.Log($"[Stash] Verification URL: {verifyUrl}");
        
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
                Debug.Log($"[Stash] Purchase response: {responseText}");
                
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
                        
                    Debug.Log($"[Stash] Purchase verification result: {isSuccessful}");
                    
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
                Debug.Log($"[Stash] Purchase verified successfully for ID: {currentCheckoutId}");
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
                OnPurchaseCompleted?.Invoke(storeItems[currentItemIndex].id, true);
            }
            else
            {
                // Purchase failed verification
                Debug.LogWarning("[Stash] Purchase verification failed");
                HandleFailedPurchase(currentItemIndex);
                OnPurchaseCompleted?.Invoke(storeItems[currentItemIndex].id, false);
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
        Debug.Log($"Purchase successful for item: {storeItems[itemIndex].id}");
        
        // Implement your purchase success logic
        // Could include adding the item to inventory, showing success message, etc.
    }
    
    private void HandleFailedPurchase(int itemIndex)
    {
        Debug.LogError($"Purchase failed for item: {storeItems[itemIndex].id}");
        
        // Show the purchase failed state on the button
        Button button = buyButtons[itemIndex];
        button.AddToClassList("purchase-failed");
        
        // Remove the failed state after a short delay
        Invoke(() => {
            button.RemoveFromClassList("purchase-failed");
        }, 2f);
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
            button.RemoveFromClassList("button-loading");
            button.text = "BUY";
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
}

// Simple success popup class for showing purchase success messages
public class SuccessPopup : MonoBehaviour
{
    private string title;
    private string message;
    private bool isShowing = false;
    private float showDuration = 3f;
    private GUIStyle titleStyle;
    private GUIStyle messageStyle;
    private Rect windowRect;
    
    public void Show(string title, string message)
    {
        this.title = title;
        this.message = message;
        isShowing = true;
        
        // Set up styles
        titleStyle = new GUIStyle();
        titleStyle.fontSize = 20;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.normal.textColor = Color.white;
        titleStyle.alignment = TextAnchor.MiddleCenter;
        
        messageStyle = new GUIStyle();
        messageStyle.fontSize = 16;
        messageStyle.normal.textColor = Color.white;
        messageStyle.alignment = TextAnchor.MiddleCenter;
        messageStyle.wordWrap = true;
        
        // Set up window rect
        float width = 300;
        float height = 150;
        windowRect = new Rect((Screen.width - width) / 2, (Screen.height - height) / 2, width, height);
        
        // Close after duration
        Destroy(gameObject, showDuration);
    }
    
    private void OnGUI()
    {
        if (isShowing)
        {
            // Draw a semi-transparent background
            GUI.color = new Color(0, 0, 0, 0.8f);
            GUI.DrawTexture(windowRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            
            // Draw the content
            GUILayout.BeginArea(windowRect);
            GUILayout.Space(20);
            GUILayout.Label(title, titleStyle);
            GUILayout.Space(20);
            GUILayout.Label(message, messageStyle);
            GUILayout.EndArea();
        }
    }
} 