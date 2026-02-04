using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;
using System;

namespace Stash.Samples
{
    /// <summary>
    /// Unified notification system for displaying popups and toasts in the UI.
    /// Consolidates SuccessPopup, SimpleToast, and LoadTimeToast into a single system.
    /// </summary>
    public static class UINotificationSystem
    {
        private static VisualElement s_cachedRoot;
        private static int s_cachedRootPanelHash;

        /// <summary>
        /// Shows a popup notification (centered, modal)
        /// </summary>
        public static void ShowPopup(string title, string message, float duration = 3f, VisualElement rootElement = null)
        {
            var go = new GameObject("UIPopup");
            var popup = go.AddComponent<UIPopup>();
            popup.Initialize(title, message, duration, rootElement);
        }

        /// <summary>
        /// Shows a toast notification (bottom of screen, non-modal)
        /// </summary>
        public static void ShowToast(string title, string message, float duration = 3f, VisualElement rootElement = null)
        {
            var go = new GameObject("UIToast");
            var toast = go.AddComponent<UIToast>();
            toast.Initialize(title, message, duration, rootElement);
        }

        /// <summary>
        /// Shows a load time toast (top of screen)
        /// </summary>
        public static void ShowLoadTimeToast(double loadTimeMs, VisualElement rootElement = null)
        {
            var go = new GameObject("UILoadTimeToast");
            var toast = go.AddComponent<UILoadTimeToast>();
            toast.Initialize(loadTimeMs, rootElement);
        }

