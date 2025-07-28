using System;
using UnityEngine;

namespace Stash.Samples
{
    /// <summary>
    /// Manages authentication tokens including storage, retrieval, and refresh operations.
    /// Handles token persistence across app sessions and automatic refresh logic.
    /// </summary>
    public class TokenManager
    {
        #region Constants
        private const string KEY_ACCESS_TOKEN = "AUTH_ACCESS_TOKEN";
        private const string KEY_ID_TOKEN = "AUTH_ID_TOKEN";
        private const string KEY_REFRESH_TOKEN = "AUTH_REFRESH_TOKEN";
        private const string KEY_TOKEN_EXPIRY = "AUTH_TOKEN_EXPIRY";
        #endregion

        #region Private Fields
        private string _accessToken = "";
        private string _idToken = "";
        private string _refreshToken = "";
        private DateTime _tokenExpiryTime = DateTime.MinValue;
        private float _tokenRefreshThreshold = 300f; // 5 minutes default
        #endregion

        #region Properties
        /// <summary>
        /// Gets the current access token if valid
        /// </summary>
        public string AccessToken => IsTokenValid() ? _accessToken : "";

        /// <summary>
        /// Gets the current ID token if valid
        /// </summary>
        public string IdToken => IsTokenValid() ? _idToken : "";

        /// <summary>
        /// Gets the current refresh token
        /// </summary>
        public string RefreshToken => _refreshToken;

        /// <summary>
        /// Gets the token expiry time
        /// </summary>
        public DateTime TokenExpiryTime => _tokenExpiryTime;

        /// <summary>
        /// Gets or sets the token refresh threshold in seconds
        /// </summary>
        public float RefreshThreshold
        {
            get => _tokenRefreshThreshold;
            set => _tokenRefreshThreshold = Math.Max(60f, value); // Minimum 1 minute
        }

        /// <summary>
        /// Checks if tokens are currently valid (not expired)
        /// </summary>
        public bool IsTokenValid() => DateTime.Now < _tokenExpiryTime && !string.IsNullOrEmpty(_accessToken);

        /// <summary>
        /// Checks if tokens need to be refreshed soon
        /// </summary>
        public bool ShouldRefreshToken()
        {
            if (!IsTokenValid() || string.IsNullOrEmpty(_refreshToken))
                return false;

            TimeSpan timeRemaining = _tokenExpiryTime - DateTime.Now;
            return timeRemaining.TotalSeconds < _tokenRefreshThreshold;
        }

        /// <summary>
        /// Gets the time remaining until token expiry
        /// </summary>
        public TimeSpan TimeUntilExpiry => IsTokenValid() ? _tokenExpiryTime - DateTime.Now : TimeSpan.Zero;
        #endregion

        #region Public Methods
        /// <summary>
        /// Sets the authentication tokens
        /// </summary>
        /// <param name="accessToken">Access token</param>
        /// <param name="idToken">ID token</param>
        /// <param name="refreshToken">Refresh token</param>
        /// <param name="expiresInSeconds">Token lifetime in seconds</param>
        public void SetTokens(string accessToken, string idToken, string refreshToken, int expiresInSeconds)
        {
            _accessToken = accessToken ?? "";
            _idToken = idToken ?? "";
            _refreshToken = refreshToken ?? "";
            _tokenExpiryTime = DateTime.Now.AddSeconds(expiresInSeconds);

            // Tokens set successfully
        }

        /// <summary>
        /// Updates access and ID tokens (used during refresh)
        /// </summary>
        /// <param name="accessToken">New access token</param>
        /// <param name="idToken">New ID token</param>
        /// <param name="expiresInSeconds">Token lifetime in seconds</param>
        public void UpdateTokens(string accessToken, string idToken, int expiresInSeconds)
        {
            _accessToken = accessToken ?? "";
            _idToken = idToken ?? "";
            _tokenExpiryTime = DateTime.Now.AddSeconds(expiresInSeconds);

            // Tokens updated successfully
        }

        /// <summary>
        /// Clears all tokens
        /// </summary>
        public void ClearTokens()
        {
            _accessToken = "";
            _idToken = "";
            _refreshToken = "";
            _tokenExpiryTime = DateTime.MinValue;

            // All tokens cleared
        }

        /// <summary>
        /// Saves tokens to persistent storage
        /// </summary>
        public void SaveTokens()
        {
            if (string.IsNullOrEmpty(_accessToken) || string.IsNullOrEmpty(_idToken))
            {
                Debug.LogWarning("[TokenManager] Cannot save incomplete token set");
                return;
            }

            PlayerPrefs.SetString(KEY_ACCESS_TOKEN, _accessToken);
            PlayerPrefs.SetString(KEY_ID_TOKEN, _idToken);
            PlayerPrefs.SetString(KEY_REFRESH_TOKEN, _refreshToken);
            PlayerPrefs.SetString(KEY_TOKEN_EXPIRY, _tokenExpiryTime.Ticks.ToString());
            PlayerPrefs.Save();

            // Tokens saved to persistent storage
        }

        /// <summary>
        /// Loads tokens from persistent storage
        /// </summary>
        /// <returns>True if tokens were loaded successfully and are still valid</returns>
        public bool LoadTokens()
        {
            if (!PlayerPrefs.HasKey(KEY_ACCESS_TOKEN) || !PlayerPrefs.HasKey(KEY_ID_TOKEN) ||
                !PlayerPrefs.HasKey(KEY_REFRESH_TOKEN) || !PlayerPrefs.HasKey(KEY_TOKEN_EXPIRY))
            {
                // No saved tokens found
                return false;
            }

            _accessToken = PlayerPrefs.GetString(KEY_ACCESS_TOKEN);
            _idToken = PlayerPrefs.GetString(KEY_ID_TOKEN);
            _refreshToken = PlayerPrefs.GetString(KEY_REFRESH_TOKEN);

            // Parse expiry time
            if (long.TryParse(PlayerPrefs.GetString(KEY_TOKEN_EXPIRY, "0"), out long expiryTicks))
            {
                _tokenExpiryTime = new DateTime(expiryTicks);
            }
            else
            {
                Debug.LogError("[TokenManager] Failed to parse token expiry time");
                ClearTokens();
                return false;
            }

            bool isValid = IsTokenValid();
            // Tokens loaded from storage

            return isValid;
        }

        /// <summary>
        /// Clears saved tokens from persistent storage
        /// </summary>
        public void ClearSavedTokens()
        {
            PlayerPrefs.DeleteKey(KEY_ACCESS_TOKEN);
            PlayerPrefs.DeleteKey(KEY_ID_TOKEN);
            PlayerPrefs.DeleteKey(KEY_REFRESH_TOKEN);
            PlayerPrefs.DeleteKey(KEY_TOKEN_EXPIRY);
            PlayerPrefs.Save();

            // Saved tokens cleared from storage
        }
        #endregion
    }
} 