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
using Stash.Samples;

namespace Stash.Samples
{
    /// <summary>
    /// Manages the authentication UI for the Stash store sample.
    /// Handles user authentication display, tab navigation, and webshop integration.
    /// Works with AuthenticationManager to provide a complete authentication experience.
    /// Optional UXML elements (when present) enable extra features: refresh-token-button, help-button,
    /// close-help-button, watch-video-button, help-dialog, profile-name, token-expiry-value, attributes-container.
    /// </summary>
    public class StoreUIAuthController : MonoBehaviour
    {
        #region Constants
        private static string GetWebshopGenerateUrlEndpoint()
        {
            string envStr = PlayerPrefs.GetString(DemoAppConstants.PREF_STASH_ENVIRONMENT, "Test");
            var env = envStr == "Production" ? StashDemoEnvironment.Production : StashDemoEnvironment.Test;
            return $"{DemoAppConstants.GetStashApiBaseUrl(env)}/sdk/server/generate_url";
        }
        #endregion

        #region Fields
        [SerializeField] private UIDocument storeUIDocument;
        
        // UI Elements
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
        #endregion

        #region Tab Configuration
        private readonly Dictionary<string, string> tabDescriptions = new()
        {
            { "user", "Stash works with any authentication provider out of the box. Login or create account here to try webshop account linking and Stash Pay seamless identification during purchase." },
            { "store", "Try Stash Pay with native system dialog, our seamless D2C alternative to in-app purchases with purchase channel selection. Check out anonymously, or log in in the Account tab to use your game account." },
            { "webshop", "Open pre-authenticated Stash Webshop with a press of a button right from the game client. Log in in the Account tab to use your custom account."}
        };
        
        private readonly Dictionary<string, string> tabHeaders = new()
        {
            { "user", "Account Linking" },
            { "store", "Stash Pay for IAPs" },
            { "webshop", "Stash Webshop" }
        };
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            // Initializing Store Authentication UI
            
            // Reset help dialog dismissal settings for this session
            PlayerPrefs.DeleteKey(DemoAppConstants.PREF_HELP_DISMISSED + "user");
            PlayerPrefs.DeleteKey(DemoAppConstants.PREF_HELP_DISMISSED + "store");
            PlayerPrefs.DeleteKey(DemoAppConstants.PREF_HELP_DISMISSED + "webshop");
            PlayerPrefs.Save();
        }
        
        private void Start()
        {
            // Initialize with slight delay to ensure everything is properly set up
            StartCoroutine(InitializeWithDelay(0.5f));
            
            StartCoroutine(PeriodicProfileUpdate());
        }
        
        private void OnDestroy()
        {
            if (loginButton != null) loginButton.clicked -= OnLoginButtonClicked;
            if (refreshTokenButton != null) refreshTokenButton.clicked -= OnRefreshTokenButtonClicked;
            if (webshopButton != null) webshopButton.clicked -= OnWebshopButtonClicked;
            if (helpButton != null) helpButton.clicked -= OnHelpButtonClicked;
            if (closeHelpButton != null) closeHelpButton.clicked -= OnCloseHelpButtonClicked;
            if (watchVideoButton != null) watchVideoButton.clicked -= OnWatchVideoButtonClicked;
            if (helpDescriptionCloseButton != null) helpDescriptionCloseButton.clicked -= OnHelpDescriptionCloseButtonClicked;
            if (userTabButton != null) userTabButton.clicked -= OnUserTabClicked;
            if (storeTabButton != null) storeTabButton.clicked -= OnStoreTabClicked;
            if (webshopTabButton != null) webshopTabButton.clicked -= OnWebshopTabClicked;
            UnsubscribeFromAuthEvents();
        }
        #endregion

