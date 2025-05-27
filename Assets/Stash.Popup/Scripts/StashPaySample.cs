using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;
using System;
using Stash.Core;
using Stash.Scripts.Core;
using System.Threading.Tasks;

namespace StashPopup
{
    /// <summary>
    /// Example component for opening URLs using StashPayCard.
    /// Attach to a Button GameObject.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class StashPaySample : MonoBehaviour
    {
        [Header("Stash Checkout Configuration")]
        [Tooltip("Your Stash API Key")]
        [SerializeField] private string apiKey = "your-api-key-here";
        
        [Tooltip("Environment to use")]
        [SerializeField] private StashEnvironment environment = StashEnvironment.Test;
        
        [Header("User Information")]
        [Tooltip("External user ID to identify the user")]
        [SerializeField] private string externalUserId = "user123";
        
        [Tooltip("User's email address")]
        [SerializeField] private string userEmail = "example@example.com";
        
        [Tooltip("User's display name")]
        [SerializeField] private string displayName = "Example User";
        
        [Tooltip("URL to user's avatar/icon")]
        [SerializeField] private string avatarIconUrl = "";
        
        [Tooltip("URL to user's profile")]
        [SerializeField] private string profileUrl = "";
        
        [Header("Shop Configuration")]
        [Tooltip("Shop handle")]
        [SerializeField] private string shopHandle = "demo-shop";
        
        [Tooltip("Currency code")]
        [SerializeField] private string currency = "USD";
        
        [Header("Product Information")]
        [Tooltip("Product ID")]
        [SerializeField] private string productId = "product-123";
        
        [Tooltip("Product price per item")]
        [SerializeField] private string pricePerItem = "9.99";
        
        [Tooltip("Product quantity")]
        [SerializeField] private int quantity = 1;
        
        [Tooltip("Product image URL")]
        [SerializeField] private string productImageUrl = "";
        
        [Tooltip("Product name")]
        [SerializeField] private string productName = "Example Product";
        
        [Tooltip("Product description")]
        [SerializeField] private string productDescription = "This is an example product";
        
        [Header("Card Configuration")]
        [Tooltip("Position mode for the card")]
        [SerializeField] private Stash.Samples.CardPositionMode positionMode = Stash.Samples.CardPositionMode.BottomSheet;
        
        [Tooltip("Height of the card as percentage of screen (0.1 to 0.9)")]
        [Range(0.1f, 0.9f)]
        [SerializeField] private float cardHeightRatio = 0.4f;
        
        [Tooltip("Vertical position for custom mode (0.0 = top, 0.5 = middle, 1.0 = bottom)")]
        [Range(0.0f, 1.0f)]
        [SerializeField] private float customVerticalPosition = 0.5f;
        
        [Header("Events")]
        [Tooltip("Events triggered when the StashPayCard is opened")]
        [SerializeField] private UnityEvent onCardOpened;
        
        [Tooltip("Events triggered when the StashPayCard is closed")]
        [SerializeField] private UnityEvent onCardClosed;
        
        private Button button;
        private string generatedCheckoutUrl;
        
        private void Awake()
        {
            button = GetComponent<Button>();
        }
        
        private void OnEnable()
        {
            button.onClick.AddListener(OpenURL);
        }
        
        private void OnDisable()
        {
            button.onClick.RemoveListener(OpenURL);
        }
        
        /// <summary>
        /// Generates a checkout URL using the Stash Checkout API
        /// </summary>
        private async Task<string> GenerateCheckoutUrl()
        {
            // Generate the checkout URL using the Stash Checkout API (simplified method)
            var (url, id) = await StashCheckout.CreateCheckoutLink(
                externalUserId,
                userEmail,
                shopHandle,
                productId,
                apiKey,
                environment
            );
            
            Debug.Log($"[Stash] Generated checkout URL: {url} with ID: {id}");
            return url;
        }
        
        /// <summary>
        /// Opens the URL using StashPayCard
        /// </summary>
        public async void OpenURL()
        {
            try
            {
                // Configure the card based on selected mode
                ConfigureCardPosition();
                
                // Generate or use cached checkout URL
                if (string.IsNullOrEmpty(generatedCheckoutUrl))
                {
                    generatedCheckoutUrl = await GenerateCheckoutUrl();
                }
                
                // Open the checkout URL in the StashPayCard
                StashPayCard.Instance.OpenURL(generatedCheckoutUrl, OnBrowserClosed);
                onCardOpened?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Stash] Error generating checkout URL: {ex.Message}");
                // No fallback - just log the error without displaying the card
            }
        }
        
        /// <summary>
        /// Configure the card position based on the selected mode
        /// </summary>
        private void ConfigureCardPosition()
        {
            switch (positionMode)
            {
                case Stash.Samples.CardPositionMode.BottomSheet:
                    StashPayCard.Instance.ConfigureAsBottomSheet(cardHeightRatio);
                    Debug.Log($"[Stash] Configured card as bottom sheet with height ratio {cardHeightRatio}");
                    break;
                    
                case Stash.Samples.CardPositionMode.CenterDialog:
                    StashPayCard.Instance.ConfigureAsDialog(cardHeightRatio);
                    Debug.Log($"[Stash] Configured card as center dialog with height ratio {cardHeightRatio}");
                    break;
                    
                case Stash.Samples.CardPositionMode.FullScreen:
                    StashPayCard.Instance.ConfigureAsFullScreen(cardHeightRatio);
                    Debug.Log($"[Stash] Configured card as fullscreen with height ratio {cardHeightRatio}");
                    break;
                    
                case Stash.Samples.CardPositionMode.Custom:
                    StashPayCard.Instance.SetCardHeightRatio(cardHeightRatio);
                    StashPayCard.Instance.SetCardVerticalPosition(customVerticalPosition);
                    Debug.Log($"[Stash] Configured card with custom position: height ratio {cardHeightRatio}, vertical position {customVerticalPosition}");
                    break;
            }
        }
        
        /// <summary>
        /// Callback when the browser is closed
        /// </summary>
        private void OnBrowserClosed()
        {
            Debug.Log("Browser was closed!");
            onCardClosed?.Invoke();
        }
    }
} 