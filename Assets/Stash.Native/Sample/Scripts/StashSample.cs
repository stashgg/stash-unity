using UnityEngine;
using UnityEngine.UI;
using Stash.Native;

/// <summary>
/// Reference usage of StashNative native plugin: OpenCard , OpenModal, OpenBrowser.
///
/// </summary>
public class StashSample : MonoBehaviour
{
    // We provide a simple test url test.stashpreview.com, that will let you simulate callbacks in your integration.
    private const string TEST_URL = "https://test.stashpreview.com/";

    [SerializeField] private Text statusText;
    [SerializeField] private StashLinkGenerator linkGenerator;

    private void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message;
        Debug.Log(message);
    }

    /// <summary>Open card with custom StashNativeCardConfig. Ideal for Stash Pay payment links and pre-authenticated webshop links.</summary>
    public void OpenCard()
    {
        var config = StashNativeCardConfig.Default;
        // Configure sizing and orientation as desired, or use the default values:
        // config.cardHeightRatioPortrait = 0.85f;
        // config.cardWidthRatioLandscape = 0.95f;
        // config.cardHeightRatioLandscape = 0.55f;
        // config.tabletWidthRatioPortrait = 0.35f;
        // config.tabletHeightRatioPortrait = 0.45f;
        // config.tabletWidthRatioLandscape = 0.25f;
        // config.tabletHeightRatioLandscape = 0.5f;
        // If your game is locked to landscape, uncomment this to force portrait for checkout:
        // config.forcePortrait = true;

        StashNative.Instance.OpenCard(TEST_URL,
            () => SetStatus("Dismissed"),
            () => SetStatus("Success"),
            () => SetStatus("Failure"),
            config);

    }

    /// <summary>Open modal with custom StashNativeModalConfig. Ideal for channel selection urls or as an alternative Stash Pay checkout style.</summary>
    public void OpenModal()
    {
        var config = StashNativeModalConfig.Default;
        // Configure sizing and dismiss behaviour as desired, or use the default values:
        // config.showDragBar = true;
        // config.allowDismiss = true;
        // config.phoneWidthRatioPortrait = 0.9f;
        // config.phoneHeightRatioPortrait = 0.6f;
        // config.phoneWidthRatioLandscape = 0.6f;
        // config.phoneHeightRatioLandscape = 0.85f;
        // config.tabletWidthRatioPortrait = 0.45f;
        // config.tabletHeightRatioPortrait = 0.55f;
        // config.tabletWidthRatioLandscape = 0.35f;
        // config.tabletHeightRatioLandscape = 0.6f;
        // config.forcePortrait = false;

        StashNative.Instance.OpenModal(TEST_URL,
            () => SetStatus("Modal Dismissed"),
            () => SetStatus("Success"),
            () => SetStatus("Failure"),
            config);
    }

    /// <summary>Open browser (SFSafariWebView / Chrome Custom Tabs) with default config. Ideal as an OS-native alternative for Stash Pay checkout links or pre-authenticated webshop links.</summary>
    public void OpenBrowser()
    {
        // Open the URL in the in-app browser surfaces provided by the OS (Chrome Custom Tabs on Android, SFSafariViewController on iOS).
        // While isolated, they open inside your app UI and are fully provided by the OS. A lightweight alternative to the card and modal.
        // No callbacks are supported for this method, instead deeplinks can be used to handle the result.
        StashNative.Instance.OpenBrowser(TEST_URL);
    }

    /// <summary>Generate a checkout URL via StashLinkGenerator and open it in a default card.</summary>
    public void GenerateLinkAndOpen()
    {
        if (linkGenerator == null)
        {
            SetStatus("LinkGenerator not assigned.");
            return;
        }

        // Request checkout URL from the Stash API.
        linkGenerator.RequestCheckoutUrl(
            onUrl: url =>
            {
                // Open the returned URL in the native card.
                var config = StashNativeCardConfig.Default;
                // If your game is locked to landscape, uncomment this to force portrait for checkout:
                // config.forcePortrait = true;
                StashNative.Instance.OpenCard(url,
                    () => SetStatus("Dismissed"),
                    () => SetStatus("Success"),
                    () => SetStatus("Failure"),
                    config);
            },
            onError: error =>
            {
                SetStatus("Failed to generate link: " + error);
            });
    }

    /// <summary>Generate a pre-authenticated webshop URL via StashLinkGenerator and open it in a default card.</summary>
    public void GenerateWebshopLinkAndOpen()
    {
        if (linkGenerator == null)
        {
            SetStatus("LinkGenerator not assigned.");
            return;
        }

        // Request pre-auth webshop URL (target: HOME, or use LOYALTY, ROOT, STORE, DEFAULT).
        linkGenerator.RequestAuthenticatedWebshopUrl(
            onUrl: url =>
            {
                // Open the returned URL in the native card.
                var config = StashNativeCardConfig.Default;
                StashNative.Instance.OpenCard(url,
                    () => SetStatus("Dismissed"),
                    () => SetStatus("Success"),
                    () => SetStatus("Failure"),
                    config);
            },
            onError: error =>
            {
                SetStatus("Failed to generate webshop link: " + error);
            },
            target: "DEFAULT");
    }

}
