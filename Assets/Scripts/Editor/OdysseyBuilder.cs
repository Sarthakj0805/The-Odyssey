using System.Collections.Generic;
using System.IO;
using System.Linq;
using Odyssey.Core;
using Odyssey.Dialogue;
using Odyssey.Director.ML;
using Odyssey.Enemies;
using Odyssey.LevelGen;
using Odyssey.Modifiers;
using Odyssey.Player;
using Odyssey.UI;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Odyssey.EditorTools
{
    /// <summary>
    /// One-click project setup: physics layers, all ScriptableObject data, and the five scenes.
    /// Idempotent: existing data assets are kept (so Inspector tweaks survive) unless you use
    /// "Rebuild Data (overwrite)". Scenes are always regenerated - they only hold bootstrap objects.
    /// </summary>
    public static class OdysseyBuilder
    {
        private const string Data = "Assets/Data";
        private const string ScenesDir = "Assets/Scenes";
        public const string ModelPath = "Assets/ML/Models/Director.onnx";
        private const string BehaviorName = "Director";
        private const int TrainingAgents = 16;

        [MenuItem("Odyssey/Build Project Content", priority = 0)]
        public static void BuildAll() => Build(false);

        [MenuItem("Odyssey/Rebuild Data (overwrite)", priority = 1)]
        public static void RebuildData()
        {
            if (EditorUtility.DisplayDialog("Overwrite data?", "Reset every Odyssey data asset (boons, enemies, chunks, dialogue, player) to defaults?", "Overwrite", "Cancel"))
                Build(true);
        }

        [MenuItem("Odyssey/Assign Director Model To Scenes", priority = 2)]
        public static void AssignModelMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildScenes();
            Debug.Log(File.Exists(ModelPath) ? $"Director model assigned from {ModelPath}" : $"No model at {ModelPath} - RL director will use the heuristic fallback.");
        }

        [MenuItem("Odyssey/Run Director Evaluation", priority = 20)]
        public static void RunEvaluation()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene($"{ScenesDir}/DirectorEval.unity");
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Odyssey/Open Training Scene", priority = 21)]
        public static void OpenTraining()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene($"{ScenesDir}/DirectorTraining.unity");
        }

        private static void Build(bool overwrite)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureLayers();
            var db = BuildData(overwrite);
            BuildScenes();
            PlayerSettings.productName = "The Odyssey";
            PlayerSettings.companyName = "Sarthak Jain";
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene($"{ScenesDir}/MainMenu.unity");
            Debug.Log($"[Odyssey] Build complete: {db.Boons.Count} boons, {db.Chunks.Combat.Count} combat chunks, 5 scenes. Press Play in MainMenu.");
        }

        // ------------------------------------------------------------------ layers
        private static void EnsureLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            foreach (var name in new[] { Layers.GroundName, Layers.PlayerName, Layers.EnemyName, Layers.ProjectileName })
            {
                bool exists = false;
                for (int i = 0; i < layers.arraySize; i++)
                    if (layers.GetArrayElementAtIndex(i).stringValue == name) exists = true;
                if (exists) continue;
                for (int i = 6; i < layers.arraySize; i++)
                {
                    var p = layers.GetArrayElementAtIndex(i);
                    if (string.IsNullOrEmpty(p.stringValue)) { p.stringValue = name; break; }
                }
            }
            tagManager.ApplyModifiedProperties();
        }

        // ------------------------------------------------------------------ data
        private static T Asset<T>(string path, bool overwrite, System.Action<T> init) where T : ScriptableObject
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null && File.Exists(path)) AssetDatabase.DeleteAsset(path);   // stale asset with a broken script link
            bool created = a == null;
            if (created) { a = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(a, path); }
            if (created || overwrite) { init(a); EditorUtility.SetDirty(a); }
            return a;
        }

        private static GameDatabase BuildData(bool ow)
        {
            var player = Asset<PlayerConfig>($"{Data}/PlayerConfig.asset", ow, p => { });

            var melee = Asset<EnemyConfig>($"{Data}/Enemies/CyclopsBrute.asset", ow, e =>
            {
                e.DisplayName = "Cyclops Brute"; e.Kind = EnemyKind.Melee; e.Size = new Vector2(0.9f, 1.4f);
                e.Tint = new Color(0.68f, 0.36f, 0.3f); e.MaxHealth = 30; e.Damage = 12; e.PatrolSpeed = 1.5f; e.ChaseSpeed = 3.2f;
                e.AggroRange = 7; e.AttackRange = 1.4f; e.AttackCooldown = 1.2f; e.Windup = 0.45f; e.Active = 0.15f; e.Recovery = 0.4f;
            });
            var ranged = Asset<EnemyConfig>($"{Data}/Enemies/RockHurler.asset", ow, e =>
            {
                e.DisplayName = "Rock Hurler"; e.Kind = EnemyKind.Ranged; e.Size = new Vector2(0.8f, 1.2f);
                e.Tint = new Color(0.5f, 0.45f, 0.62f); e.MaxHealth = 20; e.Damage = 10; e.PatrolSpeed = 0; e.ChaseSpeed = 0;
                e.AggroRange = 11; e.AttackRange = 11; e.AttackCooldown = 2.2f; e.Windup = 0.6f; e.Active = 0.05f; e.Recovery = 0.6f;
                e.Knockback = new Vector2(5, 3); e.ProjectileFlightTime = 1.0f; e.ProjectileGravity = 1.2f;
            });
            var boons = new List<BoonDefinition>();
            foreach (var b in BoonTable)
                boons.Add(Asset<BoonDefinition>($"{Data}/Boons/{b.id}.asset", ow, x =>
                {
                    x.Id = b.id; x.DisplayName = b.name; x.God = b.god; x.Description = b.desc;
                    x.MaxStacks = 3; x.Tint = b.tint; x.Modifiers = b.mods;
                }));

            var gold = new Color(0.95f, 0.8f, 0.4f);
            var grey = new Color(0.75f, 0.85f, 1f);
            var opening = Asset<DialogueSequence>($"{Data}/Dialogue/Opening.asset", ow, d => d.Lines = new[]
            {
                new DialogueLine("Narrator", "Troy has fallen. After ten years of war, Odysseus and his crew sail for Ithaca - but the winds carry them to a wild, unknown island.", grey),
                new DialogueLine("Odysseus", "Ten years of war... and now only the sea stands between us and home.", gold),
                new DialogueLine("Eurylochus", "Captain, this island reeks of sheep and giants. The men are uneasy.", new Color(0.7f, 1f, 0.7f)),
                new DialogueLine("Odysseus", "We need food and water. Stay close, and keep your spears ready.", gold),
                new DialogueLine("Narrator", "Fight across the island, honour the gods at their shrines, and reach the ship before the Cyclops finds you.", grey),
            });
            var ending = Asset<DialogueSequence>($"{Data}/Dialogue/Ending.asset", ow, d => d.Lines = new[]
            {
                new DialogueLine("Odysseus", "To the ships! Row, and do not look back!", gold),
                new DialogueLine("Narrator", "Behind them, the Cyclops roars across the water. Ithaca is still far away...", grey),
            });

            var library = Asset<ChunkLibrary>($"{Data}/Chunks/ChunkLibrary.asset", ow, l => { });
            var combat = new List<ChunkDefinition>();
            foreach (var e in DefaultChunks.All)
            {
                var c = Asset<ChunkDefinition>($"{Data}/Chunks/{e.Id}.asset", ow, x =>
                {
                    x.Id = e.Id; x.Kind = e.Kind; x.Difficulty = e.Difficulty; x.GroundTint = e.Tint; x.Map = e.Map.Trim('\r', '\n');
                });
                if (e.Kind == ChunkKind.Intro) library.Intro = c;
                else if (e.Kind == ChunkKind.Finale) library.Finale = c;
                else combat.Add(c);
            }
            if (ow || library.Combat.Count == 0) library.Combat = combat;
            EditorUtility.SetDirty(library);

            var db = Asset<GameDatabase>("Assets/Resources/GameDatabase.asset", ow, x => { });
            db.Player = player; db.MeleeEnemy = melee; db.RangedEnemy = ranged;
            if (ow || db.Boons.Count == 0) db.Boons = boons;
            db.Chunks = library; db.Opening = opening; db.Ending = ending;
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            return db;
        }

        private struct BoonRow
        {
            public string id, name, god, desc; public Color tint; public StatModifier[] mods;
        }

        private static BoonRow Boon(string id, string name, string god, Color tint, string desc, params StatModifier[] mods) =>
            new BoonRow { id = id, name = name, god = god, tint = tint, desc = desc, mods = mods };

        private static StatModifier M(StatType s, ModifierOp op, float v) => new StatModifier(s, op, v);

        /// <summary>Six Boons of the Gods, one per god.</summary>
        private static readonly BoonRow[] BoonTable =
        {
            Boon("athena", "Athena's Insight", "Athena", new Color(0.7f, 0.8f, 1f), "+25% spear damage", M(StatType.Damage, ModifierOp.PercentAdd, 0.25f)),
            Boon("hermes", "Winged Sandals", "Hermes", new Color(0.6f, 1f, 0.9f), "+15% move speed", M(StatType.MoveSpeed, ModifierOp.PercentAdd, 0.15f)),
            Boon("apollo", "Apollo's Tempo", "Apollo", new Color(1f, 0.85f, 0.4f), "+20% attack speed", M(StatType.AttackSpeed, ModifierOp.PercentAdd, 0.20f)),
            Boon("hera", "Hera's Resolve", "Hera", new Color(1f, 0.6f, 0.8f), "+25 max health", M(StatType.MaxHealth, ModifierOp.Flat, 25f)),
            Boon("ares", "Ares' Bloodlust", "Ares", new Color(1f, 0.4f, 0.35f), "Heal 5 HP on every kill", M(StatType.HealOnKill, ModifierOp.Flat, 5f)),
            Boon("poseidon", "Undertow", "Poseidon", new Color(0.4f, 0.7f, 1f), "Hits knock enemies back 60% further", M(StatType.Knockback, ModifierOp.PercentAdd, 0.6f)),
        };

        // ------------------------------------------------------------------ scenes
        public static void BuildScenes()
        {
            Directory.CreateDirectory(ScenesDir);
            var scenes = new List<EditorBuildSettingsScene>
            {
                MakeScene("MainMenu", new Color(0.05f, 0.09f, 0.18f), s => new GameObject("MainMenu").AddComponent<MainMenuController>()),
                MakeScene("Level01", new Color(0.45f, 0.68f, 0.88f), s =>
                {
                    new GameObject("RunManager").AddComponent<RunManager>();
                    MakeAgent("DirectorAgent", DirectorAgent.Mode.Gameplay, 1, deterministic: true);
                }),
                MakeScene("EndScreen", Color.black, s => new GameObject("EndScreen").AddComponent<EndScreenController>()),
                MakeScene("DirectorTraining", new Color(0.1f, 0.1f, 0.12f), s =>
                {
                    var arena = new GameObject("TrainingArena").AddComponent<DirectorTrainingArena>();
                    for (int i = 0; i < TrainingAgents; i++)
                    {
                        var a = MakeAgent($"DirectorAgent_{i:00}", DirectorAgent.Mode.Training, 101 + i, deterministic: false, assignModel: false);
                        a.transform.SetParent(arena.transform);
                        var req = a.gameObject.AddComponent<DecisionRequester>();
                        req.DecisionPeriod = 1;
                        req.TakeActionsBetweenDecisions = false;
                    }
                }),
                MakeScene("DirectorEval", new Color(0.1f, 0.1f, 0.12f), s =>
                {
                    var agent = MakeAgent("DirectorAgent", DirectorAgent.Mode.Gameplay, 7, deterministic: true);
                    var ev = new GameObject("DirectorEvaluator").AddComponent<DirectorEvaluator>();
                    var so = new SerializedObject(ev);
                    so.FindProperty("agent").objectReferenceValue = agent;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }),
            };
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static EditorBuildSettingsScene MakeScene(string name, Color bg, System.Action<Scene> populate)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 7f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = bg;
            camGo.transform.position = new Vector3(0f, 5f, -10f);
            camGo.AddComponent<AudioListener>();
            populate(scene);
            string path = $"{ScenesDir}/{name}.unity";
            EditorSceneManager.SaveScene(scene, path);
            return new EditorBuildSettingsScene(path, true);
        }

        private static DirectorAgent MakeAgent(string name, DirectorAgent.Mode mode, int seed, bool deterministic, bool assignModel = true)
        {
            var go = new GameObject(name);
            var bp = go.AddComponent<BehaviorParameters>();
            bp.BehaviorName = BehaviorName;
            bp.BrainParameters.VectorObservationSize = Odyssey.Director.DirectorObservation.Size;
            bp.BrainParameters.ActionSpec = ActionSpec.MakeDiscrete(3);   // easy / medium / hard
            bp.BehaviorType = BehaviorType.Default;
            bp.DeterministicInference = deterministic;
            var agent = go.AddComponent<DirectorAgent>();
            agent.Configure(mode, seed);
            if (assignModel && File.Exists(ModelPath))
            {
                var model = AssetDatabase.LoadMainAssetAtPath(ModelPath);
                var so = new SerializedObject(bp);
                so.FindProperty("m_Model").objectReferenceValue = model;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(bp);
            EditorUtility.SetDirty(agent);
            return agent;
        }
    }
}
