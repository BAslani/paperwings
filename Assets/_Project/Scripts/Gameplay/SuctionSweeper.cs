using UnityEngine;
using PaperWings.Air;

namespace PaperWings.Gameplay
{
    /// <summary>
    /// Mum pushing the vacuum (Stage 49): while its suction column is running, this object (the whole
    /// cleaner, nozzle and air together) glides back and forth along a line on the floor, turning to
    /// face the way it goes. When the suction dies down it stops where it is.
    /// </summary>
    public class SuctionSweeper : MonoBehaviour
    {
        [SerializeField] ColumnAir suction;
        [Tooltip("Local-space offsets from its rest position that it glides between.")]
        [SerializeField] Vector3 from = new Vector3(-45f, 0f, 0f);
        [SerializeField] Vector3 to = new Vector3(45f, 0f, 0f);
        [SerializeField] float speed = 14f;
        [Tooltip("Spin the wheels / brush on these while it moves.")]
        [SerializeField] Transform[] spinners;

        Vector3 rest;
        float t;
        int dir = 1;

        void Awake() => rest = transform.localPosition;

        void Update()
        {
            if (suction == null || suction.Strength < 0.15f) return;
            Vector3 a = rest + from, b = rest + to;
            float length = Mathf.Max(1f, Vector3.Distance(a, b));
            t += dir * speed * Time.deltaTime / length;
            if (t >= 1f) { t = 1f; dir = -1; }
            if (t <= 0f) { t = 0f; dir = 1; }
            Vector3 p = Vector3.Lerp(a, b, Mathf.SmoothStep(0f, 1f, t));
            Vector3 move = (dir > 0 ? b - a : a - b);
            transform.localPosition = p;
            if (move.sqrMagnitude > 0.01f)
            {
                var face = Quaternion.LookRotation(new Vector3(move.x, 0f, move.z), Vector3.up);
                transform.localRotation = Quaternion.Slerp(transform.localRotation, face, 1f - Mathf.Exp(-4f * Time.deltaTime));
            }
            if (spinners != null)
                foreach (var s in spinners) if (s != null) s.Rotate(speed * Time.deltaTime * 40f, 0f, 0f, Space.Self);
        }
    }
}
