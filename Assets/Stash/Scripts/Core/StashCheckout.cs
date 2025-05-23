using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Stash.Core.Exceptions;
using Stash.Models;
using Stash.Scripts.Core;
using System;

namespace Stash.Core
{
    /// <summary>
    /// Handles the creation of checkout links for Stash payments and opening URLs in browsers.
    /// </summary>
    public static class StashCheckout
    {
        [Serializable]
        private class CheckoutResponse
        {
            public string url;
            public string id;
        }
        
        /// <summary>
        /// Creates a checkout link for Stash payments.
        /// </summary>
        /// <param name="externalUserId">The external user ID.</param>
        /// <param name="validatedEmail">The validated email of the user.</param>
        /// <param name="displayName">The display name of the user.</param>
        /// <param name="avatarIconUrl">The URL of the user's avatar icon.</param>
        /// <param name="profileUrl">The URL of the user's profile.</param>
        /// <param name="shopHandle">The handle of the shop.</param>
        /// <param name="currency">The currency to use for the checkout.</param>
        /// <param name="items">The list of items as JSON string.</param>
        /// <param name="apiKey">The Stash API key.</param>
        /// <param name="environment">The Stash environment (defaults to Test).</param>
        /// <returns>A response containing the URL and ID.</returns>
        public static async Task<(string url, string id)> CreateCheckoutLink(
            string externalUserId,
            string validatedEmail,
            string displayName,
            string avatarIconUrl,
            string profileUrl,
            string shopHandle,
            string currency,
            string itemsJson,
            string apiKey,
            StashEnvironment environment = StashEnvironment.Test)
        {
            // Create the authorization header with the API key
            RequestHeader authorizationHeader = new()
            {
                Key = "X-Stash-Api-Key",
                Value = apiKey
            };

            // Create the complete request body JSON string
            string requestBody = $"{{\"externalUser\":{{\"id\":\"{externalUserId}\",\"validatedEmail\":\"{validatedEmail}\",\"displayName\":\"{displayName}\",\"avatarIconUrl\":\"{avatarIconUrl}\",\"profileUrl\":\"{profileUrl}\"}},\"shopHandle\":\"{shopHandle}\",\"currency\":\"{currency}\",\"items\":{itemsJson}}}";

            // Set the URL for the checkout link creation endpoint
            string requestUrl = environment.GetRootUrl() + StashConstants.CheckoutLinks;

            // Make a POST request to create the checkout link
            Response result = await RestClient.Post(requestUrl, requestBody, new List<RequestHeader> { authorizationHeader });

            // Check the response status code
            if (result.StatusCode == 200)
            {
                try
    {
                    // Parse the response data into a CheckoutResponse object
                    CheckoutResponse checkoutResponse = JsonUtility.FromJson<CheckoutResponse>(result.Data);
                    return (checkoutResponse.url, checkoutResponse.id);
                }
                catch (Exception ex)
                {
                    // Throw an error if there is an issue parsing the response data
                    throw new StashParseError($"{result.Data}. Error: {ex.Message}");
                }
            }

            // Throw an error if the API request was not successful
            throw new StashRequestError(result.StatusCode, result.Data);
        }

        /// <summary>
        /// Creates a checkout link for Stash payments with structured item data.
        /// </summary>
        /// <param name="externalUserId">The external user ID.</param>
        /// <param name="validatedEmail">The validated email of the user.</param>
        /// <param name="displayName">The display name of the user.</param>
        /// <param name="avatarIconUrl">The URL of the user's avatar icon.</param>
        /// <param name="profileUrl">The URL of the user's profile.</param>
        /// <param name="shopHandle">The handle of the shop.</param>
        /// <param name="currency">The currency to use for the checkout.</param>
        /// <param name="items">Array of checkout items.</param>
        /// <param name="apiKey">The Stash API key.</param>
        /// <param name="environment">The Stash environment (defaults to Test).</param>
        /// <returns>A response containing the URL and ID.</returns>
        public static async Task<(string url, string id)> CreateCheckoutLinkWithItems(
            string externalUserId,
            string validatedEmail,
            string displayName,
            string avatarIconUrl,
            string profileUrl,
            string shopHandle,
            string currency,
            CheckoutItemData[] items,
            string apiKey,
            StashEnvironment environment = StashEnvironment.Test)
        {
            // Convert the items to a JSON array string
            string itemsJson = "[";
            for (int i = 0; i < items.Length; i++)
        {
                var item = items[i];
                itemsJson += $"{{\"id\":\"{item.id}\",\"pricePerItem\":\"{item.pricePerItem}\",\"quantity\":{item.quantity},\"imageUrl\":\"{item.imageUrl}\",\"name\":\"{item.name}\",\"description\":\"{item.description}\"}}";
                
                if (i < items.Length - 1)
                {
                    itemsJson += ",";
                }
            }
            itemsJson += "]";

            // Call the main method with the items JSON
            return await CreateCheckoutLink(
                externalUserId,
                validatedEmail,
                displayName,
                avatarIconUrl,
                profileUrl,
                shopHandle,
                currency,
                itemsJson,
                apiKey,
                environment);
        }

        /// <summary>
        /// Data structure for a checkout item.
        /// </summary>
        public struct CheckoutItemData
        {
            public string id;
            public string pricePerItem;
            public int quantity;
            public string imageUrl;
            public string name;
            public string description;
        }

        /// <summary>
        /// Opens a URL in the browser based on the current platform (iOS, Android, Desktop).
        /// </summary>
        /// <param name="url">The URL to open.</param>
        public static void OpenUrlInBrowser(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                Debug.LogError("[STASH] Cannot open empty URL in browser");
                return;
            }

            try
            {
                #if UNITY_EDITOR || UNITY_STANDALONE
                // For desktop platforms
                Application.OpenURL(url);
                #elif UNITY_ANDROID
                // For Android
                Application.OpenURL(url);
                #elif UNITY_IOS
                // For iOS
                Application.OpenURL(url);
                #else
                // Fallback for other platforms
                Application.OpenURL(url);
                #endif

                Debug.Log($"[STASH] Opened URL in browser: {url}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[STASH] Error opening URL in browser: {e.Message}");
            }
        }
    }
}