using UnityEngine;

namespace PaperWings.Combat
{
    /// <summary>
    /// Tuning shared by the air bosses (The Mainframe, The Condenser): health, weak spot, phases,
    /// volleys and the rhythm of their attacks. Each boss reads the parts it uses.
    /// </summary>
    [CreateAssetMenu(fileName = "AirBossSettings", menuName = "PaperWings/Air Boss Settings")]
    public class AirBossSettings : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "THE MAINFRAME";
        public string title = "IT Department";

        [Header("Health & Weak Spot")]
        public float maxHealth = 700f;
        [Range(0f, 1f)] public float armoredMultiplier = 0.2f;
        public float weakSpotMultiplier = 2.5f;
        [Tooltip("Only wakes up when you're this close.")]
        public float aggroRange = 90f;
        [Tooltip("Never attacks until you've been flying this long.")]
        public float engageDelay = 4f;
        [Tooltip("Warning rattle when it first notices you.")]
        public float wakeUpTime = 1.4f;

        [Header("Phases (by remaining health)")]
        [Range(0f, 1f)] public float phase2At = 0.6f;
        [Range(0f, 1f)] public float phase3At = 0.3f;

        [Header("Rhythm")]
        [Tooltip("Seconds of warning before a big air attack.")]
        public float telegraph = 1.1f;
        [Tooltip("Seconds the big air attack lasts.")]
        public float attackDuration = 3.5f;
        [Tooltip("Seconds the weak spot stays open afterwards (your damage window).")]
        public float vulnerableTime = 3.5f;
        [Tooltip("Seconds of rest between rounds (shortened in phase 3).")]
        public float restTime = 1.5f;

        [Header("Air")]
        [Tooltip("Main air speed of its attack (fan sweep wind, updraft).")]
        public float airSpeed = 16f;
        [Tooltip("Secondary air speed (purge downdraft, suction pull).")]
        public float secondaryAirSpeed = 12f;
        [Tooltip("Paper damage per second if you touch its intake while it sucks (Condenser).")]
        public float contactDamagePerSecond = 40f;

        [Header("Volleys")]
        public int shotsPerVolley = 6;
        public float spreadAngle = 40f;
        public float projectileSpeed = 38f;
        public float projectileDamage = 10f;
        public float projectileLifetime = 2.5f;
        [Tooltip("Projectile scale (shredded paper and packets are big and readable).")]
        public float projectileScale = 2f;

        [Header("Defeat")]
        public float respawnDelay = 25f;
    }
}
