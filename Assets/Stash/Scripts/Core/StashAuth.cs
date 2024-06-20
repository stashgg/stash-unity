using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Stash.Core.Exceptions;
using Stash.Models;
using Stash.Scripts.Core;

namespace Stash.Core
{
    /// <summary>
    /// Linking the player's account to Stash web shop or using 3rd party authentication provider.
    /// </summary>
    
public static class StashAuth
{
    /// <summary>
    /// Links the player's account to Stash account for Apple Account & Google Account.
    /// Requires a valid JWT token issued by any of the supported providers no older than 1 hour.
    /// </summary>
    /// <param name="challenge">Stash code challenge from the deeplink.</param>
    /// <param name="playerId">Player identification, that will be used to identify purchases.</param>
    /// <param name="idToken">Valid JWT token of the player.</param>
    /// <param name="environment">Stash API environment (Defaults to Test).</param>
    /// <returns>Returns a confirmation response, or throws StashAPIRequestError if fails.</returns>
    public static async Task<LinkResponse> LinkAccount(string challenge, 
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
    
        // Create the request body with the challenge and internal user id
        var requestBody = new LinkBody
        {
            codeChallenge = challenge,
            user = new LinkBody.User
            {
                id = playerId
            }
        };
    
        // Set the URL for the link account endpoint
        string requestUrl = environment.GetRootUrl() + StashConstants.LinkAccount;
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

        // Throw an error if the API request was not successful
        throw new StashRequestError(result.StatusCode, result.Data);
    }
    
    
    /// <summary>
    /// Links an Apple Game Center account to the Stash user's account.
    /// Requires a valid response (signature, salt, timestamp, publicKeyUrl) received from GameKit "fetchItems" no older than 1 hour.
    /// </summary>
    /// <param name="challenge">Stash code challenge from the deeplink.</param>
    /// <param name="playerId">Player identification, that will be used to identify purchases.</param>
    /// <param name="bundleId">The bundle ID of the app (CFBundleIdentifier)</param>
    /// <param name="teamPlayerID">GameKit identifier for a player of all the games that you distribute using your Apple developer account.</param>
    /// <param name="signature">The verification signature data that GameKit generates. (Base64 Encoded)</param>
    /// <param name="salt">A random string that GameKit uses to compute the hash and randomize it. (Base64 Encoded)</param>
    /// <param name="publicKeyUrl">The URL for the public encryption key.</param>
    /// <param name="timestamp">The signature’s creation date and time.</param>
    /// <param name="environment">Stash API environment (Defaults to Test).</param>
    /// <returns>A LinkResponse object.</returns>
    public static async Task<LinkResponse> LinkAppleGameCenter(string challenge, string playerId, string bundleId, string teamPlayerID, string signature, 
        string salt, string publicKeyUrl, string timestamp, StashEnvironment environment = StashEnvironment.Test)
    {
        // Create the request body with the challenge and internal user id
        var requestBody = new LinkGameCenterBody()
        {
            codeChallenge = challenge,
            verification = new LinkGameCenterBody.Verification()
            {
                player = new LinkGameCenterBody.Player()
                {
                    bundleId = bundleId,
                    teamPlayerId = teamPlayerID
                },
                response = new LinkGameCenterBody.Response()
                {
                    signature = signature,
                    salt = salt,
                    publicKeyUrl = publicKeyUrl,
                    timestamp = timestamp
                } 
            },
            user = new LinkGameCenterBody.User()
            {
                id = playerId
            }
        };
    
        // Set the URL for the link account endpoint
        string requestUrl = environment.GetRootUrl() + StashConstants.LinkAppleGameCenter;
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
    /// Links a Google Play Games account to the Stash user's account.
    /// Requires valid authorization code generated using "RequestServerSideAccess" from GooglePlayGames no older than 1 hour.
    /// </summary>
    /// <param name="challenge">Stash code challenge from the deeplink.</param>
    /// <param name="playerId">Player identification, that will be used to identify purchases.</param>
    /// <param name="authCode">The authorization code generated using RequestServerSideAccess</param>
    /// <returns>A LinkResponse object.</returns>
    public static async Task<LinkResponse> LinkGooglePlayGames(string challenge, string playerId, string authCode, StashEnvironment environment = StashEnvironment.Test)
    {
        // Create the request body with the challenge and internal user id
        var requestBody = new LinkGooglePlayGamesBody()
        {
            codeChallenge = challenge,
            authCode = authCode,
            user = new LinkGooglePlayGamesBody.User()
            {
                id = playerId
            }
        };
    
        // Set the URL for the link account endpoint
        string requestUrl = environment.GetRootUrl() + StashConstants.LinkGooglePlayGames;
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
    /// Log in to stash account created using 3rd party authentication provider.
    /// For use with bespoke login provider. Not intended for general account linking.
    /// </summary>
    /// <param name="code">Stash code challenge from the log in deeplink.</param>
    /// <param name="playerId">Player identification, that will be used to identify purchases.</param>
    /// <param name="idToken">Valid identification token (OICD) of the player.</param>
    /// <param name="profileImageUrl">URL to the player's profile image/avatar to be displayed during login and on web shop.</param>
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
}
}