using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Stash.Scripts.Core;

namespace Stash.Core
{
    /// <summary>
    /// Analytics and event tracking for Stash SDK
    /// </summary>
    public class StashAnalytics
    {
        private readonly string shopHandle;
        private readonly string eventEndpoint;

        public StashAnalytics(string shopHandle, StashEnvironment environment)
        {
            this.shopHandle = shopHandle;
            this.eventEndpoint = GetEventEndpoint(environment);
        }

        private string GetEventEndpoint(StashEnvironment environment)
        {
            switch (environment)
            {
                case StashEnvironment.Production:
                    return "https://api.stash.gg/sdk/event/log_event";
                case StashEnvironment.Test:
                    return "https://test-api.stash.gg/sdk/event/log_event";
                default:
                    return "https://test-api.stash.gg/sdk/event/log_event";
            }
        }

        /// <summary>
        /// Track a generic click event with device and build information
        /// </summary>
        /// <param name="elementId">ID of the clicked element (required)</param>
        /// <param name="screenName">Name of the current screen (optional)</param>
        /// <param name="additionalParams">Additional custom parameters (optional)</param>
        public IEnumerator TrackClickEvent(
            string elementId,
            string screenName = null,
            Dictionary<string, object> additionalParams = null)
        {
            if (string.IsNullOrEmpty(elementId))
            {
                Debug.LogWarning("[Stash Analytics] elementId is required");
                yield break;
            }

            var eventData = BuildClickEventData(elementId, screenName, additionalParams);
            yield return SendEvent("GENERIC_CLICK", eventData);  // Event type: GENERIC_CLICK
        }

        private Dictionary<string, object> BuildClickEventData(
            string elementId,
            string screenName,
            Dictionary<string, object> additionalParams)
        {
            var eventData = new Dictionary<string, object>
            {
                // Event identification
                { "clickId", Guid.NewGuid().ToString() },
                { "elementId", elementId },

                // Device information
                { "deviceModel", SystemInfo.deviceModel },
                { "deviceType", SystemInfo.deviceType.ToString() },
                { "operatingSystem", SystemInfo.operatingSystem },
                { "platform", Application.platform.ToString() },

                // Build information
                { "gameBuildVersion", Application.version },
                { "unityVersion", Application.unityVersion },

                // Timestamp
                { "clientTimestamp", DateTime.UtcNow.ToString("o") }
            };

            // Add screen name if provided
            if (!string.IsNullOrEmpty(screenName))
            {
                eventData["screenName"] = screenName;
            }

            // Merge additional parameters
            if (additionalParams != null)
            {
                foreach (var kvp in additionalParams)
                {
                    if (!eventData.ContainsKey(kvp.Key))
                    {
                        eventData[kvp.Key] = kvp.Value;
                    }
                }
            }

            return eventData;
        }

        private IEnumerator SendEvent(string eventName, Dictionary<string, object> eventParams)
        {
            // Build request body matching SDK event API structure
            // POST /sdk/event/log_event
            var requestBody = new EventRequest
            {
                shopHandle = this.shopHandle,
                eventData = new EventData
                {
                    name = eventName,  // e.g., "GENERIC_CLICK"
                    parameters = ConvertDictionaryToJson(eventParams)
                }
            };

            string jsonData = JsonUtility.ToJson(requestBody);
            
            // Replace "parameters" with "params" and "eventData" with "event" in JSON
            jsonData = jsonData.Replace("\"eventData\":", "\"event\":");
            jsonData = jsonData.Replace("\"parameters\":", "\"params\":");

            using (UnityWebRequest request = UnityWebRequest.Post(eventEndpoint, ""))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[Stash Analytics] Failed to send event: {request.error}");
                }
                else
                {
                    Debug.Log($"[Stash Analytics] Event '{eventName}' sent successfully");
                }
            }
        }

        private string ConvertDictionaryToJson(Dictionary<string, object> dict)
        {
            var items = new List<string>();
            foreach (var kvp in dict)
            {
                string value;
                if (kvp.Value is string)
                {
                    value = $"\"{EscapeJson(kvp.Value.ToString())}\"";
                }
                else if (kvp.Value is bool)
                {
                    value = kvp.Value.ToString().ToLower();
                }
                else if (kvp.Value == null)
                {
                    value = "null";
                }
                else
                {
                    value = kvp.Value.ToString();
                }
                items.Add($"\"{kvp.Key}\":{value}");
            }
            return "{" + string.Join(",", items) + "}";
        }

        private string EscapeJson(string str)
        {
            return str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }

        [Serializable]
        private class EventRequest
        {
            public string shopHandle;
            public EventData eventData;
        }

        [Serializable]
        private class EventData
        {
            public string name;
            public string parameters;
        }
    }
}

