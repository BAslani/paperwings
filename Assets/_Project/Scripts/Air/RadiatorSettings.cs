using UnityEngine;

namespace PaperWings.Air
{
    /// <summary>
    /// Tuning for a radiator: a long, thin curtain of warm air rising up a wall. The length is set
    /// per radiator (on the RadiatorSource), everything else is shared here.
    /// </summary>
    [CreateAssetMenu(fileName = "RadiatorSettings", menuName = "PaperWings/Radiator Settings")]
    public class RadiatorSettings : ScriptableObject
    {
        [Header("Lift")]
        [Tooltip("Upward air speed at the heart of the curtain (units/sec). Gentler than a laptop: you ride along it, not circle in it.")]
        public float liftSpeed = 6.5f;

        [Tooltip("How far the warm air reaches out from the wall. Fly within this band to be lifted.")]
        public float depth = 11f;

        [Tooltip("How high the curtain rises above the radiator.")]
        public float height = 95f;

        [Tooltip("Warm air leans away from the wall as it rises: this fraction of the lift pushes you outward, " +
                 "so riding a wall feels like slope soaring instead of sticking to it.")]
        [Range(0f, 0.6f)] public float outwardLean = 0.18f;

        [Tooltip("Fraction of the length at each end where the lift fades out.")]
        [Range(0.01f, 0.6f)] public float endSoftness = 0.25f;

        [Tooltip("Lift fades over this top fraction of the height.")]
        [Range(0.01f, 1f)] public float topFade = 0.3f;

        [Header("Visuals")]
        [Tooltip("Shimmer particles per second for every 10 units of radiator.")]
        public float shimmerPer10Units = 4f;
        public Color shimmerColor = new Color(1f, 0.55f, 0.15f, 0.1f);
    }
}
