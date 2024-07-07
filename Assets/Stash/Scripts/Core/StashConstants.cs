namespace Stash.Core
{
    public class StashConstants
    {
        //Root URLs
        public const string RootUrl = "https://api.stash.gg";
        public const string RootUrlTest = "https://test-api.stash.gg";
        
        //Account Linking 
        public const string LinkAccount = "/sdk/link_code/link";
        public const string LinkAppleGameCenter = "/sdk/link_code/link_apple_game_center";
        public const string LinkGooglePlayGames = "/sdk/link_code/link_google_play";
        
        //Custom Login
        public const string CustomLogin = "/sdk/custom_login/approve_with_jwt";
        public const string LoginAppleGameCenter = "/sdk/custom_login/approve_apple_game_center";
        public const string LoginGooglePlayGames = "/sdk/custom_login/google_play";
        
    }
}
