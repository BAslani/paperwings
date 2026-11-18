using UnityEngine;

namespace PaperWings.Combat
{
    /// <summary>Tuning for a staple-style projectile weapon. One asset per weapon/owner type.</summary>
    [CreateAssetMenu(fileName = "WeaponSettings", menuName = "PaperWings/Weapon Settings")]
    public class WeaponSettings : ScriptableObject
    {
        [Header("Firing")]
        [Tooltip("Seconds between shots while the trigger is held.")]
        public float fireInterval = 0.22f;

        [Tooltip("Max staples carried. Fully refilled when the plane is back in hand. 0 = unlimited.")]
        public int clipSize = 30;

        [Tooltip("Staples regained per second while flying and not shooting (a slow trickle, so you're never dry for long). 0 = off.")]
        public float refillPerSecond = 0.75f;

        [Tooltip("Seconds after your last shot before the trickle refill starts.")]
        public float refillDelay = 1.5f;

        [Header("Projectile")]
        [Tooltip("Muzzle speed (units/sec), added on top of the shooter's own velocity.")]
        public float projectileSpeed = 75f;

        [Tooltip("Seconds before a staple that hit nothing disappears.")]
        public float projectileLifetime = 1.2f;

        [Tooltip("Paper damage per hit. Rivals have ~60 paper, so ~4 hits.")]
        public float damage = 18f;

        [Tooltip("Random spread (degrees). A little looks more papery.")]
        [Range(0f, 10f)] public float spread = 1f;

        [Header("Aim Assist  (forgiving)")]
        [Tooltip("Shots bend toward a target inside this cone (degrees) in front of the nose. 0 = off.")]
        [Range(0f, 30f)] public float aimAssistAngle = 8f;

        [Tooltip("Aim assist only considers targets closer than this.")]
        public float aimAssistRange = 45f;
    }
}
