using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;
using StashPopup;

namespace Stash.Samples
{
    /// <summary>
    /// Logs native exceptions from Stash.Popup and displays them in a scrollable UI panel.
    /// </summary>
    public class NativeExceptionLogger : MonoBehaviour
    {
        [Header("UI Configuration")]
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private bool autoShowOnException = true;
        
        /// <summary>
        /// Public property to set the UI document reference
        /// </summary>
        public UIDocument UIDocument
        {
            get => uiDocument;
            set => uiDocument = value;
        }
        
        [Header("Log Settings")]
        [SerializeField] private int maxLogEntries = 100;
        [SerializeField] private bool clearOnStart = false;
        
        private VisualElement root;
        private VisualElement logPanel;
        private ScrollView logScrollView;
        private Label logCountLabel;
        private bool isPanelVisible = false;
        
        private readonly List<LogEntry> logEntries = new List<LogEntry>();
        
        private class LogEntry
        {
            public string Operation { get; set; }
            public string Message { get; set; }
            public string StackTrace { get; set; }
            public DateTime Timestamp { get; set; }
        }
        
        private void Start()
        {
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
                if (uiDocument == null)
                {
                    uiDocument = FindObjectOfType<UIDocument>();
                }
            }
            
            if (uiDocument == null)
            {
                Debug.LogError("[NativeExceptionLogger] No UIDocument found!");
                return;
            }
            
            root = uiDocument.rootVisualElement;
            
            if (clearOnStart)
            {
                logEntries.Clear();
            }
            
            // Subscribe to native exceptions
            StashPayCard.Instance.OnNativeException += OnNativeException;
            
            // Create UI
            CreateLogUI();
            
            // Update UI
            UpdateLogDisplay();
        }
        
        private void OnDestroy()
        {
            if (StashPayCard.Instance != null)
            {
                StashPayCard.Instance.OnNativeException -= OnNativeException;
            }
        }
        
        private void OnNativeException(string operation, Exception exception)
        {
            var entry = new LogEntry
            {
                Operation = operation,
                Message = exception?.Message ?? "Unknown error",
                StackTrace = exception?.StackTrace ?? "",
                Timestamp = DateTime.Now
            };
            
            logEntries.Add(entry);
            
            // Limit log size
            if (logEntries.Count > maxLogEntries)
            {
                logEntries.RemoveAt(0);
            }
            
            // Log to Unity console as well
            Debug.LogError($"[NativeExceptionLogger] {operation}: {entry.Message}");
            if (!string.IsNullOrEmpty(entry.StackTrace))
            {
                Debug.LogError($"[NativeExceptionLogger] StackTrace: {entry.StackTrace}");
            }
            
            // Update UI
            UpdateLogDisplay();
            
            // Auto-show if enabled
            if (autoShowOnException && !isPanelVisible)
            {
                ShowLogPanel();
            }
        }
        
        private void CreateLogUI()
        {
            // Create log panel (initially hidden)
            logPanel = new VisualElement();
            logPanel.name = "native-log-panel";
            logPanel.style.position = Position.Absolute;
            logPanel.style.top = 0;
            logPanel.style.left = 0;
            logPanel.style.width = Length.Percent(100);
            logPanel.style.height = Length.Percent(100);
            logPanel.style.backgroundColor = new Color(0, 0, 0, 0.85f);
            logPanel.style.display = DisplayStyle.None;
            logPanel.style.flexDirection = FlexDirection.Column;
            
            // Make panel clickable to close (clicking backdrop closes)
            logPanel.RegisterCallback<ClickEvent>(evt => {
                // Only close if clicking the backdrop itself, not child elements
                if (evt.target == logPanel)
                {
                    HideLogPanel();
                }
            });
            
            // Create main content container (centered card)
            var contentContainer = new VisualElement();
            contentContainer.name = "log-content-container";
            contentContainer.style.width = Length.Percent(90);
            contentContainer.style.maxWidth = 800;
            contentContainer.style.height = Length.Percent(85);
            contentContainer.style.maxHeight = 600;
            contentContainer.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.98f);
            contentContainer.style.borderTopLeftRadius = 12;
            contentContainer.style.borderTopRightRadius = 12;
            contentContainer.style.borderBottomLeftRadius = 12;
            contentContainer.style.borderBottomRightRadius = 12;
            contentContainer.style.borderTopWidth = 2;
            contentContainer.style.borderBottomWidth = 2;
            contentContainer.style.borderLeftWidth = 2;
            contentContainer.style.borderRightWidth = 2;
            contentContainer.style.borderTopColor = new Color(1f, 0.5f, 0f, 0.8f);
            contentContainer.style.borderBottomColor = new Color(1f, 0.5f, 0f, 0.8f);
            contentContainer.style.borderLeftColor = new Color(1f, 0.5f, 0f, 0.8f);
            contentContainer.style.borderRightColor = new Color(1f, 0.5f, 0f, 0.8f);
            contentContainer.style.flexDirection = FlexDirection.Column;
            contentContainer.style.alignSelf = Align.Center;
            contentContainer.style.alignItems = Align.Stretch;
            
