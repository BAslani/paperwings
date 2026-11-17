using UnityEngine;

namespace PaperWings.Air
{
    /// <summary>
    /// Tuning for a fan-style draft: a cone of wind that weakens with distance.
    /// Values are deliberately exaggerated. This is a game, not a wind tunnel.
    /// </summary>
    [CreateAssetMenu(fileName = "FanSettings", menuName = "PaperWings/Fan Settings")]
    public class FanSettings : ScriptableObject
    {
        [Header("Wind")]
        [Tooltip("Wind speed right in front of the fan (units/sec). Cruise speed is ~14, so 16 is a strong shove.")]
        public float windSpeed = 16f;

        [Tooltip("How far the wind reaches.")]
        public float range = 35f;

        [Tooltip("Half-angle of the wind cone. Bigger = wider, easier to hit.")]
        [Range(2f, 60f)] public float coneHalfAngle = 22f;

        [Tooltip("Radius of the cone where it leaves the fan (about the fan's blade radius).")]
        public float fanRadius = 1.2f;

        [Tooltip("How the wind weakens with distance. 1 = linear, <1 = stays strong longer, >1 = drops fast.")]
        [Range(0.2f, 3f)] public float distanceFalloff = 0.8f;

        [Tooltip("Fraction of the cone's radius over which the wind fades out at the edge. Softer edges feel smoother.")]
        [Range(0f, 1f)] public float edgeSoftness = 0.4f;

        [Tooltip("Tilts the wind slightly upward so surfing a fan also gives a little lift.")]
        [Range(0f, 1f)] public float upwardBias = 0.15f;

        [Header("Oscillation")]
        public bool oscillate = true;
        [Tooltip("How far the head swings left and right of center.")]
        [Range(0f, 90f)] public float oscillateAngle = 30f;
        [Tooltip("Full left-right-left swings per second.")]
        [Range(0.01f, 2f)] public float oscillateSpeed = 0.15f;

        [Header("Visuals")]
        [Tooltip("Blade spin speed in degrees per second (visual only).")]
        public float bladeSpinSpeed = 1440f;
        [Tooltip("Wind streak particles per second.")]
        public float streakRate = 45f;
        public Color streakColor = new Color(0.85f, 0.95f, 1f, 0.45f);
    }
}
