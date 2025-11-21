using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Stash.Webshop;
using Stash.Models;
using System.Threading.Tasks;

namespace Stash.Samples
{
    /// <summary>
    /// Manages Cognito authentication flow including login, logout, and token refresh.
    /// Uses TokenManager for token handling and UserDataManager for user data.
    /// Provides a clean authentication interface for the Stash samples.
    /// </summary>
    public class AuthenticationManager : MonoBehaviour
    {
        public static AuthenticationManager Instance { get; private set; }

        #region Configuration
        [Header("Cognito Configuration")]
        [SerializeField] private string appClientId = "56mch61rjl2rtkqvqdr87irq6d";
        [SerializeField] private string cognitoDomain = "stashdemo.auth.eu-north-1.amazoncognito.com";
        [SerializeField] private string redirectScheme = "stashdemo://";
        [SerializeField] private string redirectHost = "callback";
        #endregion

        #region Private Fields
        private TokenManager _tokenManager;
        private UserDataManager _userDataManager;
        
        // PKCE parameters for OAuth flow
        private string _codeVerifier;
        private string _codeChallenge;
        
        // Token refresh state
        private bool _refreshInProgress = false;
        private bool _autoRefreshEnabled = true;
        #endregion

        #region Events
        /// <summary>
        /// Fired when login completes successfully
        /// </summary>
        public event Action OnLoginSuccess;
        
        /// <summary>
        /// Fired when login fails
        /// </summary>
        public event Action<string> OnLoginFailed;
        
        /// <summary>
        /// Fired when user logs out
        /// </summary>
        public event Action OnLogout;
        #endregion

        #region Properties
        /// <summary>
        /// Gets whether the user is currently authenticated
        /// </summary>
        public bool IsAuthenticated() => _tokenManager?.IsTokenValid() ?? false;

        /// <summary>
        /// Gets the current access token if authenticated
        /// </summary>
        public string GetAccessToken() => _tokenManager?.AccessToken ?? "";

        /// <summary>
        /// Gets the current user data if authenticated
        /// </summary>
        public UserData GetUserData() => IsAuthenticated() ? _userDataManager?.CurrentUser ?? new UserData() : new UserData();

        /// <summary>
        /// Gets the time remaining until token expiry
        /// </summary>
        public TimeSpan GetTokenTimeRemaining() => _tokenManager?.TimeUntilExpiry ?? TimeSpan.Zero;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            // Singleton pattern
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeComponents();
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            // Validate and fix redirect scheme format
            if (!redirectScheme.EndsWith("://"))
            {
                redirectScheme = $"{redirectScheme.TrimEnd(':')}://";
            }

            // Register for deep link handling
            Application.deepLinkActivated += OnDeepLinkActivated;

            // Check if app was opened via deep link
            if (!string.IsNullOrEmpty(Application.absoluteURL))
            {
                OnDeepLinkActivated(Application.absoluteURL);
            }

            // Try to restore previous authentication state
            TryRestoreAuthenticationState();
        }

        private void Update()
        {
            // Auto-refresh tokens if enabled and needed
            if (_autoRefreshEnabled && !_refreshInProgress && _tokenManager != null && _tokenManager.ShouldRefreshToken())
            {
                RefreshToken();
            }
        }

