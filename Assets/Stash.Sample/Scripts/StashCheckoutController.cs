using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Stash.Core;
using UnityEngine;
using UnityEngine.UIElements;
using System.Text;
using System.Linq;

namespace Stash.Sample
{
    public class StashCheckoutController : MonoBehaviour
    {
        [SerializeField] public UIDocument uiDocument;
        
        private VisualElement root;
        private Button createCheckoutButton;
        private Button openBrowserButton;
        private Button openIOSViewButton;
        private Button addItemButton;
        private VisualElement responseContainer;
        private TextField checkoutUrlField;
        private TextField checkoutIdField;
        private VisualElement itemsContainer;
        private readonly List<VisualElement> itemElements = new List<VisualElement>();
        
        // Tab navigation
        private Button userTabButton;
        private Button shopTabButton;
        private Button itemsTabButton;
        private VisualElement userSection;
        private VisualElement shopSection;
        private VisualElement itemsSection;

        // iOS native methods
        #if UNITY_IOS
        [DllImport("__Internal")]
        private static extern void _OpenURLInSafariVC(string url);
        #endif

        private void Start()
        {
            if (uiDocument == null)
            {
                Debug.LogError("UIDocument is not assigned.");
                return;
            }
            
            root = uiDocument.rootVisualElement;
            if (root == null)
            {
                Debug.LogError("Root VisualElement not found.");
                return;
            }
            
            // Get all UI elements and set up event handlers
            SetupUI();
            SetupEventHandlers();
        }
        
        private void SetupUI()
        {
            // Tab elements
            userTabButton = root.Q<Button>("UserTabButton");
            shopTabButton = root.Q<Button>("ShopTabButton");
            itemsTabButton = root.Q<Button>("ItemsTabButton");
            userSection = root.Q<VisualElement>("UserSection");
            shopSection = root.Q<VisualElement>("ShopSection");
            itemsSection = root.Q<VisualElement>("ItemsSection");
            
            // Other UI elements
            createCheckoutButton = root.Q<Button>("CreateCheckoutButton");
            openBrowserButton = root.Q<Button>("OpenBrowserButton");
            openIOSViewButton = root.Q<Button>("OpenIOSViewButton");
            addItemButton = root.Q<Button>("AddItemButton");
            responseContainer = root.Q<VisualElement>("ResponseContainer");
            checkoutUrlField = root.Q<TextField>("CheckoutURL");
            checkoutIdField = root.Q<TextField>("CheckoutID");
            itemsContainer = root.Q<VisualElement>("ItemsContainer");
            
            // Set initial state
            responseContainer.style.display = DisplayStyle.None;
            openBrowserButton.SetEnabled(false);
            openIOSViewButton.SetEnabled(false);
            
            // Set iOS button visibility based on platform
            #if !UNITY_IOS
            if (openIOSViewButton != null)
            {
                openIOSViewButton.style.display = DisplayStyle.None;
            }
            #endif
            
            // Get the existing item as a template
            VisualElement itemTemplate = itemsContainer.Q<VisualElement>("Item0");
            if (itemTemplate != null)
            {
                itemElements.Add(itemTemplate);
            }
        }
        
        private void SetupEventHandlers()
        {
            // Tab button events
            userTabButton.clicked += () => SwitchTab("user");
            shopTabButton.clicked += () => SwitchTab("shop");
            itemsTabButton.clicked += () => SwitchTab("items");
            
            // Other button events
            createCheckoutButton.clicked += CreateCheckoutLink;
            openBrowserButton.clicked += OpenInBrowser;
            
            if (openIOSViewButton != null)
            {
                openIOSViewButton.clicked += OpenInIOSWebView;
            }
            
            addItemButton.clicked += AddItem;
            
            // Setup remove button for existing item
            if (itemElements.Count > 0)
            {
                SetupRemoveItemButton(itemElements[0], 0);
            }
        }
        
        private void SwitchTab(string tabName)
        {
            // Reset all tabs
            userTabButton.RemoveFromClassList("tab-button-active");
            shopTabButton.RemoveFromClassList("tab-button-active");
            itemsTabButton.RemoveFromClassList("tab-button-active");
            
            userSection.RemoveFromClassList("tab-content-active");
            shopSection.RemoveFromClassList("tab-content-active");
            itemsSection.RemoveFromClassList("tab-content-active");
            
            // Activate selected tab
            switch (tabName)
            {
                case "user":
                    userTabButton.AddToClassList("tab-button-active");
                    userSection.AddToClassList("tab-content-active");
                    break;
                case "shop":
                    shopTabButton.AddToClassList("tab-button-active");
                    shopSection.AddToClassList("tab-content-active");
                    break;
                case "items":
                    itemsTabButton.AddToClassList("tab-button-active");
                    itemsSection.AddToClassList("tab-content-active");
                    break;
            }
            
            Debug.Log($"Switched to {tabName} tab");
        }
        
