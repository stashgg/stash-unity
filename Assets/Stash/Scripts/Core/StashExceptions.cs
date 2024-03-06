using System;

namespace Stash.Core.Exceptions
{
    [Serializable]
    public class StashAPIRequestError : Exception
    {
        public StashAPIRequestError() { }

        public StashAPIRequestError(long code, string message = null)
            : base($"Error Code: {code}, Message: {message}")
        {

        }
    }
    
    [Serializable]
    public class StashParseError : Exception
    {
        public StashParseError() { }

        public StashParseError(string message = null)
            : base($"Failed while parsing the Stash API response. Message: {message}")
        {

        }
    }
}