        #region Initialization
        private IEnumerator InitializeWithDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            InitializeUI();
        }
        
        private void InitializeUI()
        {
            // Initializing UI elements
            
            if (storeUIDocument == null)
            {
                storeUIDocument = GetComponent<UIDocument>();
                if (storeUIDocument == null)
                {
                    Debug.LogError("[StoreAuth] No UIDocument found");
                    return;
                }
            }
            
            var root = storeUIDocument.rootVisualElement;
            FindUIElements(root);
            SetupEventHandlers();
            SetupTabNavigation();
            SubscribeToAuthEvents();
            
            // Set initial state
            SelectTab("store");
            UpdateAuthUI();
            
            // UI initialization complete
        }

        private void FindUIElements(VisualElement root)
        {
            // Required: login-button, open-webshop-button, help-description-*, user-id-value, email-value, profile-status, tab buttons/content.
            // Optional (require UXML elements): refresh-token-button, help-button, close-help-button, watch-video-button, help-dialog, profile-name, token-expiry-value, attributes-container.
            loginButton = root.Q<Button>("login-button");
            refreshTokenButton = root.Q<Button>("refresh-token-button");
            webshopButton = root.Q<Button>("open-webshop-button");
            
            // Help elements
            helpButton = root.Q<Button>("help-button");
            closeHelpButton = root.Q<Button>("close-help-button");
            watchVideoButton = root.Q<Button>("watch-video-button");
            helpDialog = root.Q<VisualElement>("help-dialog");
            helpDescriptionDialog = root.Q<VisualElement>("help-description-dialog");
            helpDescriptionCloseButton = root.Q<Button>("help-description-close-button");
            helpDescriptionText = root.Q<Label>("help-description-text");
            
            // Profile elements
            profileStatus = root.Q<Label>("profile-status");
            profileName = root.Q<Label>("profile-name");
            userIdValue = root.Q<Label>("user-id-value");
            emailValue = root.Q<Label>("email-value");
            tokenExpiryValue = root.Q<Label>("token-expiry-value");
            attributesContainer = root.Q<VisualElement>("attributes-container");
            
            // Tab elements
            userTabButton = root.Q<Button>("user-tab-button");
            storeTabButton = root.Q<Button>("store-tab-button");
            webshopTabButton = root.Q<Button>("webshop-tab-button");
            userTabContent = root.Q<VisualElement>("user-tab-content");
            storeTabContent = root.Q<VisualElement>("store-tab-content");
            webshopTabContent = root.Q<VisualElement>("webshop-tab-content");
        }

        private void SetupEventHandlers()
        {
            // Authentication handlers
            if (loginButton != null)
                loginButton.clicked += OnLoginButtonClicked;
            
            if (refreshTokenButton != null)
                refreshTokenButton.clicked += OnRefreshTokenButtonClicked;
            
            if (webshopButton != null)
                webshopButton.clicked += OnWebshopButtonClicked;
            
            // Help handlers
            if (helpButton != null)
                helpButton.clicked += OnHelpButtonClicked;
            
            if (closeHelpButton != null)
                closeHelpButton.clicked += OnCloseHelpButtonClicked;
            
            if (watchVideoButton != null)
                watchVideoButton.clicked += OnWatchVideoButtonClicked;
            
            if (helpDescriptionCloseButton != null)
                helpDescriptionCloseButton.clicked += OnHelpDescriptionCloseButtonClicked;
        }

        private void SetupTabNavigation()
        {
            if (userTabButton == null || storeTabButton == null || webshopTabButton == null ||
                userTabContent == null || storeTabContent == null || webshopTabContent == null)
            {
                Debug.LogError("[StoreAuth] Tab elements not found");
                return;
            }
            userTabButton.clicked += OnUserTabClicked;
            storeTabButton.clicked += OnStoreTabClicked;
            webshopTabButton.clicked += OnWebshopTabClicked;
        }

        private void OnUserTabClicked() => SelectTab("user");
        private void OnStoreTabClicked() => SelectTab("store");
        private void OnWebshopTabClicked() => SelectTab("webshop");

        private void SubscribeToAuthEvents()
        {
            var authManager = AuthenticationManager.Instance;
            if (authManager != null)
            {
                authManager.OnLoginSuccess += OnLoginSuccess;
                authManager.OnLoginFailed += OnLoginFailed;
                authManager.OnLogout += OnLogout;
                // Subscribed to authentication events
            }
            else
            {
                Debug.LogWarning("[StoreAuth] No AuthenticationManager found");
            }
        }

        private void UnsubscribeFromAuthEvents()
        {
            var authManager = AuthenticationManager.Instance;
            if (authManager != null)
            {
                authManager.OnLoginSuccess -= OnLoginSuccess;
                authManager.OnLoginFailed -= OnLoginFailed;
                authManager.OnLogout -= OnLogout;
            }
        }
        #endregion

        #region Authentication Event Handlers
        private void OnLoginSuccess()
        {
            // Login success received
            UpdateAuthUI();
            SelectTab("user");
        }
        
        private void OnLoginFailed(string errorMessage)
        {
            Debug.LogError($"[StoreAuth] Login failed: {errorMessage}");
            UpdateAuthUI();
        }
        
        private void OnLogout()
        {
            // Logout received
            UpdateAuthUI();
        }
        #endregion

        #region Button Event Handlers
        private void OnLoginButtonClicked()
        {
            // Login button clicked
            
            try
            {
                if (AuthenticationManager.Instance == null)
                {
                    Debug.LogError("[StoreAuth] AuthenticationManager.Instance is null");
                    return;
                }
                
                bool isAuthenticated = AuthenticationManager.Instance.IsAuthenticated();
                
                if (isAuthenticated)
                {
                    AuthenticationManager.Instance.Logout();
                }
                else
                {
                    AuthenticationManager.Instance.OpenLoginUI();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[StoreAuth] Error in login button handler: {ex.Message}");
            }
        }
        
        private void OnRefreshTokenButtonClicked()
        {
            // Refresh token button clicked
            
            if (AuthenticationManager.Instance?.IsAuthenticated() == true)
            {
                refreshTokenButton?.SetEnabled(false);
                if (refreshTokenButton != null)
                    refreshTokenButton.text = "Refreshing...";
                
                bool refreshStarted = AuthenticationManager.Instance.ForceRefreshToken();
                
                if (refreshStarted)
                {
                    StartCoroutine(ReEnableRefreshButton(3f));
                }
                else
                {
                    if (refreshTokenButton != null)
                    {
                        refreshTokenButton.text = "Refresh Failed";
                        StartCoroutine(ReEnableRefreshButton(2f));
                    }
                }
            }
            else
            {
                if (refreshTokenButton != null)
                {
                    refreshTokenButton.text = "Not Logged In";
                    StartCoroutine(ReEnableRefreshButton(2f));
                }
            }
        }
        
        private async void OnWebshopButtonClicked()
        {
            try
            {
                string userId, userEmail;
                if (AuthenticationManager.Instance?.IsAuthenticated() == true)
                {
                    var userData = AuthenticationManager.Instance.GetUserData();
                    userId = userData.UserId;
                    userEmail = userData.Email;
                }
                else
                {
#if UNITY_EDITOR
                    userEmail = $"editor_{Guid.NewGuid().ToString("N").Substring(0, 8)}@example.com";
                    userId = userEmail;
#else
                    Debug.LogWarning("[StoreAuth] Cannot open webshop - user not authenticated");
                    return;
#endif
                }
                await OpenWebshop(userId, userEmail);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[StoreAuth] Error in webshop button handler: {ex.Message}");
            }
        }

        private void OnHelpButtonClicked()
        {
            if (helpDialog != null)
                helpDialog.style.display = DisplayStyle.Flex;
        }
        
        private void OnCloseHelpButtonClicked()
        {
            if (helpDialog != null)
                helpDialog.style.display = DisplayStyle.None;
        }

        private void OnWatchVideoButtonClicked()
        {
            Application.OpenURL("https://www.youtube.com/watch?v=dQw4w9WgXcQ");
        }

        private void OnHelpDescriptionCloseButtonClicked()
        {
            HideHelpDescription();
            
            string activeTab = GetActiveTab();
            if (!string.IsNullOrEmpty(activeTab))
            {
                PlayerPrefs.SetInt(DemoAppConstants.PREF_HELP_DISMISSED + activeTab, 1);
                PlayerPrefs.Save();
            }
        }
        #endregion

        #region UI Updates
        private void UpdateAuthUI()
        {
            bool isAuthenticated = AuthenticationManager.Instance?.IsAuthenticated() ?? false;
            
            UpdateUserProfile(isAuthenticated);
            UpdateWebshopButton(isAuthenticated);
        }

        private void UpdateUserProfile(bool isAuthenticated)
        {
            // Update login button
            if (loginButton != null)
                loginButton.text = isAuthenticated ? "LOGOUT" : "LOGIN / SIGN UP";
            
            // Update profile status
            if (profileStatus != null)
            {
                if (isAuthenticated)
                {
                    try
                    {
                        var userData = AuthenticationManager.Instance.GetUserData();
                        
                        profileStatus.text = "Logged in";
                        profileStatus.style.color = new Color(0.3f, 0.8f, 0.3f);
                        
                        UpdateProfileFields(userData);
                        UpdateUserAttributes(userData);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[StoreAuth] Error updating profile: {ex.Message}");
                        profileStatus.text = "Error loading profile";
                        profileStatus.style.color = new Color(0.8f, 0.3f, 0.3f);
                    }
                }
                else
                {
                    ResetProfileFields();
                }
            }
            
            // Update refresh button visibility
            if (refreshTokenButton != null)
                refreshTokenButton.style.display = isAuthenticated ? DisplayStyle.Flex : DisplayStyle.None;
        }

         private void UpdateProfileFields(UserData userData)
         {
             if (profileName != null)
                 profileName.text = string.IsNullOrEmpty(userData.Name) ? "User" : userData.Name;
             
             if (userIdValue != null)
                 userIdValue.text = userData.UserId;
             
             if (emailValue != null)
                 emailValue.text = userData.Email;
             
             if (tokenExpiryValue != null)
             {
                 var tokenRemaining = AuthenticationManager.Instance.GetTokenTimeRemaining();
                 string expiryText = tokenRemaining.Hours > 0 
                     ? $"{tokenRemaining.Hours}h {tokenRemaining.Minutes}m {tokenRemaining.Seconds}s"
                     : $"{tokenRemaining.Minutes}m {tokenRemaining.Seconds}s";
                 tokenExpiryValue.text = expiryText;
             }
         }

         private void UpdateUserAttributes(UserData userData)
        {
            if (attributesContainer == null) return;
            
            attributesContainer.Clear();
            
            if (userData.Attributes.Count > 0)
            {
                // Show important attributes first
                string[] priorityAttrs = { "sub", "email", "name", "given_name", "family_name", "exp", "iat" };
                var sortedAttrs = userData.Attributes.OrderBy(a => a.Key).ToList();
                
                // Add priority attributes
                foreach (var key in priorityAttrs)
                {
                    if (userData.Attributes.TryGetValue(key, out string value))
                    {
                        AddAttributeRow(key, FormatAttributeValue(key, value));
                        sortedAttrs.RemoveAll(a => a.Key == key);
                    }
                }
                
                // Add remaining attributes
                foreach (var attr in sortedAttrs)
                {
                    AddAttributeRow(attr.Key, FormatAttributeValue(attr.Key, attr.Value));
                }
            }
        }

        private void ResetProfileFields()
        {
            if (profileStatus != null)
            {
                profileStatus.text = "Not logged in";
                profileStatus.style.color = new Color(0.7f, 0.7f, 0.7f);
            }
            
            if (profileName != null) profileName.text = "User Name";
            if (userIdValue != null) userIdValue.text = "--";
            if (emailValue != null) emailValue.text = "--";
            if (tokenExpiryValue != null) tokenExpiryValue.text = "--";
            
            attributesContainer?.Clear();
        }

        private void UpdateWebshopButton(bool isAuthenticated)
        {
            if (webshopButton == null) return;
            
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
        #endregion

        #region Tab Management
        private void SelectTab(string tabToShow)
        {
            // Selecting default tab
            
            if (!ValidateTabElements()) return;

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
                    Debug.LogError($"[StoreAuth] Unknown tab: {tabToShow}");
                    break;
            }

            // Show help description after a delay
            StartCoroutine(ShowHelpDescriptionWithDelay(tabToShow));
            
            // Force style refresh
            RefreshTabElements();
        }

        private bool ValidateTabElements()
        {
            return userTabButton != null && storeTabButton != null && webshopTabButton != null &&
                   userTabContent != null && storeTabContent != null && webshopTabContent != null;
        }

        private void RefreshTabElements()
        {
            userTabButton?.MarkDirtyRepaint();
            storeTabButton?.MarkDirtyRepaint();
            webshopTabButton?.MarkDirtyRepaint();
            userTabContent?.MarkDirtyRepaint();
            storeTabContent?.MarkDirtyRepaint();
            webshopTabContent?.MarkDirtyRepaint();
        }

        private string GetActiveTab()
        {
            if (userTabButton?.ClassListContains("tab-selected") == true) return "user";
            if (storeTabButton?.ClassListContains("tab-selected") == true) return "store";
            if (webshopTabButton?.ClassListContains("tab-selected") == true) return "webshop";
            return string.Empty;
        }
        #endregion

        #region Help System
        private IEnumerator ShowHelpDescriptionWithDelay(string tabName)
        {
            yield return new WaitForSeconds(0.3f);
            
            // Check if help was dismissed for this tab
            if (PlayerPrefs.GetInt(DemoAppConstants.PREF_HELP_DISMISSED + tabName.ToLower(), 0) == 1)
                yield break;

            if (helpDescriptionDialog != null && helpDescriptionText != null)
            {
                // Set header text
                var headerLabel = helpDescriptionDialog.Q<Label>("help-description-title");
                if (headerLabel != null && tabHeaders.ContainsKey(tabName.ToLower()))
                {
                    headerLabel.text = tabHeaders[tabName.ToLower()];
                }

                // Set description text
                if (tabDescriptions.ContainsKey(tabName.ToLower()))
                {
                    helpDescriptionText.text = tabDescriptions[tabName.ToLower()];
                    helpDescriptionDialog.AddToClassList("visible");
                }
            }
        }

        private void HideHelpDescription()
        {
            helpDescriptionDialog?.RemoveFromClassList("visible");
        }
        #endregion

        #region Helper Methods
        private IEnumerator ReEnableRefreshButton(float delay)
        {
            yield return new WaitForSeconds(delay);
            
            if (refreshTokenButton != null)
            {
                refreshTokenButton.SetEnabled(true);
                refreshTokenButton.text = "Force Refresh";
            }
        }

        private IEnumerator PeriodicProfileUpdate()
        {
            while (true)
            {
                yield return new WaitForSeconds(5f);
                
                // Update profile if authenticated to show token countdown
                if (AuthenticationManager.Instance?.IsAuthenticated() == true)
                {
                    UpdateUserProfile(true);
                }
            }
        }

        private void AddAttributeRow(string name, string value)
        {
            if (string.IsNullOrEmpty(value) || attributesContainer == null)
                return;
            
            var row = new VisualElement();
            row.AddToClassList("attribute-row");
            
            var nameLabel = new Label(FormatAttributeName(name));
            nameLabel.AddToClassList("attribute-name");
            
            var valueLabel = new Label(value);
            valueLabel.AddToClassList("attribute-value");
            
            row.Add(nameLabel);
            row.Add(valueLabel);
            attributesContainer.Add(row);
        }

        private string FormatAttributeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            
            // Handle special cases
            if (name == "sub") return "SUB";
            if (name == "iat") return "IAT";
            
            // Convert to title case
            string result = name.Replace('_', ' ');
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(result);
        }

        private string FormatAttributeValue(string key, string value)
        {
            // Format timestamps
            if ((key == "exp" || key == "iat" || key == "auth_time") && long.TryParse(value, out long timestamp))
            {
                var date = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    .AddSeconds(timestamp)
                    .ToLocalTime();
                return date.ToString("yyyy-MM-dd HH:mm:ss");
            }
            
            return value;
        }

        private async Task OpenWebshop(string userId, string userEmail)
        {
            try
            {
                var request = new WebshopRequest
                {
                    user = new WebshopUser { id = userId, email = userEmail },
                    target = "DEFAULT"
                };

                string jsonPayload = JsonUtility.ToJson(request);

                string endpoint = GetWebshopGenerateUrlEndpoint();
                using (var webRequest = new UnityWebRequest(endpoint, "POST"))
                {
                    byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
                    webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    webRequest.downloadHandler = new DownloadHandlerBuffer();
                    webRequest.SetRequestHeader("Content-Type", "application/json");
                    string apiKey = PlayerPrefs.GetString(DemoAppConstants.PREF_STASH_API_KEY, "");
                    if (string.IsNullOrEmpty(apiKey))
                    {
                        Debug.LogError("[StoreAuth] API key not found. Please configure it in settings.");
                        return;
                    }
                    webRequest.SetRequestHeader("x-stash-api-key", apiKey);

                    var operation = webRequest.SendWebRequest();
                    while (!operation.isDone)
                    {
                        await Task.Yield();
                    }

                    if (webRequest.result == UnityWebRequest.Result.Success)
                    {
                        var response = JsonUtility.FromJson<WebshopResponse>(webRequest.downloadHandler.text);
                        if (!string.IsNullOrEmpty(response.url))
                        {
                            // Opening webshop URL
                            Application.OpenURL(response.url);
                        }
                        else
                        {
                            Debug.LogError("[StoreAuth] Empty URL in webshop response");
                        }
                    }
                    else
                    {
                        Debug.LogError($"[StoreAuth] Webshop request failed: {webRequest.error}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[StoreAuth] Error opening webshop: {ex.Message}");
            }
        }
        #endregion

        #region Data Classes
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
        #endregion
    }
} 