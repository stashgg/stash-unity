#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Stash.Sample.Editor
{
    public static class StashCheckoutSetupEditor
    {
        [MenuItem("Stash/Setup Checkout UI")]
        public static void SetupCheckoutUI()
        {
            // Create a UI Document GameObject if it doesn't exist
            GameObject uiDocumentObj = GameObject.Find("StashCheckoutUI");
            
            if (uiDocumentObj == null)
            {
                uiDocumentObj = new GameObject("StashCheckoutUI");
                uiDocumentObj.AddComponent<UIDocument>();
            }
            
            // Add our helper component which will set up everything else
            var helper = uiDocumentObj.AddComponent<StashCheckoutHelper>();
            
            // Select the GameObject in the hierarchy
            Selection.activeGameObject = uiDocumentObj;
            
            Debug.Log("Stash Checkout UI setup complete. The UI will load at runtime from the Resources folder.");
        }
    }
}
#endif 