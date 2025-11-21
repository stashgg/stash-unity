using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Stash.Webshop.Exceptions;
using Stash.Models;

namespace Stash.Webshop
{
    /// <summary>
    /// Provides functionality for linking a player's account to a Stash Account.
    /// For custom sign-in, use StashCustomLogin.
    /// </summary>
    public static class StashLink
    {
        /// <summary>
        /// Links the player's account to Stash account for Apple Account and Google Account.
        /// Requires a valid JWT token issued by any of the supported providers no older than 1 hour.
        /// </summary>
        /// <param name="challenge">Stash code challenge from the deeplink.</param>
        /// <param name="playerId">Player identification that will be used to identify purchases.</param>
        /// <param name="idToken">Valid JWT token of the player.</param>
        /// <param name="environment">Stash API environment (defaults to Test).</param>
        /// <returns>A LinkResponse object containing the confirmation response.</returns>
        /// <exception cref="ArgumentException">Thrown when required parameters are null or empty.</exception>
        /// <exception cref="StashRequestError">Thrown when the API request fails.</exception>
        /// <exception cref="StashParseError">Thrown when the response cannot be parsed.</exception>
        public static async Task<LinkResponse> LinkAccount(
            string challenge,
            string playerId,
            string idToken,
            StashEnvironment environment = StashEnvironment.Test)
        {
            if (string.IsNullOrEmpty(challenge))
                throw new ArgumentException("Challenge cannot be null or empty", nameof(challenge));
            if (string.IsNullOrEmpty(playerId))
                throw new ArgumentException("Player ID cannot be null or empty", nameof(playerId));
            if (string.IsNullOrEmpty(idToken))
                throw new ArgumentException("ID token cannot be null or empty", nameof(idToken));

            var authorizationHeader = new RequestHeader
            {
                Key = "Authorization",
                Value = "Bearer " + idToken
            };

            var requestBody = new LinkBody
            {
                codeChallenge = challenge,
                user = new LinkBody.User
                {
                    id = playerId
                }
            };

            string requestUrl = environment.GetRootUrl() + StashConstants.LinkAccount;
            Response result = await RestClient.Post(requestUrl, JsonUtility.ToJson(requestBody), new List<RequestHeader> { authorizationHeader });

            return ParseLinkResponse(result);
        }
    
    
        /// <summary>
        /// Links an Apple Game Center account to the Stash user's account.
        /// Requires a valid response (signature, salt, timestamp, publicKeyUrl) received from GameKit "fetchItems" no older than 1 hour.
        /// </summary>
        /// <param name="challenge">Stash code challenge from the deeplink.</param>
        /// <param name="playerId">Player identification that will be used to identify purchases.</param>
        /// <param name="bundleId">The bundle ID of the app (CFBundleIdentifier).</param>
        /// <param name="teamPlayerID">GameKit identifier for a player of all the games that you distribute using your Apple developer account.</param>
        /// <param name="signature">The verification signature data that GameKit generates (Base64 Encoded).</param>
        /// <param name="salt">A random string that GameKit uses to compute the hash and randomize it (Base64 Encoded).</param>
        /// <param name="publicKeyUrl">The URL for the public encryption key.</param>
        /// <param name="timestamp">The signature's creation date and time.</param>
        /// <param name="environment">Stash API environment (defaults to Test).</param>
        /// <returns>A LinkResponse object.</returns>
        /// <exception cref="ArgumentException">Thrown when required parameters are null or empty.</exception>
        /// <exception cref="StashRequestError">Thrown when the API request fails.</exception>
        /// <exception cref="StashParseError">Thrown when the response cannot be parsed.</exception>
        public static async Task<LinkResponse> LinkAppleGameCenter(
            string challenge,
            string playerId,
            string bundleId,
            string teamPlayerID,
            string signature,
            string salt,
            string publicKeyUrl,
            string timestamp,
            StashEnvironment environment = StashEnvironment.Test)
        {
            if (string.IsNullOrEmpty(challenge))
                throw new ArgumentException("Challenge cannot be null or empty", nameof(challenge));
            if (string.IsNullOrEmpty(playerId))
                throw new ArgumentException("Player ID cannot be null or empty", nameof(playerId));
            if (string.IsNullOrEmpty(bundleId))
                throw new ArgumentException("Bundle ID cannot be null or empty", nameof(bundleId));
            if (string.IsNullOrEmpty(teamPlayerID))
                throw new ArgumentException("Team Player ID cannot be null or empty", nameof(teamPlayerID));

            var requestBody = new LinkGameCenterBody
            {
                codeChallenge = challenge,
                verification = new LinkGameCenterBody.Verification
                {
                    player = new LinkGameCenterBody.Player
                    {
                        bundleId = bundleId,
                        teamPlayerId = teamPlayerID
                    },
                    response = new LinkGameCenterBody.Response
                    {
                        signature = signature,
                        salt = salt,
                        publicKeyUrl = publicKeyUrl,
                        timestamp = timestamp
                    }
                },
                user = new LinkGameCenterBody.User
                {
                    id = playerId
                }
            };

            string requestUrl = environment.GetRootUrl() + StashConstants.LinkAppleGameCenter;
            Response result = await RestClient.Post(requestUrl, JsonUtility.ToJson(requestBody));

            return ParseLinkResponse(result);
        }
    
        /// <summary>
        /// Links a Google Play Games account to the Stash user's account.
        /// Requires valid authorization code generated using "RequestServerSideAccess" from GooglePlayGames no older than 1 hour.
        /// </summary>
        /// <param name="challenge">Stash code challenge from the deeplink.</param>
        /// <param name="playerId">Player identification that will be used to identify purchases.</param>
        /// <param name="authCode">The authorization code generated using RequestServerSideAccess.</param>
        /// <param name="environment">Stash API environment (defaults to Test).</param>
        /// <returns>A LinkResponse object.</returns>
        /// <exception cref="ArgumentException">Thrown when required parameters are null or empty.</exception>
        /// <exception cref="StashRequestError">Thrown when the API request fails.</exception>
        /// <exception cref="StashParseError">Thrown when the response cannot be parsed.</exception>
        public static async Task<LinkResponse> LinkGooglePlayGames(
            string challenge,
            string playerId,
            string authCode,
            StashEnvironment environment = StashEnvironment.Test)
        {
            if (string.IsNullOrEmpty(challenge))
                throw new ArgumentException("Challenge cannot be null or empty", nameof(challenge));
            if (string.IsNullOrEmpty(playerId))
                throw new ArgumentException("Player ID cannot be null or empty", nameof(playerId));
            if (string.IsNullOrEmpty(authCode))
                throw new ArgumentException("Auth code cannot be null or empty", nameof(authCode));

            var requestBody = new LinkGooglePlayGamesBody
            {
                codeChallenge = challenge,
                authCode = authCode,
                user = new LinkGooglePlayGamesBody.User
                {
                    id = playerId
                }
            };

            string requestUrl = environment.GetRootUrl() + StashConstants.LinkGooglePlayGames;
            Response result = await RestClient.Post(requestUrl, JsonUtility.ToJson(requestBody));

            return ParseLinkResponse(result);
        }

        /// <summary>
        /// Parses a link response from the API.
        /// </summary>
        private static LinkResponse ParseLinkResponse(Response result)
        {
            if (result.StatusCode == 200)
            {
                try
                {
                    return JsonUtility.FromJson<LinkResponse>(result.Data);
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