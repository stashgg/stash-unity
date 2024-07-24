using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Stash.Core.Exceptions;
using Stash.Models;
using Stash.Scripts.Core;

namespace Stash.Core
{
    /// <summary>
    /// Sign in to a Stash web shop using custom auth provider.
    /// For linking to Stash account, use StashLink.
    /// </summary>
    
public static class StashCustomLogin
{
    /// <summary>
    /// Log in to stash account created using 3rd party authentication provider.
    /// For use with bespoke login provider. Not intended for general account linking.
    /// </summary>
    /// <param name="code">Stash code challenge from the log in deeplink.</param>
    /// <param name="playerId">Player identification, that will be used to identify purchases.</param>
    /// <param name="idToken">Valid identification token (OICD) of the player.</param>
    /// <param name="profileImageUrl">URL to the player's profile image/avatar to be displayed during login and on web shop.</param>
    /// <param name="environment">Stash API environment (Defaults to Test).</param>
    /// <returns>Returns a confirmation response, or throws StashAPIRequestError if fails.</returns>
    public static async Task<LinkResponse> CustomLogin(string code, string playerId, string idToken, string profileImageUrl, StashEnvironment environment = StashEnvironment.Test)
    {
        // Create the authorization header with the access token
        RequestHeader authorizationHeader = new()
        {
            Key = "Authorization",
            Value = "Bearer " + idToken
        };
    
        // Create the request body with the challenge and internal user id
        var requestBody = new CustomLoginBody()
        {
            code = code,
            user = new CustomLoginBody.User
            {
                id = playerId,
                profile_image_url = profileImageUrl
            }
        };
    
        // Set the URL for the link account endpoint
        string requestUrl = environment.GetRootUrl() + StashConstants.CustomLogin;
        // Make a POST request to link the access token
        Response result = await RestClient.Post(requestUrl, JsonUtility.ToJson(requestBody), new List<RequestHeader> { authorizationHeader });
    
        // Check the response status code
        if (result.StatusCode == 200)
        {
            try
            {
                // Parse the response data into a LinkResponse object
                LinkResponse resultResponse = JsonUtility.FromJson<LinkResponse>(result.Data);
                return resultResponse;
            }
            catch
            {
                // Throw an error if there is an issue parsing the response data
                throw new StashParseError(result.Data);
            }
        }
        else
        {
            // Throw an error if the API request was not successful
            throw new StashRequestError(result.StatusCode, result.Data);
        }
    }
    
