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
    private Button settingsTabButton;
    private VisualElement userTabContent;
    private VisualElement storeTabContent;
    private VisualElement settingsTabContent;
    private bool initialized = false;
    
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
        settingsTabButton = root.Q<Button>("settings-tab-button");
        
        // Get tab content panels
        userTabContent = root.Q<VisualElement>("user-tab-content");
        storeTabContent = root.Q<VisualElement>("store-tab-content");
        settingsTabContent = root.Q<VisualElement>("settings-tab-content");
        
        // Log what we found
        Debug.Log($"TabController: Found elements - " +
                 $"User Tab Button: {(userTabButton != null ? "Yes" : "No")}, " +
                 $"IAP Tab Button: {(storeTabButton != null ? "Yes" : "No")}, " +
                 $"Settings Tab Button: {(settingsTabButton != null ? "Yes" : "No")}, " +
                 $"User Tab Content: {(userTabContent != null ? "Yes" : "No")}, " +
                 $"IAP Tab Content: {(storeTabContent != null ? "Yes" : "No")}, " +
                 $"Settings Tab Content: {(settingsTabContent != null ? "Yes" : "No")}");
        
        if (userTabButton == null || storeTabButton == null || settingsTabButton == null ||
            userTabContent == null || storeTabContent == null || settingsTabContent == null)
        {
            Debug.LogError("Tab UI elements not found!");
            return false;
        }
        
        // Set up event handlers
        userTabButton.clicked += () => {
            Debug.Log("TabController: User tab button clicked");
            SelectTab("user");
        };
        
        storeTabButton.clicked += () => {
            Debug.Log("TabController: IAP tab button clicked");
            SelectTab("store");
        };
        
        settingsTabButton.clicked += () => {
            Debug.Log("TabController: Settings tab button clicked");
            SelectTab("settings");
        };
        
        // Set initial state
        SelectTab("store");
        
        initialized = true;
        Debug.Log("TabController: Initialized successfully");
        return true;
    }
    
    /// <summary>
    /// Switches to the specified tab
    /// </summary>
    /// <param name="tabName">The name of the tab to select (user or store)</param>
    public void SelectTab(string tabName)
    {
        if (!initialized)
        {
            Debug.LogWarning("TabController: Not initialized yet, trying to initialize now");
            if (!InitializeTabController())
            {
                Debug.LogError("TabController: Failed to initialize on demand");
                return;
            }
        }
        
        if (userTabButton == null || storeTabButton == null || settingsTabButton == null ||
            userTabContent == null || storeTabContent == null || settingsTabContent == null)
        {
            Debug.LogWarning("TabController: Tab elements are null, can't switch tabs");
            return;
        }
        
        Debug.Log($"TabController: Switching to tab: {tabName}");
        
        // Update tab button styles
        userTabButton.RemoveFromClassList("tab-selected");
        storeTabButton.RemoveFromClassList("tab-selected");
        settingsTabButton.RemoveFromClassList("tab-selected");
        
        // Hide all tab content
        userTabContent.style.display = DisplayStyle.None;
        storeTabContent.style.display = DisplayStyle.None;
        settingsTabContent.style.display = DisplayStyle.None;
        
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
                
            case "settings":
                settingsTabButton.AddToClassList("tab-selected");
                settingsTabContent.style.display = DisplayStyle.Flex;
                Debug.Log("TabController: Settings tab selected and activated");
                break;
                
            default:
                Debug.LogWarning($"Unknown tab name: {tabName}");
                break;
        }
        
        // Force visual refresh
        userTabButton.MarkDirtyRepaint();
        storeTabButton.MarkDirtyRepaint();
        settingsTabButton.MarkDirtyRepaint();
    }
} 