using System.Collections;
using UnityEngine;
using PaperWings.Air;
using PaperWings.Flight;

namespace PaperWings.Combat
{
    /// <summary>
    /// THE CONDENSER (Stage 26): the rooftop's final boss, built on UPDRAFT and SUCTION.
    ///
    /// A giant rooftop AC unit with a horizontal fan on top. Its round:
    ///   1. BLOW: the fan throws a huge column of air upward and spits debris. The column lifts
    ///      anything above it, you included: great for height, bad for aiming.
    ///   2. STALL: the fan stops to reverse. The hub glows: the weak spot. Dive in and hit it.
    ///   3. SUCTION: the fan reverses and drags air (and you) down into the grille. Touching the
    ///      grille shreds paper. Pull away hard.
    ///   4. (phase 2+) SHOCKWAVE: a ring of air bursts outward from the unit before it blows.
    ///   5. (phase 3) reinforcements, shorter stalls.
    /// Stage 29 rework:
    ///   - While it blows it throws RINGS of debris (fly through the gaps) instead of plain volleys,
    ///     and while it sucks a debris SPIRAL turns over the grille.
    ///   - Four COOLANT TANKS on its corners are parts: each one shot off makes the stall last
    ///     longer and its rings thinner.
    ///   - Phase 2+, every other round: MAINTENANCE DRONES shield it until you shoot them down.
    /// </summary>
    public class CondenserBoss : AirBoss
    {
        [Header("Condenser parts")]
        [Tooltip("The column above the fan: lifts while blowing, sucks while reversing.")]
        [SerializeField] ColumnAir column;
        [Tooltip("Outward burst ring (phase 2+).")]
        [SerializeField] ColumnAir shockwave;
        [Tooltip("The fan blades (spin around local Y).")]
        [SerializeField] Transform fanRotor;
        [Tooltip("Top of the grille, for contact damage while sucking.")]
        [SerializeField] Transform grilleTop;
        [SerializeField] float grilleRadius = 18f;
        [Tooltip("Rising streaks while it blows (updraft colour).")]
        [SerializeField] ParticleSystem blowStreaks;
        [Tooltip("Falling streaks while it sucks (downdraft colour).")]
        [SerializeField] ParticleSystem suckStreaks;

        protected override string BaseHint => "ARMORED: ride its updraft, wait for the fan to stall";
        public override string WeakSpotText => "FAN STALLED! hit the hub";

        float fanSpin;     // degrees/sec, signed (+ blow, - suck)
        float fanTarget;
        bool sucking;
        int round;

        int TanksBroken => Parts.Length - PartsLeft;
        protected override void OnResetBoss() => round = 0;

        protected override void AirOff()
        {
            if (column != null) column.targetStrength = 0f;
            if (shockwave != null) shockwave.targetStrength = 0f;
            fanTarget = 0f;
            sucking = false;
        }

        protected override void WakeTick(float t) => fanTarget = Mathf.Repeat(t * 3f, 1f) < 0.5f ? 400f : -400f;

        protected override IEnumerator Round(PlaneController player)
        {
            round++;
            if (Phase >= 2 && drones != null && round % 2 == 0)
            {
                yield return Telegraph("MAINTENANCE DRONES!  shoot them down to break its shield", settings.telegraph);
                fanTarget = 600f;
                var storm = StartCoroutine(FireSpiral(30f, 2.5f, -45f, 3, 2f));
                yield return DroneShield(30f);
                StopCoroutine(storm);
            }

            if (Phase >= 2 && shockwave != null)
            {
                yield return Telegraph("SHOCKWAVE!  it's going to blast outward", settings.telegraph * 0.8f);
                shockwave.radialSpeed = settings.airSpeed * 1.2f;
                shockwave.SetStrengthNow(1f);
                shockwave.targetStrength = 0f;
                Audio.AudioFx.PlayAt(Audio.Sfx.BossSlam, transform.position, 1f, 0.8f, 0f);
            }

            // BLOW: updraft plus debris.
            yield return Telegraph("UPDRAFT!  it's spinning up", settings.telegraph * 0.7f);
            fanTarget = 1800f;
            if (column != null)
            {
                column.verticalSpeed = settings.airSpeed;
                column.radialSpeed = 2f;
                column.targetStrength = 1f;
            }
            float end = Time.time + settings.attackDuration;
            float nextVolley = Time.time + 0.8f;
            int ring = 0;
            while (Time.time < end)
            {
                if (Time.time >= nextVolley)
                {
                    // Debris rings (thinner as its tanks go), alternating tilt so the gaps move; an aimed volley every third.
                    int shots = Mathf.Max(8, 18 - 2 * TanksBroken);
                    if (ring % 3 == 2) FireVolley(FindFlyingPlayer(), settings.shotsPerVolley, settings.spreadAngle);
                    else FireRing(shots, ring % 2 == 0 ? 8f : 22f, 0.55f, ring * 11f);
                    ring++;
                    nextVolley = Time.time + (Phase >= 3 ? 0.9f : 1.3f);
                }
                yield return null;
            }

            // STALL: the weak window.
            if (column != null) column.targetStrength = 0f;
            fanTarget = 0f;
            yield return WeakWindow(settings.vulnerableTime * (Phase >= 3 ? 0.8f : 1f) * (1f + 0.2f * TanksBroken));

            // SUCTION.
            yield return Telegraph("SUCTION!  pull away from the grille!", settings.telegraph);
            fanTarget = -1600f;
            sucking = true;
            if (column != null)
            {
                column.verticalSpeed = -settings.secondaryAirSpeed;
                column.radialSpeed = -settings.secondaryAirSpeed * 0.6f;
                column.targetStrength = 1f;
            }
            yield return FireSpiral(settings.attackDuration * 0.8f, 2.5f, 80f, Phase >= 3 ? 3 : 2, 25f, 0.5f);
            if (column != null) column.targetStrength = 0f;
            sucking = false;
            fanTarget = 0f;
        }

        protected override void Update()
        {
            base.Update();
            fanSpin = Mathf.Lerp(fanSpin, IsDefeated ? 0f : fanTarget, 1f - Mathf.Exp(-1.8f * Time.deltaTime));
            if (fanRotor != null) fanRotor.Rotate(0f, fanSpin * Time.deltaTime, 0f, Space.Self);

            SetEmission(blowStreaks, fanSpin > 300f && column != null ? column.Strength : 0f, AirLook.Updraft);
            SetEmission(suckStreaks, fanSpin < -300f && column != null ? column.Strength : 0f, AirLook.Downdraft);

            // Touching the grille while it sucks tears paper.
            if (sucking && grilleTop != null && settings != null)
            {
                var player = FindFlyingPlayer();
                if (player != null)
                {
                    Vector3 d = player.transform.position - grilleTop.position;
                    if (d.y < 5f && d.y > -2f && new Vector2(d.x, d.z).magnitude < grilleRadius)
                    {
                        var paper = player.GetComponent<PaperDamage>();
                        if (paper != null) paper.ApplyDamage(settings.contactDamagePerSecond * Time.deltaTime);
                    }
                }
            }
        }

        void SetEmission(ParticleSystem ps, float strength01, AirLook look)
        {
            if (ps == null) return;
            var main = ps.main;
            main.startColor = AirPalette.Of(look, 0.35f);
            var emission = ps.emission;
            emission.rateOverTime = 60f * Mathf.Clamp01(strength01);
        }
    }
}
