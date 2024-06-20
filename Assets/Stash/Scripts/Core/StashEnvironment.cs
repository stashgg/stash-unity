namespace Stash.Scripts.Core
{
    public enum StashEnvironment
    {
        Test,
        Production
    }
    
    public static class StashEnvironmentAdapter {
        public static string GetRootUrl(this StashEnvironment env) {
            switch (env)
            {
                case StashEnvironment.Test:
                    return Stash.Core.StashConstants.RootUrlTest;
                case StashEnvironment.Production:
                default:
                    return Stash.Core.StashConstants.RootUrl;
                
            }
        }
    }
}