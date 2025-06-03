using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using System.Text;
using System.Globalization;
using System.Linq;
using UnityEngine.Networking;
using System.Threading.Tasks;

public class StoreUIAuthController : MonoBehaviour
{
    private const string API_KEY = "p0SVSU3awmdDv8VUPFZ_adWz_uC81xXsEY95Gg7WSwx9TZAJ5_ch-ePXK2Xh3B6o";
    private const string WEBSHOP_URL_ENDPOINT = "https://test-api.stash.gg/sdk/server/generate_url";
    
    [SerializeField] private UIDocument storeUIDocument;
    
    private Button loginButton;
    private Button refreshTokenButton;
    private Button webshopButton;
    private Button helpButton;
    private Button closeHelpButton;
    private Button watchVideoButton;
    private VisualElement helpDialog;
    private VisualElement helpDescriptionDialog;
    private Button helpDescriptionCloseButton;
    private Label helpDescriptionText;
    
    // User profile elements
    private Label profileStatus;
    private Label profileName;
    private Label userIdValue;
    private Label emailValue;
    private Label tokenExpiryValue;
    private VisualElement attributesContainer;
    
    // Tab elements
    private Button userTabButton;
    private Button storeTabButton;
    private Button webshopTabButton;
    private VisualElement userTabContent;
    private VisualElement storeTabContent;
    private VisualElement webshopTabContent;
    
    private readonly Dictionary<string, string> tabDescriptions = new()
    {
        { "user", "Stash works with any authentication provider out of the box. Login or create account here to try webshop account linking and Stash Pay seamless identification during purchase." },
        { "store", "Try Stash Pay with native system dialog, our seamless D2C alternative to in-app purchases. Check out anonymously, or log in in the Account tab to use your game account." },
        { "webshop", "Open pre-authenticated Stash Webshop with a press of a button right from the game client. Log in in the Account tab to use your custom account."}
    };
    
    private readonly Dictionary<string, string> tabHeaders = new()
    {
        { "user", "Account Linking" },
        { "store", "Stash Pay for IAPs" },
        { "webshop", "Stash Webshop" }
    };
    
    private const string HELP_DISMISSED_PREFIX = "help_dismissed_";
    
    private void Awake()
    {
        Debug.Log("StoreUIAuthController: Awake called");
        
        // Reset help dialog dismissal settings
        PlayerPrefs.DeleteKey(HELP_DISMISSED_PREFIX + "user");
        PlayerPrefs.DeleteKey(HELP_DISMISSED_PREFIX + "store");
        PlayerPrefs.DeleteKey(HELP_DISMISSED_PREFIX + "webshop");
        PlayerPrefs.Save();
        Debug.Log("StoreUIAuthController: Reset help dialog dismissal settings");
    }
    
