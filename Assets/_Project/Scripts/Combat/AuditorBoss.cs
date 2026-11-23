using System.Collections;
using UnityEngine;
using PaperWings.Air;
using PaperWings.Flight;

namespace PaperWings.Combat
{
    /// <summary>
    /// THE AUDITOR (Stage 29): the break room's boss, and the first one that FLIES.
    ///
    /// A giant bomber folded from a whole ledger, circling the room under the ceiling. It is a
    /// dogfight with something much bigger than you:
    ///   - Two WING TURRETS fire aimed bursts whenever you're in their sights. They are parts: shoot
    ///     them off and the sky gets a lot quieter.
    ///   - STRAFING RUN: it turns toward you and both turrets fire fast.
    ///   - CLIP BOMBS: it flies straight over you dropping paper-clip bombs, and its wash pushes
    ///     you down. Get out from under it.
    ///   - ESCORTS (phase 2+, every other round): two fighters launch from its belly and shield it
    ///     until you shoot them down.
    ///   - FLAK (phase 3): rings of staples burst from its hull.
    ///   - ENGINE STALL: after each attack its engine overheats and it slows right down: the glowing
    ///     engine at the back is the weak spot. Get on its tail.
    ///   - With both turrets gone it gets desperate: RAMMING DIVES straight at you.
    /// The root stays in the middle of the room (the arena); only the Body flies.
    /// </summary>
    public class AuditorBoss : AirBoss, IMovingTarget
    {
        [Header("Flight")]
        [Tooltip("The part that flies (everything visible, the turrets, the muzzle).")]
        [SerializeField] Transform body;
        [Tooltip("Half-size of the oval it circles (x, z), around this object.")]
        [SerializeField] Vector2 orbitRadius = new Vector2(80f, 42f);
        [SerializeField] float altitude = 70f;
        [SerializeField] float altitudeSwing = 12f;
        [SerializeField] float cruiseSpeed = 15f;
        [SerializeField] float turnRate = 55f;
        [Tooltip("Keeps the body inside the room (local to this object): min / max corner.")]
        [SerializeField] Vector3 boundsMin = new Vector3(-110f, 18f, -60f);
        [SerializeField] Vector3 boundsMax = new Vector3(110f, 105f, 60f);

        [Header("Weapons")]
        [SerializeField] BossPart[] turrets;
        [Tooltip("Where each turret fires from (same order as Turrets).")]
        [SerializeField] Transform[] turretMuzzles;
        [SerializeField] float turretInterval = 1.6f;
        [SerializeField] int turretBurst = 3;
        [SerializeField] float turretRange = 85f;
        [Tooltip("Under the belly: drops bombs from here.")]
        [SerializeField] Transform bombBay;
        [Tooltip("Downdraft wash under the belly during bomb runs.")]
        [SerializeField] ColumnAir wash;
        [SerializeField] Transform[] propellers;

        protected override string BaseHint => "ARMORED: dodge its runs, shoot off the turrets, hit the engine when it stalls";
        public override string WeakSpotText => "ENGINE STALLED! get on its tail and shoot the engine";

        public Vector3 Velocity { get; private set; }

        enum Move { Orbit, Pass, Dive, Crash }
        Move move;
        Vector3 passTarget, startPos, washOffset;
        Quaternion startRot;
        float speedScale = 1f, orbitAngle, nextTurret, bank, crashTime;
        float turretRate = 1f;
        int round;

        protected override void Awake()
        {
            if (body != null) { startPos = body.localPosition; startRot = body.localRotation; }
            if (wash != null && body != null) washOffset = wash.transform.position - body.position;
            base.Awake();
        }

        protected override void AirOff()
        {
            if (wash != null) wash.targetStrength = 0f;
        }

        protected override void OnResetBoss()
        {
            round = 0;
            move = Move.Orbit;
            speedScale = 1f;
            turretRate = 1f;
            orbitAngle = 0f;
            if (body != null) { body.localPosition = startPos; body.localRotation = startRot; }
        }

        int TurretsLeft { get { int n = 0; if (turrets != null) foreach (var t in turrets) if (t != null && !t.Broken) n++; return n; } }

