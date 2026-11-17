using UnityEngine;

namespace PaperWings.Gameplay
{
    /// <summary>
    /// Tuning for a landing target. Defaults are generous on purpose:
    /// landing should feel like an achievement you can actually pull off.
    /// </summary>
    [CreateAssetMenu(fileName = "LandingSettings", menuName = "PaperWings/Landing Settings")]
    public class LandingSettings : ScriptableObject
    {
        [Header("Approach Assist")]
        [Tooltip("Horizontal distance from the target where steering assist starts.")]
        public float assistRadius = 16f;

        [Tooltip("How far above the target the assist still works.")]
        public float assistHeight = 25f;

        [Tooltip("Assist only kicks in if you're roughly heading at the target (degrees off-course).")]
        [Range(5f, 90f)] public float assistMaxAngle = 55f;

        [Header("Touchdown Limits  (forgiving)")]
        [Tooltip("Max airspeed for a successful landing. Cruise is ~14.")]
        public float maxLandingSpeed = 22f;

        [Tooltip("Max speed INTO the surface (i.e. how hard you come down).")]
        public float maxImpactSpeed = 9f;

        [Tooltip("Max bank angle at touchdown.")]
        [Range(5f, 90f)] public float maxBank = 45f;

        [Header("Snap & Celebrate")]
        [Tooltip("Seconds to slide the plane into a neat resting pose on the target.")]
        public float snapTime = 0.35f;

        [Tooltip("Random message shown on a successful landing.")]
        public string[] successMessages =
        {
            "MUG-NIFICENT!",
            "Perfect landing! Coffee's still warm.",
            "Touchdown on the java!",
            "The night shift salutes you.",
            "Smooth as a fresh ream.",
            "Bean there, landed that."
        };
    }
}
