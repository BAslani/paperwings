using System.Collections;
using UnityEngine;
using PaperWings.Air;
using PaperWings.Flight;

namespace PaperWings.Combat
{
    /// <summary>
    /// THE TYPHOON (Stage 47): Tiny War's second boss, Sam's secret weapon. A hairdryer gaffer-taped to
    /// a toy truck that rolls up and down the bathroom floor, its nozzle tracking you. Built on a
    /// MOVING BEAM of air that changes temperature:
    ///   - HOT BLAST: a hot jet aimed under you that throws you UP (and out of position).
    ///   - COLD BLAST: a cold jet straight at you that shoves you BACK across the room.
    ///   - Soap bubbles fly out with every blast (dodge or shoot them).
    ///   - TAPE STRAPS (parts): three loops of tape hold the dryer on. Every strap shot off makes it
    ///     wobble (worse aim) and weakens its blasts.
    ///   - GREEN GANG (phase 2+, every other round): crayon planes shield it until they're shot down.
    ///   - OVERHEAT: after its blasts it overheats, glowing and smoking, and stops: its SWITCH on the
    ///     handle glows. That's the weak spot.
    /// </summary>
    public class TyphoonBoss : AirBoss
    {
        [Header("The truck")]
        [Tooltip("The toy truck (and everything on it): rolls between the two lane ends.")]
        [SerializeField] Transform truck;
        [SerializeField] Transform laneA, laneB;
        [SerializeField] float truckSpeed = 14f;
        [SerializeField] Transform[] wheels;

        [Header("The hairdryer")]
        [Tooltip("Turns (yaw and pitch) to aim the nozzle.")]
        [SerializeField] Transform dryer;
        [SerializeField] float aimSpeed = 70f;
        [Tooltip("The jet of air (a beam along the dryer's forward).")]
        [SerializeField] BeamAir blast;
        [Tooltip("Glow inside the nozzle: orange when hot, blue when cold, red when overheated.")]
        [SerializeField] Renderer[] nozzleGlow;
        [SerializeField] Light nozzleLight;
        [SerializeField] ParticleSystem overheatSmoke;

        protected override string BaseHint => "ARMORED: ride out the blasts, shoot off its tape straps, hit the switch when it overheats";
        public override string WeakSpotText => "OVERHEATED! it's cut out: shoot the switch on its handle";

        enum Heat { Off, Hot, Cold, Overheated }
        Heat heat;
        int round, lane = 1;
        float wobble, glow;
        MaterialPropertyBlock block;
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        int StrapsBroken => Parts.Length - PartsLeft;
        float Power => 1f - 0.18f * StrapsBroken;

        protected override void Awake()
        {
            block = new MaterialPropertyBlock();
            base.Awake();
        }

        protected override void AirOff()
        {
            if (blast != null) blast.targetStrength = 0f;
            if (heat != Heat.Overheated) heat = Heat.Off;
        }

        protected override void OnResetBoss()
        {
            round = 0;
            heat = Heat.Off;
            if (overheatSmoke != null) overheatSmoke.Stop();
        }

        protected override void WakeTick(float t) => wobble = 3f * (1f - t);

        protected override IEnumerator Round(PlaneController player)
        {
            round++;
            if (Phase >= 2 && drones != null && round % 2 == 0)
            {
                yield return Telegraph("GREEN GANG!  shoot down Sam's crayon planes to break its shield", settings.telegraph);
                yield return DroneShield(28f);
            }

            // HOT: throws you up.
            yield return Telegraph("HOT BLAST!  it'll throw you upward", settings.telegraph * 0.8f);
            yield return Blow(Heat.Hot, settings.attackDuration);
            // COLD: shoves you back.
            yield return Telegraph("COLD BLAST!  it'll shove you back", settings.telegraph * 0.7f);
            yield return Blow(Heat.Cold, settings.attackDuration * 0.85f);
            if (Phase >= 3)
            {
                yield return Telegraph("HOT BLAST!  again!", settings.telegraph * 0.5f);
                yield return Blow(Heat.Hot, settings.attackDuration * 0.7f);
            }

            // OVERHEAT: the weak window.
            if (blast != null) blast.targetStrength = 0f;
            heat = Heat.Overheated;
            if (overheatSmoke != null) overheatSmoke.Play();
            Audio.AudioFx.PlayAt(Audio.Sfx.BossClank, transform.position, 1f, 0.6f);
            yield return WeakWindow(settings.vulnerableTime * (1f + 0.15f * StrapsBroken));
            if (overheatSmoke != null) overheatSmoke.Stop();
            heat = Heat.Off;
        }

