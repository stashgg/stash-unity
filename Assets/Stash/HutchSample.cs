using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text.RegularExpressions;
using Stash.Core;

public class HutchSample : MonoBehaviour
{
    // The URL scheme for the app
    private const string DeeplinkScheme = "tatcicus-shop://";

    // Start is called before the first frame update
    void Start()
    {
        // Register for deeplink events
        Application.deepLinkActivated += OnDeeplinkActivated;

        // Check if the app was opened via deeplink
        if (!string.IsNullOrEmpty(Application.absoluteURL))
        {
            OnDeeplinkActivated(Application.absoluteURL);
        }
    }

    // Handle deeplink activation
    private async void OnDeeplinkActivated(string url)
    {
        Debug.Log($"Deeplink received: {url}");

        if (url.StartsWith(DeeplinkScheme))
        {
            string loginCode = ExtractLoginCode(url);
            if (!string.IsNullOrEmpty(loginCode))
            {
                Debug.Log($"Login code extracted: {loginCode}");
                // f1clash-link://codechallenge=1234567890


                // Extract the code challenge and call Stash Linking.
            }
            else
            {
                Debug.LogWarning("No login code found in the deeplink");
            }
        }
        else
        {
            Debug.LogWarning("Received deeplink with unknown scheme");
        }
    }

    // Extract the login code from the deeplink
    private string ExtractLoginCode(string url)
    {
        // Use regex to extract the code between codechallenge="..." pattern
        Regex regex = new Regex(@"codechallenge=""([^""]+)""");
        Match match = regex.Match(url);

        if (match.Success && match.Groups.Count > 1)
        {
            return match.Groups[1].Value;
        }

        return string.Empty;
    }

    // Update is called once per frame
    void Update()
    {

    }
}
