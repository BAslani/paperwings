using UnityEngine;
using PaperWings.Air;
using PaperWings.Flight;

namespace PaperWings.Combat
{
    /// <summary>
    /// AI that flies a paper plane, for any faction: Accounting rivals or Marketing wingmen.
    /// It implements IFlightInput, so it "holds the stick" exactly like the player: same flight
    /// model, same energy rules, same air.
    ///
    /// Each frame it picks a goal, then steers toward a target point:
    ///   Avoid     - feelers see scenery ahead: take the clearest heading nearest the goal.
    ///   Recover   - too low or too slow: find the nearest thermal and circle up in it.
    ///   Chase     - an enemy plane is near: fly at where it's going, shoot when lined up.
    ///   Formation - (wingmen) no enemy: hold position off the leader's wing.
    ///   Patrol    - (rivals) no enemy: circle the patrol point at a comfortable height.
    /// Simple on purpose: easy to read, easy to tune, easy to make smarter later.
    /// </summary>
    [RequireComponent(typeof(PlaneController))]
    public class RivalPilot : MonoBehaviour, IFlightInput
    {
        public enum Goal { Patrol, Chase, Recover, Avoid, Formation }

        [SerializeField] RivalPilotSettings settings;
        [SerializeField] StapleGun gun;

        public Goal CurrentGoal { get; private set; }
        /// <summary>Its decision knobs (an ace night swaps in sharper ones).</summary>
        public RivalPilotSettings Settings { get => settings; set => settings = value; }
        public Vector3 PatrolCenter { get; set; }
        /// <summary>Wingmen: the plane to fly formation with (the player). Null for rivals.</summary>
        public PlaneController Leader { get; set; }
        /// <summary>Wingmen: formation slot relative to the leader (x right, y up, z forward).</summary>
        public Vector3 FormationOffset { get; set; } = new Vector3(7f, 1f, -6f);
        /// <summary>The enemy currently being chased (for debugging / HUD).</summary>
        public CombatTarget CurrentEnemy { get; private set; }

        public float PitchInput { get; private set; }
        public float RollInput { get; private set; }
        public bool ConsumeBoost() => false;   // AI doesn't boost (yet)

        PlaneController plane;
        Faction faction;
        float burstShotsLeft;
        float burstReadyTime;
        float patrolAngle;

        void Awake()
        {
            plane = GetComponent<PlaneController>();
            if (gun == null) gun = GetComponent<StapleGun>();
            PatrolCenter = transform.position;
            patrolAngle = Random.value * 360f;
            faction = CombatTarget.FactionOfPlane(plane);
        }

        void Update()
        {
            if (settings == null || plane.State != PlaneState.Flying)
            {
                PitchInput = RollInput = 0f;
                return;
            }

            CurrentEnemy = FindEnemy();
            Vector3 target = ChooseTarget(CurrentEnemy);
            target = AvoidObstacles(target, out bool avoiding);
            if (avoiding) CurrentGoal = Goal.Avoid;
            SteerToward(target, avoiding);
            if (CurrentGoal == Goal.Chase) TryShoot(CurrentEnemy);
        }

        /// <summary>Nearest enemy plane in range, keeping the current one a bit longer (so it doesn't flip-flop).</summary>
        CombatTarget FindEnemy()
        {
            if (CurrentEnemy != null && CurrentEnemy.IsAlive &&
                Vector3.Distance(transform.position, CurrentEnemy.Position) < settings.disengageRange)
                return CurrentEnemy;
            return CombatTarget.FindNearestEnemyPlane(transform.position, faction, settings.engageRange);
        }

        // ------------------------------------------------------------------ decisions

