using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Stash.Native
{
    internal sealed class StashExternalPaymentResolver
    {
        private const string DeviceIdPlayerPrefsKey = "stash-device-id";

        private readonly StashNative _host;
        private readonly HashSet<string> _handledPaymentIds = new HashSet<string>(StringComparer.Ordinal);

        private StashCheckoutContext _sessionContext = new StashCheckoutContext();
        private Coroutine _pollCoroutine;
        private bool _resolutionCompleted;

        public StashExternalPaymentResolver(StashNative host)
        {
            _host = host;
        }

        public void BeginCheckoutSession(string checkoutUrl)
        {
            CancelPolling();
            _sessionContext = StashCheckoutUrlParser.Parse(checkoutUrl);
            _sessionContext.ExternalPaymentInProgress = false;
            _host.StartCoroutine(PrefetchOrderSummary(_sessionContext));
        }

        public void OnExternalPayment(string externalUrl)
        {
            var parsed = StashCheckoutUrlParser.Parse(externalUrl);
            _sessionContext = StashCheckoutUrlParser.Merge(_sessionContext, parsed);
            _sessionContext.ExternalPaymentInProgress = true;
            _host.StartCoroutine(PrefetchOrderSummary(_sessionContext));
        }

        public void OnBrowserClosed()
        {
            if (!_sessionContext.ExternalPaymentInProgress)
                return;

            _sessionContext.ExternalPaymentInProgress = false;
            CancelPolling();
            _resolutionCompleted = false;
            _pollCoroutine = _host.StartCoroutine(PollUntilTerminal());
        }

        public void ResetSession()
        {
            CancelPolling();
            _sessionContext = new StashCheckoutContext();
        }

        public bool TryMarkPaymentHandled(string paymentId)
        {
            if (string.IsNullOrEmpty(paymentId))
                return false;
            return _handledPaymentIds.Add(paymentId);
        }

        public void NotePaymentHandledFromNative(string orderPayload)
        {
            var paymentId = TryExtractPaymentId(orderPayload);
            if (!string.IsNullOrEmpty(paymentId))
                _handledPaymentIds.Add(paymentId);
        }

        private static string TryExtractPaymentId(string orderPayload)
        {
            if (string.IsNullOrEmpty(orderPayload))
                return null;

            const string marker = "\"paymentId\"";
            var index = orderPayload.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                return null;

            var colon = orderPayload.IndexOf(':', index + marker.Length);
            if (colon < 0)
                return null;

            var startQuote = orderPayload.IndexOf('"', colon + 1);
            if (startQuote < 0)
                return null;
            var endQuote = orderPayload.IndexOf('"', startQuote + 1);
            if (endQuote <= startQuote)
                return null;

            return orderPayload.Substring(startQuote + 1, endQuote - startQuote - 1);
        }

        private void CancelPolling()
        {
            if (_pollCoroutine != null)
            {
                _host.StopCoroutine(_pollCoroutine);
                _pollCoroutine = null;
            }
        }

        private IEnumerator PrefetchOrderSummary(StashCheckoutContext context)
        {
            if (string.IsNullOrEmpty(context.CheckoutLinkId) || string.IsNullOrEmpty(context.ApiBaseUrl))
                yield break;

            var headers = BuildRequestHeaders(context.CheckoutPageUrl);
            OrderSummaryV2Payload summary = null;
            string error = null;

            yield return StashPaymentApiClient.FetchOrderSummaryV2(
                context.ApiBaseUrl,
                context.CheckoutLinkId,
                headers,
                payload => summary = payload,
                err => error = err);

            if (summary == null)
            {
                if (!string.IsNullOrEmpty(error))
                    Debug.LogWarning("[StashNative] Failed to prefetch order summary for external payment: " + error);
                yield break;
            }

            if (!string.IsNullOrEmpty(summary.shopHandle))
                context.ShopHandle = summary.shopHandle;

            if (summary.multiPspPaymentFlow != null && !string.IsNullOrEmpty(summary.multiPspPaymentFlow.sessionId))
                context.Flow = StashCheckoutPaymentFlow.MultiPsp;
            else if (summary.adyenAdvancedFlow != null && !string.IsNullOrEmpty(summary.adyenAdvancedFlow.id))
            {
                context.Flow = StashCheckoutPaymentFlow.AdyenLegacy;
                context.LegacyPaymentIntentId = summary.adyenAdvancedFlow.id;
                if (string.IsNullOrEmpty(context.PaymentId))
                    context.PaymentId = summary.adyenAdvancedFlow.id;
            }
        }

        private IEnumerator PollUntilTerminal()
        {
            var context = _sessionContext;
            if (string.IsNullOrEmpty(context.ApiBaseUrl))
            {
                FireFailure();
                yield break;
            }

            if (context.Flow == StashCheckoutPaymentFlow.MultiPsp &&
                !string.IsNullOrEmpty(context.CheckoutLinkId) &&
                !string.IsNullOrEmpty(context.PaymentId))
            {
                yield return PollMultiPsp(context);
                yield break;
            }

            if (!string.IsNullOrEmpty(context.LegacyPaymentIntentId))
            {
                context.Flow = StashCheckoutPaymentFlow.AdyenLegacy;
                yield return PollLegacyAdyen(context);
                yield break;
            }

            if (!string.IsNullOrEmpty(context.CheckoutLinkId))
            {
                yield return PrefetchOrderSummary(context);
                if (context.Flow == StashCheckoutPaymentFlow.MultiPsp &&
                    !string.IsNullOrEmpty(context.PaymentId))
                {
                    yield return PollMultiPsp(context);
                    yield break;
                }

                if (!string.IsNullOrEmpty(context.LegacyPaymentIntentId))
                {
                    yield return PollLegacyAdyen(context);
                    yield break;
                }
            }

            Debug.LogWarning("[StashNative] External payment browser closed but checkout context was incomplete; treating as failure.");
            FireFailure();
        }

        private IEnumerator PollLegacyAdyen(StashCheckoutContext context)
        {
            var paymentIntentId = context.LegacyPaymentIntentId;
            var headers = BuildRequestHeaders(context.CheckoutPageUrl);
            var startedAt = Time.realtimeSinceStartup;

            while (Time.realtimeSinceStartup - startedAt < StashPaymentApiClient.MaxPollDurationSeconds)
            {
                LegacyOrderResultPayload result = null;
                string error = null;
                yield return StashPaymentApiClient.GetLegacyOrderResult(
                    context.ApiBaseUrl,
                    paymentIntentId,
                    headers,
                    payload => result = payload,
                    err => error = err);

                if (result?.resultStatus != null)
                {
                    var status = result.resultStatus.status ?? "";
                    if (status.Equals("SUCCEEDED", StringComparison.OrdinalIgnoreCase))
                    {
                        yield return CompleteLegacyAndFire(context, paymentIntentId, headers);
                        yield break;
                    }

                    if (status.Equals("CANCELED", StringComparison.OrdinalIgnoreCase))
                    {
                        FireFailure(paymentIntentId);
                        yield break;
                    }
                }
                else if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogWarning("[StashNative] Legacy order result poll failed: " + error);
                }

                yield return new WaitForSecondsRealtime(StashPaymentApiClient.PollIntervalSeconds);
            }

            FireFailure(paymentIntentId);
        }

        private IEnumerator CompleteLegacyAndFire(
            StashCheckoutContext context,
            string paymentIntentId,
            StashRequestHeaders headers)
        {
            CompletePurchasePayload complete = null;
            string error = null;
            yield return StashPaymentApiClient.CompleteLegacyPurchase(
                context.ApiBaseUrl,
                paymentIntentId,
                context.ShopHandle,
                headers,
                payload => complete = payload,
                err => error = err);

            if (complete != null &&
                (complete.status ?? "").Equals("SUCCEEDED", StringComparison.OrdinalIgnoreCase))
            {
                FireSuccess(paymentIntentId);
                yield break;
            }

            if (!string.IsNullOrEmpty(error))
                Debug.LogWarning("[StashNative] Legacy complete purchase failed: " + error);

            FireFailure(paymentIntentId);
        }

        private IEnumerator PollMultiPsp(StashCheckoutContext context)
        {
            var paymentId = context.PaymentId;
            var headers = BuildRequestHeaders(context.CheckoutPageUrl);
            var startedAt = Time.realtimeSinceStartup;
            var completeTriggered = false;

            while (Time.realtimeSinceStartup - startedAt < StashPaymentApiClient.MaxPollDurationSeconds)
            {
                MultiPspPaymentStatusPayload statusPayload = null;
                string error = null;
                yield return StashPaymentApiClient.GetMultiPspPaymentStatus(
                    context.ApiBaseUrl,
                    context.CheckoutLinkId,
                    paymentId,
                    headers,
                    payload => statusPayload = payload,
                    err => error = err);

                var status = statusPayload?.status ?? "";
                if (IsMultiPspFailureStatus(status))
                {
                    FireFailure(paymentId);
                    yield break;
                }

                if (IsMultiPspSuccessStatus(status))
                {
                    if (!completeTriggered &&
                        status.Equals("PAYMENT_STATUS_AUTHORIZATION_SUCCEEDED", StringComparison.Ordinal))
                    {
                        completeTriggered = true;
                        CompletePurchasePayload complete = null;
                        string completeError = null;
                        yield return StashPaymentApiClient.CompleteMultiPspCheckout(
                            context.ApiBaseUrl,
                            paymentId,
                            email: null,
                            headers,
                            payload => complete = payload,
                            err => completeError = err);

                        if (complete != null &&
                            (complete.status ?? "").Equals("SUCCEEDED", StringComparison.OrdinalIgnoreCase))
                        {
                            FireSuccess(paymentId);
                            yield break;
                        }

                        if (!string.IsNullOrEmpty(completeError))
                            Debug.LogWarning("[StashNative] Multi-PSP complete failed: " + completeError);

                        FireFailure(paymentId);
                        yield break;
                    }

                    FireSuccess(paymentId);
                    yield break;
                }

                if (!string.IsNullOrEmpty(error))
                    Debug.LogWarning("[StashNative] Multi-PSP status poll failed: " + error);

                yield return new WaitForSecondsRealtime(StashPaymentApiClient.PollIntervalSeconds);
            }

            FireFailure(paymentId);
        }

        private static bool IsMultiPspSuccessStatus(string status)
        {
            return status.Equals("PAYMENT_STATUS_AUTHORIZATION_SUCCEEDED", StringComparison.Ordinal) ||
                   status.Equals("PAYMENT_STATUS_CAPTURE_SUCCEEDED", StringComparison.Ordinal);
        }

        private static bool IsMultiPspFailureStatus(string status)
        {
            return status.Equals("PAYMENT_STATUS_AUTHORIZATION_FAILED", StringComparison.Ordinal) ||
                   status.Equals("PAYMENT_STATUS_CAPTURE_FAILED", StringComparison.Ordinal);
        }

        private void FireSuccess(string paymentId)
        {
            if (_resolutionCompleted)
                return;
            if (!string.IsNullOrEmpty(paymentId) && !TryMarkPaymentHandled(paymentId))
                return;

            _resolutionCompleted = true;
            var payload = string.IsNullOrEmpty(paymentId) ? "" : $"{{\"paymentId\":\"{EscapeJson(paymentId)}\"}}";
            _host.DispatchPaymentSuccess(payload);
        }

        private void FireFailure(string paymentId = null)
        {
            if (_resolutionCompleted)
                return;
            if (!string.IsNullOrEmpty(paymentId) && !TryMarkPaymentHandled(paymentId))
                return;

            _resolutionCompleted = true;
            _host.DispatchPaymentFailure();
        }

        private static string EscapeJson(string value)
        {
            return (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static StashRequestHeaders BuildRequestHeaders(string checkoutPageUrl)
        {
            var headers = new StashRequestHeaders
            {
                DeviceId = GetOrCreateDeviceId()
            };

#if UNITY_ANDROID && !UNITY_EDITOR
            if (!string.IsNullOrEmpty(checkoutPageUrl))
            {
                try
                {
                    using (var cookieManager = new AndroidJavaClass("android.webkit.CookieManager"))
                    {
                        var instance = cookieManager.CallStatic<AndroidJavaObject>("getInstance");
                        var cookies = instance.Call<string>("getCookie", checkoutPageUrl);
                        if (!string.IsNullOrEmpty(cookies))
                            headers.Cookie = cookies;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[StashNative] Failed to read checkout cookies: " + e.Message);
                }
            }
#endif
            return headers;
        }

        private static string GetOrCreateDeviceId()
        {
            var existing = PlayerPrefs.GetString(DeviceIdPlayerPrefsKey, "");
            if (!string.IsNullOrEmpty(existing))
                return existing;

            var deviceId = Guid.NewGuid().ToString();
            PlayerPrefs.SetString(DeviceIdPlayerPrefsKey, deviceId);
            PlayerPrefs.Save();
            return deviceId;
        }
    }
}
