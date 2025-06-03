using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using System.Runtime.Serialization;
using UnityEngine.Networking;
using Stash.Core;
using Stash.Models;
using Stash.Scripts.Core;
using System.Threading.Tasks;
using UnityEngine.UI;
// Add reference to JwtDecoder from global namespace
// using JwtDecoder;  // Not needed since JwtDecoder is in global namespace

public class AuthenticationManager : MonoBehaviour
{
    public static AuthenticationManager Instance { get; private set; }

    // Cognito configuration
    [Header("Cognito Configuration")]
    [SerializeField] private string appClientId = "56mch61rjl2rtkqvqdr87irq6d";
    [SerializeField] private string cognitoDomain = "stashdemo.auth.eu-north-1.amazoncognito.com";
    [SerializeField] private string redirectScheme = "stashdemo://";
    [SerializeField] private string redirectHost = "callback";
    
    // PKCE parameters
    private string _codeVerifier;
    private string _codeChallenge;
    
    // Authentication state
    private bool _isAuthenticated = false;
    private string _accessToken = "";
    private string _idToken = "";
    private string _refreshToken = "";
    private DateTime _tokenExpiryTime = DateTime.MinValue;
    
    // Token refresh
    private bool _refreshInProgress = false;
    private float _tokenRefreshThreshold = 300f; // Refresh token if less than 5 minutes remaining
    private bool _autoRefreshEnabled = true;
    
    // User data
    private UserData _userData = new UserData();
    
    // Keys for storing tokens
    private const string KEY_ACCESS_TOKEN = "AUTH_ACCESS_TOKEN";
    private const string KEY_ID_TOKEN = "AUTH_ID_TOKEN";
    private const string KEY_REFRESH_TOKEN = "AUTH_REFRESH_TOKEN";
    private const string KEY_TOKEN_EXPIRY = "AUTH_TOKEN_EXPIRY";
    private const string KEY_USER_DATA_ID = "AUTH_USER_ID";
    private const string KEY_USER_DATA_EMAIL = "AUTH_USER_EMAIL";
    private const string KEY_USER_DATA_NAME = "AUTH_USER_NAME";
    private const string KEY_USER_ATTRIBUTES_PREFIX = "AUTH_ATTR_";
    
    // Events
    public event Action OnLoginSuccess;
    public event Action<string> OnLoginFailed;
    public event Action OnLogout;

    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        // Validate redirect scheme format
        if (!redirectScheme.EndsWith("://"))
        {
            Debug.LogWarning($"Redirect scheme '{redirectScheme}' is not properly formatted with '://'. Fixing automatically.");
            redirectScheme = $"{redirectScheme.TrimEnd(':')}://";
        }
        
        Debug.Log($"AuthenticationManager initialized with:\n" +
                  $"- App Client ID: {appClientId}\n" +
                  $"- Cognito Domain: {cognitoDomain}\n" +
                  $"- Redirect Scheme: {redirectScheme}\n" +
                  $"- Redirect Host: {redirectHost}");
        
        // Register deep link handler
        Application.deepLinkActivated += OnDeepLinkActivated;
        
        // Check if app was opened via deep link
        if (!string.IsNullOrEmpty(Application.absoluteURL))
        {
            OnDeepLinkActivated(Application.absoluteURL);
        }
        
