using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using System.Text;
using System.Globalization;
using System.Linq;

public class StoreUIAuthController : MonoBehaviour
{
    [SerializeField] private UIDocument storeUIDocument;
    [SerializeField] private TabController tabController; // Reference to TabController
    
    private Button loginButton;
    private Button refreshTokenButton;
    
    // User profile elements
    private Label profileStatus;
    private Label profileName;
    private Label userIdValue;
    private Label emailValue;
    private Label tokenExpiryValue;
    private VisualElement attributesContainer;
    
    private void Awake()
    {
        Debug.Log("StoreUIAuthController: Awake called");
    }
    
    private void Start()
    {
        Debug.Log("StoreUIAuthController: Start method called");
        
        // Try to find TabController if not set
        if (tabController == null)
        {
            tabController = GetComponent<TabController>();
            if (tabController == null)
            {
                tabController = FindObjectOfType<TabController>();
            }
            
            if (tabController != null)
            {
                Debug.Log("StoreUIAuthController: Found TabController");
            }
            else
            {
                Debug.LogWarning("StoreUIAuthController: TabController not found! Tab switching won't work.");
            }
        }
        
        // Call with slight delay to ensure everything is properly set up
        StartCoroutine(InitializeWithDelay(0.5f));
        
        // Start periodic profile updates for token countdown
        StartCoroutine(PeriodicProfileUpdate());
    }
    
    private IEnumerator InitializeWithDelay(float delay)
    {
        Debug.Log($"StoreUIAuthController: Waiting {delay}s before initialization");
        yield return new WaitForSeconds(delay);
        
        Initialize();
    }
    
    private void Initialize()
    {
        Debug.Log("StoreUIAuthController: Initialize method called");
        
        if (storeUIDocument == null)
        {
            storeUIDocument = GetComponent<UIDocument>();
            Debug.Log("StoreUIAuthController: Found UIDocument on same GameObject");
        }
        
        if (storeUIDocument == null)
        {
            Debug.LogError("No UIDocument found for StoreUIAuthController");
            return;
        }
        
        // Try to ensure TabController is initialized
        if (tabController != null)
        {
            Debug.Log("StoreUIAuthController: Initializing TabController explicitly");
            tabController.InitializeTabController();
        }
        else
        {
            // Try to find it again
            tabController = GetComponent<TabController>();
            if (tabController == null)
            {
                tabController = FindObjectOfType<TabController>();
            }
            
            if (tabController != null)
            {
                Debug.Log("StoreUIAuthController: Found TabController, initializing it");
                tabController.InitializeTabController();
            }
        }
        
        // Get UI elements
        var root = storeUIDocument.rootVisualElement;
        
        // Get tab buttons as fallback (in case TabController isn't working)
        var userTabButton = root.Q<Button>("user-tab-button");
        var storeTabButton = root.Q<Button>("store-tab-button");
        
        // Get tab content panels
        var userTabContent = root.Q<VisualElement>("user-tab-content");
        var storeTabContent = root.Q<VisualElement>("store-tab-content");
        
        // Set up fallback tab button handlers
        if (userTabButton != null && storeTabButton != null && 
            userTabContent != null && storeTabContent != null && tabController == null)
        {
            Debug.Log("StoreUIAuthController: Setting up fallback tab button handlers (TabController not found)");
            
            // Set up direct handlers for tab buttons only if TabController is not present
            userTabButton.clicked += () => {
                Debug.Log("User tab button clicked directly");
                DirectTabSwitch(userTabButton, storeTabButton, userTabContent, storeTabContent, true);
            };
            
            storeTabButton.clicked += () => {
                Debug.Log("IAP tab button clicked directly");
                DirectTabSwitch(userTabButton, storeTabButton, userTabContent, storeTabContent, false);
            };
        }
        
        // Get user profile elements
        loginButton = root.Q<Button>("login-button");
        profileStatus = root.Q<Label>("profile-status");
        profileName = root.Q<Label>("profile-name");
        userIdValue = root.Q<Label>("user-id-value"); 
        emailValue = root.Q<Label>("email-value");
        tokenExpiryValue = root.Q<Label>("token-expiry-value");
        attributesContainer = root.Q<VisualElement>("attributes-container");
        refreshTokenButton = root.Q<Button>("refresh-token-button");
        
        Debug.Log($"StoreUIAuthController: UI Elements - " +
                 $"Login Button: {(loginButton != null ? "Found" : "Not Found")}, " +
                 $"Profile Status: {(profileStatus != null ? "Found" : "Not Found")}, " +
                 $"Profile Name: {(profileName != null ? "Found" : "Not Found")}, " +
                 $"UserID: {(userIdValue != null ? "Found" : "Not Found")}, " +
                 $"Email: {(emailValue != null ? "Found" : "Not Found")}, " +
                 $"Token Expiry: {(tokenExpiryValue != null ? "Found" : "Not Found")}, " +
                 $"Attributes Container: {(attributesContainer != null ? "Found" : "Not Found")}, " +
                 $"Refresh Token Button: {(refreshTokenButton != null ? "Found" : "Not Found")}");
        
        if (loginButton == null)
        {
            Debug.LogError("StoreUIAuthController: Login button not found! Check UXML file.");
            return;
        }
        
        // Setup event handlers
        loginButton.clicked += OnLoginButtonClicked;
        Debug.Log("StoreUIAuthController: Login button click handler attached");
        
        // Setup refresh token button handler
        if (refreshTokenButton != null)
        {
            refreshTokenButton.clicked += OnRefreshTokenButtonClicked;
            Debug.Log("StoreUIAuthController: Force Refresh button click handler attached");
        }
        else
        {
            Debug.LogWarning("StoreUIAuthController: Force Refresh button not found");
        }
        
        // Subscribe to authentication events
        AuthenticationManager authManager = FindObjectOfType<AuthenticationManager>();
        if (authManager != null)
        {
            Debug.Log("StoreUIAuthController: Found AuthenticationManager, subscribing to events");
            
            // Unsubscribe first to avoid double subscription
            authManager.OnLoginSuccess -= OnLoginSuccess;
            authManager.OnLoginFailed -= OnLoginFailed;
            authManager.OnLogout -= OnLogout;
            
            // Now subscribe
            authManager.OnLoginSuccess += OnLoginSuccess;
            authManager.OnLoginFailed += OnLoginFailed;
            authManager.OnLogout += OnLogout;
            
            Debug.Log("StoreUIAuthController: Subscribed to authentication events");
        }
        else
        {
            Debug.LogWarning("No AuthenticationManager found in the scene. Authentication will not work.");
        }
        
        // Update UI based on current auth state
        UpdateAuthUI();
        
        // Force a refresh after a short delay to ensure UI is updated
        StartCoroutine(ForceUIRefresh());
    }
    
