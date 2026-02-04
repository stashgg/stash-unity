using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using StashPopup;
using Stash.Samples;

namespace Stash.Samples
{
    /// <summary>
    /// Handles Stash Pay checkout flow: generate checkout URL, open checkout/modal, and verify purchase.
    /// Keeps StashPayCard and Stash API logic out of UI controllers.
    /// </summary>
    public class StoreCheckoutService
    {
        /// <summary>Result of purchase verification for UI to display.</summary>
        public struct VerificationResult
        {
            public bool Success;
            public string ItemName;
            public string Total;
            public string Currency;
            public string Tax;
            public string TimeMillis;
        }

        #region Checkout URL (Stash API)

        [Serializable]
        private class CheckoutRequest
        {
            public string regionCode;
            public string currency;
            public CheckoutRequestItem item;
            public CheckoutRequestUser user;
        }

        [Serializable]
        private class CheckoutRequestItem
        {
            public string id;
            public string pricePerItem;
            public int quantity;
            public string imageUrl;
            public string name;
            public string description;
        }

        [Serializable]
        private class CheckoutRequestUser
        {
            public string id;
            public string validatedEmail;
            public string regionCode;
            public string platform;
        }

        [Serializable]
        private class CheckoutLinkResponse
        {
            public string url;
            public string id;
        }

        /// <summary>
        /// Requests a checkout URL from the Stash API.
        /// </summary>
        /// <returns>URL and checkout ID, or null on failure.</returns>
        public static async Task<(string url, string checkoutId)?> GetCheckoutUrlAsync(
            string itemId,
            string itemName,
            string itemDescription,
            string pricePerItem,
            int quantity,
            string imageUrl,
            string userId,
            string email,
            string currency,
            string apiKey,
            StashDemoEnvironment environment)
        {
            string baseUrl = DemoAppConstants.GetStashApiBaseUrl(environment);
            string apiUrl = $"{baseUrl}/sdk/server/checkout_links/generate_quick_pay_url";
            string platformString = Application.platform == RuntimePlatform.IPhonePlayer ? "IOS" : "ANDROID";

            var body = new CheckoutRequest
            {
                regionCode = DemoAppConstants.DEFAULT_REGION_ALPHA3,
                currency = currency,
                item = new CheckoutRequestItem
                {
                    id = itemId,
                    name = itemName,
                    description = itemDescription ?? "",
                    pricePerItem = pricePerItem,
                    quantity = quantity,
                    imageUrl = imageUrl ?? ""
                },
                user = new CheckoutRequestUser
                {
                    id = userId,
                    validatedEmail = email,
                    regionCode = DemoAppConstants.DEFAULT_REGION_ALPHA2,
                    platform = platformString
                }
            };

            string requestJson = JsonUtility.ToJson(body);
            using (var request = new UnityWebRequest(apiUrl, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(requestJson));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("X-Stash-Api-Key", apiKey);

                var op = request.SendWebRequest();
                while (!op.isDone)
                    await Task.Yield();

                if (request.result != UnityWebRequest.Result.Success || request.responseCode != 200)
                {
                    Debug.LogError($"[StoreCheckoutService] Checkout URL failed: {request.responseCode} {request.error}");
                    return null;
                }

                try
                {
                    var parsed = JsonUtility.FromJson<CheckoutLinkResponse>(request.downloadHandler.text);
                    if (string.IsNullOrEmpty(parsed?.url) || string.IsNullOrEmpty(parsed?.id))
                    {
                        Debug.LogError("[StoreCheckoutService] Invalid checkout response: missing url or id");
                        return null;
                    }
                    return (parsed.url, parsed.id);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[StoreCheckoutService] Parse error: {ex.Message}");
                    return null;
                }
            }
        }

        #endregion

        #region Open Checkout (Stash Pay)

        /// <summary>
        /// Opens the Stash Pay checkout with the given URL. Callbacks are invoked from the main thread.
        /// </summary>
        public static void OpenCheckout(
            string checkoutUrl,
            bool useSafariWebView,
            Action onSuccess,
            Action onFailure,
            Action onDismiss)
        {
            if (StashPayCard.Instance == null)
            {
                Debug.LogError("[StoreCheckoutService] StashPayCard.Instance is null");
                onFailure?.Invoke();
                return;
            }
            StashPayCard.Instance.ForceWebBasedCheckout = useSafariWebView;
            StashPayCard.Instance.OpenCheckout(checkoutUrl, onDismiss, onSuccess, onFailure);
        }

