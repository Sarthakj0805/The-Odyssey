using Unity.MLAgents;
using UnityEngine;

namespace Odyssey.Director.ML
{
    /// <summary>
    /// Training scene root. Holds N DirectorAgents (each with its own simulated player) that are
    /// stepped by the Academy every FixedUpdate. Start `mlagents-learn` then press Play.
    /// </summary>
    public class DirectorTrainingArena : MonoBehaviour
    {
        private void Awake()
        {
            Academy.Instance.AutomaticSteppingEnabled = true;
            Application.runInBackground = true;
        }

        private void OnGUI()
        {
            var a = Academy.Instance;
            GUI.Label(new Rect(10, 10, 600, 60),
                $"Director training arena — communicator {(a.IsCommunicatorOn ? "CONNECTED" : "not connected (run mlagents-learn first)")}\n" +
                $"Academy steps: {a.TotalStepCount}  episodes: {a.EpisodeCount}");
        }
    }
}
