using System;
using System.Collections;
using Stash.Core;
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
        StashClient.Instance.LinkGoogleOrApple("TEST", "TEST", "TEST");
        if (Instance == null)
        {
            Instance = this;                
            Application.deepLinkActivated += onDeepLinkActivated;
            if (!string.IsNullOrEmpty(Application.absoluteURL))
            {
                // Cold start and Application.absoluteURL not null so process Deep Link.
                onDeepLinkActivated(Application.absoluteURL);
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
 
    private void onDeepLinkActivated(string url)
    {
        //Extract the challenge parameter from the link.
        stashChallenge = url.Split("/link?challenge=")[1];
        if (!string.IsNullOrEmpty(stashChallenge))
        {
            //Work with the code challenge, prompt user for confirmation.
            Debug.Log("Stash: Deep Link Challenge: " + stashChallenge);

            
            //Show Confirm Panel
            ConfirmPanel.SetActive(true);
           
        }
    }
    
}