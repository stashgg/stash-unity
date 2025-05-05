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
        [SerializeField] private bool optimizeForMobile = true;
        
        // Added new configuration options
        [SerializeField] private float mobileScale = 1.0f;
        [SerializeField] private float matchValue = 0.5f; // 0=width, 1=height, 0.5=balanced

        private void Awake()
        {
            LoadUIDocument();
        }

        private void Start()
        {
            // Additional check in Start to ensure panel settings are applied
            if (uiDocument != null && uiDocument.rootVisualElement != null)
            {
                ApplyResponsiveLayout();
            }
        }

        private void OnEnable()
        {
            if (uiDocument != null && uiDocument.visualTreeAsset == null)
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
                CreatePanelSettings();
            }
            else
            {
                // Update existing panel settings
                ConfigurePanelSettings(uiDocument.panelSettings);
            }
        }
        
        private void CreatePanelSettings()
        {
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.name = "RuntimePanelSettings";
            
            ConfigurePanelSettings(panelSettings);
            
            // Assign to the UI Document
            uiDocument.panelSettings = panelSettings;
            
            Debug.Log("New panel settings created and assigned.");
        }
        
        private void ConfigurePanelSettings(PanelSettings panelSettings)
        {
            // Configure the panel settings for proper scaling
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            
            // Detect if we're running on a mobile device
            bool isMobile = Application.isMobilePlatform || optimizeForMobile;
            
            // Adjust scaling based on screen size and orientation
            if (isMobile)
            {
                // For mobile, use a resolution that works well in both orientations
                // Use smaller reference resolution for better scaling on phones
                panelSettings.referenceResolution = new Vector2Int(360, 640);
                panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                panelSettings.match = matchValue; // Balance width/height (0.5 is balanced)
                panelSettings.scale = mobileScale; // Adjust UI scale specifically for mobile
                
                // Higher DPI for mobile
                panelSettings.referenceDpi = 160;
                panelSettings.fallbackDpi = 160;
            }
            else
            {
                // Desktop resolution
                panelSettings.referenceResolution = new Vector2Int(1200, 800);
                panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                panelSettings.match = 0.5f; // Balance width and height matching
                
                // Standard DPI for desktop
                panelSettings.referenceDpi = 96;
                panelSettings.fallbackDpi = 96;
            }
            
            Debug.Log($"Panel settings configured for {(isMobile ? "mobile" : "desktop")} with match={panelSettings.match}, " +
                $"resolution={panelSettings.referenceResolution}, scale={panelSettings.scale}");
        }
        
        private void ApplyResponsiveLayout()
        {
            var root = uiDocument.rootVisualElement;
            if (root == null) return;
            
            // Get current screen dimensions
            float screenWidth = Screen.width;
            float screenHeight = Screen.height;
            bool isNarrowScreen = screenWidth < 600;
            
            // Apply responsive CSS class based on screen width
            if (isNarrowScreen)
            {
                root.AddToClassList("narrow-screen");
                root.RemoveFromClassList("wide-screen");
                Debug.Log("Applied narrow-screen layout");
            }
            else
            {
                root.AddToClassList("wide-screen");
                root.RemoveFromClassList("narrow-screen");
                Debug.Log("Applied wide-screen layout");
            }
            
            // Apply orientation class
            bool isPortrait = screenHeight > screenWidth;
            if (isPortrait)
            {
                root.AddToClassList("portrait");
                root.RemoveFromClassList("landscape");
            }
            else
            {
                root.AddToClassList("landscape");
                root.RemoveFromClassList("portrait");
            }
            
            // Log screen dimensions for debugging
            Debug.Log($"Screen dimensions: {screenWidth}x{screenHeight}, isNarrowScreen: {isNarrowScreen}, isPortrait: {isPortrait}");
        }
    }
} 