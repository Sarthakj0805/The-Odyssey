using System.Collections.Generic;
using UnityEngine;

namespace Odyssey.LevelGen
{
    public enum ChunkKind { Intro, Combat, Finale }

    /// <summary>
    /// A handcrafted level chunk drawn as an ASCII map (row 0 = top). Legend:
    ///   #  solid block     ^  spikes     m  melee enemy slot     r  ranged enemy slot
    ///   P  player start    X  exit (ship)     .  empty
    /// Combat chunks get a checkpoint on their left edge and a shrine gate on their right edge.
    /// </summary>
    [CreateAssetMenu(menuName = "Odyssey/Chunk", fileName = "Chunk")]
    public class ChunkDefinition : ScriptableObject
    {
        public string Id = "chunk";
        public ChunkKind Kind = ChunkKind.Combat;
        [Range(0, 2)] public int Difficulty = 1;
        public Color GroundTint = new Color(0.45f, 0.38f, 0.3f);
        [TextArea(12, 20)] public string Map = "";

        public string[] Rows => Map.Replace("\r", "").Trim('\n').Split('\n');
    }
}
