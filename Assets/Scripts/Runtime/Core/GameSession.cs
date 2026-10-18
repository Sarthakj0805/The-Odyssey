using Odyssey.Director;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Odyssey.Core
{
    /// <summary>Cross-scene state: chosen Director and the result shown on the end screen.</summary>
    public static class GameSession
    {
        public const string MainMenuScene = "MainMenu", LevelScene = "Level01", EndScene = "EndScreen";
        private const string DirectorPref = "odyssey.director";

        public static DirectorKind Director
        {
            get => (DirectorKind)PlayerPrefs.GetInt(DirectorPref, (int)DirectorKind.RL);
            set { PlayerPrefs.SetInt(DirectorPref, (int)value); PlayerPrefs.Save(); }
        }

        public struct RunResult
        {
            public bool Victory;
            public float Seconds;
            public int Kills, Deaths, Boons, SegmentsCleared;
            public string DirectorName;
        }

        public static RunResult LastResult;

        public static void Load(string scene)
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(scene);
        }
    }
}
