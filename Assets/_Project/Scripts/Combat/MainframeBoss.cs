using System.Collections;
using UnityEngine;
using PaperWings.Air;
using PaperWings.Flight;

namespace PaperWings.Combat
{
    /// <summary>
    /// THE MAINFRAME (Stage 26, reworked in Stage 29): the server room's boss, built on DRAFTS and HEAT.
    ///
    /// A wall-sized server cabinet with two big exhaust fans. The fight changes as you take it apart:
    ///   FAN SWEEP: a beam of wind sweeps the aisle while it fires packet volleys. Each FAN is a part
    ///      you can shoot off: one fan left = a narrower, weaker sweep; none = no sweep at all,
    ///      so it switches to PACKET STORMS (spinning spirals of packets) instead.
    ///   OVERHEAT: after attacking it overheats. Its top hatch opens (the weak spot) and hot exhaust
    ///      rises from it: ride the heat up for a clean shot.
    ///   FIREWALL (phase 2+, every other round): it launches two drones and shields itself while
    ///      a packet spiral turns. Shoot the drones down and it overheats straight away.
    ///   COOLING PURGE (phase 2+): a cold downdraft dumps from the ceiling in front of it first.
    ///   Phase 3: reinforcements, a faster rhythm, double sweeps.
    /// </summary>
    public class MainframeBoss : AirBoss
    {
        [Header("Mainframe parts")]
        [Tooltip("The sweeping wind beam. Rotated around world-up to sweep across the room.")]
        [SerializeField] BeamAir sweepBeam;
        [Tooltip("Hot exhaust rising from the top hatch while it overheats (the way up to the weak spot).")]
        [SerializeField] ColumnAir exhaust;
        [Tooltip("Cold downdraft dumped in front of it (phase 2+).")]
        [SerializeField] ColumnAir purge;
        [Tooltip("The two front fans (spin around local Z). Same order as Fan Parts.")]
        [SerializeField] Transform[] fanRotors;
        [Tooltip("The fans as shootable parts (same order as Fan Rotors).")]
        [SerializeField] BossPart[] fanParts;
        [Tooltip("The heat-sink hatch lid (rotates open on local X).")]
        [SerializeField] Transform hatch;
        [Tooltip("Total angle the fan beam sweeps through (degrees).")]
        [SerializeField] float sweepAngle = 110f;

        protected override string BaseHint => "ARMORED: shoot off its fans, survive the sweep, climb its exhaust to the hatch";
        public override string WeakSpotText => "HATCH OPEN! ride the heat up and shoot it";

        float fanSpin, hatchOpen01, hatchTarget;
        Quaternion sweepRest;
        Vector2 sweepSize;
        int round;

        protected override void Awake()
        {
            if (sweepBeam != null) { sweepRest = sweepBeam.transform.localRotation; sweepSize = sweepBeam.size; }
            base.Awake();
        }

        protected override void AirOff()
        {
            if (sweepBeam != null) sweepBeam.targetStrength = 0f;
            if (exhaust != null) exhaust.targetStrength = 0f;
            if (purge != null) purge.targetStrength = 0f;
            hatchTarget = 0f;
        }

        protected override void OnResetBoss()
        {
            round = 0;
            if (sweepBeam != null) { sweepBeam.transform.localRotation = sweepRest; sweepBeam.size = sweepSize; }
        }

        int FansLeft
        {
            get
            {
                if (fanParts == null || fanParts.Length == 0) return 2;
                int n = 0;
                foreach (var f in fanParts) if (f != null && !f.Broken) n++;
                return n;
            }
        }

        protected override void OnPartDestroyed(BossPart part)
        {
            if (sweepBeam != null) sweepBeam.size = new Vector2(sweepSize.x * (0.4f + 0.3f * FansLeft), sweepSize.y);
        }

        protected override void WakeTick(float t) => fanSpin = Mathf.Lerp(fanSpin, 900f * Mathf.Repeat(t * 4f, 1f), 0.2f);

