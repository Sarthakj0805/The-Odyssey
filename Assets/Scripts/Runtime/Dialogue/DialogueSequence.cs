using System;
using UnityEngine;

namespace Odyssey.Dialogue
{
    [Serializable]
    public struct DialogueLine
    {
        public string Speaker;
        [TextArea(2, 4)] public string Text;
        public Color SpeakerColor;
        public DialogueLine(string speaker, string text, Color color) { Speaker = speaker; Text = text; SpeakerColor = color; }
    }

    /// <summary>Writers edit dialogue as assets, not code.</summary>
    [CreateAssetMenu(menuName = "Odyssey/Dialogue Sequence", fileName = "Dialogue")]
    public class DialogueSequence : ScriptableObject
    {
        public DialogueLine[] Lines = Array.Empty<DialogueLine>();
        [Min(1f)] public float CharactersPerSecond = 45f;
    }
}
