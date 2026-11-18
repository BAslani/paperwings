using System;
using UnityEngine;
using PaperWings.Air;
using PaperWings.Gameplay;

namespace PaperWings.Flight
{
    public enum PlaneState
    {
        Held,    // in the player's hand, waiting to be thrown (kinematic, no physics)
        Flying,  // the flight model is in control
        Down,    // crashed or touched down somewhere that isn't a target; plain physics until reset
        Landed   // successfully landed on a target (e.g. the mug); resting in a snapped pose
    }

    /// <summary>
    /// Arcade glider flight model.
    ///
    /// HOW IT WORKS (read this first):
    ///  1. The plane's orientation is three plain angles: heading (yaw), pitch and bank.
    ///     Player input changes pitch and bank directly. Bank automatically turns the
    ///     heading, so steering only ever needs one stick.
    ///  2. "Speed" is the plane's energy. Pointing down adds speed, pointing up spends it.
    ///     That single rule is the whole "altitude is fuel" loop.
    ///  3. Each physics tick we build a desired velocity: along the nose at 'speed',
    ///     plus a small constant sink (gliding loses height), plus any wind.
    ///     The Rigidbody's velocity eases toward it (airGrip), which gives a slightly
    ///     floaty, papery slide instead of rails.
    ///  4. Forgiveness: no input = nose eases to a gentle glide and wings level out.
    ///     Too slow = soft stall: nose drops on its own, speed comes back, control returns.
    ///
    /// UNITY NOTE: The Rigidbody is still used. It moves the plane, handles collisions and
    /// interpolates rendering. We just don't let it simulate aerodynamics or gravity;
    /// we set its velocity and rotation ourselves every FixedUpdate (the physics tick,
    /// 50 times/sec by default, independent of frame rate).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlaneController : MonoBehaviour
    {
        [SerializeField] FlightSettings settings;
        [SerializeField] PlaneInput input;

        // ---- Public read-only state (used by HUD, camera, wobble, etc.) ----
        public FlightSettings Settings => settings;
        public PlaneState State { get; private set; } = PlaneState.Held;
        public float Speed => speed;
        public Vector3 Velocity => rb.linearVelocity;
        public float PitchAngle => pitch;
        public float BankAngle => bank;
        public bool IsStalling { get; private set; }
        /// <summary>0..1, smoothed. Use for visuals/warnings.</summary>
        public float StallAmount => stallBlend;
        public float BoostCooldownRemaining => Mathf.Max(0f, boostReadyTime - Time.time);
        public bool LastDownWasCrash { get; private set; }
        /// <summary>Human-readable reason for the last crash/touchdown/landing (for the HUD).</summary>
        public string LastResultMessage { get; private set; }
        /// <summary>Seconds in the air during the current (or last) flight.</summary>
        public float FlightTime => State == PlaneState.Flying ? Time.time - launchTime : lastFlightTime;
        /// <summary>0..1, how much a landing target is currently steering us.</summary>
        public float LandingAssistStrength { get; private set; }

        /// <summary>Fired when the plane stops flying. Argument: true = crash, false = gentle touchdown.</summary>
        public event Action<bool> Downed;
        /// <summary>Fired when a boost fires (for effects).</summary>
        public event Action Boosted;
        /// <summary>Fired on a glancing bump that didn't end the flight. Argument: impact speed into the surface.</summary>
        public event Action<float> Bumped;

        /// <summary>
        /// Runtime multipliers on top of FlightSettings (paper damage, sogginess...).
        /// Other systems write these; the flight model reads them every tick.
        /// </summary>
        public FlightModifiers Modifiers { get; } = new FlightModifiers();

        /// <summary>
        /// Smoothed velocity of the air around the plane, sampled from the AirField.
        /// It is simply added to the plane's motion, so wind pushes you around and
        /// rising air lifts you.
        /// </summary>
        public Vector3 AirVelocity { get; private set; }

        /// <summary>Raw air sample from the last physics tick (for HUD/effects: in thermal? in draft?).</summary>
        public AirSample Air { get; private set; }

        Rigidbody rb;

        // The three angles that define orientation (degrees).
        float yaw;    // heading, around world up
        float pitch;  // nose up (+) / down (-)
        float bank;   // right wing down (+) / left wing down (-)

        float speed;          // airspeed along the nose
        float stallBlend;     // 0 = flying normally, 1 = fully stalled (smoothed)
        float boostReadyTime; // Time.time when the next boost is allowed

        float launchTime, lastFlightTime;

        // Landing snap (a short slide into the rest pose on a target).
        Vector3 snapFromPos, snapToPos;
        Quaternion snapFromRot, snapToRot;
        float snapProgress, snapDuration;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (input == null) input = GetComponent<PlaneInput>();
        }

