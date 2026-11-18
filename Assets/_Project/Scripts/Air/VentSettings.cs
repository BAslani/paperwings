using UnityEngine;

namespace PaperWings.Air
{
    /// <summary>
    /// Tuning for an AC vent: a straight rectangular column of air blowing out of the vent's face.
    /// Pointed down from the ceiling it's a downdraft (a hazard to route around);
    /// pointed sideways from a wall it's a draft.
    /// </summary>
    [CreateAssetMenu(fileName = "VentSettings", menuName = "PaperWings/Vent Settings")]
    public class VentSettings : ScriptableObject
    {
        [Header("Wind")]
        [Tooltip("Air speed at the vent face (units/sec). The plane sinks ~1.5 normally, so 9 down is a strong push.")]
        public float windSpeed = 9f;

        [Tooltip("Width (x) and height (y) of the vent opening / column cross-section.")]
        public Vector2 size = new Vector2(12f, 10f);

        [Tooltip("How far the air reaches from the vent.")]
        public float range = 55f;

        [Tooltip("How the air weakens with distance. <1 = stays strong longer.")]
        [Range(0.2f, 3f)] public float distanceFalloff = 0.6f;

        [Tooltip("Fraction of the column (from the edge inward) that fades out, for smooth entry.")]
        [Range(0f, 1f)] public float edgeSoftness = 0.35f;

        [Header("Visuals")]
        public float streakRate = 40f;
        public Color streakColor = new Color(0.7f, 0.8f, 1f, 0.35f);
    }
}
