using Odyssey.Core;
using Odyssey.Director;
using UnityEngine;
using UnityEngine.UI;

namespace Odyssey.UI
{
    /// <summary>Title screen: Start, AI Director selector (RL / Heuristic / Fixed), Quit.</summary>
    public class MainMenuController : MonoBehaviour
    {
        private Text _directorLabel;

        private void Start()
        {
            Time.timeScale = 1f;
            var c = UIFactory.Canvas("MainMenu", 0).transform;
            UIFactory.Panel(c, "Bg", new Color(0.05f, 0.09f, 0.18f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            UIFactory.Panel(c, "Sea", new Color(0.1f, 0.25f, 0.4f), Vector2.zero, UIFactory.V(1, 0.3f), Vector2.zero, Vector2.zero);
            UIFactory.Label(c, "Title", "THE ODYSSEY", 120, TextAnchor.MiddleCenter, UIFactory.Gold, UIFactory.V(0, 0.68f), UIFactory.V(1, 0.88f), Vector2.zero, Vector2.zero, FontStyle.Bold);
            UIFactory.Label(c, "Sub", "Book IX — The Island of the Cyclops", 40, TextAnchor.MiddleCenter, UIFactory.Parchment, UIFactory.V(0, 0.6f), UIFactory.V(1, 0.68f), Vector2.zero, Vector2.zero, FontStyle.Italic);

            UIFactory.Button(c, "Start", "Set sail", () => GameSession.Load(GameSession.LevelScene), UIFactory.V(0.38f, 0.44f), UIFactory.V(0.62f, 0.53f), Vector2.zero, Vector2.zero, 42);
            var dir = UIFactory.Button(c, "Director", "", CycleDirector, UIFactory.V(0.38f, 0.33f), UIFactory.V(0.62f, 0.41f), Vector2.zero, Vector2.zero, 30);
            _directorLabel = dir.GetComponentInChildren<Text>();
            UIFactory.Button(c, "Quit", "Quit", Quit, UIFactory.V(0.38f, 0.22f), UIFactory.V(0.62f, 0.3f), Vector2.zero, Vector2.zero, 34);
            UIFactory.Label(c, "Note", "Beta — placeholder art. The AI Director adapts each segment to how you play.", 24, TextAnchor.LowerCenter,
                new Color(1, 1, 1, 0.5f), Vector2.zero, UIFactory.V(1, 0), UIFactory.V(0, 20), UIFactory.V(0, 60));
            RefreshDirector();
        }

        private void CycleDirector()
        {
            GameSession.Director = (DirectorKind)(((int)GameSession.Director + 1) % 3);
            RefreshDirector();
        }

        private void RefreshDirector() => _directorLabel.text = $"AI Director: {GameSession.Director}";

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
