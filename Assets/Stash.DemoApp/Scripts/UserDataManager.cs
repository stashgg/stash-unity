using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Stash.Samples
{
    /// <summary>
    /// Class to store user data extracted from authentication tokens
    /// </summary>
    [Serializable]
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
    /// Manages user data including extraction from JWT tokens and persistence.
    /// Handles user attributes and provides utilities for token parsing.
    /// </summary>
    public class UserDataManager
    {
        #region Constants
        private const string KEY_USER_DATA_ID = "AUTH_USER_ID";
        private const string KEY_USER_DATA_EMAIL = "AUTH_USER_EMAIL";
        private const string KEY_USER_DATA_NAME = "AUTH_USER_NAME";
        private const string KEY_USER_ATTRIBUTES_PREFIX = "AUTH_ATTR_";
        #endregion

        #region Private Fields
        private UserData _userData = new UserData();
        #endregion

        #region Properties
        /// <summary>
        /// Gets the current user data
        /// </summary>
        public UserData CurrentUser => _userData;

        /// <summary>
        /// Checks if user data is available
        /// </summary>
        public bool HasUserData => !string.IsNullOrEmpty(_userData.UserId);
        #endregion

        #region Public Methods
        /// <summary>
        /// Extracts user data from an ID token
        /// </summary>
        /// <param name="idToken">The JWT ID token to parse</param>
        /// <param name="tokenExpiry">Token expiry time</param>
        public void ExtractUserDataFromIdToken(string idToken, DateTime tokenExpiry)
        {
            if (string.IsNullOrEmpty(idToken))
            {
                Debug.LogError("[UserDataManager] Cannot extract user data from empty ID token");
                return;
            }

            try
            {
                Dictionary<string, string> claims = DecodeJwtToken(idToken);

                // Create new user data
                _userData = new UserData
                {
                    TokenExpiry = tokenExpiry
                };

                // Set basic fields
                if (claims.TryGetValue("sub", out string sub))
                    _userData.UserId = sub;

                if (claims.TryGetValue("email", out string email))
                    _userData.Email = email;

                if (claims.TryGetValue("name", out string name))
                    _userData.Name = name;

                // Copy all claims to attributes
                foreach (var claim in claims)
                {
                    _userData.Attributes[claim.Key] = claim.Value;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UserDataManager] Error extracting user data: {ex.Message}");
                
                // Fallback to basic user data
                _userData = new UserData
                {
                    UserId = "unknown",
                    Email = "unknown@example.com",
                    Name = "Unknown User",
                    TokenExpiry = tokenExpiry
                };
                
                _userData.Attributes["sub"] = "unknown";
                _userData.Attributes["auth_time"] = DateTime.Now.ToString("o");
            }
        }

        /// <summary>
        /// Clears user data
        /// </summary>
        public void ClearUserData()
        {
            _userData = new UserData();
        }

        /// <summary>
        /// Saves user data to persistent storage
        /// </summary>
        public void SaveUserData()
        {
            if (!HasUserData)
            {
                Debug.LogWarning("[UserDataManager] No user data to save");
                return;
            }

            PlayerPrefs.SetString(KEY_USER_DATA_ID, _userData.UserId);
            PlayerPrefs.SetString(KEY_USER_DATA_EMAIL, _userData.Email);
            PlayerPrefs.SetString(KEY_USER_DATA_NAME, _userData.Name);

            // Save all user attributes
            foreach (var attribute in _userData.Attributes)
            {
                string key = KEY_USER_ATTRIBUTES_PREFIX + attribute.Key;
                PlayerPrefs.SetString(key, attribute.Value);
            }

            PlayerPrefs.Save();
        }

        /// <summary>
        /// Loads user data from persistent storage
        /// </summary>
        /// <returns>True if user data was loaded successfully</returns>
        public bool LoadUserData()
        {
            if (!PlayerPrefs.HasKey(KEY_USER_DATA_ID))
            {
                return false;
            }

            _userData = new UserData
            {
                UserId = PlayerPrefs.GetString(KEY_USER_DATA_ID, ""),
                Email = PlayerPrefs.GetString(KEY_USER_DATA_EMAIL, ""),
                Name = PlayerPrefs.GetString(KEY_USER_DATA_NAME, "")
            };

            // Restore all saved user attributes
            RestoreUserAttributes();

            return HasUserData;
        }

        /// <summary>
        /// Clears saved user data from persistent storage
        /// </summary>
        public void ClearSavedUserData()
        {
            PlayerPrefs.DeleteKey(KEY_USER_DATA_ID);
            PlayerPrefs.DeleteKey(KEY_USER_DATA_EMAIL);
            PlayerPrefs.DeleteKey(KEY_USER_DATA_NAME);

            // Clear all attribute keys
            ClearSavedAttributes();

            PlayerPrefs.Save();
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Decodes a JWT token into a dictionary of claims
        /// </summary>
        private Dictionary<string, string> DecodeJwtToken(string token)
        {
            Dictionary<string, string> claims = new Dictionary<string, string>();

            try
            {
                string[] parts = token.Split('.');
                if (parts.Length != 3)
                {
                    Debug.LogError("[UserDataManager] Invalid JWT token format");
                    return claims;
                }

                // Get the payload (second part) and decode it
                string payload = parts[1];
                string jsonPayload = Base64UrlDecode(payload);

                // Parse the JSON using a simple approach since Unity JsonUtility is limited
                ParseJwtPayload(jsonPayload, claims);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UserDataManager] Error decoding JWT token: {ex.Message}");
            }

            return claims;
        }

        /// <summary>
        /// Parses JWT payload JSON into claims dictionary
        /// </summary>
        private void ParseJwtPayload(string jsonPayload, Dictionary<string, string> claims)
        {
            try
            {
                // Remove outer braces
                string content = jsonPayload.Trim();
                if (content.StartsWith("{")) content = content.Substring(1);
                if (content.EndsWith("}")) content = content.Substring(0, content.Length - 1);

                // Split by commas (simple parsing)
                string[] pairs = content.Split(',');
                
                foreach (string pair in pairs)
                {
                    string[] keyValue = pair.Split(':');
                    if (keyValue.Length == 2)
                    {
                        string key = CleanJsonString(keyValue[0]);
                        string value = CleanJsonString(keyValue[1]);
                        
                        if (!string.IsNullOrEmpty(key))
                        {
                            claims[key] = value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UserDataManager] Error parsing JWT payload: {ex.Message}");
            }
        }

        /// <summary>
        /// Cleans JSON strings by removing quotes and unescaping
        /// </summary>
        private string CleanJsonString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            input = input.Trim();

            // Remove surrounding quotes
            if (input.StartsWith("\"") && input.EndsWith("\""))
            {
                input = input.Substring(1, input.Length - 2);
            }

            // Unescape JSON
            input = input
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\")
                .Replace("\\n", "\n")
                .Replace("\\r", "\r")
                .Replace("\\t", "\t")
                .Replace("\\/", "/");

            return input;
        }

        /// <summary>
        /// Base64Url decodes a string
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

            // Decode and convert to string
            byte[] bytes = Convert.FromBase64String(base64);
            return Encoding.UTF8.GetString(bytes);
        }

        /// <summary>
        /// Restores all user attributes from PlayerPrefs
        /// </summary>
        private void RestoreUserAttributes()
        {
            // Common attributes to check for
            string[] commonAttrs = { "sub", "email", "name", "given_name", "family_name", "exp", "iat", "auth_time", "iss", "email_verified" };
            
            foreach (string attr in commonAttrs)
            {
                string key = KEY_USER_ATTRIBUTES_PREFIX + attr;
                if (PlayerPrefs.HasKey(key))
                {
                    string value = PlayerPrefs.GetString(key, "");
                    if (!string.IsNullOrEmpty(value))
                    {
                        _userData.Attributes[attr] = value;
                    }
                }
            }

            Debug.Log($"[UserDataManager] Restored {_userData.Attributes.Count} user attributes");
        }

        /// <summary>
        /// Clears saved attributes from PlayerPrefs
        /// </summary>
        private void ClearSavedAttributes()
        {
            string[] commonAttrs = { "sub", "email", "name", "given_name", "family_name", "exp", "iat", "auth_time", "iss", "email_verified" };
            
            foreach (string attr in commonAttrs)
            {
                string key = KEY_USER_ATTRIBUTES_PREFIX + attr;
                if (PlayerPrefs.HasKey(key))
                {
                    PlayerPrefs.DeleteKey(key);
                }
            }
        }
        #endregion
    }
} 