        private void OnDestroy()
        {
            Application.deepLinkActivated -= OnDeepLinkActivated;
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Opens the Cognito Hosted UI for user authentication
        /// </summary>
        public void OpenLoginUI()
        {
            // Generate PKCE codes for secure OAuth flow
            GeneratePKCECodes();

            // Construct the Cognito Hosted UI URL
            string redirectUri = redirectScheme + redirectHost;
            string encodedRedirectUri = UnityWebRequest.EscapeURL(redirectUri);

            string loginUrl = $"https://{cognitoDomain}/login?" +
                             $"client_id={appClientId}&" +
                             $"response_type=code&" +
                             $"scope=email+openid+phone&" +
                             $"redirect_uri={encodedRedirectUri}&" +
                             $"code_challenge={_codeChallenge}&" +
                             $"code_challenge_method=S256";

            Application.OpenURL(loginUrl);
        }

        /// <summary>
        /// Logs out the current user
        /// </summary>
        public void Logout()
        {
            // Clear tokens and user data
            _tokenManager?.ClearTokens();
            _tokenManager?.ClearSavedTokens();
            _userDataManager?.ClearUserData();
            _userDataManager?.ClearSavedUserData();

            // Fire logout event
            OnLogout?.Invoke();
        }

        /// <summary>
        /// Forces a token refresh if user is authenticated
        /// </summary>
        /// <returns>True if refresh was initiated</returns>
        public bool ForceRefreshToken()
        {
            if (!IsAuthenticated() || string.IsNullOrEmpty(_tokenManager?.RefreshToken))
            {
                Debug.LogWarning("[Auth] Cannot refresh: not authenticated or no refresh token");
                return false;
            }

            RefreshToken();
            return true;
        }

        /// <summary>
        /// Sets whether tokens should be automatically refreshed when nearing expiry
        /// </summary>
        public void SetAutoRefresh(bool enabled)
        {
            _autoRefreshEnabled = enabled;
        }

        /// <summary>
        /// Sets the threshold in seconds before expiry when tokens should be refreshed
        /// </summary>
        public void SetRefreshThreshold(float seconds)
        {
            _tokenManager?.SetRefreshThreshold(seconds);
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Initializes the component managers
        /// </summary>
        private void InitializeComponents()
        {
            _tokenManager = new TokenManager();
            _userDataManager = new UserDataManager();
        }

        /// <summary>
        /// Tries to restore authentication state from storage
        /// </summary>
        private void TryRestoreAuthenticationState()
        {
            bool tokensLoaded = _tokenManager.LoadTokens();
            bool userDataLoaded = _userDataManager.LoadUserData();

            if (tokensLoaded && userDataLoaded)
            {
                if (_tokenManager.ShouldRefreshToken())
                {
                    RefreshToken();
                }
                else
                {
                    StartCoroutine(InvokeEventOnMainThread(() => OnLoginSuccess?.Invoke()));
                }
            }
        }

        /// <summary>
        /// Generates PKCE code verifier and challenge for OAuth flow
        /// </summary>
        private void GeneratePKCECodes()
        {
            // Generate code verifier (43-128 characters)
            _codeVerifier = GenerateRandomString(64);

            // Generate code challenge (SHA256 hash of verifier, base64url encoded)
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] challengeBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(_codeVerifier));
                _codeChallenge = Base64UrlEncode(challengeBytes);
            }