        protected override IEnumerator Round(PlaneController player)
        {
            round++;
            float pace = Phase >= 3 ? 0.8f : 1f;

            if (Phase >= 2 && drones != null && round % 2 == 0)
            {
                yield return Telegraph("ESCORTS LAUNCHED!  shoot them down to break its guard", settings.telegraph);
                yield return DroneShield(30f);
            }

            if (TurretsLeft == 0 && round % 2 == 1)
            {
                // Desperate: ram the player.
                yield return Telegraph("RAMMING DIVE!  break left or right!", settings.telegraph * 0.9f);
                yield return Dive();
            }
            else if (round % 2 == 1)
            {
                yield return Telegraph("STRAFING RUN!  get out of its sights", settings.telegraph);
                turretRate = 2.5f;
                float end = Time.time + settings.attackDuration * pace;
                bool volley = false;
                while (Time.time < end && !IsDefeated)
                {
                    var p = FindFlyingPlayer();
                    if (p != null) { move = Move.Pass; passTarget = p.transform.position + Vector3.up * 6f; }
                    if (!volley && Time.time > end - settings.attackDuration * 0.5f) { volley = true; FireVolley(p, settings.shotsPerVolley, settings.spreadAngle); }
                    yield return null;
                }
                turretRate = 1f;
                move = Move.Orbit;
            }
            else
            {
                yield return Telegraph("CLIP BOMBS!  get out from under it", settings.telegraph);
                yield return BombRun(pace);
            }

            if (Phase >= 3) { FireRing(14, 0f, 0.6f); FireRing(14, -20f, 0.6f, 12f); }

            // ENGINE STALL: slow, the engine glows.
            speedScale = 0.45f;
            yield return WeakWindow(settings.vulnerableTime * (TurretsLeft == 0 ? 1.3f : 1f));
            speedScale = 1f;
        }

        IEnumerator BombRun(float pace)
        {
            var p = FindFlyingPlayer();
            if (p == null || body == null) yield break;
            // Line up from where it is, across the player, and out the other side.
            Vector3 across = p.transform.position - body.position;
            across.y = 0f;
            if (across.sqrMagnitude < 1f) across = body.forward;
            Vector3 end = p.transform.position + across.normalized * 70f;
            end.y = Mathf.Max(p.transform.position.y + 22f, transform.position.y + boundsMin.y + 25f);
            move = Move.Pass;
            passTarget = end;
            if (wash != null) { wash.verticalSpeed = -settings.secondaryAirSpeed; wash.targetStrength = 1f; }

            float stop = Time.time + settings.attackDuration * 1.4f * pace, nextBomb = 0f;
            while (Time.time < stop && !IsDefeated)
            {
                if (Vector3.Distance(Flat(body.position), Flat(passTarget)) < 12f) break;
                var pl = FindFlyingPlayer();
                bool over = pl != null && Vector3.Distance(Flat(body.position), Flat(pl.transform.position)) < 35f;
                if (over && Time.time >= nextBomb && bombBay != null)
                {
                    nextBomb = Time.time + (Phase >= 3 ? 0.18f : 0.26f);
                    Drop(bombBay.position + body.right * Random.Range(-4f, 4f), Velocity, 24f);
                }
                yield return null;
            }
            if (wash != null) wash.targetStrength = 0f;
            move = Move.Orbit;
        }

