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
            
            // Add click handler
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
        
        // Disable the buy button to prevent multiple checkouts
        SetButtonEnabled(buyButtons[itemIndex], false);
        
        // Display a loading indicator
        SetButtonLoadingState(buyButtons[itemIndex], true);
        
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
                Debug.Log($"[Stash] Using generated guest credentials - UserId: {userId}, Email: {email}");
            }
            
            // Generate the checkout URL using the Stash Checkout API
            var (url, id) = await StashCheckout.CreateCheckoutLink(
                userId,
                email,
                shopHandle,
                item.id,
                apiKey,
                environment
            );
            
            // Store the checkout ID and item index for verification later
            currentCheckoutId = id;
            currentItemIndex = itemIndex;
            
            Debug.Log($"[Stash] Generated checkout URL: {url} with ID: {id}");
            
            // Open the checkout URL in the StashPayCard
            StashPayCard.Instance.OpenURL(url, () => OnBrowserClosed(), () => OnPaymentSuccessDetected());
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
        
        // Re-enable the buy button since checkout was dismissed
        SetButtonEnabled(buyButtons[currentItemIndex], true);
        
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
        
        // Re-enable the buy button after successful purchase
        SetButtonEnabled(buyButtons[itemIndex], true);
        
        // Implement your purchase success logic
        // Could include adding the item to inventory, showing success message, etc.
    }
    
    private void HandleFailedPurchase(int itemIndex)
    {
        Debug.LogError($"Purchase failed for item: {storeItems[itemIndex].id}");
        
        // Re-enable the buy button after failed purchase
        SetButtonEnabled(buyButtons[itemIndex], true);
        
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
            // Get the item index from the button name
            string buttonName = button.name;
            int itemIndex = int.Parse(buttonName.Split('-')[2]) - 1;
            button.text = "$" + storeItems[itemIndex].pricePerItem;
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
        Debug.Log("[Stash] Payment success detected from iOS - showing celebration!");
        
        try
        {
            // Ensure we're on the main thread for UI operations
            if (this != null && gameObject != null)
            {
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
            }
            else
            {
                Debug.LogWarning("[Stash] Cannot show celebration - component or GameObject is null");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Stash] Error showing payment success celebration: {ex.Message}");
        }
    }
    
    private void ShowSuccessPopupWithConfetti(string title, string message)
    {
        try
        {
            // Create confetti system that renders in front of UI
            GameObject confettiGO = new GameObject("ConfettiSystem");
            
            // Add Canvas component for UI rendering
            Canvas confettiCanvas = confettiGO.AddComponent<Canvas>();
            confettiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            confettiCanvas.sortingOrder = 1000; // Very high sorting order to render in front of everything
            
            // Add CanvasScaler for consistent scaling
            CanvasScaler scaler = confettiGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            
            // Position the confetti container at the top of the screen
            RectTransform rectTransform = confettiGO.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0, 1); // Top-left anchor
            rectTransform.anchorMax = new Vector2(1, 1); // Top-right anchor
            rectTransform.anchoredPosition = new Vector2(0, 100); // Slightly above screen
            rectTransform.sizeDelta = new Vector2(0, 200); // Full width, 200px height
            
            // Create multiple confetti emitters across the screen width
            for (int i = 0; i < 5; i++)
            {
                GameObject emitter = new GameObject($"ConfettiEmitter_{i}");
                emitter.transform.SetParent(confettiGO.transform, false);
                
                RectTransform emitterRect = emitter.AddComponent<RectTransform>();
                float xPos = (i / 4.0f) - 0.5f; // Spread across screen: -0.5 to 0.5
                emitterRect.anchorMin = new Vector2(0.5f + xPos, 0.5f);
                emitterRect.anchorMax = new Vector2(0.5f + xPos, 0.5f);
                emitterRect.anchoredPosition = Vector2.zero;
                
                ParticleSystem confetti = emitter.AddComponent<ParticleSystem>();
                
                // Configure main module for UI-space confetti
                var main = confetti.main;
                main.startLifetime = 4.0f;
                main.startSpeed = 300.0f; // Higher speed for UI space
                main.startSize = 20.0f; // Larger size for UI space (pixels)
                main.startColor = GetRandomConfettiColor(i);
                main.maxParticles = 40; // Per emitter
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                
                // Configure emission for staggered bursts
                var emission = confetti.emission;
                emission.rateOverTime = 0;
                emission.SetBursts(new ParticleSystem.Burst[] {
                    new ParticleSystem.Burst(i * 0.1f, 20), // Stagger the bursts
                    new ParticleSystem.Burst(i * 0.1f + 0.3f, 25),
                    new ParticleSystem.Burst(i * 0.1f + 0.6f, 15)
                });
                
                // Configure shape for wide spread
                var shape = confetti.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 50; // Spread radius in UI pixels
                
                // Configure velocity for falling effect
                var velocityOverLifetime = confetti.velocityOverLifetime;
                velocityOverLifetime.enabled = true;
                velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
                velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(-400f, -200f); // Downward fall
                velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-100f, 100f); // Side drift
                
                // Configure size over lifetime
                var sizeOverLifetime = confetti.sizeOverLifetime;
                sizeOverLifetime.enabled = true;
                AnimationCurve sizeCurve = new AnimationCurve();
                sizeCurve.AddKey(0, 1.0f);
                sizeCurve.AddKey(0.3f, 1.3f); // Grow slightly
                sizeCurve.AddKey(1, 0.8f); // Shrink at end
                sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1.0f, sizeCurve);
                
                // Configure vibrant colors
                var colorOverLifetime = confetti.colorOverLifetime;
                colorOverLifetime.enabled = true;
                Gradient gradient = new Gradient();
                gradient.SetKeys(
                    new GradientColorKey[] { 
                        new GradientColorKey(GetRandomConfettiColor(i), 0.0f), 
                        new GradientColorKey(GetRandomConfettiColor(i + 1), 0.5f),
                        new GradientColorKey(GetRandomConfettiColor(i + 2), 1.0f)
                    },
                    new GradientAlphaKey[] { 
                        new GradientAlphaKey(1.0f, 0.0f), 
                        new GradientAlphaKey(1.0f, 0.7f),
                        new GradientAlphaKey(0.0f, 1.0f) 
                    }
                );
                colorOverLifetime.color = gradient;
                
                // Configure rotation for spinning effect
                var rotationOverLifetime = confetti.rotationOverLifetime;
                rotationOverLifetime.enabled = true;
                rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-360f, 360f);
                
                // Play each emitter
                confetti.Play();
            }
            
            Debug.Log($"[Stash] UI-space confetti system created with 5 emitters");
            Debug.Log($"[Stash] Canvas sorting order: {confettiCanvas.sortingOrder}");
            Debug.Log($"[Stash] Confetti will render in front of UI elements");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Stash] Error creating confetti effect: {ex.Message}");
        }
    }
    
    private Color GetRandomConfettiColor(int seed)
    {
        Color[] colors = {
            Color.yellow,
            Color.magenta,
            Color.cyan,
            Color.green,
            Color.red,
            new Color(1f, 0.5f, 0f), // Orange
            new Color(0.5f, 0f, 1f), // Purple
            new Color(1f, 0.75f, 0.8f) // Pink
        };
        return colors[seed % colors.Length];
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from payment success events
        if (StashPayCard.Instance != null)
        {
            StashPayCard.Instance.OnPaymentSuccess -= OnPaymentSuccessDetected;
        }
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
        
        // Set up window rect
        float width = 1200; // 4x bigger: was 300, now 1200
        float height = 600;  // 4x bigger: was 150, now 600
        windowRect = new Rect((Screen.width - width) / 2, (Screen.height - height) / 2, width, height);
        
        // Set up styles
        titleStyle = new GUIStyle();
        titleStyle.fontSize = 80; // 4x bigger: was 20, now 80
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.normal.textColor = Color.white;
        titleStyle.alignment = TextAnchor.MiddleCenter;
        
        messageStyle = new GUIStyle();
        messageStyle.fontSize = 64; // 4x bigger: was 16, now 64
        messageStyle.normal.textColor = Color.white;
        messageStyle.alignment = TextAnchor.MiddleCenter;
        messageStyle.wordWrap = true;
        
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
            GUILayout.Space(80); // 4x bigger spacing: was 20, now 80
            GUILayout.Label(title, titleStyle);
            GUILayout.Space(80); // 4x bigger spacing: was 20, now 80
            GUILayout.Label(message, messageStyle);
            GUILayout.EndArea();
        }
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