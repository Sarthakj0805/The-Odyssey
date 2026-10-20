using System;
using Odyssey.Combat;
using Odyssey.Core;
using Odyssey.Player;
using UnityEngine;

namespace Odyssey.Level
{
    /// <summary>Shrine gate at the end of a combat segment: a wall that opens after the boon choice.</summary>
    public class SegmentGate : MonoBehaviour
    {
        public int SegmentIndex;
        public event Action<SegmentGate> Reached;
        private GameObject _wall;
        private SpriteRenderer _glow;
        private bool _reached;

        public static SegmentGate Create(Transform parent, Vector2 bottomCenter, float height, int segmentIndex)
        {
            var go = new GameObject($"ShrineGate_{segmentIndex + 1}");
            go.transform.SetParent(parent, false);
            go.transform.position = bottomCenter;
            var gate = go.AddComponent<SegmentGate>();
            gate.SegmentIndex = segmentIndex;

            var trigger = go.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(1.5f, height);
            trigger.offset = new Vector2(-1.2f, height / 2f);

            gate._wall = new GameObject("Wall");
            gate._wall.transform.SetParent(go.transform, false);
            gate._wall.layer = Layers.Ground;
            var wallCol = gate._wall.AddComponent<BoxCollider2D>();
            wallCol.size = new Vector2(0.6f, height * 2f);
            wallCol.offset = new Vector2(0f, height);
            Placeholder.AddSprite(gate._wall.transform, "Bars", null, new Color(0.85f, 0.75f, 0.45f), new Vector2(0.4f, height), new Vector2(0f, height / 2f), 2);
            Placeholder.AddSprite(go.transform, "PillarL", null, new Color(0.9f, 0.9f, 0.85f), new Vector2(0.5f, 3.2f), new Vector2(-0.7f, 1.6f), 1);
            Placeholder.AddSprite(go.transform, "PillarR", null, new Color(0.9f, 0.9f, 0.85f), new Vector2(0.5f, 3.2f), new Vector2(0.7f, 1.6f), 1);
            gate._glow = Placeholder.AddSprite(go.transform, "Altar", Placeholder.Circle, new Color(1f, 0.9f, 0.4f, 0.8f), new Vector2(0.8f, 0.8f), new Vector2(-1.4f, 0.6f), 3);
            return gate;
        }

        public void Open()
        {
            if (_wall != null) _wall.SetActive(false);
            if (_glow != null) _glow.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_reached || other.GetComponentInParent<PlayerController>() == null) return;
            _reached = true;
            Reached?.Invoke(this);
        }
    }

    /// <summary>Spikes: damage + upward knock on contact.</summary>
    public class Hazard : MonoBehaviour
    {
        public float Damage = 20f;
        private void OnTriggerStay2D(Collider2D other)
        {
            var d = other.GetComponentInParent<IDamageable>();
            if (d == null || d.Team != Team.Player) return;
            d.TakeDamage(new DamageInfo(Damage, new Vector2(0f, 12f), Team.Environment, gameObject));
        }
    }

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
