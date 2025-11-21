using System;

namespace Stash.Webshop.Exceptions
{
    /// <summary>
    /// Thrown when any error occurs while processing a request on the Stash API.
    /// Parses the API error code and message.
    /// </summary>
    [Serializable]
    public class StashRequestError : Exception
    {
        public long Code { get; }
        public new string Message { get; }
        
        public StashRequestError() { }

        public StashRequestError(long code, string message = null)
            : base($"[STASH] Error - Code: {code}, Message: {message}")
        {
            Code = code;
            Message = message;
        }
    }
    
    /// <summary>
    /// Exception occurs while parsing the Stash API response, may also occur when the API or network is not reachable.
    /// </summary>
    [Serializable]
    public class StashParseError : Exception
    {
        public StashParseError() { }

        public StashParseError(string message = null)
            : base($"[STASH] Error while parsing API response. Message: {message}")
        {

        }
    }
}