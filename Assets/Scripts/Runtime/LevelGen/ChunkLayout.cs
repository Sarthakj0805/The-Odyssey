using System;
using System.Collections.Generic;
using UnityEngine;

namespace Odyssey.LevelGen
{
    /// <summary>
    /// Parsed, engine-independent view of a chunk map. Solid cells are merged into as few
    /// rectangles as possible (horizontal runs, then stacked vertically) to keep collider count low.
    /// Coordinates: x = column, y = height from the bottom (row 0 of the map is the top).
    /// </summary>
    public sealed class ChunkLayout
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public readonly List<RectInt> Solids = new List<RectInt>();
        public readonly List<RectInt> OneWays = new List<RectInt>();
        public readonly List<Vector2Int> Spikes = new List<Vector2Int>();
        public readonly List<Vector2Int> MeleeSlots = new List<Vector2Int>();
        public readonly List<Vector2Int> RangedSlots = new List<Vector2Int>();
        public Vector2Int? PlayerStart, Boss, Exit;
        public int? TriggerColumn;

        private bool[,] _solid;

        public static ChunkLayout Parse(string[] rows)
        {
            if (rows == null || rows.Length == 0) throw new ArgumentException("empty map");
            int h = rows.Length, w = 0;
            foreach (var r in rows) w = Math.Max(w, r.Length);

            var l = new ChunkLayout { Width = w, Height = h, _solid = new bool[w, h] };
            var oneWay = new bool[w, h];
            for (int row = 0; row < h; row++)
            {
                int y = h - 1 - row;
                string line = rows[row];
                for (int x = 0; x < line.Length; x++)
                {
                    var cell = new Vector2Int(x, y);
                    switch (line[x])
                    {
                        case '#': l._solid[x, y] = true; break;
                        case '=': oneWay[x, y] = true; break;
                        case '^': l.Spikes.Add(cell); break;
                        case 'm': l.MeleeSlots.Add(cell); break;
                        case 'r': l.RangedSlots.Add(cell); break;
                        case 'B': l.Boss = cell; break;
                        case 'P': l.PlayerStart = cell; break;
                        case 'X': l.Exit = cell; break;
                        case 'T': l.TriggerColumn = x; break;
                    }
                }
            }
            l.Solids.AddRange(MergeRects(l._solid, w, h));
            l.OneWays.AddRange(MergeRects(oneWay, w, h));
            return l;
        }

        public bool IsSolid(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height && _solid[x, y];

        /// <summary>Height (cells from bottom) of the first free cell above the highest solid in a column.</summary>
        public int StandHeight(int column)
        {
            for (int y = Height - 1; y >= 0; y--)
                if (IsSolid(column, y)) return y + 1;
            return 1;
        }

        private static List<RectInt> MergeRects(bool[,] grid, int w, int h)
        {
            // 1) horizontal runs per row
            var runs = new List<RectInt>();
            for (int y = 0; y < h; y++)
            {
                int x = 0;
                while (x < w)
                {
                    if (!grid[x, y]) { x++; continue; }
                    int start = x;
                    while (x < w && grid[x, y]) x++;
                    runs.Add(new RectInt(start, y, x - start, 1));
                }
            }
            // 2) stack identical runs from consecutive rows
            runs.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.width != b.width ? a.width.CompareTo(b.width) : a.y.CompareTo(b.y));
            var merged = new List<RectInt>();
            foreach (var r in runs)
            {
                if (merged.Count > 0)
                {
                    var last = merged[merged.Count - 1];
                    if (last.x == r.x && last.width == r.width && last.y + last.height == r.y)
                    {
                        merged[merged.Count - 1] = new RectInt(last.x, last.y, last.width, last.height + 1);
                        continue;
                    }
                }
                merged.Add(r);
            }
            return merged;
        }
    }

    /// <summary>Deterministic (seeded) chunk + enemy-slot selection. No Unity objects: unit-testable.</summary>
    public static class ChunkSelector
    {
        /// <summary>Fraction of enemy slots filled for density 0/1/2.</summary>
        public static readonly float[] DensityFill = { 0.4f, 0.7f, 1.0f };
        /// <summary>Enemy HP/damage multiplier for difficulty 0/1/2.</summary>
        public static readonly float[] TierMultiplier = { 0.8f, 1.0f, 1.3f };

        public static ChunkDefinition Pick(IList<ChunkDefinition> combat, int difficulty, System.Random rng, ChunkDefinition avoid = null)
        {
            var candidates = new List<ChunkDefinition>();
            foreach (var c in combat) if (c != null && c.Difficulty == difficulty) candidates.Add(c);
            if (candidates.Count == 0) foreach (var c in combat) if (c != null) candidates.Add(c);
            if (candidates.Count == 0) return null;
            if (candidates.Count > 1 && avoid != null) candidates.Remove(avoid);
            return candidates[rng.Next(candidates.Count)];
        }

        /// <summary>Choose which slots get an enemy (at least one when any slot exists).</summary>
        public static List<int> ChooseSlots(int slotCount, int density, System.Random rng)
        {
            var idx = new List<int>();
            for (int i = 0; i < slotCount; i++) idx.Add(i);
            if (slotCount == 0) return idx;
            int n = Math.Max(1, (int)Math.Round(slotCount * DensityFill[Math.Max(0, Math.Min(2, density))]));
            for (int i = idx.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (idx[i], idx[j]) = (idx[j], idx[i]); }
            idx.RemoveRange(n, idx.Count - n);
            idx.Sort();
            return idx;
        }
    }
}
