using UnityEngine;

namespace PaperWings.Gameplay
{
    /// <summary>
    /// Flickers lights like a TV playing in the next room (Stage 43, the kitchen): every so often it
    /// picks a new brightness and colour and eases toward them. Purely visual.
    /// </summary>
    public class LightFlicker : MonoBehaviour
    {
        public Light[] lights;
        [Tooltip("Brightness range, as a share of each light's own intensity.")]
        public Vector2 scale = new Vector2(0.45f, 1.15f);
        [Tooltip("Colours the picture cycles through (a TV's light shifts with what's on screen).")]
        public Color[] colors = { new Color(0.55f, 0.7f, 1f), new Color(0.75f, 0.85f, 1f), new Color(1f, 0.85f, 0.7f) };
        [Tooltip("Seconds between changes (random in this range).")]
        public Vector2 interval = new Vector2(0.08f, 0.6f);
        [Tooltip("How fast it eases to each new value.")]
        public float sharpness = 14f;

        float[] baseIntensity;
        float targetScale = 1f, currentScale = 1f, next;
        Color targetColor = Color.white, currentColor = Color.white;

        void Awake()
        {
            baseIntensity = new float[lights != null ? lights.Length : 0];
            for (int i = 0; i < baseIntensity.Length; i++) baseIntensity[i] = lights[i] != null ? lights[i].intensity : 0f;
            if (colors != null && colors.Length > 0) currentColor = targetColor = colors[0];
        }

        void Update()
        {
            if (Time.time >= next)
            {
                next = Time.time + Random.Range(interval.x, interval.y);
                targetScale = Random.Range(scale.x, scale.y);
                if (colors != null && colors.Length > 0) targetColor = colors[Random.Range(0, colors.Length)];
            }
            float k = 1f - Mathf.Exp(-sharpness * Time.deltaTime);
            currentScale = Mathf.Lerp(currentScale, targetScale, k);
            currentColor = Color.Lerp(currentColor, targetColor, k);
            for (int i = 0; i < baseIntensity.Length; i++)
            {
                if (lights[i] == null) continue;
                lights[i].intensity = baseIntensity[i] * currentScale;
                lights[i].color = currentColor;
            }
        }
    }
}
