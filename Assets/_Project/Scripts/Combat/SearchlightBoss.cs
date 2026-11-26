using System.Collections;
using UnityEngine;
using PaperWings.Flight;

namespace PaperWings.Combat
{
    /// <summary>
    /// THE NIGHT WATCH (Stage 48): Tiny War's third boss. Sam's gooseneck desk lamp, bolted to the top
    /// bunk, with a torch-beam for a weapon. The room is dark; the lamp is the only big light. It is
    /// built on LIGHT AND SHADOW, not on air:
    ///   - SWEEP: the beam scans the room. While you're lit (and nothing is between you and the lamp)
    ///     it SPOTS you and fires aimed volleys. Fly behind the pillow fort, the wardrobe or the bed
    ///     frame and it loses you.
    ///   - LOCK-ON: the beam snaps onto you and tracks. If it still has you when the lock completes it
    ///     fires a ring of marbles plus an aimed burst. Break line of sight.
    ///   - GREEN GANG (phase 2+, every other round): crayon planes shield the lamp.
    ///   - OVERHEAT: the beam dies and the shade flips up: the BULB glows. That's the weak spot.
    ///   - HINGE BOLTS (parts): three bolts hold the lamp head. Each one shot off slows the sweep and
    ///     the tracking, and lengthens the overheat window.
    /// </summary>
    public class SearchlightBoss : AirBoss
    {
        [Header("The lamp")]
        [Tooltip("The lamp head: turns to aim the beam (its forward is the beam).")]
        [SerializeField] Transform head;
        [Tooltip("The spot light on the head.")]
        [SerializeField] Light beam;
        [Tooltip("Where the beam sweeps around (the middle of the room).")]
        [SerializeField] Transform sweepCenter;
        [SerializeField] float sweepArc = 85f;
        [SerializeField] float sweepSpeed = 0.7f;
        [SerializeField] float beamLength = 230f;
        [SerializeField] float beamHalfAngle = 12f;
        [SerializeField] float trackSpeed = 55f;
        [Tooltip("Spot light intensity while the beam is on.")]
        [SerializeField] float beamIntensity = 14000f;
        [Tooltip("Flips up when the lamp overheats (shows the bulb).")]
        [SerializeField] Transform shade;
        [SerializeField] Renderer[] bulbGlow;

        protected override string BaseHint => "ARMORED: stay out of its beam (hide behind the pillows), shoot off its hinge bolts, hit the bulb when it overheats";
        public override string WeakSpotText => "OVERHEATED! the shade flipped up: shoot the BULB";

        enum Mode { Off, Sweep, Lock, Overheated }
        Mode mode;
        int round;
        float shadeAngle, beamLevel;
        MaterialPropertyBlock block;
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        int BoltsBroken => Parts.Length - PartsLeft;
        float Slow => 1f - 0.17f * BoltsBroken;

        protected override void Awake()
        {
            block = new MaterialPropertyBlock();
            base.Awake();
        }

        protected override void AirOff()
        {
            if (mode != Mode.Overheated) mode = Mode.Off;
        }

        protected override void OnResetBoss()
        {
            round = 0;
            mode = Mode.Off;
            warning = false;
        }

        protected override IEnumerator Round(PlaneController player)
        {
            round++;
            if (Phase >= 2 && drones != null && round % 2 == 0)
            {
                yield return Telegraph("GREEN GANG!  shoot down Sam's crayon planes to break its shield", settings.telegraph);
                yield return DroneShield(28f);
            }

            // SWEEP: the beam scans; being lit gets you shot at.
            yield return Telegraph("SEARCHLIGHT!  stay out of the beam: hide behind the pillows", settings.telegraph);
            yield return Sweep(settings.attackDuration);

            // LOCK-ON: it picks you out and tracks.
            yield return Telegraph("LOCK-ON!  break line of sight!", settings.telegraph * 0.7f);
            yield return Lock(2.4f);
            if (Phase >= 3)
            {
                yield return Telegraph("SEARCHLIGHT!  again!", settings.telegraph * 0.5f);
                yield return Sweep(settings.attackDuration * 0.7f);
            }

            // OVERHEAT: the weak window.
            mode = Mode.Overheated;
            warning = false;
            Audio.AudioFx.PlayAt(Audio.Sfx.BossClank, transform.position, 1f, 0.6f);
            yield return WeakWindow(settings.vulnerableTime * (1f + 0.15f * BoltsBroken));
            mode = Mode.Off;
        }

