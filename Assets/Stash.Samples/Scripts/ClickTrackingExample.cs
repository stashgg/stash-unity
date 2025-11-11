using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Stash.Core;
using Stash.Scripts.Core;

namespace Stash.Samples
{
    /// <summary>
    /// Example script demonstrating how to use Stash click event tracking
    /// </summary>
    public class ClickTrackingExample : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Your shop handle from Stash dashboard")]
        public string shopHandle = "your-shop-handle";
        
        [Tooltip("Environment to use (Test for development, Production for release)")]
        public StashEnvironment environment = StashEnvironment.Test;

        [Header("UI References (Optional)")]
        public Button shopButton;
        public Button settingsButton;
        public Button playButton;

        void Start()
        {
            // Initialize Stash SDK with analytics
            StashSDK.Initialize(shopHandle, environment);

            // Add click tracking to buttons if assigned
            if (shopButton != null)
            {
                shopButton.onClick.AddListener(() => OnShopButtonClick());
            }

            if (settingsButton != null)
            {
                settingsButton.onClick.AddListener(() => OnSettingsButtonClick());
            }

            if (playButton != null)
            {
                playButton.onClick.AddListener(() => OnPlayButtonClick());
            }
        }

        // Example 1: Basic click tracking
        void OnShopButtonClick()
        {
            // Track the click with just element ID and screen name
            StashSDK.TrackClick("shop_button", "main_menu");

            Debug.Log("Shop button clicked!");
        }

        // Example 2: Click tracking with custom data
        void OnSettingsButtonClick()
        {
            var customData = new Dictionary<string, object>
            {
                { "settingsSection", "audio" },
                { "sessionTime", Time.timeSinceLevelLoad }
            };

            StashSDK.TrackClick("settings_button", "main_menu", customData);

            Debug.Log("Settings button clicked!");
        }

        // Example 3: Simple click tracking (element ID only)
        void OnPlayButtonClick()
        {
            StashSDK.TrackClick("play_button");

            Debug.Log("Play button clicked!");
        }

        // Example 4: Track special offer click with rich metadata
        public void OnSpecialOfferClick(string offerId, float price)
        {
            var offerData = new Dictionary<string, object>
            {
                { "offerId", offerId },
                { "offerPrice", price },
                { "currency", "USD" },
                { "discount", 0.25f }
            };

            StashSDK.TrackClick("special_offer_banner", "store_page", offerData);

            Debug.Log($"Special offer {offerId} clicked!");
        }

        // Example 5: Track inventory item click
        public void OnInventoryItemClick(int itemIndex, string itemId)
        {
            var itemData = new Dictionary<string, object>
            {
                { "itemId", itemId },
                { "itemIndex", itemIndex },
                { "viewMode", "grid" }
            };

            StashSDK.TrackClick("inventory_item", "inventory_screen", itemData);

            Debug.Log($"Inventory item {itemId} at index {itemIndex} clicked!");
        }

        // Example 6: Track navigation between screens
        public void OnScreenChange(string newScreen, string previousScreen)
        {
            var navigationData = new Dictionary<string, object>
            {
                { "previousScreen", previousScreen },
                { "loadTime", Time.time }
            };

            StashSDK.TrackClick($"nav_to_{newScreen}", previousScreen, navigationData);

            Debug.Log($"Navigated from {previousScreen} to {newScreen}");
        }

        // Test method that can be called from Unity Inspector or other scripts
        [ContextMenu("Test Click Tracking")]
        public void TestClickTracking()
        {
            if (!StashSDK.IsInitialized())
            {
                Debug.LogWarning("SDK not initialized. Initializing with test values...");
                StashSDK.Initialize("test-shop", StashEnvironment.Test);
            }

            var testData = new Dictionary<string, object>
            {
                { "testParam", "testValue" },
                { "timestamp", System.DateTime.UtcNow.ToString("o") }
            };

            StashSDK.TrackClick("test_button", "test_scene", testData);
            Debug.Log("Test click event sent!");
        }
    }
}

