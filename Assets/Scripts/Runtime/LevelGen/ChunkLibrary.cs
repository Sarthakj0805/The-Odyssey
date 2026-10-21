using System.Collections.Generic;
using UnityEngine;

namespace Odyssey.LevelGen
{
    /// <summary>All chunks the level assembler can use: the intro, the finale (ship) and the combat chunks.</summary>
    [CreateAssetMenu(menuName = "Odyssey/Chunk Library", fileName = "ChunkLibrary")]
    public class ChunkLibrary : ScriptableObject
    {
        public ChunkDefinition Intro;
        public ChunkDefinition Finale;
        public List<ChunkDefinition> Combat = new List<ChunkDefinition>();
    }
}