        #endregion

        #region Verify Purchase (Stash API)

        [Serializable]
        private class PurchaseVerificationResponse
        {
            public string currency;
            public PurchaseItem[] items;
            public PaymentSummary paymentSummary;
        }

        [Serializable]
        private class PurchaseItem
        {
            public string id;
            public string name;
            public string description;
            public string pricePerItem;
            public int quantity;
            public string imageUrl;
        }

        [Serializable]
        private class PaymentSummary
        {
            public string timeMillis;
            public string total;
            public string tax;
        }

        /// <summary>
        /// Verifies a checkout with the Stash API and returns a result for UI display.
        /// </summary>
        public static async Task<VerificationResult?> VerifyPurchaseAsync(
            string checkoutId,
            string apiKey,
            StashDemoEnvironment environment)
        {
            if (string.IsNullOrEmpty(checkoutId))
            {
                Debug.LogError("[StoreCheckoutService] checkoutId is null or empty");
                return null;
            }

            string baseUrl = DemoAppConstants.GetStashApiBaseUrl(environment);
            string verifyUrl = $"{baseUrl}/sdk/checkout_links/order/{checkoutId}";

            using (var request = UnityWebRequest.PostWwwForm(verifyUrl, ""))
            {
                request.SetRequestHeader("X-Stash-Api-Key", apiKey);
                var op = request.SendWebRequest();
                while (!op.isDone)
                    await Task.Yield();

                if (request.result != UnityWebRequest.Result.Success || request.responseCode != 200)
                {
                    Debug.LogWarning($"[StoreCheckoutService] Verify failed: {request.responseCode} {request.error}");
                    return new VerificationResult { Success = false };
                }

                try
                {
                    var parsed = JsonUtility.FromJson<PurchaseVerificationResponse>(request.downloadHandler.text);
                    bool ok = parsed != null
                        && parsed.paymentSummary != null
                        && !string.IsNullOrEmpty(parsed.paymentSummary.total)
                        && parsed.items != null
                        && parsed.items.Length > 0;

                    return new VerificationResult
                    {
                        Success = ok,
                        ItemName = parsed?.items?.Length > 0 ? parsed.items[0].name : "",
                        Total = parsed?.paymentSummary?.total ?? "",
                        Currency = parsed?.currency ?? "",
                        Tax = parsed?.paymentSummary?.tax ?? "",
                        TimeMillis = parsed?.paymentSummary?.timeMillis ?? ""
                    };
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[StoreCheckoutService] Verify parse error: {ex.Message}");
                    return new VerificationResult { Success = false };
                }
            }
        }

        #endregion

        #region Channel Selection Modal (Stash Pay)

        /// <summary>
        /// Opens the payment channel selection modal. Callbacks are invoked from the main thread.
        /// </summary>
        public static void OpenChannelSelection(
            string url,
            StashPayModalConfig? config,
            Action<string> onOptinResponse,
            Action onNetworkError,
            Action onDismiss)
        {
            if (StashPayCard.Instance == null)
            {
                Debug.LogError("[StoreCheckoutService] StashPayCard.Instance is null");
                onDismiss?.Invoke();
                return;
            }
            StashPayCard.Instance.OnOptinResponse += OnOptin;
            StashPayCard.Instance.OnNetworkError += OnNetworkErr;
            void OnOptin(string optinType)
            {
                StashPayCard.Instance.OnOptinResponse -= OnOptin;
                StashPayCard.Instance.OnNetworkError -= OnNetworkErr;
                onOptinResponse?.Invoke(optinType ?? "");
            }
            void OnNetworkErr()
            {
                StashPayCard.Instance.OnOptinResponse -= OnOptin;
                StashPayCard.Instance.OnNetworkError -= OnNetworkErr;
                onNetworkError?.Invoke();
            }
            StashPayCard.Instance.OpenModal(url, dismissCallback: () =>
            {
                StashPayCard.Instance.OnOptinResponse -= OnOptin;
                StashPayCard.Instance.OnNetworkError -= OnNetworkErr;
                onDismiss?.Invoke();
            }, config: config);
        }

        #endregion
    }
}
