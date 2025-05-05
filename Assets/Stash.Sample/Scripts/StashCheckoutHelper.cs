using UnityEngine;
using UnityEngine.UIElements;

namespace Stash.Sample
{
    /// <summary>
    /// Helper class to set up Stash Checkout UI in a scene.
    /// Attach this to a GameObject with a UIDocument component.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class StashCheckoutHelper : MonoBehaviour
    {
        private void Awake()
        {
            var uiDocument = GetComponent<UIDocument>();
            
            // Add the UXML loader if it doesn't exist
            if (GetComponent<StashCheckoutUXMLLoader>() == null)
            {
                var loader = gameObject.AddComponent<StashCheckoutUXMLLoader>();
                loader.enabled = true;
            }
            
            // Add the controller if it doesn't exist
            if (GetComponent<StashCheckoutController>() == null)
            {
                var controller = gameObject.AddComponent<StashCheckoutController>();
                controller.uiDocument = uiDocument;
                controller.enabled = true;
            }
        }
    }
} 