        /// <summary>
        /// Finds the root visual element from the scene. Caches result to avoid repeated FindObjectOfType calls.
        /// </summary>
        public static VisualElement FindRootElement(VisualElement overrideElement = null)
        {
            if (overrideElement != null)
                return overrideElement;

            if (s_cachedRoot != null && s_cachedRoot.panel != null)
            {
                int currentHash = s_cachedRoot.panel.GetHashCode();
                if (s_cachedRootPanelHash == currentHash)
                    return s_cachedRoot;
            }
            s_cachedRoot = null;
            s_cachedRootPanelHash = 0;

            var storeUIController = UnityEngine.Object.FindObjectOfType<StashStoreUIController>();
            if (storeUIController != null)
            {
                var uiDocument = storeUIController.GetComponent<UIDocument>();
                if (uiDocument?.rootVisualElement != null)
                {
                    s_cachedRoot = uiDocument.rootVisualElement;
                    s_cachedRootPanelHash = s_cachedRoot.panel?.GetHashCode() ?? 0;
                    return s_cachedRoot;
                }
            }

            var allUIDocuments = UnityEngine.Object.FindObjectsOfType<UIDocument>();
            foreach (var doc in allUIDocuments)
            {
                if (doc.rootVisualElement != null)
                {
                    s_cachedRoot = doc.rootVisualElement;
                    s_cachedRootPanelHash = s_cachedRoot.panel?.GetHashCode() ?? 0;
                    return s_cachedRoot;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Base class for UI notifications
    /// </summary>
    public abstract class UINotificationBase : MonoBehaviour
    {
        protected VisualElement container;
        protected VisualElement rootElement;
        protected float duration;

        public void Initialize(float duration, VisualElement rootElement = null)
        {
            this.duration = duration;
            this.rootElement = UINotificationSystem.FindRootElement(rootElement);
            
            if (this.rootElement == null)
            {
                Debug.LogError("[UINotification] Could not find root visual element");
                Destroy(gameObject);
                return;
            }

            CreateNotification();
            StartCoroutine(AutoDestroy());
        }

        protected abstract void CreateNotification();

        protected IEnumerator AutoDestroy()
        {
            yield return new WaitForSeconds(duration);
            Destroy(gameObject);
        }

        protected virtual void OnDestroy()
        {
            if (container != null && container.parent != null)
            {
                container.RemoveFromHierarchy();
            }
        }
    }

    /// <summary>
    /// Centered popup notification
    /// </summary>
    public class UIPopup : UINotificationBase
    {
        private string title;
        private string message;

        public void Initialize(string title, string message, float duration, VisualElement rootElement = null)
        {
            this.title = title;
            this.message = message;
            base.Initialize(duration, rootElement);
        }

        protected override void CreateNotification()
        {
            float screenWidth = Screen.width;
            float screenHeight = Screen.height;
            float popupWidth = Mathf.Clamp(screenWidth * 0.5f, 150f, 200f);
            float popupHeight = Mathf.Clamp(screenHeight * 0.2f, 120f, 180f);

            // Create container
            container = new VisualElement();
            container.style.position = Position.Absolute;
            container.style.top = 0;
            container.style.left = 0;
            container.style.width = Length.Percent(100);
            container.style.height = Length.Percent(100);
            container.style.backgroundColor = new Color(0, 0, 0, 0.75f);
            container.style.alignItems = Align.Center;
            container.style.justifyContent = Justify.Center;

            // Create card
            var card = new VisualElement();
            card.style.backgroundColor = new Color(0.26f, 0.26f, 0.29f, 0.95f);
            card.style.borderTopLeftRadius = 12;
            card.style.borderTopRightRadius = 12;
            card.style.borderBottomLeftRadius = 12;
            card.style.borderBottomRightRadius = 12;
            card.style.borderTopWidth = 3;
            card.style.borderBottomWidth = 3;
            card.style.borderLeftWidth = 3;
            card.style.borderRightWidth = 3;
            card.style.borderTopColor = new Color(0.18f, 0.18f, 0.18f, 0.96f);
            card.style.borderBottomColor = new Color(0.18f, 0.18f, 0.18f, 0.96f);
            card.style.borderLeftColor = new Color(0.18f, 0.18f, 0.18f, 0.96f);
            card.style.borderRightColor = new Color(0.18f, 0.18f, 0.18f, 0.96f);
            card.style.paddingLeft = 16;
            card.style.paddingRight = 16;
            card.style.paddingTop = 14;
            card.style.paddingBottom = 14;
            card.style.width = popupWidth;
            card.style.maxHeight = popupHeight;

            // Title
            var titleLabel = new Label(title);
            titleLabel.style.fontSize = Mathf.Clamp(screenWidth * 0.03f, 14f, 16f);
            titleLabel.style.color = new Color(1f, 0.84f, 0f, 1f);
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            titleLabel.style.marginBottom = 8;
            titleLabel.style.whiteSpace = WhiteSpace.Normal;

            // Message
            var messageLabel = new Label(message);
            messageLabel.style.fontSize = Mathf.Clamp(screenWidth * 0.025f, 11f, 13f);
            messageLabel.style.color = Color.white;
            messageLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            messageLabel.style.whiteSpace = WhiteSpace.Normal;

            card.Add(titleLabel);
            card.Add(messageLabel);
            container.Add(card);
            rootElement.Add(container);

            // Animate in
            card.style.scale = new Vector2(0.8f, 0.8f);
            container.style.opacity = 0;
            StartCoroutine(AnimateIn());
        }

        private IEnumerator AnimateIn()
        {
            yield return StartCoroutine(AnimateFloat(0f, 1f, 0.2f, v => container.style.opacity = v));
            yield return StartCoroutine(AnimateFloat(0.8f, 1f, 0.3f, v => {
                if (container.childCount > 0)
                    container[0].style.scale = new Vector2(v, v);
            }));
        }

        private IEnumerator AnimateFloat(float from, float to, float duration, Action<float> onUpdate)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                t = t < 0.5f ? 2f * t * t : -1f + (4f - 2f * t) * t;
                onUpdate(Mathf.Lerp(from, to, t));
                yield return null;
            }
            onUpdate(to);
        }
    }

    /// <summary>
    /// Bottom toast notification
    /// </summary>
    public class UIToast : UINotificationBase
    {
        private string title;
        private string message;

        public void Initialize(string title, string message, float duration, VisualElement rootElement = null)
        {
            this.title = title;
            this.message = message;
            base.Initialize(duration, rootElement);
        }

        protected override void CreateNotification()
        {
            container = new VisualElement();
            container.style.position = Position.Absolute;
            container.style.bottom = 100;
            container.style.left = Length.Percent(50);
            container.style.translate = new Translate(Length.Percent(-50), 0);
            container.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.95f);
            container.style.borderTopLeftRadius = 8;
            container.style.borderTopRightRadius = 8;
            container.style.borderBottomLeftRadius = 8;
            container.style.borderBottomRightRadius = 8;
            container.style.paddingLeft = 16;
            container.style.paddingRight = 16;
            container.style.paddingTop = 12;
            container.style.paddingBottom = 12;
            container.style.minWidth = 200;
            container.style.maxWidth = 300;
            container.style.flexDirection = FlexDirection.Column;
            container.style.borderTopWidth = 2;
            container.style.borderBottomWidth = 2;
            container.style.borderLeftWidth = 2;
            container.style.borderRightWidth = 2;
            var borderColor = new Color(0.6f, 0.6f, 0.6f, 0.8f);
            container.style.borderTopColor = borderColor;
            container.style.borderBottomColor = borderColor;
            container.style.borderLeftColor = borderColor;
            container.style.borderRightColor = borderColor;

            var titleLabel = new Label(title);
            titleLabel.style.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            titleLabel.style.fontSize = 14;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            titleLabel.style.marginBottom = 4;

            var messageLabel = new Label(message);
            messageLabel.style.color = Color.white;
            messageLabel.style.fontSize = 12;
            messageLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            messageLabel.style.whiteSpace = WhiteSpace.Normal;

            container.Add(titleLabel);
            container.Add(messageLabel);
            rootElement.Add(container);

            container.style.opacity = 0;
            container.style.bottom = 80;
            StartCoroutine(AnimateIn());
        }

        private IEnumerator AnimateIn()
        {
            float elapsed = 0f;
            float animDuration = 0.3f;
            while (elapsed < animDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / animDuration;
                container.style.opacity = t;
                container.style.bottom = Mathf.Lerp(80, 100, t);
                yield return null;
            }
            container.style.opacity = 1;
            container.style.bottom = 100;

            yield return new WaitForSeconds(duration - 0.6f);
            StartCoroutine(AnimateOut());
        }

        private IEnumerator AnimateOut()
        {
            float elapsed = 0f;
            float animDuration = 0.3f;
            while (elapsed < animDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / animDuration;
                container.style.opacity = 1 - t;
                container.style.bottom = Mathf.Lerp(100, 80, t);
                yield return null;
            }
        }
    }