        protected override IEnumerator Round(PlaneController player)
        {
            round++;
            float pace = Phase >= 3 ? 0.8f : 1f;

            if (Phase >= 2 && drones != null && round % 2 == 0)
            {
                // FIREWALL: shielded while its drones fly; a packet spiral turns round it meanwhile.
                yield return Telegraph("FIREWALL UP!  shoot down its drones", settings.telegraph);
                var storm = StartCoroutine(FireSpiral(30f, 3f, 50f, Phase >= 3 ? 4 : 3, 4f));
                yield return DroneShield(30f);
                StopCoroutine(storm);
            }
            else
            {
                // Phase 2+: a cold purge first.
                if (Phase >= 2 && purge != null)
                {
                    yield return Telegraph("COOLING PURGE!  get out from under it!", settings.telegraph);
                    purge.verticalSpeed = -settings.secondaryAirSpeed;
                    purge.targetStrength = 1f;
                    Audio.AudioFx.PlayAt(Audio.Sfx.BossSlam, purge.transform.position, 0.8f, 1.4f);
                    yield return new WaitForSeconds(1.6f);
                    purge.targetStrength = 0f;
                }

                if (FansLeft > 0) yield return FanSweep(pace);
                else
                {
                    // No fans left: it vents packets instead.
                    yield return Telegraph("PACKET STORM!  weave through the spiral", settings.telegraph);
                    yield return FireSpiral(settings.attackDuration * pace, 4f, 70f, Phase >= 3 ? 5 : 4, 6f);
                }
            }

            // Overheat: hatch open, hot exhaust lifts you up to it.
            hatchTarget = 1f;
            if (exhaust != null)
            {
                exhaust.verticalSpeed = settings.secondaryAirSpeed;
                exhaust.heat = true;
                exhaust.targetStrength = 1f;
            }
            yield return WeakWindow(settings.vulnerableTime * (FansLeft == 0 ? 1.3f : 1f));
            hatchTarget = 0f;
            if (exhaust != null) exhaust.targetStrength = 0f;
        }

        IEnumerator FanSweep(float pace)
        {
            yield return Telegraph(FansLeft == 2 ? "FAN SWEEP!  brace against the wind" : "FAN SWEEP!  (one fan: a narrower blast)", settings.telegraph);
            if (sweepBeam != null)
            {
                sweepBeam.windSpeed = settings.airSpeed * (FansLeft == 2 ? 1f : 0.75f);
                sweepBeam.targetStrength = 1f;
            }
            float duration = settings.attackDuration * pace;
            float start = Time.time;
            bool firedMid = false;
            int sweeps = Phase >= 3 ? 2 : 1;
            while (Time.time < start + duration)
            {
                float t = (Time.time - start) / duration;
                float s = Mathf.Sin((t * sweeps - 0.25f) * Mathf.PI * 2f) * 0.5f;
                if (sweepBeam != null) sweepBeam.transform.localRotation = sweepRest * Quaternion.Euler(0f, s * sweepAngle, 0f);
                if (!firedMid && t > 0.5f) { firedMid = true; FireVolley(FindFlyingPlayer(), settings.shotsPerVolley, settings.spreadAngle); }
                yield return null;
            }
            if (sweepBeam != null) sweepBeam.targetStrength = 0f;
            FireVolley(FindFlyingPlayer(), settings.shotsPerVolley, settings.spreadAngle);
        }

        protected override void OnDefeated() => hatchTarget = 1f;   // slumps open

        protected override void Update()
        {
            base.Update();
            float target = warning || (sweepBeam != null && sweepBeam.Strength > 0.05f) ? 2200f : (IsAwake ? 300f : 60f);
            if (IsDefeated) target = 0f;
            if (!Waking) fanSpin = Mathf.Lerp(fanSpin, target, 1f - Mathf.Exp(-2f * Time.deltaTime));
            if (fanRotors != null)
                for (int i = 0; i < fanRotors.Length; i++)
                {
                    bool broken = fanParts != null && i < fanParts.Length && fanParts[i] != null && fanParts[i].Broken;
                    if (fanRotors[i] != null && !broken) fanRotors[i].Rotate(0f, 0f, fanSpin * Time.deltaTime, Space.Self);
                }

            hatchOpen01 = Mathf.MoveTowards(hatchOpen01, hatchTarget, Time.deltaTime * 2f);
            if (hatch != null) hatch.localRotation = Quaternion.Euler(-70f * hatchOpen01, 0f, 0f);
        }
    }
}
