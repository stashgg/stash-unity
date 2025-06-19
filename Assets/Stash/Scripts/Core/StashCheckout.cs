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
    /// Platform enumeration for user identification.
    /// </summary>
    public enum Platform
    {
        Undefined = 0,
        IOS = 1,
        Android = 2
    }

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
        /// Creates a checkout link for Stash payments using the new simplified API.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <param name="validatedEmail">The validated email of the user.</param>
        /// <param name="shopHandle">The handle of the shop.</param>
        /// <param name="itemId">The ID of the item to purchase.</param>
        /// <param name="apiKey">The Stash API key.</param>
        /// <param name="environment">The Stash environment (defaults to Test).</param>
        /// <returns>A response containing the URL and ID.</returns>
        public static async Task<(string url, string id)> CreateCheckoutLink(
            string userId,
            string validatedEmail,
            string shopHandle,
            string itemId,
            string apiKey,
            StashEnvironment environment = StashEnvironment.Test)
        {
            // Create the authorization header with the API key
            RequestHeader authorizationHeader = new()
            {
                Key = "X-Stash-Api-Key",
                Value = apiKey
            };

            // Create the request body JSON string with the new simplified structure
            string requestBody = $"{{\"user\":{{\"id\":\"{userId}\",\"validated_email\":\"{validatedEmail}\",\"platform\":\"IOS\"}},\"shop_handle\":\"{shopHandle}\",\"item_id\":\"{itemId}\"}}";

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
        /// This method is kept for backward compatibility but now uses the simplified API.
        /// Only the first item from the array will be used.
        /// </summary>
        /// <param name="externalUserId">The external user ID.</param>
        /// <param name="validatedEmail">The validated email of the user.</param>
        /// <param name="displayName">The display name of the user (ignored in new API).</param>
        /// <param name="avatarIconUrl">The URL of the user's avatar icon (ignored in new API).</param>
        /// <param name="profileUrl">The URL of the user's profile (ignored in new API).</param>
        /// <param name="shopHandle">The handle of the shop.</param>
        /// <param name="currency">The currency to use for the checkout (ignored in new API).</param>
        /// <param name="items">Array of checkout items (only the first item's ID will be used).</param>
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
            if (items == null || items.Length == 0)
            {
                throw new ArgumentException("At least one item must be provided");
            }

            // Use the first item's ID for the new API
            string itemId = items[0].id;

            // Call the new simplified method
            return await CreateCheckoutLink(
                externalUserId,
                validatedEmail,
                shopHandle,
                itemId,
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

        /// <summary>
        /// Creates a checkout link for Stash payments using the client API with Bearer token authentication.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <param name="shopHandle">The handle of the shop.</param>
        /// <param name="itemId">The ID of the item to purchase.</param>
        /// <param name="idToken">The ID token for Bearer authentication (Cognito).</param>
        /// <param name="environment">The Stash environment (defaults to Test).</param>
        /// <param name="platform">The platform of the user (defaults to Undefined).</param>
        /// <param name="validatedEmail">Optional validated email of the user.</param>
        /// <param name="profileImageUrl">Optional URL of the user's profile image.</param>
        /// <param name="displayName">Optional display name of the user.</param>
        /// <param name="regionCode">Optional region code for the user (ISO 3166-1 Alpha-3 format, e.g., "USA", "GBR").</param>
        /// <returns>A response containing the URL and ID.</returns>
        public static async Task<(string url, string id)> CreateCheckoutLinkClient(
            string userId,
            string shopHandle,
            string itemId,
            string idToken,
            StashEnvironment environment = StashEnvironment.Test,
            Platform platform = Platform.Undefined,
            string validatedEmail = null,
            string profileImageUrl = null,
            string displayName = null,
            string regionCode = null)
        {
            // Create the authorization header with Bearer token
            RequestHeader authorizationHeader = new()
            {
                Key = "Authorization",
                Value = $"Bearer {idToken}"
            };

            // Build the user JSON object with all optional fields
            var userFields = new List<string>
            {
                $"\"id\":\"{userId}\"",
                $"\"platform\":\"{platform.ToString().ToUpper()}\""
            };

            if (!string.IsNullOrEmpty(validatedEmail))
            {
                userFields.Add($"\"validated_email\":\"{validatedEmail}\"");
            }

            if (!string.IsNullOrEmpty(profileImageUrl))
            {
                userFields.Add($"\"profile_image_url\":\"{profileImageUrl}\"");
            }

            if (!string.IsNullOrEmpty(displayName))
            {
                userFields.Add($"\"display_name\":\"{displayName}\"");
            }

            if (!string.IsNullOrEmpty(regionCode))
            {
                userFields.Add($"\"region_code\":\"{regionCode}\"");
            }

            string userJson = "{" + string.Join(",", userFields) + "}";
            string requestBody = $"{{\"user\":{userJson},\"shop_handle\":\"{shopHandle}\",\"item_id\":\"{itemId}\"}}";

            // Set the URL for the client checkout link creation endpoint
            string requestUrl = environment.GetRootUrl() + "/sdk/client/checkout_links/generate_quick_pay_url";

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
    }
}