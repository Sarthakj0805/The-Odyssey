using System;
using System.Collections.Generic;
using Odyssey.Modifiers;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Odyssey.UI
{
    /// <summary>Shrine UI: pick 1 of up to 3 boons (click or keys 1/2/3). Pauses the game while open.</summary>
    public class BoonChoicePanel : MonoBehaviour
    {
        private GameObject _root;
        private Text _title;
        private readonly List<GameObject> _cards = new List<GameObject>();
        private List<BoonDefinition> _offer;
        private Action<BoonDefinition> _onPick;
        public bool IsOpen => _root != null && _root.activeSelf;

        public static BoonChoicePanel Create()
        {
            var canvas = UIFactory.Canvas("BoonCanvas", 40);
            var p = canvas.gameObject.AddComponent<BoonChoicePanel>();
            p._root = UIFactory.Panel(canvas.transform, "Dim", new Color(0, 0, 0, 0.7f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            p._title = UIFactory.Label(p._root.transform, "Title", "", 52, TextAnchor.MiddleCenter, UIFactory.Gold,
                UIFactory.V(0, 0.78f), UIFactory.V(1, 0.92f), Vector2.zero, Vector2.zero, FontStyle.Bold);
            p._root.SetActive(false);
            return p;
        }

        public void Show(string title, List<BoonDefinition> offer, Action<BoonDefinition> onPick)
        {
            foreach (var c in _cards) Destroy(c);
            _cards.Clear();
            _offer = offer; _onPick = onPick;
            _title.text = title;
            _root.SetActive(true);
            Time.timeScale = 0f;

            int n = offer.Count;
            for (int i = 0; i < n; i++)
            {
                var boon = offer[i];
                float w = 0.26f, gap = 0.04f, total = n * w + (n - 1) * gap, x0 = 0.5f - total / 2f + i * (w + gap);
                int index = i;
                var btn = UIFactory.Button(_root.transform, $"Card{i}", "", () => Pick(index),
                    UIFactory.V(x0, 0.25f), UIFactory.V(x0 + w, 0.72f), Vector2.zero, Vector2.zero);
                btn.GetComponent<Image>().color = new Color(0.16f, 0.2f, 0.3f, 0.97f);
                var t = btn.transform;
                UIFactory.Label(t, "Key", $"[{i + 1}]", 28, TextAnchor.UpperLeft, new Color(1, 1, 1, 0.6f), UIFactory.V(0, 1), UIFactory.V(1, 1), UIFactory.V(20, -60), UIFactory.V(-20, -15));
                UIFactory.Label(t, "God", boon.God.ToUpperInvariant(), 30, TextAnchor.UpperCenter, boon.Tint, UIFactory.V(0, 1), UIFactory.V(1, 1), UIFactory.V(20, -110), UIFactory.V(-20, -60), FontStyle.Bold);
                UIFactory.Label(t, "Name", boon.DisplayName, 38, TextAnchor.UpperCenter, UIFactory.Gold, UIFactory.V(0, 1), UIFactory.V(1, 1), UIFactory.V(20, -220), UIFactory.V(-20, -115), FontStyle.Bold);
                UIFactory.Label(t, "Desc", boon.Description, 28, TextAnchor.UpperCenter, UIFactory.Parchment, UIFactory.V(0, 0), UIFactory.V(1, 1), UIFactory.V(25, 60), UIFactory.V(-25, -230));
                _cards.Add(btn.gameObject);
            }
        }

        private void Update()
        {
            if (!IsOpen || _offer == null) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.digit1Key.wasPressedThisFrame) Pick(0);
            else if (kb.digit2Key.wasPressedThisFrame) Pick(1);
            else if (kb.digit3Key.wasPressedThisFrame) Pick(2);
        }

        private void Pick(int i)
        {
            if (!IsOpen || _offer == null || i < 0 || i >= _offer.Count) return;
            var boon = _offer[i];
            _root.SetActive(false);
            Time.timeScale = 1f;
            _offer = null;
            _onPick?.Invoke(boon);
        }
    }
}
