using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Handles tab switching functionality in the UI
/// </summary>
public class TabController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    
    private Button userTabButton;
    private Button storeTabButton;
    private Button webshopTabButton;
    private VisualElement userTabContent;
    private VisualElement storeTabContent;
    private VisualElement webshopTabContent;
    private VisualElement helpDescriptionDialog;
    private Button helpDescriptionCloseButton;
    private Label helpDescriptionText;
    private bool initialized = false;

    private readonly Dictionary<string, string> tabDescriptions = new Dictionary<string, string>
    {
        { "user", "Manage your account settings and view your profile information. You can log in or create a new account here." },
        { "store", "Browse and purchase items using Stash Pay. This is our seamless alternative to in-app purchases." },
        { "webshop", "Open the Stash Webshop directly from the game. All purchases are synchronized with your game account." }
    };
    
    private void Awake()
    {
        Debug.Log("TabController: Awake called");
    }
    
    private void Start()
    {
        Debug.Log("TabController: Start method called");
        
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
            Debug.Log("TabController: Found UIDocument on same GameObject");
        }
        
        if (uiDocument == null)
        {
            Debug.LogError("No UIDocument found for TabController");
            return;
        }
        
        // Wait a short time to ensure the UI is fully loaded
        StartCoroutine(InitializeWithDelay(0.1f));
    }
    
    private IEnumerator InitializeWithDelay(float delay)
    {
        Debug.Log($"TabController: Waiting {delay}s before initialization");
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
            Debug.Log("TabController: Already initialized");
            return true;
        }
        
        Debug.Log("TabController: Initialize method called");
        
        if (uiDocument == null)
        {
            Debug.LogError("TabController: UIDocument is null, cannot initialize");
            return false;
        }
        
        var root = uiDocument.rootVisualElement;
        if (root == null)
        {
            Debug.LogError("TabController: Root visual element is null");
            return false;
        }
        
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
        
        // Log what we found
        Debug.Log($"TabController: Found elements - " +
                 $"User Tab Button: {(userTabButton != null ? "Yes" : "No")}, " +
                 $"Store Tab Button: {(storeTabButton != null ? "Yes" : "No")}, " +
                 $"Webshop Tab Button: {(webshopTabButton != null ? "Yes" : "No")}, " +
                 $"User Tab Content: {(userTabContent != null ? "Yes" : "No")}, " +
                 $"Store Tab Content: {(storeTabContent != null ? "Yes" : "No")}, " +
                 $"Webshop Tab Content: {(webshopTabContent != null ? "Yes" : "No")}, " +
                 $"Help Description Dialog: {(helpDescriptionDialog != null ? "Yes" : "No")}");
        
        if (userTabButton == null || storeTabButton == null || webshopTabButton == null ||
            userTabContent == null || storeTabContent == null || webshopTabContent == null ||
            helpDescriptionDialog == null || helpDescriptionCloseButton == null || helpDescriptionText == null)
        {
            Debug.LogError("Tab UI elements not found!");
            return false;
        }
        
        // Set up event handlers using RegisterCallback
        userTabButton.RegisterCallback<ClickEvent>(evt => {
            Debug.Log("TabController: User tab button clicked via RegisterCallback");
            SelectTab("user");
        });
        
        storeTabButton.RegisterCallback<ClickEvent>(evt => {
            Debug.Log("TabController: Store tab button clicked via RegisterCallback");
            SelectTab("store");
        });
        
        webshopTabButton.RegisterCallback<ClickEvent>(evt => {
            Debug.Log("TabController: Webshop tab button clicked via RegisterCallback");
            SelectTab("webshop");
        });

        // Set up help description close button
        helpDescriptionCloseButton.RegisterCallback<ClickEvent>(evt => {
            Debug.Log("TabController: Help description close button clicked");
            HideHelpDescription();
        });
        
        // Also add the clicked event as a backup
        userTabButton.clicked += () => {
            Debug.Log("TabController: User tab button clicked via clicked event");
            SelectTab("user");
        };
        
        storeTabButton.clicked += () => {
            Debug.Log("TabController: Store tab button clicked via clicked event");
            SelectTab("store");
        };
        
        webshopTabButton.clicked += () => {
            Debug.Log("TabController: Webshop tab button clicked via clicked event");
            SelectTab("webshop");
        };
        
        // Set initial state
        SelectTab("store");
        
        initialized = true;
        Debug.Log("TabController: Initialized successfully");
        return true;
    }

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

    private void HideHelpDescription()
    {
        if (helpDescriptionDialog != null)
        {
            helpDescriptionDialog.RemoveFromClassList("visible");
        }
    }
    
    /// <summary>
    /// Switches to the specified tab
    /// </summary>
    /// <param name="tabName">The name of the tab to select (user, store, or webshop)</param>
    public void SelectTab(string tabName)
    {
        Debug.Log($"TabController: SelectTab called with tabName={tabName}");
        
        if (!initialized)
        {
            Debug.LogWarning("TabController: Not initialized yet, trying to initialize now");
            if (!InitializeTabController())
            {
                Debug.LogError("TabController: Failed to initialize on demand");
                return;
            }
        }
        
        if (userTabButton == null || storeTabButton == null || webshopTabButton == null ||
            userTabContent == null || storeTabContent == null || webshopTabContent == null)
        {
            Debug.LogWarning("TabController: Tab elements are null, can't switch tabs");
            return;
        }
        
        Debug.Log($"TabController: Switching to tab: {tabName}");
        
        // Update tab button styles
        userTabButton.RemoveFromClassList("tab-selected");
        storeTabButton.RemoveFromClassList("tab-selected");
        webshopTabButton.RemoveFromClassList("tab-selected");
        
        // Hide all tab content
        userTabContent.style.display = DisplayStyle.None;
        storeTabContent.style.display = DisplayStyle.None;
        webshopTabContent.style.display = DisplayStyle.None;
        
        // Show the selected tab
        switch (tabName.ToLower())
        {
            case "user":
                userTabButton.AddToClassList("tab-selected");
                userTabContent.style.display = DisplayStyle.Flex;
                Debug.Log("TabController: User tab selected and activated");
                break;
                
            case "store":
                storeTabButton.AddToClassList("tab-selected");
                storeTabContent.style.display = DisplayStyle.Flex;
                Debug.Log("TabController: Store tab selected and activated");
                break;
                
            case "webshop":
                webshopTabButton.AddToClassList("tab-selected");
                webshopTabContent.style.display = DisplayStyle.Flex;
                Debug.Log("TabController: Webshop tab selected and activated");
                break;
                
            default:
                Debug.LogWarning($"Unknown tab name: {tabName}");
                break;
        }

        // Show help description for the selected tab
        ShowHelpDescription(tabName);
        
        // Force visual refresh
        userTabButton.MarkDirtyRepaint();
        storeTabButton.MarkDirtyRepaint();
        webshopTabButton.MarkDirtyRepaint();
        
        // Additional debug logging
        Debug.Log($"TabController: After switching - " +
                 $"User Tab Display: {userTabContent.style.display}, " +
                 $"Store Tab Display: {storeTabContent.style.display}, " +
                 $"Webshop Tab Display: {webshopTabContent.style.display}");
    }
} 