    private IEnumerator ForceUIRefresh()
    {
        yield return new WaitForSeconds(1.0f);
        UpdateAuthUI();
        Debug.Log("StoreUIAuthController: Forced UI refresh");
    }
    
    private void OnDestroy()
    {
        Debug.Log("StoreUIAuthController: OnDestroy called, unsubscribing from events");
        
        // Unsubscribe from events
        AuthenticationManager authManager = FindObjectOfType<AuthenticationManager>();
        if (authManager != null)
        {
            authManager.OnLoginSuccess -= OnLoginSuccess;
            authManager.OnLoginFailed -= OnLoginFailed;
            authManager.OnLogout -= OnLogout;
            Debug.Log("StoreUIAuthController: Unsubscribed from authentication events");
        }
    }
    
    private void OnLoginSuccess()
    {
        Debug.Log("StoreUIAuthController: OnLoginSuccess event received");
        UpdateAuthUI();
        
        // Switch to the user tab to show profile
        SwitchToUserTab();
    }
    
    private void OnLoginFailed(string errorMessage)
    {
        Debug.LogError($"StoreUIAuthController: Login failed: {errorMessage}");
        UpdateAuthUI();
    }
    
    private void OnLogout()
    {
        Debug.Log("StoreUIAuthController: OnLogout event received");
        UpdateAuthUI();
    }
    
