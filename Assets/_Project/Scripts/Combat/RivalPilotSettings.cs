using UnityEngine;

namespace PaperWings.Combat
{
    /// <summary>
    /// Behaviour knobs for AI rival pilots. The AI flies with the same flight model as the
    /// player, so these only describe its decisions, not its physics.
    /// </summary>
    [CreateAssetMenu(fileName = "RivalPilotSettings", menuName = "PaperWings/Rival Pilot Settings")]
    public class RivalPilotSettings : ScriptableObject
    {
        [Header("Awareness")]
        [Tooltip("Starts chasing the player inside this distance.")]
        public float engageRange = 45f;

        [Tooltip("Gives up the chase beyond this distance.")]
        public float disengageRange = 70f;

        [Header("Shooting")]
        [Tooltip("Only shoots when the player is closer than this...")]
        public float fireRange = 30f;

        [Tooltip("...and within this many degrees of its nose.")]
        [Range(1f, 45f)] public float fireAngle = 10f;

        [Tooltip("Shots per burst, then a pause. Gives the player a rhythm to dodge.")]
        public int burstLength = 3;
        public float burstPause = 1.6f;

        [Header("Flying")]
        [Tooltip("Circles this far from its patrol point.")]
        public float patrolRadius = 22f;

        [Tooltip("Preferred height above whatever is below it.")]
        public float preferredHeight = 22f;

        [Tooltip("Below this height (or near stall speed) it looks for a thermal to climb in.")]
        public float lowHeight = 10f;

        [Tooltip("How hard it steers (0.5 = lazy, 1 = full stick). Lower = easier to out-turn.")]
        [Range(0.2f, 1f)] public float aggressiveness = 0.75f;

        [Tooltip("Looks ahead this far for obstacles and swerves to avoid them.")]
        public float avoidDistance = 14f;

        [Header("Respawn")]
        [Tooltip("Seconds after being shot down (or crashing) before a fresh rival appears.")]
        public float respawnDelay = 4f;
    }
}
