using UnityEngine;
using PaperWings.Flight;

namespace PaperWings.Combat
{
    /// <summary>
    /// A small pencil that rides under the wing until it's launched, then homes in on its target.
    ///   Mounted  - parented to a wing slot, just decoration.
    ///   Dropping - detached: falls away from the plane for a moment (so you see it leave the wing).
    ///   Homing   - "ignites": speeds up and turns toward where the target WILL be, trailing graphite smoke.
    /// Turning is rate-limited, so a hard break turn at the right moment can still dodge it.
    /// </summary>
    public class PencilMissile : MonoBehaviour
    {
        public enum Phase { Mounted, Dropping, Homing }

        [SerializeField] TrailRenderer smokeTrail;

        public Phase CurrentPhase { get; private set; } = Phase.Mounted;
        public CombatTarget Target { get; private set; }

        PlaneController owner;
        PencilSettings settings;
        Vector3 velocity;
        float speed;
        float phaseTimer;
        float dieTime;

        void Awake()
        {
            if (smokeTrail != null) smokeTrail.emitting = false;
        }

        /// <summary>Detach from the wing and go after 'target'.</summary>
        public void Launch(PlaneController shooter, CombatTarget target, PencilSettings pencilSettings)
        {
            owner = shooter;
            Target = target;
            settings = pencilSettings;

            transform.SetParent(null, worldPositionStays: true);
            velocity = shooter.Velocity + Vector3.down * 2f;   // a little push downward, away from the wing
            phaseTimer = settings.dropTime;
            dieTime = Time.time + settings.lifetime;
            CurrentPhase = Phase.Dropping;
        }

        void Update()
        {
            if (CurrentPhase == Phase.Mounted) return;
            float dt = Time.deltaTime;
            if (Time.time >= dieTime) { Destroy(gameObject); return; }

            if (CurrentPhase == Phase.Dropping)
            {
                velocity += Vector3.down * 9.81f * dt;
                phaseTimer -= dt;
                if (phaseTimer <= 0f)
                {
                    CurrentPhase = Phase.Homing;
                    Audio.AudioFx.PlayAt(Audio.Sfx.PencilLaunch, transform.position, 1f);   // ignition whoosh
                    speed = velocity.magnitude;
                    if (smokeTrail != null) { smokeTrail.Clear(); smokeTrail.emitting = true; }
                }
            }
            else
            {
                speed = Mathf.MoveTowards(speed, settings.speed, settings.acceleration * dt);

                Vector3 forward = transform.forward;
                if (TargetAlive)
                {
                    // Aim at the lead point and turn toward it, but only so fast.
                    float travel = Vector3.Distance(transform.position, Target.Position) / Mathf.Max(speed, 1f);
                    Vector3 lead = Target.Position + Target.Velocity * travel;
                    Vector3 desired = (lead - transform.position).normalized;
                    forward = Vector3.RotateTowards(forward, desired, settings.turnRate * Mathf.Deg2Rad * dt, 0f);
                    transform.rotation = Quaternion.LookRotation(forward);

                    // Proximity fuse: near misses still count.
                    if (Vector3.Distance(transform.position, Target.Position) < settings.proximityFuse)
                    {
                        Projectiles.ApplyHit(Target, settings.damage, owner, true, Audio.Sfx.PencilImpact);
                        Destroy(gameObject);
                        return;
                    }
                }
                velocity = forward * speed;
            }

            // Move, sweeping for anything in the way (the desk, a book, a plane, the boss...).
            Vector3 step = velocity * dt;
            if (Projectiles.SweepHit(transform.position, step, owner != null ? owner.transform : null, out RaycastHit hit))
            {
                if (!Projectiles.ApplyHit(hit.collider, settings.damage, owner, true, Audio.Sfx.PencilImpact))
                    Audio.AudioFx.PlayAt(Audio.Sfx.PencilImpact, hit.point, 0.6f, 0.85f);   // thudded into the scenery
                Destroy(gameObject);
                return;
            }
            transform.position += step;
        }

        bool TargetAlive => Target != null && Target.IsAlive;
    }
}