        // ------------------------------------------------------------------
        // Public control API (used by LaunchAndReset and, later, the landing zone)
        // ------------------------------------------------------------------

        /// <summary>Puts the plane in the player's hand at a pose. Physics is off while held.</summary>
        public void Hold(Vector3 position, Quaternion rotation)
        {
            if (State != PlaneState.Held)
            {
                State = PlaneState.Held;
                rb.isKinematic = true;   // kinematic = moved by code only, ignores forces
                rb.useGravity = false;
            }
            // Teleport (both rb and transform, so interpolation doesn't smear the move).
            rb.position = position;
            rb.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
        }

        /// <summary>Throws the plane from its current held pose.</summary>
        public void Launch(float throwSpeed, float pitchKick)
        {
            Vector3 euler = transform.rotation.eulerAngles;
            yaw = euler.y;
            pitch = Mathf.Clamp(-NormalizeAngle(euler.x) + pitchKick, -settings.maxPitchDown, settings.maxPitchUp);
            bank = 0f;

            speed = throwSpeed;
            stallBlend = 0f;
            IsStalling = false;
            boostReadyTime = Time.time;
            launchTime = Time.time;
            AirVelocity = Vector3.zero;
            Air = default;
            LandingAssistStrength = 0f;
            LastResultMessage = null;

            rb.isKinematic = false;
            rb.useGravity = false;      // our flight model handles "gravity" via sink + dive gain
            rb.freezeRotation = true;   // collisions must not spin us; we own rotation
            rb.linearDamping = 0f;
            rb.angularDamping = 0.05f;

            Quaternion rot = CurrentRotation();
            rb.rotation = rot;
            rb.linearVelocity = rot * Vector3.forward * speed;
            rb.angularVelocity = Vector3.zero;

            State = PlaneState.Flying;
        }

        // ------------------------------------------------------------------
        // The flight model
        // ------------------------------------------------------------------

