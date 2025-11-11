using System.IO;
using System.Text;
using System.Xml;
using UnityEditor;
using UnityEditor.Android;
using UnityEngine;

namespace StashPopup.Editor
{
    /// <summary>
    /// Post-processes Android builds to inject StashPayCardPortraitActivity into the Android manifest
    /// </summary>
    public class StashPopupAndroidPostProcess : IPostGenerateGradleAndroidProject
    {
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
                nsMgr.AddNamespace("android", "http://schemas.android.com/apk/res/android");
                
                XmlNode applicationNode = manifest.SelectSingleNode("/manifest/application");
                
                if (applicationNode == null)
                {
                    Debug.LogError("StashPopup: Could not find <application> node in manifest");
                    return;
                }
                
                // Check if Activity already exists
                XmlNode existingActivity = applicationNode.SelectSingleNode(
                    "activity[@android:name='com.stash.popup.StashPayCardPortraitActivity']", nsMgr);
                
                if (existingActivity != null)
                {
                    Debug.Log("StashPopup: StashPayCardPortraitActivity already exists in manifest");
                    return;
                }
                
                // Create the Activity element
                XmlElement activityElement = manifest.CreateElement("activity");
                activityElement.SetAttribute("name", "http://schemas.android.com/apk/res/android", 
                    "com.stash.popup.StashPayCardPortraitActivity");
                activityElement.SetAttribute("screenOrientation", "http://schemas.android.com/apk/res/android", 
                    "portrait");
                activityElement.SetAttribute("theme", "http://schemas.android.com/apk/res/android", 
                    "@android:style/Theme.Translucent.NoTitleBar.Fullscreen");
                activityElement.SetAttribute("launchMode", "http://schemas.android.com/apk/res/android", 
                    "singleTop");
                activityElement.SetAttribute("taskAffinity", "http://schemas.android.com/apk/res/android", 
                    "");
                activityElement.SetAttribute("excludeFromRecents", "http://schemas.android.com/apk/res/android", 
                    "true");
                activityElement.SetAttribute("configChanges", "http://schemas.android.com/apk/res/android", 
                    "orientation|screenSize|keyboardHidden");
                activityElement.SetAttribute("exported", "http://schemas.android.com/apk/res/android", 
                    "false");
                activityElement.SetAttribute("hardwareAccelerated", "http://schemas.android.com/apk/res/android", 
                    "true");
                
                activityElement.SetAttribute("noHistory", "http://schemas.android.com/apk/res/android", "true");
                
                applicationNode.AppendChild(activityElement);
                
                // Save the modified manifest
                using (XmlTextWriter writer = new XmlTextWriter(manifestPath, Encoding.UTF8))
                {
                    writer.Formatting = Formatting.Indented;
                    manifest.Save(writer);
                }
                
                Debug.Log("StashPopup: Successfully injected StashPayCardPortraitActivity into manifest");
            }
            catch (System.Exception e)
            {
                Debug.LogError("StashPopup: Error modifying AndroidManifest.xml: " + e.Message);
            }
        }
    }
}

