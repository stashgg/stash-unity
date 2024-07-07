using System;

namespace Stash.Models
{
    [Serializable]
    public class LoginGooglePlayGamesBody
    {
        public string code;
        public string authCode;
        public User user;
        
        [Serializable]
        public class User
        {
            public string id;
        }
    }
}