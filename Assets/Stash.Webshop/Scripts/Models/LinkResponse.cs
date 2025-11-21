namespace Stash.Models
{
    /// <summary>
    /// Response from linking an account or custom login operation.
    /// </summary>
    public class LinkResponse
    {
        /// <summary>
        /// The code challenge returned from the API.
        /// </summary>
        public string codeChallenge { get; set; }
    }
}