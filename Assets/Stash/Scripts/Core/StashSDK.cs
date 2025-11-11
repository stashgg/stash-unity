using System.Collections.Generic;
using UnityEngine;
using Stash.Scripts.Core;

namespace Stash.Core
{
    /// <summary>
    /// Main SDK class for Stash integration
    /// Provides initialization and analytics tracking functionality
    /// </summary>
    public static class StashSDK
    {
        private static StashAnalytics analytics;
        private static string currentShopHandle;
        private static StashEnvironment currentEnvironment;
        private static bool isInitialized = false;

        /// <summary>
        /// Initialize the Stash SDK with analytics support
        /// </summary>
        /// <param name="shopHandle">Your shop handle</param>
        /// <param name="environment">Environment (Production or Test)</param>
        public static void Initialize(string shopHandle, StashEnvironment environment = StashEnvironment.Production)
        {
            if (string.IsNullOrEmpty(shopHandle))
            {
                Debug.LogError("[Stash SDK] Shop handle cannot be null or empty");
                return;
            }

            currentShopHandle = shopHandle;
            currentEnvironment = environment;

            // Initialize analytics
            analytics = new StashAnalytics(shopHandle, environment);
            isInitialized = true;

            Debug.Log($"[Stash SDK] Initialized with shop handle: {shopHandle}, environment: {environment}");
        }

        /// <summary>
        /// Track a click event with optional custom data
        /// </summary>
        /// <param name="elementId">Identifier of the clicked element (required)</param>
        /// <param name="screenName">Name of the current screen (optional)</param>
        /// <param name="customData">Additional custom parameters (optional)</param>
        public static void TrackClick(
            string elementId,
            string screenName = null,
            Dictionary<string, object> customData = null)
        {
            if (!isInitialized || analytics == null)
            {
                Debug.LogError("[Stash SDK] Not initialized. Call StashSDK.Initialize() first.");
                return;
            }

            CoroutineRunner.Instance.StartCoroutine(
                analytics.TrackClickEvent(elementId, screenName, customData)
            );
        }

        /// <summary>
        /// Get the current shop handle
        /// </summary>
        public static string GetShopHandle()
        {
            return currentShopHandle;
        }

        /// <summary>
        /// Get the current environment
        /// </summary>
        public static StashEnvironment GetEnvironment()
        {
            return currentEnvironment;
        }

        /// <summary>
        /// Check if the SDK is initialized
        /// </summary>
        public static bool IsInitialized()
        {
            return isInitialized;
        }
    }
}

