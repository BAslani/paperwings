using System;
using System.Collections.Generic;
using UnityEngine;
using PaperWings.Air;
using PaperWings.Flight;

namespace PaperWings.Gameplay
{
    /// <summary>How much a landing target wants to steer the plane this tick.</summary>
    public struct LandingAssist
    {
        /// <summary>0 = no assist, 1 = full assist.</summary>
        public float strength;
        /// <summary>Signed heading error to the target, in degrees (positive = target is to the right).</summary>
        public float yawError;
        /// <summary>The pitch angle that points the nose at the target.</summary>
        public float approachPitch;
    }

    /// <summary>
    /// A landing target (the coffee mug). Does two things:
    ///  1. Approach assist: when you're near and roughly heading at it, it gently nudges
    ///     heading and glide angle toward it (the PlaneController asks via GetAssist).
    ///  2. Touchdown judging: when the plane touches the top surface, it checks forgiving
    ///     speed/impact/bank limits. Pass = snap to a neat pose and celebrate. Fail = crash with a reason.
    ///
    /// Place the object at the base of the target. 'surfaceHeight' is where the top is.
    /// Touching the SIDE of the target is handled by the plane's normal bump/crash rules.
    /// </summary>
    public class LandingZone : MonoBehaviour
    {
        [SerializeField] LandingSettings settings;
        [Tooltip("Height of the landing surface above this object's origin.")]
        [SerializeField] float surfaceHeight = 5f;
        [Tooltip("Radius of the landing surface.")]
        [SerializeField] float surfaceRadius = 3f;
        [Tooltip("Optional burst played on a successful landing.")]
        [SerializeField] ParticleSystem celebration;

        /// <summary>Fired on a successful landing. Args: plane, message.</summary>
        public static event Action<PlaneController, string> AnyLanded;

        static readonly List<LandingZone> zones = new List<LandingZone>();

        public LandingSettings Settings => settings;
        public Vector3 SurfaceCenter => transform.position + Vector3.up * surfaceHeight;

        void OnEnable() => zones.Add(this);
        void OnDisable() => zones.Remove(this);

        /// <summary>The landing target closest to a point (each room has its own mug).</summary>
        public static LandingZone Nearest(Vector3 position)
        {
            LandingZone best = null;
            float bestSq = float.MaxValue;
            foreach (var z in zones)
            {
                float d = (z.transform.position - position).sqrMagnitude;
                if (d < bestSq) { bestSq = d; best = z; }
            }
            return best;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => zones.Clear();

        // ------------------------------------------------------------------ assist

        /// <summary>Strongest assist from any landing zone, for a plane at 'position' with heading 'yawDeg'.</summary>
        public static LandingAssist GetAssist(Vector3 position, float yawDeg)
        {
            LandingAssist best = default;
            foreach (var z in zones)
            {
                LandingAssist a = z.ComputeAssist(position, yawDeg);
                if (a.strength > best.strength) best = a;
            }
            return best;
        }

        LandingAssist ComputeAssist(Vector3 position, float yawDeg)
        {
            LandingAssist none = default;
            if (settings == null) return none;

            Vector3 toTarget = SurfaceCenter + Vector3.up * 0.3f - position;
            Vector3 flat = new Vector3(toTarget.x, 0f, toTarget.z);
            float dist = flat.magnitude;

            // Must be within range, and above (or just about level with) the surface.
            if (dist > settings.assistRadius) return none;
            if (toTarget.y < -settings.assistHeight || toTarget.y > 1.5f) return none;

            float targetYaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
            float yawError = Mathf.DeltaAngle(yawDeg, targetYaw);
            if (Mathf.Abs(yawError) > settings.assistMaxAngle) return none;

            // Stronger the closer you are and the better you're lined up.
            float closeness = Mathf.SmoothStep(0f, 1f, 1f - dist / settings.assistRadius);
            float alignment = 1f - Mathf.Abs(yawError) / settings.assistMaxAngle;

            return new LandingAssist
            {
                strength = closeness * alignment,
                yawError = yawError,
                approachPitch = Mathf.Clamp(Mathf.Atan2(toTarget.y, Mathf.Max(dist, 1f)) * Mathf.Rad2Deg, -30f, 0f)
            };
        }

        // ------------------------------------------------------------------ touchdown

        /// <summary>
        /// Called by the plane when it hits one of this zone's colliders.
        /// Returns true if the zone handled it (landed or crashed), false to use normal collision rules.
        /// </summary>
        public bool HandleTouchdown(PlaneController plane, Collision collision)
        {
            if (settings == null || !plane.IsPlayer) return false;   // only the player lands on targets; rivals just bump

            ContactPoint contact = collision.GetContact(0);
            if (contact.normal.y < 0.5f) return false;   // hit the side, not the top

            float impact = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, contact.normal));
            string fail = null;
            if (plane.Speed > settings.maxLandingSpeed)
                fail = $"Too fast! ({plane.Speed:0} / max {settings.maxLandingSpeed:0})";
            else if (impact > settings.maxImpactSpeed)
                fail = "Came down too hard! Flatten out sooner.";
            else if (Mathf.Abs(plane.BankAngle) > settings.maxBank)
                fail = "Wings weren't level!";

            if (fail != null)
            {
                plane.GoDown(crash: true, reason: fail);
                return true;
            }

            // Light snap: slide to a level pose on the surface, keeping heading, pulled toward the middle.
            Vector3 offset = plane.transform.position - SurfaceCenter;
            offset.y = 0f;
            offset = Vector3.ClampMagnitude(offset, surfaceRadius * 0.4f);
            Vector3 restPos = SurfaceCenter + offset + Vector3.up * 0.2f;
            Quaternion restRot = Quaternion.Euler(0f, plane.transform.eulerAngles.y, 0f);

            string msg = settings.successMessages != null && settings.successMessages.Length > 0
                ? settings.successMessages[UnityEngine.Random.Range(0, settings.successMessages.Length)]
                : "Landed!";

            plane.Land(restPos, restRot, settings.snapTime, msg);
            if (celebration != null) celebration.Play();
            AnyLanded?.Invoke(plane, msg);
            return true;
        }

        // ------------------------------------------------------------------ gizmos

        void OnDrawGizmos()
        {
            Vector3 c = SurfaceCenter;
            Gizmos.color = new Color(0.3f, 1f, 0.4f);
            AirGizmos.DrawCircle(c, Vector3.up, surfaceRadius);
            if (settings == null) return;

            // Assist volume: a cylinder from the surface up to assistHeight.
            Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.35f);
            AirGizmos.DrawCircle(c, Vector3.up, settings.assistRadius);
            AirGizmos.DrawCircle(c + Vector3.up * settings.assistHeight, Vector3.up, settings.assistRadius);
            for (int i = 0; i < 4; i++)
            {
                Vector3 side = Quaternion.Euler(0f, i * 90f, 0f) * Vector3.forward * settings.assistRadius;
                Gizmos.DrawLine(c + side, c + side + Vector3.up * settings.assistHeight);
            }
        }
    }
}
