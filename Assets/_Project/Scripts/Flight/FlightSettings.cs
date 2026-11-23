using UnityEngine;

namespace PaperWings.Flight
{
    /// <summary>
    /// All the "feel" knobs for one plane type, stored as an asset.
    ///
    /// UNITY NOTE: A ScriptableObject is a data asset that lives in the Project window
    /// (not in a scene). Edits you make to it during Play mode are KEPT when you stop,
    /// unlike edits to scene components. That makes it ideal for live tuning.
    /// Create more via: Project window > right-click > Create > PaperWings > Flight Settings.
    ///
    /// World scale: the office is built at about 1 unit = 2.3 cm (the desk is 70 units = 160 cm), so the
    /// dart (~2 units) is a small ~5 cm plane in a big room. That is deliberate: a full-bank turn
    /// (~21 units across) fits over the desk. Keep new props at this scale (see Stage 23).
    /// Speeds are in units/second. Angles in degrees.
    /// </summary>
    [CreateAssetMenu(fileName = "FlightSettings", menuName = "PaperWings/Flight Settings")]
    public class FlightSettings : ScriptableObject
    {
        // NOTE: Invert Pitch moved to the global Controls asset (ControlSettings) in Stage 7,
        // so it applies to every fold. PaperWings > Settings > Controls.

        [Header("Speed & Energy  (altitude is fuel)")]
        [Tooltip("The speed the plane naturally settles at in a gentle glide.")]
        public float cruiseSpeed = 14f;

        [Tooltip("Hard top speed, reached in a long steep dive.")]
        public float maxSpeed = 45f;

        [Tooltip("How much speed you gain per second when pointing straight down. " +
                 "Scaled by how steep the dive is.")]
        public float diveSpeedGain = 18f;

        [Tooltip("How much speed you lose per second when pointing straight up. " +
                 "Keep this >= Dive Speed Gain, or dive/climb porpoising creates free height.")]
        public float climbSpeedCost = 20f;

        [Tooltip("How quickly extra speed above cruise bleeds away (fraction per second). " +
                 "Lower = dives carry you further.")]
        [Range(0f, 2f)] public float overspeedDrag = 0.25f;

        [Tooltip("When level or nose-down and slower than cruise, how fast speed returns to cruise " +
                 "(fraction per second). The sink rate 'pays' for this.")]
        [Range(0f, 2f)] public float glideSpeedRecovery = 0.3f;

        [Header("Glide  (how fast you sink)")]
        [Tooltip("Glide efficiency: distance travelled forward per unit of height lost, at cruise speed. " +
                 "Higher = floatier. 9 means ~1.5 units/sec of sink at cruise.")]
        [Min(1f)] public float glideRatio = 9f;

        [Tooltip("Extra sink (units/sec) when you're slow, near stall speed. Makes slow flight feel mushy.")]
        public float slowSinkExtra = 2.5f;

        [Tooltip("Extra sink (units/sec) at full bank. Turning costs a little height.")]
        public float turnSinkExtra = 1.2f;

        [Tooltip("How tightly the plane's motion follows its nose (per second). " +
                 "Lower = slidier, floatier paper feel. Higher = on rails.")]
        [Range(1f, 20f)] public float airGrip = 5f;

        [Header("Steering")]
        [Tooltip("How fast the nose pitches up/down at full stick (deg/sec).")]
        public float pitchRate = 70f;

        [Tooltip("Steepest climb angle allowed.")]
        [Range(10f, 85f)] public float maxPitchUp = 60f;

        [Tooltip("Steepest dive angle allowed.")]
        [Range(10f, 85f)] public float maxPitchDown = 75f;

        [Tooltip("Bank angle at full roll input. Banking is what makes you turn.")]
        [Range(10f, 80f)] public float maxBank = 55f;

        [Tooltip("How quickly the plane rolls into the bank you ask for (per second).")]
        public float bankResponse = 5f;

        [Tooltip("Turn rate at full bank and good speed (deg/sec). Roll leads to yaw automatically.")]
        public float turnRate = 75f;

        [Tooltip("Control authority when at stall speed (1 = full). Slow planes turn and climb sluggishly.")]
        [Range(0.1f, 1f)] public float lowSpeedAuthority = 0.4f;

        [Header("Auto-Level  (forgiveness when hands are off)")]
        [Tooltip("With no pitch input, the nose drifts to this angle. Slightly negative = steady glide.")]
        [Range(-20f, 10f)] public float glidePitch = -4f;

        [Tooltip("How strongly the nose returns to Glide Pitch with no input (per second). 0 = off.")]
        [Range(0f, 5f)] public float autoLevelStrength = 1.2f;

        [Tooltip("How quickly the wings return to level with no roll input (per second).")]
        [Range(0f, 10f)] public float rollAutoLevel = 3f;

        [Header("Soft Stall")]
        [Tooltip("Below this speed the plane stalls: the nose drops on its own until speed returns.")]
        public float stallSpeed = 7f;

