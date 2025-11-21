using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Stash.Webshop.Exceptions;
using Stash.Models;

namespace Stash.Webshop
{
    /// <summary>
    /// Provides functionality for signing in to a Stash web shop using custom authentication providers.
    /// For linking to Stash account, use StashLink.
    /// </summary>
    public static class StashCustomLogin
    {
        /// <summary>
        /// Logs in to a Stash account created using a 3rd party authentication provider.
        /// For use with bespoke login provider. Not intended for general account linking.
        /// </summary>
        /// <param name="code">Stash code challenge from the login deeplink.</param>
        /// <param name="playerId">Player identification that will be used to identify purchases.</param>
        /// <param name="idToken">Valid identification token (OIDC) of the player.</param>
        /// <param name="profileImageUrl">Optional profile image URL of the player.</param>
        /// <param name="environment">Stash API environment (defaults to Test).</param>
        /// <returns>A LinkResponse object containing the confirmation response.</returns>
        /// <exception cref="ArgumentException">Thrown when required parameters are null or empty.</exception>
        /// <exception cref="StashRequestError">Thrown when the API request fails.</exception>
        /// <exception cref="StashParseError">Thrown when the response cannot be parsed.</exception>
        public static async Task<LinkResponse> CustomLogin(
            string code,
            string playerId,
            string idToken,
            string profileImageUrl = null,
            StashEnvironment environment = StashEnvironment.Test)
        {
            if (string.IsNullOrEmpty(code))
                throw new ArgumentException("Code cannot be null or empty", nameof(code));
            if (string.IsNullOrEmpty(playerId))
                throw new ArgumentException("Player ID cannot be null or empty", nameof(playerId));
            if (string.IsNullOrEmpty(idToken))
                throw new ArgumentException("ID token cannot be null or empty", nameof(idToken));

            var authorizationHeader = new RequestHeader
            {
                Key = "Authorization",
                Value = "Bearer " + idToken
            };

            var requestBody = new CustomLoginBody
            {
                code = code,
                user = new CustomLoginBody.User
                {
                    id = playerId,
                    profile_image_url = profileImageUrl
                }
            };

            string requestUrl = environment.GetRootUrl() + StashConstants.CustomLogin;
            Response result = await RestClient.Post(requestUrl, JsonUtility.ToJson(requestBody), new List<RequestHeader> { authorizationHeader });

            return ParseLinkResponse(result);
        }
    
        /// <summary>
        /// Logs in to Stash web shop via Apple Game Center account.
        /// Requires a valid response (signature, salt, timestamp, publicKeyUrl) received from GameKit "fetchItems" no older than 1 hour.
        /// </summary>
        /// <param name="code">Stash code challenge from the deeplink.</param>
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
            string code,
            string playerId,
            string bundleId,
            string teamPlayerID,
            string signature,
            string salt,
            string publicKeyUrl,
            string timestamp,
            StashEnvironment environment = StashEnvironment.Test)
        {
            if (string.IsNullOrEmpty(code))
                throw new ArgumentException("Code cannot be null or empty", nameof(code));
            if (string.IsNullOrEmpty(playerId))
                throw new ArgumentException("Player ID cannot be null or empty", nameof(playerId));
            if (string.IsNullOrEmpty(bundleId))
                throw new ArgumentException("Bundle ID cannot be null or empty", nameof(bundleId));
            if (string.IsNullOrEmpty(teamPlayerID))
                throw new ArgumentException("Team Player ID cannot be null or empty", nameof(teamPlayerID));

            var requestBody = new LoginGameCenterBody
            {
                code = code,
                verification = new LoginGameCenterBody.Verification
                {
                    player = new LoginGameCenterBody.Player
                    {
                        bundleId = bundleId,
                        teamPlayerId = teamPlayerID
                    },
                    response = new LoginGameCenterBody.Response
                    {
                        signature = signature,
                        salt = salt,
                        publicKeyUrl = publicKeyUrl,
                        timestamp = timestamp
                    }
                },
                user = new LoginGameCenterBody.User
                {
                    id = playerId
                }
            };

            string requestUrl = environment.GetRootUrl() + StashConstants.LoginAppleGameCenter;
            Response result = await RestClient.Post(requestUrl, JsonUtility.ToJson(requestBody));

            return ParseLinkResponse(result);
        }
    
