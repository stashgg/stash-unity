using System;

namespace Stash.Models
{
    [Serializable]
    public class LoginFacebook
    {
        public string code;
        public string appId;
        public string inputToken;
        public User user;
        
        [Serializable]
        public class User
        {
            public string id;
        }
    }
}