        private void AddItem()
        {
            if (itemElements.Count > 0)
            {
                // Get the template item
                VisualElement template = itemElements[0];
                
                // Create a new index for the item
                int newIndex = itemElements.Count;
                
                // Clone the template
                VisualElement newItem = template.CloneElement();
                newItem.name = $"Item{newIndex}";
                
                // Update the label
                var itemTitle = newItem.Q<Label>(className: "item-title");
                if (itemTitle != null)
                {
                    itemTitle.text = $"Item {newIndex + 1}";
                }
                
                // Update names of all field elements within the new item
                UpdateFieldNames(newItem, newIndex);
                
                // Set up the remove button
                SetupRemoveItemButton(newItem, newIndex);
                
                // Add the new item to the container and to our list
                itemsContainer.Add(newItem);
                itemElements.Add(newItem);
                
                Debug.Log($"Added item {newIndex + 1}");
            }
        }
        
        private void UpdateFieldNames(VisualElement item, int index)
        {
            // Find all TextField and IntegerField elements and update their names
            var textFields = item.Query<TextField>().ToList();
            foreach (var field in textFields)
            {
                string baseName = field.name;
                if (baseName.EndsWith("0"))
                {
                    string newName = baseName.Substring(0, baseName.Length - 1) + index;
                    field.name = newName;
                }
            }
            
            var intFields = item.Query<IntegerField>().ToList();
            foreach (var field in intFields)
            {
                string baseName = field.name;
                if (baseName.EndsWith("0"))
                {
                    string newName = baseName.Substring(0, baseName.Length - 1) + index;
                    field.name = newName;
                }
            }
            
            // Update the name of the remove button
            var removeButton = item.Q<Button>(name: $"RemoveItem0");
            if (removeButton != null)
            {
                removeButton.name = $"RemoveItem{index}";
            }
        }
        
        private void SetupRemoveItemButton(VisualElement item, int index)
        {
            var removeButton = item.Q<Button>(name: $"RemoveItem{index}");
            if (removeButton != null)
            {
                removeButton.clicked += () => RemoveItem(index);
            }
        }
        
        private void RemoveItem(int index)
        {
            if (index < itemElements.Count)
            {
                // Remove the item from the visual container
                VisualElement itemToRemove = itemElements[index];
                itemsContainer.Remove(itemToRemove);
                
                // Remove from our list
                itemElements.RemoveAt(index);
                
                // Renumber the remaining items
                for (int i = 0; i < itemElements.Count; i++)
                {
                    var item = itemElements[i];
                    item.name = $"Item{i}";
                    
                    // Update title
                    var itemTitle = item.Q<Label>(className: "item-title");
                    if (itemTitle != null)
                    {
                        itemTitle.text = $"Item {i + 1}";
                    }
                    
                    // Update field names
                    UpdateFieldNames(item, i);
                    
                    // Update remove button
                    var removeButton = item.Q<Button>(name: $"RemoveItem{i}");
                    if (removeButton != null)
                    {
                        // Remove any existing handlers
                        removeButton.clicked -= () => RemoveItem(i);
                        
                        // Add new handler
                        int capturedIndex = i; // Capture the current index to avoid closure issues
                        removeButton.clicked += () => RemoveItem(capturedIndex);
                    }
                }
                
                Debug.Log($"Removed item at index {index}");
            }
        }
        
        private async void CreateCheckoutLink()
        {
            try
            {
                // Build checkout request from UI fields
                var items = GatherItemsData();
                
                string userId = GetFieldValue("UserID");
                string email = GetFieldValue("Email");
                string displayName = GetFieldValue("DisplayName");
                string avatarUrl = GetFieldValue("AvatarURL");
                string profileUrl = GetFieldValue("ProfileURL");
                string shopHandle = GetFieldValue("ShopHandle");
                string currency = GetFieldValue("Currency");
                string apiKey = GetFieldValue("ApiKey");
                
                // Call the Stash API
                var result = await StashCheckout.CreateCheckoutLinkWithItems(
                    userId,
                    email,
                    displayName,
                    avatarUrl,
                    profileUrl,
                    shopHandle,
                    currency,
                    items.ToArray(),
                    apiKey
                );
                
                // Display the result
                DisplayCheckoutResponse(result.url, result.id);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error creating checkout link: {ex.Message}");
                DisplayError($"Error: {ex.Message}");
            }
        }
        
        private List<StashCheckout.CheckoutItemData> GatherItemsData()
        {
            var items = new List<StashCheckout.CheckoutItemData>();
            
            for (int i = 0; i < itemElements.Count; i++)
            {
                var item = new StashCheckout.CheckoutItemData
                {
                    id = GetFieldValue($"ItemID{i}"),
                    pricePerItem = GetFieldValue($"PricePerItem{i}"),
                    quantity = int.Parse(GetFieldValue($"Quantity{i}")),
                    imageUrl = GetFieldValue($"ItemImageURL{i}"),
                    name = GetFieldValue($"ItemName{i}"),
                    description = GetFieldValue($"ItemDescription{i}")
                };
                
                items.Add(item);
            }
            
            return items;
        }
        