            // Store verifier for token exchange
            PlayerPrefs.SetString(DemoAppConstants.PREF_PKCE_CODE_VERIFIER, _codeVerifier);
        }

        /// <summary>
        /// Generates a cryptographically random string
        /// </summary>
        private string GenerateRandomString(int length)
        {
            const string allowedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._~";
            char[] result = new char[length];
            byte[] randomBytes = new byte[length];

            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(randomBytes);
            }

            for (int i = 0; i < length; i++)
            {
                result[i] = allowedChars[randomBytes[i] % allowedChars.Length];
            }

            return new string(result);
        }

        /// <summary>
        /// Encodes bytes as base64url (URL-safe base64)
        /// </summary>
        private string Base64UrlEncode(byte[] input)
        {
            return Convert.ToBase64String(input)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }

        /// <summary>
        /// Handles deep link activation from Cognito or Stash
        /// </summary>
        private async void OnDeepLinkActivated(string url)
        {
            try
            {
                // Check if this is a Stash login callback
                if (url.Contains("stash/login"))
                {
                    await HandleStashLoginCallback(url);
                }
                else
                {
                    // Handle Cognito callback
                    HandleCognitoCallback(new Uri(url));
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Auth] Error processing deep link: {ex.Message}");
                ShowPopup("Error", $"Authentication error: {ex.Message}", false);
                OnLoginFailed?.Invoke($"Deep link error: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles Stash custom login callback
        /// </summary>
        private async Task HandleStashLoginCallback(string url)
        {
            // Extract code from URL
            int codeIndex = url.IndexOf("code=");
            if (codeIndex == -1)
            {
                Debug.LogError("[Auth] No code found in Stash login URL");
                OnLoginFailed?.Invoke("Invalid Stash login callback");
                return;
            }

            string code = url.Substring(codeIndex + 5);

            if (!IsAuthenticated())
            {
                Debug.LogError("[Auth] User not authenticated for Stash login");
                ShowPopup("Authentication Required", "Please login first to proceed with Stash login.", false);
                OnLoginFailed?.Invoke("User not authenticated");
                return;
            }

            try
            {
                // Create Stash login request
                var userData = GetUserData();
                var requestBody = new
                {
                    code = code,
                    user = new
                    {
                        id = userData.UserId,
                        email = userData.Email
                    }
                };

                string jsonBody = JsonUtility.ToJson(requestBody);
                
                // Send request to Stash API
                using (UnityWebRequest request = new UnityWebRequest("https://test-api.stash.gg/sdk/custom_login/approve", "POST"))
                {
                    byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                    request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.SetRequestHeader("x-stash-api-key", "p0SVSU3awmdDv8VUPFZ_adWz_uC81xXsEY95Gg7WSwx9TZAJ5_ch-ePXK2Xh3B6o");

                    // Send request
                    var operation = request.SendWebRequest();
                    while (!operation.isDone)
                    {
                        await Task.Yield();
                    }

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        ShowPopup("Account Linked", "Account linked successfully. Navigate back to the web shop.", true);
                        OnLoginSuccess?.Invoke();
                    }
                    else
                    {
                        Debug.LogError($"[Auth] Stash login failed: {request.error}");
                        ShowPopup("Login Failed", $"Stash login failed: {request.error}", false);
                        OnLoginFailed?.Invoke($"Stash login failed: {request.error}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Auth] Stash login exception: {ex.Message}");
                ShowPopup("Login Failed", $"Stash login failed: {ex.Message}", false);
                OnLoginFailed?.Invoke($"Stash login failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles Cognito OAuth callback
        /// </summary>
        private void HandleCognitoCallback(Uri uri)
        {
            // Parse query parameters
            var queryParams = ParseQueryString(uri.Query.TrimStart('?'));

            // Check for authorization code (success)
            if (queryParams.TryGetValue("code", out string authCode))
            {
                // Get stored code verifier
                string codeVerifier = PlayerPrefs.GetString(DemoAppConstants.PREF_PKCE_CODE_VERIFIER, "");
                if (string.IsNullOrEmpty(codeVerifier))
                {
                    Debug.LogError("[Auth] Code verifier not found");
                    OnLoginFailed?.Invoke("Code verifier not found");
                    return;
                }

                // Exchange authorization code for tokens
                StartCoroutine(ExchangeAuthCodeForTokens(authCode, codeVerifier));
            }
            // Check for error
            else if (queryParams.TryGetValue("error", out string error))
            {
                string errorDescription = queryParams.GetValueOrDefault("error_description", "Unknown error");
                Debug.LogError($"[Auth] OAuth error: {error} - {errorDescription}");
                OnLoginFailed?.Invoke(errorDescription);
            }
            else
            {
                Debug.LogError("[Auth] Invalid OAuth callback");
                OnLoginFailed?.Invoke("Invalid callback URL format");
            }
        }

        /// <summary>
        /// Exchanges authorization code for tokens
        /// </summary>
        private IEnumerator ExchangeAuthCodeForTokens(string authCode, string codeVerifier)
        {
            string tokenEndpoint = $"https://{cognitoDomain}/oauth2/token";
            string redirectUri = redirectScheme + redirectHost;

            // Create token request
            WWWForm form = new WWWForm();
            form.AddField("grant_type", "authorization_code");
            form.AddField("client_id", appClientId);
            form.AddField("code", authCode);
            form.AddField("redirect_uri", redirectUri);
            form.AddField("code_verifier", codeVerifier);

            using (UnityWebRequest request = UnityWebRequest.Post(tokenEndpoint, form))
            {
                request.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[Auth] Token exchange failed: {request.error}");
                    OnLoginFailed?.Invoke($"Token exchange failed: {request.error}");
                    yield break;
                }

                // Parse token response
                try
                {
                    var tokenResponse = JsonUtility.FromJson<TokenResponse>(request.downloadHandler.text);
                    
                    // Store tokens
                    _tokenManager.SetTokens(
                        tokenResponse.access_token,
                        tokenResponse.id_token,
                        tokenResponse.refresh_token,
                        tokenResponse.expires_in
                    );

                    // Extract user data from ID token
                    _userDataManager.ExtractUserDataFromIdToken(tokenResponse.id_token, _tokenManager.TokenExpiryTime);

                    // Save to storage
                    _tokenManager.SaveTokens();
                    _userDataManager.SaveUserData();

                    OnLoginSuccess?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Auth] Error processing token response: {ex.Message}");
                    OnLoginFailed?.Invoke($"Token processing error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Refreshes the access token using the refresh token
        /// </summary>
        private void RefreshToken()
        {
            if (_refreshInProgress || string.IsNullOrEmpty(_tokenManager?.RefreshToken))
            {
                return;
            }

            _refreshInProgress = true;
            StartCoroutine(RequestTokenRefresh());
        }

        /// <summary>
        /// Makes the token refresh request
        /// </summary>
        private IEnumerator RequestTokenRefresh()
        {
            string tokenEndpoint = $"https://{cognitoDomain}/oauth2/token";

            WWWForm form = new WWWForm();
            form.AddField("grant_type", "refresh_token");
            form.AddField("client_id", appClientId);
            form.AddField("refresh_token", _tokenManager.RefreshToken);

            using (UnityWebRequest request = UnityWebRequest.Post(tokenEndpoint, form))
            {
                request.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");

                yield return request.SendWebRequest();

                _refreshInProgress = false;

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[Auth] Token refresh failed: {request.error}");
                    
                    // If refresh fails with auth error, logout user
                    if (request.responseCode == 400 || request.responseCode == 401)
                    {
                        Debug.LogWarning("[Auth] Refresh token invalid, logging out");
                        Logout();
                    }
                    yield break;
                }

                // Parse refresh response
                try
                {
                    var tokenResponse = JsonUtility.FromJson<TokenResponse>(request.downloadHandler.text);
                    
                    // Update tokens (refresh token may not be included)
                    _tokenManager.UpdateTokens(
                        tokenResponse.access_token,
                        tokenResponse.id_token,
                        tokenResponse.expires_in
                    );

                    // Update user data if ID token was refreshed
                    if (!string.IsNullOrEmpty(tokenResponse.id_token))
                    {
                        _userDataManager.ExtractUserDataFromIdToken(tokenResponse.id_token, _tokenManager.TokenExpiryTime);
                    }

                    // Save updated state
                    _tokenManager.SaveTokens();
                    _userDataManager.SaveUserData();

                    OnLoginSuccess?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Auth] Error processing refresh response: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Helper to parse URL query parameters
        /// </summary>
        private Dictionary<string, string> ParseQueryString(string query)
        {
            var parameters = new Dictionary<string, string>();
            
            if (string.IsNullOrEmpty(query))
                return parameters;

            string[] pairs = query.Split('&');
            foreach (string pair in pairs)
            {
                string[] keyValue = pair.Split('=');
                if (keyValue.Length == 2)
                {
                    parameters[keyValue[0]] = UnityWebRequest.UnEscapeURL(keyValue[1]);
                }
            }

            return parameters;
        }

        /// <summary>
        /// Helper to invoke events on the main thread
        /// </summary>
        private IEnumerator InvokeEventOnMainThread(Action action)
        {
            yield return new WaitForEndOfFrame();
            action?.Invoke();
        }

        /// <summary>
        /// Shows a popup message to the user
        /// </summary>
        private void ShowPopup(string title, string message, bool withConfetti = false)
        {
            Debug.Log($"[Auth] {title}: {message}");
            // Popup display can be implemented using UI Toolkit if needed
        }
        #endregion
    }

    #region Helper Classes
    /// <summary>
    /// Token response from Cognito OAuth endpoint
    /// </summary>
    [Serializable]
    public class TokenResponse
    {
        public string access_token;
        public string id_token;
        public string refresh_token;
        public int expires_in;
        public string token_type;
    }
    #endregion
} 