namespace Stash.Webshop
{
    /// <summary>
    /// Represents the Stash API environment configuration.
    /// </summary>
    public enum StashEnvironment
    {
        /// <summary>
        /// Test environment for development and testing.
        /// </summary>
        Test,
        
        /// <summary>
        /// Production environment for live applications.
        /// </summary>
        Production
    }
    
    /// <summary>
    /// Extension methods for StashEnvironment enum.
    /// </summary>
    public static class StashEnvironmentExtensions
    {
        /// <summary>
        /// Gets the root URL for the specified Stash environment.
        /// </summary>
        /// <param name="env">The Stash environment.</param>
        /// <returns>The root URL string for the environment.</returns>
        public static string GetRootUrl(this StashEnvironment env)
        {
            return env switch
            {
                StashEnvironment.Test => StashConstants.RootUrlTest,
                StashEnvironment.Production => StashConstants.RootUrl,
                _ => StashConstants.RootUrl
            };
        }
    }
}