using System;
using System.Collections;
using Stash.Core;
using Stash.Core.Exceptions;
using Stash.Models;
using TMPro;
using UnityEngine;

public class DeeplinkExample : MonoBehaviour
{
    public static DeeplinkExample Instance { get; private set; }
    
    public TextMeshProUGUI userLabel;
    public GameObject confirmPanel;
    
    private string _stashChallenge;
    private const string InternalPlayerId = "TEST_PLAYER_ID";

    private void Awake()
    {
        //Event handler "OnDeepLinkActivated" is invoked every time the game is launched or resumed via the Stash’s deep link. 
        if (Instance == null)
        {
            Instance = this;
            Application.deepLinkActivated += OnDeepLinkActivated;
            if (!string.IsNullOrEmpty(Application.absoluteURL))
            {
                // Application.absoluteURL not null so process Deep Link on app cold start.
                OnDeepLinkActivated(Application.absoluteURL);
            }
            // Initialize DeepLink Manager global variable.
            else _stashChallenge = "[none]";
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        // Automatically log in player on the game launch based on the platform.
#if UNITY_ANDROID
        PlayGames.Activate();
#endif
        
#if UNITY_IOS
        GameCenter.Activate();
#endif
    }

    
    private void OnDeepLinkActivated(string url)
    {
        //Extract the challenge parameter from the link.
        _stashChallenge = url.Split("/link?challenge=")[1];
        if (!string.IsNullOrEmpty(_stashChallenge))
        {
            Debug.Log("[STASH] Deeplink Code Challenge: " + _stashChallenge);
            
            //We display the confirmation dialog. This is not necessary but recommended linking flow.
            //If user confirms the linking, dialog object will trigger the ProcessLinking() method.
            confirmPanel.SetActive(true);
        }
    }
    
    /// <summary>
    /// Processes linking of the Stash account with Google Play Games or Apple Game Center. 
    /// </summary>
    public async void ProcessLinking()
    {
        confirmPanel.SetActive(false);
        
    #if UNITY_ANDROID
        PlayGames.RefreshCredentials();
        try
        {
            LinkResponse response = await StashAuth.LinkGooglePlayGames(_stashChallenge, InternalPlayerId, PlayGames.AuthCode);
            
            Debug.Log("[STASH][Google Play Games] Account linked successfully.");
        }
        catch (StashRequestError e)
        {
            Debug.LogError($"[STASH][Google Play Games] Account linking failed. Code: {e.Code}, Message: {e.Message}");
        }
    #endif
        
    #if UNITY_IOS 
        await GameCenter.RefreshCredentials();
        try
        {
            LinkResponse response = await StashClient.LinkAppleGameCenter( _stashChallenge, InternalPlayerId, "com.Stash.iosdemo", 
                GameCenter.TeamPlayerID, GameCenter.Signature, GameCenter.Salt, GameCenter.PublicKeyUrl, GameCenter.Timestamp);
            
            Debug.Log("[STASH][Apple Game Center] Account linked successfully.");
        }
        catch (StashRequestError e)
        {
            Debug.LogError($"[STASH][Apple Game Center] Account linking failed. Code: {e.Code}, Message: {e.Message}");
        }
    #endif
    }
    
    public void DisplayUsername(string username)
    {
        userLabel.text = username;
    }
}