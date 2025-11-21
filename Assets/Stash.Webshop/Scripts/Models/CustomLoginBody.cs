using System;

namespace Stash.Models
{
    /// <summary>
    /// Request body for custom login to Stash.
    /// </summary>
    [Serializable]
    public class CustomLoginBody
    {
        /// <summary>
        /// The Stash code challenge from the login deeplink.
        /// </summary>
        public string code;
        
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
            
            /// <summary>
            /// Optional profile image URL of the player.
            /// </summary>
            public string profile_image_url;
        }
    }
}