        // Try to restore authentication state
        TryRestoreAuthenticationState();
    }
    
    private void Update()
    {
        // Check if token needs refreshing
        if (_isAuthenticated && _autoRefreshEnabled && !_refreshInProgress)
        {
            TimeSpan timeRemaining = _tokenExpiryTime - DateTime.Now;
            if (timeRemaining.TotalSeconds < _tokenRefreshThreshold)
            {
                RefreshToken();
            }
        }
    }
    
    /// <summary>
    /// Tries to restore authentication state from persistent storage
    /// </summary>
    private void TryRestoreAuthenticationState()
    {
        Debug.Log("Attempting to restore authentication state");
        
        // Check if we have saved tokens
        if (PlayerPrefs.HasKey(KEY_ACCESS_TOKEN) && 
            PlayerPrefs.HasKey(KEY_ID_TOKEN) && 
            PlayerPrefs.HasKey(KEY_REFRESH_TOKEN) && 
            PlayerPrefs.HasKey(KEY_TOKEN_EXPIRY))
        {
            // Retrieve tokens
            _accessToken = PlayerPrefs.GetString(KEY_ACCESS_TOKEN);
            _idToken = PlayerPrefs.GetString(KEY_ID_TOKEN);
            _refreshToken = PlayerPrefs.GetString(KEY_REFRESH_TOKEN);
            
            // Parse expiry time
            long expiryTicks = Convert.ToInt64(PlayerPrefs.GetString(KEY_TOKEN_EXPIRY, "0"));
            _tokenExpiryTime = new DateTime(expiryTicks);
            
            // Check if token is still valid or nearly expired
            TimeSpan timeRemaining = _tokenExpiryTime - DateTime.Now;
            
            if (timeRemaining.TotalSeconds > 0)
            {
                // Token is still valid
                _isAuthenticated = true;
                
                // Restore user data
                _userData = new UserData
                {
                    UserId = PlayerPrefs.GetString(KEY_USER_DATA_ID, ""),
                    Email = PlayerPrefs.GetString(KEY_USER_DATA_EMAIL, ""),
                    Name = PlayerPrefs.GetString(KEY_USER_DATA_NAME, ""),
                    TokenExpiry = _tokenExpiryTime
                };
                
                // Restore all saved user attributes
                RestoreUserAttributes();
                
                Debug.Log($"Successfully restored authentication state for user: {_userData.Email}");
                Debug.Log($"Token expires in: {timeRemaining:hh\\:mm\\:ss}");
                Debug.Log($"Restored {_userData.Attributes.Count} user attributes");
                
                // If token will expire soon, refresh it
                if (timeRemaining.TotalSeconds < _tokenRefreshThreshold)
                {
                    Debug.Log("Token expiring soon, refreshing...");
                    RefreshToken();
                }
                
                // Invoke login success event
                if (OnLoginSuccess != null)
                {
                    StartCoroutine(InvokeOnMainThread(OnLoginSuccess));
                }
            }
            else
            {
                // Token has expired, try to refresh
                Debug.Log("Token has expired, attempting to refresh");
                RefreshToken();
            }
        }
        else
        {
            Debug.Log("No saved authentication state found");
        }
    }
    
    /// <summary>
    /// Restores all user attributes from PlayerPrefs
    /// </summary>
    private void RestoreUserAttributes()
    {
        // Get all PlayerPrefs keys
        List<string> attributeKeys = new List<string>();
        
        // Find all keys that start with our attribute prefix
        foreach (var key in GetAllPlayerPrefsKeys())
        {
            if (key.StartsWith(KEY_USER_ATTRIBUTES_PREFIX))
            {
                attributeKeys.Add(key);
            }
        }
        
        // Restore each attribute
        foreach (var key in attributeKeys)
        {
            string attrName = key.Substring(KEY_USER_ATTRIBUTES_PREFIX.Length);
            string attrValue = PlayerPrefs.GetString(key, "");
            
            if (!string.IsNullOrEmpty(attrValue))
            {
                _userData.Attributes[attrName] = attrValue;
                Debug.Log($"Restored attribute: {attrName} = {attrValue}");
            }
        }
    }
    
    /// <summary>
    /// Gets all PlayerPrefs keys (workaround since Unity doesn't provide a direct method)
    /// </summary>
    private List<string> GetAllPlayerPrefsKeys()
    {
        // This is a simplified approach that works for our known keys
        List<string> keys = new List<string>();
        
        // Check for standard token keys
        keys.Add(KEY_ACCESS_TOKEN);
        keys.Add(KEY_ID_TOKEN);
        keys.Add(KEY_REFRESH_TOKEN);
        keys.Add(KEY_TOKEN_EXPIRY);
        keys.Add(KEY_USER_DATA_ID);
        keys.Add(KEY_USER_DATA_EMAIL);
        keys.Add(KEY_USER_DATA_NAME);
        
        // Check for standard attribute keys we expect might exist
        string[] commonAttrs = new[] { "sub", "email", "name", "given_name", "family_name", "exp", "iat", "auth_time", "iss", "email_verified" };
        foreach (var attr in commonAttrs)
        {
            string key = KEY_USER_ATTRIBUTES_PREFIX + attr;
            if (PlayerPrefs.HasKey(key))
            {
                keys.Add(key);
            }
        }
        
        return keys;
    }
    
    /// <summary>
    /// Refreshes the access token using the refresh token
    /// </summary>
    private void RefreshToken()
    {
        if (string.IsNullOrEmpty(_refreshToken))
        {
            Debug.LogError("Cannot refresh token: No refresh token available");
            return;
        }
        
        if (_refreshInProgress)
        {
            Debug.Log("Token refresh already in progress");
            return;
        }
        
        _refreshInProgress = true;
        Debug.Log("Starting token refresh");
        
        // Start coroutine to make the token refresh request
        StartCoroutine(RequestTokenRefresh(_refreshToken));
    }
    
    /// <summary>
    /// Makes the token refresh request to Cognito
    /// </summary>
    private IEnumerator RequestTokenRefresh(string refreshToken)
    {
        string tokenEndpoint = $"https://{cognitoDomain}/oauth2/token";
        
        // Create form data for the token request
        WWWForm form = new WWWForm();
        form.AddField("grant_type", "refresh_token");
        form.AddField("client_id", appClientId);
        form.AddField("refresh_token", refreshToken);
        
        // Create and send the request
        using (UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Post(tokenEndpoint, form))
        {
            request.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");
            
            Debug.Log($"Sending token refresh request to {tokenEndpoint}");
            
            // Send the request and wait for response
            yield return request.SendWebRequest();
            
            _refreshInProgress = false;
            
            if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Token refresh request failed: {request.error}\n{request.downloadHandler.text}");
                
                // If refresh fails due to invalid refresh token, log out the user
                if (request.responseCode == 400 || request.responseCode == 401)
                {
                    Debug.LogWarning("Refresh token appears to be invalid or expired. Logging user out.");
                    Logout();
                }
                
                yield break;
            }
            
            // Parse the response
            string responseText = request.downloadHandler.text;
            Debug.Log($"Token refresh response received");
            
            try
            {
                // Parse JSON response using JsonUtility
                TokenResponse tokenResponse = JsonUtility.FromJson<TokenResponse>(responseText);
                
                if (tokenResponse != null)
                {
                    // Update tokens - note that refresh_token might not be included in the refresh response
                    _accessToken = tokenResponse.access_token;
                    _idToken = tokenResponse.id_token;
                    
                    // Calculate new expiry time
                    _tokenExpiryTime = DateTime.Now.AddSeconds(tokenResponse.expires_in);
                    
                    // Mark as authenticated
                    _isAuthenticated = true;
                    
                    // Extract user data from ID token if it was updated
                    if (!string.IsNullOrEmpty(_idToken))
                    {
                        ExtractUserDataFromIdToken(_idToken);
                    }
                    
                    // Save updated tokens
                    SaveAuthenticationState();
                    
                    Debug.Log($"Token refresh successful. New expiry: {_tokenExpiryTime}");
                    
                    // Invoke login success event (as refresh succeeded)
                    if (OnLoginSuccess != null)
                    {
                        StartCoroutine(InvokeOnMainThread(OnLoginSuccess));
                    }
                }
                else
                {
                    Debug.LogError("Failed to parse token refresh response");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error parsing token refresh response: {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// Saves the current authentication state to persistent storage
    /// </summary>
    private void SaveAuthenticationState()
    {
        // Only save if authenticated with valid tokens
        if (!_isAuthenticated || string.IsNullOrEmpty(_accessToken) || 
            string.IsNullOrEmpty(_idToken) || string.IsNullOrEmpty(_refreshToken))
        {
            Debug.LogWarning("Not saving authentication state: Invalid or missing tokens");
            return;
        }
        
        // Save tokens
        PlayerPrefs.SetString(KEY_ACCESS_TOKEN, _accessToken);
        PlayerPrefs.SetString(KEY_ID_TOKEN, _idToken);
        PlayerPrefs.SetString(KEY_REFRESH_TOKEN, _refreshToken);
        PlayerPrefs.SetString(KEY_TOKEN_EXPIRY, _tokenExpiryTime.Ticks.ToString());
        
        // Save user data
        PlayerPrefs.SetString(KEY_USER_DATA_ID, _userData.UserId);
        PlayerPrefs.SetString(KEY_USER_DATA_EMAIL, _userData.Email);
        PlayerPrefs.SetString(KEY_USER_DATA_NAME, _userData.Name);
        
        // Save all user attributes
        foreach (var attribute in _userData.Attributes)
        {
            string key = KEY_USER_ATTRIBUTES_PREFIX + attribute.Key;
            PlayerPrefs.SetString(key, attribute.Value);
        }
        
        // Ensure data is saved
        PlayerPrefs.Save();
        
        Debug.Log($"Authentication state saved successfully with {_userData.Attributes.Count} attributes");
    }
    
    /// <summary>
    /// Clears saved authentication state
    /// </summary>
    private void ClearSavedAuthenticationState()
    {
        PlayerPrefs.DeleteKey(KEY_ACCESS_TOKEN);
        PlayerPrefs.DeleteKey(KEY_ID_TOKEN);
        PlayerPrefs.DeleteKey(KEY_REFRESH_TOKEN);
        PlayerPrefs.DeleteKey(KEY_TOKEN_EXPIRY);
        PlayerPrefs.DeleteKey(KEY_USER_DATA_ID);
        PlayerPrefs.DeleteKey(KEY_USER_DATA_EMAIL);
        PlayerPrefs.DeleteKey(KEY_USER_DATA_NAME);
        
        // Clear all attribute keys
        List<string> attributeKeys = new List<string>();
        foreach (var key in GetAllPlayerPrefsKeys())
        {
            if (key.StartsWith(KEY_USER_ATTRIBUTES_PREFIX))
            {
                PlayerPrefs.DeleteKey(key);
            }
        }
        
        PlayerPrefs.Save();
        
        Debug.Log("Saved authentication state cleared");
    }
    
    /// <summary>
    /// Opens the Cognito Hosted UI for user authentication
    /// </summary>
    public void OpenLoginUI()
    {
        // Generate PKCE code verifier and challenge
        GeneratePKCECodes();
        
        // Construct the Cognito Hosted UI URL
        string redirectUri = redirectScheme + redirectHost;
        Debug.Log($"Using redirect URI: {redirectUri}");
        
        string encodedRedirectUri = UnityEngine.Networking.UnityWebRequest.EscapeURL(redirectUri);
        Debug.Log($"Encoded redirect URI: {encodedRedirectUri}");
        
        string loginUrl = $"https://{cognitoDomain}/login?client_id={appClientId}" +
                          $"&response_type=code" +
                          $"&scope=email+openid+phone" +
                          $"&redirect_uri={encodedRedirectUri}" +
                          $"&code_challenge={_codeChallenge}" +
                          $"&code_challenge_method=S256";
        
        Debug.Log($"Opening login URL: {loginUrl}");
        
        // Open the URL
        Application.OpenURL(loginUrl);
    }
    
    /// <summary>
    /// Generates PKCE code verifier and code challenge
    /// </summary>
    private void GeneratePKCECodes()
    {
        // Generate code verifier - a random string of length between 43 and 128 characters
        _codeVerifier = GenerateRandomString(64);
        
        // Generate code challenge - base64url encoded SHA256 hash of the code verifier
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] challengeBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(_codeVerifier));
            _codeChallenge = Base64UrlEncode(challengeBytes);
        }
        
        // Store the code verifier to use it when exchanging the authorization code for tokens
        PlayerPrefs.SetString("PKCE_CODE_VERIFIER", _codeVerifier);
    }
    
    /// <summary>
    /// Generates a random string using characters A-Z, a-z, 0-9, and "-._~" 
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
    /// Encodes a byte array into a base64url string (URL-safe base64)
    /// </summary>
    private string Base64UrlEncode(byte[] input)
    {
        string base64 = Convert.ToBase64String(input);
        string base64Url = base64
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
        
        return base64Url;
    }
    
    /// <summary>
    /// Handles deep link activation from Cognito redirect or Stash login
    /// </summary>
    /// <param name="url">The deep link URL</param>
    private async void OnDeepLinkActivated(string url)
    {
        Debug.Log($"Deep link activated: {url}");
        
        try
        {
            if (url.Contains("stash/login"))
            {
                int codeIndex = url.IndexOf("code=");
                if (codeIndex != -1)
                {
                    string code = url.Substring(codeIndex + 5);
                    Debug.Log($"Extracted code: {code}");
                    
                    if (!IsAuthenticated())
                    {
                        Debug.LogError("User is not authenticated. Cannot proceed with Stash login.");
                        ShowPopup("Authentication Required", "Please login first to proceed with Stash login.", false);
                        InvokeLoginFailed("User not authenticated");
                        return;
                    }
                    
                    try
                    {
                        // Create the request body using serializable classes
                        var requestBody = new CustomLoginRequest
                        {
                            code = code,
                            user = new CustomLoginUser
                            {
                                id = _userData.UserId,
                                email = _userData.Email
                            }
                        };
                        
                        string jsonBody = JsonUtility.ToJson(requestBody);
                        Debug.Log($"Request body: {jsonBody}");
                        
                        // Create and configure the request
                        using (UnityWebRequest request = new UnityWebRequest("https://test-api.stash.gg/sdk/custom_login/approve", "POST"))
                        {
                            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
                            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                            request.downloadHandler = new DownloadHandlerBuffer();
                            request.SetRequestHeader("Content-Type", "application/json");
                            request.SetRequestHeader("x-stash-api-key", "p0SVSU3awmdDv8VUPFZ_adWz_uC81xXsEY95Gg7WSwx9TZAJ5_ch-ePXK2Xh3B6o");
                            
                            // Send the request
                            var operation = request.SendWebRequest();
                            while (!operation.isDone)
                            {
                                await Task.Yield();
                            }
                            
                            if (request.result == UnityWebRequest.Result.Success)
                            {
                                Debug.Log($"Stash login successful: {request.downloadHandler.text}");
                                ShowPopup("Account Linked", "Account Linked Successfully. Navigate back to the web shop.", true); // Show confetti on success
                                
                                if (OnLoginSuccess != null)
                                {
                                    StartCoroutine(InvokeOnMainThread(OnLoginSuccess));
                                }
                            }
                            else
                            {
                                Debug.LogError($"Stash login failed: {request.error}\n{request.downloadHandler.text}");
                                ShowPopup("Login Failed", $"Stash login failed: {request.error}", false);
                                InvokeLoginFailed($"Stash login failed: {request.error}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Stash login failed: {ex.Message}");
                        ShowPopup("Login Failed", $"Stash login failed: {ex.Message}", false);
                        InvokeLoginFailed($"Stash login failed: {ex.Message}");
                    }
                    return;
                }
            }
            
            // If not a Stash login URL, treat it as a Cognito callback
            HandleCognitoCallback(new Uri(url));
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error processing deep link: {ex.Message}\n{ex.StackTrace}");
            ShowPopup("Error", $"Error processing authentication: {ex.Message}", false);
            InvokeLoginFailed($"Error processing authentication response: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Handles the Cognito callback flow
    /// </summary>
    private void HandleCognitoCallback(Uri uri)
    {
        // Parse query parameters
        string query = uri.Query.TrimStart('?');
        Dictionary<string, string> queryParams = ParseQueryString(query);
        
        // Log all query parameters for debugging
        foreach (var param in queryParams)
        {
            Debug.Log($"Query param: {param.Key} = {param.Value}");
        }
        
        // Check for authorization code (success)
        if (queryParams.TryGetValue("code", out string authCode))
        {
            Debug.Log($"Received authorization code: {authCode}");
            
            // Retrieve the code verifier we stored earlier
            string codeVerifier = PlayerPrefs.GetString("PKCE_CODE_VERIFIER", "");
            
            if (string.IsNullOrEmpty(codeVerifier))
            {
                Debug.LogError("Code verifier not found. Cannot exchange authorization code for tokens.");
                InvokeLoginFailed("Code verifier not found");
                return;
            }
            
            Debug.Log("Code verifier retrieved successfully");
            
            // Exchange auth code for tokens
            ExchangeAuthCodeForTokens(authCode);
        }
        // Check for error
        else if (queryParams.TryGetValue("error", out string error))
        {
            string errorDescription = queryParams.ContainsKey("error_description") 
                ? queryParams["error_description"] 
                : "Unknown error";
            
            Debug.LogError($"Authentication error: {error} - {errorDescription}");
            InvokeLoginFailed(errorDescription);
        }
        else
        {
            Debug.LogError("No authorization code or error found in callback URL");
            InvokeLoginFailed("Invalid callback URL format");
        }
    }
    
    /// <summary>
    /// Exchanges the authorization code for tokens from Cognito
    /// </summary>
    private void ExchangeAuthCodeForTokens(string authCode)
    {
        // Retrieve the code verifier we stored earlier
        string codeVerifier = PlayerPrefs.GetString("PKCE_CODE_VERIFIER", "");
        
        if (string.IsNullOrEmpty(codeVerifier))
        {
            Debug.LogError("Code verifier not found. Cannot exchange authorization code for tokens.");
            InvokeLoginFailed("Code verifier not found");
            return;
        }
        
        Debug.Log("Starting token exchange with Cognito");
        
        // Construct the redirect URI (same as used in the initial request)
        string redirectUri = redirectScheme + redirectHost;
        
        // Start coroutine to make the token request
        StartCoroutine(RequestTokens(authCode, codeVerifier, redirectUri));
    }
    
    /// <summary>
    /// Makes the token request to Cognito
    /// </summary>
    private IEnumerator RequestTokens(string authCode, string codeVerifier, string redirectUri)
    {
        string tokenEndpoint = $"https://{cognitoDomain}/oauth2/token";
        
        // Create form data for the token request
        WWWForm form = new WWWForm();
        form.AddField("grant_type", "authorization_code");
        form.AddField("client_id", appClientId);
        form.AddField("code", authCode);
        form.AddField("redirect_uri", redirectUri);
        form.AddField("code_verifier", codeVerifier);
        
        // Create and send the request
        using (UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Post(tokenEndpoint, form))
        {
            request.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");
            
            Debug.Log($"Sending token request to {tokenEndpoint}");
            
            // Send the request and wait for response
            yield return request.SendWebRequest();
            
            if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Token request failed: {request.error}\n{request.downloadHandler.text}");
                InvokeLoginFailed($"Failed to exchange authorization code: {request.error}");
                yield break;
            }
            
            // Parse the response
            string responseText = request.downloadHandler.text;
            Debug.Log($"Token response received: {responseText}");
            
            try
            {
                // Parse JSON response using JsonUtility
                TokenResponse tokenResponse = JsonUtility.FromJson<TokenResponse>(responseText);
                
                if (tokenResponse != null)
                {
                    // Set tokens and expiry time
                    _accessToken = tokenResponse.access_token;
                    _idToken = tokenResponse.id_token;
                    _refreshToken = tokenResponse.refresh_token;
                    
                    // Calculate expiry time
                    _tokenExpiryTime = DateTime.Now.AddSeconds(tokenResponse.expires_in);
                    
                    _isAuthenticated = true;
                    
                    // Extract user data from ID token
                    ExtractUserDataFromIdToken(_idToken);
                    
                    // Save authentication state for future app launches
                    SaveAuthenticationState();
                    
                    Debug.Log($"Authentication successful for user: {_userData.Email}");
                    
                    // Invoke success event
                    if (OnLoginSuccess != null)
                    {
                        StartCoroutine(InvokeOnMainThread(OnLoginSuccess));
                    }
                }
                else
                {
                    Debug.LogError("Failed to parse token response");
                    InvokeLoginFailed("Invalid token response format");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error parsing token response: {ex.Message}");
                InvokeLoginFailed($"Error processing token response: {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// Extracts user data from the ID token
    /// </summary>
    private void ExtractUserDataFromIdToken(string idToken)
    {
        if (string.IsNullOrEmpty(idToken))
        {
            Debug.LogError("Cannot extract user data from empty ID token");
            return;
        }
        
        try
        {
            // Parse the claims using our JWT decoder
            Dictionary<string, string> claims = DecodeJwtToken(idToken);
            
            // Log the claims
            Debug.Log($"Extracted {claims.Count} claims from ID token");
            foreach (var claim in claims)
            {
                Debug.Log($"  {claim.Key}: {claim.Value}");
            }
            
            // Set user data from claims
            _userData = new UserData();
            
            // Mandatory fields
            if (claims.TryGetValue("sub", out string sub))
                _userData.UserId = sub;
                
            if (claims.TryGetValue("email", out string email))
                _userData.Email = email;
                
            if (claims.TryGetValue("name", out string name))
                _userData.Name = name;
                
            // Set token expiry
            if (claims.TryGetValue("exp", out string expStr) && int.TryParse(expStr, out int exp))
            {
                DateTime expTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(exp);
                _userData.TokenExpiry = expTime.ToLocalTime();
            }
            else
            {
                _userData.TokenExpiry = _tokenExpiryTime;
            }
            
            // Copy all claims to attributes
            foreach (var claim in claims)
            {
                _userData.Attributes[claim.Key] = claim.Value;
            }
            
            Debug.Log($"User data extracted successfully: {_userData}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error extracting user data from ID token: {ex.Message}");
            
            // Fallback to basic user data
            _userData = new UserData
            {
                UserId = "unknown",
                Email = "unknown@example.com",
                Name = "Unknown User",
                TokenExpiry = _tokenExpiryTime
            };
            
            _userData.Attributes["sub"] = "unknown";
            _userData.Attributes["auth_time"] = DateTime.Now.ToString("o");
        }
    }
    
    /// <summary>
    /// Decode a JWT token string into a dictionary of claims
    /// </summary>
    private Dictionary<string, string> DecodeJwtToken(string token)
    {
        Dictionary<string, string> claims = new Dictionary<string, string>();
        
        try
        {
            // JWT tokens have three parts separated by dots: header.payload.signature
            string[] parts = token.Split('.');
            
            if (parts.Length != 3)
            {
                Debug.LogError("Invalid JWT token format");
                return claims;
            }
            
            // Get the payload (second part)
            string payload = parts[1];
            
            // Base64Url decode
            string jsonPayload = Base64UrlDecode(payload);
            Debug.Log($"Decoded JWT payload: {jsonPayload}");
            
            // Parse the JSON
            try
            {
                // Use JsonUtility with a wrapper class since it can't deserialize to Dictionary directly
                JwtPayload jwtPayload = JsonUtility.FromJson<JwtPayload>(jsonPayload);
                
                // Check if we have email, sub, etc.
                if (!string.IsNullOrEmpty(jwtPayload?.email))
                {
                    claims["email"] = jwtPayload.email;
                }
                if (!string.IsNullOrEmpty(jwtPayload?.sub))
                {
                    claims["sub"] = jwtPayload.sub;
                }
                if (!string.IsNullOrEmpty(jwtPayload?.name))
                {
                    claims["name"] = jwtPayload.name;
                }
                if (jwtPayload?.exp > 0)
                {
                    claims["exp"] = jwtPayload.exp.ToString();
                }
                
                Debug.Log($"Successfully decoded JWT token with claims: email={jwtPayload?.email}, sub={jwtPayload?.sub}, name={jwtPayload?.name}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error parsing JWT payload JSON: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error decoding JWT token: {ex.Message}");
        }
        
        return claims;
    }
    
    /// <summary>
    /// Decodes a Base64Url string to a regular string
    /// </summary>
    private string Base64UrlDecode(string input)
    {
        // Convert Base64Url to regular Base64
        string base64 = input
            .Replace('-', '+')
            .Replace('_', '/');
        
        // Add padding if needed
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        
        // Decode Base64 to bytes
        byte[] bytes = Convert.FromBase64String(base64);
        
        // Convert bytes to string
        return Encoding.UTF8.GetString(bytes);
    }
    
    /// <summary>
    /// Helper coroutine to invoke an Action on the main thread
    /// </summary>
    private IEnumerator InvokeOnMainThread(Action action)
    {
        // Wait for end of frame to ensure we're on the main thread
        yield return new WaitForEndOfFrame();
        action.Invoke();
        Debug.Log("Action invoked on main thread via coroutine");
    }
    
    /// <summary>
    /// Helper coroutine to invoke an Action<string> on the main thread
    /// </summary>
    private IEnumerator InvokeOnMainThread<T>(Action<T> action, T parameter)
    {
        // Wait for end of frame to ensure we're on the main thread
        yield return new WaitForEndOfFrame();
        action.Invoke(parameter);
        Debug.Log($"Action<T> invoked on main thread via coroutine with parameter: {parameter}");
    }
    
    /// <summary>
    /// Logs the user out by clearing tokens
    /// </summary>
    public void Logout()
    {
        _isAuthenticated = false;
        _accessToken = "";
        _idToken = "";
        _refreshToken = "";
        _tokenExpiryTime = DateTime.MinValue;
        
        // Clear user data
        _userData = new UserData();
        
        Debug.Log("User logged out");
        
        // Invoke event on main thread
        int subscriberCount = OnLogout?.GetInvocationList()?.Length ?? 0;
        Debug.Log($"Number of subscribers to OnLogout: {subscriberCount}");
        
        if (OnLogout != null)
        {
            // Use coroutine to dispatch to main thread
            StartCoroutine(InvokeOnMainThread(OnLogout));
            Debug.Log("OnLogout event will be invoked on main thread");
        }
        
        // Clear saved authentication state
        ClearSavedAuthenticationState();
    }
    
    /// <summary>
    /// Gets whether the user is authenticated
    /// </summary>
    public bool IsAuthenticated()
    {
        return _isAuthenticated && DateTime.Now < _tokenExpiryTime;
    }
    
    /// <summary>
    /// Gets the access token if authenticated
    /// </summary>
    public string GetAccessToken()
    {
        return IsAuthenticated() ? _accessToken : "";
    }
    
    /// <summary>
    /// Forces a token refresh if user is authenticated
    /// </summary>
    /// <returns>True if refresh initiated, false otherwise</returns>
    public bool ForceRefreshToken()
    {
        if (!IsAuthenticated() || string.IsNullOrEmpty(_refreshToken))
        {
            return false;
        }
        RefreshToken();
        return true;
    }
    
    /// <summary>
    /// Gets the user data if authenticated
    /// </summary>
    public UserData GetUserData()
    {
        return IsAuthenticated() ? _userData : new UserData();
    }
    
    /// <summary>
    /// Gets the time remaining until token expiry
    /// </summary>
    public TimeSpan GetTokenTimeRemaining()
    {
        if (!IsAuthenticated())
        {
            return TimeSpan.Zero;
        }
        
        return _tokenExpiryTime - DateTime.Now;
    }
    
    /// <summary>
    /// Sets whether tokens should be automatically refreshed when nearing expiry
    /// </summary>
    /// <param name="enabled">Whether auto-refresh should be enabled</param>
    public void SetAutoRefresh(bool enabled)
    {
        _autoRefreshEnabled = enabled;
        Debug.Log($"Token auto-refresh {(enabled ? "enabled" : "disabled")}");
    }
    
    /// <summary>
    /// Sets the threshold in seconds before expiry when tokens should be refreshed
    /// </summary>
    /// <param name="seconds">Threshold in seconds</param>
    public void SetRefreshThreshold(float seconds)
    {
        if (seconds < 60)
        {
            Debug.LogWarning($"Refresh threshold of {seconds} seconds is very low. Setting to minimum of 60 seconds.");
            _tokenRefreshThreshold = 60;
        }
        else
        {
            _tokenRefreshThreshold = seconds;
        }
        
        Debug.Log($"Token refresh threshold set to {_tokenRefreshThreshold} seconds");
    }
    
    /// <summary>
    /// Helper method to parse query string parameters
    /// </summary>
    private Dictionary<string, string> ParseQueryString(string query)
    {
        Dictionary<string, string> parameters = new Dictionary<string, string>();
        
        string[] pairs = query.Split('&');
        foreach (string pair in pairs)
        {
            string[] keyValue = pair.Split('=');
            if (keyValue.Length == 2)
            {
                parameters[keyValue[0]] = UnityEngine.Networking.UnityWebRequest.UnEscapeURL(keyValue[1]);
            }
        }
        
        return parameters;
    }
    
    /// <summary>
    /// Helper method to safely invoke the OnLoginFailed event on the main thread
    /// </summary>
    private void InvokeLoginFailed(string errorMessage)
    {
        int subscriberCount = OnLoginFailed?.GetInvocationList()?.Length ?? 0;
        Debug.Log($"Number of subscribers to OnLoginFailed: {subscriberCount}");
        
        if (OnLoginFailed != null)
        {
            // Use coroutine to dispatch to main thread
            StartCoroutine(InvokeOnMainThread(OnLoginFailed, errorMessage));
            Debug.Log($"OnLoginFailed event will be invoked on main thread with message: {errorMessage}");
        }
        else
        {
            Debug.LogWarning("No subscribers to OnLoginFailed event");
        }
    }

    private void ShowPopup(string title, string message, bool withConfetti = false)
    {
        var popup = new GameObject("AuthPopup");
        var popupScript = popup.AddComponent<AuthPopup>();
        popupScript.Show(title, message);
        
        if (withConfetti)
        {
            // Create confetti system that renders in front of UI
            GameObject confettiGO = new GameObject("ConfettiSystem");
            
            // Add Canvas component for UI rendering
            Canvas confettiCanvas = confettiGO.AddComponent<Canvas>();
            confettiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            confettiCanvas.sortingOrder = 1000; // Very high sorting order to render in front of everything
            
            // Add CanvasScaler for consistent scaling
            CanvasScaler scaler = confettiGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            
            // Position the confetti container at the top of the screen
            RectTransform rectTransform = confettiGO.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0, 1); // Top-left anchor
            rectTransform.anchorMax = new Vector2(1, 1); // Top-right anchor
            rectTransform.anchoredPosition = new Vector2(0, 100); // Slightly above screen
            rectTransform.sizeDelta = new Vector2(0, 200); // Full width, 200px height
            
            // Create multiple confetti emitters across the screen width
            for (int i = 0; i < 5; i++)
            {
                GameObject emitter = new GameObject($"ConfettiEmitter_{i}");
                emitter.transform.SetParent(confettiGO.transform, false);
                
                RectTransform emitterRect = emitter.AddComponent<RectTransform>();
                float xPos = (i / 4.0f) - 0.5f; // Spread across screen: -0.5 to 0.5
                emitterRect.anchorMin = new Vector2(0.5f + xPos, 0.5f);
                emitterRect.anchorMax = new Vector2(0.5f + xPos, 0.5f);
                emitterRect.anchoredPosition = Vector2.zero;
                
                ParticleSystem confetti = emitter.AddComponent<ParticleSystem>();
                
                // Configure main module for UI-space confetti
                var main = confetti.main;
                main.startLifetime = 4.0f;
                main.startSpeed = 300.0f; // Higher speed for UI space
                main.startSize = 20.0f; // Larger size for UI space (pixels)
                main.startColor = GetRandomConfettiColor(i);
                main.maxParticles = 40; // Per emitter
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                
                // Configure emission for staggered bursts
                var emission = confetti.emission;
                emission.rateOverTime = 0;
                emission.SetBursts(new ParticleSystem.Burst[] {
                    new ParticleSystem.Burst(i * 0.1f, 20), // Stagger the bursts
                    new ParticleSystem.Burst(i * 0.1f + 0.3f, 25),
                    new ParticleSystem.Burst(i * 0.1f + 0.6f, 15)
                });
                
                // Configure shape for wide spread
                var shape = confetti.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 50; // Spread radius in UI pixels
                
                // Configure velocity for falling effect
                var velocityOverLifetime = confetti.velocityOverLifetime;
                velocityOverLifetime.enabled = true;
                velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
                velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(-400f, -200f); // Downward fall
                velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-100f, 100f); // Side drift
                
                // Configure size over lifetime
                var sizeOverLifetime = confetti.sizeOverLifetime;
                sizeOverLifetime.enabled = true;
                AnimationCurve sizeCurve = new AnimationCurve();
                sizeCurve.AddKey(0, 1.0f);
                sizeCurve.AddKey(0.3f, 1.3f); // Grow slightly
                sizeCurve.AddKey(1, 0.8f); // Shrink at end
                sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1.0f, sizeCurve);
                
                // Configure vibrant colors
                var colorOverLifetime = confetti.colorOverLifetime;
                colorOverLifetime.enabled = true;
                Gradient gradient = new Gradient();
                gradient.SetKeys(
                    new GradientColorKey[] { 
                        new GradientColorKey(GetRandomConfettiColor(i), 0.0f), 
                        new GradientColorKey(GetRandomConfettiColor(i + 1), 0.5f),
                        new GradientColorKey(GetRandomConfettiColor(i + 2), 1.0f)
                    },
                    new GradientAlphaKey[] { 
                        new GradientAlphaKey(1.0f, 0.0f), 
                        new GradientAlphaKey(1.0f, 0.7f),
                        new GradientAlphaKey(0.0f, 1.0f) 
                    }
                );
                colorOverLifetime.color = gradient;
                
                // Configure rotation for spinning effect
                var rotationOverLifetime = confetti.rotationOverLifetime;
                rotationOverLifetime.enabled = true;
                rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-360f, 360f);
                
                // Play each emitter
                confetti.Play();
            }
            
            // Destroy the confetti system after a delay
            Destroy(confettiGO, 5f);
        }
    }

    private Color GetRandomConfettiColor(int seed)
    {
        Color[] colors = {
            Color.yellow,
            Color.magenta,
            Color.cyan,
            Color.green,
            Color.red,
            new Color(1f, 0.5f, 0f), // Orange
            new Color(0.5f, 0f, 1f), // Purple
            new Color(1f, 0.75f, 0.8f) // Pink
        };
        return colors[seed % colors.Length];
    }
}

/// <summary>
/// Class to store user data extracted from tokens
/// </summary>
[System.Serializable]
public class UserData
{
    public string UserId { get; set; } = "";
    public string Email { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime TokenExpiry { get; set; } = DateTime.MinValue;
    public Dictionary<string, string> Attributes { get; private set; } = new Dictionary<string, string>();
    
    public override string ToString()
    {
        return $"UserId: {UserId}, Email: {Email}, Name: {Name}";
    }
}

/// <summary>
/// Class to deserialize the token response from Cognito
/// </summary>
[System.Serializable]
public class TokenResponse
{
    public string access_token;
    public string id_token;
    public string refresh_token;
    public int expires_in;
    public string token_type;
}

/// <summary>
/// Class for deserializing JWT payload
/// </summary>
[System.Serializable]
public class JwtPayload
{
    public string email;
    public string sub;
    public string name;
    public string given_name;
    public string family_name;
    public bool email_verified;
    public string iss;
    public string client_id;
    public string origin_jti;
    public int auth_time;
    public int exp;
    public int iat;
    public string jti;
}

[System.Serializable]
public class CustomLoginUser
{
    public string id;
    public string email;
}

[System.Serializable]
public class CustomLoginRequest
{
    public string code;
    public CustomLoginUser user;
}

// Custom popup class for auth messages
public class AuthPopup : MonoBehaviour
{
    private string title;
    private string message;
    private bool isShowing = false;
    private float showDuration = 3f;
    private GUIStyle titleStyle;
    private GUIStyle messageStyle;
    private GUIStyle containerStyle;
    private Rect windowRect;
    private Texture2D containerTexture;
    
    public void Show(string title, string message)
    {
        this.title = title;
        this.message = message;
        isShowing = true;
        
        // Set up window rect
        float width = Screen.width * 0.8f; // 80% of screen width
        float height = Screen.height * 0.3f; // 30% of screen height
        windowRect = new Rect((Screen.width - width) / 2, (Screen.height - height) / 2, width, height);
        
        // Create container texture with rounded corners
        int cornerRadius = 12;
        containerTexture = new Texture2D(cornerRadius * 2, cornerRadius * 2);
        
        // Create a rounded rectangle texture
        for (int x = 0; x < cornerRadius * 2; x++)
        {
            for (int y = 0; y < cornerRadius * 2; y++)
            {
                float distanceFromCorner = 0f;
                
                // Check which corner we're in
                if (x < cornerRadius && y < cornerRadius) // Top-left
                    distanceFromCorner = Vector2.Distance(new Vector2(x, y), new Vector2(cornerRadius, cornerRadius));
                else if (x >= cornerRadius && y < cornerRadius) // Top-right
                    distanceFromCorner = Vector2.Distance(new Vector2(x, y), new Vector2(cornerRadius, cornerRadius));
                else if (x < cornerRadius && y >= cornerRadius) // Bottom-left
                    distanceFromCorner = Vector2.Distance(new Vector2(x, y), new Vector2(cornerRadius, cornerRadius));
                else // Bottom-right
                    distanceFromCorner = Vector2.Distance(new Vector2(x, y), new Vector2(cornerRadius, cornerRadius));
                
                // Set pixel color based on distance
                if (distanceFromCorner <= cornerRadius)
                {
                    containerTexture.SetPixel(x, y, new Color(0.26f, 0.26f, 0.29f, 0.95f)); // rgb(67, 67, 75) with 95% opacity
                }
                else
                {
                    containerTexture.SetPixel(x, y, Color.clear);
                }
            }
        }
        containerTexture.Apply();
        
        // Set up styles
        titleStyle = new GUIStyle();
        titleStyle.fontSize = Mathf.RoundToInt(height * 0.12f); // 12% of height
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.normal.textColor = Color.white;
        titleStyle.alignment = TextAnchor.MiddleCenter;
        titleStyle.wordWrap = true;
        
        messageStyle = new GUIStyle();
        messageStyle.fontSize = Mathf.RoundToInt(height * 0.08f); // 8% of height
        messageStyle.normal.textColor = new Color(0.78f, 0.78f, 0.78f); // rgb(200, 200, 200)
        messageStyle.alignment = TextAnchor.MiddleCenter;
        messageStyle.wordWrap = true;
        
        containerStyle = new GUIStyle();
        containerStyle.normal.background = containerTexture;
        containerStyle.border = new RectOffset(cornerRadius, cornerRadius, cornerRadius, cornerRadius);
        containerStyle.padding = new RectOffset(cornerRadius, cornerRadius, cornerRadius, cornerRadius);
        
        // Close after duration
        Destroy(gameObject, showDuration);
    }
    
    private void OnGUI()
    {
        if (isShowing)
        {
            // Draw the container
            GUI.Box(windowRect, "", containerStyle);
            
            // Draw the content
            GUILayout.BeginArea(windowRect);
            
            // Center content vertically
            GUILayout.FlexibleSpace();
            
            // Title
            GUILayout.Label(title, titleStyle);
            GUILayout.Space(windowRect.height * 0.05f); // 5% of height spacing
            
            // Message
            GUILayout.Label(message, messageStyle);
            
            // Center content vertically
            GUILayout.FlexibleSpace();
            
            GUILayout.EndArea();
        }
    }
    
    private void OnDestroy()
    {
        if (containerTexture != null)
        {
            Destroy(containerTexture);
        }
    }
} 