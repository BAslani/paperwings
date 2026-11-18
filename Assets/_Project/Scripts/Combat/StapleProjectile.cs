using UnityEngine;
using PaperWings.Flight;

namespace PaperWings.Combat
{
    /// <summary>
    /// A fired staple. It moves itself and raycasts from its last position to its new one every
    /// frame, so even very fast shots can't skip through thin targets (no Rigidbody needed).
    /// It damages anything with an IDamageable on the OTHER side (no friendly fire), ignores
    /// whoever fired it, and disappears on hitting anything solid.
    /// </summary>
    public class StapleProjectile : MonoBehaviour
    {
        PlaneController ownerPlane;   // for kill credit (null when a boss fires)
        Transform ownerRoot;          // never hit anything under this (the shooter itself)
        bool firedByPlayerSide;
        Vector3 velocity;
        float damage;
        float dieTime;

        public void Launch(PlaneController shooterPlane, Transform shooterRoot, bool playerSide,
                           Vector3 startVelocity, float hitDamage, float lifetime)
        {
            ownerPlane = shooterPlane;
            ownerRoot = shooterRoot;
            firedByPlayerSide = playerSide;
            velocity = startVelocity;
            damage = hitDamage;
            dieTime = Time.time + lifetime;
            transform.rotation = Quaternion.LookRotation(velocity);
        }

        void Update()
        {
            if (Time.time >= dieTime) { Destroy(gameObject); return; }

            Vector3 from = transform.position;
            Vector3 step = velocity * Time.deltaTime;

            if (Projectiles.SweepHit(from, step, ownerRoot, out RaycastHit hit))
            {
                Projectiles.ApplyHit(hit.collider, damage, ownerPlane, firedByPlayerSide);
                Destroy(gameObject);
                return;
            }
            transform.position = from + step;
        }
    }

    /// <summary>Shared projectile helpers (staples, pencils, boss shots).</summary>
    public static class Projectiles
    {
        /// <summary>Nearest solid (non-trigger) hit along 'step', skipping anything under 'ignoreRoot'.</summary>
        public static bool SweepHit(Vector3 from, Vector3 step, Transform ignoreRoot, out RaycastHit nearest)
        {
            nearest = default;
            float dist = step.magnitude;
            if (dist < 1e-5f) return false;

            var hits = Physics.RaycastAll(from, step / dist, dist, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;
            foreach (var h in hits)
            {
                if (ignoreRoot != null && h.collider.transform.IsChildOf(ignoreRoot)) continue;
                if (h.distance < best) { best = h.distance; nearest = h; found = true; }
            }
            return found;
        }

        /// <summary>Damages what was hit if it's damageable and on the other side. Returns true if it took damage.</summary>
        public static bool ApplyHit(Component hit, float damage, PlaneController shooterPlane, bool firedByPlayerSide,
                                    Audio.Sfx hitSound = Audio.Sfx.StapleHit)
        {
            var damageable = hit.GetComponentInParent<IDamageable>();
            if (damageable == null || !damageable.IsAlive) return false;

            bool? side = CombatTarget.SideOf(hit);
            if (side.HasValue && side.Value == firedByPlayerSide) return false;   // no friendly fire

            damageable.ApplyDamage(damage, shooterPlane);
            Audio.AudioFx.PlayAt(hitSound, ((Component)damageable).transform.position, 0.9f);
            CombatScore.ReportHit(shooterPlane, ((Component)damageable).gameObject);
            return true;
        }
    }
}
