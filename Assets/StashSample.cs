using System;
using System.Collections;
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

    private void Awake()
    {
        StashNative.Instance.OnExternalPayment += url =>
            SetStatus("External payment" + url);
    }

    [SerializeField] private Text statusText;
    [SerializeField] private StashLinkGenerator linkGenerator;

#if UNITY_ANDROID && !UNITY_EDITOR
    private bool _androidBackdropSet;

    private static sbyte[] UnsignedBytesToSignedBytes(byte[] bytes)
    {
        var signed = new sbyte[bytes.Length];
        Buffer.BlockCopy(bytes, 0, signed, 0, bytes.Length);
        return signed;
    }

    private void ClearAndroidCardBackdropIfSet()
    {
        if (!_androidBackdropSet)
            return;
        _androidBackdropSet = false;
        using (var cls = new AndroidJavaClass("com.stash.stashnative.StashNativeCard"))
        {
            cls.CallStatic("setBackdropBytes", (object)null);
        }
    }
#endif

    private void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message;
        Debug.Log(message);
    }

    /// <summary>Open card with custom StashNativeCardConfig. Ideal for Stash Pay payment links and pre-authenticated webshop links.</summary>
    public void OpenCard()
    {
        StartCoroutine(OpenCardRoutine());
    }

    private IEnumerator OpenCardRoutine()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        yield return new WaitForEndOfFrame();
        var tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
        tex.Apply();
        byte[] jpg = ImageConversion.EncodeToJPG(tex, 75); // quality = 75 (adjust as needed)
        Destroy(tex);
        using (var cls = new AndroidJavaClass("com.stash.stashnative.StashNativeCard"))
        {
            // Pass sbyte[] so Unity does not use obsolete Byte[] JNI conversion (logcat warnings).
            cls.CallStatic("setBackdropBytes", (object)UnsignedBytesToSignedBytes(jpg));
        }
        _androidBackdropSet = true;
#endif

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
        config.forcePortrait = true;

        // If your game support multiple orientations, we recommend to store the current orientation and
        // restore it after the checkout is dismissed. Otherwise, the game can rotate behind the card.
        var orientation = Screen.orientation;

        void RestoreOrientation()
        {
            //Screen.orientation = orientation;
        }

        void OnCardFlowEnded(string message)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            ClearAndroidCardBackdropIfSet();
#endif
            //RestoreOrientation();
            SetStatus(message);
        }

        StashNative.Instance.OpenCard(TEST_URL,
            () => OnCardFlowEnded("Dismissed"),
            _ => OnCardFlowEnded("Success"),
            () => OnCardFlowEnded("Failure"),
            config);

        yield break;
    }

    /// <summary>Open modal with custom StashNativeModalConfig. Ideal for channel selection urls or as an alternative Stash Pay checkout style.</summary>
    public void OpenModal()
    {
        var config = StashNativeModalConfig.Default;
        // Configure sizing, dismiss behaviour, or optional shell color as desired:
        // config.allowDismiss = true;
        // config.backgroundColor = "#1a1a1a";
        // config.phoneWidthRatioPortrait = 0.9f;
        // config.phoneHeightRatioPortrait = 0.6f;

        StashNative.Instance.OpenModal(TEST_URL,
            () => SetStatus("Modal Dismissed"),
            _ => SetStatus("Success"),
            () => SetStatus("Failure"),
            config);
    }

    /// <summary>Open browser (SFSafariWebView / Chrome Custom Tabs) with default config. Ideal as an OS-native alternative for Stash Pay checkout links or pre-authenticated webshop links.</summary>
    public void OpenBrowser()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // Optional: reduce chance of process kill on low-memory Android when user leaves for Custom Tabs (see stash-native README).
        // StashNative.Instance.SetKeepAliveEnabled(true);
        // StashNative.Instance.SetKeepAliveConfig(new StashNativeKeepAliveConfig
        // {
        //     notificationTitle = "Payment in progress",
        //     notificationText = "Tap to return to the app",
        //     notificationIconResId = 0
        // });
#endif
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
                    _ => SetStatus("Success"),
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
                    _ => SetStatus("Success"),
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
