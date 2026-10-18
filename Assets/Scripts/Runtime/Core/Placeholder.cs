using System.Collections.Generic;
using UnityEngine;

namespace Odyssey.Core
{
    /// <summary>
    /// Generated placeholder art (solid-colour sprites) so the repo runs with zero third-party assets.
    /// Real sprites assigned in configs override these.
    /// </summary>
    public static class Placeholder
    {
        private static Sprite _square, _circle, _triangle;
        private static Material _unlit;

        /// <summary>Unlit sprite material so placeholder art is visible without any 2D lights (URP 2D renderer).</summary>
        public static Material UnlitMaterial
        {
            get
            {
                if (_unlit != null) return _unlit;
                var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
                if (shader != null) _unlit = new Material(shader) { name = "PlaceholderUnlit" };
                return _unlit;
            }
        }

        public static Sprite Square => _square ? _square : (_square = Make("ph_square", (x, y, n) => true));
        public static Sprite Circle => _circle ? _circle : (_circle = Make("ph_circle", (x, y, n) =>
        {
            float dx = x - n / 2f + 0.5f, dy = y - n / 2f + 0.5f;
            return dx * dx + dy * dy <= n * n / 4f;
        }));
        public static Sprite Triangle => _triangle ? _triangle : (_triangle = Make("ph_triangle", (x, y, n) =>
            Mathf.Abs(x - n / 2f + 0.5f) <= (n - y) / 2f));

        private static Sprite Make(string name, System.Func<int, int, int, bool> inside)
        {
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = name };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    px[y * n + x] = inside(x, y, n) ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            tex.SetPixels32(px);
            tex.Apply();
            var s = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            s.name = name;
            return s;
        }

        /// <summary>Create a child sprite object sized in world units.</summary>
        public static SpriteRenderer AddSprite(Transform parent, string name, Sprite sprite, Color color, Vector2 size, Vector2 offset, int order = 0)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = offset;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite != null ? sprite : Square;
            sr.color = color;
            sr.sortingOrder = order;
            if (UnlitMaterial != null) sr.sharedMaterial = UnlitMaterial;
            if (sprite == null) go.transform.localScale = new Vector3(size.x, size.y, 1f);
            return sr;
        }
    }
}
