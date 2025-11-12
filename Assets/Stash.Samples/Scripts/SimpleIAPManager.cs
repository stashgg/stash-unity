using System;
using UnityEngine;
using UnityEngine.Purchasing;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Core;

namespace Stash.Samples
{
    /// <summary>
    /// Simple IAP Manager designed for TestFlight sandbox purchases.
    /// This manager handles Unity IAP integration with minimal complexity,
    /// working with whatever products are available in App Store Connect.
    /// 
    /// Features:
    /// - Automatic Unity Gaming Services initialization
    /// - Automatic IAP initialization on first access
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
        private bool isUGSInitialized = false;
        private bool isInitializing = false;
        
        /// <summary>
        /// Product IDs to initialize. Works with whatever exists in App Store Connect.
        /// For TestFlight: products just need "Cleared for Sale" = YES (no approval required)
        /// 
        /// IMPORTANT: These IDs must EXACTLY match the Product IDs in App Store Connect
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
        async void Awake()
        {
            Debug.Log("[SimpleIAP] 🔵 Awake called - Starting initialization");
            
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                isInitializing = true;
                
                Debug.Log($"[SimpleIAP] Platform: {Application.platform}, Bundle ID: {Application.identifier}");
                Debug.Log("[SimpleIAP] Instance created, starting async initialization...");
                
                await InitializeUnityGamingServices();
                
                if (isUGSInitialized)
                {
                    Debug.Log("[SimpleIAP] UGS initialized successfully, starting IAP initialization...");
                    InitializePurchasing();
                }
                else
                {
                    Debug.LogError("[SimpleIAP] ❌ Cannot initialize IAP because UGS initialization failed");
                    isInitializing = false;
                }
            }
            else
            {
                Debug.LogWarning("[SimpleIAP] Duplicate instance detected - destroying");
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
                if (isInitializing)
                {
                    Debug.LogWarning($"[SimpleIAP] IAP not ready: Still initializing (UGS={isUGSInitialized}, IAP={isReady})");
                }
                else
                {
                    Debug.LogWarning($"[SimpleIAP] IAP not ready: initialized={isReady}, controller={storeController != null}, UGS={isUGSInitialized}");
                }
            }
            return ready;
        }
        
        /// <summary>
        /// Returns true if initialization is currently in progress
        /// </summary>
        public bool IsInitializing() 
        {
            return isInitializing;
        }
        
        /// <summary>
        /// Returns initialization status details for debugging
        /// </summary>
        public string GetInitializationStatus()
        {
            if (isReady && storeController != null)
                return "Ready";
            if (isInitializing)
                return $"Initializing (UGS: {(isUGSInitialized ? "✓" : "...")} IAP: {(isReady ? "✓" : "...")})";
            if (!isUGSInitialized)
                return "Failed: Unity Gaming Services not initialized";
            if (!isReady)
                return "Failed: IAP not initialized";
            return "Unknown";
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
        /// Initializes Unity Gaming Services (required before IAP)
        /// </summary>
        async Task InitializeUnityGamingServices()
        {
            try
            {
                Debug.Log("[SimpleIAP] 🟡 Initializing Unity Gaming Services...");
                Debug.Log($"[SimpleIAP] Device ID: {SystemInfo.deviceUniqueIdentifier}");
                
                await UnityServices.InitializeAsync();
                isUGSInitialized = true;
                
                Debug.Log("[SimpleIAP] ✅ Unity Gaming Services initialized successfully");
                Debug.Log($"[SimpleIAP] UGS State: {UnityServices.State}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SimpleIAP] ❌ Unity Gaming Services initialization failed!");
                Debug.LogError($"[SimpleIAP] Error Type: {ex.GetType().Name}");
                Debug.LogError($"[SimpleIAP] Error Message: {ex.Message}");
                Debug.LogError($"[SimpleIAP] Stack Trace: {ex.StackTrace}");
                Debug.LogError($"[SimpleIAP] IAP will not be available.");
                isUGSInitialized = false;
                isInitializing = false;
            }
        }
        
        /// <summary>
        /// Initializes Unity IAP system
        /// </summary>
        void InitializePurchasing()
        {
            Debug.Log("[SimpleIAP] 🟡 Starting Unity IAP initialization...");
            
            if (!isUGSInitialized)
            {
                Debug.LogError("[SimpleIAP] ❌ Cannot initialize IAP: Unity Gaming Services not initialized");
                isInitializing = false;
                return;
            }
            
            try
            {
                var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
                
                // Add all product IDs as consumables
                Debug.Log($"[SimpleIAP] Configuring {productIds.Length} products...");
                foreach (string productId in productIds)
                {
                    builder.AddProduct(productId, ProductType.Consumable);
                    Debug.Log($"[SimpleIAP]   - Added product: {productId}");
                }
                
                Debug.Log("[SimpleIAP] Calling UnityPurchasing.Initialize...");
                UnityPurchasing.Initialize(this, builder);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SimpleIAP] ❌ IAP Initialization exception!");
                Debug.LogError($"[SimpleIAP] Error Type: {ex.GetType().Name}");
                Debug.LogError($"[SimpleIAP] Error Message: {ex.Message}");
                Debug.LogError($"[SimpleIAP] Stack Trace: {ex.StackTrace}");
                isInitializing = false;
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
            isInitializing = false;
            
            Debug.Log("[SimpleIAP] ✅✅✅ IAP INITIALIZATION SUCCESSFUL ✅✅✅");
            Debug.Log($"[SimpleIAP] {controller.products.all.Length} products loaded and available for purchase");
            
            // Log all products with detailed information
            foreach (var product in controller.products.all)
            {
                Debug.Log($"[SimpleIAP] ═══ Product Details ═══");
                Debug.Log($"[SimpleIAP]   ID: {product.definition.id}");
                Debug.Log($"[SimpleIAP]   Available: {product.availableToPurchase}");
                Debug.Log($"[SimpleIAP]   Title: {product.metadata.localizedTitle}");
                Debug.Log($"[SimpleIAP]   Price: {product.metadata.localizedPriceString}");
                Debug.Log($"[SimpleIAP]   Type: {product.definition.type}");
            }
        }

        /// <summary>
        /// Called when Unity IAP initialization fails
        /// </summary>
        public void OnInitializeFailed(InitializationFailureReason error)
        {
            Debug.LogError($"[SimpleIAP] ❌❌❌ IAP INITIALIZATION FAILED ❌❌❌");
            Debug.LogError($"[SimpleIAP] Failure Reason: {error}");
            LogTroubleshootingInfo(error.ToString());
            isReady = false;
            isInitializing = false;
        }

        /// <summary>
        /// Called when Unity IAP initialization fails with additional details
        /// </summary>
        public void OnInitializeFailed(InitializationFailureReason error, string message)
        {
            Debug.LogError($"[SimpleIAP] ❌❌❌ IAP INITIALIZATION FAILED (with details) ❌❌❌");
            Debug.LogError($"[SimpleIAP] Failure Reason: {error}");
            Debug.LogError($"[SimpleIAP] Error Message: {message}");
            
            // Try to get any partial product information that might have been retrieved
            if (storeController != null && storeController.products != null)
            {
                Debug.LogWarning($"[SimpleIAP] 📦 Products retrieved (even though init failed): {storeController.products.all.Length}");
                foreach (var product in storeController.products.all)
                {
                    Debug.LogWarning($"[SimpleIAP]   - {product.definition.id}: Available={product.availableToPurchase}, HasReceipt={product.hasReceipt}");
                }
            }
            else
            {
                Debug.LogError($"[SimpleIAP] ⚠️ No products were retrieved from App Store");
            }
            
            LogTroubleshootingInfo($"{error}: {message}");
            isReady = false;
            isInitializing = false;
        }

        /// <summary>
        /// Called when a purchase completes successfully
        /// </summary>
        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
        {
            string productId = args.purchasedProduct.definition.id;
            Debug.Log($"[SimpleIAP] ✅ Purchase completed successfully: {productId}");
            
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
            Debug.LogError($"[SimpleIAP] ═══ Troubleshooting Info ═══");
            Debug.LogError($"[SimpleIAP] Error: {error}");
            Debug.LogError($"[SimpleIAP] Bundle ID: {Application.identifier}");
            Debug.LogError($"[SimpleIAP] Platform: {Application.platform}");
            Debug.LogError($"[SimpleIAP] Requested Products ({productIds.Length}):");
            foreach (var productId in productIds)
            {
                Debug.LogError($"[SimpleIAP]   - {productId}");
            }
            Debug.LogError($"[SimpleIAP] ═══════════════════════");
            Debug.LogError($"[SimpleIAP]");
            Debug.LogError($"[SimpleIAP] Common fixes:");
            Debug.LogError($"[SimpleIAP] 1. Verify product IDs match exactly in App Store Connect (case-sensitive)");
            Debug.LogError($"[SimpleIAP] 2. Sign in with Sandbox Test User: Settings > App Store > Sandbox Account");
            Debug.LogError($"[SimpleIAP] 3. Wait 1-24 hours if products were just created");
            Debug.LogError($"[SimpleIAP] 4. Check 'Paid Applications' agreement is active in App Store Connect");
            Debug.LogError($"[SimpleIAP] 5. Ensure bundle ID '{Application.identifier}' matches App Store Connect");
        }
        #endregion
    }
} 