        IEnumerator Blow(Heat kind, float seconds)
        {
            heat = kind;
            if (blast != null)
            {
                blast.heat = kind == Heat.Hot;
                blast.windSpeed = (kind == Heat.Hot ? settings.airSpeed : settings.secondaryAirSpeed) * Power;
                blast.targetStrength = 1f;
            }
            float end = Time.time + seconds;
            float nextBubbles = Time.time + 0.6f;
            while (Time.time < end && !IsDefeated)
            {
                if (Time.time >= nextBubbles)
                {
                    nextBubbles = Time.time + (Phase >= 3 ? 0.9f : 1.4f);
                    var p = FindFlyingPlayer();
                    if (p != null) FireVolley(p, settings.shotsPerVolley, settings.spreadAngle);
                }
                yield return null;
            }
            if (blast != null) blast.targetStrength = 0f;
            heat = Heat.Off;
        }

        protected override void OnDefeated()
        {
            heat = Heat.Overheated;
            if (overheatSmoke != null) overheatSmoke.Play();
        }

        protected override void Update()
        {
            base.Update();
            float dt = Time.deltaTime, t = Time.time;
            bool live = IsAwake && !IsDefeated;

            // The truck rolls up and down its lane (not while overheated: it's stalled).
            if (truck != null && laneA != null && laneB != null && live && heat != Heat.Overheated && !Waking)
            {
                var goal = lane == 0 ? laneA.position : laneB.position;
                truck.position = Vector3.MoveTowards(truck.position, goal, truckSpeed * dt);
                if (Vector3.Distance(truck.position, goal) < 0.5f) lane = 1 - lane;
                if (wheels != null) foreach (var w in wheels) if (w != null) w.Rotate(truckSpeed * dt * 40f * (lane == 0 ? -1f : 1f), 0f, 0f, Space.Self);
            }

            // The dryer tracks you, wobbling more as its tape comes off.
            var player = FindFlyingPlayer();
            if (dryer != null && live && player != null && heat != Heat.Overheated)
            {
                Vector3 aim = player.transform.position - dryer.position;
                if (heat == Heat.Hot) aim = aim + Vector3.down * 10f;   // the hot jet goes in under you and lifts
                aim = Quaternion.Euler(Mathf.Sin(t * 7f) * (2f + 5f * StrapsBroken + wobble), Mathf.Sin(t * 5.3f) * (2f + 5f * StrapsBroken + wobble), 0f) * aim;
                if (aim.sqrMagnitude > 0.01f)
                    dryer.rotation = Quaternion.RotateTowards(dryer.rotation, Quaternion.LookRotation(aim, Vector3.up), aimSpeed * dt);
            }
            else if (dryer != null && heat == Heat.Overheated)
                dryer.localRotation = Quaternion.Slerp(dryer.localRotation, Quaternion.Euler(18f, dryer.localEulerAngles.y, 0f), dt * 2f);   // droops
            wobble = Mathf.MoveTowards(wobble, 0f, dt);

            // Nozzle glow and light by temperature.
            Color want = heat switch
            {
                Heat.Hot => new Color(1f, 0.45f, 0.1f) * 3f,
                Heat.Cold => new Color(0.4f, 0.7f, 1f) * 2.5f,
                Heat.Overheated => new Color(1f, 0.12f, 0.05f) * (2f + Mathf.Sin(t * 12f)),
                _ => new Color(0.2f, 0.18f, 0.15f)
            };
            if (block != null && nozzleGlow != null)
            {
                block.SetColor(EmissionId, want);
                foreach (var r in nozzleGlow) if (r != null) r.SetPropertyBlock(block);
            }
            if (nozzleLight != null)
            {
                nozzleLight.color = heat == Heat.Cold ? new Color(0.5f, 0.75f, 1f) : new Color(1f, 0.45f, 0.15f);
                glow = Mathf.MoveTowards(glow, heat == Heat.Off || !live ? 0f : 1f, dt * 3f);
                nozzleLight.intensity = 120f * glow;
            }
        }
    }
}
