using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Stash.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Stash.Sample
{
    public class StashCheckoutController : MonoBehaviour
    {
        [SerializeField] public UIDocument uiDocument;
        
        // Text fields for user information
        private TextField userIdField;
        private TextField emailField;
        private TextField displayNameField;
        private TextField avatarUrlField;
        private TextField profileUrlField;
        
        // Text fields for shop information
        private TextField shopHandleField;
        private TextField currencyField;
        private TextField apiKeyField;
        
        // Items container
        private VisualElement itemsContainer;
        private Button addItemButton;
        
        // Response fields
        private TextField checkoutUrlField;
        private TextField checkoutIdField;
        
        // Buttons and containers
        private Button createCheckoutButton;
        private Button openBrowserButton;
        private VisualElement responseContainer;

        // Counter for unique item IDs
        private int nextItemIndex = 1; // Start with 1 because we already have Item0

        // Ensures UI is only initialized once
        private bool uiInitialized = false;
        
        private void Start()
        {
            // Directly initialize without adding a loader component
            // Start a coroutine to check for UI initialization
            StartCoroutine(WaitForUIInitialization());
        }

        private IEnumerator WaitForUIInitialization()
        {
            // Wait for two frames to ensure everything is set up
            yield return null;
            yield return null;

            // Wait until UI Document is ready
            int attempts = 0;
            while ((uiDocument == null || uiDocument.rootVisualElement == null) && attempts < 20)
            {
                Debug.Log("Waiting for UI Document to initialize...");
                yield return new WaitForSeconds(0.1f);
                attempts++;
            }

            if (uiDocument == null || uiDocument.rootVisualElement == null)
            {
                Debug.LogError("UI Document failed to initialize after multiple attempts.");
                yield break;
            }

            // Initialize the UI
            InitializeUI();
        }
        
        private void InitializeUI()
        {
            if (uiInitialized)
            {
                return;
            }

            Debug.Log("Initializing UI elements...");
            
            if (uiDocument == null)
            {
                Debug.LogError("UIDocument is null!");
                return;
            }
            
            if (uiDocument.rootVisualElement == null)
            {
                Debug.LogError("Root visual element is null!");
                return;
            }
            
            var root = uiDocument.rootVisualElement;
            
            // Initialize user information fields
            userIdField = root.Q<TextField>("UserID");
            emailField = root.Q<TextField>("Email");
            displayNameField = root.Q<TextField>("DisplayName");
            avatarUrlField = root.Q<TextField>("AvatarURL");
            profileUrlField = root.Q<TextField>("ProfileURL");
            
            // Initialize shop information fields
            shopHandleField = root.Q<TextField>("ShopHandle");
            currencyField = root.Q<TextField>("Currency");
            apiKeyField = root.Q<TextField>("ApiKey");
            
            // Initialize items container and add button
            itemsContainer = root.Q<VisualElement>("ItemsContainer");
            addItemButton = root.Q<Button>("AddItemButton");
            
            // Initialize response fields
            checkoutUrlField = root.Q<TextField>("CheckoutURL");
            checkoutIdField = root.Q<TextField>("CheckoutID");
            
            // Initialize buttons and containers
            createCheckoutButton = root.Q<Button>("CreateCheckoutButton");
            openBrowserButton = root.Q<Button>("OpenBrowserButton");
            responseContainer = root.Q<VisualElement>("ResponseContainer");
            
            if (createCheckoutButton == null)
            {
                Debug.LogError("Create Checkout Button not found in the UI! Check element name in UXML.");
                return;
            }
            
            if (addItemButton == null)
            {
                Debug.LogError("Add Item Button not found in the UI! Check element name in UXML.");
                return;
            }
            
            // Clear any existing callbacks to avoid duplicates
            createCheckoutButton.clickable = null;
            addItemButton.clickable = null;
            
            // Register event handlers
            createCheckoutButton.clickable = new Clickable(CreateCheckoutLink);
            addItemButton.clickable = new Clickable(AddNewItem);
            
            // Initialize the first item's remove button
            InitializeRemoveButton(0);
            
            if (openBrowserButton != null)
            {
                openBrowserButton.clickable = null;
                openBrowserButton.clickable = new Clickable(OpenCheckoutInBrowser);
            }

            // Set flag to indicate UI is initialized
            uiInitialized = true;
            
            // Log UI element references to debug
            Debug.Log($"UI initialized successfully! Create button: {createCheckoutButton != null}, Open browser button: {openBrowserButton != null}");
            
            // For debugging: Add a simple callback
            createCheckoutButton.RegisterCallback<ClickEvent>(evt => 
            {
                Debug.Log("Button clicked via callback!");
            });
        }
        
        private void AddNewItem()
        {
            Debug.Log("Adding new item...");
            
            // Clone the first item element as a template
            var templateItem = itemsContainer.Q<VisualElement>($"Item0");
            if (templateItem == null)
            {
                Debug.LogError("Template item not found!");
                return;
            }
            
            // Create a new item with a unique index
            var newItemIndex = nextItemIndex++;
            var newItem = new VisualElement();
            newItem.name = $"Item{newItemIndex}";
            newItem.AddToClassList("item-container");
            
            // Add header with label and remove button
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 5;
            
            var label = new Label($"Item {newItemIndex + 1}");
            label.style.fontSize = 16;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = new Color(0.86f, 0.86f, 0.86f);
            
            var removeButton = new Button();
            removeButton.text = "Remove";
            removeButton.name = $"RemoveItem{newItemIndex}";
            removeButton.AddToClassList("button-delete");
            removeButton.style.marginTop = 0;
            
            header.Add(label);
            header.Add(removeButton);
            newItem.Add(header);
            
            // Add form fields
            var itemIdField = new TextField("Item ID");
            itemIdField.name = $"ItemID{newItemIndex}";
            itemIdField.value = $"item_id_{newItemIndex}";
            
            var priceField = new TextField("Price Per Item");
            priceField.name = $"PricePerItem{newItemIndex}";
            priceField.value = "199";
            
            var quantityField = new IntegerField("Quantity");
            quantityField.name = $"Quantity{newItemIndex}";
            quantityField.value = 1;
            
            var imageUrlField = new TextField("Image URL");
            imageUrlField.name = $"ItemImageURL{newItemIndex}";
            imageUrlField.value = "https://api.braincloudservers.com/files/portal/g/15152/metadata/products/battle_pass.png";
            
            var nameField = new TextField("Name");
            nameField.name = $"ItemName{newItemIndex}";
            nameField.value = $"item_name_{newItemIndex}";
            
            var descField = new TextField("Description");
            descField.name = $"ItemDescription{newItemIndex}";
            descField.value = $"item_description_{newItemIndex}";
            
            // Add all fields to the new item
            newItem.Add(itemIdField);
            newItem.Add(priceField);
            newItem.Add(quantityField);
            newItem.Add(imageUrlField);
            newItem.Add(nameField);
            newItem.Add(descField);
            
            // Add to the container
            itemsContainer.Add(newItem);
            
            // Register remove button click event
            removeButton.clickable = new Clickable(() => RemoveItem(newItemIndex));
            
            Debug.Log($"New item added with index {newItemIndex}");
        }
        
        private void RemoveItem(int itemIndex)
        {
            Debug.Log($"Removing item {itemIndex}...");
            
            var itemToRemove = itemsContainer.Q<VisualElement>($"Item{itemIndex}");
            if (itemToRemove != null)
            {
                itemsContainer.Remove(itemToRemove);
                Debug.Log($"Item {itemIndex} removed successfully");
            }
            else
            {
                Debug.LogError($"Could not find item with index {itemIndex}");
            }
        }
        
        private void InitializeRemoveButton(int itemIndex)
        {
            var removeButton = itemsContainer.Q<Button>($"RemoveItem{itemIndex}");
            if (removeButton != null)
            {
                removeButton.clickable = null;
                removeButton.clickable = new Clickable(() => RemoveItem(itemIndex));
            }
            else
            {
                Debug.LogWarning($"Remove button for item {itemIndex} not found");
            }
        }
        
        private List<StashCheckout.CheckoutItemData> GatherAllItems()
        {
            var items = new List<StashCheckout.CheckoutItemData>();
            
            // Find all item containers
            for (int i = 0; i < nextItemIndex; i++)
            {
                var itemContainer = itemsContainer.Q<VisualElement>($"Item{i}");
                if (itemContainer == null) continue; // Skip if this item has been removed
                
                var itemIdField = itemContainer.Q<TextField>($"ItemID{i}");
                var priceField = itemContainer.Q<TextField>($"PricePerItem{i}");
                var quantityField = itemContainer.Q<IntegerField>($"Quantity{i}");
                var imageUrlField = itemContainer.Q<TextField>($"ItemImageURL{i}");
                var nameField = itemContainer.Q<TextField>($"ItemName{i}");
                var descField = itemContainer.Q<TextField>($"ItemDescription{i}");
                
                if (itemIdField != null && priceField != null && quantityField != null && 
                    imageUrlField != null && nameField != null && descField != null)
                {
                    var item = new StashCheckout.CheckoutItemData
                    {
                        id = itemIdField.value,
                        pricePerItem = priceField.value,
                        quantity = quantityField.value,
                        imageUrl = imageUrlField.value,
                        name = nameField.value,
                        description = descField.value
                    };
                    
                    items.Add(item);
                    Debug.Log($"Added item {i} to checkout request: {item.name}");
                }
            }
            
            return items;
        }
        
        private async void CreateCheckoutLink()
        {
            Debug.Log("Creating checkout link with multiple items...");
            
            try
            {
                // Gather all items from the UI
                var items = GatherAllItems();
                
                if (items.Count == 0)
                {
                    Debug.LogError("No items found to add to checkout!");
                    return;
                }
                
                // Create a checkout link with all items
                var result = await StashCheckout.CreateCheckoutLinkWithItems(
                    userIdField.value,
                    emailField.value,
                    displayNameField.value,
                    avatarUrlField.value,
                    profileUrlField.value,
                    shopHandleField.value,
                    currencyField.value,
                    items.ToArray(),
                    apiKeyField.value
                );
                
                // Display the result
                checkoutUrlField.value = result.url;
                checkoutIdField.value = result.id;
                responseContainer.style.display = DisplayStyle.Flex;
                
                Debug.Log($"Checkout link created successfully with {items.Count} items: {result.url}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error creating checkout link: {ex.Message}");
                
                // Display error in the UI
                if (checkoutUrlField != null)
                {
                    checkoutUrlField.value = $"Error: {ex.Message}";
                    
                    if (checkoutIdField != null)
                    {
                        checkoutIdField.value = "N/A";
                    }
                    
                    if (responseContainer != null)
                    {
                        responseContainer.style.display = DisplayStyle.Flex;
                    }
                }
            }
        }
        
        private void OpenCheckoutInBrowser()
        {
            Debug.Log("Opening browser...");
            if (checkoutUrlField != null && !string.IsNullOrEmpty(checkoutUrlField.value) && !checkoutUrlField.value.StartsWith("Error:"))
            {
                Debug.Log($"Opening URL: {checkoutUrlField.value}");
                StashCheckout.OpenUrlInBrowser(checkoutUrlField.value);
            }
            else
            {
                Debug.LogWarning("Cannot open URL: No valid checkout URL available");
            }
        }
    }
} 