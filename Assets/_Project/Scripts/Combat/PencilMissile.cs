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
        public PlaneController Target { get; private set; }

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
        public void Launch(PlaneController shooter, PlaneController target, PencilSettings pencilSettings)
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
                    float travel = Vector3.Distance(transform.position, Target.transform.position) / Mathf.Max(speed, 1f);
                    Vector3 lead = Target.transform.position + Target.Velocity * travel;
                    Vector3 desired = (lead - transform.position).normalized;
                    forward = Vector3.RotateTowards(forward, desired, settings.turnRate * Mathf.Deg2Rad * dt, 0f);
                    transform.rotation = Quaternion.LookRotation(forward);

                    if (Vector3.Distance(transform.position, Target.transform.position) < settings.proximityFuse)
                    {
                        Hit(Target);
                        return;
                    }
                }
                velocity = forward * speed;
            }

            MoveAndCollide(velocity * dt);
        }

        bool TargetAlive => Target != null && Target.State == PlaneState.Flying;

        /// <summary>Moves, raycasting along the way so fast pencils can't pass through things.</summary>
        void MoveAndCollide(Vector3 step)
        {
            float dist = step.magnitude;
            if (dist < 1e-5f) return;

            var hits = Physics.RaycastAll(transform.position, step / dist, dist, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Collider nearest = null;
            foreach (var h in hits)
            {
                if (owner != null && h.collider.GetComponentInParent<PlaneController>() == owner) continue;
                if (h.distance < best) { best = h.distance; nearest = h.collider; }
            }

            if (nearest != null)
            {
                var victim = nearest.GetComponentInParent<PlaneController>();
                if (victim != null) Hit(victim);
                else Destroy(gameObject);   // hit the desk, a book, the floor...
                return;
            }
            transform.position += step;
        }

        void Hit(PlaneController victim)
        {
            var paper = victim.GetComponent<PaperDamage>();
            if (paper != null) paper.ApplyDamage(settings.damage, owner);
            CombatScore.ReportHit(owner, victim);
            Destroy(gameObject);
        }
    }
}