        private void DisplayCheckoutResponse(string url, string id)
        {
            // Update UI with the response information
            checkoutUrlField.value = url;
            checkoutIdField.value = id;
            
            // Show the response container
            responseContainer.style.display = DisplayStyle.Flex;
            
            // Enable the browser buttons
            openBrowserButton.SetEnabled(true);
            
            if (openIOSViewButton != null)
            {
                openIOSViewButton.SetEnabled(true);
            }
            
            // Automatically open the URL in browser
            if (!string.IsNullOrEmpty(url))
            {
                #if UNITY_IOS && !UNITY_EDITOR
                OpenInIOSWebView();
                #else
                StashCheckout.OpenUrlInBrowser(url);
                #endif
                Debug.Log($"Automatically opening checkout URL: {url}");
            }
            
            Debug.Log("Checkout link generated successfully");
        }
        
        private void DisplayError(string errorMessage)
        {
            // Show error in the checkout URL field
            checkoutUrlField.value = errorMessage;
            checkoutIdField.value = string.Empty;
            
            // Show the response container
            responseContainer.style.display = DisplayStyle.Flex;
            
            // Disable the browser buttons
            openBrowserButton.SetEnabled(false);
            
            if (openIOSViewButton != null)
            {
                openIOSViewButton.SetEnabled(false);
            }
        }
        
        private void OpenInBrowser()
        {
            if (!string.IsNullOrEmpty(checkoutUrlField.value))
            {
                StashCheckout.OpenUrlInBrowser(checkoutUrlField.value);
            }
        }
        
        private void OpenInIOSWebView()
        {
            if (string.IsNullOrEmpty(checkoutUrlField.value)) return;
            
            #if UNITY_IOS && !UNITY_EDITOR
            try
            {
                _OpenURLInSafariVC(checkoutUrlField.value);
                Debug.Log($"Opening URL in iOS Safari View: {checkoutUrlField.value}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error opening URL in iOS Safari View: {ex.Message}");
                // Fallback to regular browser open
                StashCheckout.OpenUrlInBrowser(checkoutUrlField.value);
            }
            #else
            // Fallback to regular browser open on non-iOS platforms
            Debug.Log("Safari View is only available on iOS. Opening in regular browser instead.");
            StashCheckout.OpenUrlInBrowser(checkoutUrlField.value);
            #endif
        }
        
        private string GetFieldValue(string fieldName)
        {
            var textField = root.Q<TextField>(fieldName);
            if (textField != null)
            {
                return textField.value;
            }
            
            var intField = root.Q<IntegerField>(fieldName);
            if (intField != null)
            {
                return intField.value.ToString();
            }
            
            return string.Empty;
        }
    }
    
    // Helper extension method to clone a VisualElement
    public static class VisualElementExtensions
    {
        public static VisualElement CloneElement(this VisualElement original)
        {
            // Create a new element of the same type
            var clone = new VisualElement();
            clone.name = original.name;
            
            // Copy classes
            foreach (var className in original.GetClasses())
            {
                clone.AddToClassList(className);
            }
            
            // Copy style (this is a simplified approach)
            clone.style.width = original.style.width;
            clone.style.height = original.style.height;
            clone.style.marginTop = original.style.marginTop;
            clone.style.marginRight = original.style.marginRight;
            clone.style.marginBottom = original.style.marginBottom;
            clone.style.marginLeft = original.style.marginLeft;
            clone.style.paddingTop = original.style.paddingTop;
            clone.style.paddingRight = original.style.paddingRight;
            clone.style.paddingBottom = original.style.paddingBottom;
            clone.style.paddingLeft = original.style.paddingLeft;
            
            // Clone child elements
            foreach (var child in original.Children())
            {
                VisualElement clonedChild;
                
                // Special handling for different element types
                if (child is Label label)
                {
                    var clonedLabel = new Label(label.text);
                    clonedLabel.name = label.name;
                    clonedChild = clonedLabel;
                }
                else if (child is Button button)
                {
                    var clonedButton = new Button();
                    clonedButton.text = button.text;
                    clonedButton.name = button.name;
                    clonedChild = clonedButton;
                }
                else if (child is TextField textField)
                {
                    var clonedTextField = new TextField();
                    clonedTextField.name = textField.name;
                    clonedTextField.label = textField.label;
                    clonedTextField.value = textField.value;
                    clonedTextField.multiline = textField.multiline;
                    clonedChild = clonedTextField;
                }
                else if (child is IntegerField intField)
                {
                    var clonedIntField = new IntegerField();
                    clonedIntField.name = intField.name;
                    clonedIntField.label = intField.label;
                    clonedIntField.value = intField.value;
                    clonedChild = clonedIntField;
                }
                else
                {
                    clonedChild = child.CloneElement();
                }
                
                // Copy classes to the cloned child
                foreach (var className in child.GetClasses())
                {
                    clonedChild.AddToClassList(className);
                }
                
                clone.Add(clonedChild);
            }
            
            return clone;
        }
    }
} 