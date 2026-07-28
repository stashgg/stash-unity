using System;
using System.Collections;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Serialization;

/// <summary>
/// Fetches URLs from the Stash API using versioned HMAC auth (set App ID + ingress secret in the Inspector).
/// Intended for sample usage only. For production, always generate Stash URLs securely on your backend.
/// See https://docs.stash.gg/guides/get-started/stash-api-keys/overview
/// </summary>
public class StashLinkGenerator : MonoBehaviour
{
    #region API Calls

    private const string QUICK_PAY_ENDPOINT = "https://test-api.stash.gg/sdk/server/checkout_links/generate_quick_pay_url";
    private const string AUTHENTICATED_URL_ENDPOINT = "https://test-api.stash.gg/sdk/server/generate_url";

    [Tooltip("App ID from Studio → Project Settings → App details")]
    [SerializeField] private string appId = "YOUR_APP_ID";

    [FormerlySerializedAs("apiKey")]
    [SerializeField] private string ingressApiKey = "YOUR_INGRESS_API_KEY";

    /// <summary>Request a quick pay checkout URL. See https://docs.stash.gg/api/server-quickpay/GenerateQuickPayUrl for reference.</summary>
    public void RequestCheckoutUrl(Action<string> onUrl, Action<string> onError)
    {
        StartCoroutine(RequestCheckoutUrlCoroutine(onUrl, onError));
    }

    private IEnumerator RequestCheckoutUrlCoroutine(Action<string> onUrl, Action<string> onError)
    {
        // Determine purchase platform dynamically
        string platformValue;
#if UNITY_IOS
            platformValue = "IOS";
#elif UNITY_ANDROID
            platformValue = "ANDROID";
#else
        platformValue = Application.platform.ToString().ToUpper();
#endif

        var request = new QuickPayRequest
        {
            regionCode = "USA",
            currency = "USD",
            item = new QuickPayItem
            {
                id = "test_item_id",
                pricePerItem = "0.99",
                quantity = 1,
                imageUrl = "https://storage.googleapis.com/stash_assets/stash_logo_128.png",
                name = "Test Item",
                description = "A description of the test item"
            },
            user = new QuickPayUser
            {
                id = "test_user_id",
                validatedEmail = "test@domain.com",
                platform = platformValue
            }
        };

        byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(request));
        using (var www = new UnityWebRequest(QUICK_PAY_ENDPOINT, "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(body);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            if (!TrySetHmacAuthHeader(www, body, onError))
                yield break;
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<QuickPayResponse>(www.downloadHandler.text);
                onUrl?.Invoke(response.url);
            }
            else
            {
                onError?.Invoke(FormatRequestError(www));
            }
        }
    }

    /// <summary>Request a pre-authenticated webshop URL. See https://docs.stash.gg/api/server-urls/GenerateAuthenticatedUrl for reference.</summary>
    /// <param name="target">Target destination: "DEFAULT", "HOME", "LOYALTY", "ROOT", or "STORE"</param>
    public void RequestAuthenticatedWebshopUrl(Action<string> onUrl, Action<string> onError, string target = "HOME")
    {
        StartCoroutine(RequestAuthenticatedWebshopUrlCoroutine(onUrl, onError, target));
    }

    private IEnumerator RequestAuthenticatedWebshopUrlCoroutine(Action<string> onUrl, Action<string> onError, string target)
    {
        var request = new AuthenticatedUrlRequest
        {
            user = new AuthenticatedUrlUser { id = "WEBSHOP_USER_ID" },
            target = target
        };

        byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(request));
        using (var www = new UnityWebRequest(AUTHENTICATED_URL_ENDPOINT, "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(body);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            if (!TrySetHmacAuthHeader(www, body, onError))
                yield break;
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<AuthenticatedUrlResponse>(www.downloadHandler.text);
                onUrl?.Invoke(response.url);
            }
            else
            {
                onError?.Invoke(FormatRequestError(www));
            }
        }
    }

    /// <summary>
    /// Builds <c>x-stash-hmac-signature: v1;{appId};{unixMs};{base64Hmac}</c> over
    /// <c>{unixMs}.{body}</c> using the base64-decoded ingress secret.
    /// </summary>
    private bool TrySetHmacAuthHeader(UnityWebRequest www, byte[] body, Action<string> onError)
    {
        if (string.IsNullOrWhiteSpace(appId) || appId == "YOUR_APP_ID")
        {
            onError?.Invoke("Set App ID (Studio → Project Settings → App details) on StashLinkGenerator.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(ingressApiKey) ||
            ingressApiKey == "YOUR_INGRESS_API_KEY" ||
            ingressApiKey == "YOUR_STASH_API_KEY" ||
            ingressApiKey == "YOUR_TEST_API_KEY" ||
            ingressApiKey == "YOUR_INGRESS_SECRET")
        {
            onError?.Invoke("Set ingress API key (Studio → Project Settings → API Secrets) on StashLinkGenerator.");
            return false;
        }

        byte[] key;
        try
        {
            key = Convert.FromBase64String(ingressApiKey);
        }
        catch (FormatException)
        {
            onError?.Invoke("Ingress secret must be the base64 value.");
            return false;
        }

        string unixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        string signedMsg = unixMs + "." + Encoding.UTF8.GetString(body);
        using (var hmac = new HMACSHA256(key))
        {
            string sig = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(signedMsg)));
            www.SetRequestHeader("x-stash-hmac-signature", $"v1;{appId};{unixMs};{sig}");
        }

        return true;
    }

    private static string FormatRequestError(UnityWebRequest www)
    {
        string body = www.downloadHandler != null ? www.downloadHandler.text : null;
        if (string.IsNullOrEmpty(body))
            return $"{www.responseCode} {www.error}";
        return $"{www.responseCode} {www.error}: {body}";
    }

    #endregion

    #region API models

    [Serializable] private class QuickPayRequest { public string regionCode; public string currency; public QuickPayItem item; public QuickPayUser user; }
    [Serializable] private class QuickPayItem { public string id; public string pricePerItem; public int quantity; public string imageUrl; public string name; public string description; }
    [Serializable] private class QuickPayUser { public string id; public string validatedEmail; public string regionCode; public string platform; }
    [Serializable] private class QuickPayResponse { public string id; public string url; public string regionCode; }
    [Serializable] private class AuthenticatedUrlRequest { public AuthenticatedUrlUser user; public string target; }
    [Serializable] private class AuthenticatedUrlUser { public string id; }
    [Serializable] private class AuthenticatedUrlResponse { public string url; }

    #endregion
}
