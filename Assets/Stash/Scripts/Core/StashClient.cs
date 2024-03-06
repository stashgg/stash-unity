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
    /// Links the players's account to Stash account for Apple Account & Google Account. A valid idToken is required. 
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
        var requestBody = new RequestBody()
        {
            code_challenge = challenge,
            user = new RequestBody.User
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