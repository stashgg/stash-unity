using System;
using System.Collections;
using Stash.Core;
using Stash.Core.Exceptions;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class DeeplinkHandler : MonoBehaviour
{
    public static DeeplinkHandler Instance { get; private set; }
    private string stashChallenge;

    public GameObject ConfirmPanel;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Application.deepLinkActivated += OnDeepLinkActivated;
            if (!string.IsNullOrEmpty(Application.absoluteURL))
            {
                // Cold start and Application.absoluteURL not null so process Deep Link.
                OnDeepLinkActivated(Application.absoluteURL);
            }
            // Initialize DeepLink Manager global variable.
            else stashChallenge = "[none]";

            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private async void OnDeepLinkActivated(string url)
    {
        //Extract the challenge parameter from the link.
        stashChallenge = url.Split("/link?challenge=")[1];
        if (!string.IsNullOrEmpty(stashChallenge))
        {
            //Work with the code challenge, prompt user for confirmation.
            Debug.Log("Stash: Deep Link Challenge: " + stashChallenge);

            //Get the Game Center Signature, Salt, Timestamp, TeamPlayerID and GamePlayerID from player prefs.
            string Signature = PlayerPrefs.GetString("Signature");
            string Salt = PlayerPrefs.GetString("Salt");
            string Timestamp = PlayerPrefs.GetString("Timestamp");
            string TeamPlayerID = PlayerPrefs.GetString("TeamPlayerID");
            string PublicKeyURL = PlayerPrefs.GetString("PublicKeyURL");

            //Call linking function "LinkAppleGameCenter" and pass the parameters.
            try
            {
                await StashClient.Instance.LinkAppleGameCenter(stashChallenge, "com.Stash.iosdemo", Signature, Salt,
                    PublicKeyURL, TeamPlayerID, Timestamp);
                Debug.Log("[STASH] Account linked successfully !");
            }
            catch (StashAPIRequestError e)
            {
                Debug.LogWarning("[STASH] Account link failed: " + e.Message);
            }
            catch (StashParseError e)
            {
                Debug.LogWarning("[STASH] Failure while parsing the Stash API response: " + e.Message);
            }
        }
    }
}