    /// <summary>
    /// Logs in to stash web shop via Apple Game Center account.
    /// Requires a valid response (signature, salt, timestamp, publicKeyUrl) received from GameKit "fetchItems" no older than 1 hour.
    /// </summary>
    /// <param name="code">Stash code challenge from the deeplink.</param>
    /// <param name="playerId">Player identification, that will be used to identify purchases.</param>
    /// <param name="bundleId">The bundle ID of the app (CFBundleIdentifier)</param>
    /// <param name="teamPlayerID">GameKit identifier for a player of all the games that you distribute using your Apple developer account.</param>
    /// <param name="signature">The verification signature data that GameKit generates. (Base64 Encoded)</param>
    /// <param name="salt">A random string that GameKit uses to compute the hash and randomize it. (Base64 Encoded)</param>
    /// <param name="publicKeyUrl">The URL for the public encryption key.</param>
    /// <param name="timestamp">The signature’s creation date and time.</param>
    /// <param name="environment">Stash API environment (Defaults to Test).</param>
    /// <returns>A LinkResponse object.</returns>
    public static async Task<LinkResponse> LinkAppleGameCenter(string code, string playerId, string bundleId, string teamPlayerID, string signature, 
        string salt, string publicKeyUrl, string timestamp, StashEnvironment environment = StashEnvironment.Test)
    {
        // Create the request body with the challenge and internal user id
        var requestBody = new LoginGameCenterBody()
        {
            code = code,
            verification = new LoginGameCenterBody.Verification()
            {
                player = new LoginGameCenterBody.Player()
                {
                    bundleId = bundleId,
                    teamPlayerId = teamPlayerID
                },
                response = new LoginGameCenterBody.Response()
                {
                    signature = signature,
                    salt = salt,
                    publicKeyUrl = publicKeyUrl,
                    timestamp = timestamp
                } 
            },
            user = new LoginGameCenterBody.User()
            {
                id = playerId
            }
        };
    
        // Set the URL for the link account endpoint
        string requestUrl = environment.GetRootUrl() + StashConstants.LoginAppleGameCenter;
        // Make a POST request to link the access token
        Response result = await RestClient.Post(requestUrl, JsonUtility.ToJson(requestBody));
    
        // Check the response status code
        if (result.StatusCode == 200)
        {
            try
            {
                LinkResponse resultResponse = JsonUtility.FromJson<LinkResponse>(result.Data);
                return resultResponse;
            }
            catch
            {
                // Throw an error if there is an issue parsing the response data
                throw new StashParseError(result.Data);
            }
        }
        else
        {
            // Throw an error if the API request was not successful
            throw new StashRequestError(result.StatusCode, result.Data);
        }
    }
    
    
    /// <summary>
    /// Logs in to stash web shop via Google Play Games account.
    /// Requires valid authorization code generated using "RequestServerSideAccess" from GooglePlayGames no older than 1 hour.
    /// </summary>
    /// <param name="code">Stash code challenge from the deeplink.</param>
    /// <param name="playerId">Player identification, that will be used to identify purchases.</param>
    /// <param name="authCode">The authorization code generated using RequestServerSideAccess</param>
    /// <param name="environment">Stash API environment (Defaults to Test).</param>
    /// <returns>A LinkResponse object.</returns>
    public static async Task<LinkResponse> LinkGooglePlayGames(string code, string playerId, string authCode, StashEnvironment environment = StashEnvironment.Test)
    {
        // Create the request body with the challenge and internal user id
        var requestBody = new LoginGooglePlayGamesBody()
        {
            code = code,
            authCode = authCode,
            user = new LoginGooglePlayGamesBody.User()
            {
                id = playerId
            }
        };
    
        // Set the URL for the link account endpoint
        string requestUrl = environment.GetRootUrl() + StashConstants.LoginGooglePlayGames;
        // Make a POST request to link the access token
        Response result = await RestClient.Post(requestUrl, JsonUtility.ToJson(requestBody));
    
        // Check the response status code
        if (result.StatusCode == 200)
        {
            try
            {
                Debug.Log("[RESPONSE RAW] " + result.Data);
                LinkResponse resultResponse = JsonUtility.FromJson<LinkResponse>(result.Data);
                return resultResponse;
            }
            catch
            {
                // Throw an error if there is an issue parsing the response data
                throw new StashParseError(result.Data);
            }
        }
        else
        {
            // Throw an error if the API request was not successful
            throw new StashRequestError(result.StatusCode, result.Data);
        }
    }
    
    
    
    /// <summary>
    /// Logs in to stash web shop via Facebook account.
    /// Requires valid access token generated from Facebook login.
    /// </summary>
    /// <param name="code">Stash code challenge from the deeplink.</param>
    /// <param name="playerId">Player identification, that will be used to identify purchases.</param>
    /// <param name="appId">Facebook app id.</param>
    /// <param name="accessToken">Facebook access token.</param>
    /// <param name="environment">Stash API environment (Defaults to Test).</param>
    /// <returns>A LinkResponse object.</returns>
    public static async Task<LinkResponse> LinkFacebook(string code, string playerId, string appId, string accessToken, StashEnvironment environment = StashEnvironment.Test)
    {
        // Create the request body with the challenge and internal user id
        var requestBody = new LoginFacebook()
        {
            code = code,
            appId = appId,
            inputToken = accessToken,
            user = new LoginFacebook.User()
            {
                id = playerId
            }
        };
    
        // Set the URL for the link account endpoint
        string requestUrl = environment.GetRootUrl() + StashConstants.LoginFacebook;
        // Make a POST request to link the access token
        Response result = await RestClient.Post(requestUrl, JsonUtility.ToJson(requestBody));
    
        // Check the response status code
        if (result.StatusCode == 200)
        {
            try
            {
                Debug.Log("[RESPONSE RAW] " + result.Data);
                LinkResponse resultResponse = JsonUtility.FromJson<LinkResponse>(result.Data);
                return resultResponse;
            }
            catch
            {
                // Throw an error if there is an issue parsing the response data
                throw new StashParseError(result.Data);
            }
        }
        else
        {
            // Throw an error if the API request was not successful
            throw new StashRequestError(result.StatusCode, result.Data);
        }
    }
}
}