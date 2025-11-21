using System;

namespace Stash.Models
{
    /// <summary>
    /// Request body for launcher checkout operations.
    /// </summary>
    [Serializable]
    public class CheckoutBody
    {
        /// <summary>
        /// The item to purchase.
        /// </summary>
        public Item item;
        
        /// <summary>
        /// The user information.
        /// </summary>
        public User user;

        /// <summary>
        /// Item information for checkout.
        /// </summary>
        [Serializable]
        public class Item
        {
            /// <summary>
            /// The item ID.
            /// </summary>
            public string id;
        }

        /// <summary>
        /// User information for checkout.
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