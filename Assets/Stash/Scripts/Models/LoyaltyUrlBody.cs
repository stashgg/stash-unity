using System;

namespace Stash.Models
{
    [Serializable]
    public class LoyaltyUrlBody
    {
        public User user;

         [Serializable]
        public class User
        {
            public string id;
        }
    }
}