    private void OnLoginButtonClicked()
    {
        Debug.Log("StoreUIAuthController: Login button clicked");
        
        try
        {
            // Check if already authenticated
            if (AuthenticationManager.Instance == null)
            {
                Debug.LogError("AuthenticationManager.Instance is null");
                return;
            }
            
            bool isAuthenticated = AuthenticationManager.Instance.IsAuthenticated();
            Debug.Log($"Current authentication state: {(isAuthenticated ? "Authenticated" : "Not authenticated")}");
            
            if (isAuthenticated)
            {
                Debug.Log("StoreUIAuthController: User is already authenticated, logging out");
                // If already logged in, perform logout
                AuthenticationManager.Instance.Logout();
            }
            else
            {
                Debug.Log("StoreUIAuthController: User is not authenticated, initiating login flow");
                // Trigger login flow
                AuthenticationManager.Instance.OpenLoginUI();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error in OnLoginButtonClicked: {ex.Message}\n{ex.StackTrace}");
        }
    }
    
    private void OnRefreshTokenButtonClicked()
    {
        Debug.Log("StoreUIAuthController: Refresh token button clicked");
        
        if (AuthenticationManager.Instance != null && AuthenticationManager.Instance.IsAuthenticated())
        {
            // Disable the button while refresh is in progress
            if (refreshTokenButton != null)
            {
                refreshTokenButton.SetEnabled(false);
                refreshTokenButton.text = "Refreshing...";
            }
            
            // Force refresh the token
            bool refreshStarted = AuthenticationManager.Instance.ForceRefreshToken();
            
            if (refreshStarted)
            {
                Debug.Log("StoreUIAuthController: Token refresh initiated successfully");
                
                // Re-enable the button after a delay
                StartCoroutine(ReEnableRefreshButton(3f));
            }
            else
            {
                Debug.LogError("StoreUIAuthController: Failed to initiate token refresh");
                
                // Re-enable the button immediately
                if (refreshTokenButton != null)
                {
                    refreshTokenButton.SetEnabled(true);
                    refreshTokenButton.text = "Refresh Failed";
                    
                    // Reset text after a delay
                    StartCoroutine(ReEnableRefreshButton(2f));
                }
            }
        }
        else
        {
            Debug.LogWarning("StoreUIAuthController: Cannot refresh token - user not authenticated");
            
            if (refreshTokenButton != null)
            {
                refreshTokenButton.text = "Not Logged In";
                
                // Reset text after a delay
                StartCoroutine(ReEnableRefreshButton(2f));
            }
        }
    }
    
    private IEnumerator ReEnableRefreshButton(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (refreshTokenButton != null)
        {
            refreshTokenButton.SetEnabled(true);
            refreshTokenButton.text = "Force Refresh";
        }
    }
    
    private void UpdateAuthUI()
    {
        Debug.Log("StoreUIAuthController: UpdateAuthUI called");
        
        bool isAuthenticated = AuthenticationManager.Instance != null && 
                              AuthenticationManager.Instance.IsAuthenticated();
        
        Debug.Log($"StoreUIAuthController: Authentication state - IsAuthenticated: {isAuthenticated}");
        
        // Update user profile in the User tab
        UpdateUserProfile(isAuthenticated);
    }
    
    private void UpdateUserProfile(bool isAuthenticated)
    {
        Debug.Log($"UpdateUserProfile called with isAuthenticated = {isAuthenticated}");

        // Update login button
        if (loginButton != null)
        {
            loginButton.text = isAuthenticated ? "Logout" : "Login";
            Debug.Log($"Updated login button text to: {loginButton.text}");
        }
        else
        {
            Debug.LogError("Login button is null!");
        }
        
        if (profileStatus != null)
        {
            if (isAuthenticated)
            {
                try
                {
                    // Get user data from AuthenticationManager
                    UserData userData = AuthenticationManager.Instance.GetUserData();
                    
                    profileStatus.text = "Logged in";
                    profileStatus.style.color = new Color(0.3f, 0.8f, 0.3f); // Green color
                    
                    Debug.Log($"Got user data: {userData.UserId}, {userData.Email}, {userData.Name}");
                    
                    // Update profile details card
                    if (profileName != null)
                    {
                        profileName.text = string.IsNullOrEmpty(userData.Name) ? 
                            "User" : userData.Name;
                        Debug.Log($"Updated profile name to: {profileName.text}");
                    }
                    else
                    {
                        Debug.LogError("profileName element is null!");
                    }
                    
                    if (userIdValue != null)
                    {
                        userIdValue.text = userData.UserId;
                        Debug.Log($"Updated user ID to: {userIdValue.text}");
                    }
                    else
                    {
                        Debug.LogError("userIdValue element is null!");
                    }
                    
                    if (emailValue != null)
                    {
                        emailValue.text = userData.Email;
                        Debug.Log($"Updated email to: {emailValue.text}");
                    }
                    else
                    {
                        Debug.LogError("emailValue element is null!");
                    }
                    
                    // Update token expiry
                    if (tokenExpiryValue != null)
                    {
                        TimeSpan tokenRemaining = AuthenticationManager.Instance.GetTokenTimeRemaining();
                        string expiryText;
                        
                        if (tokenRemaining.Hours > 0)
                        {
                            expiryText = $"{tokenRemaining.Hours}h {tokenRemaining.Minutes}m {tokenRemaining.Seconds}s";
                        }
                        else
                        {
                            expiryText = $"{tokenRemaining.Minutes}m {tokenRemaining.Seconds}s";
                        }
                        
                        tokenExpiryValue.text = expiryText;
                        Debug.Log($"Updated token expiry to: {tokenExpiryValue.text}");
                    }
                    else
                    {
                        Debug.LogError("tokenExpiryValue element is null!");
                    }
                    
                    // Update attributes container
                    if (attributesContainer != null)
                    {
                        // Clear previous attributes
                        attributesContainer.Clear();
                        
                        Debug.Log($"Attributes count: {userData.Attributes.Count}");
                        
                        if (userData.Attributes.Count > 0)
                        {
                            // Sort attributes for consistent display
                            var sortedAttrs = userData.Attributes.OrderBy(a => a.Key).ToList();
                            
                            // Important attributes to show first (prioritize common ones)
                            string[] priorityAttrs = new[] { "sub", "email", "name", "given_name", "family_name", "exp", "iat" };
                            
                            // Add priority attributes first
                            foreach (var key in priorityAttrs)
                            {
                                if (userData.Attributes.TryGetValue(key, out string value))
                                {
                                    // Format special attributes differently (like timestamps)
                                    if (key == "exp" || key == "iat" || key == "auth_time")
                                    {
                                        // Try to convert Unix timestamp to readable date if possible
                                        if (long.TryParse(value, out long timestamp))
                                        {
                                            DateTime date = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                                                .AddSeconds(timestamp)
                                                .ToLocalTime();
                                            value = date.ToString("yyyy-MM-dd HH:mm:ss");
                                        }
                                    }
                                    
                                    AddAttributeRow(attributesContainer, key, value);
                                    sortedAttrs.RemoveAll(a => a.Key == key);
                                }
                            }
                            
                            // Add remaining attributes
                            foreach (var attr in sortedAttrs)
                            {
                                AddAttributeRow(attributesContainer, attr.Key, attr.Value);
                            }
                            
                            Debug.Log("Attributes added to container");
                        }
                        else
                        {
                            Debug.LogWarning("No attributes found in user data!");
                        }
                    }
                    else
                    {
                        Debug.LogError("attributesContainer element is null!");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error updating user profile: {ex.Message}\n{ex.StackTrace}");
                    // Reset to non-logged in state on error
                    profileStatus.text = "Error loading profile";
                    profileStatus.style.color = new Color(0.8f, 0.3f, 0.3f); // Red color
                }
            }
            else
            {
                profileStatus.text = "Not logged in";
                profileStatus.style.color = new Color(0.7f, 0.7f, 0.7f); // Gray color
                
                // Reset profile details
                if (profileName != null)
                {
                    profileName.text = "User Name";
                }
                
                if (userIdValue != null)
                {
                    userIdValue.text = "--";
                }
                
                if (emailValue != null)
                {
                    emailValue.text = "--";
                }
                
                if (tokenExpiryValue != null)
                {
                    tokenExpiryValue.text = "--";
                }
                
                // Clear attributes
                if (attributesContainer != null)
                {
                    attributesContainer.Clear();
                }
            }
        }
        else
        {
            Debug.LogError("profileStatus element is null!");
        }
        
        // Update refresh token button visibility
        if (refreshTokenButton != null)
        {
            refreshTokenButton.style.display = isAuthenticated ? DisplayStyle.Flex : DisplayStyle.None;
        }
        else
        {
            Debug.LogWarning("refreshTokenButton element is null!");
        }
    }
    
    /// <summary>
    /// Helper method to add an attribute row to the container
    /// </summary>
    private void AddAttributeRow(VisualElement container, string name, string value)
    {
        // Skip empty values
        if (string.IsNullOrEmpty(value))
            return;
        
        // Create row container
        VisualElement row = new VisualElement();
        row.AddToClassList("attribute-row");
        
        // Create name label
        Label nameLabel = new Label(FormatAttributeName(name));
        nameLabel.AddToClassList("attribute-name");
        
        // Create value label
        Label valueLabel = new Label(value);
        valueLabel.AddToClassList("attribute-value");
        
        // Add to row
        row.Add(nameLabel);
        row.Add(valueLabel);
        
        // Add to container
        container.Add(row);
        
        Debug.Log($"Added attribute row: {name} = {value}");
    }
    
    /// <summary>
    /// Helper method to format attribute names to be more readable
    /// </summary>
    private string FormatAttributeName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;
        
        // Replace underscores with spaces
        string result = name.Replace('_', ' ');
        
        // Capitalize each word
        TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;
        result = textInfo.ToTitleCase(result);
        
        // Special abbreviations that should be uppercase
        if (name == "sub")
            return "SUB";
        if (name == "iat")
            return "IAT";
            
        return result;
    }
    
    private void SwitchToUserTab()
    {
        Debug.Log("StoreUIAuthController: Attempting to switch to User tab");
        
        // Use TabController reference to switch tabs
        if (tabController != null)
        {
            Debug.Log("StoreUIAuthController: Using TabController to switch tabs");
            tabController.SelectTab("user");
        }
        else
        {
            Debug.LogWarning("StoreUIAuthController: TabController not found, using direct tab switching");
            
            // Try to find it again as a fallback
            tabController = FindObjectOfType<TabController>();
            if (tabController != null)
            {
                Debug.Log("StoreUIAuthController: Found TabController on fallback");
                tabController.SelectTab("user");
            }
            else
            {
                // Direct tab switching as last resort
                DirectSwitchToUserTab();
            }
        }
    }
    
    private void DirectSwitchToUserTab()
    {
        Debug.Log("StoreUIAuthController: Using direct tab switching method");
        
        var root = storeUIDocument.rootVisualElement;
        var userTabButton = root.Q<Button>("user-tab-button");
        var storeTabButton = root.Q<Button>("store-tab-button");
        var userTabContent = root.Q<VisualElement>("user-tab-content");
        var storeTabContent = root.Q<VisualElement>("store-tab-content");
        
        if (userTabButton != null && storeTabButton != null && 
            userTabContent != null && storeTabContent != null)
        {
            // Update button states first - clear all tab selections
            userTabButton.RemoveFromClassList("tab-selected");
            storeTabButton.RemoveFromClassList("tab-selected");
            
            // Select the user tab
            userTabButton.AddToClassList("tab-selected");
            
            // Update visibility - this is crucial for the tab content to show
            userTabContent.style.display = DisplayStyle.Flex;
            storeTabContent.style.display = DisplayStyle.None;
            
            // Force visual refresh
            userTabButton.MarkDirtyRepaint();
            storeTabButton.MarkDirtyRepaint();
            
            Debug.Log("StoreUIAuthController: Successfully switched to User tab directly");
        }
        else
        {
            Debug.LogError("StoreUIAuthController: Failed to find tab elements for direct switching");
        }
    }
    
    // Helper method for direct tab switching
    private void DirectTabSwitch(Button userButton, Button storeButton, VisualElement userContent, VisualElement storeContent, bool showUserTab)
    {
        Debug.Log($"StoreUIAuthController: DirectTabSwitch called, showUserTab={showUserTab}");
        
        // Clear previous selections
        userButton.RemoveFromClassList("tab-selected");
        storeButton.RemoveFromClassList("tab-selected");
        
        if (showUserTab)
        {
            // Add the selected class to the user tab
            userButton.AddToClassList("tab-selected");
            
            // Show user content, hide store content
            userContent.style.display = DisplayStyle.Flex;
            storeContent.style.display = DisplayStyle.None;
            
            Debug.Log("StoreUIAuthController: Switched to User tab");
        }
        else
        {
            // Add the selected class to the store tab
            storeButton.AddToClassList("tab-selected");
            
            // Show store content, hide user content
            userContent.style.display = DisplayStyle.None;
            storeContent.style.display = DisplayStyle.Flex;
            
            Debug.Log("StoreUIAuthController: Switched to Store tab");
        }
        
        // Force a style refresh
        userButton.MarkDirtyRepaint();
        storeButton.MarkDirtyRepaint();
    }
    
    private IEnumerator PeriodicProfileUpdate()
    {
        while (true)
        {
            // Wait for 1 second
            yield return new WaitForSeconds(1f);
            
            // Check if user is authenticated
            bool isAuthenticated = AuthenticationManager.Instance != null && 
                                  AuthenticationManager.Instance.IsAuthenticated();
            
            // Only update the profile if authenticated to show token countdown
            if (isAuthenticated)
            {
                UpdateUserProfile(true);
            }
        }
    }
} 