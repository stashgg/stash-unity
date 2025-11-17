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
        
        Debug.Log($"SetAndroidVersionCode: Environment variable UNITY_ANDROID_BUILD_NUMBER = '{buildNumberEnv}'");
        
        if (string.IsNullOrEmpty(buildNumberEnv))
        {
            Debug.LogError("SetAndroidVersionCode: UNITY_ANDROID_BUILD_NUMBER environment variable not set! Build will use default/current value.");
            Debug.LogError("SetAndroidVersionCode: Current PlayerSettings.Android.bundleVersionCode = " + PlayerSettings.Android.bundleVersionCode);
            return;
        }
        
        // Parse Unix timestamp (epoch seconds) as integer for Android versionCode
        // Android versionCode is a 32-bit signed integer (max: 2,147,483,647)
        // Current Unix timestamps (~1.7 billion) are well within this limit
        if (int.TryParse(buildNumberEnv, out int buildNumber))
        {
            // Validate the value is reasonable (Unix timestamp should be > 1000000000 for recent dates)
            if (buildNumber < 1000000000)
            {
                Debug.LogError($"SetAndroidVersionCode: Build number {buildNumber} seems too small for a Unix timestamp. Expected > 1,000,000,000");
            }
            
            int oldValue = PlayerSettings.Android.bundleVersionCode;
            PlayerSettings.Android.bundleVersionCode = buildNumber;
            
            // Save the changes
            AssetDatabase.SaveAssets();
            
            // Verify the value was actually set
            int verifyValue = PlayerSettings.Android.bundleVersionCode;
            if (verifyValue == buildNumber)
            {
                Debug.Log($"SetAndroidVersionCode: Successfully changed Android bundleVersionCode from {oldValue} to {buildNumber} (Unix timestamp)");
            }
            else
            {
                Debug.LogError($"SetAndroidVersionCode: FAILED to set version code! Tried to set {buildNumber} but got {verifyValue}. Unity may have overridden it.");
            }
        }
        else
        {
            Debug.LogError($"SetAndroidVersionCode: Failed to parse build number '{buildNumberEnv}' as integer");
        }
    }
}

