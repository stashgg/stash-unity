using System;

namespace Stash.Models
{
    [Serializable]
    public class LinkGameCenterBody
    {
        public string codeChallenge;
        public Verification verification;
        public User user;
        
        [Serializable]
        public class Verification
        {
            public Player player;
            public Response response;
        }
        
        [Serializable]
        public class Player
        {
            public string bundleId;
            public string teamPlayerId;
        }
        
        [Serializable]
        public class Response
        {
            public string signature;
            public string salt;
            public string publicKeyUrl;
            public string timestamp;
        }
        
        [Serializable]
        public class User
        {
            public string id;
        }
    }
}