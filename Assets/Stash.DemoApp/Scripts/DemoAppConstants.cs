namespace Stash.Samples
{
    /// <summary>
    /// Constants and configuration values for the Stash Demo App.
    /// Centralizes configuration to avoid hardcoded values throughout the codebase.
    /// </summary>
    public static class DemoAppConstants
    {
        #region PlayerPrefs Keys
        public const string PREF_ORIENTATION_MODE = "OrientationMode";
        public const string PREF_PAYMENT_METHOD = "PaymentMethod";
        public const string PREF_STASH_API_KEY = "StashApiKey";
        public const string PREF_STASH_ENVIRONMENT = "StashEnvironment";
        public const string PREF_CHANNEL_SELECTION_URL = "ChannelSelectionUrl";
        public const string PREF_SHOW_METRICS = "ShowMetrics";
        public const string PREF_USE_CUSTOM_POPUP_SIZE = "UseCustomPopupSize";
        public const string PREF_POPUP_PORTRAIT_WIDTH = "PopupPortraitWidth";
        public const string PREF_POPUP_PORTRAIT_HEIGHT = "PopupPortraitHeight";
        public const string PREF_POPUP_LANDSCAPE_WIDTH = "PopupLandscapeWidth";
        public const string PREF_POPUP_LANDSCAPE_HEIGHT = "PopupLandscapeHeight";
        public const string PREF_HELP_DISMISSED = "help_dismissed_";
        public const string PREF_PKCE_CODE_VERIFIER = "PKCE_CODE_VERIFIER";
        #endregion

        #region Default Values
        public const string DEFAULT_ORIENTATION_MODE = "Portrait";
        public const string DEFAULT_PAYMENT_METHOD = "STASH_PAY";
        public const string DEFAULT_CURRENCY = "USD";
        public const string DEFAULT_SHOP_HANDLE = "demo-shop";
        #endregion

        #region Payment Methods
        public const string PAYMENT_METHOD_NATIVE_IAP = "NATIVE_IAP";
        public const string PAYMENT_METHOD_STASH_PAY = "STASH_PAY";
        #endregion

        #region Orientation Modes
        public const string ORIENTATION_AUTO = "Auto";
        public const string ORIENTATION_PORTRAIT = "Portrait";
        public const string ORIENTATION_LANDSCAPE = "Landscape";
        #endregion
    }
}

