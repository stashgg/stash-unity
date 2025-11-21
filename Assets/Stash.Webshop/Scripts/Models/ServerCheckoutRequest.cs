using System;

namespace Stash.Models
{
    /// <summary>
    /// Request body for creating a checkout link using the server API.
    /// </summary>
    [Serializable]
    public class ServerCheckoutRequest
    {
        public User user;
        public string currency;
        public Item item;

        [Serializable]
        public class User
        {
            public string platform;
            public string id;
            public string validatedEmail;
            public string displayName;
            public string avatarIconUrl;
            public string profileUrl;
        }

        [Serializable]
        public class Item
        {
            public string id;
            public string pricePerItem;
            public int quantity;
            public string imageUrl;
            public string name;
            public string description;
        }
    }
}

