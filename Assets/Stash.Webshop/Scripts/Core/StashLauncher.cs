using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Stash.Webshop.Exceptions;
using Stash.Models;

namespace Stash.Webshop
{
    /// <summary>
    /// Provides functionality for Stash Launcher-specific operations including loyalty URLs and checkout.
    /// </summary>
    public static class StashLauncher
    {
        /// <summary>
        /// Gets the loyalty URL for a player using the Stash Launcher API.
        /// </summary>
        /// <param name="playerId">The player identification.</param>
        /// <param name="idToken">The ID token for Bearer authentication.</param>
        /// <param name="environment">The Stash environment (defaults to Test).</param>
        /// <returns>A LoyaltyUrlResponse object containing the loyalty URL.</returns>
        /// <exception cref="ArgumentException">Thrown when required parameters are null or empty.</exception>
        /// <exception cref="StashRequestError">Thrown when the API request fails.</exception>
        /// <exception cref="StashParseError">Thrown when the response cannot be parsed.</exception>
        public static async Task<LoyaltyUrlResponse> GetLoyaltyUrl(
            string playerId,
            string idToken,
            StashEnvironment environment = StashEnvironment.Test)
        {
            if (string.IsNullOrEmpty(playerId))
                throw new ArgumentException("Player ID cannot be null or empty", nameof(playerId));
            if (string.IsNullOrEmpty(idToken))
                throw new ArgumentException("ID token cannot be null or empty", nameof(idToken));

            var authorizationHeader = new RequestHeader
            {
                Key = "Authorization",
                Value = "Bearer " + idToken
            };

            var requestBody = new LoyaltyUrlBody
            {
                user = new LoyaltyUrlBody.User
                {
                    id = playerId
                }
            };

            string requestUrl = environment.GetRootUrl() + StashConstants.LauncherLoyaltyUrl;
            Response result = await RestClient.Post(requestUrl, JsonUtility.ToJson(requestBody), new List<RequestHeader> { authorizationHeader });

            return ParseLoyaltyUrlResponse(result);
        }

        /// <summary>
        /// Creates a checkout link for an item using the Stash Launcher API.
        /// </summary>
        /// <param name="itemId">The ID of the item to purchase.</param>
        /// <param name="playerId">The player identification.</param>
        /// <param name="idToken">The ID token for Bearer authentication.</param>
        /// <param name="environment">The Stash environment (defaults to Test).</param>
        /// <returns>A CheckoutResponse object containing the checkout URL.</returns>
        /// <exception cref="ArgumentException">Thrown when required parameters are null or empty.</exception>
        /// <exception cref="StashRequestError">Thrown when the API request fails.</exception>
        /// <exception cref="StashParseError">Thrown when the response cannot be parsed.</exception>
        public static async Task<CheckoutResponse> Checkout(
            string itemId,
            string playerId,
            string idToken,
            StashEnvironment environment = StashEnvironment.Test)
        {
            if (string.IsNullOrEmpty(itemId))
                throw new ArgumentException("Item ID cannot be null or empty", nameof(itemId));
            if (string.IsNullOrEmpty(playerId))
                throw new ArgumentException("Player ID cannot be null or empty", nameof(playerId));
            if (string.IsNullOrEmpty(idToken))
                throw new ArgumentException("ID token cannot be null or empty", nameof(idToken));

            var authorizationHeader = new RequestHeader
            {
                Key = "Authorization",
                Value = "Bearer " + idToken
            };

            var requestBody = new CheckoutBody
            {
                item = new CheckoutBody.Item
                {
                    id = itemId
                },
                user = new CheckoutBody.User
                {
                    id = playerId
                }
            };

            string requestUrl = environment.GetRootUrl() + StashConstants.LauncherCheckout;
            Response result = await RestClient.Post(requestUrl, JsonUtility.ToJson(requestBody), new List<RequestHeader> { authorizationHeader });

            return ParseCheckoutResponse(result);
        }

        /// <summary>
        /// Parses a loyalty URL response from the API.
        /// </summary>
        private static LoyaltyUrlResponse ParseLoyaltyUrlResponse(Response result)
        {
            if (result.StatusCode == 200)
            {
                try
                {
                    return JsonUtility.FromJson<LoyaltyUrlResponse>(result.Data);
                }
                catch (Exception ex)
                {
                    throw new StashParseError($"{result.Data}. Error: {ex.Message}");
                }
            }

            throw new StashRequestError(result.StatusCode, result.Data);
        }

        /// <summary>
        /// Parses a checkout response from the API.
        /// </summary>
        private static CheckoutResponse ParseCheckoutResponse(Response result)
        {
            if (result.StatusCode == 200)
            {
                try
                {
                    return JsonUtility.FromJson<CheckoutResponse>(result.Data);
                }
                catch (Exception ex)
                {
                    throw new StashParseError($"{result.Data}. Error: {ex.Message}");
                }
            }

            throw new StashRequestError(result.StatusCode, result.Data);
        }
    }
}