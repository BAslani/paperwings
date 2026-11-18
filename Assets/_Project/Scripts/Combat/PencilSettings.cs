using UnityEngine;

namespace PaperWings.Combat
{
    /// <summary>
    /// Tuning for pencil missiles: lock-on rules and how the pencils fly.
    /// The number of pencils is the number of mount slots on the plane (4 on the dart).
    /// </summary>
    [CreateAssetMenu(fileName = "PencilSettings", menuName = "PaperWings/Pencil Missile Settings")]
    public class PencilSettings : ScriptableObject
    {
        [Header("Lock-on")]
        [Tooltip("Seconds a rival must stay in front of you to get a lock.")]
        public float lockTime = 0.7f;

        [Tooltip("Lock cone (degrees from your nose) to START locking.")]
        [Range(2f, 45f)] public float lockAngle = 15f;

        [Tooltip("Once locked, you keep the lock until the target leaves this wider cone.")]
        [Range(2f, 60f)] public float keepLockAngle = 28f;

        [Tooltip("Max lock distance.")]
        public float lockRange = 70f;

        [Tooltip("Seconds between pencil launches.")]
        public float fireCooldown = 0.35f;

        [Header("Flight")]
        [Tooltip("After detaching, the pencil drops for this long before its 'engine' kicks in.")]
        public float dropTime = 0.25f;

        [Tooltip("Top speed (units/sec). Faster than any plane, so a pencil always catches up eventually.")]
        public float speed = 50f;

        [Tooltip("How fast it reaches top speed (units/sec²).")]
        public float acceleration = 90f;

        [Tooltip("How sharply it can turn toward the target (deg/sec). Lower = easier to dodge with a hard turn.")]
        public float turnRate = 140f;

        [Tooltip("Seconds before a pencil that missed fizzles out.")]
        public float lifetime = 5f;

        [Tooltip("Explodes when this close to the target (generous, so near-misses still count).")]
        public float proximityFuse = 1.8f;

        [Tooltip("Paper damage on hit. Rivals have 60, so the default is a one-hit kill.")]
        public float damage = 60f;
    }
}
