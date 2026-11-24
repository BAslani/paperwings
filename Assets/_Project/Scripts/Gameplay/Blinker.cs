using UnityEngine;

namespace PaperWings.Gameplay
{
    /// <summary>
    /// Blinks lights and glowing parts on and off, like an aircraft warning beacon (Stage 41, the
    /// rooftop). Purely visual: nothing else reads it.
    /// </summary>
    public class Blinker : MonoBehaviour
    {
        [Tooltip("Seconds for one full on-off cycle.")]
        public float period = 1.6f;
        [Range(0f, 1f), Tooltip("Share of each cycle spent on.")]
        public float onFraction = 0.3f;
        [Range(0f, 1f), Tooltip("Where in the cycle this blinker starts, so neighbours don't blink together.")]
        public float phase;
        public Light[] lights;
        [Tooltip("Glowing parts shown only while on.")]
        public Renderer[] glows;

        float[] baseIntensity;
        bool wasOn = true;

        void Awake()
        {
            baseIntensity = new float[lights != null ? lights.Length : 0];
            for (int i = 0; i < baseIntensity.Length; i++) baseIntensity[i] = lights[i] != null ? lights[i].intensity : 0f;
        }

        void Update()
        {
            bool on = Mathf.Repeat(Time.time / Mathf.Max(0.05f, period) + phase, 1f) < onFraction;
            if (on == wasOn) return;
            wasOn = on;
            for (int i = 0; i < baseIntensity.Length; i++) if (lights[i] != null) lights[i].intensity = on ? baseIntensity[i] : 0f;
            if (glows != null) foreach (var r in glows) if (r != null) r.enabled = on;
        }
    }
}