            // Prevent clicks on content from closing the panel
            contentContainer.RegisterCallback<ClickEvent>(evt => {
                evt.StopPropagation();
            });
            
            // Create header
            var header = new VisualElement();
            header.name = "log-header";
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            header.style.paddingLeft = 12;
            header.style.paddingRight = 12;
            header.style.paddingTop = 12;
            header.style.paddingBottom = 12;
            header.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.95f);
            header.style.borderTopLeftRadius = 12;
            header.style.borderTopRightRadius = 12;
            header.style.borderBottomWidth = 2;
            header.style.borderBottomColor = new Color(1f, 0.5f, 0f, 0.8f);
            header.style.flexShrink = 0; // Don't shrink header
            header.style.minHeight = 50; // Ensure minimum height
            
            // Left side: Title
            var titleContainer = new VisualElement();
            titleContainer.style.flexDirection = FlexDirection.Column;
            titleContainer.style.flexGrow = 1;
            titleContainer.style.flexShrink = 1;
            titleContainer.style.minWidth = 0; // Allow shrinking
            titleContainer.style.marginRight = 8;
            
            var titleLabel = new Label("Native Exception Log");
            titleLabel.style.color = new Color(1f, 0.84f, 0f, 1f);
            titleLabel.style.fontSize = 16;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.whiteSpace = WhiteSpace.Normal;
            titleLabel.style.overflow = Overflow.Hidden;
            titleLabel.style.textOverflow = TextOverflow.Ellipsis;
            
            logCountLabel = new Label("0 entries");
            logCountLabel.name = "log-count-label";
            logCountLabel.style.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            logCountLabel.style.fontSize = 12;
            logCountLabel.style.marginTop = 2;
            
            titleContainer.Add(titleLabel);
            titleContainer.Add(logCountLabel);
            
            // Right side: Close button
            var closeButton = new Button(HideLogPanel);
            closeButton.text = "Close";
            closeButton.name = "close-log-button";
            closeButton.style.minWidth = 70;
            closeButton.style.maxWidth = 100;
            closeButton.style.width = Length.Auto();
            closeButton.style.height = 32;
            closeButton.style.paddingLeft = 12;
            closeButton.style.paddingRight = 12;
            closeButton.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.9f);
            closeButton.style.borderTopLeftRadius = 6;
            closeButton.style.borderTopRightRadius = 6;
            closeButton.style.borderBottomLeftRadius = 6;
            closeButton.style.borderBottomRightRadius = 6;
            closeButton.style.color = Color.white;
            closeButton.style.fontSize = 12;
            closeButton.style.flexShrink = 0; // Don't shrink button
            closeButton.style.marginLeft = 8;
            
            header.Add(titleContainer);
            header.Add(closeButton);
            
            // Create scrollable log view
            logScrollView = new ScrollView();
            logScrollView.name = "log-scroll-view";
            logScrollView.style.flexGrow = 1;
            logScrollView.style.flexShrink = 1;
            logScrollView.style.paddingLeft = 16;
            logScrollView.style.paddingRight = 16;
            logScrollView.style.paddingTop = 16;
            logScrollView.style.paddingBottom = 16;
            logScrollView.style.overflow = Overflow.Hidden;
            logScrollView.verticalScroller.valueChanged += OnScrollValueChanged;
            
            // Add header and scroll view to content container
            contentContainer.Add(header);
            contentContainer.Add(logScrollView);
            
            // Add content container to panel (centered)
            logPanel.style.alignItems = Align.Center;
            logPanel.style.justifyContent = Justify.Center;
            logPanel.Add(contentContainer);
            root.Add(logPanel);
        }
        
        private void UpdateLogDisplay()
        {
            if (logScrollView == null) return;
            
            // Update count label
            if (logCountLabel != null)
            {
                logCountLabel.text = $"{logEntries.Count} entries";
            }
            
            // Badge update removed - button is now in settings popup
            
            // Clear existing log entries
            logScrollView.Clear();
            
            // Add log entries (newest first)
            for (int i = logEntries.Count - 1; i >= 0; i--)
            {
                var entry = logEntries[i];
                var entryElement = CreateLogEntryElement(entry);
                logScrollView.Add(entryElement);
            }
            
            // Scroll to top
            if (logScrollView.verticalScroller != null)
            {
                logScrollView.verticalScroller.value = logScrollView.verticalScroller.highValue;
            }
        }
        
        private VisualElement CreateLogEntryElement(LogEntry entry)
        {
            var container = new VisualElement();
            container.style.marginBottom = 12;
            container.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.9f);
            container.style.borderTopLeftRadius = 8;
            container.style.borderTopRightRadius = 8;
            container.style.borderBottomLeftRadius = 8;
            container.style.borderBottomRightRadius = 8;
            container.style.borderTopWidth = 1;
            container.style.borderBottomWidth = 1;
            container.style.borderLeftWidth = 1;
            container.style.borderRightWidth = 1;
            container.style.borderTopColor = new Color(1f, 0.5f, 0f, 0.5f);
            container.style.borderBottomColor = new Color(1f, 0.5f, 0f, 0.5f);
            container.style.borderLeftColor = new Color(1f, 0.5f, 0f, 0.5f);
            container.style.borderRightColor = new Color(1f, 0.5f, 0f, 0.5f);
            container.style.paddingLeft = 12;
            container.style.paddingRight = 12;
            container.style.paddingTop = 10;
            container.style.paddingBottom = 10;
            
            // Header row
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.justifyContent = Justify.SpaceBetween;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.marginBottom = 6;
            
            var operationLabel = new Label(entry.Operation);
            operationLabel.style.color = new Color(1f, 0.84f, 0f, 1f);
            operationLabel.style.fontSize = 14;
            operationLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            
            var timeLabel = new Label(entry.Timestamp.ToString("HH:mm:ss"));
            timeLabel.style.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            timeLabel.style.fontSize = 11;
            
            headerRow.Add(operationLabel);
            headerRow.Add(timeLabel);
            
            // Message
            var messageLabel = new Label(entry.Message);
            messageLabel.style.color = Color.white;
            messageLabel.style.fontSize = 12;
            messageLabel.style.whiteSpace = WhiteSpace.Normal;
            messageLabel.style.marginBottom = 4;
            
            // Stack trace (collapsible)
            var stackTraceContainer = new VisualElement();
            stackTraceContainer.style.display = DisplayStyle.None;
            
            var stackTraceToggle = new Button(() => {
                stackTraceContainer.style.display = 
                    stackTraceContainer.style.display == DisplayStyle.None 
                        ? DisplayStyle.Flex 
                        : DisplayStyle.None;
            });
            stackTraceToggle.text = "Show Stack Trace";
            stackTraceToggle.style.width = Length.Auto();
            stackTraceToggle.style.height = 24;
            stackTraceToggle.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.9f);
            stackTraceToggle.style.borderTopLeftRadius = 4;
            stackTraceToggle.style.borderTopRightRadius = 4;
            stackTraceToggle.style.borderBottomLeftRadius = 4;
            stackTraceToggle.style.borderBottomRightRadius = 4;
            stackTraceToggle.style.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            stackTraceToggle.style.fontSize = 10;
            stackTraceToggle.style.marginTop = 4;
            
            if (!string.IsNullOrEmpty(entry.StackTrace))
            {
                var stackTraceLabel = new Label(entry.StackTrace);
                stackTraceLabel.style.color = new Color(0.8f, 0.6f, 0.6f, 1f);
                stackTraceLabel.style.fontSize = 10;
                stackTraceLabel.style.whiteSpace = WhiteSpace.Normal;
                stackTraceLabel.style.marginTop = 4;
                stackTraceLabel.style.paddingLeft = 8;
                stackTraceLabel.style.paddingRight = 8;
                stackTraceLabel.style.paddingTop = 6;
                stackTraceLabel.style.paddingBottom = 6;
                stackTraceLabel.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.9f);
                stackTraceLabel.style.borderTopLeftRadius = 4;
                stackTraceLabel.style.borderTopRightRadius = 4;
                stackTraceLabel.style.borderBottomLeftRadius = 4;
                stackTraceLabel.style.borderBottomRightRadius = 4;
                
                stackTraceContainer.Add(stackTraceToggle);
                stackTraceContainer.Add(stackTraceLabel);
            }
            
            container.Add(headerRow);
            container.Add(messageLabel);
            if (stackTraceContainer.childCount > 0)
            {
                container.Add(stackTraceContainer);
            }
            
            return container;
        }
        
        /// <summary>
        /// Public method to show the log panel (called from settings button)
        /// </summary>
        public void ShowLogPanel()
        {
            if (logPanel != null)
            {
                logPanel.style.display = DisplayStyle.Flex;
                isPanelVisible = true;
                
                // Scroll to top
                if (logScrollView?.verticalScroller != null)
                {
                    logScrollView.verticalScroller.value = logScrollView.verticalScroller.highValue;
                }
            }
        }
        
        /// <summary>
        /// Public method to hide the log panel
        /// </summary>
        public void HideLogPanel()
        {
            if (logPanel != null)
            {
                logPanel.style.display = DisplayStyle.None;
                isPanelVisible = false;
            }
        }
        
        private void ClearLogs()
        {
            logEntries.Clear();
            UpdateLogDisplay();
        }
        
        private void OnScrollValueChanged(float value)
        {
            // Auto-scroll handling if needed
        }
    }
}

