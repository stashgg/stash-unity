using System;

namespace Stash.Models
{
    /// <summary>
    /// Request body for generating a loyalty URL.
    /// </summary>
    [Serializable]
    public class LoyaltyUrlBody
    {
        /// <summary>
        /// The user information.
        /// </summary>
        public User user;

        /// <summary>
        /// User information for loyalty URL generation.
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