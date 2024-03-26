#if UNITY_ANDROID

using UnityEngine;
using GooglePlayGames;
using GooglePlayGames.BasicApi;

// More details: https://docs.unity.com/ugs/en-us/manual/authentication/manual/platform-signin-google-play-games
public static class PlayGames
{
    public static string AuthCode;

    public static void Activate()
    {
        PlayGamesPlatform.Activate();
        Login();
    }

    private static void Login()
    {
        PlayGamesPlatform.Instance.Authenticate((success) =>
        {
            if (success == SignInStatus.Success)
            {
                string displayName = PlayGamesPlatform.Instance.GetUserDisplayName();
                Debug.Log($"[GOOGLE PLAY GAMES] Login successful - {displayName}");
                
                //Fetch AuthCode for server-side verification.
                RefreshCredentials();
                DeeplinkExample.Instance.DisplayUsername(displayName);
            }
            else
            {
                Debug.Log("[GOOGLE PLAY GAMES] Login unsuccessful.");
            }
        });
    }
    
    public static void RefreshCredentials()
    {
        PlayGamesPlatform.Instance.RequestServerSideAccess(true, code =>
        {
            Debug.Log($"[GOOGLE PLAY GAMES] Credentials refreshed - {code}");
            AuthCode = code;
        });
    }
}

#endif
