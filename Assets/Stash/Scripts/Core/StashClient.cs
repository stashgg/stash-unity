using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Stash.Core.Exceptions;
using Stash.Models;

namespace Stash.Core
{
public static class StashClient
{
    /// <summary>
    /// Links the player's account to Stash account for Apple Account & Google Account.
    /// Requires a valid JWT token issued by any of the supported providers no older than 1 hour.
    /// </summary>
    /// <param name="challenge">Stash code challenge from the deeplink.</param>
    /// <param name="playerId">Player identification, that will be used to identify purchases.</param>
    /// <param name="idToken">Valid JWT token of the player.</param>
    /// <returns>Returns a confirmation response, or throws StashAPIRequestError if fails.</returns>
    public static async Task<LinkResponse> LinkAccount(string challenge, string playerId, string idToken)
    {
        // Create the authorization header with the access token
        RequestHeader authorizationHeader = new()
        {
            Key = "Authorization",
            Value = "Bearer " + idToken
        };
    
        // Create the request body with the challenge and internal user id
        var requestBody = new LinkBody()
        {
            codeChallenge = challenge,
            user = new LinkBody.User
            {
                id = playerId
            }
        };
    
        // Set the URL for the link account endpoint
        const string requestUrl = StashConstants.RootUrlTest + StashConstants.LinkAccount;
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
    /// <returns>A LinkResponse object.</returns>
    public static async Task<LinkResponse> LinkAppleGameCenter(string challenge, string playerId, string bundleId, string teamPlayerID, string signature, 
        string salt, string publicKeyUrl, string timestamp )
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
        const string requestUrl = StashConstants.RootUrlTest + StashConstants.LinkAppleGameCenter;
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
    public static async Task<LinkResponse> LinkGooglePlayGames(string challenge, string playerId, string authCode)
    {
        // Create the request body with the challenge and internal user id
        var requestBody = new LinkGooglePlayGamesBody()
        {
            codeChallenge = challenge,
            verification = new LinkGooglePlayGamesBody.Verification()
            {
                authCode = authCode
            },
            user = new LinkGooglePlayGamesBody.User()
            {
                id = playerId
            }
        };
    
        // Set the URL for the link account endpoint
        const string requestUrl = StashConstants.RootUrlTest + StashConstants.LinkGooglePlayGames;
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
}
}