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
        public DialogueTrigger Trigger;
        public readonly List<EnemyController> Enemies = new List<EnemyController>();
        public float EnemyHpSpawned;
        public EnemyController Boss;
    }

    /// <summary>
    /// Builds the level at runtime by stitching chunks left-to-right. The Director's decision
    /// picks the chunk pool (difficulty), the enemy tier and how many enemy slots are filled.
    /// </summary>
    public class LevelAssembler : MonoBehaviour
    {
        public float CursorX { get; private set; }
        public event Action<EnemyController> EnemySpawned;

        private Transform _target;
        private EnemyConfig _melee, _ranged, _boss;

        public void Init(Transform playerTarget, EnemyConfig melee, EnemyConfig ranged, EnemyConfig boss)
        {
            _target = playerTarget; _melee = melee; _ranged = ranged; _boss = boss;
        }

        public void SetTarget(Transform t) => _target = t;

        public ChunkInstance Append(ChunkDefinition def, int difficulty, int density, System.Random rng, int segmentIndex)
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

            // terrain
            foreach (var r in layout.Solids) MakeBlock(root, r, def.GroundTint, false);
            foreach (var r in layout.OneWays) MakeBlock(root, r, Color.Lerp(def.GroundTint, Color.white, 0.25f), true);
            foreach (var s in layout.Spikes) MakeSpike(root, s);

            if (layout.PlayerStart.HasValue)
                inst.PlayerStart = new Vector2(CursorX + layout.PlayerStart.Value.x + 0.5f, layout.PlayerStart.Value.y + 0.8f);

            // enemies
            float tier = ChunkSelector.TierMultiplier[Mathf.Clamp(difficulty, 0, 2)];
            var slots = new List<(Vector2Int cell, EnemyConfig cfg)>();
            foreach (var c in layout.MeleeSlots) slots.Add((c, _melee));
            foreach (var c in layout.RangedSlots) slots.Add((c, _ranged));
            foreach (int i in ChunkSelector.ChooseSlots(slots.Count, density, rng))
                inst.Enemies.Add(SpawnEnemy(root, slots[i].cell, slots[i].cfg, tier));
            if (layout.Boss.HasValue && _boss != null)
            {
                inst.Boss = SpawnEnemy(root, layout.Boss.Value, _boss, 1f);
                inst.Enemies.Add(inst.Boss);
            }
            foreach (var e in inst.Enemies) inst.EnemyHpSpawned += e.Health.Model.Max;

            // flow pieces
            if (def.Kind == ChunkKind.Combat)
                inst.Gate = SegmentGate.Create(root, new Vector2(inst.EndX - 0.5f, layout.StandHeight(layout.Width - 1)), layout.Height, segmentIndex);
            if (layout.Exit.HasValue)
                inst.Exit = LevelExit.Create(root, new Vector2(CursorX + layout.Exit.Value.x + 0.5f, layout.Exit.Value.y + 0.5f), inst.Boss != null);
            if (layout.TriggerColumn.HasValue && def.Dialogue != null)
            {
                var go = new GameObject("DialogueTrigger");
                go.transform.SetParent(root, false);
                go.transform.position = new Vector3(CursorX + layout.TriggerColumn.Value + 0.5f, layout.Height / 2f, 0f);
                var col = go.AddComponent<BoxCollider2D>();
                col.isTrigger = true;
                col.size = new Vector2(1f, layout.Height * 2f);
                inst.Trigger = go.AddComponent<DialogueTrigger>();
                inst.Trigger.Sequence = def.Dialogue;
            }

            CursorX = inst.EndX;
            return inst;
        }

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

        private static void MakeBlock(Transform root, RectInt r, Color tint, bool oneWay)
        {
            var go = new GameObject(oneWay ? "Platform" : "Ground");
            go.transform.SetParent(root, false);
            go.layer = Layers.Ground;
            float h = oneWay ? 0.35f : r.height;
            go.transform.localPosition = new Vector3(r.x + r.width / 2f, oneWay ? r.y + 1f - h / 2f : r.y + r.height / 2f, 0f);
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(r.width, h);
            if (oneWay)
            {
                col.usedByEffector = true;
                var eff = go.AddComponent<PlatformEffector2D>();
                eff.useOneWay = true;
                eff.surfaceArc = 170f;
            }
            Placeholder.AddSprite(go.transform, "Visual", null, tint, new Vector2(r.width, h), Vector2.zero, 0);
            if (!oneWay) // grass/sand top strip for readability
                Placeholder.AddSprite(go.transform, "Top", null, Color.Lerp(tint, new Color(0.9f, 0.85f, 0.55f), 0.5f),
                    new Vector2(r.width, 0.15f), new Vector2(0f, h / 2f - 0.075f), 1);
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
