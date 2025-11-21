using System;

namespace Stash.Models
{
    /// <summary>
    /// Response from creating a checkout link.
    /// </summary>
    [Serializable]
    public class CheckoutResponse
    {
        /// <summary>
        /// The checkout URL to open.
        /// </summary>
        public string url;
        
        /// <summary>
        /// The checkout link ID.
        /// </summary>
        public string id;
    }
}