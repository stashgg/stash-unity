using System.IO;
using System.Text;
using System.Xml;
using UnityEditor;
using UnityEditor.Android;
using UnityEngine;

namespace StashPopup.Editor
{
    /// <summary>
    /// Post-processes Android builds to add permissions that the Stash Pay AAR may need
    /// (e.g. for Chrome Custom Tabs keep-alive). The AAR (stash-native 1.2.4+) declares
    /// its own Activity and components via manifest merge.
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
                
                // Permissions that the Stash Pay AAR may need (e.g. Chrome Custom Tabs keep-alive)
                modified |= AddPermissionIfMissing(manifest, manifestNode, nsMgr, "android.permission.FOREGROUND_SERVICE");
                modified |= AddPermissionIfMissing(manifest, manifestNode, nsMgr, "android.permission.FOREGROUND_SERVICE_SHORT_SERVICE");
                modified |= AddPermissionIfMissing(manifest, manifestNode, nsMgr, "android.permission.POST_NOTIFICATIONS");
                
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
        
    }
}

