using UnityEngine;

namespace PaperWings.Air
{
    /// <summary>Tuning for a ceiling fan: a downdraft under the blades and a ring of upwash around them.</summary>
    [CreateAssetMenu(fileName = "CeilingFanSettings", menuName = "PaperWings/Ceiling Fan Settings")]
    public class CeilingFanSettings : ScriptableObject
    {
        [Header("Shape")]
        [Tooltip("Blade radius. The downdraft core is this wide.")]
        public float bladeRadius = 14f;

        [Tooltip("Width of the upwash ring just outside the blades.")]
        public float ringWidth = 12f;

        [Tooltip("How far below the fan the core downdraft reaches.")]
        public float coreReach = 75f;

        [Tooltip("How far below the fan the upwash ring starts lifting you.")]
        public float ringReach = 70f;

        [Header("Air")]
        [Tooltip("Downward air speed right under the blades (units/sec). Above ~6 you can't climb through it.")]
        public float coreDownSpeed = 8f;

        [Tooltip("Upward air speed in the middle of the ring. Circle the fan's edge to climb.")]
        public float ringUpSpeed = 7f;

        [Tooltip("Sideways swirl around the fan (units/sec): it carries you round as you circle.")]
        public float swirlSpeed = 3.5f;

        [Header("Cycle  (0 = always on)")]
        [Tooltip("Seconds the fan runs before it winds down. 0 = it never stops.")]
        public float onTime = 0f;
        [Tooltip("Seconds it rests before spinning up again.")]
        public float offTime = 4f;
        [Tooltip("Seconds to spin up or wind down.")]
        public float spinUpTime = 1.5f;

        [Header("Visuals")]
        [Tooltip("Blade spin at full speed (degrees/sec).")]
        public float bladeSpin = 540f;
        public float streakRate = 30f;
        [Range(0f, 1f)] public float streakAlpha = 0.35f;
    }
}
