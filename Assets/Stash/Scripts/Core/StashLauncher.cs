using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Stash.Core.Exceptions;
using Stash.Models;
using Stash.Scripts.Core;

namespace Stash.Core
{
    public static class StashLauncher
    {
        public static async Task<CheckoutResponse> Checkout(string itemId,
            string playerId,
            string idToken,
            StashEnvironment environment = StashEnvironment.Test)
        {
            // Create the authorization header with the access token
            RequestHeader authorizationHeader = new()
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

            if (result.StatusCode == 200)
            {
                try
                {
                    CheckoutResponse resultResponse = JsonUtility.FromJson<CheckoutResponse>(result.Data);
                    return resultResponse;
                }
                catch
                {
                    throw new StashParseError(result.Data);
                }
            }

            throw new StashRequestError(result.StatusCode, result.Data);
        }

    }
}