using System;

namespace Stash.Models
{
    [Serializable]
    public class CustomLoginBody
    {
        public string code;
        public User user;

        [Serializable]
        public class User
        {
            public string id;
            public string profile_image_url;
        }
    }
}