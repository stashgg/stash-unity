using UnityEngine;
using UnityEngine.UIElements;
using StashPopup;
using System.Linq;

namespace Stash.Samples
{
    /// <summary>
    /// Card position modes for StashPayCard display
    /// </summary>
    public enum CardPositionMode
    {
        BottomSheet,
        CenterDialog,
        FullScreen,
        Custom
    }

    public class SettingsTabInit : MonoBehaviour
    {
        private DropdownField cardPositionDropdown;
        private SliderInt cardHeightSlider;
        private SliderInt cardPositionSlider;
        private SliderInt cardWidthSlider;
        private VisualElement cardPositionSliderContainer;
        private VisualElement cardWidthSliderContainer;
        private Label heightValueLabel;
        private Label positionValueLabel;
        private Label widthValueLabel;
        
        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            
            // Find the store tab content
            var storeTab = root.Q<VisualElement>("store-tab-content");
            if (storeTab != null)
            {
                InitializeCardSettings(storeTab);
            }
            
            // Set up tab switching
            var storeTabButton = root.Q<Button>("store-tab-button");
            if (storeTabButton != null)
            {
                storeTabButton.clicked += () => {
                    // Re-initialize card settings when store tab is shown
                    InitializeCardSettings(storeTab);
                };
            }
        }
        
        private void InitializeCardSettings(VisualElement container)
        {
            if (container == null) return;
            
            var cardSettingsContainer = container.Q<VisualElement>("card-settings-container");
            if (cardSettingsContainer == null) return;
            
            // Card Position Mode Dropdown
            cardPositionDropdown = cardSettingsContainer.Q<DropdownField>("card-position-mode");
            if (cardPositionDropdown != null)
            {
                // Populate dropdown from enum
                var enumNames = System.Enum.GetNames(typeof(CardPositionMode))
                    .Select(name => AddSpacesToCamelCase(name))
                    .ToList();
                
                cardPositionDropdown.choices = enumNames;
                cardPositionDropdown.value = AddSpacesToCamelCase(CardPositionMode.BottomSheet.ToString());
                cardPositionDropdown.RegisterValueChangedCallback(evt => OnCardPositionModeChanged(evt.newValue));
            }
            
            // Card Height Slider
            cardHeightSlider = cardSettingsContainer.Q<SliderInt>("card-height-slider");
            heightValueLabel = cardSettingsContainer.Q<Label>("height-value");
            if (cardHeightSlider != null)
            {
                cardHeightSlider.RegisterValueChangedCallback(evt => {
                    OnCardHeightChanged(evt.newValue);
                    if (heightValueLabel != null)
                    {
                        heightValueLabel.text = $"{evt.newValue}%";
                    }
                });
            }
            
            // Custom Position Slider
            cardPositionSliderContainer = cardSettingsContainer.Q<VisualElement>("card-position-slider-container");
            cardPositionSlider = cardSettingsContainer.Q<SliderInt>("card-position-slider");
            positionValueLabel = cardSettingsContainer.Q<Label>("position-value");
            if (cardPositionSlider != null)
            {
                cardPositionSlider.RegisterValueChangedCallback(evt => {
                    OnCardPositionChanged(evt.newValue);
                    if (positionValueLabel != null)
                    {
                        positionValueLabel.text = $"{evt.newValue}%";
                    }
                });
            }
            
            // Card Width Slider
            cardWidthSliderContainer = cardSettingsContainer.Q<VisualElement>("card-width-slider-container");
            cardWidthSlider = cardSettingsContainer.Q<SliderInt>("card-width-slider");
            widthValueLabel = cardSettingsContainer.Q<Label>("width-value");
            if (cardWidthSlider != null)
            {
                cardWidthSlider.RegisterValueChangedCallback(evt => {
                    OnCardWidthChanged(evt.newValue);
                    if (widthValueLabel != null)
                    {
                        widthValueLabel.text = $"{evt.newValue}%";
                    }
                });
            }
            
            // Initialize with default values
            UpdateCardConfiguration();
            
            // Initially hide the position and width sliders
            if (cardPositionSliderContainer != null)
            {
                cardPositionSliderContainer.style.display = DisplayStyle.None;
            }
            
            if (cardWidthSliderContainer != null)
            {
                cardWidthSliderContainer.style.display = DisplayStyle.None;
            }
        }
        
        /// <summary>
        /// Converts enum name from CamelCase to spaced format (e.g., "BottomSheet" -> "Bottom Sheet")
        /// </summary>
        private string AddSpacesToCamelCase(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            
            var result = "";
            for (int i = 0; i < text.Length; i++)
            {
                if (i > 0 && char.IsUpper(text[i]))
                {
                    result += " ";
                }
                result += text[i];
            }
            return result;
        }
        
