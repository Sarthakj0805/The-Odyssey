using UnityEngine;

namespace Odyssey.Core
{
    /// <summary>Moves an object with the camera by a factor (1 = fixed on screen) for cheap parallax.</summary>
    public class ParallaxFollow : MonoBehaviour
    {
        public float Factor = 0.5f;
        private Vector3 _start, _camStart;
        private Transform _cam;

        private void Start()
        {
            _cam = Camera.main != null ? Camera.main.transform : null;
            _start = transform.position;
            if (_cam != null) _camStart = _cam.position;
        }

        private void LateUpdate()
        {
            if (_cam == null) return;
            var d = _cam.position - _camStart;
            transform.position = _start + new Vector3(d.x * Factor, d.y * Factor, 0f);
        }
    }
}
