using UnityEngine;
using UnityEngine.Networking;
using System.Text;
using StashPopup;

public class StashPaySample : MonoBehaviour
{
    // This is our test API key so you can test the checkout flow right away.
    // You can find your API key in the Stash Studio to run tests on your own Stash instance.
    private const string API_KEY = "p0SVSU3awmdDv8VUPFZ_adWz_uC81xXsEY95Gg7WSwx9TZAJ5_ch-ePXK2Xh3B6o";
    
    void Start()
    {
        // Subscribe to opt-in response for channel selection
        StashPayCard.Instance.OnOptinResponse += OnChannelSelected;
    }
    
    void OnDestroy()
    {
        if (StashPayCard.Instance != null)
        {
            StashPayCard.Instance.OnOptinResponse -= OnChannelSelected;
        }
    }
    
    /// <summary>
    /// Opens a Stash Pay checkout URL in a card dialog
    /// </summary>
    public void OpenCheckout()
    {
        StartCoroutine(OpenCheckoutCoroutine());
    }
    
    private System.Collections.IEnumerator OpenCheckoutCoroutine()
    {
        // You can generate a checkout URL using the Stash API.
        // https://docs.stash.gg/api/server-quickpay/GenerateQuickPayUrl
        // For demo purpouses this happens locally, but in production always generate on the backend !

        var request = new CheckoutRequest
        {
            regionCode = "USA",
            currency = "USD",
            item = new CheckoutItem
            {
                id = "1d56f95f-28df-4ea5-9829-9671241f455e",
                pricePerItem = "9.99",
                quantity = 1,
                imageUrl = "https://upload.wikimedia.org/wikipedia/en/2/2d/Angry_Birds_promo_art.png",
                name = "Test Item",
                description = "This is a test purchase item"
            },
            user = new CheckoutUser
            {
                id = "1d56f95f-28df-4ea5-9829-9671241f455e",
                validatedEmail = "test@rovio.com",
                regionCode = "US",
                platform = "IOS"
            }
        };
        
        string json = JsonUtility.ToJson(request);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        
        using (UnityWebRequest www = new UnityWebRequest("https://test-api.stash.gg/sdk/server/checkout_links/generate_quick_pay_url", "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.SetRequestHeader("X-Stash-Api-Key", API_KEY);
            
            yield return www.SendWebRequest();
            
            if (www.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<CheckoutResponse>(www.downloadHandler.text);

                // Open the checkout URL in a Stash Pay card dialog.
                // This will display the Stash Pay checkout page in a card dialog.
                StashPayCard.Instance.OpenURL(
                    response.url,
                    dismissCallback: OnCheckoutDismissed,
                    successCallback: OnPaymentSuccess,
                    failureCallback: OnPaymentFailure
                );
            }
            else
            {
                Debug.LogError($"Failed to generate checkout URL: {www.error}");
            }
        }
    }
    
    /// <summary>
    /// Opens payment channel selection opt-in popup
    /// </summary>
    public void OpenOptin()
    {
        // Stash provides unique optin URLs for each game, so user can select the payment method they prefer.
        // https://store.howlingwoods.shop/pay/channel-selection is our sample optin URL.
        // Advantage of this optin is that you can customize the optin popup remotely in Stash studio on per-player basis.
        StashPayCard.Instance.OpenPopup(
            "https://store.howlingwoods.shop/pay/channel-selection",
            dismissCallback: () => Debug.Log("Opt-in popup closed")
        );
    }
    
    void OnCheckoutDismissed()
    {
        Debug.Log("Checkout dismissed.");
    }
    
    void OnPaymentSuccess()
    {
        Debug.Log("Payment success - verifying on backend");
        // Always verify purchase on backend via Stash webhooks.
    }
    
    void OnPaymentFailure()
    {
        Debug.Log("Payment failed");
        // Show error message to user
    }
    
    void OnChannelSelected(string channel)
    {
        // Receives "native_iap" or "stash_pay", based on the response from the optin popup.
        string paymentMethod = channel.ToUpper();
        
        PlayerPrefs.SetString("PaymentMethod", paymentMethod);
        PlayerPrefs.Save();
        
        Debug.Log($"User selected payment method: {paymentMethod}");
    }
    
    // These are the request and response classes for the checkout link generation API.
    // As the checkout link generation should happen server side you can disregard these.
    [System.Serializable]
    private class CheckoutRequest
    {
        public string regionCode;
        public string currency;
        public CheckoutItem item;
        public CheckoutUser user;
    }
    
    [System.Serializable]
    private class CheckoutItem
    {
        public string id;
        public string pricePerItem;
        public int quantity;
        public string imageUrl;
        public string name;
        public string description;
    }
    
    [System.Serializable]
    private class CheckoutUser
    {
        public string id;
        public string validatedEmail;
        public string regionCode;
        public string platform;
    }
    
    [System.Serializable]
    private class CheckoutResponse
    {
        public string id;
        public string url;
        public string regionCode;
    }
}