        Vector3 ChooseTarget(CombatTarget enemy)
        {
            Vector3 pos = transform.position;

            float height = HeightAboveSurface();
            bool low = height < settings.lowHeight || plane.IsStalling;

            // 2. Low or slow: circle in the nearest thermal (or just glide straight to build speed).
            if (low)
            {
                CurrentGoal = Goal.Recover;
                if (TryFindThermal(out Vector3 thermalCenter)) return CirclePoint(thermalCenter, 7f, pos.y + 5f);
                return pos + Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized * 20f;
            }

            // 3. Enemy plane nearby: chase where it's going.
            if (enemy != null)
            {
                CurrentGoal = Goal.Chase;
                return enemy.Position + enemy.Velocity * 0.6f;   // lead the target
            }

            // 4. Wingmen: hold a slot off the leader's wing (a little ahead of it, so we keep up).
            if (Leader != null && Leader.State == PlaneState.Flying)
            {
                CurrentGoal = Goal.Formation;
                Transform l = Leader.transform;
                Quaternion heading = Quaternion.Euler(0f, l.eulerAngles.y, 0f);
                return l.position + heading * FormationOffset + l.forward * 10f;
            }

            // 5. Patrol: circle the patrol point at a comfortable height above the ground there.
            CurrentGoal = Goal.Patrol;
            return CirclePoint(PatrolCenter, settings.patrolRadius, PatrolAltitude());
        }

        /// <summary>A point a little ahead of us on a circle around 'center', so following it makes us orbit.</summary>
        Vector3 CirclePoint(Vector3 center, float radius, float y)
        {
            Vector3 fromCenter = transform.position - center;
            fromCenter.y = 0f;
            float angle = fromCenter.sqrMagnitude > 0.01f ? Mathf.Atan2(fromCenter.x, fromCenter.z) * Mathf.Rad2Deg : patrolAngle;
            patrolAngle = angle + 35f;   // aim 35 degrees further around the circle
            Vector3 offset = Quaternion.Euler(0f, patrolAngle, 0f) * Vector3.forward * radius;
            return new Vector3(center.x + offset.x, y, center.z + offset.z);
        }

        // ------------------------------------------------------------------ steering

        void SteerToward(Vector3 target, bool urgent)
        {
            float stick = urgent ? 1f : settings.aggressiveness;
            Vector3 to = target - transform.position;
            Vector3 flat = new Vector3(to.x, 0f, to.z);

            // Heading: bank toward the target (the flight model turns us from the bank).
            float desiredYaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
            float yawError = Mathf.DeltaAngle(transform.eulerAngles.y, desiredYaw);
            RollInput = Mathf.Clamp(yawError / 35f, -1f, 1f) * stick;

            // Height: pitch toward the angle that points at the target, gently.
            float desiredPitch = Mathf.Clamp(Mathf.Atan2(to.y, Mathf.Max(flat.magnitude, 1f)) * Mathf.Rad2Deg, -35f, 25f);
            float pitchError = desiredPitch - plane.PitchAngle;
            PitchInput = Mathf.Clamp(pitchError / 15f, -1f, 1f) * stick;
        }

        void TryShoot(CombatTarget enemy)
        {
            if (gun == null || enemy == null || !enemy.IsAlive) return;
            Vector3 to = enemy.Position - transform.position;
            if (to.magnitude > settings.fireRange || Vector3.Angle(transform.forward, to) > settings.fireAngle) return;
            if (Time.time < burstReadyTime) return;

            if (gun.TryFire() && --burstShotsLeft <= 0)
            {
                burstShotsLeft = settings.burstLength;
                burstReadyTime = Time.time + settings.burstPause;
            }
        }

        // ------------------------------------------------------------------ sensing

        // ---- Obstacle avoidance: "feelers" ----
        // Probes the way it wants to go and the way it's pointing, far enough ahead for its
        // current speed. If either is blocked, it tries a fan of headings (left/right, up/down)
        // and takes the clear one closest to where it wanted to go. Racks, walls, shelves and
        // gantries all just look like "blocked" to it.
        static readonly float[] YawFan = { 0f, 25f, -25f, 50f, -50f, 80f, -80f, 115f, -115f, 150f, -150f };
        static readonly float[] PitchFan = { 0f, 22f, -18f, 40f };

        float Lookahead => Mathf.Max(settings.avoidDistance, plane.Speed * 1.4f + 6f);