        /// <summary>
        /// Converts spaced format back to enum name (e.g., "Bottom Sheet" -> "BottomSheet")
        /// </summary>
        private string RemoveSpacesFromText(string text)
        {
            return text?.Replace(" ", "") ?? "";
        }
        
        /// <summary>
        /// Gets the CardPositionMode enum value from the dropdown selection
        /// </summary>
        private CardPositionMode GetSelectedPositionMode()
        {
            if (cardPositionDropdown == null) return CardPositionMode.BottomSheet;
            
            string enumName = RemoveSpacesFromText(cardPositionDropdown.value);
            if (System.Enum.TryParse<CardPositionMode>(enumName, out CardPositionMode mode))
            {
                return mode;
            }
            
            return CardPositionMode.BottomSheet; // Default fallback
        }
        
        private void OnCardPositionModeChanged(string newValue)
        {
            // Show/hide custom position slider based on mode
            if (cardPositionSliderContainer != null)
            {
                CardPositionMode mode = GetSelectedPositionMode();
                bool isCustom = mode == CardPositionMode.Custom;
                cardPositionSliderContainer.style.display = isCustom ? DisplayStyle.Flex : DisplayStyle.None;
                cardPositionSliderContainer.EnableInClassList("visible", isCustom);
            }
            
            // Show/hide custom width slider based on mode
            if (cardWidthSliderContainer != null)
            {
                CardPositionMode mode = GetSelectedPositionMode();
                bool isCustom = mode == CardPositionMode.Custom;
                cardWidthSliderContainer.style.display = isCustom ? DisplayStyle.Flex : DisplayStyle.None;
                cardWidthSliderContainer.EnableInClassList("visible", isCustom);
            }
            
            UpdateCardConfiguration();
        }
        
        private void OnCardHeightChanged(int newValue)
        {
            UpdateCardConfiguration();
        }
        
        private void OnCardPositionChanged(int newValue)
        {
            UpdateCardConfiguration();
        }
        
        private void OnCardWidthChanged(int newValue)
        {
            UpdateCardConfiguration();
        }
        
        /// <summary>
        /// Updates the StashPayCard configuration based on current UI settings
        /// </summary>
        private void UpdateCardConfiguration()
        {
            if (cardPositionDropdown == null || cardHeightSlider == null) return;
            
            // Convert height from percentage (10-100) to ratio (0.1-1.0)
            float heightRatio = cardHeightSlider.value / 100f;
            CardPositionMode mode = GetSelectedPositionMode();
            
            switch (mode)
            {
                case CardPositionMode.BottomSheet:
                    StashPayCard.Instance.ConfigureAsBottomSheet(heightRatio);
                    break;
                
                case CardPositionMode.CenterDialog:
                    StashPayCard.Instance.ConfigureAsDialog(heightRatio);
                    break;
                
                case CardPositionMode.FullScreen:
                    StashPayCard.Instance.ConfigureAsFullScreen(heightRatio);
                    break;
                
                case CardPositionMode.Custom:
                    if (cardPositionSlider != null && cardWidthSlider != null)
                    {
                        // Convert position from percentage (0-100) to ratio (0.0-1.0)
                        float positionRatio = cardPositionSlider.value / 100f;
                        float widthRatio = cardWidthSlider.value / 100f;
                        
                        StashPayCard.Instance.SetCardHeightRatio(heightRatio);
                        StashPayCard.Instance.SetCardVerticalPosition(positionRatio);
                        StashPayCard.Instance.SetCardWidthRatio(widthRatio);
                    }
                    break;
            }
        }
        
        /// <summary>
        /// Public method to get current card settings for external use
        /// </summary>
        public CardConfigurationData GetCurrentCardConfiguration()
        {
            if (cardPositionDropdown == null || cardHeightSlider == null)
                return new CardConfigurationData(); // Return default
            
            return new CardConfigurationData
            {
                mode = GetSelectedPositionMode(),
                heightRatio = cardHeightSlider.value / 100f,
                customPosition = cardPositionSlider?.value / 100f ?? 0.5f,
                customWidth = cardWidthSlider?.value / 100f ?? 0.9f
            };
        }
    }
    
    /// <summary>
    /// Data structure for card configuration settings
    /// </summary>
    [System.Serializable]
    public struct CardConfigurationData
    {
        public CardPositionMode mode;
        public float heightRatio;
        public float customPosition;
        public float customWidth;
        
        public CardConfigurationData(CardPositionMode mode = CardPositionMode.BottomSheet, float heightRatio = 0.4f, float customPosition = 0.5f, float customWidth = 0.9f)
        {
            this.mode = mode;
            this.heightRatio = heightRatio;
            this.customPosition = customPosition;
            this.customWidth = customWidth;
        }
    }
}
