using Odyssey.Core;
using Odyssey.Director;
using UnityEngine;
using UnityEngine.UI;

namespace Odyssey.UI
{
    /// <summary>Victory / defeat screen with run stats.</summary>
    public class EndScreenController : MonoBehaviour
    {
        private void Start()
        {
            Time.timeScale = 1f;
            var r = GameSession.LastResult;
            var c = UIFactory.Canvas("EndScreen", 0).transform;
            UIFactory.Panel(c, "Bg", r.Victory ? new Color(0.06f, 0.12f, 0.2f) : new Color(0.15f, 0.05f, 0.05f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            string title = r.Victory ? "The ships sail on…" : "Your journey ends here";
            string sub = r.Victory ? "The Cyclops' island falls behind. Ithaca is still far away.  — To be continued —"
                                   : "The Cyclops' island claims another crew.";
            UIFactory.Label(c, "Title", title, 90, TextAnchor.MiddleCenter, UIFactory.Gold, UIFactory.V(0, 0.68f), UIFactory.V(1, 0.85f), Vector2.zero, Vector2.zero, FontStyle.Bold);
            UIFactory.Label(c, "Sub", sub, 36, TextAnchor.MiddleCenter, UIFactory.Parchment, UIFactory.V(0.1f, 0.6f), UIFactory.V(0.9f, 0.68f), Vector2.zero, Vector2.zero, FontStyle.Italic);
            UIFactory.Label(c, "Stats",
                $"Time {r.Seconds:0}s    Segments cleared {r.SegmentsCleared}    Kills {r.Kills}    Deaths {r.Deaths}    Boons {r.Boons}\nAI Director: {r.DirectorName}",
                30, TextAnchor.MiddleCenter, Color.white, UIFactory.V(0.1f, 0.47f), UIFactory.V(0.9f, 0.58f), Vector2.zero, Vector2.zero);
            UIFactory.Button(c, "Restart", "Sail again", () => GameSession.Load(GameSession.LevelScene), UIFactory.V(0.38f, 0.3f), UIFactory.V(0.62f, 0.39f), Vector2.zero, Vector2.zero, 38);
            UIFactory.Button(c, "Menu", "Main menu", () => GameSession.Load(GameSession.MainMenuScene), UIFactory.V(0.38f, 0.19f), UIFactory.V(0.62f, 0.27f), Vector2.zero, Vector2.zero, 34);
        }
    }
}
