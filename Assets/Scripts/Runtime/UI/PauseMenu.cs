using Odyssey.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Odyssey.UI
{
    public class PauseMenu : MonoBehaviour
    {
        private GameObject _root;
        public System.Func<bool> CanPause = () => true;
        public bool IsPaused => _root.activeSelf;

        public static PauseMenu Create()
        {
            var canvas = UIFactory.Canvas("PauseCanvas", 60);
            var p = canvas.gameObject.AddComponent<PauseMenu>();
            p._root = UIFactory.Panel(canvas.transform, "Dim", new Color(0, 0, 0, 0.75f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            UIFactory.Label(p._root.transform, "Title", "Paused", 72, TextAnchor.MiddleCenter, UIFactory.Gold, UIFactory.V(0, 0.65f), UIFactory.V(1, 0.8f), Vector2.zero, Vector2.zero, FontStyle.Bold);
            UIFactory.Button(p._root.transform, "Resume", "Resume", () => p.SetPaused(false), UIFactory.V(0.4f, 0.48f), UIFactory.V(0.6f, 0.56f), Vector2.zero, Vector2.zero);
            UIFactory.Button(p._root.transform, "Restart", "Restart run", () => GameSession.Load(GameSession.LevelScene), UIFactory.V(0.4f, 0.38f), UIFactory.V(0.6f, 0.46f), Vector2.zero, Vector2.zero);
            UIFactory.Button(p._root.transform, "Menu", "Main menu", () => GameSession.Load(GameSession.MainMenuScene), UIFactory.V(0.4f, 0.28f), UIFactory.V(0.6f, 0.36f), Vector2.zero, Vector2.zero);
            p._root.SetActive(false);
            return p;
        }

        private void Update()
        {
            var kb = Keyboard.current;
            bool toggle = (kb != null && kb.escapeKey.wasPressedThisFrame) || (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);
            if (!toggle) return;
            if (IsPaused) SetPaused(false);
            else if (CanPause()) SetPaused(true);
        }

        public void SetPaused(bool paused)
        {
            _root.SetActive(paused);
            Time.timeScale = paused ? 0f : 1f;
        }
    }
}
