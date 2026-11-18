using UnityEngine;

namespace PaperWings.Combat
{
    /// <summary>Tuning for the Stapler boss: health, weak spot, attack timings, phases.</summary>
    [CreateAssetMenu(fileName = "BossSettings", menuName = "PaperWings/Boss Settings")]
    public class BossSettings : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "THE STAPLER";
        public string title = "Head of Accounting";

        [Header("Health & Weak Spot")]
        [Tooltip("Staples do 18, pencils 60. With the jaw open (x2) that's ~17 staple hits or 5 pencils.")]
        public float maxHealth = 600f;

        [Tooltip("Damage multiplier while the jaw is SHUT (armored). Low = you must wait for the opening.")]
        [Range(0f, 1f)] public float armoredMultiplier = 0.2f;

        [Tooltip("Damage multiplier while the jaw is OPEN (weak spot glowing).")]
        public float weakSpotMultiplier = 2f;

        [Tooltip("Only wakes up when you're this close.")]
        public float aggroRange = 45f;

        [Tooltip("Never attacks until you've been flying this long (no cheap shots right after the throw).")]
        public float engageDelay = 5f;

        [Tooltip("When it first notices you, its jaw rattles this long before the first attack (your warning).")]
        public float wakeUpTime = 1.2f;

        [Header("Phases  (by remaining health)")]
        [Tooltip("Below this, it starts slamming.")]
        [Range(0f, 1f)] public float phase2At = 0.6f;
        [Tooltip("Below this, it calls reinforcements and fires faster.")]
        [Range(0f, 1f)] public float phase3At = 0.3f;

        [Header("Jaw")]
        public float jawOpenAngle = 35f;
        [Tooltip("How fast the jaw opens/closes normally (fraction per second).")]
        public float jawSpeed = 3f;
        [Tooltip("Seconds the jaw takes to open before firing (your warning).")]
        public float openTelegraph = 0.5f;
        [Tooltip("Seconds the jaw stays open after firing (your damage window).")]
        public float vulnerableTime = 1.6f;

        [Header("Staple Barrage")]
        [Tooltip("Seconds between barrages (shorter in phase 3).")]
        public float barrageInterval = 2.4f;
        public int shotsPerFan = 5;
        [Tooltip("Total spread of each fan (degrees).")]
        public float fanAngle = 28f;
        public float projectileSpeed = 42f;
        public float projectileDamage = 12f;
        public float projectileLifetime = 2.2f;

        [Header("Slam  (phase 2+)")]
        [Tooltip("Seconds between slams.")]
        public float slamInterval = 8f;
        [Tooltip("Seconds the jaw rises before slamming (your warning).")]
        public float slamTelegraph = 0.8f;
        [Tooltip("How long the downdraft blast lasts.")]
        public float slamDuration = 1.2f;
        public float slamRadius = 28f;
        public float slamHeight = 45f;
        [Tooltip("Downward air speed at the centre of the blast.")]
        public float slamDownSpeed = 14f;
        [Tooltip("Outward push at the centre of the blast.")]
        public float slamOutwardSpeed = 9f;

        [Header("Defeat")]
        [Tooltip("Seconds after defeat before it's back for a rematch.")]
        public float respawnDelay = 20f;
    }
}
