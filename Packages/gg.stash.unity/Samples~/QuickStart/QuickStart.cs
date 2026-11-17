using UnityEngine;
using UnityEngine.UI;
using Stash.Native;

/// <summary>
/// Stash QuickStart: the smallest possible integration. One uGUI button opens a checkout card and the
/// callbacks tell you what happened. Read the numbered comments top to bottom.
///
/// Scene: a Camera, an EventSystem, a Canvas with one Button and a status Text, and this component.
/// The Button's OnClick is wired to OpenCheckout() in the Inspector.
/// </summary>
public class QuickStart : MonoBehaviour
{
    // 1. THE CHECKOUT URL
    //    This is Stash's public test page. It renders a fake cart and lets you trigger every
    //    callback (success, failure, dismiss) by tapping buttons inside the card.
    //
    //    In your game, replace it with a URL your BACKEND generates for the player:
    //      - Stash Pay checkout link  -> https://docs.stash.gg/api/server-quickpay/GenerateQuickPayUrl
    //      - pre-authenticated webshop -> https://docs.stash.gg/api/server-urls/GenerateAuthenticatedUrl
    //    Never sign requests with your ingress secret inside the game build.
    [SerializeField] private string checkoutUrl = "https://test.stashpreview.com/";

    // The label under the button; shows what the last callback said.
    [SerializeField] private Text statusText;

    private void Start()
    {
        // 2. (OPTIONAL) GLOBAL EVENTS
        //    Fire for every card, scanner and browser the SDK presents. Good for analytics or logging.
        //    For the result of one specific purchase, use the per-call callbacks in OpenCheckout() below.
        StashNative.Instance.OnPaymentSuccess += order => Debug.Log("[Stash] payment success " + order);
        StashNative.Instance.OnNetworkError += () => SetStatus("Network error. Check the connection and try again.");
    }

    /// <summary>Wired to the Button's OnClick in the scene.</summary>
    public void OpenCheckout()
    {
        // 3. (OPTIONAL) CARD OPTIONS
        //    A new config carries the native defaults, so set only what you need.
        var config = new StashNativeCardConfig
        {
            // Landscape-only game? Show the checkout in portrait on phones:
            // orientationPreference = StashNativeOrientationPreference.Portrait,

            // Keep the card open after the payment result instead of closing it:
            // autoClose = false,
        };

        // 4. OPEN THE CARD
        //    Exactly one of the callbacks ends the card. With autoClose (default) the card is already
        //    closed when success/failure fires, so you can open the next card right from the callback.
        bool opened = StashNative.Instance.OpenCard(
            checkoutUrl,
            dismissCallback: () => SetStatus("Checkout dismissed."),
            successCallback: order =>
            {
                // 5. GRANT THE PURCHASE
                //    `order` is whatever the checkout page reported (may be empty). Do not trust it
                //    for entitlements: verify the purchase with your backend or Stash webhooks, then
                //    grant the item.
                SetStatus("Payment succeeded. Verify with your backend, then grant the item.");
            },
            failureCallback: () => SetStatus("Payment failed."),
            config: config,
            networkErrorCallback: () => SetStatus("Checkout lost its connection."),
            externalPaymentCallback: url => SetStatus("Continuing in the browser (e.g. PayPal, Klarna)."));

        // 6. OVERLAPPING OPENS
        //    OpenCard returns false if a card or scanner is already on screen (double tap, two buttons).
        //    Nothing happens to the open card; your callbacks for it stay intact.
        if (!opened) SetStatus("A checkout is already open.");
    }

    private void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message;
        Debug.Log("[Stash QuickStart] " + message);
    }
}
