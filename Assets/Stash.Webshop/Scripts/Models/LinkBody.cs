using System;

namespace Stash.Models
{
    /// <summary>
    /// Request body for linking an account to Stash.
    /// </summary>
    [Serializable]
    public class LinkBody
    {
        /// <summary>
        /// The Stash code challenge from the deeplink.
        /// </summary>
        public string codeChallenge;
        
        /// <summary>
        /// The user information.
        /// </summary>
        public User user;

        /// <summary>
        /// User information for account linking.
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