using System;

namespace Stash.Models
{
    /// <summary>
    /// Request body for custom login with Facebook.
    /// </summary>
    [Serializable]
    public class LoginFacebook
    {
        /// <summary>
        /// The Stash code challenge from the login deeplink.
        /// </summary>
        public string code;
        
        /// <summary>
        /// The Facebook app ID.
        /// </summary>
        public string appId;
        
        /// <summary>
        /// The Facebook access token.
        /// </summary>
        public string inputToken;
        
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