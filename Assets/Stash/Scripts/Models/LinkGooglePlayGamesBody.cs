using System;

namespace Stash.Models
{
    [Serializable]
    public class LinkGooglePlayGamesBody
    {
        public string codeChallenge;
        public Verification verification;
        public User user;
        
        [Serializable]
        public class Verification
        {
            public string authCode;
        }
        
        [Serializable]
        public class User
        {
            public string id;
        }
    }
}