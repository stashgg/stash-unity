using System;
using UnityEngine;
using UnityEngine.Purchasing;
using System.Collections.Generic;

namespace Stash.Samples
{
    /// <summary>
    /// Simple IAP Manager designed for TestFlight sandbox purchases.
    /// This manager handles Unity IAP integration with minimal complexity,
    /// working with whatever products are available in App Store Connect.
    /// 
    /// Features:
    /// - Automatic initialization on first access
    /// - Works with any products configured in App Store Connect
    /// - TestFlight optimized (works with unapproved products)
    /// - Clean event-based architecture
    /// </summary>
    public class SimpleIAPManager : MonoBehaviour, IStoreListener
    {
        #region Singleton Implementation
        public static SimpleIAPManager Instance;
        #endregion

        #region Private Fields
        private IStoreController storeController;
        private bool isReady = false;
        
        /// <summary>
        /// Product IDs to initialize. Works with whatever exists in App Store Connect.
        /// For TestFlight: products just need "Cleared for Sale" = YES (no approval required)
        /// </summary>
        private readonly string[] productIds = {
            "fistful_of_potions",
            "barrel_of_potions", 
            "battle_pass",
            "small_resource_shipment_1"
        };
        #endregion

        #region Events
        /// <summary>
        /// Fired when a purchase completes successfully.
        /// Parameter: productId of the purchased item
        /// </summary>
        public event Action<string> OnPurchaseSuccess;
        
        /// <summary>
        /// Fired when a purchase fails.
        /// Parameters: productId, error message
        /// </summary>
        public event Action<string, string> OnPurchaseFailure;
        #endregion

        #region Unity Lifecycle
        void Awake()
        {
            // Initializing Simple IAP Manager
            // Platform and bundle ID available in logs if needed for debugging
            
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                // Instance created successfully
                InitializePurchasing();
            }
            else
            {
                // Duplicate instance detected - destroying
                Destroy(gameObject);
            }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Returns true if IAP is ready to process purchases
        /// </summary>
        public bool IsReady() 
        {
            bool ready = isReady && storeController != null;
            if (!ready)
            {
                Debug.LogWarning($"[SimpleIAP] IAP not ready: initialized={isReady}, controller={storeController != null}");
            }
            return ready;
        }

        /// <summary>
        /// Initiates a purchase for the specified product
        /// </summary>
        /// <param name="productId">Product ID to purchase</param>
        public void BuyProduct(string productId)
        {
            // Purchase request initiated
            
            if (!IsReady())
            {
                Debug.LogError("[SimpleIAP] IAP not ready for purchases");
                OnPurchaseFailure?.Invoke(productId, "IAP not ready");
                return;
            }

            try
            {
                var product = storeController.products.WithID(productId);
                
                if (product != null && product.availableToPurchase)
                {
                    // Initiating purchase request
                    storeController.InitiatePurchase(product);
                }
                else
                {
                    string error = product == null ? "Product not found" : "Product not available for purchase";
                    Debug.LogError($"[SimpleIAP] Cannot purchase {productId}: {error}");
                    OnPurchaseFailure?.Invoke(productId, error);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SimpleIAP] Exception during purchase: {ex.Message}");
                OnPurchaseFailure?.Invoke(productId, $"Purchase exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the localized price string for a product
        /// </summary>
        /// <param name="productId">Product ID to get price for</param>
        /// <returns>Localized price string or null if not available</returns>
        public string GetPrice(string productId)
        {
            if (!isReady) return null;
            var product = storeController.products.WithID(productId);
            return product?.metadata.localizedPriceString;
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Initializes Unity IAP system
        /// </summary>
        void InitializePurchasing()
        {
            // Starting Unity IAP initialization
            
            try
            {
                var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
                
                // Add all product IDs as consumables
                foreach (string productId in productIds)
                {
                    builder.AddProduct(productId, ProductType.Consumable);
                }
                
                // Initializing IAP with configured products
                UnityPurchasing.Initialize(this, builder);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SimpleIAP] Initialization failed: {ex.Message}");
            }
        }
        #endregion

        #region IStoreListener Implementation
        /// <summary>
        /// Called when Unity IAP initialization succeeds
        /// </summary>
        public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
        {
            storeController = controller;
            isReady = true;
            
            // IAP initialization successful
            
            // Log available products (only in debug builds)
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Products loaded and available for purchase
            #endif
        }

        /// <summary>
        /// Called when Unity IAP initialization fails
        /// </summary>
        public void OnInitializeFailed(InitializationFailureReason error)
        {
            Debug.LogError($"[SimpleIAP] ❌ Initialization failed: {error}");
            LogTroubleshootingInfo(error.ToString());
            isReady = false;
        }

        /// <summary>
        /// Called when Unity IAP initialization fails with additional details
        /// </summary>
        public void OnInitializeFailed(InitializationFailureReason error, string message)
        {
            Debug.LogError($"[SimpleIAP] ❌ Initialization failed: {error} - {message}");
            LogTroubleshootingInfo($"{error}: {message}");
            isReady = false;
        }

        /// <summary>
        /// Called when a purchase completes successfully
        /// </summary>
        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
        {
            string productId = args.purchasedProduct.definition.id;
            // Purchase completed successfully
            
            OnPurchaseSuccess?.Invoke(productId);
            return PurchaseProcessingResult.Complete;
        }

        /// <summary>
        /// Called when a purchase fails
        /// </summary>
        public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
        {
            string productId = product?.definition.id ?? "unknown";
            Debug.LogError($"[SimpleIAP] ❌ Purchase failed: {productId} - {failureReason}");
            
            OnPurchaseFailure?.Invoke(productId, failureReason.ToString());
        }
        #endregion

        #region Helper Methods
        /// <summary>
        /// Logs initialization failure with essential info
        /// </summary>
        private void LogTroubleshootingInfo(string error)
        {
            Debug.LogError($"[SimpleIAP] Initialization failed: {error}");
            Debug.LogError($"[SimpleIAP] Bundle ID: {Application.identifier} | Products: {string.Join(", ", productIds)}");
        }
        #endregion
    }
} 