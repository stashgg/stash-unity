using UnityEngine;
using UnityEngine.UIElements;

namespace StashPopup
{
    /// <summary>
    /// Handles blocking navigation in the UI during purchase flows
    /// </summary>
    public class NavigationBlocker : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        private VisualElement navigationBlocker;
        private static NavigationBlocker instance;

        public static NavigationBlocker Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("NavigationBlocker");
                    instance = go.AddComponent<NavigationBlocker>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }

            // Create navigation blocker element if it doesn't exist
            CreateNavigationBlocker();
        }

        private void CreateNavigationBlocker()
        {
            if (uiDocument == null)
            {
                uiDocument = FindObjectOfType<UIDocument>();
            }

            if (uiDocument != null)
            {
                var root = uiDocument.rootVisualElement;
                
                // Check if blocker already exists
                navigationBlocker = root.Q("navigation-blocker");
                if (navigationBlocker == null)
                {
                    // Create new blocker
                    navigationBlocker = new VisualElement
                    {
                        name = "navigation-blocker"
                    };
                    
                    // Add CSS class
                    navigationBlocker.AddToClassList("navigation-blocker");
                    
                    // Add click event to prevent click-through
                    navigationBlocker.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
                    
                    // Add to root
                    root.Add(navigationBlocker);
                }
            }
            else
            {
                Debug.LogError("NavigationBlocker: No UIDocument found in the scene!");
            }
        }

        /// <summary>
        /// Block navigation in the UI
        /// </summary>
        public void BlockNavigation()
        {
            if (navigationBlocker != null)
            {
                navigationBlocker.style.display = DisplayStyle.Flex;
                navigationBlocker.AddToClassList("visible");
            }
        }

        /// <summary>
        /// Unblock navigation in the UI
        /// </summary>
        public void UnblockNavigation()
        {
            if (navigationBlocker != null)
            {
                navigationBlocker.RemoveFromClassList("visible");
                // Wait for transition to complete before hiding
                navigationBlocker.schedule.Execute(() => {
                    navigationBlocker.style.display = DisplayStyle.None;
                }).StartingIn(200); // 200ms matches transition-duration in CSS
            }
        }
    }
} 