    /// <summary>
    /// Load time toast (top of screen)
    /// </summary>
    public class UILoadTimeToast : UINotificationBase
    {
        private double loadTimeMs;

        public void Initialize(double loadTimeMs, VisualElement rootElement = null)
        {
            this.loadTimeMs = loadTimeMs;
            base.Initialize(2.5f, rootElement);
        }

        protected override void CreateNotification()
        {
            container = new VisualElement();
            container.style.position = Position.Absolute;
            container.style.top = 60;
            container.style.left = Length.Percent(50);
            container.style.translate = new Translate(Length.Percent(-50), 0);
            container.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.95f);
            container.style.borderTopLeftRadius = 8;
            container.style.borderTopRightRadius = 8;
            container.style.borderBottomLeftRadius = 8;
            container.style.borderBottomRightRadius = 8;
            container.style.paddingLeft = 16;
            container.style.paddingRight = 16;
            container.style.paddingTop = 8;
            container.style.paddingBottom = 8;
            container.style.minWidth = 150;
            container.style.borderTopWidth = 2;
            container.style.borderBottomWidth = 2;
            container.style.borderLeftWidth = 2;
            container.style.borderRightWidth = 2;
            var borderColor = new Color(0.3f, 0.7f, 1f, 0.6f);
            container.style.borderTopColor = borderColor;
            container.style.borderBottomColor = borderColor;
            container.style.borderLeftColor = borderColor;
            container.style.borderRightColor = borderColor;

            var label = new Label($"Rendered in {loadTimeMs:F0}ms");
            label.style.color = Color.white;
            label.style.fontSize = 13;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;

            container.Add(label);
            rootElement.Add(container);

            container.style.opacity = 0;
            container.style.top = 50;
            StartCoroutine(AnimateIn());
        }

        private IEnumerator AnimateIn()
        {
            float elapsed = 0f;
            float animDuration = 0.3f;
            while (elapsed < animDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / animDuration;
                container.style.opacity = t;
                container.style.top = Mathf.Lerp(50, 60, t);
                yield return null;
            }
            container.style.opacity = 1;
            container.style.top = 60;

            yield return new WaitForSeconds(duration - 0.6f);
            StartCoroutine(AnimateOut());
        }

        private IEnumerator AnimateOut()
        {
            float elapsed = 0f;
            float animDuration = 0.3f;
            while (elapsed < animDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / animDuration;
                container.style.opacity = 1 - t;
                container.style.top = Mathf.Lerp(60, 50, t);
                yield return null;
            }
        }
    }
}

