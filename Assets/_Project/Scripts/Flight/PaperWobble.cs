using UnityEngine;

namespace PaperWings.Flight
{
    /// <summary>
    /// Visual-only paper flutter. Rotates the plane's child mesh ("Visual") with smooth noise,
    /// so physics and collisions are untouched. Turn it off in FlightSettings > Wobble Enabled.
    ///
    /// UNITY NOTE: Splitting a "root with physics" from a "child with the mesh" is a common
    /// pattern. You can shake, squash or animate the child freely without upsetting the Rigidbody.
    /// </summary>
    public class PaperWobble : MonoBehaviour
    {
        [SerializeField] PlaneController plane;
        [SerializeField] Transform visual;

        float seed;
        float kick;   // decaying extra wobble from throws/boosts

        void Awake()
        {
            if (plane == null) plane = GetComponent<PlaneController>();
            seed = Random.value * 100f;
        }

        void OnEnable()  { if (plane) plane.Boosted += OnBoost; }
        void OnDisable() { if (plane) plane.Boosted -= OnBoost; }

        void OnBoost() => Kick(6f);

        /// <summary>Adds a burst of flutter (degrees) that fades out.</summary>
        public void Kick(float degrees) => kick = Mathf.Max(kick, degrees);

        void LateUpdate()
        {
            if (visual == null || plane == null || plane.Settings == null) return;
            FlightSettings s = plane.Settings;

            Quaternion target = Quaternion.identity;
            if (s.wobbleEnabled && plane.State == PlaneState.Flying)
            {
                float speed01 = Mathf.InverseLerp(0f, s.maxSpeed, plane.Speed);
                float amount = s.wobbleAmount * Mathf.Lerp(0.4f, 1.2f, speed01)
                             + s.stallShake * plane.StallAmount
                             + kick;

                // Stall shake is faster and more nervous than normal flutter.
                float freq = s.wobbleFrequency * (1f + plane.StallAmount * 2f);
                float t = Time.time * freq;

                float rollN  = Noise(t, 0f);
                float pitchN = Noise(t, 10f) * 0.5f;   // pitch flutter is subtler
                float yawN   = Noise(t, 20f) * 0.3f;

                target = Quaternion.Euler(pitchN * amount, yawN * amount, rollN * amount);
            }

            kick = Mathf.MoveTowards(kick, 0f, 10f * Time.deltaTime);
            visual.localRotation = Quaternion.Slerp(visual.localRotation, target, 1f - Mathf.Exp(-15f * Time.deltaTime));
        }

        // Smooth noise in -1..1
        float Noise(float t, float offset) => (Mathf.PerlinNoise(t, seed + offset) - 0.5f) * 2f;
    }
}