        /// <summary>
        /// Logs in to Stash web shop via Google Play Games account.
        /// Requires valid authorization code generated using "RequestServerSideAccess" from GooglePlayGames no older than 1 hour.
        /// </summary>
        /// <param name="code">Stash code challenge from the deeplink.</param>
        /// <param name="playerId">Player identification that will be used to identify purchases.</param>
        /// <param name="authCode">The authorization code generated using RequestServerSideAccess.</param>
        /// <param name="environment">Stash API environment (defaults to Test).</param>
        /// <returns>A LinkResponse object.</returns>
        /// <exception cref="ArgumentException">Thrown when required parameters are null or empty.</exception>
        /// <exception cref="StashRequestError">Thrown when the API request fails.</exception>
        /// <exception cref="StashParseError">Thrown when the response cannot be parsed.</exception>
        public static async Task<LinkResponse> LinkGooglePlayGames(
            string code,
            string playerId,
            string authCode,
            StashEnvironment environment = StashEnvironment.Test)
        {
            if (string.IsNullOrEmpty(code))
                throw new ArgumentException("Code cannot be null or empty", nameof(code));
            if (string.IsNullOrEmpty(playerId))
                throw new ArgumentException("Player ID cannot be null or empty", nameof(playerId));
            if (string.IsNullOrEmpty(authCode))
                throw new ArgumentException("Auth code cannot be null or empty", nameof(authCode));

            var requestBody = new LoginGooglePlayGamesBody
            {
                code = code,
                authCode = authCode,
                user = new LoginGooglePlayGamesBody.User
                {
                    id = playerId
                }
            };

            string requestUrl = environment.GetRootUrl() + StashConstants.LoginGooglePlayGames;
            Response result = await RestClient.Post(requestUrl, JsonUtility.ToJson(requestBody));

            return ParseLinkResponse(result);
        }

        /// <summary>
        /// Logs in to Stash web shop via Facebook account.
        /// Requires valid access token generated from Facebook login.
        /// </summary>
        /// <param name="code">Stash code challenge from the deeplink.</param>
        /// <param name="playerId">Player identification that will be used to identify purchases.</param>
        /// <param name="appId">Facebook app ID.</param>
        /// <param name="accessToken">Facebook access token.</param>
        /// <param name="environment">Stash API environment (defaults to Test).</param>
        /// <returns>A LinkResponse object.</returns>
        /// <exception cref="ArgumentException">Thrown when required parameters are null or empty.</exception>
        /// <exception cref="StashRequestError">Thrown when the API request fails.</exception>
        /// <exception cref="StashParseError">Thrown when the response cannot be parsed.</exception>
        public static async Task<LinkResponse> LinkFacebook(
            string code,
            string playerId,
            string appId,
            string accessToken,
            StashEnvironment environment = StashEnvironment.Test)
        {
            if (string.IsNullOrEmpty(code))
                throw new ArgumentException("Code cannot be null or empty", nameof(code));
            if (string.IsNullOrEmpty(playerId))
                throw new ArgumentException("Player ID cannot be null or empty", nameof(playerId));
            if (string.IsNullOrEmpty(appId))
                throw new ArgumentException("App ID cannot be null or empty", nameof(appId));
            if (string.IsNullOrEmpty(accessToken))
                throw new ArgumentException("Access token cannot be null or empty", nameof(accessToken));

            var requestBody = new LoginFacebook
            {
                code = code,
                appId = appId,
                inputToken = accessToken,
                user = new LoginFacebook.User
                {
                    id = playerId
                }
            };

            string requestUrl = environment.GetRootUrl() + StashConstants.LoginFacebook;
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
