using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Stash.Samples
{
    /// <summary>
    /// Handles tab switching functionality in the Stash Store UI.
    /// Manages tab buttons, content panels, and help descriptions for a clean navigation experience.
    /// 
    /// Features:
    /// - Tab button management
    /// - Content panel switching
    /// - Help description system
    /// - Initialization with delay handling
    /// </summary>
    public class TabController : MonoBehaviour
    {
        #region Inspector Fields
        [SerializeField] private UIDocument uiDocument;
        #endregion

        #region Private Fields
        // Tab buttons
        private Button userTabButton;
        private Button storeTabButton;
        private Button webshopTabButton;
        
        // Tab content panels
        private VisualElement userTabContent;
        private VisualElement storeTabContent;
        private VisualElement webshopTabContent;
        
        // Help system
        private VisualElement helpDescriptionDialog;
        private Button helpDescriptionCloseButton;
        private Label helpDescriptionText;
        
        private bool initialized = false;
        #endregion

        #region Tab Configuration
        /// <summary>
        /// Descriptions for each tab to show in help dialogs
        /// </summary>
        private readonly Dictionary<string, string> tabDescriptions = new Dictionary<string, string>
        {
            { "user", "Manage your account settings and view your profile information. You can log in or create a new account here." },
            { "store", "Browse and purchase items using Stash Pay. This is our seamless alternative to in-app purchases." },
            { "webshop", "Open the Stash Webshop directly from the game. All purchases are synchronized with your game account." }
        };
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            // TabController initializing
        }
        
        private void Start()
        {
            // TabController starting
            
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
                // Found UIDocument component
            }
            
            if (uiDocument == null)
            {
                Debug.LogError("[TabController] No UIDocument found");
                return;
            }
            
            // Wait a short time to ensure the UI is fully loaded
            StartCoroutine(InitializeWithDelay(0.1f));
        }
        #endregion

        #region Initialization
        /// <summary>
        /// Initializes the tab controller after a short delay
        /// </summary>
        private IEnumerator InitializeWithDelay(float delay)
        {
            // Delaying initialization
            yield return new WaitForSeconds(delay);
            
            InitializeTabController();
        }
        
        /// <summary>
        /// Manually initialize the tab controller
        /// </summary>
        /// <returns>True if initialization was successful</returns>
        public bool InitializeTabController()
        {
            if (initialized)
            {
                // Already initialized
                return true;
            }
            
            // Initialize method called
            
            if (uiDocument == null)
            {
                Debug.LogError("[TabController] UIDocument is null, cannot initialize");
                return false;
            }
            
            var root = uiDocument.rootVisualElement;
            if (root == null)
            {
                Debug.LogError("[TabController] Root visual element is null");
                return false;
            }
            
            if (!FindUIElements(root))
            {
                Debug.LogError("[TabController] Failed to find required UI elements");
                return false;
            }
            
            SetupEventHandlers();
            
            // Set initial state
            SelectTab("store");
            
            initialized = true;
            // Initialized successfully
            return true;
        }

        /// <summary>
        /// Finds and caches all required UI elements
        /// </summary>
        private bool FindUIElements(VisualElement root)
        {
            // Get tab buttons
            userTabButton = root.Q<Button>("user-tab-button");
            storeTabButton = root.Q<Button>("store-tab-button");
            webshopTabButton = root.Q<Button>("webshop-tab-button");
            
            // Get tab content panels
            userTabContent = root.Q<VisualElement>("user-tab-content");
            storeTabContent = root.Q<VisualElement>("store-tab-content");
            webshopTabContent = root.Q<VisualElement>("webshop-tab-content");

            // Get help description dialog elements
            helpDescriptionDialog = root.Q<VisualElement>("help-description-dialog");
            helpDescriptionCloseButton = root.Q<Button>("help-description-close-button");
            helpDescriptionText = root.Q<Label>("help-description-text");
            
            // Found tab elements successfully
            
            // Validate that all required elements were found
            return userTabButton != null && storeTabButton != null && webshopTabButton != null &&
                   userTabContent != null && storeTabContent != null && webshopTabContent != null &&
                   helpDescriptionDialog != null && helpDescriptionCloseButton != null && helpDescriptionText != null;
        }

        /// <summary>
        /// Sets up event handlers for all interactive elements
        /// </summary>
        private void SetupEventHandlers()
        {
            // Set up tab button handlers
            userTabButton.clicked += () => SelectTab("user");
            storeTabButton.clicked += () => SelectTab("store");
            webshopTabButton.clicked += () => SelectTab("webshop");

            // Set up help description close button
            if (helpDescriptionCloseButton != null)
                helpDescriptionCloseButton.clicked += HideHelpDescription;
        }
        #endregion

        #region Public API
        /// <summary>
        /// Switches to the specified tab
        /// </summary>
        /// <param name="tabName">The name of the tab to select (user, store, or webshop)</param>
        public void SelectTab(string tabName)
        {
            // Selecting tab
            
            if (!initialized)
            {
                Debug.LogWarning("[TabController] Not initialized yet, trying to initialize now");
                if (!InitializeTabController())
                {
                    Debug.LogError("[TabController] Failed to initialize on demand");
                    return;
                }
            }
            
            if (!ValidateTabElements())
            {
                Debug.LogWarning("[TabController] Tab elements are null, can't switch tabs");
                return;
            }
            
            // Switching to selected tab
            
            // Update tab button styles
            ClearTabSelections();
            
            // Hide all tab content
            HideAllTabContent();
            
            // Show the selected tab
            ShowSelectedTab(tabName.ToLower());

            // Show help description for the selected tab
            ShowHelpDescription(tabName);
            
            // Force visual refresh
            RefreshTabDisplay();
            
            // Tab switch complete
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Validates that all tab elements are available
        /// </summary>
        private bool ValidateTabElements()
        {
            return userTabButton != null && storeTabButton != null && webshopTabButton != null &&
                   userTabContent != null && storeTabContent != null && webshopTabContent != null;
        }

        /// <summary>
        /// Clears the selection state from all tab buttons
        /// </summary>
        private void ClearTabSelections()
        {
            userTabButton.RemoveFromClassList("tab-selected");
            storeTabButton.RemoveFromClassList("tab-selected");
            webshopTabButton.RemoveFromClassList("tab-selected");
        }

        /// <summary>
        /// Hides all tab content panels
        /// </summary>
        private void HideAllTabContent()
        {
            userTabContent.style.display = DisplayStyle.None;
            storeTabContent.style.display = DisplayStyle.None;
            webshopTabContent.style.display = DisplayStyle.None;
        }

        /// <summary>
        /// Shows the content for the specified tab
        /// </summary>
        private void ShowSelectedTab(string tabName)
        {
            switch (tabName)
            {
                case "user":
                    userTabButton.AddToClassList("tab-selected");
                    userTabContent.style.display = DisplayStyle.Flex;
                    break;
                    
                case "store":
                    storeTabButton.AddToClassList("tab-selected");
                    storeTabContent.style.display = DisplayStyle.Flex;
                    break;
                    
                case "webshop":
                    webshopTabButton.AddToClassList("tab-selected");
                    webshopTabContent.style.display = DisplayStyle.Flex;
                    break;
                    
                default:
                    Debug.LogWarning($"[TabController] Unknown tab name: {tabName}");
                    break;
            }
        }

        /// <summary>
        /// Forces a visual refresh of all tab elements
        /// </summary>
        private void RefreshTabDisplay()
        {
            userTabButton.MarkDirtyRepaint();
            storeTabButton.MarkDirtyRepaint();
            webshopTabButton.MarkDirtyRepaint();
        }
        #endregion

        #region Help System
        /// <summary>
        /// Shows the help description for the specified tab
        /// </summary>
        private void ShowHelpDescription(string tabName)
        {
            if (helpDescriptionDialog != null && helpDescriptionText != null)
            {
                if (tabDescriptions.TryGetValue(tabName.ToLower(), out string description))
                {
                    helpDescriptionText.text = description;
                    helpDescriptionDialog.AddToClassList("visible");
                }
            }
        }

        /// <summary>
        /// Hides the help description dialog
        /// </summary>
        private void HideHelpDescription()
        {
            helpDescriptionDialog?.RemoveFromClassList("visible");
        }
        #endregion
    }
} 