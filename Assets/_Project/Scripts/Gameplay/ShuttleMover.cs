using UnityEngine;

namespace PaperWings.Gameplay
{
    /// <summary>
    /// Moving machinery (Stage 29): shuttles between local points, pausing at each, optionally
    /// dropping an "arm" at every stop. The server room's tape-library robot rides its gantry with
    /// this; the arm plunges into the racks and comes back up. Solid: planes that hit it crumple.
    /// Give the moving objects a kinematic Rigidbody so physics knows their colliders move.
    /// </summary>
    public class ShuttleMover : MonoBehaviour
    {
        [Tooltip("Stops, in local space of this object's parent. It travels them in order, then back (or loops).")]
        public Vector3[] points = { Vector3.zero, new Vector3(0f, 0f, 60f) };
        public float speed = 14f;
        public float pause = 1.5f;
        public bool pingPong = true;
        [Header("Arm (optional)")]
        public Transform arm;
        [Tooltip("How far the arm drops (local -Y) at each stop.")]
        public float armDrop = 20f;
        public float armSpeed = 30f;
        [Tooltip("Spinning parts (fans on the carriage), spun around local Y.")]
        public Transform[] spinners;
        public float spinSpeed = 600f;

        int target = 1, dir = 1;
        float waitUntil, armRestY;
        bool atStop;

        void Awake()
        {
            if (arm != null) armRestY = arm.localPosition.y;
            if (points != null && points.Length > 0) transform.localPosition = points[0];
        }

        void Update()
        {
            if (points == null || points.Length < 2) return;

            if (atStop)
            {
                // Drop the arm and bring it back up while waiting.
                float half = pause * 0.5f;
                float t = Mathf.Clamp01(1f - Mathf.Abs((waitUntil - Time.time) - half) / Mathf.Max(0.01f, half));
                if (arm != null)
                {
                    Vector3 p = arm.localPosition;
                    float want = armRestY - armDrop * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 1.6f));
                    p.y = Mathf.MoveTowards(p.y, want, armSpeed * Time.deltaTime);
                    arm.localPosition = p;
                }
                if (Time.time >= waitUntil)
                {
                    atStop = false;
                    if (arm != null) { var p = arm.localPosition; p.y = armRestY; arm.localPosition = p; }
                    Advance();
                }
            }
            else
            {
                Vector3 goal = points[target];
                transform.localPosition = Vector3.MoveTowards(transform.localPosition, goal, speed * Time.deltaTime);
                if ((transform.localPosition - goal).sqrMagnitude < 0.01f)
                {
                    atStop = true;
                    waitUntil = Time.time + pause;
                }
            }

            if (spinners != null) foreach (var s in spinners) if (s != null) s.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);
        }

        void Advance()
        {
            if (pingPong)
            {
                if (target + dir >= points.Length || target + dir < 0) dir = -dir;
                target += dir;
            }
            else target = (target + 1) % points.Length;
        }
    }
}
