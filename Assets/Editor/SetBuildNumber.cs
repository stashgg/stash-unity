using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Sets Android bundleVersionCode from environment variable before build
/// </summary>
public class SetBuildNumber : IPreprocessBuildWithReport
{
    public int callbackOrder => 0; // Run first
    
    public void OnPreprocessBuild(BuildReport report)
    {
        // Only process Android builds
        if (report.summary.platform != BuildTarget.Android)
        {
            return;
        }
        
        // Get build number from environment variable
        string buildNumberEnv = System.Environment.GetEnvironmentVariable("UNITY_ANDROID_BUILD_NUMBER");
        
        if (string.IsNullOrEmpty(buildNumberEnv))
        {
            Debug.LogWarning("SetBuildNumber: UNITY_ANDROID_BUILD_NUMBER environment variable not set, using default");
            return;
        }
        
        // Parse the build number
        if (int.TryParse(buildNumberEnv, out int buildNumber))
        {
            Debug.Log($"SetBuildNumber: Setting Android bundleVersionCode to {buildNumber}");
            PlayerSettings.Android.bundleVersionCode = buildNumber;
            Debug.Log($"SetBuildNumber: Successfully set Android bundleVersionCode to {PlayerSettings.Android.bundleVersionCode}");
        }
        else
        {
            Debug.LogError($"SetBuildNumber: Failed to parse build number from environment variable: {buildNumberEnv}");
        }
    }
}

