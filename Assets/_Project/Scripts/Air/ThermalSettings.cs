using UnityEngine;

namespace PaperWings.Air
{
    /// <summary>
    /// Tuning for a thermal: a vertical column of rising air.
    /// Circling inside should feel like riding an elevator.
    /// </summary>
    [CreateAssetMenu(fileName = "ThermalSettings", menuName = "PaperWings/Thermal Settings")]
    public class ThermalSettings : ScriptableObject
    {
        [Header("Lift")]
        [Tooltip("Upward air speed in the core (units/sec). The plane sinks ~1.5-3, so 9 gives a brisk climb.")]
        public float liftSpeed = 9f;

        [Tooltip("Column radius. Generous: a full-bank circle is ~11 units across, so keep this above ~11.")]
        public float radius = 12f;

        [Tooltip("Column height above the heat source.")]
        public float height = 55f;

        [Tooltip("Fraction of the radius that gets full-strength lift. Lift fades smoothly outside it.")]
        [Range(0f, 1f)] public float coreFraction = 0.45f;

        [Tooltip("Lift fades over this top fraction of the column, so the 'elevator' slows to a gentle stop.")]
        [Range(0.01f, 1f)] public float topFade = 0.25f;

        [Tooltip("Gentle sideways pull toward the center (units/sec at the edge). Helps you stay in while circling.")]
        public float centeringAssist = 1.5f;

        [Header("Visuals")]
        [Tooltip("Shimmer particles per second.")]
        public float shimmerRate = 35f;
        public Color shimmerColor = new Color(1f, 0.7f, 0.35f, 0.22f);
    }
}
