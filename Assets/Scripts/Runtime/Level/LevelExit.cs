using System;
using Odyssey.Combat;
using Odyssey.Core;
using Odyssey.Player;
using UnityEngine;

namespace Odyssey.Level
{
    /// <summary>Odysseus' ship at the end of the level: touching it wins the run.</summary>
    public class LevelExit : MonoBehaviour
    {
        public event Action Reached;
        private bool _reached;

        public static LevelExit Create(Transform parent, Vector2 position)
        {
            var go = new GameObject("Ship");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var exit = go.AddComponent<LevelExit>();
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(3f, 4f);
            col.offset = new Vector2(0f, 1.5f);
            Placeholder.AddSprite(go.transform, "Hull", null, new Color(0.45f, 0.28f, 0.15f), new Vector2(4f, 1f), new Vector2(0f, 0f), 2);
            Placeholder.AddSprite(go.transform, "Mast", null, new Color(0.35f, 0.22f, 0.12f), new Vector2(0.2f, 3.5f), new Vector2(0f, 2f), 1);
            Placeholder.AddSprite(go.transform, "Sail", Placeholder.Triangle, new Color(1f, 0.95f, 0.8f), new Vector2(2f, 2.4f), new Vector2(0.2f, 2.5f), 3);
            return exit;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_reached || other.GetComponentInParent<PlayerController>() == null) return;
            _reached = true;
            Reached?.Invoke();
        }
    }
}
