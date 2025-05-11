using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Utility class for decoding JWT tokens
/// </summary>
public static class JwtDecoder
{
    /// <summary>
    /// Decodes a JWT token and returns the payload as a dictionary
    /// </summary>
    /// <param name="token">The JWT token to decode</param>
    /// <returns>Dictionary containing the token claims</returns>
    public static Dictionary<string, string> DecodeToken(string token)
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
            
            // Parse JSON into dictionary
            ParseJsonToDictionary(jsonPayload, claims);
            
            Debug.Log($"Successfully decoded JWT token with {claims.Count} claims");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error decoding JWT token: {ex.Message}");
        }
        
        return claims;
    }
    
    /// <summary>
    /// Decodes Base64Url encoded string to regular string
    /// </summary>
    private static string Base64UrlDecode(string input)
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
    /// Simple JSON parser to extract key-value pairs
    /// For a real implementation, use a proper JSON library
    /// </summary>
    private static void ParseJsonToDictionary(string json, Dictionary<string, string> result)
    {
        // For a production app, use a proper JSON parser
        // This is a simplified parser for demonstration purposes
        
        try
        {
            // Remove outer brackets
            json = json.Trim();
            if (json.StartsWith("{")) json = json.Substring(1);
            if (json.EndsWith("}")) json = json.Substring(0, json.Length - 1);
            
            bool inQuotes = false;
            bool inKey = true;
            string currentKey = "";
            string currentValue = "";
            char lastChar = '\0';
            
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                
                // Handle quotes
                if (c == '"' && lastChar != '\\')
                {
                    inQuotes = !inQuotes;
                    continue;
                }
                
                // Handle separators outside quotes
                if (!inQuotes)
                {
                    // Key-value separator
                    if (c == ':')
                    {
                        inKey = false;
                        continue;
                    }
                    
                    // Pair separator
                    if (c == ',')
                    {
                        AddKeyValuePair(result, currentKey.Trim(), currentValue.Trim());
                        currentKey = "";
                        currentValue = "";
                        inKey = true;
                        continue;
                    }
                    
                    // Skip whitespace outside quotes
                    if (char.IsWhiteSpace(c))
                    {
                        continue;
                    }
                }
                
                // Add character to current key or value
                if (inKey)
                {
                    currentKey += c;
                }
                else
                {
                    currentValue += c;
                }
                
                lastChar = c;
            }
            
            // Add the last pair
            if (!string.IsNullOrEmpty(currentKey))
            {
                AddKeyValuePair(result, currentKey.Trim(), currentValue.Trim());
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error parsing JSON: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Helper to clean up and add key-value pairs to the dictionary
    /// </summary>
    private static void AddKeyValuePair(Dictionary<string, string> dict, string key, string value)
    {
        // Clean up the key and value
        key = CleanJsonString(key);
        value = CleanJsonString(value);
        
        // Add to dictionary if not empty
        if (!string.IsNullOrEmpty(key) && !dict.ContainsKey(key))
        {
            dict[key] = value;
        }
    }
    
    /// <summary>
    /// Helper to clean up JSON strings
    /// </summary>
    private static string CleanJsonString(string input)
    {
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
} 