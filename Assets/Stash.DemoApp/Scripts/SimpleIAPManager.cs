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
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                isInitializing = true;
                
                await InitializeUnityGamingServices();
                
                if (isUGSInitialized)
                {
                    InitializePurchasing();
                }
                else
                {
                    Debug.LogError("[SimpleIAP] Cannot initialize IAP because UGS initialization failed");
                    isInitializing = false;
                }
            }
            else
            {
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
            return isReady && storeController != null;
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
                await UnityServices.InitializeAsync();
                isUGSInitialized = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SimpleIAP] Unity Gaming Services initialization failed: {ex.Message}");
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
                Debug.LogError("[SimpleIAP] Cannot initialize IAP: Unity Gaming Services not initialized");
                isInitializing = false;
                return;
            }
            
            try
            {
                var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
                
                foreach (string productId in productIds)
                {
                    builder.AddProduct(productId, ProductType.Consumable);
                }
                
                UnityPurchasing.Initialize(this, builder);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SimpleIAP] IAP Initialization exception: {ex.Message}");
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
        }

        /// <summary>
        /// Called when Unity IAP initialization fails
        /// </summary>
        public void OnInitializeFailed(InitializationFailureReason error)
        {
            Debug.LogError($"[SimpleIAP] IAP initialization failed: {error}");
            LogTroubleshootingInfo(error.ToString());
            isReady = false;
            isInitializing = false;
        }

        /// <summary>
        /// Called when Unity IAP initialization fails with additional details
        /// </summary>
        public void OnInitializeFailed(InitializationFailureReason error, string message)
        {
            Debug.LogError($"[SimpleIAP] IAP initialization failed: {error} - {message}");
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
            OnPurchaseSuccess?.Invoke(productId);
            return PurchaseProcessingResult.Complete;
        }

        /// <summary>
        /// Called when a purchase fails
        /// </summary>
        public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
        {
            string productId = product?.definition.id ?? "unknown";
            Debug.LogError($"[SimpleIAP] Purchase failed: {productId} - {failureReason}");
            OnPurchaseFailure?.Invoke(productId, failureReason.ToString());
        }
        #endregion

        #region Helper Methods
        /// <summary>
        /// Logs initialization failure with essential info
        /// </summary>
        private void LogTroubleshootingInfo(string error)
        {
            Debug.LogError($"[SimpleIAP] Troubleshooting: {error}");
            Debug.LogError($"[SimpleIAP] Bundle ID: {Application.identifier}, Platform: {Application.platform}");
            Debug.LogError($"[SimpleIAP] Products: {string.Join(", ", productIds)}");
        }
        #endregion
    }
} 