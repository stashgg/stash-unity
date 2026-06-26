using System;
using System.Text.RegularExpressions;

namespace Stash.Native
{
    internal enum StashCheckoutPaymentFlow
    {
        Unknown,
        AdyenLegacy,
        MultiPsp
    }

    internal sealed class StashCheckoutContext
    {
        public string CheckoutLinkId;
        public string ShopHandle;
        public string PaymentId;
        public string LegacyPaymentIntentId;
        public string ApiBaseUrl;
        public string CheckoutPageUrl;
        public StashCheckoutPaymentFlow Flow = StashCheckoutPaymentFlow.Unknown;
        public bool ExternalPaymentInProgress;
    }

    internal static class StashCheckoutUrlParser
    {
        private static readonly Regex PayOrderPath =
            new Regex(@"/pay/(?!success|failed|confirming|authorizing|channel-selection)([^/?#]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex LegacyOrderPath =
            new Regex(@"/order/([^/?#]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex PaymentIntentPath =
            new Regex(@"/pay/(?:success|failed|confirming)/([^/?#]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static StashCheckoutContext Parse(string url)
        {
            var context = new StashCheckoutContext();
            if (string.IsNullOrWhiteSpace(url))
                return context;

            if (!Uri.TryCreate(NormalizeUrl(url), UriKind.Absolute, out var uri))
                return context;

            context.CheckoutPageUrl = uri.GetLeftPart(UriPartial.Path);
            context.ApiBaseUrl = ResolveApiBaseUrl(uri);
            context.ShopHandle = ParseShopHandle(uri);
            context.CheckoutLinkId = ParseCheckoutLinkId(uri);
            context.PaymentId = ParseQueryValue(uri, "paymentId");
            context.LegacyPaymentIntentId = ParsePaymentIntentId(uri);

            if (string.IsNullOrEmpty(context.PaymentId))
                context.PaymentId = context.LegacyPaymentIntentId;

            return context;
        }

        public static StashCheckoutContext Merge(StashCheckoutContext primary, StashCheckoutContext secondary)
        {
            if (primary == null) return secondary ?? new StashCheckoutContext();
            if (secondary == null) return primary;

            if (string.IsNullOrEmpty(primary.CheckoutLinkId))
                primary.CheckoutLinkId = secondary.CheckoutLinkId;
            if (string.IsNullOrEmpty(primary.ShopHandle))
                primary.ShopHandle = secondary.ShopHandle;
            if (string.IsNullOrEmpty(primary.PaymentId))
                primary.PaymentId = secondary.PaymentId;
            if (string.IsNullOrEmpty(primary.LegacyPaymentIntentId))
                primary.LegacyPaymentIntentId = secondary.LegacyPaymentIntentId;
            if (string.IsNullOrEmpty(primary.ApiBaseUrl))
                primary.ApiBaseUrl = secondary.ApiBaseUrl;
            if (string.IsNullOrEmpty(primary.CheckoutPageUrl))
                primary.CheckoutPageUrl = secondary.CheckoutPageUrl;
            if (primary.Flow == StashCheckoutPaymentFlow.Unknown)
                primary.Flow = secondary.Flow;

            return primary;
        }

        public static string NormalizeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return url;
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return "https://" + url;
            return url;
        }

        private static string ParseCheckoutLinkId(Uri uri)
        {
            var path = uri.AbsolutePath ?? "";
            var payMatch = PayOrderPath.Match(path);
            if (payMatch.Success)
                return payMatch.Groups[1].Value;

            var legacyMatch = LegacyOrderPath.Match(path);
            if (legacyMatch.Success)
                return legacyMatch.Groups[1].Value;

            return null;
        }

        private static string ParsePaymentIntentId(Uri uri)
        {
            var fromPath = PaymentIntentPath.Match(uri.AbsolutePath ?? "");
            if (fromPath.Success)
                return fromPath.Groups[1].Value;

            return ParseQueryValue(uri, "paymentIntentId");
        }

        private static string ParseShopHandle(Uri uri)
        {
            var fromQuery = ParseQueryValue(uri, "shopHandle");
            if (!string.IsNullOrEmpty(fromQuery))
                return fromQuery;

            var host = uri.Host ?? "";
            if (host.Equals("checkout.stash.gg", StringComparison.OrdinalIgnoreCase) ||
                host.Equals("checkout.stashstaging.com", StringComparison.OrdinalIgnoreCase) ||
                host.Equals("test.stashpreview.com", StringComparison.OrdinalIgnoreCase))
                return null;

            const string stagingSuffix = ".stashstaging.com";
            const string prodSuffix = ".stash.gg";
            const string previewSuffix = ".stashpreview.com";

            if (host.EndsWith(stagingSuffix, StringComparison.OrdinalIgnoreCase))
                return host.Substring(0, host.Length - stagingSuffix.Length);
            if (host.EndsWith(prodSuffix, StringComparison.OrdinalIgnoreCase))
                return host.Substring(0, host.Length - prodSuffix.Length);
            if (host.EndsWith(previewSuffix, StringComparison.OrdinalIgnoreCase))
                return host.Substring(0, host.Length - previewSuffix.Length);

            return null;
        }

        private static string ParseQueryValue(Uri uri, string key)
        {
            if (uri == null || string.IsNullOrEmpty(key))
                return null;

            var query = uri.Query;
            if (string.IsNullOrEmpty(query))
                return null;

            foreach (var part in query.TrimStart('?').Split('&'))
            {
                if (string.IsNullOrEmpty(part))
                    continue;
                var eq = part.IndexOf('=');
                var name = eq >= 0 ? part.Substring(0, eq) : part;
                if (!name.Equals(key, StringComparison.OrdinalIgnoreCase))
                    continue;
                var value = eq >= 0 ? part.Substring(eq + 1) : "";
                return Uri.UnescapeDataString(value.Replace('+', ' '));
            }

            return null;
        }

        private static string ResolveApiBaseUrl(Uri checkoutUri)
        {
            var host = checkoutUri.Host ?? "";
            if (host.Contains("stashstaging", StringComparison.OrdinalIgnoreCase) ||
                host.Contains("stashpreview", StringComparison.OrdinalIgnoreCase))
                return "https://test-api.stashstaging.com";

            return "https://api.stash.gg";
        }
    }
}
