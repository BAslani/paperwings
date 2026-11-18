using UnityEngine;

namespace PaperWings.Air
{
    /// <summary>
    /// Tuning for a steam thermal (hot coffee/tea). Compared to the laptop thermal it's
    /// stronger but gusty: lift comes in puffs and the column sways, so riding it takes a bit of skill.
    /// </summary>
    [CreateAssetMenu(fileName = "SteamSettings", menuName = "PaperWings/Steam Settings")]
    public class SteamSettings : ScriptableObject
    {
        [Header("Lift")]
        [Tooltip("Average upward air speed in the core (units/sec). Stronger than the laptop.")]
        public float liftSpeed = 13f;

        [Tooltip("Column radius. ~9 is just enough to circle in at full bank, so it's tighter than the laptop.")]
        public float radius = 9f;

        [Tooltip("Column height above the cup.")]
        public float height = 40f;

        [Range(0f, 1f)] public float coreFraction = 0.4f;
        [Range(0.01f, 1f)] public float topFade = 0.3f;
        public float centeringAssist = 1f;

        [Header("Gusts  (what makes steam feel like steam)")]
        [Tooltip("How much the lift pulses. 0.5 = between 50% and 150% of Lift Speed.")]
        [Range(0f, 1f)] public float puffAmount = 0.5f;

        [Tooltip("Puffs per second (roughly).")]
        [Range(0.05f, 3f)] public float puffFrequency = 0.8f;

        [Tooltip("How far the top of the column wanders sideways (units). The base stays on the cup.")]
        public float swayAmount = 3f;

        [Range(0.01f, 1f)] public float swayFrequency = 0.15f;

        [Header("Visuals  (looks only; the lift zone above is unaffected)")]
        [Tooltip("Steam particles per second (scaled by puffs).")]
        public float steamRate = 16f;
        public Color steamColor = new Color(0.95f, 0.95f, 1f, 0.14f);

        [Tooltip("How wide the steam leaves the cup, as a fraction of the lift Radius. " +
                 "Small = a thin wisp, while the lift still covers the full radius.")]
        [Range(0.02f, 1f)] public float steamSpread = 0.15f;

        [Tooltip("Random particle size range (min, max) when it leaves the cup. Particles still billow out as they rise.")]
        public Vector2 steamSize = new Vector2(0.35f, 0.8f);
    }
}
