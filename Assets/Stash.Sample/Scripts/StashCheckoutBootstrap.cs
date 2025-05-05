using UnityEngine;
using UnityEngine.UIElements;

namespace Stash.Sample
{
    public class StashCheckoutBootstrap : MonoBehaviour
    {
        [SerializeField] private TextAsset uxmlAsset;
        [SerializeField] private TextAsset ussAsset;
        
        private void Start()
        {
            SetupScene();
        }
        
        private void SetupScene()
        {
            // Create camera
            var cameraObj = new GameObject("Main Camera");
            cameraObj.tag = "MainCamera";
            var camera = cameraObj.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.1f, 0.1f, 0.1f);
            cameraObj.AddComponent<AudioListener>();
            
            // Create light
            var lightObj = new GameObject("Directional Light");
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            lightObj.transform.rotation = Quaternion.Euler(50, -30, 0);
            
            // Create UI
            var uiObj = new GameObject("UI");
            var uiDocument = uiObj.AddComponent<UIDocument>();
            
            // Add our controller
            var controller = uiObj.AddComponent<StashCheckoutController>();
            controller.uiDocument = uiDocument;
            
            // Add our setup script
            var setup = uiObj.AddComponent<StashCheckoutSetup>();
            setup.uiDocument = uiDocument;
            
            Debug.Log("StashCheckout scene created programmatically. Please open the Stash.Sample/UI/StashCheckoutUI.uxml in the UI Builder to customize the interface.");
        }
    }
} 