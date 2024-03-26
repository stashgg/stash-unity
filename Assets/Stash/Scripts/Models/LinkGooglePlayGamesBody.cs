using System;

namespace Stash.Models
{
    [Serializable]
    public class LinkGooglePlayGamesBody
    {
        public string codeChallenge;
        public string authCode;
        public User user;
        
        [Serializable]
        public class User
        {
            public string id;
        }
    }
}