using System;

namespace Stash.Models
{
    /// <summary>
    /// Response from generating a loyalty URL.
    /// </summary>
    [Serializable]
    public class LoyaltyUrlResponse
    {
        /// <summary>
        /// The loyalty URL to open.
        /// </summary>
        public string url;
    }
}