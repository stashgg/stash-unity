using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

namespace Stash.Samples
{
    /// <summary>
    /// Unified confetti effect system for UI celebrations
    /// </summary>
    public static class ConfettiEffect
    {
        /// <summary>
        /// Creates a confetti effect on the UI
        /// </summary>
        public static void Create(VisualElement rootElement)
        {
            if (rootElement == null)
            {
                Debug.LogError("[ConfettiEffect] Root element is null");
                return;
            }

            var confettiGO = new GameObject("ConfettiAnimator");
            var animator = confettiGO.AddComponent<ConfettiAnimator>();
            animator.Initialize(rootElement);
            UnityEngine.Object.Destroy(confettiGO, 6f);
        }
    }

    /// <summary>
    /// Confetti animator component
    /// </summary>
    public class ConfettiAnimator : MonoBehaviour
    {
        private VisualElement container;
        private VisualElement rootElement;
        private List<ConfettiPiece> pieces = new List<ConfettiPiece>();
        private float elapsed = 0f;

        private class ConfettiPiece
        {
            public VisualElement element;
            public float startX;
            public float driftX;
            public float rotationSpeed;
            public float duration;
            public float elapsed;
            public float startDelay;
        }

        public void Initialize(VisualElement root)
        {
            rootElement = root;

            container = new VisualElement();
            container.name = "ConfettiContainer";
            container.style.position = Position.Absolute;
            container.style.top = 0;
            container.style.left = 0;
            container.style.width = Length.Percent(100);
            container.style.height = Length.Percent(100);
            container.pickingMode = PickingMode.Ignore;
            rootElement.Add(container);

            Color[] colors = { 
                Color.red, Color.blue, Color.green, Color.yellow, 
                Color.magenta, Color.cyan, new Color(1f, 0.5f, 0f) 
            };

            int confettiCount = 250;
            for (int i = 0; i < confettiCount; i++)
            {
                VisualElement piece = new VisualElement();
                float size = UnityEngine.Random.Range(4f, 10f);
                piece.style.width = size;
                piece.style.height = size;
                piece.style.backgroundColor = colors[UnityEngine.Random.Range(0, colors.Length)];
                piece.style.position = Position.Absolute;

                float startX = UnityEngine.Random.Range(0f, 100f);
                piece.style.left = Length.Percent(startX);
                piece.style.top = 0f;
                piece.style.rotate = new Rotate(UnityEngine.Random.Range(0f, 360f));
                piece.style.opacity = 0f;

                container.Add(piece);

                pieces.Add(new ConfettiPiece
                {
                    element = piece,
                    startX = startX,
                    driftX = UnityEngine.Random.Range(-120f, 120f),
                    rotationSpeed = UnityEngine.Random.Range(180f, 540f),
                    duration = UnityEngine.Random.Range(2.5f, 4f),
                    startDelay = UnityEngine.Random.Range(0f, 1.5f)
                });
            }
        }

        private void Update()
        {
            if (container == null || container.parent == null)
            {
                Destroy(gameObject);
                return;
            }

            elapsed += Time.deltaTime;

            for (int i = pieces.Count - 1; i >= 0; i--)
            {
                ConfettiPiece piece = pieces[i];

                if (piece.element == null || piece.element.parent == null)
                {
                    pieces.RemoveAt(i);
                    continue;
                }

                if (elapsed < piece.startDelay)
                    continue;

                if (piece.elapsed == 0f)
                    piece.element.style.opacity = 1f;

                float animationTime = elapsed - piece.startDelay;
                piece.elapsed = animationTime;
                float t = piece.elapsed / piece.duration;

                if (t >= 1f)
                {
                    if (piece.element.parent != null)
                        piece.element.RemoveFromHierarchy();
                    pieces.RemoveAt(i);
                    continue;
                }

                float easedT = 1f - Mathf.Pow(1f - t, 2f);
                float currentX = piece.startX + (piece.driftX * easedT);
                float currentY = easedT * 100f;

                piece.element.style.left = Length.Percent(currentX);
                piece.element.style.top = Length.Percent(currentY);
                piece.element.style.rotate = new Rotate(piece.elapsed * piece.rotationSpeed);

                if (t > 0.7f)
                {
                    float fadeT = (t - 0.7f) / 0.3f;
                    piece.element.style.opacity = 1f - fadeT;
                }
            }

            if (pieces.Count == 0 && elapsed > 6f)
            {
                if (container != null && container.parent != null)
                    container.RemoveFromHierarchy();
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (container != null && container.parent != null)
                container.RemoveFromHierarchy();
        }
    }
}

