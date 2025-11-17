using System.IO;
using System.Text;
using System.Xml;
using UnityEditor;
using UnityEditor.Android;
using UnityEngine;

/// <summary>
/// Post-processes Android builds to set versionCode in AndroidManifest.xml
/// Reads from ProjectSettings.asset to ensure correct Unix timestamp build number
/// </summary>
public class SetAndroidVersionCodePostProcess : IPostGenerateGradleAndroidProject
{
    public int callbackOrder => 1; // Run early, before other post-process scripts
    
    public void OnPostGenerateGradleAndroidProject(string path)
    {
        Debug.Log("SetAndroidVersionCode: Post-processing Android project at: " + path);
        
        string manifestPath = Path.Combine(path, "src/main/AndroidManifest.xml");
        
        if (!File.Exists(manifestPath))
        {
            Debug.LogWarning("SetAndroidVersionCode: AndroidManifest.xml not found at: " + manifestPath);
            return;
        }
        
        try
        {
            XmlDocument manifest = new XmlDocument();
            manifest.Load(manifestPath);
            
            XmlNamespaceManager nsMgr = new XmlNamespaceManager(manifest.NameTable);
            nsMgr.AddNamespace("android", "http://schemas.android.com/apk/res/android");
            
            // Set versionCode - read directly from ProjectSettings.asset to ensure we get the correct value
            XmlNode manifestNode = manifest.SelectSingleNode("/manifest");
            if (manifestNode != null)
            {
                int versionCode = PlayerSettings.Android.bundleVersionCode;
                
                // Try to read directly from ProjectSettings.asset as fallback (in case Unity didn't pick it up)
                string projectSettingsPath = Path.Combine(Application.dataPath, "..", "ProjectSettings", "ProjectSettings.asset");
                if (File.Exists(projectSettingsPath))
                {
                    string projectSettingsContent = File.ReadAllText(projectSettingsPath);
                    System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(@"AndroidBundleVersionCode:\s*(\d+)");
                    System.Text.RegularExpressions.Match match = regex.Match(projectSettingsContent);
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int fileVersionCode))
                    {
                        if (fileVersionCode != versionCode)
                        {
                            Debug.LogWarning($"SetAndroidVersionCode: ProjectSettings.asset has versionCode {fileVersionCode} but PlayerSettings has {versionCode}. Using file value.");
                            versionCode = fileVersionCode;
                        }
                    }
                }
                
                // Find or create the android:versionCode attribute
                XmlAttribute versionCodeAttr = manifestNode.Attributes.GetNamedItem("versionCode", "http://schemas.android.com/apk/res/android") as XmlAttribute;
                if (versionCodeAttr == null)
                {
                    versionCodeAttr = manifest.CreateAttribute("android", "versionCode", "http://schemas.android.com/apk/res/android");
                    manifestNode.Attributes.Append(versionCodeAttr);
                }
                
                int oldVersionCode = versionCodeAttr.Value != null && int.TryParse(versionCodeAttr.Value, out int oldVal) ? oldVal : 0;
                versionCodeAttr.Value = versionCode.ToString();
                
                // Save the modified manifest
                using (XmlTextWriter writer = new XmlTextWriter(manifestPath, Encoding.UTF8))
                {
                    writer.Formatting = Formatting.Indented;
                    manifest.Save(writer);
                }
                
                Debug.Log($"SetAndroidVersionCode: Set AndroidManifest.xml versionCode from {oldVersionCode} to {versionCode} (Unix timestamp)");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("SetAndroidVersionCode: Error modifying AndroidManifest.xml: " + e.Message);
        }
    }
}

