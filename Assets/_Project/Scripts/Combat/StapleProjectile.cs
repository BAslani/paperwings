using UnityEngine;
using PaperWings.Flight;

namespace PaperWings.Combat
{
    /// <summary>
    /// A fired staple. It moves itself and raycasts from its last position to its new one every
    /// frame, so even very fast shots can't skip through thin targets (no Rigidbody needed).
    /// On hitting a plane it deals paper damage; on hitting anything else it just disappears.
    /// </summary>
    public class StapleProjectile : MonoBehaviour
    {
        PlaneController owner;
        Vector3 velocity;
        float damage;
        float dieTime;


        public void Launch(PlaneController shooter, Vector3 startVelocity, float hitDamage, float lifetime)
        {
            owner = shooter;
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

            // Check everything along this frame's path; ignore trigger zones and our own shooter.
            var hits = Physics.RaycastAll(from, step.normalized, step.magnitude, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            RaycastHit? nearest = null;
            foreach (var h in hits)
            {
                if (owner != null && h.collider.GetComponentInParent<PlaneController>() == owner) continue;
                if (h.distance < best) { best = h.distance; nearest = h; }
            }

            if (nearest.HasValue)
            {
                var victim = nearest.Value.collider.GetComponentInParent<PlaneController>();
                if (victim != null)
                {
                    var paper = victim.GetComponent<PaperDamage>();
                    if (paper != null) paper.ApplyDamage(damage, owner);
                    CombatScore.ReportHit(owner, victim);
                }
                Destroy(gameObject);
                return;
            }

            transform.position = from + step;
        }
    }
}
