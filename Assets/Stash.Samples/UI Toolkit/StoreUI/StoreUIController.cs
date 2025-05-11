using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class StoreUIController : MonoBehaviour
{
    [SerializeField] private UIDocument storeUIDocument;
    [SerializeField] private Texture2D[] itemImages;

    private VisualElement root;
    private List<Button> buyButtons = new List<Button>();
    private List<string> itemPrices = new List<string>() { "$4.99", "$0.99", "$2.99", "$5.99" };
    private List<string> itemIds = new List<string>() { "premium_sword", "health_potion", "magic_shield", "xp_booster" };

    // Delegate for purchase callbacks
    public delegate void PurchaseCompletedDelegate(string itemId, bool success);
    public event PurchaseCompletedDelegate OnPurchaseCompleted;

    private void OnEnable()
    {
        // Get the root of the UI document
        root = storeUIDocument.rootVisualElement;

        // Setup buy buttons
        for (int i = 1; i <= 4; i++)
        {
            string buttonName = $"buy-button-{i}";
            Button buyButton = root.Q<Button>(buttonName);
            
            if (buyButton != null)
            {
                int itemIndex = i - 1; // Store index for the callback
                buyButton.clicked += () => ProcessPurchase(itemIndex);
                buyButtons.Add(buyButton);
            }
        }

        // Set item images
        for (int i = 1; i <= 4; i++)
        {
            if (itemImages != null && itemImages.Length >= i && itemImages[i-1] != null)
            {
                string imageName = $"item-{i}-image";
                VisualElement imageElement = root.Q<VisualElement>(imageName);
                
                if (imageElement != null)
                {
                    // Set the background image
                    imageElement.style.backgroundImage = new StyleBackground(itemImages[i-1]);
                }
            }
        }
    }

    private void ProcessPurchase(int itemIndex)
    {
        if (itemIndex < 0 || itemIndex >= itemIds.Count)
        {
            Debug.LogError("Invalid item index!");
            return;
        }

        string itemId = itemIds[itemIndex];
        
        // Here you would typically call your payment processor
        // For example: IAPManager.PurchaseItem(itemId);
        
        Debug.Log($"Processing purchase for item: {itemId} at price: {itemPrices[itemIndex]}");
        
        // Display a loading indicator
        SetButtonLoadingState(buyButtons[itemIndex], true);
        
        // Simulate purchase process (replace with actual IAP implementation)
        SimulatePurchaseProcess(itemIndex);
    }
    
    private void SimulatePurchaseProcess(int itemIndex)
    {
        // This is just for demonstration. In a real app, you'd integrate with
        // platform-specific IAP services (e.g., Unity IAP, Apple StoreKit, Google Play Billing)
        string itemId = itemIds[itemIndex];
        
        // Simulate network delay
        Invoke(() => {
            // Simulate 80% success rate
            bool success = (Random.value < 0.8f);
            
            if (success)
            {
                HandleSuccessfulPurchase(itemIndex);
            }
            else
            {
                HandleFailedPurchase(itemIndex);
            }
            
            // Reset button state
            SetButtonLoadingState(buyButtons[itemIndex], false);
            
            // Notify listeners
            OnPurchaseCompleted?.Invoke(itemId, success);
            
        }, 1.5f);
    }
    
    private void HandleSuccessfulPurchase(int itemIndex)
    {
        Debug.Log($"Purchase successful for item: {itemIds[itemIndex]}");
        
        // Here you would grant the item to the player
        // For example: Player.GrantItem(itemIds[itemIndex]);
        
        // Show success feedback
        Button button = buyButtons[itemIndex];
        button.text = "OWNED";
        button.SetEnabled(false);
    }
    
    private void HandleFailedPurchase(int itemIndex)
    {
        Debug.LogWarning($"Purchase failed for item: {itemIds[itemIndex]}");
        
        // Show error feedback temporarily
        Button button = buyButtons[itemIndex];
        string originalText = button.text;
        button.text = "FAILED";
        button.AddToClassList("purchase-failed");
        
        // Reset button after delay
        Invoke(() => {
            button.text = originalText;
            button.RemoveFromClassList("purchase-failed");
        }, 2f);
    }
    
    private void SetButtonLoadingState(Button button, bool isLoading)
    {
        if (isLoading)
        {
            button.text = "...";
            button.SetEnabled(false);
            button.AddToClassList("button-loading");
        }
        else
        {
            button.text = "BUY";
            button.SetEnabled(true);
            button.RemoveFromClassList("button-loading");
        }
    }

    // Helper for delayed actions
    private void Invoke(System.Action action, float delay)
    {
        StartCoroutine(InvokeRoutine(action, delay));
    }

    private System.Collections.IEnumerator InvokeRoutine(System.Action action, float delay)
    {
        yield return new WaitForSeconds(delay);
        action();
    }
} 