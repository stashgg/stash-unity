using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class SetAndroidVersionCode : IPreprocessBuildWithReport
{
    public int callbackOrder => 0; // Run early
    
    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform == BuildTarget.Android)
        {
            SetVersionCode();
        }
    }
    
    static void SetVersionCode()
    {
        // Read Unix timestamp build number from environment variable (set by GitHub Actions workflow)
        // This ensures Android versionCode matches iOS build number
        string buildNumberEnv = System.Environment.GetEnvironmentVariable("UNITY_ANDROID_BUILD_NUMBER");
        
        if (string.IsNullOrEmpty(buildNumberEnv))
        {
            Debug.LogWarning("SetAndroidVersionCode: UNITY_ANDROID_BUILD_NUMBER environment variable not set, skipping");
            return;
        }
        
        // Parse Unix timestamp (epoch seconds) as integer for Android versionCode
        if (int.TryParse(buildNumberEnv, out int buildNumber))
        {
            int oldValue = PlayerSettings.Android.bundleVersionCode;
            PlayerSettings.Android.bundleVersionCode = buildNumber;
            AssetDatabase.SaveAssets();
            Debug.Log($"SetAndroidVersionCode: Changed Android bundleVersionCode from {oldValue} to {buildNumber} (Unix timestamp)");
        }
        else
        {
            Debug.LogError($"SetAndroidVersionCode: Failed to parse build number '{buildNumberEnv}' as integer");
        }
    }
}

