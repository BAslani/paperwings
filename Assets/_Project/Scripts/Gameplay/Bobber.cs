using UnityEngine;

namespace PaperWings.Gameplay
{
    /// <summary>
    /// Bobs and rocks an object gently, like a rubber duck on bath water (Stage 47). Optionally drifts
    /// round a slow circle. Purely visual.
    /// </summary>
    public class Bobber : MonoBehaviour
    {
        public float bob = 0.5f;
        public float rock = 6f;
        public float period = 2.6f;
        [Tooltip("Drift round a circle of this radius (0 = stay put).")]
        public float drift = 0f;
        public float driftPeriod = 30f;
        [Range(0f, 1f)] public float phase;

        Vector3 start;
        Quaternion startRot;

        void Awake() { start = transform.localPosition; startRot = transform.localRotation; }

        void Update()
        {
            float t = Time.time / Mathf.Max(0.1f, period) + phase;
            float a = (Time.time / Mathf.Max(1f, driftPeriod) + phase) * Mathf.PI * 2f;
            var off = drift > 0f ? new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * drift : Vector3.zero;
            transform.localPosition = start + off + Vector3.up * Mathf.Sin(t * Mathf.PI * 2f) * bob;
            var heading = drift > 0f ? Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f) : Quaternion.identity;
            transform.localRotation = heading * startRot * Quaternion.Euler(Mathf.Sin(t * Mathf.PI * 2f + 1f) * rock, 0f, Mathf.Sin(t * Mathf.PI * 2f) * rock * 0.7f);
        }
    }
}
