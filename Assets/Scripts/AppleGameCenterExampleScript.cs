using System;
using System.Threading.Tasks;
using UnityEngine;
using Apple.GameKit;
using Stash.Core;
using TMPro;
using Unity.Services.Authentication;
using Unity.Services.Core;


//https://docs.unity.com/ugs/en-us/manual/authentication/manual/platform-signin-apple-game-center
public class AppleGameCenterExampleScript : MonoBehaviour
{
    public TextMeshProUGUI UsernameLabel;
    
    string Signature;
    string TeamPlayerID;
    string Salt;
    string Timestamp;
    string GamePlayerID;
    
    async void Start()
    {
        try
        {
            await UnityServices.InitializeAsync();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
        
        await Login();
    }

    public async Task Login()
    {
        if (!GKLocalPlayer.Local.IsAuthenticated)
        {
            // Perform the authentication.
            var player = await GKLocalPlayer.Authenticate();
            Debug.Log($"GameKit Authentication: player {player}");

            // Grab the display name.
            var localPlayer = GKLocalPlayer.Local;
            Debug.Log($"Local Player: {localPlayer.DisplayName}");
            UsernameLabel.text = localPlayer.DisplayName;

            // Fetch the items.
            var fetchItemsResponse =  await GKLocalPlayer.Local.FetchItems();
            Signature = Convert.ToBase64String(fetchItemsResponse.Signature);
            Salt = Convert.ToBase64String(fetchItemsResponse.Salt);
            Timestamp = fetchItemsResponse.Timestamp.ToString();
            TeamPlayerID = localPlayer.TeamPlayerId;
            GamePlayerID = localPlayer.GamePlayerId;
            
            // Send signature, teamPlayerId, publicKeyURL, salt, timestamp to Stash.
            Debug.Log($"GameKit Signature: {Signature}");
            Debug.Log($"GameKit Salt: {Salt}");
            Debug.Log($"GameKit PublicKey URL: {fetchItemsResponse.PublicKeyUrl}");
            Debug.Log($"GameKit TeamPlayerID: {TeamPlayerID} / GamePlayerID: {GamePlayerID}");
            Debug.Log($"GameKit Timestamp: {Timestamp}");
            
        }
        else
        {
            Debug.Log("AppleGameCenter player already logged in.");
        }
    }
    
}