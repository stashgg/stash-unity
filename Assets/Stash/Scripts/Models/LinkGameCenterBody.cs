using System;

namespace Stash.Models
{
    [Serializable]
    public class LinkGameCenterBody
    {
        public string codeChallenge;
        public Player player;
        public Verification verification;

        [Serializable]
        public class Player
        {
            public string bundleId;
            public string playerId;
        }
        
        [Serializable]
        public class Verification
        {
            public string signature;
            public string salt;
            public string publicKeyUrl;
            public string timestamp;
        }
    }
}