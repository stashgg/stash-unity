using UnityEngine;
using UnityEngine.UIElements;
using System.IO;

namespace Stash.Sample
{
    public class StashCheckoutUXMLLoader : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private string uxmlResourcePath = "StashCheckoutUI";
        [SerializeField] private string ussResourcePath = "StashCheckoutUI";

        private void Awake()
        {
            LoadUIDocument();
        }

        private void OnEnable()
        {
            if (uiDocument.visualTreeAsset == null)
            {
                LoadUIDocument();
            }
        }

        private void LoadUIDocument()
        {
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
                if (uiDocument == null)
                {
                    Debug.LogError("UIDocument component not found!");
                    return;
                }
            }

            // Make sure we have a valid path
            if (string.IsNullOrEmpty(uxmlResourcePath))
            {
                Debug.LogError("UXML resource path is empty!");
                return;
            }

            // Load the UXML asset
            var uxmlAsset = Resources.Load<VisualTreeAsset>(uxmlResourcePath);
            if (uxmlAsset == null)
            {
                Debug.LogError($"Failed to load UXML asset from Resources: {uxmlResourcePath}. Make sure it's in a Resources folder!");
                return;
            }

            // Assign the visual tree asset to the UI document
            uiDocument.visualTreeAsset = uxmlAsset;
            
            Debug.Log("UXML asset loaded successfully!");
            
            // Create panel settings if needed
            if (uiDocument.panelSettings == null)
            {
                var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                panelSettings.name = "RuntimePanelSettings";
                
                // Configure the panel settings
                panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panelSettings.referenceResolution = new Vector2Int(1200, 800);
                panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                
                // Assign to the UI Document
                uiDocument.panelSettings = panelSettings;
                
                Debug.Log("Panel settings created and assigned.");
            }
        }
    }
} 