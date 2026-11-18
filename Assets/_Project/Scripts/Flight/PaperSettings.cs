using UnityEngine;

namespace PaperWings.Flight
{
    /// <summary>
    /// How this plane's paper wears and gets wet. Separate from FlightSettings so different
    /// papers (printer paper, cardstock, napkin...) can share one flight feel, or vice versa.
    /// </summary>
    [CreateAssetMenu(fileName = "PaperSettings", menuName = "PaperWings/Paper Settings")]
    public class PaperSettings : ScriptableObject
    {
        [Header("Damage  (bumps add up)")]
        public bool damageEnabled = true;

        [Tooltip("Total paper health. At 0 the plane crumples (counts as a crash).")]
        public float maxIntegrity = 100f;

        [Tooltip("Damage from any glancing bump, however soft.")]
        public float bumpBaseDamage = 8f;

        [Tooltip("Extra damage per unit of impact speed. Survivable bumps are 0-6, so a bump costs ~8-26.")]
        public float bumpDamagePerImpact = 3f;

        [Header("What damage does  (at 0% paper)")]
        [Tooltip("Glide ratio multiplier when fully worn. 0.6 = sinks ~1.7x faster.")]
        [Range(0.2f, 1f)] public float glideAtZero = 0.6f;

        [Tooltip("Pitch/turn response multiplier when fully worn.")]
        [Range(0.2f, 1f)] public float controlAtZero = 0.7f;

        [Tooltip("A bent wing: bank angle the plane drifts into with no input when fully worn. " +
                 "The side is picked at the first bump.")]
        [Range(0f, 25f)] public float maxRollBias = 8f;

        [Header("Wetness  (steam lifts you, but soaks the paper)")]
        public bool wetnessEnabled = true;

        [Tooltip("How fast you get wet in the steam core (fraction per second). 0.7 = soaked in ~1.5s at the core; " +
                 "a quick pass through the column soaks you about halfway.")]
        [Range(0f, 3f)] public float wetFromSteam = 0.7f;

        [Tooltip("How fast you dry anywhere outside wet air (fraction per second). 0.025 = ~40s to dry fully on its own.")]
        [Range(0f, 1f)] public float dryRate = 0.025f;

        [Tooltip("Extra drying in DRY heat, like the laptop thermal (fraction per second at its core).")]
        [Range(0f, 2f)] public float heatDryRate = 0.5f;

        [Tooltip("Extra sink when soaked (units/sec). Soggy paper is heavy.")]
        public float wetExtraSink = 1.8f;

        [Tooltip("Pitch/turn response multiplier when soaked.")]
        [Range(0.2f, 1f)] public float wetControlScale = 0.8f;

        [Header("Visuals")]
        [Tooltip("How far vertices get pushed when fully crumpled (units). The dart is ~2 long.")]
        [Range(0f, 0.5f)] public float crumpleAmount = 0.18f;

        public Color damagedColor = new Color(0.78f, 0.74f, 0.66f);
        public Color wetColor = new Color(0.6f, 0.67f, 0.75f);
    }
}
