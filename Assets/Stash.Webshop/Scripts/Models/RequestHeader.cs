namespace Stash.Models
{
    /// <summary>
    /// Represents an HTTP request header key-value pair.
    /// </summary>
    public class RequestHeader
    {
        /// <summary>
        /// The header key (name).
        /// </summary>
        public string Key { get; set; }
        
        /// <summary>
        /// The header value.
        /// </summary>
        public string Value { get; set; }
    }
}
