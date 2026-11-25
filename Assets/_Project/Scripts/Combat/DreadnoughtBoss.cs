using System.Collections;
using UnityEngine;
using PaperWings.Air;
using PaperWings.Flight;

namespace PaperWings.Combat
{
    /// <summary>
    /// THE DREADNOUGHT (Stage 46): Tiny War's first boss. The family's robot vacuum, woken by its timer,
    /// with a cardboard turret Maya taped on top. It is a GROUND boss built on SUCTION:
    ///   - PATROL: it drives its loop round the kitchen floor with its suction on. The air above it
    ///     pulls anything low down into it, and touching its intake tears paper. Its turret fires
    ///     crumbs at you (aimed volleys, and low rings of crumbs in phase 2+). Stay high.
    ///   - TURBO (phase 2+, every other round): faster, harder suction, a spiral of crumbs.
    ///   - SIDE BRUSHES (parts): its two spinning brushes. Shoot one off and its suction weakens; with
    ///     both gone it barely pulls at all.
    ///   - LOW BATTERY: after each patrol it heads home to charge, beeping. Docked, its DUST BIN pops
    ///     open on top: the weak spot. Dive and shoot into it before it closes.
    /// Biscuit is terrified of it: while it drives she flees to a high perch (CatBrain.Scare).
    /// </summary>
    public class DreadnoughtBoss : AirBoss
    {
        [Header("The vacuum")]
        [Tooltip("The puck itself: drives round the floor (everything that moves with it is under it).")]
        [SerializeField] Transform body;
        [Tooltip("Floor points of its patrol loop, in order.")]
        [SerializeField] Transform[] patrol;
        [Tooltip("Where it charges: it parks here facing this transform's forward.")]
        [SerializeField] Transform dock;
        [SerializeField] float speed = 24f;
        [SerializeField] float turnSpeed = 140f;
        [Tooltip("Rides on the body: pulls low flyers down into it.")]
        [SerializeField] ColumnAir suction;
        [Tooltip("Touching it inside this radius (and below its top + a little) tears paper.")]
        [SerializeField] float contactRadius = 18f;
        [SerializeField] float contactHeight = 12f;
        [Tooltip("The dust bin's lid: swings open while it's docked.")]
        [SerializeField] Transform binLid;
        [SerializeField] float binOpenAngle = -80f;
        [Tooltip("Maya's cardboard turret: turns to face you.")]
        [SerializeField] Transform turret;
        [Tooltip("The spinning side brushes (their meshes spin about local Y).")]
        [SerializeField] Transform[] brushSpinners;
        [SerializeField] ParticleSystem dustTrail;
        [SerializeField] ParticleSystem binDust;
        [Tooltip("Status light: glows red while it hunts, blinks amber on low battery.")]
        [SerializeField] Light statusLight;
        [Tooltip("Scene objects hidden while the boss is out (the sleeping vacuum in its dock).")]
        [SerializeField] GameObject[] hideWhileActive;
        [Tooltip("Seconds between aimed crumb volleys while it patrols.")]
        [SerializeField] float volleyInterval = 2.6f;

        protected override string BaseHint => "ARMORED: stay off the floor, shoot off its side brushes, hit the dust bin when it docks";
        public override string WeakSpotText => "DOCKED! its dust bin is open: shoot inside";

        int round;
        bool driving, docked, turbo;
        int waypoint;
        float bin, nextVolley, nextRing, beep;
        Vector3 restPos;
        Quaternion restRot;

        int BrushesLeft => PartsLeft;
        float SuctionScale => Mathf.Lerp(0.25f, 1f, Parts.Length > 0 ? BrushesLeft / (float)Parts.Length : 1f);

        protected override void Awake()
        {
            if (body != null) { restPos = body.position; restRot = body.rotation; }
            base.Awake();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (hideWhileActive != null) foreach (var g in hideWhileActive) if (g != null) g.SetActive(false);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (hideWhileActive != null) foreach (var g in hideWhileActive) if (g != null) g.SetActive(true);
            if (body != null) Gameplay.CatBrain.Scare(body, false);
        }