        [Tooltip("Stall ends once speed climbs back above this (a little higher than Stall Speed, so it doesn't flicker).")]
        public float stallRecoverSpeed = 10f;

        [Tooltip("Where the nose gets pulled during a stall.")]
        [Range(-80f, 0f)] public float stallNosePitch = -40f;

        [Tooltip("How hard the nose is pulled down during a stall (per second).")]
        [Range(0.5f, 10f)] public float stallRecoveryStrength = 2.5f;

        [Tooltip("Extra sink (units/sec) while fully stalled.")]
        public float stallExtraSink = 3f;

        [Header("Rubber-Band Boost")]
        [Tooltip("Instant speed added by a boost.")]
        public float boostSpeed = 12f;

        [Tooltip("Seconds between boosts.")]
        public float boostCooldown = 3f;

        [Tooltip("Upward push (units/sec) a boost adds when this fold has the pusher propeller (none without it), fading over Boost Lift Duration.")]
        public float boostLift = 0f;

        [Tooltip("Seconds the boost lift takes to fade out.")]
        [Range(0.1f, 3f)] public float boostLiftDuration = 1f;

        [Header("Hand Launch")]
        [Tooltip("Throw speed with a quick tap.")]
        public float throwSpeedMin = 12f;

        [Tooltip("Throw speed with a fully charged wind-up.")]
        public float throwSpeedMax = 20f;

        [Tooltip("Seconds of holding to reach a full charge.")]
        public float throwChargeTime = 0.6f;

        [Tooltip("Nose-up angle added at release, for a nice arcing throw.")]
        public float throwPitchKick = 8f;

        [Header("Air  (how wind, drafts and thermals affect this plane)")]
        [Tooltip("Multiplier on all wind. 1 = as authored on the sources. Light planes might use >1.")]
        [Range(0f, 3f)] public float airInfluence = 1f;

        [Tooltip("How quickly the plane picks up a change in the air (per second). " +
                 "Lower = smoother entry into zones, higher = snappier.")]
        [Range(0.5f, 20f)] public float airResponse = 4f;

        [Tooltip("Flying WITH the wind adds airspeed: fraction of the tailwind (along your nose) " +
                 "added to speed per second. This is how drafts give you energy back.")]
        [Range(0f, 2f)] public float tailwindSpeedGain = 0.5f;

        [Header("Landing Assist  (how much landing targets help this plane)")]
        [Tooltip("How strongly the heading swings toward a landing target (per second, at full assist). 0 = off.")]
        [Range(0f, 5f)] public float landingYawAssist = 1.5f;

        [Tooltip("With no pitch input near a target, how much the glide angle aims at it. 0 = off, 1 = full.")]
        [Range(0f, 1f)] public float landingPitchAssist = 0.8f;

        [Tooltip("Air brake on final approach: extra bleed of speed above cruise while assisted " +
                 "(fraction per second, at full assist). Stops you arriving too fast to land.")]
        [Range(0f, 5f)] public float landingSpeedBrake = 1.5f;

        [Header("Collisions  (be forgiving)")]
        [Tooltip("Hitting a wall slower than this (along the wall's normal) is just a bump; you keep flying.")]
        public float bumpTolerance = 6f;

        [Tooltip("Fraction of speed kept after a bump.")]
        [Range(0f, 1f)] public float bumpSpeedKeep = 0.75f;

        [Tooltip("Touching a floor-like surface: impact speed (into the surface) above this counts as a crash.")]
        public float touchdownMaxImpact = 6f;

        [Tooltip("Touching a floor-like surface: forward speed above this counts as a crash.")]
        public float touchdownMaxSpeed = 18f;

        [Header("Paper Wobble  (visual only)")]
        public bool wobbleEnabled = true;

        [Tooltip("Gentle flutter amplitude in degrees.")]
        [Range(0f, 10f)] public float wobbleAmount = 2.5f;

        [Tooltip("Flutter speed. Higher = more nervous paper.")]
        [Range(0.1f, 10f)] public float wobbleFrequency = 2.2f;

        [Tooltip("Extra shake in degrees while stalling (doubles as a stall warning).")]
        [Range(0f, 20f)] public float stallShake = 8f;

        [Tooltip("Extra flutter (degrees) per unit of wind speed. Makes being in moving air visible on the plane.")]
        [Range(0f, 1f)] public float turbulenceWobble = 0.25f;

        [Header("Wing Trails  (visual only)")]
        public bool trailsEnabled = true;
        [Tooltip("Trails start fading in above this speed.")]
        public float trailMinSpeed = 18f;
        [Tooltip("Trails are fully visible at this speed.")]
        public float trailFullSpeed = 35f;
        [Tooltip("How long trails linger (seconds).")]
        [Range(0.1f, 3f)] public float trailTime = 0.8f;
        [Range(0.01f, 0.5f)] public float trailWidth = 0.1f;

        // Convenience: sink rate at cruise speed, derived from the glide ratio.
        public float CruiseSinkRate => cruiseSpeed / glideRatio;
    }
}
