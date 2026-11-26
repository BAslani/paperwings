using System.Collections;
using UnityEngine;
using PaperWings.Flight;

namespace PaperWings.Combat
{
    /// <summary>
    /// THE KAIJU (Stage 49): Tiny War's last boss. Maya and Sam stopped fighting and built a monster
    /// together: a cardboard-box giant that stomps across the living-room floor. It is built on
    /// HEIGHT: its attacks come in low, so the safe place is above them.
    ///   - STOMP: each footfall sends a ring of shock-shots out along the floor. Stay high.
    ///   - ROCKET FISTS (phase 2+): both fists fire aimed volleys. Each SHOULDER BOLT (a part) shot off
    ///     drops that fist and its volley.
    ///   - QUAKE (phase 3): a low spiral of shots winds out from its feet.
    ///   - RED + GREEN (phase 2+, every other round): drones shield it until they're shot down.
    ///   - STAGGER: after its attacks it falls to a knee and the CHEST HATCH flaps open on a glowing
    ///     fuse: that's the weak spot.
    /// </summary>
    public class KaijuBoss : AirBoss
    {
        [Header("The giant")]
        [Tooltip("Everything that walks (body, legs, arms). Moves between the two lane ends.")]
        [SerializeField] Transform giant;
        [SerializeField] Transform laneA, laneB;
        [SerializeField] float walkSpeed = 8f;
        [SerializeField] Transform[] legs;
        [Tooltip("Fist muzzles, left then right. Each goes with the shoulder bolt of the same index.")]
        [SerializeField] Transform[] fists;
        [Tooltip("The chest hatch: flips open on its top edge when it staggers.")]
        [SerializeField] Transform hatch;
        [SerializeField] Renderer[] eyes;

        protected override string BaseHint => "ARMORED: stay high above the stomps, shoot off the shoulder bolts, hit the fuse when the hatch opens";
        public override string WeakSpotText => "STAGGERED! the chest hatch is open: shoot the FUSE";

        int round, lane = 1;
        bool walking, staggered, charging;
        float hatchAngle, kneel, walkPhase;
        MaterialPropertyBlock block;
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        protected override void Awake()
        {
            block = new MaterialPropertyBlock();
            base.Awake();
        }

        protected override void AirOff() { walking = false; charging = false; if (!IsDefeated) staggered = false; }

        protected override void OnResetBoss()
        {
            round = 0;
            walking = staggered = charging = false;
        }

        protected override void WakeTick(float t) => walkPhase += Time.deltaTime * 6f;

        bool FistLive(int i) => fists != null && i < fists.Length && fists[i] != null && (Parts == null || i >= Parts.Length || !Parts[i].Broken);

        protected override IEnumerator Round(PlaneController player)
        {
            round++;
            walking = true;
            if (Phase >= 2 && drones != null && round % 2 == 0)
            {
                yield return Telegraph("RED + GREEN!  shoot down the drones to break its shield", settings.telegraph);
                yield return DroneShield(28f);
            }

            // STOMP: three footfalls, each a ring along the floor.
            yield return Telegraph("STOMP!  fly HIGH, the shockwaves stay low", settings.telegraph);
            int stomps = Phase >= 3 ? 4 : 3;
            for (int i = 0; i < stomps && !IsDefeated; i++)
            {
                walking = false;
                charging = true;
                yield return new WaitForSeconds(0.7f);
                FireRing(18, 0f, 0.55f, i * 10f);
                Audio.AudioFx.PlayAt(Audio.Sfx.BossSlam, transform.position, 1f);
                charging = false;
                walking = true;
                yield return new WaitForSeconds(Mathf.Max(0.9f, 1.5f - 0.15f * Phase));
            }

            // ROCKET FISTS: aimed volleys from whichever arms it still has.
            if (Phase >= 2)
            {
                yield return Telegraph("ROCKET FISTS!  weave", settings.telegraph * 0.8f);
                for (int v = 0; v < 3 && !IsDefeated; v++)
                {
                    var p = FindFlyingPlayer();
                    for (int i = 0; i < 2; i++)
                        if (p != null && FistLive(i)) FireAimed(fists[i], p, settings.shotsPerVolley, settings.spreadAngle * 0.4f);
                    yield return new WaitForSeconds(0.9f);
                }
            }

            // QUAKE: a low spiral out of its feet.
            if (Phase >= 3)
            {
                yield return Telegraph("QUAKE!  stay above the spiral", settings.telegraph * 0.7f);
                yield return FireSpiral(3.2f, 7f, 80f, 2, 0f, 0.5f);
            }

            // STAGGER: on one knee, hatch open.
            walking = false;
            staggered = true;
            Audio.AudioFx.PlayAt(Audio.Sfx.BossClank, transform.position, 1f, 0.6f);
            yield return WeakWindow(settings.vulnerableTime * (1f + 0.15f * (Parts.Length - PartsLeft)));
            staggered = false;
        }

        protected override void OnDefeated()
        {
            walking = false;
            staggered = true;
        }

        protected override void Update()
        {
            base.Update();
            float dt = Time.deltaTime, t = Time.time;
            bool live = IsAwake && !IsDefeated;

            // Walk the lane (slower as it takes damage? no: steadily, so you can read it).
            if (giant != null && laneA != null && laneB != null && live && walking && !Waking)
            {
                var goal = lane == 0 ? laneA.position : laneB.position;
                giant.position = Vector3.MoveTowards(giant.position, goal, walkSpeed * dt);
                if (Vector3.Distance(giant.position, goal) < 0.8f) lane = 1 - lane;
                walkPhase += dt * walkSpeed * 0.45f;
            }
            if (legs != null)
                for (int i = 0; i < legs.Length; i++)
                    if (legs[i] != null)
                    {
                        float swing = walking ? Mathf.Sin(walkPhase + i * Mathf.PI) * 28f : 0f;
                        float lift = charging && i == 0 ? -40f : 0f;
                        legs[i].localRotation = Quaternion.Slerp(legs[i].localRotation, Quaternion.Euler(swing + lift, 0f, 0f), 1f - Mathf.Exp(-10f * dt));
                    }

            // Kneel when staggered; the hatch flips open.
            kneel = Mathf.MoveTowards(kneel, staggered ? 1f : 0f, dt * 1.6f);
            if (giant != null) giant.localScale = new Vector3(1f, 1f - 0.08f * kneel, 1f);
            hatchAngle = Mathf.MoveTowards(hatchAngle, staggered ? 100f : 0f, dt * 180f);
            if (hatch != null) hatch.localRotation = Quaternion.Euler(-hatchAngle, 0f, 0f);

            if (block != null && eyes != null)
            {
                Color c = staggered ? new Color(0.3f, 0.3f, 0.3f)
                        : warning ? new Color(1f, 0.15f, 0.1f) * (2f + Mathf.Sin(t * 20f))
                        : new Color(1f, 0.8f, 0.3f) * 1.6f;
                block.SetColor(EmissionId, c);
                foreach (var r in eyes) if (r != null) r.SetPropertyBlock(block);
            }
        }
    }
}