        protected override void AirOff()
        {
            if (suction != null) suction.targetStrength = 0f;
            driving = false;
            turbo = false;
            if (dustTrail != null) dustTrail.Stop();
        }

        protected override void OnResetBoss()
        {
            round = 0;
            waypoint = 0;
            docked = true;
            bin = 0f;
            if (body != null && restPos != Vector3.zero) body.SetPositionAndRotation(restPos, restRot);
            if (binLid != null) binLid.localRotation = Quaternion.identity;
        }

        protected override void WakeTick(float t)
        {
            // Beeps, wiggles out of the dock.
            docked = false;
            beep = Mathf.Repeat(t * 4f, 1f) < 0.5f ? 1f : 0f;
            if (body != null && dock != null)
                body.position = Vector3.Lerp(restPos, restPos + dock.forward * 22f, Mathf.SmoothStep(0f, 1f, t));
        }

        protected override IEnumerator Round(PlaneController player)
        {
            round++;
            turbo = Phase >= 2 && round % 2 == 0;
            yield return Telegraph(turbo ? "TURBO MODE!  it's vacuuming flat out" : "PATROL!  it's vacuuming: stay off the floor", settings.telegraph * 0.8f);

            // PATROL: drive the loop with the suction on, firing crumbs.
            driving = true;
            docked = false;
            if (dustTrail != null) dustTrail.Play();
            if (suction != null)
            {
                suction.verticalSpeed = -settings.airSpeed * (turbo ? 1.35f : 1f);
                suction.radialSpeed = -settings.airSpeed * 0.45f;
                suction.targetStrength = SuctionScale;
            }
            float end = Time.time + settings.attackDuration * (turbo ? 2.6f : 2.2f) * (Phase >= 3 ? 1.2f : 1f);
            Coroutine spiral = turbo ? StartCoroutine(FireSpiral(settings.attackDuration * 2.2f, 2.2f, 70f, 2, 18f, 0.5f)) : null;
            while (Time.time < end && !IsDefeated)
            {
                if (suction != null) suction.targetStrength = SuctionScale;
                DriveLoop(turbo ? 1.6f : 1f);
                yield return null;
            }
            if (spiral != null) StopCoroutine(spiral);

            // LOW BATTERY: home to charge, weak suction, beeping.
            yield return Telegraph("LOW BATTERY!  it's heading home to charge", settings.telegraph * 0.6f);
            if (suction != null) suction.targetStrength = SuctionScale * 0.35f;
            float limit = Time.time + 20f;
            while (!AtDock() && Time.time < limit && !IsDefeated)
            {
                DriveTo(DockApproach(), 1.15f);
                yield return null;
            }
            // Back in, nose to the charger.
            float t0 = Time.time;
            var from = body.position;
            var fromRot = body.rotation;
            while (Time.time - t0 < 0.8f)
            {
                float u = Mathf.SmoothStep(0f, 1f, (Time.time - t0) / 0.8f);
                body.position = Vector3.Lerp(from, restPos, u);
                body.rotation = Quaternion.Slerp(fromRot, restRot, u);
                yield return null;
            }
            AirOff();
            docked = true;
            if (binDust != null) binDust.Play();
            Audio.AudioFx.PlayAt(Audio.Sfx.Ding, body.position, 0.9f, 0.6f);
            yield return WeakWindow(settings.vulnerableTime * (Phase >= 3 ? 0.85f : 1f));
            if (binDust != null) binDust.Stop();

            // Off the dock again.
            docked = false;
            t0 = Time.time;
            from = body.position;
            while (Time.time - t0 < 0.7f)
            {
                body.position = Vector3.Lerp(from, restPos + dock.forward * 22f, Mathf.SmoothStep(0f, 1f, (Time.time - t0) / 0.7f));
                yield return null;
            }
        }

        Vector3 DockApproach() => restPos + (dock != null ? dock.forward : Vector3.forward) * 22f;
        bool AtDock() => body != null && Flat(body.position, DockApproach()) < 3f;
        static float Flat(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }

        void DriveLoop(float speedScale)
        {
            if (patrol == null || patrol.Length == 0 || body == null) return;
            var goal = patrol[waypoint % patrol.Length].position;
            if (Flat(body.position, goal) < 4f) { waypoint = (waypoint + 1) % patrol.Length; goal = patrol[waypoint].position; }
            DriveTo(goal, speedScale);
        }

        /// <summary>Turns toward the goal, then rolls forward (slower while turning hard), like a real robot vacuum.</summary>
        void DriveTo(Vector3 goal, float speedScale)
        {
            Vector3 d = goal - body.position; d.y = 0f;
            if (d.sqrMagnitude < 0.01f) return;
            var want = Quaternion.LookRotation(d.normalized, Vector3.up);
            body.rotation = Quaternion.RotateTowards(body.rotation, want, turnSpeed * speedScale * Time.deltaTime);
            float ang = Quaternion.Angle(body.rotation, want);
            float v = speed * speedScale * Mathf.Clamp01(1f - ang / 70f);
            body.position += body.forward * v * Time.deltaTime;
        }

        protected override void OnDefeated()
        {
            AirOff();
            docked = true;   // the bin flops open, dust pours out
            if (binDust != null) binDust.Play();
        }

        protected override void Update()
        {
            base.Update();
            float dt = Time.deltaTime;
            if (body != null) Gameplay.CatBrain.Scare(body, IsAwake && !IsDefeated && !docked);

            bin = Mathf.MoveTowards(bin, docked && (WeakSpotOpen || IsDefeated) ? 1f : 0f, dt * 3f);
            if (binLid != null) binLid.localRotation = Quaternion.Euler(binOpenAngle * bin, 0f, 0f);

            // Brushes spin while it drives (each one that's still attached).
            if (brushSpinners != null)
                for (int i = 0; i < brushSpinners.Length; i++)
                    if (brushSpinners[i] != null) brushSpinners[i].Rotate(0f, (i % 2 == 0 ? 1f : -1f) * (driving ? (turbo ? 1400f : 900f) : 60f) * dt, 0f, Space.Self);

            // Status light: red while hunting, amber blink going home / docked.
            if (statusLight != null)
            {
                bool home = !driving && IsAwake && !IsDefeated;
                float blink = Mathf.Repeat(Time.time * 2f, 1f) < 0.5f ? 1f : 0.2f;
                statusLight.color = home ? new Color(1f, 0.65f, 0.1f) : new Color(1f, 0.15f, 0.1f);
                statusLight.intensity = (IsDefeated ? 0f : home ? 60f * blink : IsAwake ? 120f : 40f * blink) * (beep > 0f ? 1.5f : 1f);
            }

            if (!IsAwake || IsDefeated || settings == null || body == null) return;
            var player = FindFlyingPlayer();

            // The turret tracks you.
            if (turret != null && player != null)
            {
                var d = player.transform.position - turret.position; d.y = 0f;
                if (d.sqrMagnitude > 0.01f) turret.rotation = Quaternion.RotateTowards(turret.rotation, Quaternion.LookRotation(d, Vector3.up), 200f * dt);
            }

            // Touching its intake while the suction's on tears paper.
            if (player != null && suction != null && suction.Strength > 0.2f)
            {
                Vector3 p = player.transform.position - body.position;
                if (p.y < contactHeight && p.y > -2f && new Vector2(p.x, p.z).magnitude < contactRadius)
                {
                    var paper = player.GetComponent<PaperDamage>();
                    if (paper != null) paper.ApplyDamage(settings.contactDamagePerSecond * suction.Strength * dt);
                }
            }

            // Crumbs while it patrols: aimed volleys, and (phase 2+) low rings you have to hop.
            if (driving && player != null && !Waking)
            {
                if (Time.time >= nextVolley)
                {
                    nextVolley = Time.time + volleyInterval * (Phase >= 3 ? 0.7f : 1f);
                    FireVolley(player, settings.shotsPerVolley, settings.spreadAngle);
                }
                if (Phase >= 2 && Time.time >= nextRing)
                {
                    nextRing = Time.time + 3.2f;
                    FireRing(12, 6f, 0.5f, Time.time * 40f);
                }
            }
        }
    }
}