        void FixedUpdate()
        {
            // Always consume a queued boost press, so a press made while held
            // doesn't fire the instant we're thrown.
            bool boostRequested = input != null && input.ConsumeBoost();

            if (State != PlaneState.Flying) return;

            float dt = Time.fixedDeltaTime;
            Vector2 stick = input != null ? input.Steer : Vector2.zero;

            // Stick forward (+y) = nose DOWN by default, like a real flight stick.
            float pitchInput = settings.invertPitch ? stick.y : -stick.y;
            float rollInput = stick.x;

            // Slow planes respond sluggishly. 0..1 between stall speed and cruise speed.
            float speed01 = Mathf.InverseLerp(settings.stallSpeed, settings.cruiseSpeed, speed);
            float authority = Mathf.Lerp(settings.lowSpeedAuthority, 1f, speed01) * Modifiers.controlScale;

            UpdateAir(dt);
            LandingAssist assist = LandingZone.GetAssist(rb.position, yaw);
            LandingAssistStrength = assist.strength;

            UpdateStall(dt);
            UpdateBank(rollInput, dt);
            UpdatePitch(pitchInput, authority, assist, dt);

            // Roll leads to yaw: the more you bank, the faster you turn.
            yaw += settings.turnRate * (bank / settings.maxBank) * authority * dt;
            // Landing assist: gently swing the heading toward the target.
            yaw += assist.yawError * settings.landingYawAssist * assist.strength * dt;

            Quaternion rotation = CurrentRotation();
            rb.MoveRotation(rotation);

            UpdateSpeed(rotation * Vector3.forward, dt);
            if (boostRequested) TryBoost();

            // Final-approach air brake: near a landing target, excess speed bleeds off faster.
            if (assist.strength > 0f && speed > settings.cruiseSpeed)
                speed -= (speed - settings.cruiseSpeed) * settings.landingSpeedBrake * assist.strength * dt;

            // ---- Build the velocity we want this tick ----
            Vector3 forward = rotation * Vector3.forward;
            Vector3 desiredVelocity = forward * speed
                                    + Vector3.down * CurrentSinkRate(speed01)
                                    + AirVelocity;

            // Ease toward it. (1 - e^(-k*dt)) is a frame-rate-independent smoothing factor.
            float grip = 1f - Mathf.Exp(-settings.airGrip * dt);
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, desiredVelocity, grip);
        }

        void UpdateAir(float dt)
        {
            // Ask the air field what the air is doing here, then ease into it so
            // entering a zone feels like a swell, not a wall.
            Air = AirField.Sample(rb.position);
            Vector3 targetAir = Air.wind * settings.airInfluence;
            AirVelocity = Vector3.Lerp(AirVelocity, targetAir, 1f - Mathf.Exp(-settings.airResponse * dt));
        }

        void UpdateStall(float dt)
        {
            // Hysteresis: enter below stallSpeed, exit above stallRecoverSpeed.
            if (!IsStalling && speed < settings.stallSpeed) IsStalling = true;
            else if (IsStalling && speed > settings.stallRecoverSpeed) IsStalling = false;

            // Smooth 0..1 so the nose drop and warning fade in/out over ~0.3s.
            stallBlend = Mathf.MoveTowards(stallBlend, IsStalling ? 1f : 0f, dt / 0.3f);
        }

        void UpdateBank(float rollInput, float dt)
        {
            // Bank goes to a TARGET angle set by the stick, not a free roll rate.
            // No barrel rolls, no inverted flight. Release the stick and wings level.
            // rollBias = a bent wing: with no input the plane settles into a slight bank and drifts.
            float targetBank = Mathf.Clamp(rollInput * settings.maxBank + Modifiers.rollBias, -settings.maxBank, settings.maxBank);
            float response = Mathf.Abs(rollInput) > 0.01f ? settings.bankResponse : settings.rollAutoLevel;
            bank = Mathf.Lerp(bank, targetBank, 1f - Mathf.Exp(-response * dt));
        }

        void UpdatePitch(float pitchInput, float authority, LandingAssist assist, float dt)
        {
            if (Mathf.Abs(pitchInput) > 0.05f)
            {
                // During a stall, pulling up is ignored (you must let the nose drop).
                if (pitchInput > 0f) pitchInput *= 1f - stallBlend;
                pitch += pitchInput * settings.pitchRate * authority * dt;
            }
            else
            {
                // Hands off: ease the nose to a steady glide angle...
                // ...or, near a landing target, toward the angle that points at it.
                float target = Mathf.Lerp(settings.glidePitch, assist.approachPitch,
                                          assist.strength * settings.landingPitchAssist);
                float strength = Mathf.Max(settings.autoLevelStrength, 2f * assist.strength * settings.landingPitchAssist);
                pitch = Mathf.Lerp(pitch, target, 1f - Mathf.Exp(-strength * dt));
            }

            // Soft stall: pull the nose down (but never pull it UP if the player is already diving harder).
            if (stallBlend > 0f && pitch > settings.stallNosePitch)
            {
                float t = stallBlend * (1f - Mathf.Exp(-settings.stallRecoveryStrength * dt));
                pitch = Mathf.Lerp(pitch, settings.stallNosePitch, t);
            }

            pitch = Mathf.Clamp(pitch, -settings.maxPitchDown, settings.maxPitchUp);
        }

        void UpdateSpeed(Vector3 forward, float dt)
        {
            // forward.y is the sine of the pitch angle: -1 straight down, +1 straight up.
            float slope = forward.y;

            if (slope < 0f) speed += settings.diveSpeedGain * -slope * dt;   // diving: height -> speed
            else            speed -= settings.climbSpeedCost * slope * dt;   // climbing: speed -> height

            if (speed > settings.cruiseSpeed)
            {
                // Extra speed slowly bleeds off, so dives are a resource, not permanent.
                speed -= (speed - settings.cruiseSpeed) * settings.overspeedDrag * dt;
            }
            else if (slope <= 0f)
            {
                // Level or nose-down and slow: gently regain cruise speed (the sink pays for it).
                speed += (settings.cruiseSpeed - speed) * settings.glideSpeedRecovery * dt;
            }

            // Riding WITH the air gives energy back (headwinds don't take it away; forgiving).
            float tailwind = Vector3.Dot(AirVelocity, forward);
            if (tailwind > 0f) speed += tailwind * settings.tailwindSpeedGain * dt;

            speed = Mathf.Clamp(speed, 2f, settings.maxSpeed);
        }

        float CurrentSinkRate(float speed01)
        {
            float sink = settings.cruiseSpeed / (settings.glideRatio * Modifiers.glideScale); // base glide
            sink += Modifiers.extraSink;                                       // e.g. soggy paper
            sink += settings.slowSinkExtra * (1f - speed01);                   // mushy when slow
            sink += settings.turnSinkExtra * Mathf.Abs(bank) / settings.maxBank; // turns cost height
            sink += settings.stallExtraSink * stallBlend;                      // stalls drop you
            return sink;
        }

        /// <summary>Adds airspeed (e.g. a reward for flying through a ring). Only while flying.</summary>
        public void AddSpeed(float amount)
        {
            if (State != PlaneState.Flying) return;
            speed = Mathf.Clamp(speed + amount, 2f, settings.maxSpeed);
        }

        void TryBoost()
        {
            if (Time.time < boostReadyTime) return;
            speed = Mathf.Min(speed + settings.boostSpeed, settings.maxSpeed);
            boostReadyTime = Time.time + settings.boostCooldown;
            Boosted?.Invoke();
        }

        // ------------------------------------------------------------------
        // Collisions
        // ------------------------------------------------------------------

        void OnCollisionEnter(Collision collision)
        {
            if (State != PlaneState.Flying) return;

            // Landing targets get first say (they judge touchdowns on their top surface).
            var pad = collision.collider.GetComponentInParent<LandingZone>();
            if (pad != null && pad.HandleTouchdown(this, collision)) return;

            // Contact normal points away from the surface we hit, toward us.
            Vector3 normal = collision.GetContact(0).normal;
            float impactSpeed = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, normal));
            bool floorLike = normal.y > 0.6f;   // roughly upward-facing surface

            if (floorLike)
            {
                bool tooHard = impactSpeed > settings.touchdownMaxImpact || speed > settings.touchdownMaxSpeed;
                GoDown(crash: tooHard, reason: tooHard ? "Crunch!" : "Touchdown... but not on the mug.");
            }
            else if (impactSpeed < settings.bumpTolerance)
            {
                // Glancing hit on a wall: just scrub some speed and keep flying.
                speed *= settings.bumpSpeedKeep;
                Bumped?.Invoke(impactSpeed);   // paper damage, camera kick, etc.
            }
            else
            {
                GoDown(crash: true, reason: "Crunch!");
            }
        }

        /// <summary>Stops the flight model and hands the plane to plain physics.</summary>
        public void GoDown(bool crash, string reason = null)
        {
            if (State == PlaneState.Down || State == PlaneState.Landed) return;
            if (State == PlaneState.Flying) lastFlightTime = Time.time - launchTime;
            State = PlaneState.Down;
            LastDownWasCrash = crash;
            LastResultMessage = reason ?? (crash ? "Crunch!" : "Touchdown!");

            rb.useGravity = true;
            rb.freezeRotation = false;
            rb.linearDamping = 0.5f;
            rb.angularDamping = 0.5f;
            if (crash)
            {
                // A little tumble so crashes read clearly.
                rb.AddTorque(UnityEngine.Random.insideUnitSphere * 2f, ForceMode.VelocityChange);
            }

            Downed?.Invoke(crash);
        }

        /// <summary>
        /// Successful landing on a target: freeze physics and slide into a neat rest pose
        /// over 'snapTime' seconds (the "light snapping" that makes landings look clean).
        /// </summary>
        public void Land(Vector3 restPosition, Quaternion restRotation, float snapTime, string message)
        {
            if (State != PlaneState.Flying) return;
            lastFlightTime = Time.time - launchTime;
            State = PlaneState.Landed;
            LastDownWasCrash = false;
            LastResultMessage = message;

            rb.isKinematic = true;
            snapFromPos = transform.position;
            snapFromRot = transform.rotation;
            snapToPos = restPosition;
            snapToRot = restRotation;
            snapProgress = 0f;
            snapDuration = Mathf.Max(0.01f, snapTime);
        }

        void Update()
        {
            if (State != PlaneState.Landed || snapProgress >= 1f) return;
            snapProgress = Mathf.Min(1f, snapProgress + Time.deltaTime / snapDuration);
            float t = 1f - (1f - snapProgress) * (1f - snapProgress);   // ease-out
            Vector3 p = Vector3.Lerp(snapFromPos, snapToPos, t);
            Quaternion r = Quaternion.Slerp(snapFromRot, snapToRot, t);
            rb.position = p;
            rb.rotation = r;
            transform.SetPositionAndRotation(p, r);
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        Quaternion CurrentRotation()
        {
            // Unity's Euler: +X tilts the nose DOWN, so pitch is negated.
            // +Z rolls left-wing-down, so bank is negated too.
            return Quaternion.Euler(-pitch, yaw, -bank);
        }

        static float NormalizeAngle(float a)
        {
            a %= 360f;
            if (a > 180f) a -= 360f;
            if (a < -180f) a += 360f;
            return a;
        }

        void OnDrawGizmos()
        {
            // Scene view: yellow line = actual velocity (1 second ahead).
            if (rb == null) return;
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, transform.position + rb.linearVelocity);
        }
    }
}
