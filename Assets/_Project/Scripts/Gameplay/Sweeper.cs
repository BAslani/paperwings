using UnityEngine;

namespace PaperWings.Gameplay
{
    /// <summary>
    /// Swings an object slowly from side to side around its starting rotation, like a searchlight
    /// (Stage 43: the torch in Blue's fort). Purely visual.
    /// </summary>
    public class Sweeper : MonoBehaviour
    {
        [Tooltip("Degrees either side of the starting direction.")]
        public float amplitude = 35f;
        [Tooltip("Seconds for one full sweep there and back.")]
        public float period = 7f;
        [Range(0f, 1f)] public float phase;

        Quaternion start;

        void Awake() => start = transform.localRotation;

        void Update()
        {
            float a = amplitude * Mathf.Sin((Time.time / Mathf.Max(0.1f, period) + phase) * Mathf.PI * 2f);
            transform.localRotation = Quaternion.AngleAxis(a, Vector3.up) * start;
        }
    }
}
