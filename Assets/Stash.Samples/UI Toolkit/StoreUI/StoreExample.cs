using UnityEngine;
using UnityEngine.UIElements;

public class StoreExample : MonoBehaviour
{
    [SerializeField] private UIDocument storeUIDocument;
    [SerializeField] private GameObject storeUIGameObject;
    
    // Reference to store controller
    private StashStoreUIController storeController;

    private void Start()
    {
        // Get or add store controller component
        storeController = storeUIGameObject.GetComponent<StashStoreUIController>();
        if (storeController == null)
        {
            storeController = storeUIGameObject.AddComponent<StashStoreUIController>();
        }
        
        // Subscribe to purchase events
        storeController.OnPurchaseCompleted += HandlePurchaseCompleted;
        
        // Initially hide the store
        storeUIGameObject.SetActive(false);
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from events when destroyed
        if (storeController != null)
        {
            storeController.OnPurchaseCompleted -= HandlePurchaseCompleted;
        }
    }

    private void Update()
    {
        // Example: Press 'S' to toggle the store
        if (Input.GetKeyDown(KeyCode.S))
        {
            ToggleStore();
        }
    }
    
    private void HandlePurchaseCompleted(string itemId, bool success)
    {
        // Handle the purchase result
        if (success)
        {
            // Grant the purchased item to the player
            switch (itemId)
            {
                case "premium_sword":
                    Debug.Log("Player received Premium Sword!");
                    // PlayerInventory.AddItem(itemId);
                    break;
                case "health_potion":
                    Debug.Log("Player received Health Potion!");
                    // PlayerInventory.AddItem(itemId);
                    break;
                case "magic_shield":
                    Debug.Log("Player received Magic Shield!");
                    // PlayerInventory.AddItem(itemId);
                    break;
                case "xp_booster":
                    Debug.Log("Player received XP Booster!");
                    // PlayerInventory.AddItem(itemId);
                    // PlayerStats.ApplyXpBoost(3600); // 1 hour in seconds
                    break;
            }
        }
        else
        {
            // Handle failed purchase
            Debug.LogWarning($"Purchase failed for item: {itemId}");
            // Perhaps show a UI message to the player
        }
    }

    // Toggle store visibility
    public void ToggleStore()
    {
        storeUIGameObject.SetActive(!storeUIGameObject.activeSelf);
    }

    // Open store
    public void OpenStore()
    {
        storeUIGameObject.SetActive(true);
    }

    // Close store
    public void CloseStore()
    {
        storeUIGameObject.SetActive(false);
    }
} 