        IEnumerator Sweep(float seconds)
        {
            mode = Mode.Sweep;
            float end = Time.time + seconds, nextShot = Time.time + 0.7f;
            while (Time.time < end && !IsDefeated)
            {
                var p = FindFlyingPlayer();
                bool lit = p != null && Lit(p);
                warning = lit;
                warningText = "SPOTTED!  get behind cover";
                if (lit && Time.time >= nextShot)
                {
                    nextShot = Time.time + (Phase >= 3 ? 0.7f : 1.0f);
                    FireVolley(p, Mathf.Max(2, settings.shotsPerVolley - 1), settings.spreadAngle);
                }
                yield return null;
            }
            warning = false;
        }

        IEnumerator Lock(float seconds)
        {
            mode = Mode.Lock;
            float end = Time.time + seconds;
            while (Time.time < end && !IsDefeated)
            {
                var p = FindFlyingPlayer();
                warning = p != null && Lit(p);
                warningText = "LOCKED ON!";
                yield return null;
            }
            warning = false;
            var player = FindFlyingPlayer();
            if (player != null && !IsDefeated && Lit(player))
            {
                FireRing(14, 0f, 0.6f, 0f);
                FireAimed(muzzle != null ? muzzle : head, player, settings.shotsPerVolley, settings.spreadAngle * 0.5f);
            }
            mode = Mode.Off;
        }

        /// <summary>True if 'p' is inside the beam cone with nothing solid between it and the lamp.</summary>
        bool Lit(PlaneController p)
        {
            if (head == null || mode == Mode.Off || mode == Mode.Overheated) return false;
            Vector3 from = head.position, to = p.transform.position - from;
            float dist = to.magnitude;
            if (dist > beamLength || Vector3.Angle(head.forward, to) > beamHalfAngle) return false;
            foreach (var h in Physics.RaycastAll(from, to / dist, dist, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.collider.transform.IsChildOf(transform)) continue;
                if (h.collider.GetComponentInParent<PlaneController>() != null) continue;
                if (h.collider.GetComponentInParent<PathFlyer>() != null) continue;
                return false;   // a pillow, a wardrobe, the bed frame: you're in the shadow
            }
            return true;
        }

        protected override void OnDefeated()
        {
            mode = Mode.Overheated;
        }

        protected override void Update()
        {
            base.Update();
            float dt = Time.deltaTime, t = Time.time;
            bool live = IsAwake && !IsDefeated;
            var player = FindFlyingPlayer();

            // Aim the head.
            if (head != null && live && !Waking)
            {
                Vector3 want = head.position + head.forward * 50f;
                if (mode == Mode.Sweep && sweepCenter != null)
                {
                    Vector3 side = Vector3.Cross(Vector3.up, (sweepCenter.position - head.position).normalized);
                    float arc = Mathf.Sin(t * sweepSpeed * Slow) * sweepArc;
                    want = sweepCenter.position + side * arc + Vector3.up * (Mathf.Sin(t * 0.9f) * 18f - 20f);
                }
                else if (mode == Mode.Lock && player != null)
                    want = player.transform.position + player.Velocity * 0.25f;
                else if (mode == Mode.Overheated && sweepCenter != null)
                    want = head.position + Vector3.up * 40f + (sweepCenter.position - head.position).normalized * 20f;
                Vector3 dir = want - head.position;
                if (dir.sqrMagnitude > 0.01f)
                {
                    float speed = (mode == Mode.Lock ? trackSpeed : trackSpeed * 0.7f) * Slow;
                    head.rotation = Quaternion.RotateTowards(head.rotation, Quaternion.LookRotation(dir, Vector3.up), speed * dt);
                }
            }

            // Beam light: on while sweeping or locking; orange while locked.
            if (beam != null)
            {
                bool on = live && (mode == Mode.Sweep || mode == Mode.Lock);
                beamLevel = Mathf.MoveTowards(beamLevel, on ? 1f : 0f, dt * 4f);
                beam.intensity = beamIntensity * beamLevel;
                beam.color = mode == Mode.Lock ? new Color(1f, 0.55f, 0.35f) : new Color(1f, 0.95f, 0.8f);
            }

            // The shade flips up to show the bulb when it overheats.
            shadeAngle = Mathf.MoveTowards(shadeAngle, mode == Mode.Overheated ? 70f : 0f, dt * 120f);
            if (shade != null) shade.localRotation = Quaternion.Euler(-shadeAngle, 0f, 0f);

            if (block != null && bulbGlow != null)
            {
                Color c = mode == Mode.Overheated ? new Color(1f, 0.15f, 0.05f) * (2.4f + Mathf.Sin(t * 12f))
                        : beamLevel > 0.05f ? new Color(1f, 0.95f, 0.75f) * (2.5f * beamLevel)
                        : new Color(0.1f, 0.09f, 0.06f);
                block.SetColor(EmissionId, c);
                foreach (var r in bulbGlow) if (r != null) r.SetPropertyBlock(block);
            }
        }
    }
}