        IEnumerator Dive()
        {
            var p = FindFlyingPlayer();
            if (p == null) yield break;
            move = Move.Dive;
            passTarget = p.transform.position + p.Velocity * 1.2f;
            speedScale = 2.2f;
            float stop = Time.time + 4f;
            while (Time.time < stop && !IsDefeated && Vector3.Distance(body.position, passTarget) > 10f) yield return null;
            speedScale = 1f;
            move = Move.Orbit;
            FireRing(12, 10f, 0.6f);
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        protected override void OnDefeated()
        {
            move = Move.Crash;
            crashTime = Time.time;
        }

        protected override void Update()
        {
            base.Update();
            if (body == null) return;
            float dt = Time.deltaTime;

            if (move == Move.Crash)
            {
                // Spiral down onto the floor, then sit there smoking until the rematch reset.
                float t = Time.time - crashTime;
                if (body.position.y > transform.position.y + 6f)
                {
                    Velocity = Vector3.Lerp(Velocity, body.forward * cruiseSpeed * 0.6f + Vector3.down * 18f, 1f - Mathf.Exp(-2f * dt));
                    body.position += Velocity * dt;
                    body.rotation = Quaternion.LookRotation(Velocity.normalized) * Quaternion.Euler(0f, 0f, t * 140f);
                }
                else Velocity = Vector3.zero;
                return;
            }

            bool flying = IsAwake && !IsDefeated;
            // Idle (player away or in hand): keep cruising slowly so it's visibly up there.
            float speed = cruiseSpeed * (flying ? speedScale : 0.6f) * (Phase >= 3 ? 1.15f : 1f);

            Vector3 goal;
            if (move == Move.Orbit || !flying)
            {
                // A point a bit further round the oval.
                Vector3 local = Quaternion.Inverse(transform.rotation) * (body.position - transform.position);
                float a = Mathf.Atan2(local.z / orbitRadius.y, local.x / orbitRadius.x);
                orbitAngle = a + 0.45f;
                goal = transform.position + transform.rotation * new Vector3(Mathf.Cos(orbitAngle) * orbitRadius.x,
                    altitude + Mathf.Sin(orbitAngle * 2f) * altitudeSwing, Mathf.Sin(orbitAngle) * orbitRadius.y);
            }
            else goal = passTarget;

            // Steer toward the goal at a limited turn rate, inside the room.
            Vector3 to = goal - body.position;
            if (to.sqrMagnitude < 0.01f) to = body.forward;
            float rate = turnRate * (move == Move.Dive ? 1.6f : 1f);
            Quaternion want = Quaternion.LookRotation(to.normalized);
            Quaternion heading = Quaternion.RotateTowards(Quaternion.LookRotation(Velocity.sqrMagnitude > 0.1f ? Velocity.normalized : body.forward), want, rate * dt);
            Vector3 forward = heading * Vector3.forward;
            // Never point steeply up or down (it's a bomber, not a stunt plane).
            float maxPitch = move == Move.Dive ? 50f : 25f;
            Vector3 flat = Flat(forward);
            if (flat.sqrMagnitude < 0.001f) flat = Flat(body.forward);
            float pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(forward.y, -1f, 1f)) * Mathf.Rad2Deg, -maxPitch, maxPitch);
            forward = Quaternion.AngleAxis(-pitch, Vector3.Cross(Vector3.up, flat.normalized)) * flat.normalized;
            if (forward.sqrMagnitude < 0.001f) forward = body.forward;

            Velocity = forward * speed;
            Vector3 next = body.position + Velocity * dt;
            Vector3 lp = Quaternion.Inverse(transform.rotation) * (next - transform.position);
            lp = Vector3.Max(boundsMin, Vector3.Min(boundsMax, lp));
            next = transform.position + transform.rotation * lp;

            float yawRate = Mathf.DeltaAngle(body.eulerAngles.y, Quaternion.LookRotation(forward).eulerAngles.y) / Mathf.Max(dt, 1e-4f);
            bank = Mathf.Lerp(bank, Mathf.Clamp(-yawRate * 0.6f, -40f, 40f), 1f - Mathf.Exp(-3f * dt));
            body.SetPositionAndRotation(next, Quaternion.LookRotation(forward) * Quaternion.Euler(0f, 0f, bank));
            if (wash != null) wash.transform.position = body.position + washOffset;

            if (propellers != null) foreach (var pr in propellers) if (pr != null) pr.Rotate(0f, 0f, (WeakSpotOpen ? 200f : 1500f) * dt, Space.Self);

            // Turrets fire on their own whenever the player is in range and in front-ish.
            if (flying && !Waking && Time.time >= nextTurret && turrets != null)
            {
                var player = FindFlyingPlayer();
                nextTurret = Time.time + turretInterval / turretRate * (Phase >= 3 ? 0.75f : 1f);
                if (player != null)
                    for (int i = 0; i < turrets.Length; i++)
                    {
                        if (turrets[i] == null || turrets[i].Broken) continue;
                        Transform m = turretMuzzles != null && i < turretMuzzles.Length ? turretMuzzles[i] : turrets[i].transform;
                        Vector3 d = player.transform.position - m.position;
                        if (d.magnitude < turretRange && Vector3.Dot(d.normalized, Flat(body.forward).normalized) > -0.3f)
                            FireAimed(m, player, turretBurst, 3f);
                    }
            }
        }
    }
}
