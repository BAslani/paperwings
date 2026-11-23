using System;
using UnityEngine;
using PaperWings.Flight;

namespace PaperWings.Combat
{
    /// <summary>
    /// A scripted flying target for missions (Stage 29): it flies a list of waypoints at a steady
    /// speed, banking into its turns, until it reaches the end or is shot down.
    ///   - Accounting COURIERS / BOMBERS: shoot them down before they get away (Intercept nights).
    ///   - Marketing COURIERS: keep the rivals off them until they arrive (Escort nights).
    ///   - PRACTICE DRONES: loop forever, easy to hit (the first night).
    /// It's not a PlaneController (no flight model): it flies a fixed line so the fight around
    /// it is readable. A ConvoySpawner owns it and counts what happened to it.
    /// </summary>
    public class PathFlyer : MonoBehaviour, IDamageable, IMovingTarget
    {
        [SerializeField] Transform visual;
        [Tooltip("Played when it's shot down.")]
        [SerializeField] ParticleSystem downBurst;
        [Tooltip("Turns this many degrees per second toward the next waypoint (lower = wider, lazier turns).")]
        [SerializeField] float turnRate = 70f;
        [SerializeField] float maxBank = 35f;
        [Tooltip("Gentle up-and-down drift so it looks like paper, not a rail car.")]
        [SerializeField] float bobAmount = 0.6f;

        /// <summary>Args: this flyer, true = reached the end, false = shot down. Raised once.</summary>
        public event Action<PathFlyer, bool> Finished;

        public bool IsAlive => !finished && health > 0f;
        public float Health01 => maxHealth > 0f ? health / maxHealth : 0f;
        public Vector3 Velocity { get; private set; }
        public PlaneController LastAttacker { get; private set; }
        public Faction Faction => target != null ? target.Faction : Faction.Accounting;

        Vector3[] path;
        bool loop;
        int next;
        float speed, health, maxHealth, bank, bobPhase;
        bool finished;
        CombatTarget target;
        Vector3 basePos;

        /// <summary>Sets it flying from the first point. Faction decides who shoots at it.</summary>
        public void Launch(Vector3[] points, bool loopPath, float flySpeed, float hitPoints, Faction faction, string displayName)
        {
            path = points;
            loop = loopPath;
            speed = flySpeed;
            maxHealth = health = Mathf.Max(1f, hitPoints);
            finished = false;
            next = Mathf.Min(1, points.Length - 1);
            bobPhase = UnityEngine.Random.value * 10f;
            basePos = points[0];
            transform.position = basePos;
            if (points.Length > 1) transform.rotation = Quaternion.LookRotation(points[1] - points[0]);

            target = GetComponent<CombatTarget>();
            if (target == null) target = gameObject.AddComponent<CombatTarget>();
            target.Faction = faction;
            target.DisplayName = displayName;
            target.AiTarget = true;
        }

        void Update()
        {
            if (finished || path == null || path.Length == 0) return;

            Vector3 goal = path[next];
            Vector3 to = goal - basePos;
            if (to.magnitude < Mathf.Max(4f, speed * 0.6f))
            {
                next++;
                if (next >= path.Length)
                {
                    if (loop) next = 0;
                    else { Finish(true); return; }
                }
                goal = path[next];
                to = goal - basePos;
            }

            // Turn toward the goal at a limited rate, bank into the turn.
            Quaternion want = Quaternion.LookRotation(to.normalized);
            Quaternion heading = Quaternion.RotateTowards(Quaternion.LookRotation(transform.forward), want, turnRate * Time.deltaTime);
            float yawDelta = Mathf.DeltaAngle(transform.eulerAngles.y, want.eulerAngles.y);
            bank = Mathf.Lerp(bank, Mathf.Clamp(-yawDelta * 0.8f, -maxBank, maxBank), 1f - Mathf.Exp(-3f * Time.deltaTime));

            Vector3 forward = heading * Vector3.forward;
            Vector3 step = forward * speed * Time.deltaTime;
            basePos += step;
            Velocity = forward * speed;

            float bob = Mathf.Sin((Time.time + bobPhase) * 1.7f) * bobAmount;
            transform.SetPositionAndRotation(basePos + Vector3.up * bob, heading * Quaternion.Euler(0f, 0f, bank));
        }

        public void ApplyDamage(float amount, PlaneController source)
        {
            if (!IsAlive || amount <= 0f) return;
            health = Mathf.Max(0f, health - amount);
            if (source != null) LastAttacker = source;
            if (health <= 0f) Finish(false);
        }

        void Finish(bool arrived)
        {
            if (finished) return;
            finished = true;
            Velocity = Vector3.zero;
            if (!arrived)
            {
                if (downBurst != null) { downBurst.transform.SetParent(null, true); downBurst.Play(); Destroy(downBurst.gameObject, 4f); }
                Audio.AudioFx.PlayAt(Audio.Sfx.ShotDown, transform.position, 1f);
            }
            Finished?.Invoke(this, arrived);
            Destroy(gameObject, arrived ? 0f : 0.05f);
        }

        void OnDrawGizmosSelected()
        {
            if (path == null) return;
            Gizmos.color = Color.yellow;
            for (int i = 1; i < path.Length; i++) Gizmos.DrawLine(path[i - 1], path[i]);
        }
    }
}
