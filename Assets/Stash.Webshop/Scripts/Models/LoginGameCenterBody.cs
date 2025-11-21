using System;

namespace Stash.Models
{
    /// <summary>
    /// Request body for custom login with Apple Game Center.
    /// </summary>
    [Serializable]
    public class LoginGameCenterBody
    {
        /// <summary>
        /// The Stash code challenge from the login deeplink.
        /// </summary>
        public string code;
        
        /// <summary>
        /// The Game Center verification data.
        /// </summary>
        public Verification verification;
        
        /// <summary>
        /// The user information.
        /// </summary>
        public User user;
        
        /// <summary>
        /// Game Center verification information.
        /// </summary>
        [Serializable]
        public class Verification
        {
            /// <summary>
            /// Player information from Game Center.
            /// </summary>
            public Player player;
            
            /// <summary>
            /// Game Center authentication response.
            /// </summary>
            public Response response;
        }
        
        /// <summary>
        /// Game Center player information.
        /// </summary>
        [Serializable]
        public class Player
        {
            /// <summary>
            /// The bundle ID of the app (CFBundleIdentifier).
            /// </summary>
            public string bundleId;
            
            /// <summary>
            /// GameKit identifier for a player of all games distributed using your Apple developer account.
            /// </summary>
            public string teamPlayerId;
        }
        
        /// <summary>
        /// Game Center authentication response data.
        /// </summary>
        [Serializable]
        public class Response
        {
            /// <summary>
            /// The verification signature data that GameKit generates (Base64 Encoded).
            /// </summary>
            public string signature;
            
            /// <summary>
            /// A random string that GameKit uses to compute the hash and randomize it (Base64 Encoded).
            /// </summary>
            public string salt;
            
            /// <summary>
            /// The URL for the public encryption key.
            /// </summary>
            public string publicKeyUrl;
            
            /// <summary>
            /// The signature's creation date and time.
            /// </summary>
            public string timestamp;
        }
        
        /// <summary>
        /// User information for custom login.
        /// </summary>
        [Serializable]
        public class User
        {
            /// <summary>
            /// The player identification.
            /// </summary>
            public string id;
        }
    }
}