using System;

namespace Stash.Models
{
    [Serializable]
    public class LinkBody
    {
        public string code_challenge;
        public User user;

        [Serializable]
        public class User
        {
            public string id;
        }
    }
}