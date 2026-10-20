using System;
using System.Collections.Generic;
using Odyssey.Core;
using Odyssey.Enemies;
using Odyssey.Level;
using UnityEngine;

namespace Odyssey.LevelGen
{
    /// <summary>Result of appending one chunk to the level.</summary>
    public sealed class ChunkInstance
    {
        public ChunkDefinition Definition;
        public ChunkLayout Layout;
        public Transform Root;
        public float StartX, EndX;
        public Vector2 Checkpoint;           // respawn point (left edge, standing height)
        public Vector2? PlayerStart;
        public SegmentGate Gate;
        public LevelExit Exit;
        public readonly List<EnemyController> Enemies = new List<EnemyController>();
    }

    /// <summary>
    /// Builds the level at runtime by placing chunks left-to-right. The Director's difficulty picks
    /// which chunk comes next, how strong its enemies are and how many enemy slots are filled.
    /// </summary>
    public class LevelAssembler : MonoBehaviour
    {
        public float CursorX { get; private set; }
        public event Action<EnemyController> EnemySpawned;

        private Transform _target;
        private EnemyConfig _melee, _ranged;

        public void Init(Transform playerTarget, EnemyConfig melee, EnemyConfig ranged)
        {
            _target = playerTarget; _melee = melee; _ranged = ranged;
        }

        public void SetTarget(Transform t) => _target = t;

        public ChunkInstance Append(ChunkDefinition def, int difficulty, System.Random rng, int segmentIndex)
        {
            var layout = ChunkLayout.Parse(def.Rows);
            var root = new GameObject($"Chunk_{segmentIndex + 1}_{def.Id}").transform;
            root.SetParent(transform, false);
            root.position = new Vector3(CursorX, 0f, 0f);

            var inst = new ChunkInstance
            {
                Definition = def, Layout = layout, Root = root,
                StartX = CursorX, EndX = CursorX + layout.Width,
                Checkpoint = new Vector2(CursorX + 1.5f, layout.StandHeight(1) + 0.8f)
            };

            foreach (var r in layout.Solids) MakeBlock(root, r, def.GroundTint);
            foreach (var s in layout.Spikes) MakeSpike(root, s);

            if (layout.PlayerStart.HasValue)
                inst.PlayerStart = new Vector2(CursorX + layout.PlayerStart.Value.x + 0.5f, layout.PlayerStart.Value.y + 0.8f);

            // enemies: difficulty sets both how many slots are filled and how tough the enemies are
            float tier = ChunkSelector.TierMultiplier[Difficulty(difficulty)];
            var slots = new List<(Vector2Int cell, EnemyConfig cfg)>();
            foreach (var c in layout.MeleeSlots) slots.Add((c, _melee));
            foreach (var c in layout.RangedSlots) slots.Add((c, _ranged));
            foreach (int i in ChunkSelector.ChooseSlots(slots.Count, difficulty, rng))
                inst.Enemies.Add(SpawnEnemy(root, slots[i].cell, slots[i].cfg, tier));

            if (def.Kind == ChunkKind.Combat)
                inst.Gate = SegmentGate.Create(root, new Vector2(inst.EndX - 0.5f, layout.StandHeight(layout.Width - 1)), layout.Height, segmentIndex);
            if (layout.Exit.HasValue)
                inst.Exit = LevelExit.Create(root, new Vector2(CursorX + layout.Exit.Value.x + 0.5f, layout.Exit.Value.y + 0.5f));

            CursorX = inst.EndX;
            return inst;
        }

        private static int Difficulty(int d) => Mathf.Clamp(d, 0, 2);

        private EnemyController SpawnEnemy(Transform root, Vector2Int cell, EnemyConfig cfg, float tier)
        {
            var go = new GameObject(cfg.DisplayName);
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(root.position.x + cell.x + 0.5f, cell.y + cfg.Size.y / 2f + 0.05f, 0f);
            var e = go.AddComponent<EnemyController>();
            e.Init(cfg, tier, _target);
            EnemySpawned?.Invoke(e);
            return e;
        }

        private static void MakeBlock(Transform root, RectInt r, Color tint)
        {
            var go = new GameObject("Block");
            go.transform.SetParent(root, false);
            go.layer = Layers.Ground;
            go.transform.localPosition = new Vector3(r.x + r.width / 2f, r.y + r.height / 2f, 0f);
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(r.width, r.height);
            Placeholder.AddSprite(go.transform, "Visual", null, tint, new Vector2(r.width, r.height), Vector2.zero, 0);
            Placeholder.AddSprite(go.transform, "Top", null, Color.Lerp(tint, new Color(0.9f, 0.85f, 0.55f), 0.5f),
                new Vector2(r.width, 0.15f), new Vector2(0f, r.height / 2f - 0.075f), 1);
        }

        private static void MakeSpike(Transform root, Vector2Int c)
        {
            var go = new GameObject("Spikes");
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(c.x + 0.5f, c.y + 0.3f, 0f);
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(0.9f, 0.6f);
            go.AddComponent<Hazard>();
            Placeholder.AddSprite(go.transform, "Visual", Placeholder.Triangle, new Color(0.8f, 0.8f, 0.85f), new Vector2(1f, 0.6f), Vector2.zero, 2);
        }
    }
}
