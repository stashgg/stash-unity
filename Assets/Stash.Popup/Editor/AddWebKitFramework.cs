using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

public class AddWebKitFramework
{
    // Higher priority (prioritized before other post processors)
    [PostProcessBuild(1)]
    public static void OnPostProcessBuild(BuildTarget buildTarget, string buildPath)
    {
#if UNITY_IOS
        if (buildTarget == BuildTarget.iOS)
        {
            Debug.Log("Adding Stash Pay Popup framework to Xcode project...");
            
            // Get the .xcodeproj path
            string projectPath = Path.Combine(buildPath, "Unity-iPhone.xcodeproj/project.pbxproj");
            
            // Read the project file
            PBXProject project = new PBXProject();
            project.ReadFromFile(projectPath);
            
            // Get target GUIDs - for Unity 2019.3+ we need both targets
            string mainTargetGuid = project.GetUnityMainTargetGuid();
            string frameworkTargetGuid = project.GetUnityFrameworkTargetGuid();
            
            // Add WebKit.framework to both targets (required and weak linked)
            project.AddFrameworkToProject(mainTargetGuid, "WebKit.framework", false);
            project.AddFrameworkToProject(frameworkTargetGuid, "WebKit.framework", false);
            
            // Write the changes to the project file
            File.WriteAllText(projectPath, project.WriteToString());
            
            // Add required import to prefix header
            string prefixHeaderPath = Path.Combine(buildPath, "Classes/Prefix.pch");
            if (File.Exists(prefixHeaderPath))
            {
                string prefixFileContent = File.ReadAllText(prefixHeaderPath);
                if (!prefixFileContent.Contains("#import <WebKit/WebKit.h>"))
                {
                    prefixFileContent = prefixFileContent.Replace("#import <Foundation/Foundation.h>", 
                                                                "#import <Foundation/Foundation.h>\n#import <WebKit/WebKit.h>");
                    File.WriteAllText(prefixHeaderPath, prefixFileContent);
                }
            }
            
            Debug.Log("Stash Pay Popup framework successfully added to Xcode project.");
        }
#else
        // Skip iOS-specific processing for non-iOS platforms
        if (buildTarget == BuildTarget.iOS)
        {
            Debug.LogWarning("iOS build support is not available. Stash Pay Popup iOS features will not be available.");
        }
#endif
    }
} 