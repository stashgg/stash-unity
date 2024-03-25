#if UNITY_IOS

using System;
using System.Threading.Tasks;
using UnityEngine;
using Apple.GameKit;
using Apple.GameKit.Players;

// More details: https://docs.unity.com/ugs/en-us/manual/authentication/manual/platform-signin-apple-game-center
public static class GameCenter
{
    public static string Signature;
    public static string TeamPlayerID;
    public static string Salt;
    public static string PublicKeyUrl;
    public static string Timestamp;

    public static async void Activate()
    {
        await Login();
    }

    private static async Task Login()
    {
        if (!GKLocalPlayer.Local.IsAuthenticated)
        {
            // Perform the authentication.
            var player = await GKLocalPlayer.Authenticate();
            Debug.Log($"[APPLE GAME CENTER] Login successful - {player.DisplayName}");
            
            // Grab the team player id.
            var localPlayer = GKLocalPlayer.Local;
            TeamPlayerID = localPlayer.TeamPlayerId;

            //Fetch signature, salt, timestamp and public key for server-side verification.
            RefreshCredentials();
            DeeplinkExample.Instance.DisplayUsername(player.DisplayName);
        }
        else
        {
            Debug.Log("[APPLE GAME CENTER] Already logged in.");
        }
    }

    public static async Task RefreshCredentials()
    {
        var fetchItemsResponse =  await GKLocalPlayer.Local.FetchItems();
        Debug.Log($"[APPLE GAME CENTER] Credentials refreshed");
        
        Signature = Convert.ToBase64String(fetchItemsResponse.Signature);
        Salt = Convert.ToBase64String(fetchItemsResponse.Salt);
        PublicKeyUrl = fetchItemsResponse.PublicKeyUrl;
        Timestamp = fetchItemsResponse.Timestamp.ToString(); 
    }
}

#endif