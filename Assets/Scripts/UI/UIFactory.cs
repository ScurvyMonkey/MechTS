using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MechTS.UI
{
    /// <summary>
    /// Static helpers for building runtime UGUI elements without hand-authored prefabs,
    /// matching the project's convention of code-created scene objects (see
    /// <see cref="MechTS.Core.Bootstrapper"/>'s manager instantiation).
    /// </summary>
    public static class UIFactory
    {
        /// <summary>Top-left screen anchor.</summary>
        public static readonly Vector2 TopLeft = new Vector2(0f, 1f);

        /// <summary>Top-right screen anchor.</summary>
        public static readonly Vector2 TopRight = new Vector2(1f, 1f);

        /// <summary>Bottom-left screen anchor.</summary>
        public static readonly Vector2 BottomLeft = new Vector2(0f, 0f);

        /// <summary>Bottom-right screen anchor.</summary>
        public static readonly Vector2 BottomRight = new Vector2(1f, 0f);

        /// <summary>Bottom-center screen anchor.</summary>
        public static readonly Vector2 BottomCenter = new Vector2(0.5f, 0f);

        /// <summary>Dead-center screen anchor.</summary>
        public static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        private static Font _font;

        /// <summary>
        /// Returns Unity's built-in default font, cached after first lookup.
        /// </summary>
        public static Font GetDefaultFont()
        {
            if (_font == null)
            {
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            return _font;
        }

        /// <summary>
        /// Creates a UI Text element anchored and pivoted at the given screen corner.
        /// </summary>
        public static Text CreateText(Transform parent, string name, Vector2 anchor, Vector2 anchoredPosition, Vector2 sizeDelta, int fontSize = 14)
        {
            var rect = CreateRect(parent, name, anchor, anchoredPosition, sizeDelta);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = GetDefaultFont();
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAnchor.UpperLeft;
            return text;
        }

        /// <summary>
        /// Creates a clickable button with a centered text label, anchored and pivoted at
        /// the given screen corner.
        /// </summary>
        public static Button CreateButton(Transform parent, string name, string label, Vector2 anchor, Vector2 anchoredPosition, Vector2 sizeDelta, UnityAction onClick)
        {
            var rect = CreateRect(parent, name, anchor, anchoredPosition, sizeDelta);

            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.15f, 0.15f, 0.18f, 0.9f);

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (onClick != null) button.onClick.AddListener(onClick);

            var labelText = CreateText(rect, "Label", Vector2.zero, Vector2.zero, Vector2.zero, 12);
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.text = label;

            var labelRect = labelText.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            return button;
        }

        /// <summary>
        /// Creates a semi-transparent background panel, anchored and pivoted at the given screen corner.
        /// </summary>
        public static RectTransform CreatePanel(Transform parent, string name, Vector2 anchor, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var rect = CreateRect(parent, name, anchor, anchoredPosition, sizeDelta);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.45f);
            return rect;
        }

        /// <summary>
        /// Creates a bare, invisible RectTransform GameObject anchored/pivoted at the given
        /// screen corner — useful for grouping child elements without a background Image.
        /// </summary>
        public static RectTransform CreateRect(Transform parent, Vector2 anchor, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            return CreateRect(parent, "Group", anchor, anchoredPosition, sizeDelta);
        }

        /// <summary>
        /// Creates a bare RectTransform GameObject with the given anchor, pivot, position, and size.
        /// </summary>
        private static RectTransform CreateRect(Transform parent, string name, Vector2 anchor, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            return rect;
        }
    }
}
