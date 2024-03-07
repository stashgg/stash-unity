using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Stash.Core.Exceptions;
using Stash.Models;

namespace Stash.Core
{
public class StashClient : MonoBehaviour
{
    private static StashClient _instance;
    public static StashClient Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<StashClient>();
                if (_instance == null)
                {
                    GameObject stashClient = new()
                    {
                        name = nameof(StashClient)
                    };
                    _instance = stashClient.AddComponent<StashClient>();
                    DontDestroyOnLoad(stashClient);
                }
            }
            return _instance;
        }
    }
    
    /// <summary>
    /// Links the player's account to Stash account for Apple Account & Google Account.
    /// Requires a valid JWT identity token issued by Apple or Google.
    /// </summary>
    /// <param name="challenge">Stash challenge from the deeplink.</param>
    /// <param name="internalUserId">Internal user id, will be used to identify the purchases.</param>
    /// <param name="idToken">Valid idToken (JWT) of the player to be linked.</param>
    /// <returns>Returns a confirmation response, or throws StashAPIRequestError if fails.</returns>
    public async Task<LinkResponse> LinkGoogleOrApple(string challenge, string internalUserId, string idToken)
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
            code_challenge = challenge,
            user = new LinkBody.User
            {
                id = internalUserId
            }
        };
    
        // Set the URL for the link account endpoint
        const string requestUrl = StashConstants.APIRootURL + StashConstants.LinkAccount;
        // Make a POST request to link the access token
        Response result = await RestClient.Post(requestUrl, JsonUtility.ToJson(requestBody), new List<RequestHeader> { authorizationHeader });
    
        // Check the response status code
        if (result.StatusCode == 200)
        {
            try
            {
                // Parse the response data into a LinkResponse object
                Debug.Log("[STASH] LinkGoogleOrApple successful Response: " + result.Data);
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
            throw new StashAPIRequestError(result.StatusCode, result.Data);
        }
    }
    
    /// <summary>
    /// Links an Apple Game Center account to the Stash user's account.
    /// Requires signature generated using fetchItems(forIdentityVerificationSignature:)
    /// </summary>
    /// <param name="challenge">The challenge for linking the account.</param>
    /// <param name="bundleId">The bundle ID of the iOS/macOS app. (CFBundleIdentifier)</param>
    /// <param name="signature">The verification signature data that GameKit generates.</param>
    /// <param name="salt">A random string that GameKit uses to compute the hash and randomize it.</param>
    /// <param name="publicKeyUrl">The URL for the public encryption key.</param>
    /// <param name="teamPlayerID">A unique identifier for a player of all the games that you distribute using your developer account.</param>
    /// <param name="timestamp">The signature’s creation date and time.</param>
    /// <returns>A LinkResponse object.</returns>
    public async Task<LinkResponse> LinkAppleGameCenter(string challenge, string bundleId, string signature, 
        string salt, string publicKeyUrl, string teamPlayerID, string timestamp )
    {
        
        // Create the request body with the challenge and internal user id
        var requestBody = new LinkGameCenterBody()
        {
            codeChallenge = challenge,
            player = new LinkGameCenterBody.Player
            {
                bundleId = bundleId,
                playerId = teamPlayerID
            },
            verification = new LinkGameCenterBody.Verification
            {
                signature = signature,
                salt = salt,
                publicKeyUrl = publicKeyUrl,
                timestamp = timestamp
            }
        };
    
        // Set the URL for the link account endpoint
        const string requestUrl = StashConstants.APIRootURL + StashConstants.LnkGameCenter;
        // Make a POST request to link the access token
        Response result = await RestClient.Post(requestUrl, JsonUtility.ToJson(requestBody));
    
        // Check the response status code
        if (result.StatusCode == 200)
        {
            try
            {
                Debug.Log("[STASH] LinkAppleGameCenter successful Response: " + result.Data);
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
            throw new StashAPIRequestError(result.StatusCode, result.Data);
        }
    }
    
}
}