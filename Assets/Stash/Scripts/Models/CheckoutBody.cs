using System;

namespace Stash.Models
{
    [Serializable]
    public class CheckoutBody
    {
        public Item item;
        public User user;

        [Serializable]
        public class Item
        {
            public string id;
        }

         [Serializable]
        public class User
        {
            public string id;
        }
    }
}