    private void Start()
    {
        Debug.Log("StoreUIAuthController: Start method called");
        
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
        
        // Get UI elements
        var root = storeUIDocument.rootVisualElement;
        Debug.Log("StoreUIAuthController: Got root visual element");
        
        // Get help elements
        helpButton = root.Q<Button>("help-button");
        closeHelpButton = root.Q<Button>("close-help-button");
        watchVideoButton = root.Q<Button>("watch-video-button");
        helpDialog = root.Q<VisualElement>("help-dialog");
        helpDescriptionDialog = root.Q<VisualElement>("help-description-dialog");
        helpDescriptionCloseButton = root.Q<Button>("help-description-close-button");
        helpDescriptionText = root.Q<Label>("help-description-text");
        
        if (helpButton != null && closeHelpButton != null && helpDialog != null && watchVideoButton != null)
        {
            helpButton.clicked += OnHelpButtonClicked;
            closeHelpButton.clicked += OnCloseHelpButtonClicked;
            watchVideoButton.clicked += OnWatchVideoButtonClicked;
            Debug.Log("StoreUIAuthController: Help button handlers attached");
        }
        else
        {
            Debug.LogWarning("StoreUIAuthController: Some help elements not found");
        }
        
        if (helpDescriptionDialog != null && helpDescriptionCloseButton != null && helpDescriptionText != null)
        {
            helpDescriptionCloseButton.clicked += OnHelpDescriptionCloseButtonClicked;
            Debug.Log("StoreUIAuthController: Help description dialog handlers attached");
        }
        else
        {
            Debug.LogWarning("StoreUIAuthController: Some help description elements not found");
        }
        
        // Get tab buttons
        userTabButton = root.Q<Button>("user-tab-button");
        storeTabButton = root.Q<Button>("store-tab-button");
        webshopTabButton = root.Q<Button>("webshop-tab-button");
        webshopButton = root.Q<Button>("open-webshop-button");
        
        Debug.Log($"StoreUIAuthController: Found tab buttons - User: {userTabButton != null}, Store: {storeTabButton != null}, Webshop: {webshopTabButton != null}");
        
        // Get tab content panels
        userTabContent = root.Q<VisualElement>("user-tab-content");
        storeTabContent = root.Q<VisualElement>("store-tab-content");
        webshopTabContent = root.Q<VisualElement>("webshop-tab-content");
        
        Debug.Log($"StoreUIAuthController: Found tab content - User: {userTabContent != null}, Store: {storeTabContent != null}, Webshop: {webshopTabContent != null}");
        
        // Set up tab button handlers
        if (userTabButton != null && storeTabButton != null && webshopTabButton != null && 
            userTabContent != null && storeTabContent != null && webshopTabContent != null)
        {
            Debug.Log("StoreUIAuthController: Setting up tab button handlers");
            
            // Clear any existing click events
            userTabButton.clicked -= () => DirectTabSwitch("user");
            storeTabButton.clicked -= () => DirectTabSwitch("store");
            webshopTabButton.clicked -= () => DirectTabSwitch("webshop");
            
            // Add new click events
            userTabButton.clicked += () => {
                Debug.Log("User tab button clicked");
                DirectTabSwitch("user");
            };
            
            storeTabButton.clicked += () => {
                Debug.Log("Store tab button clicked");
                DirectTabSwitch("store");
            };
            
            webshopTabButton.clicked += () => {
                Debug.Log("Webshop tab button clicked");
                DirectTabSwitch("webshop");
            };
            
            Debug.Log("StoreUIAuthController: Tab button handlers set up successfully");
            
            // Set initial tab
            DirectTabSwitch("store");
        }
        else
        {
            Debug.LogError("StoreUIAuthController: Failed to find all tab elements");
            if (userTabButton == null) Debug.LogError("userTabButton is null");
            if (storeTabButton == null) Debug.LogError("storeTabButton is null");
            if (webshopTabButton == null) Debug.LogError("webshopTabButton is null");
            if (userTabContent == null) Debug.LogError("userTabContent is null");
            if (storeTabContent == null) Debug.LogError("storeTabContent is null");
            if (webshopTabContent == null) Debug.LogError("webshopTabContent is null");
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
        
        // Setup webshop button handler
        if (webshopButton != null)
        {
            webshopButton.clicked += OnWebshopButtonClicked;
            Debug.Log("StoreUIAuthController: Webshop button click handler attached");
        }
        else
        {
            Debug.LogWarning("StoreUIAuthController: Webshop button not found");
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
        DirectTabSwitch("user");
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

        // Update webshop button state
        if (webshopButton != null)
        {
            if (isAuthenticated)
            {
                webshopButton.text = "Open Webshop";
                webshopButton.SetEnabled(true);
                webshopButton.RemoveFromClassList("disabled-button");
            }
            else
            {
                webshopButton.text = "Please Login First";
                webshopButton.SetEnabled(false);
                webshopButton.AddToClassList("disabled-button");
            }
        }
    }
    
    private void UpdateUserProfile(bool isAuthenticated)
    {
        Debug.Log($"UpdateUserProfile called with isAuthenticated = {isAuthenticated}");

        // Update login button
        if (loginButton != null)
        {
            loginButton.text = isAuthenticated ? "LOGOUT" : "LOGIN / SIGN UP";
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
    
    // Helper method for direct tab switching
    private void DirectTabSwitch(string tabToShow)
    {
        Debug.Log($"StoreUIAuthController: DirectTabSwitch called, tabToShow={tabToShow}");
        
        if (userTabButton == null || storeTabButton == null || webshopTabButton == null ||
            userTabContent == null || storeTabContent == null || webshopTabContent == null)
        {
            Debug.LogError("StoreUIAuthController: Tab elements are null in DirectTabSwitch");
            return;
        }

        // Hide help dialog first
        HideHelpDescription();
        
        // Clear previous selections
        userTabButton.RemoveFromClassList("tab-selected");
        storeTabButton.RemoveFromClassList("tab-selected");
        webshopTabButton.RemoveFromClassList("tab-selected");
        
        // Hide all content
        userTabContent.style.display = DisplayStyle.None;
        storeTabContent.style.display = DisplayStyle.None;
        webshopTabContent.style.display = DisplayStyle.None;
        
        // Show selected tab
        switch (tabToShow.ToLower())
        {
            case "user":
                userTabButton.AddToClassList("tab-selected");
                userTabContent.style.display = DisplayStyle.Flex;
                Debug.Log("StoreUIAuthController: Switched to User tab");
                break;
                
            case "store":
                storeTabButton.AddToClassList("tab-selected");
                storeTabContent.style.display = DisplayStyle.Flex;
                Debug.Log("StoreUIAuthController: Switched to Store tab");
                break;
                
            case "webshop":
                webshopTabButton.AddToClassList("tab-selected");
                webshopTabContent.style.display = DisplayStyle.Flex;
                Debug.Log("StoreUIAuthController: Switched to Webshop tab");
                break;
                
            default:
                Debug.LogError($"StoreUIAuthController: Unknown tab {tabToShow}");
                break;
        }

        // Show help description for the selected tab after a short delay
        StartCoroutine(ShowHelpDescriptionWithDelay(tabToShow));
        
        // Force a style refresh
        userTabButton.MarkDirtyRepaint();
        storeTabButton.MarkDirtyRepaint();
        webshopTabButton.MarkDirtyRepaint();
        
        // Force content refresh
        userTabContent.MarkDirtyRepaint();
        storeTabContent.MarkDirtyRepaint();
        webshopTabContent.MarkDirtyRepaint();
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

    [Serializable]
    private class WebshopResponse
    {
        public string url;
    }

    [Serializable]
    private class WebshopUser
    {
        public string id;
        public string email;
    }

    [Serializable]
    private class WebshopRequest
    {
        public WebshopUser user;
        public string target;
    }

    private async void OnWebshopButtonClicked()
    {
        Debug.Log("StoreUIAuthController: Webshop button clicked");
        
        string userId;
        string userEmail;
        
        if (AuthenticationManager.Instance == null || !AuthenticationManager.Instance.IsAuthenticated())
        {
            #if UNITY_EDITOR
            // Generate random user ID and email only in Unity Editor
            userEmail = $"editor_{Guid.NewGuid().ToString("N").Substring(0, 8)}@example.com";
            userId = userEmail; // Use email as ID
            Debug.Log($"StoreUIAuthController: Generated random email for editor: {userEmail}");
            #else
            Debug.LogWarning("StoreUIAuthController: Cannot open webshop - user not authenticated");
            return;
            #endif
        }
        else
        {
            // Get user data from AuthenticationManager
            UserData userData = AuthenticationManager.Instance.GetUserData();
            if (string.IsNullOrEmpty(userData.Email))
            {
                Debug.LogError("StoreUIAuthController: User email is null or empty");
                return;
            }
            userEmail = userData.Email;
            userId = userEmail; // Use email as ID
        }

        try
        {
            // Create request payload
            var request = new WebshopRequest
            {
                user = new WebshopUser
                {
                    id = userId, // This will now be the email
                    email = userEmail
                },
                target = "HOME"
            };

            string jsonPayload = JsonUtility.ToJson(request);
            Debug.Log($"StoreUIAuthController: Sending request with payload: {jsonPayload}");

            // Create and configure the request
            using (UnityWebRequest webRequest = new UnityWebRequest(WEBSHOP_URL_ENDPOINT, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
                webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json");
                webRequest.SetRequestHeader("x-stash-api-key", API_KEY);

                // Send the request
                var operation = webRequest.SendWebRequest();
                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    string response = webRequest.downloadHandler.text;
                    Debug.Log($"StoreUIAuthController: Raw response body:\n{response}");
                    Debug.Log($"StoreUIAuthController: Response status code: {webRequest.responseCode}");
                    Debug.Log($"StoreUIAuthController: Response headers: {webRequest.GetResponseHeaders()}");

                    // Parse the response
                    var responseData = JsonUtility.FromJson<WebshopResponse>(response);
                    if (!string.IsNullOrEmpty(responseData.url))
                    {
                        Debug.Log($"StoreUIAuthController: Opening webshop URL: {responseData.url}");
                        Application.OpenURL(responseData.url);
                    }
                    else
                    {
                        Debug.LogError("StoreUIAuthController: URL is null or empty in response");
                    }
                }
                else
                {
                    Debug.LogError($"StoreUIAuthController: Request failed: {webRequest.error}");
                    Debug.LogError($"StoreUIAuthController: Response code: {webRequest.responseCode}");
                    Debug.LogError($"StoreUIAuthController: Response headers: {webRequest.GetResponseHeaders()}");
                    Debug.LogError($"StoreUIAuthController: Raw response body:\n{webRequest.downloadHandler.text}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"StoreUIAuthController: Error opening webshop: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void OnHelpButtonClicked()
    {
        Debug.Log("StoreUIAuthController: Help button clicked");
        if (helpDialog != null)
        {
            helpDialog.style.display = DisplayStyle.Flex;
        }
    }
    
    private void OnCloseHelpButtonClicked()
    {
        Debug.Log("StoreUIAuthController: Close help button clicked");
        if (helpDialog != null)
        {
            helpDialog.style.display = DisplayStyle.None;
        }
    }

    private void OnWatchVideoButtonClicked()
    {
        Debug.Log("StoreUIAuthController: Watch video button clicked");
        Application.OpenURL("https://www.youtube.com/watch?v=dQw4w9WgXcQ"); // Replace with actual help video URL
    }

    private void OnHelpDescriptionCloseButtonClicked()
    {
        Debug.Log("StoreUIAuthController: Help description close button clicked");
        HideHelpDescription();
        
        // Get current active tab
        string activeTab = GetActiveTab();
        if (!string.IsNullOrEmpty(activeTab))
        {
            // Save that this tab's help was dismissed
            PlayerPrefs.SetInt(HELP_DISMISSED_PREFIX + activeTab, 1);
            PlayerPrefs.Save();
            Debug.Log($"StoreUIAuthController: Marked help as dismissed for tab: {activeTab}");
        }
    }

    private void ShowHelpDescription(string tabName)
    {
        if (helpDescriptionDialog == null || helpDescriptionText == null)
        {
            Debug.LogWarning("Help description dialog elements not found");
            return;
        }

        // Check if help was dismissed for this tab
        if (PlayerPrefs.GetInt(HELP_DISMISSED_PREFIX + tabName.ToLower(), 0) == 1)
        {
            Debug.Log($"StoreUIAuthController: Help already dismissed for tab: {tabName}");
            return;
        }

        // Hide first
        HideHelpDescription();

        // Start coroutine to show after a delay
        StartCoroutine(ShowHelpDescriptionWithDelay(tabName));
    }

    private IEnumerator ShowHelpDescriptionWithDelay(string tabName)
    {
        yield return new WaitForSeconds(0.3f); // Wait for hide animation

        // Check again if help was dismissed during the delay
        if (PlayerPrefs.GetInt(HELP_DISMISSED_PREFIX + tabName.ToLower(), 0) == 1)
        {
            Debug.Log($"StoreUIAuthController: Help dismissed during delay for tab: {tabName}");
            yield break;
        }

        if (helpDescriptionDialog != null && helpDescriptionText != null)
        {
            // Set the header text based on the tab
            var headerLabel = helpDescriptionDialog.Q<Label>("help-description-title");
            if (headerLabel != null)
            {
                string headerText = tabHeaders[tabName.ToLower()];
                headerLabel.text = headerText;
                Debug.Log($"StoreUIAuthController: Set help dialog header to: {headerText}");
            }
            else
            {
                Debug.LogError("StoreUIAuthController: Could not find help description title label");
            }

            // Set the description text
            helpDescriptionText.text = tabDescriptions[tabName.ToLower()];
            helpDescriptionDialog.AddToClassList("visible");
        }
    }

    private void HideHelpDescription()
    {
        if (helpDescriptionDialog != null)
        {
            helpDescriptionDialog.RemoveFromClassList("visible");
        }
    }

    private string GetActiveTab()
    {
        if (userTabButton != null && userTabButton.ClassListContains("tab-selected"))
            return "user";
        if (storeTabButton != null && storeTabButton.ClassListContains("tab-selected"))
            return "store";
        if (webshopTabButton != null && webshopTabButton.ClassListContains("tab-selected"))
            return "webshop";
        return string.Empty;
    }
} 