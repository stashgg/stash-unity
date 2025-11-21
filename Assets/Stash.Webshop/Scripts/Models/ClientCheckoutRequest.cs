using System;

namespace Stash.Models
{
    /// <summary>
    /// Request body for creating a checkout link using the client API with Bearer token authentication.
    /// </summary>
    [Serializable]
    public class ClientCheckoutRequest
    {
        public User user;
        public string shop_handle;
        public string item_id;

        [Serializable]
        public class User
        {
            public string id;
            public string platform;
            public string validated_email;
            public string profile_image_url;
            public string display_name;
            public string region_code;
        }
    }
}

