using System.IO;
using System.Text;
using System.Xml;
using UnityEditor;
using UnityEditor.Android;
using UnityEngine;

namespace StashPopup.Editor
{
    /// <summary>
    /// Post-processes Android builds to inject required components into the Android manifest:
    /// - StashPayCardPortraitActivity for the checkout card UI
    /// - StashKeepAliveService for keeping Unity alive while in Chrome Custom Tabs
    /// - Required permissions for foreground service
    /// </summary>
    public class StashPopupAndroidPostProcess : IPostGenerateGradleAndroidProject
    {
        private const string ANDROID_NS = "http://schemas.android.com/apk/res/android";
        
        public int callbackOrder => 999; // Run last
        
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            Debug.Log("StashPopup: Post-processing Android project at: " + path);
            
            string manifestPath = Path.Combine(path, "src/main/AndroidManifest.xml");
            
            if (!File.Exists(manifestPath))
            {
                Debug.LogWarning("StashPopup: AndroidManifest.xml not found at: " + manifestPath);
                return;
            }
            
            try
            {
                XmlDocument manifest = new XmlDocument();
                manifest.Load(manifestPath);
                
                XmlNamespaceManager nsMgr = new XmlNamespaceManager(manifest.NameTable);
                nsMgr.AddNamespace("android", ANDROID_NS);
                
                XmlNode manifestNode = manifest.SelectSingleNode("/manifest");
                XmlNode applicationNode = manifest.SelectSingleNode("/manifest/application");
                
                if (applicationNode == null)
                {
                    Debug.LogError("StashPopup: Could not find <application> node in manifest");
                    return;
                }
                
                bool modified = false;
                
                // Add permissions for foreground service (required for keep-alive)
                modified |= AddPermissionIfMissing(manifest, manifestNode, nsMgr, "android.permission.FOREGROUND_SERVICE");
                modified |= AddPermissionIfMissing(manifest, manifestNode, nsMgr, "android.permission.FOREGROUND_SERVICE_SHORT_SERVICE");
                // Android 13+ (API 33+): required for foreground service notification to show in the notification drawer
                modified |= AddPermissionIfMissing(manifest, manifestNode, nsMgr, "android.permission.POST_NOTIFICATIONS");
                
                // Add StashPayCardPortraitActivity
                modified |= AddActivityIfMissing(manifest, applicationNode, nsMgr);
                
                // Add StashKeepAliveService for CCT keep-alive functionality
                modified |= AddKeepAliveServiceIfMissing(manifest, applicationNode, nsMgr);
                
                if (modified)
                {
                    // Save the modified manifest
                    using (XmlTextWriter writer = new XmlTextWriter(manifestPath, Encoding.UTF8))
                    {
                        writer.Formatting = Formatting.Indented;
                        manifest.Save(writer);
                    }
                    
                    Debug.Log("StashPopup: Successfully updated AndroidManifest.xml");
                }
                else
                {
                    Debug.Log("StashPopup: AndroidManifest.xml already contains all required components");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("StashPopup: Error modifying AndroidManifest.xml: " + e.Message);
            }
        }
        
        /// <summary>
        /// Adds a uses-permission element if it doesn't already exist.
        /// </summary>
        private bool AddPermissionIfMissing(XmlDocument manifest, XmlNode manifestNode, XmlNamespaceManager nsMgr, string permissionName)
        {
            // Check if permission already exists
            XmlNode existingPermission = manifestNode.SelectSingleNode(
                $"uses-permission[@android:name='{permissionName}']", nsMgr);
            
            if (existingPermission != null)
            {
                return false;
            }
            
            // Create the permission element
            XmlElement permissionElement = manifest.CreateElement("uses-permission");
            permissionElement.SetAttribute("name", ANDROID_NS, permissionName);
            
            // Insert at the beginning of manifest, before application
            XmlNode applicationNode = manifestNode.SelectSingleNode("application");
            if (applicationNode != null)
            {
                manifestNode.InsertBefore(permissionElement, applicationNode);
            }
            else
            {
                manifestNode.AppendChild(permissionElement);
            }
            
            Debug.Log($"StashPopup: Added permission {permissionName}");
            return true;
        }
        
        /// <summary>
        /// Adds StashPayCardPortraitActivity if it doesn't already exist.
        /// </summary>
        private bool AddActivityIfMissing(XmlDocument manifest, XmlNode applicationNode, XmlNamespaceManager nsMgr)
        {
            // Check if Activity already exists
            XmlNode existingActivity = applicationNode.SelectSingleNode(
                "activity[@android:name='com.stash.popup.StashPayCardPortraitActivity']", nsMgr);
            
            if (existingActivity != null)
            {
                return false;
            }
            
            // Create the Activity element
            XmlElement activityElement = manifest.CreateElement("activity");
            activityElement.SetAttribute("name", ANDROID_NS, "com.stash.popup.StashPayCardPortraitActivity");
            // NOTE: Don't set screenOrientation in manifest - set it dynamically in onCreate based on device type
            // This prevents forced portrait rotation on tablets
            activityElement.SetAttribute("theme", ANDROID_NS, "@android:style/Theme.Translucent.NoTitleBar.Fullscreen");
            activityElement.SetAttribute("launchMode", ANDROID_NS, "singleTop");
            activityElement.SetAttribute("taskAffinity", ANDROID_NS, "");
            activityElement.SetAttribute("excludeFromRecents", ANDROID_NS, "true");
            activityElement.SetAttribute("configChanges", ANDROID_NS, "orientation|screenSize|keyboardHidden");
            activityElement.SetAttribute("exported", ANDROID_NS, "false");
            activityElement.SetAttribute("hardwareAccelerated", ANDROID_NS, "true");
            activityElement.SetAttribute("noHistory", ANDROID_NS, "true");
            
            applicationNode.AppendChild(activityElement);
            
            Debug.Log("StashPopup: Added StashPayCardPortraitActivity to manifest");
            return true;
        }
        
        /// <summary>
        /// Adds StashKeepAliveService if it doesn't already exist.
        /// This service keeps the Unity process alive while the user is in Chrome Custom Tabs.
        /// </summary>
        private bool AddKeepAliveServiceIfMissing(XmlDocument manifest, XmlNode applicationNode, XmlNamespaceManager nsMgr)
        {
            // Check if Service already exists
            XmlNode existingService = applicationNode.SelectSingleNode(
                "service[@android:name='com.stash.popup.keepalive.StashKeepAliveService']", nsMgr);
            
            if (existingService != null)
            {
                return false;
            }
            
            // Create the Service element
            XmlElement serviceElement = manifest.CreateElement("service");
            serviceElement.SetAttribute("name", ANDROID_NS, "com.stash.popup.keepalive.StashKeepAliveService");
            // shortService type is required for Android 14+ (ignored on older versions)
            serviceElement.SetAttribute("foregroundServiceType", ANDROID_NS, "shortService");
            serviceElement.SetAttribute("exported", ANDROID_NS, "false");
            
            applicationNode.AppendChild(serviceElement);
            
            Debug.Log("StashPopup: Added StashKeepAliveService to manifest");
            return true;
        }
    }
}

