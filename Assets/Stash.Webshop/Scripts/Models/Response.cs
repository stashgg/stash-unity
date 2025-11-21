namespace Stash.Models
{
    /// <summary>
    /// Represents a response from an HTTP request to the Stash API.
    /// </summary>
    public class Response
    {
        /// <summary>
        /// The HTTP status code of the response.
        /// </summary>
        public long StatusCode { get; set; }

        /// <summary>
        /// The error message if the request failed, null otherwise.
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// The response data as a JSON string.
        /// </summary>
        public string Data { get; set; }
    }
}