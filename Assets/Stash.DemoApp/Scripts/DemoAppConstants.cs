namespace Stash.Samples
{
    /// <summary>Stash API environment (DemoApp only; no dependency on Stash.Webshop).</summary>
    public enum StashDemoEnvironment
    {
        Test,
        Production
    }

    /// <summary>
    /// Constants and configuration values for the Stash Demo App.
    /// Centralizes configuration to avoid hardcoded values throughout the codebase.
    /// </summary>
    public static class DemoAppConstants
    {
        public const string StashApiBaseUrlTest = "https://test-api.stash.gg";
        public const string StashApiBaseUrlProduction = "https://api.stash.gg";
        #region PlayerPrefs Keys
        public const string PREF_ORIENTATION_MODE = "OrientationMode";
        public const string PREF_STASH_API_KEY = "StashApiKey";
        public const string PREF_STASH_ENVIRONMENT = "StashEnvironment";
        public const string PREF_CHANNEL_SELECTION_URL = "ChannelSelectionUrl";
        public const string PREF_SHOW_METRICS = "ShowMetrics";
        public const string PREF_USE_SAFARI_WEBVIEW = "UseSafariWebView";
        /* Checkout config */
        public const string PREF_CHECKOUT_FORCE_PORTRAIT = "CheckoutForcePortrait";
        public const string PREF_CHECKOUT_CARD_HEIGHT_PORTRAIT = "CheckoutCardHeightPortrait";
        public const string PREF_CHECKOUT_CARD_WIDTH_LANDSCAPE = "CheckoutCardWidthLandscape";
        public const string PREF_CHECKOUT_CARD_HEIGHT_LANDSCAPE = "CheckoutCardHeightLandscape";
        public const string PREF_CHECKOUT_TABLET_W_PORTRAIT = "CheckoutTabletWPortrait";
        public const string PREF_CHECKOUT_TABLET_H_PORTRAIT = "CheckoutTabletHPortrait";
        public const string PREF_CHECKOUT_TABLET_W_LANDSCAPE = "CheckoutTabletWLandscape";
        public const string PREF_CHECKOUT_TABLET_H_LANDSCAPE = "CheckoutTabletHLandscape";
        /* Modal config */
        public const string PREF_MODAL_SHOW_DRAG_BAR = "ModalShowDragBar";
        public const string PREF_MODAL_ALLOW_DISMISS = "ModalAllowDismiss";
        public const string PREF_MODAL_PHONE_W_PORTRAIT = "ModalPhoneWPortrait";
        public const string PREF_MODAL_PHONE_H_PORTRAIT = "ModalPhoneHPortrait";
        public const string PREF_MODAL_PHONE_W_LANDSCAPE = "ModalPhoneWLandscape";
        public const string PREF_MODAL_PHONE_H_LANDSCAPE = "ModalPhoneHLandscape";
        public const string PREF_MODAL_TABLET_W_PORTRAIT = "ModalTabletWPortrait";
        public const string PREF_MODAL_TABLET_H_PORTRAIT = "ModalTabletHPortrait";
        public const string PREF_MODAL_TABLET_W_LANDSCAPE = "ModalTabletWLandscape";
        public const string PREF_MODAL_TABLET_H_LANDSCAPE = "ModalTabletHLandscape";
        public const string PREF_HELP_DISMISSED = "help_dismissed_";
        public const string PREF_PKCE_CODE_VERIFIER = "PKCE_CODE_VERIFIER";
        #endregion

        #region Default Values
        public const string DEFAULT_ORIENTATION_MODE = "Portrait";
        public const string DEFAULT_CURRENCY = "USD";
        public const string DEFAULT_REGION_ALPHA3 = "USA";
        public const string DEFAULT_REGION_ALPHA2 = "US";
        public const string DEFAULT_SHOP_HANDLE = "demo-shop";
        #endregion

        #region Orientation Modes
        public const string ORIENTATION_AUTO = "Auto";
        public const string ORIENTATION_PORTRAIT = "Portrait";
        public const string ORIENTATION_LANDSCAPE = "Landscape";
        #endregion

        public static string GetStashApiBaseUrl(StashDemoEnvironment env)
        {
            return env == StashDemoEnvironment.Production ? StashApiBaseUrlProduction : StashApiBaseUrlTest;
        }
    }
}

