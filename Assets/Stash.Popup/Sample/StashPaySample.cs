using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Text;
using StashPopup;

/// <summary>
/// Simple sample: Open Checkout, Open Modal, Force Web Checkout, and callback status.
/// Attach to a GameObject and assign the optional Status Text and buttons in the Inspector or via code.
/// </summary>
public class StashPaySample : MonoBehaviour
{
    private const string API_KEY = "p0SVSU3awmdDv8VUPFZ_adWz_uC81xXsEY95Gg7WSwx9TZAJ5_ch-ePXK2Xh3B6o";
    private const string MODAL_URL = "https://store.howlingwoods.shop/pay/channel-selection";

    [Header("Optional UI")]
    [SerializeField] private Text statusText;
    [SerializeField] private Toggle forceWebCheckoutToggle;

    private System.Action _onDismissed;
    private System.Action _onSuccess;
    private System.Action _onFailure;
    private System.Action<string> _onOptin;
    private System.Action<double> _onPageLoaded;
    private System.Action _onNetworkError;

    void Start()
    {
        _onDismissed = () => SetStatus("Dismissed");
        _onSuccess = () => SetStatus("Success");
        _onFailure = () => SetStatus("Failure");
        _onOptin = (s) => SetStatus($"Opt-in: {s}");
        _onPageLoaded = (ms) => SetStatus($"Page loaded ({ms:F0} ms)");
        _onNetworkError = () => SetStatus("Network error");

        var card = StashPayCard.Instance;
        card.OnSafariViewDismissed += _onDismissed;
        card.OnPaymentSuccess += _onSuccess;
        card.OnPaymentFailure += _onFailure;
        card.OnOptinResponse += _onOptin;
        card.OnPageLoaded += _onPageLoaded;
        card.OnNetworkError += _onNetworkError;

        if (forceWebCheckoutToggle != null)
        {
            forceWebCheckoutToggle.isOn = card.ForceWebBasedCheckout;
            forceWebCheckoutToggle.onValueChanged.AddListener(OnForceWebCheckoutChanged);
        }
    }

    void OnDestroy()
    {
        var card = StashPayCard.Instance;
        if (card == null) return;
        if (_onDismissed != null) card.OnSafariViewDismissed -= _onDismissed;
        if (_onSuccess != null) card.OnPaymentSuccess -= _onSuccess;
        if (_onFailure != null) card.OnPaymentFailure -= _onFailure;
        if (_onOptin != null) card.OnOptinResponse -= _onOptin;
        if (_onPageLoaded != null) card.OnPageLoaded -= _onPageLoaded;
        if (_onNetworkError != null) card.OnNetworkError -= _onNetworkError;
        if (forceWebCheckoutToggle != null)
            forceWebCheckoutToggle.onValueChanged.RemoveListener(OnForceWebCheckoutChanged);
    }

    private void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message;
        Debug.Log("[StashPaySample] " + message);
    }

    private void OnForceWebCheckoutChanged(bool value)
    {
        StashPayCard.Instance.ForceWebBasedCheckout = value;
    }

    /// <summary>Open checkout (generates URL via API then opens).</summary>
    public void OpenCheckout()
    {
        SetStatus("Opening checkout...");
        StartCoroutine(OpenCheckoutCoroutine());
    }

    private System.Collections.IEnumerator OpenCheckoutCoroutine()
    {
        if (forceWebCheckoutToggle != null)
            StashPayCard.Instance.ForceWebBasedCheckout = forceWebCheckoutToggle.isOn;

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
                description = "Test purchase"
            },
            user = new CheckoutUser
            {
                id = "1d56f95f-28df-4ea5-9829-9671241f455e",
                validatedEmail = "test@domain.com",
                regionCode = "US",
                platform = "IOS"
            }
        };

        string json = JsonUtility.ToJson(request);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        using (var www = new UnityWebRequest("https://test-api.stash.gg/sdk/server/checkout_links/generate_quick_pay_url", "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.SetRequestHeader("X-Stash-Api-Key", API_KEY);
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<CheckoutResponse>(www.downloadHandler.text);
                StashPayCard.Instance.OpenCheckout(
                    response.url,
                    dismissCallback: () => SetStatus("Dismissed"),
                    successCallback: () => SetStatus("Success"),
                    failureCallback: () => SetStatus("Failure")
                );
            }
            else
            {
                SetStatus("API error: " + www.error);
            }
        }
    }

    /// <summary>Open modal (e.g. channel selection).</summary>
    public void OpenModal()
    {
        SetStatus("Opening modal...");
        StashPayCard.Instance.OpenModal(MODAL_URL, dismissCallback: () => SetStatus("Dismissed"));
    }

    [System.Serializable] private class CheckoutRequest { public string regionCode; public string currency; public CheckoutItem item; public CheckoutUser user; }
    [System.Serializable] private class CheckoutItem { public string id; public string pricePerItem; public int quantity; public string imageUrl; public string name; public string description; }
    [System.Serializable] private class CheckoutUser { public string id; public string validatedEmail; public string regionCode; public string platform; }
    [System.Serializable] private class CheckoutResponse { public string id; public string url; public string regionCode; }
}
