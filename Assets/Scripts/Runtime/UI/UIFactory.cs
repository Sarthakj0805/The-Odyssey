using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Odyssey.UI
{
    /// <summary>
    /// Builds uGUI hierarchies from code (legacy Text with Unity's built-in font) so scenes stay
    /// trivial and nothing depends on imported fonts or prefabs.
    /// </summary>
    public static class UIFactory
    {
        public static readonly Color Ink = new Color(0.08f, 0.1f, 0.16f, 0.92f);
        public static readonly Color Gold = new Color(0.95f, 0.8f, 0.4f);
        public static readonly Color Parchment = new Color(0.96f, 0.92f, 0.82f);
        private static Font _font;

        public static Font Font => _font ? _font : (_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        public static Canvas Canvas(string name, int order)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            var s = go.GetComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(1920, 1080);
            s.matchWidthOrHeight = 0.5f;
            EnsureEventSystem();
            return c;
        }

        public static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            UnityEngine.Object.DontDestroyOnLoad(es);
        }

        public static RectTransform Rect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
            return rt;
        }

        public static Image Panel(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rt = Rect(parent, name, anchorMin, anchorMax, offsetMin, offsetMax);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static Text Label(Transform parent, string name, string text, int size, TextAnchor anchor, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, FontStyle style = FontStyle.Normal)
        {
            var rt = Rect(parent, name, anchorMin, anchorMax, offsetMin, offsetMax);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font; t.text = text; t.fontSize = size; t.alignment = anchor; t.color = color; t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Button Button(Transform parent, string name, string label, Action onClick,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, int fontSize = 34)
        {
            var img = Panel(parent, name, new Color(0.18f, 0.2f, 0.3f, 0.95f), anchorMin, anchorMax, offsetMin, offsetMax);
            var b = img.gameObject.AddComponent<Button>();
            var colors = b.colors;
            colors.highlightedColor = new Color(1f, 0.9f, 0.6f);
            colors.selectedColor = new Color(1f, 0.9f, 0.6f);
            colors.pressedColor = Gold;
            b.colors = colors;
            b.onClick.AddListener(() => onClick?.Invoke());
            Label(img.transform, "Text", label, fontSize, TextAnchor.MiddleCenter, Parchment, Vector2.zero, Vector2.one, new Vector2(10, 5), new Vector2(-10, -5));
            return b;
        }

        public static Vector2 V(float x, float y) => new Vector2(x, y);
    }
}
