using System;
using System.Collections;
using Odyssey.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Odyssey.Dialogue
{
    /// <summary>
    /// Text-box dialogue with a typewriter effect. Pauses gameplay (timeScale 0) and runs on
    /// unscaled time. Space/Enter/J/click: finish line or advance. Esc: skip the sequence.
    /// </summary>
    public class DialogueRunner : MonoBehaviour
    {
        public bool IsPlaying { get; private set; }
        private GameObject _box;
        private Text _speaker, _body;

        public static DialogueRunner Create()
        {
            var canvas = UIFactory.Canvas("DialogueCanvas", 50);
            var r = canvas.gameObject.AddComponent<DialogueRunner>();
            var box = UIFactory.Panel(canvas.transform, "Box", UIFactory.Ink, UIFactory.V(0.1f, 0.03f), UIFactory.V(0.9f, 0.3f), Vector2.zero, Vector2.zero);
            r._box = box.gameObject;
            r._speaker = UIFactory.Label(box.transform, "Speaker", "", 40, TextAnchor.UpperLeft, UIFactory.Gold, UIFactory.V(0, 1), UIFactory.V(1, 1), UIFactory.V(40, -70), UIFactory.V(-40, -15), FontStyle.Bold);
            r._body = UIFactory.Label(box.transform, "Body", "", 36, TextAnchor.UpperLeft, UIFactory.Parchment, UIFactory.V(0, 0), UIFactory.V(1, 1), UIFactory.V(40, 50), UIFactory.V(-40, -80));
            UIFactory.Label(box.transform, "Hint", "Space: continue    Esc: skip", 24, TextAnchor.LowerRight, new Color(1, 1, 1, 0.5f), UIFactory.V(0, 0), UIFactory.V(1, 0), UIFactory.V(40, 12), UIFactory.V(-30, 45));
            r._box.SetActive(false);
            return r;
        }

        public void Play(DialogueSequence seq, Action onDone)
        {
            if (seq == null || seq.Lines == null || seq.Lines.Length == 0) { onDone?.Invoke(); return; }
            StartCoroutine(Run(seq, onDone));
        }

        private IEnumerator Run(DialogueSequence seq, Action onDone)
        {
            IsPlaying = true;
            float prevScale = Time.timeScale;
            Time.timeScale = 0f;
            _box.SetActive(true);
            yield return null; // don't consume the key press that triggered us

            foreach (var line in seq.Lines)
            {
                _speaker.text = line.Speaker;
                _speaker.color = line.SpeakerColor.a > 0 ? line.SpeakerColor : UIFactory.Gold;
                _body.text = "";
                float shown = 0f;
                bool skipAll = false;
                while (shown < line.Text.Length)
                {
                    if (Skip()) { skipAll = true; break; }
                    if (Advance()) { shown = line.Text.Length; }
                    shown += Time.unscaledDeltaTime * seq.CharactersPerSecond;
                    _body.text = line.Text.Substring(0, Mathf.Min(line.Text.Length, (int)shown));
                    yield return null;
                }
                if (skipAll) break;
                _body.text = line.Text;
                yield return null;
                while (!Advance()) { if (Skip()) { skipAll = true; break; } yield return null; }
                if (skipAll) break;
                yield return null;
            }

            _box.SetActive(false);
            Time.timeScale = prevScale <= 0f ? 1f : prevScale;
            IsPlaying = false;
            onDone?.Invoke();
        }

        private static bool Advance()
        {
            var kb = Keyboard.current; var m = Mouse.current; var p = Gamepad.current;
            return (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.jKey.wasPressedThisFrame))
                || (m != null && m.leftButton.wasPressedThisFrame)
                || (p != null && p.buttonSouth.wasPressedThisFrame);
        }

        private static bool Skip()
        {
            var kb = Keyboard.current; var p = Gamepad.current;
            return (kb != null && kb.escapeKey.wasPressedThisFrame) || (p != null && p.startButton.wasPressedThisFrame);
        }
    }
}
