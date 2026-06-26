using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Stash.Native
{
    internal static class StashPaymentApiClient
    {
        public const float PollIntervalSeconds = 2f;
        public const float MaxPollDurationSeconds = 300f;

        public static IEnumerator FetchOrderSummaryV2(
            string apiBaseUrl,
            string checkoutLinkId,
            StashRequestHeaders headers,
            Action<OrderSummaryV2Payload> onSuccess,
            Action<string> onError)
        {
            var url = $"{apiBaseUrl.TrimEnd('/')}/api/checkout_links/order/{Uri.EscapeDataString(checkoutLinkId)}/get_v2";
            yield return GetJson(url, headers, body =>
            {
                var payload = JsonUtility.FromJson<OrderSummaryV2Payload>(body);
                if (payload == null)
                {
                    onError?.Invoke("Failed to parse order summary.");
                    return;
                }
                onSuccess?.Invoke(payload);
            }, onError);
        }

        public static IEnumerator GetLegacyOrderResult(
            string apiBaseUrl,
            string paymentIntentId,
            StashRequestHeaders headers,
            Action<LegacyOrderResultPayload> onSuccess,
            Action<string> onError)
        {
            var url = $"{apiBaseUrl.TrimEnd('/')}/api/checkout_links/order_result/{Uri.EscapeDataString(paymentIntentId)}";
            yield return GetJson(url, headers, body =>
            {
                var payload = JsonUtility.FromJson<LegacyOrderResultPayload>(body);
                if (payload?.resultStatus == null)
                {
                    onError?.Invoke("Failed to parse order result.");
                    return;
                }
                onSuccess?.Invoke(payload);
            }, onError);
        }

        public static IEnumerator CompleteLegacyPurchase(
            string apiBaseUrl,
            string paymentIntentId,
            string shopHandle,
            StashRequestHeaders headers,
            Action<CompletePurchasePayload> onSuccess,
            Action<string> onError)
        {
            var url = $"{apiBaseUrl.TrimEnd('/')}/api/checkout_links/complete_purchase/{Uri.EscapeDataString(paymentIntentId)}";
            var body = JsonUtility.ToJson(new CompletePurchaseRequest { shopHandle = shopHandle ?? "" });
            yield return PostJson(url, body, headers, responseBody =>
            {
                var payload = JsonUtility.FromJson<CompletePurchasePayload>(responseBody);
                onSuccess?.Invoke(payload ?? new CompletePurchasePayload());
            }, onError);
        }

        public static IEnumerator GetMultiPspPaymentStatus(
            string apiBaseUrl,
            string checkoutLinkId,
            string paymentId,
            StashRequestHeaders headers,
            Action<MultiPspPaymentStatusPayload> onSuccess,
            Action<string> onError)
        {
            var url =
                $"{apiBaseUrl.TrimEnd('/')}/v1/multipsp/checkout-links/{Uri.EscapeDataString(checkoutLinkId)}/payments/{Uri.EscapeDataString(paymentId)}/status";
            yield return GetJson(url, headers, body =>
            {
                var payload = JsonUtility.FromJson<MultiPspPaymentStatusPayload>(body);
                onSuccess?.Invoke(payload ?? new MultiPspPaymentStatusPayload());
            }, onError);
        }

        public static IEnumerator CompleteMultiPspCheckout(
            string apiBaseUrl,
            string paymentId,
            string email,
            StashRequestHeaders headers,
            Action<CompletePurchasePayload> onSuccess,
            Action<string> onError)
        {
            var url = $"{apiBaseUrl.TrimEnd('/')}/v1/multipsp/complete";
            var body = JsonUtility.ToJson(new CompleteMultiPspRequest
            {
                paymentId = paymentId ?? "",
                email = email ?? ""
            });
            yield return PostJson(url, body, headers, responseBody =>
            {
                var payload = JsonUtility.FromJson<CompletePurchasePayload>(responseBody);
                onSuccess?.Invoke(payload ?? new CompletePurchasePayload());
            }, onError);
        }

        private static IEnumerator GetJson(
            string url,
            StashRequestHeaders headers,
            Action<string> onSuccess,
            Action<string> onError)
        {
            using (var request = UnityWebRequest.Get(url))
            {
                ApplyHeaders(request, headers);
                request.downloadHandler = new DownloadHandlerBuffer();
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke(request.error ?? $"HTTP {request.responseCode}");
                    yield break;
                }

                onSuccess?.Invoke(request.downloadHandler.text ?? "");
            }
        }

        private static IEnumerator PostJson(
            string url,
            string jsonBody,
            StashRequestHeaders headers,
            Action<string> onSuccess,
            Action<string> onError)
        {
            var bodyBytes = Encoding.UTF8.GetBytes(jsonBody ?? "{}");
            using (var request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyBytes);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                ApplyHeaders(request, headers);
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke(request.error ?? $"HTTP {request.responseCode}");
                    yield break;
                }

                onSuccess?.Invoke(request.downloadHandler.text ?? "");
            }
        }

        private static void ApplyHeaders(UnityWebRequest request, StashRequestHeaders headers)
        {
            if (headers == null)
                return;
            if (!string.IsNullOrEmpty(headers.DeviceId))
                request.SetRequestHeader("x-stash-did", headers.DeviceId);
            if (!string.IsNullOrEmpty(headers.Authorization))
                request.SetRequestHeader("Authorization", headers.Authorization);
            if (!string.IsNullOrEmpty(headers.Cookie))
                request.SetRequestHeader("Cookie", headers.Cookie);
        }
    }

    internal sealed class StashRequestHeaders
    {
        public string DeviceId;
        public string Authorization;
        public string Cookie;
    }

    [Serializable]
    internal sealed class OrderSummaryV2Payload
    {
        public string shopHandle;
        public AdyenAdvancedFlowPayload adyenAdvancedFlow;
        public MultiPspPaymentFlowPayload multiPspPaymentFlow;
    }

    [Serializable]
    internal sealed class AdyenAdvancedFlowPayload
    {
        public string id;
    }

    [Serializable]
    internal sealed class MultiPspPaymentFlowPayload
    {
        public string sessionId;
    }

    [Serializable]
    internal sealed class LegacyOrderResultPayload
    {
        public LegacyOrderResultStatusPayload resultStatus;
    }

    [Serializable]
    internal sealed class LegacyOrderResultStatusPayload
    {
        public string status;
    }

    [Serializable]
    internal sealed class MultiPspPaymentStatusPayload
    {
        public string status;
    }

    [Serializable]
    internal sealed class CompletePurchasePayload
    {
        public string status;
    }

    [Serializable]
    internal sealed class CompletePurchaseRequest
    {
        public string shopHandle;
    }

    [Serializable]
    internal sealed class CompleteMultiPspRequest
    {
        public string paymentId;
        public string email;
    }
}
