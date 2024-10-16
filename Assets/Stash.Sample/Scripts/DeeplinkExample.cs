using System;
using System.Collections;
using Stash.Core;
using Stash.Core.Exceptions;
using Stash.Models;
using Stash.Scripts.Core;
using TMPro;
using UnityEngine;

public class DeeplinkExample : MonoBehaviour
{
    public static DeeplinkExample Instance { get; private set; }
    
    public TextMeshProUGUI userLabel;
    public GameObject confirmPanel;
    
    private string _stashChallenge;
    private const string InternalPlayerId = "TEST_PLAYER_ID";

    private async void Awake()
    {
        try{
            CheckoutResponse response = await StashLauncher.Checkout("ITEM_ID", "ID_PLAYER", "ID_TOKEN", StashEnvironment.Test);
        }
        catch (StashRequestError e)
        {
            Console.WriteLine(e);
            throw;
        }

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
    
    private async void Start()
    {
        Debug.Log("TRYING ");

        try
        {
            LinkResponse response = await StashCustomLogin.LinkFacebook("LINK_CODE", "PLAYER_ID", "FB_APPID", "ACCESS_TOKEN", StashEnvironment.Test);

        }
        catch (StashRequestError e)
        {
            Console.WriteLine(e);
            throw;
        }
        
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
   
    }
    
}