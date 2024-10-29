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
        try{
            CheckoutResponse response = await StashLauncher.Checkout("realMoneyProduct_gems_001", "351eb55e-16c5-431a-b4fa-8e00537e0523", "eyJhbGciOiJSUzI1NiIsImtpZCI6IjhkOWJlZmQzZWZmY2JiYzgyYzgzYWQwYzk3MmM4ZWE5NzhmNmYxMzciLCJ0eXAiOiJKV1QifQ.eyJuYW1lIjoiT25kcmVqIFJlaGFjZWsiLCJwaWN0dXJlIjoiaHR0cHM6Ly9saDMuZ29vZ2xldXNlcmNvbnRlbnQuY29tL2EvQUNnOG9jSnFxM2hHX3V4T204STE3Zm5nVjRIc29NNWZoUmpSWEZFbVRBTXBna0F4WlVuRHBVRT1zOTYtYyIsImlzcyI6Imh0dHBzOi8vc2VjdXJldG9rZW4uZ29vZ2xlLmNvbS9waWUtc3R1ZGlvLTc5YTkyIiwiYXVkIjoicGllLXN0dWRpby03OWE5MiIsImF1dGhfdGltZSI6MTcyNzM2NjMxOSwidXNlcl9pZCI6IlJ0bnpwRlZTVEdhcEp4OERDNHNpc1VHRFBrUzIiLCJzdWIiOiJSdG56cEZWU1RHYXBKeDhEQzRzaXNVR0RQa1MyIiwiaWF0IjoxNzI5MTE2MTMwLCJleHAiOjE3MjkxMTk3MzAsImVtYWlsIjoib25kcmVqQGZyYWN0YWwuaXMiLCJlbWFpbF92ZXJpZmllZCI6dHJ1ZSwiZmlyZWJhc2UiOnsiaWRlbnRpdGllcyI6eyJnb29nbGUuY29tIjpbIjExNjUzOTk1MTA2ODE3MjY0MzA3NCJdLCJlbWFpbCI6WyJvbmRyZWpAZnJhY3RhbC5pcyJdfSwic2lnbl9pbl9wcm92aWRlciI6Imdvb2dsZS5jb20ifX0.PZOOAU3El4mQdDx51HmwS3RR-5qb1IbxLbQMrU-IlVrlY2JBC2YTKD_0yLBl5E-cgXrCVTJS7rw8k_ABP_tVrlJsz5n2pqAOP_0GNacRMnFZPEB0llKGr85vhuTTyuQexTK_-2qwA8RcUMrbeIF97xJPWilnvIEyFIpuwGyelocODCMsOgAPDmfxSzGP40SG-ss3u7XdbfFwRHDcgOI2cXiYHe7UDYPYN0oD0t5WBr93PhhPg-TMRYkOhD_60Hd_eYXedq7D2QbzvF4THhuXws95sufBL8Yy5EstcfZUegWyYOHW494nkiBpD4ruynI-Qm_rMWdeqaFEgWnvDCtRJw", StashEnvironment.Test);
            Debug.Log(response.url);
            Application.OpenURL(response.url);
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