        Vector3 AvoidObstacles(Vector3 target, out bool avoiding)
        {
            Vector3 pos = transform.position;
            Vector3 desired = target - pos;
            if (desired.sqrMagnitude < 0.01f) desired = transform.forward;
            desired.Normalize();
            float look = Lookahead;

            avoiding = false;
            if (FreeDistance(transform.forward, look) >= look && FreeDistance(desired, look) >= look) return target;

            avoiding = true;
            float heading = transform.eulerAngles.y;
            float climb = plane.PitchAngle;
            Vector3 best = transform.forward;
            float bestScore = float.MinValue;
            foreach (float dp in PitchFan)
            {
                foreach (float dy in YawFan)
                {
                    float pitch = Mathf.Clamp(climb * 0.3f + dp, -30f, 40f);
                    Vector3 dir = Quaternion.Euler(-pitch, heading + dy, 0f) * Vector3.forward;
                    float free = FreeDistance(dir, look);
                    // Mostly "how open is it", then "how close to what I wanted", and a mild
                    // preference for small turns (big ones bleed speed).
                    float score = free / look * 3f + Vector3.Dot(dir, desired) - Mathf.Abs(dy) / 180f - Mathf.Abs(dp) / 120f;
                    if (free >= look) score += 2f;
                    if (score > bestScore) { bestScore = score; best = dir; }
                }
            }
            return pos + best * 20f;
        }

        /// <summary>How far it can fly along 'dir' (up to 'max') before hitting scenery. Planes and mission flyers don't count.</summary>
        float FreeDistance(Vector3 dir, float max)
        {
            const float radius = 1.6f;   // a wingspan and a bit
            var hits = Physics.SphereCastAll(transform.position, radius, dir, max, ~0, QueryTriggerInteraction.Ignore);
            float nearest = max;
            foreach (var h in hits)
            {
                if (h.distance <= 0f) continue;   // started inside it (its own collider, a trigger-ish overlap)
                if (h.collider.GetComponentInParent<PlaneController>() != null) continue;
                if (h.collider.GetComponentInParent<PathFlyer>() != null) continue;
                nearest = Mathf.Min(nearest, h.distance);
            }
            return nearest;
        }

        // ---- Patrol height: above everything under the patrol circle, under the ceiling ----
        Vector3 altitudeCenter = new Vector3(float.NaN, 0f, 0f);
        float patrolAltitude;

        float PatrolAltitude()
        {
            if (!float.IsNaN(altitudeCenter.x) && (PatrolCenter - altitudeCenter).sqrMagnitude < 1f) return patrolAltitude;
            altitudeCenter = PatrolCenter;

            // The tallest thing under the circle it flies (rack rows, shelves, the boss...).
            float ground = GroundBelow(PatrolCenter);
            for (int i = 0; i < 12; i++)
            {
                Vector3 p = PatrolCenter + Quaternion.Euler(0f, i * 30f, 0f) * Vector3.forward * settings.patrolRadius;
                ground = Mathf.Max(ground, GroundBelow(p));
            }
            float y = ground + settings.preferredHeight;

            // Stay well under the ceiling (and the gantry, the cable trays...).
            if (Physics.Raycast(new Vector3(PatrolCenter.x, ground + 2f, PatrolCenter.z), Vector3.up, out RaycastHit up, 600f, ~0, QueryTriggerInteraction.Ignore))
                y = Mathf.Min(y, Mathf.Max(ground + 8f, Mathf.Max((ground + up.point.y) * 0.5f, up.point.y - 14f)));
            patrolAltitude = y;
            return y;
        }

        /// <summary>Height of whatever is under 'point', looking down from the patrol point's height (not from above the roof).</summary>
        float GroundBelow(Vector3 point)
        {
            Vector3 from = new Vector3(point.x, PatrolCenter.y + 2f, point.z);
            return Physics.Raycast(from, Vector3.down, out RaycastHit hit, 600f, ~0, QueryTriggerInteraction.Ignore)
                ? hit.point.y : PatrolCenter.y - settings.preferredHeight;
        }

        bool TryFindThermal(out Vector3 center)
        {
            center = Vector3.zero;
            float best = float.MaxValue;
            foreach (var source in AirField.Sources)
            {
                if (!source.OffersLiftCenter) continue;
                Vector3 p = source.transform.position;
                float d = Vector3.Distance(new Vector3(p.x, 0f, p.z), new Vector3(transform.position.x, 0f, transform.position.z));
                if (d < best) { best = d; center = p; }
            }
            return best < float.MaxValue;
        }

        float HeightAboveSurface()
        {
            return Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 500f, ~0, QueryTriggerInteraction.Ignore)
                ? hit.distance : 500f;
        }
    }
}
