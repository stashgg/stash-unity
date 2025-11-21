using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Stash.Models;
using UnityEngine;
using UnityEngine.Networking;

namespace Stash.Webshop
{
    /// <summary>
    /// Provides HTTP client functionality for making REST API requests to Stash services.
    /// </summary>
    public static class RestClient
    {
        /// <summary>
        /// Sends a GET request to the specified Stash API endpoint with optional headers and returns a Response object.
        /// </summary>
        /// <param name="url">The URL to send the GET request to.</param>
        /// <param name="headers">Optional headers to include in the request.</param>
        /// <returns>A Response object containing the result of the request.</returns>
        public static async Task<Response> Get(string url, IEnumerable<RequestHeader> headers = null)
        {
            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentException("URL cannot be null or empty", nameof(url));
            }

            using UnityWebRequest webRequest = UnityWebRequest.Get(url);
            ApplyHeaders(webRequest, headers);

            var operation = webRequest.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            return CreateResponse(webRequest);
        }


        /// <summary>
        /// Sends a POST request to the specified Stash API endpoint with the provided body and optional headers.
        /// </summary>
        /// <param name="url">The URL to send the POST request to.</param>
        /// <param name="body">The body of the POST request as a JSON string.</param>
        /// <param name="headers">Optional headers to include in the request.</param>
        /// <returns>A Response object with the result of the POST request.</returns>
        public static async Task<Response> Post(string url, string body, IEnumerable<RequestHeader> headers = null)
        {
            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentException("URL cannot be null or empty", nameof(url));
            }

            if (body == null)
            {
                throw new ArgumentNullException(nameof(body));
            }

            byte[] bodyPayload = Encoding.UTF8.GetBytes(body);
            using UnityWebRequest webRequest = new(url, "POST");

            ApplyHeaders(webRequest, headers);
            ApplyStashHeaders(webRequest);
            ApplyAnalyticsHeaders(webRequest);

            webRequest.uploadHandler = new UploadHandlerRaw(bodyPayload);
            webRequest.downloadHandler = new DownloadHandlerBuffer();

            var operation = webRequest.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            // Log network errors for debugging
            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[STASH] Request failed: {webRequest.result}, URL: {url}, Error: {webRequest.error}");
            }

            return CreateResponse(webRequest, includeDataOnError: true);
        }

        /// <summary>
        /// Applies custom headers to the web request.
        /// </summary>
        private static void ApplyHeaders(UnityWebRequest webRequest, IEnumerable<RequestHeader> headers)
        {
            if (headers == null) return;

            foreach (RequestHeader header in headers)
            {
                if (header != null && !string.IsNullOrEmpty(header.Key))
                {
                    webRequest.SetRequestHeader(header.Key, header.Value ?? string.Empty);
                }
            }
        }

        /// <summary>
        /// Applies Stash-specific headers including launcher headers and default content headers.
        /// </summary>
        private static void ApplyStashHeaders(UnityWebRequest webRequest)
        {
            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.SetRequestHeader("Accept", "application/json");

#if UNITY_EDITOR || UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX
            string launcherMachineId = Environment.GetEnvironmentVariable("STASH_MID");
            if (!string.IsNullOrEmpty(launcherMachineId))
            {
                Debug.Log($"[STASH] Launcher Instance Set: {launcherMachineId}");
                webRequest.SetRequestHeader("x-stash-grpc-mid", launcherMachineId);
            }
#endif
        }

        /// <summary>
        /// Applies analytics headers for SDK tracking and diagnostics.
        /// </summary>
        private static void ApplyAnalyticsHeaders(UnityWebRequest webRequest)
        {
            try
            {
                webRequest.SetRequestHeader("x-stash-unity-sdk-version", StashConstants.SdkVersion);
                webRequest.SetRequestHeader("x-stash-unity-platform", Application.platform.ToString());
                webRequest.SetRequestHeader("x-stash-unity-runtime", Application.unityVersion);
                webRequest.SetRequestHeader("x-stash-unity-build-guid", Application.buildGUID);
                webRequest.SetRequestHeader("x-stash-unity-app-version", Application.version);
                webRequest.SetRequestHeader("x-stash-unity-device-os", SystemInfo.operatingSystem);
                webRequest.SetRequestHeader("x-stash-unity-device-model", SystemInfo.deviceModel);
                webRequest.SetRequestHeader("x-stash-unity-device-type", SystemInfo.deviceType.ToString());
                webRequest.SetRequestHeader("x-stash-unity-editor", Application.isEditor.ToString());
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[STASH] Failed to set analytics headers: {e.Message}");
            }
        }

        /// <summary>
        /// Creates a Response object from the UnityWebRequest result.
        /// </summary>
        private static Response CreateResponse(UnityWebRequest webRequest, bool includeDataOnError = false)
        {
            var response = new Response
            {
                StatusCode = webRequest.responseCode
            };

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                response.Data = webRequest.downloadHandler?.text;
            }
            else
            {
                // Build comprehensive error message
                string errorMessage = webRequest.error;
                
                // If status code is 0, it means no HTTP response was received (network error)
                if (webRequest.responseCode == 0)
                {
                    if (string.IsNullOrEmpty(errorMessage))
                    {
                        errorMessage = "Network error: Unable to connect to server. Please check your internet connection.";
                    }
                    else
                    {
                        errorMessage = $"Network error: {errorMessage}";
                    }
                }
                else if (!string.IsNullOrEmpty(errorMessage))
                {
                    errorMessage = $"HTTP {webRequest.responseCode}: {errorMessage}";
                }
                else
                {
                    errorMessage = $"HTTP {webRequest.responseCode}: Request failed";
                }

                response.Error = errorMessage;
                
                if (includeDataOnError)
                {
                    response.Data = webRequest.downloadHandler?.text;
                }
            }

            return response;
        }
    }
}