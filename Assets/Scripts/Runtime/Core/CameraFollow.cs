using UnityEngine;

namespace Odyssey.Core
{
    /// <summary>Simple smooth side-scroller camera with look-ahead in the facing direction.</summary>
    public class CameraFollow : MonoBehaviour
    {
        public Transform Target;
        public float LookAhead = 2.5f;
        public float SmoothTime = 0.18f;
        public float MinX = -1000f;
        public float YOffset = 1.5f;
        public float MinY = 5f;
        private Vector3 _vel;
        private float _ahead;

        public void Snap()
        {
            if (Target == null) return;
            transform.position = Desired(0f);
        }

        private void LateUpdate()
        {
            if (Target == null) return;
            var body = Target.GetComponent<Rigidbody2D>();
            float vx = body != null ? body.linearVelocity.x : 0f;
            _ahead = Mathf.Lerp(_ahead, Mathf.Abs(vx) > 0.5f ? Mathf.Sign(vx) * LookAhead : _ahead, Time.unscaledDeltaTime * 2f);
            transform.position = Vector3.SmoothDamp(transform.position, Desired(_ahead), ref _vel, SmoothTime);
        }

        private Vector3 Desired(float ahead) =>
            new Vector3(Mathf.Max(MinX, Target.position.x + ahead), Mathf.Max(MinY, Target.position.y + YOffset), -10f);
    }

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
