namespace Stash.Webshop
{
    /// <summary>
    /// Contains constants used throughout the Stash SDK including API endpoints and SDK version.
    /// </summary>
    public static class StashConstants
    {
        /// <summary>
        /// The current version of the Stash Unity SDK.
        /// TODO: Switch to package file for version management.
        /// </summary>
        public const string SdkVersion = "1.0.0";
        
        /// <summary>
        /// Root URL for the Stash production API.
        /// </summary>
        public const string RootUrl = "https://api.stash.gg";
        
        /// <summary>
        /// Root URL for the Stash test API.
        /// </summary>
        public const string RootUrlTest = "https://test-api.stash.gg";
        
        /// <summary>
        /// API endpoint for linking accounts with JWT tokens.
        /// </summary>
        public const string LinkAccount = "/sdk/link_code/link";
        
        /// <summary>
        /// API endpoint for linking Apple Game Center accounts.
        /// </summary>
        public const string LinkAppleGameCenter = "/sdk/link_code/link_apple_game_center";
        
        /// <summary>
        /// API endpoint for linking Google Play Games accounts.
        /// </summary>
        public const string LinkGooglePlayGames = "/sdk/link_code/link_google_play";
        
        /// <summary>
        /// API endpoint for custom login with JWT tokens.
        /// </summary>
        public const string CustomLogin = "/sdk/custom_login/approve_with_jwt";
        
        /// <summary>
        /// API endpoint for custom login with Apple Game Center.
        /// </summary>
        public const string LoginAppleGameCenter = "/sdk/custom_login/approve_apple_game_center";
        
        /// <summary>
        /// API endpoint for custom login with Google Play Games.
        /// </summary>
        public const string LoginGooglePlayGames = "/sdk/custom_login/google_play";
        
        /// <summary>
        /// API endpoint for custom login with Facebook.
        /// </summary>
        public const string LoginFacebook = "/sdk/custom_login/facebook_auth";

        /// <summary>
        /// API endpoint for launcher checkout operations.
        /// </summary>
        public const string LauncherCheckout = "/sdk/launcher/payment/generate_add_to_cart_url";
        
        /// <summary>
        /// API endpoint for generating loyalty URLs.
        /// </summary>
        public const string LauncherLoyaltyUrl = "/sdk/generate_loyalty_url";
        
    }
}
