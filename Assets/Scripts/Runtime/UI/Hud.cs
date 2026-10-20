using System.Text;
using Odyssey.Modifiers;
using UnityEngine;
using UnityEngine.UI;

namespace Odyssey.UI
{
    /// <summary>Health bar, lives, segment counter, boons and the (F1) Director debug readout.</summary>
    public class Hud : MonoBehaviour
    {
        private RectTransform _hpFill;
        private Text _hpText, _info, _boons, _director, _toast;
        private float _toastUntil;
        public bool ShowDirector { get; set; } = true;

        public static Hud Create()
        {
            var canvas = UIFactory.Canvas("HUD", 10);
            var h = canvas.gameObject.AddComponent<Hud>();
            var t = canvas.transform;
            var V = (System.Func<float, float, Vector2>)UIFactory.V;

            UIFactory.Panel(t, "HpBack", new Color(0, 0, 0, 0.6f), V(0, 1), V(0, 1), V(30, -70), V(530, -30));
            var fill = UIFactory.Panel(t, "HpFill", new Color(0.8f, 0.2f, 0.2f), V(0, 1), V(0, 1), V(34, -66), V(526, -34));
            h._hpFill = fill.rectTransform;
            h._hpText = UIFactory.Label(t, "HpText", "", 26, TextAnchor.MiddleCenter, Color.white, V(0, 1), V(0, 1), V(30, -70), V(530, -30), FontStyle.Bold);
            h._info = UIFactory.Label(t, "Info", "", 28, TextAnchor.UpperLeft, UIFactory.Parchment, V(0, 1), V(0, 1), V(30, -120), V(900, -78));
            h._boons = UIFactory.Label(t, "Boons", "", 24, TextAnchor.UpperLeft, UIFactory.Gold, V(0, 1), V(0, 1), V(30, -420), V(700, -125));
            h._director = UIFactory.Label(t, "Director", "", 24, TextAnchor.UpperRight, new Color(0.6f, 0.9f, 1f), V(1, 1), V(1, 1), V(-1000, -200), V(-30, -30));
            h._toast = UIFactory.Label(t, "Toast", "", 44, TextAnchor.MiddleCenter, UIFactory.Gold, V(0, 0.62f), V(1, 0.72f), Vector2.zero, Vector2.zero, FontStyle.Bold);
            UIFactory.Label(t, "Controls", "A/D move   Space jump   J attack   Esc pause   F1 director info", 20,
                TextAnchor.LowerCenter, new Color(1, 1, 1, 0.45f), V(0, 0), V(1, 0), V(0, 10), V(0, 40));
            return h;
        }

        public void SetHealth(float current, float max)
        {
            float f = max > 0 ? Mathf.Clamp01(current / max) : 0f;
            _hpFill.anchorMax = new Vector2(0f, 1f);
            _hpFill.offsetMax = new Vector2(34 + 492 * f, -34);
            _hpText.text = $"Odysseus  {Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
        }

        public void SetInfo(int segment, int total, int livesLeft, int kills) =>
            _info.text = (segment < total ? $"Segment {segment + 1}/{total}" : "Run to the ship!") + $"    Lives {livesLeft}    Kills {kills}";

        public void SetBoons(RunModifierRegistry reg)
        {
            var sb = new StringBuilder();
            foreach (var b in reg.Picked) sb.AppendLine($"• {b.DisplayName}");
            _boons.text = sb.ToString();
        }

        public void SetDirector(string text) => _director.text = text;

        public void Toast(string text, float seconds = 2.5f)
        {
            _toast.text = text;
            _toastUntil = Time.unscaledTime + seconds;
        }

        private void Update()
        {
            if (_toast.text.Length > 0 && Time.unscaledTime > _toastUntil) _toast.text = "";
            _director.enabled = ShowDirector;
        }
    }
}
