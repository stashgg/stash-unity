using UnityEngine;
using UnityEngine.UIElements;

namespace Stash.Sample
{
    [ExecuteInEditMode]
    public class StashCheckoutSetup : MonoBehaviour
    {
        public UIDocument uiDocument;
        
        [SerializeField] private Vector2Int referenceResolution = new Vector2Int(1200, 800);
        
        private void Awake()
        {
            Setup();
        }
        
        private void OnEnable()
        {
            Setup();
        }
        
        private void Setup()
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
            
            // Check if the panel settings is already assigned
            if (uiDocument.panelSettings != null)
                return;
            
            // Create panel settings
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.name = "RuntimePanelSettings";
            
            // Configure the panel settings
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = referenceResolution;
            panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            
            // Assign to the UI Document
            uiDocument.panelSettings = panelSettings;
        }
    }
} 