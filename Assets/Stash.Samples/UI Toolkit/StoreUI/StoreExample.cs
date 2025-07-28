using UnityEngine;
using UnityEngine.UIElements;

namespace Stash.Samples
{
    /// <summary>
    /// Example implementation showing how to integrate the Stash Store UI into your game.
    /// This script demonstrates the basic setup and usage patterns for the store system.
    /// 
    /// Key Features:
    /// - Store UI management (show/hide)
    /// - Purchase event handling
    /// - Item granting logic examples
    /// - Integration with your game systems
    /// </summary>
    public class StoreExample : MonoBehaviour
    {
        #region Inspector Fields
        [Header("Store Configuration")]
        [SerializeField] private UIDocument storeUIDocument;
        [SerializeField] private GameObject storeUIGameObject;
        [Tooltip("Enable to show store at startup")]
        [SerializeField] private bool showStoreOnStart = false;
        
        [Header("Input Settings")]
        [Tooltip("Key to toggle store visibility")]
        [SerializeField] private KeyCode toggleStoreKey = KeyCode.S;
        #endregion

        #region Private Fields
        private StashStoreUIController storeController;
        #endregion

        #region Unity Lifecycle
        private void Start()
        {
            InitializeStore();
            
            if (showStoreOnStart)
            {
                OpenStore();
            }
        }

        private void OnDestroy()
        {
            CleanupStore();
        }

        private void Update()
        {
            HandleInput();
        }
        #endregion

        #region Initialization
        /// <summary>
        /// Initializes the store controller and sets up event subscriptions
        /// </summary>
        private void InitializeStore()
        {
            // Get or add store controller component
            storeController = storeUIGameObject.GetComponent<StashStoreUIController>();
            if (storeController == null)
            {
                storeController = storeUIGameObject.AddComponent<StashStoreUIController>();
                // Added StashStoreUIController component
            }

            // Subscribe to purchase events
            storeController.OnPurchaseCompleted += HandlePurchaseCompleted;
            
            // Initially hide the store
            storeUIGameObject.SetActive(false);
            
            // Store initialized successfully
        }

        /// <summary>
        /// Cleans up event subscriptions to prevent memory leaks
        /// </summary>
        private void CleanupStore()
        {
            if (storeController != null)
            {
                storeController.OnPurchaseCompleted -= HandlePurchaseCompleted;
                // Store events unsubscribed
            }
        }
        #endregion

        #region Input Handling
        /// <summary>
        /// Handles user input for store controls
        /// </summary>
        private void HandleInput()
        {
            // Toggle store with configured key
            if (Input.GetKeyDown(toggleStoreKey))
            {
                ToggleStore();
            }
            
            // Close store with Escape key
            if (Input.GetKeyDown(KeyCode.Escape) && IsStoreOpen())
            {
                CloseStore();
            }
        }
        #endregion

        #region Purchase Handling
        /// <summary>
        /// Handles purchase completion events from the store
        /// </summary>
        /// <param name="itemId">The ID of the purchased item</param>
        /// <param name="success">Whether the purchase was successful</param>
        private void HandlePurchaseCompleted(string itemId, bool success)
        {
            if (success)
            {
                // Purchase completed successfully
                GrantItemToPlayer(itemId);
                ShowPurchaseSuccessEffect(itemId);
            }
            else
            {
                Debug.LogWarning($"[StoreExample] Purchase failed: {itemId}");
                ShowPurchaseFailureMessage(itemId);
            }
        }

        /// <summary>
        /// Grants the purchased item to the player
        /// Replace this with your actual inventory/progression system
        /// </summary>
        /// <param name="itemId">The ID of the item to grant</param>
        private void GrantItemToPlayer(string itemId)
        {
            switch (itemId)
            {
                case "fistful_of_potions":
                    Debug.Log("[StoreExample] Granted: Fistful of Potions");
                    // Example: PlayerInventory.AddItem("health_potion", 5);
                    // Example: PlayerStats.AddHealth(50);
                    break;
                    
                case "barrel_of_potions":
                    Debug.Log("[StoreExample] Granted: Barrel of Potions");
                    // Example: PlayerInventory.AddItem("health_potion", 20);
                    // Example: PlayerStats.AddHealth(200);
                    break;
                    
                case "battle_pass":
                    Debug.Log("[StoreExample] Granted: Battle Pass");
                    // Example: PlayerProgression.UnlockBattlePass();
                    // Example: PlayerStats.SetBattlePassActive(true);
                    break;
                    
                case "small_resource_shipment_1":
                    Debug.Log("[StoreExample] Granted: Resource Shipment");
                    // Example: PlayerInventory.AddResources(100);
                    // Example: PlayerStats.AddCoins(100);
                    break;
                    
                default:
                    Debug.LogWarning($"[StoreExample] Unknown item purchased: {itemId}");
                    break;
            }
            
            // You might want to save the game state after granting items
            // Example: GameManager.SaveGame();
        }

        /// <summary>
        /// Shows a success effect when purchase completes
        /// </summary>
        /// <param name="itemId">The purchased item ID</param>
        private void ShowPurchaseSuccessEffect(string itemId)
        {
            // Example: Show a toast notification
            // Example: Play success sound effect
            // Example: Trigger particle effects
            Debug.Log($"[StoreExample] Success effect for: {itemId}");
        }

        /// <summary>
        /// Shows a failure message when purchase fails
        /// </summary>
        /// <param name="itemId">The failed item ID</param>
        private void ShowPurchaseFailureMessage(string itemId)
        {
            // Example: Show error dialog
            // Example: Play error sound effect
            Debug.LogError($"[StoreExample] Purchase failed message for: {itemId}");
        }
        #endregion

        #region Public API
        /// <summary>
        /// Toggles store visibility
        /// </summary>
        public void ToggleStore()
        {
            if (IsStoreOpen())
            {
                CloseStore();
            }
            else
            {
                OpenStore();
            }
        }

        /// <summary>
        /// Opens the store
        /// </summary>
        public void OpenStore()
        {
            storeUIGameObject.SetActive(true);
            Debug.Log("[StoreExample] Store opened");
            
            // Example: Pause game when store opens
            // Example: Time.timeScale = 0f;
            
            // Example: Play store open sound
            // Example: AudioManager.PlaySound("store_open");
        }

        /// <summary>
        /// Closes the store
        /// </summary>
        public void CloseStore()
        {
            storeUIGameObject.SetActive(false);
            Debug.Log("[StoreExample] Store closed");
            
            // Example: Resume game when store closes
            // Example: Time.timeScale = 1f;
            
            // Example: Play store close sound
            // Example: AudioManager.PlaySound("store_close");
        }

        /// <summary>
        /// Checks if the store is currently open
        /// </summary>
        /// <returns>True if store is visible</returns>
        public bool IsStoreOpen()
        {
            return storeUIGameObject.activeSelf;
        }

        /// <summary>
        /// Forces a specific item purchase (for testing/cheats)
        /// </summary>
        /// <param name="itemId">Item ID to purchase</param>
        public void ForcePurchaseItem(string itemId)
        {
            Debug.Log($"[StoreExample] Force purchasing: {itemId}");
            HandlePurchaseCompleted(itemId, true);
        }
        #endregion
    }
} 