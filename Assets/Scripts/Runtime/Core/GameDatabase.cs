using System.Collections.Generic;
using Odyssey.Dialogue;
using Odyssey.Enemies;
using Odyssey.LevelGen;
using Odyssey.Modifiers;
using Odyssey.Player;
using UnityEngine;

namespace Odyssey.Core
{
    /// <summary>Single entry point to all game data assets (lives in Resources/GameDatabase).</summary>
    [CreateAssetMenu(menuName = "Odyssey/Game Database", fileName = "GameDatabase")]
    public class GameDatabase : ScriptableObject
    {
        public PlayerConfig Player;
        public EnemyConfig MeleeEnemy;
        public EnemyConfig RangedEnemy;
        public List<BoonDefinition> Boons = new List<BoonDefinition>();
        public ChunkLibrary Chunks;
        public DialogueSequence Opening;
        public DialogueSequence Ending;

        [Header("Run rules (keep in sync with the Director simulator)")]
        public int Segments = Director.Sim.RunSimulator.SegmentsPerRun;
        public int Lives = Director.Sim.RunSimulator.MaxDeaths;
        public float ParSecondsPerSegment = Director.Sim.RunSimulator.ParSeconds;
        [Range(0f, 1f)] public float ShrineHeal = Director.Sim.RunSimulator.ShrineHeal;

        private static GameDatabase _instance;
        public static GameDatabase Load()
        {
            if (_instance == null) _instance = Resources.Load<GameDatabase>("GameDatabase");
            if (_instance == null) Debug.LogError("Resources/GameDatabase.asset missing. Run menu: Odyssey > Build Project Content.");
            return _instance;
        }
    }
}
