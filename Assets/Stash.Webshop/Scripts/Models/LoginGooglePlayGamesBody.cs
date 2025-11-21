using System;

namespace Stash.Models
{
    /// <summary>
    /// Request body for custom login with Google Play Games.
    /// </summary>
    [Serializable]
    public class LoginGooglePlayGamesBody
    {
        /// <summary>
        /// The Stash code challenge from the login deeplink.
        /// </summary>
        public string code;
        
        /// <summary>
        /// The authorization code generated using RequestServerSideAccess from GooglePlayGames.
        /// </summary>
        public string authCode;
        
        /// <summary>
        /// The user information.
        /// </summary>
        public User user;
        
        /// <summary>
        /// User information for custom login.
        /// </summary>
        [Serializable]
        public class User
        {
            /// <summary>
            /// The player identification.
            /// </summary>
            public string id;
        }
    }
}