using NUnit.Framework;
using Odyssey.LevelGen;
using UnityEngine;

namespace Odyssey.Tests
{
    public class LevelGenTests
    {
        private static readonly string[] Map =
        {
            "..........",
            "...r......",
            "..###.....",
            "m.......m.",
            "####..####",
            "####..####",
        };

        [Test]
        public void Parse_Merges_Solid_Cells_Into_Rectangles()
        {
            var l = ChunkLayout.Parse(Map);
            Assert.AreEqual(10, l.Width);
            Assert.AreEqual(6, l.Height);
            // two 4x2 ground blocks + one 3x1 floating block
            Assert.AreEqual(3, l.Solids.Count);
            CollectionAssert.Contains(l.Solids, new RectInt(0, 0, 4, 2));
            CollectionAssert.Contains(l.Solids, new RectInt(6, 0, 4, 2));
            CollectionAssert.Contains(l.Solids, new RectInt(2, 3, 3, 1));
        }

        [Test]
        public void Parse_Finds_Slots_And_Stand_Height()
        {
            var l = ChunkLayout.Parse(Map);
            Assert.AreEqual(2, l.MeleeSlots.Count);
            Assert.AreEqual(1, l.RangedSlots.Count);
            Assert.AreEqual(new Vector2Int(3, 4), l.RangedSlots[0]);
            Assert.AreEqual(2, l.StandHeight(0));
            Assert.AreEqual(4, l.StandHeight(3));
        }

        [Test]
        public void Slot_Selection_Is_Deterministic_And_Scales_With_Density()
        {
            var a = ChunkSelector.ChooseSlots(10, 1, new System.Random(7));
            var b = ChunkSelector.ChooseSlots(10, 1, new System.Random(7));
            CollectionAssert.AreEqual(a, b);
            Assert.AreEqual(4, ChunkSelector.ChooseSlots(10, 0, new System.Random(1)).Count);
            Assert.AreEqual(7, a.Count);
            Assert.AreEqual(10, ChunkSelector.ChooseSlots(10, 2, new System.Random(1)).Count);
            Assert.AreEqual(1, ChunkSelector.ChooseSlots(1, 0, new System.Random(1)).Count, "at least one enemy");
        }

        [Test]
        public void Chunk_Pick_Respects_Difficulty_And_Avoids_Repeats()
        {
            var easy1 = ScriptableObject.CreateInstance<ChunkDefinition>(); easy1.Difficulty = 0;
            var easy2 = ScriptableObject.CreateInstance<ChunkDefinition>(); easy2.Difficulty = 0;
            var hard = ScriptableObject.CreateInstance<ChunkDefinition>(); hard.Difficulty = 2;
            var lib = new System.Collections.Generic.List<ChunkDefinition> { easy1, easy2, hard };
            var rng = new System.Random(3);
            for (int i = 0; i < 20; i++)
            {
                Assert.AreSame(hard, ChunkSelector.Pick(lib, 2, rng));
                Assert.AreSame(easy2, ChunkSelector.Pick(lib, 0, rng, avoid: easy1));
            }
            Object.DestroyImmediate(easy1); Object.DestroyImmediate(easy2); Object.DestroyImmediate(hard);
        }
    }
}
