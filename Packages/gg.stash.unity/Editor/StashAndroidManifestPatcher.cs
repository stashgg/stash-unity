#if UNITY_ANDROID
using System;
using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

namespace Stash.Editor
{
    public class StashAndroidManifestPatcher : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 9999;

        private const string AndroidNs = "http://schemas.android.com/apk/res/android";
        private const string DefaultActivity = "com.unity3d.player.UnityPlayerActivity";
        private const string TargetActivity = "com.stash.popup.StashNativeUnityActivity";

        public void OnPostGenerateGradleAndroidProject(string gradleProjectPath)
        {
#if STASH_DISABLE_ACTIVITY_PATCH
            return;
#else
            try
            {
                var manifestPath = FindManifestPath(gradleProjectPath);
                if (manifestPath == null)
                {
                    Debug.LogWarning("StashNative: Could not locate AndroidManifest.xml under " + gradleProjectPath + "; skipping auto-patch. OnBrowserClosed may not fire for Chrome Custom Tabs.");
                    return;
                }

                var xml = new XmlDocument { PreserveWhitespace = true };
                xml.Load(manifestPath);

                var activities = xml.GetElementsByTagName("activity");
                XmlElement defaultActivity = null;
                XmlElement customLauncherActivity = null;
                bool alreadyPatched = false;

                foreach (XmlElement act in activities)
                {
                    var name = act.GetAttribute("name", AndroidNs);
                    if (name == TargetActivity) { alreadyPatched = true; break; }
                    if (!HasLauncherIntent(act)) continue;
                    if (name == DefaultActivity) defaultActivity = act;
                    else customLauncherActivity = act;
                }

                if (alreadyPatched) return;

                if (defaultActivity != null)
                {
                    defaultActivity.SetAttribute("name", AndroidNs, TargetActivity);
                    xml.Save(manifestPath);
                    Debug.Log("StashNative: Patched Android launcher activity to " + TargetActivity + " so OnBrowserClosed fires for Chrome Custom Tabs.");
                    return;
                }

                if (customLauncherActivity != null)
                {
                    var customName = customLauncherActivity.GetAttribute("name", AndroidNs);
                    Debug.LogWarning("StashNative: Detected custom Android launcher activity '" + customName + "'. To receive OnBrowserClosed for Chrome Custom Tabs, extend com.stash.popup.StashNativeUnityActivity (instead of UnityPlayerActivity), or define STASH_DISABLE_ACTIVITY_PATCH and forward onActivityResult manually.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError("StashNative: Failed to patch AndroidManifest.xml: " + e.Message);
            }
#endif
        }

        private static string FindManifestPath(string gradleProjectPath)
        {
            string[] candidates =
            {
                Path.Combine(gradleProjectPath, "launcher", "src", "main", "AndroidManifest.xml"),
                Path.Combine(gradleProjectPath, "src", "main", "AndroidManifest.xml"),
                Path.Combine(gradleProjectPath, "AndroidManifest.xml"),
            };
            foreach (var path in candidates)
                if (File.Exists(path)) return path;
            return null;
        }

        private static bool HasLauncherIntent(XmlElement activity)
        {
            foreach (XmlNode child in activity.ChildNodes)
            {
                if (child.NodeType != XmlNodeType.Element || child.LocalName != "intent-filter") continue;
                bool hasMain = false, hasLauncher = false;
                foreach (XmlNode filterChild in child.ChildNodes)
                {
                    if (filterChild.NodeType != XmlNodeType.Element) continue;
                    var element = (XmlElement)filterChild;
                    var name = element.GetAttribute("name", AndroidNs);
                    if (element.LocalName == "action" && name == "android.intent.action.MAIN") hasMain = true;
                    else if (element.LocalName == "category" && name == "android.intent.category.LAUNCHER") hasLauncher = true;
                }
                if (hasMain && hasLauncher) return true;
            }
            return false;
        }
    }
}
#endif
