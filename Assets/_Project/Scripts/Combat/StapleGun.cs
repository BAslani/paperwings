using UnityEngine;
using PaperWings.Flight;

namespace PaperWings.Combat
{
    /// <summary>
    /// A nose-mounted staple gun. Anyone can pull the trigger (player via PlayerShooter, AI via
    /// RivalPilot); this component handles rate of fire, ammo, spread and aim assist.
    /// Ammo refills whenever the plane is back in hand.
    /// </summary>
    public class StapleGun : MonoBehaviour
    {
        [SerializeField] PlaneController plane;
        [SerializeField] WeaponSettings settings;
        [SerializeField] StapleProjectile projectilePrefab;
        [Tooltip("Where staples come out. Defaults to just in front of the nose.")]
        [SerializeField] Transform muzzle;

        public WeaponSettings Settings => settings;
        public int Ammo { get; private set; }
        public bool Unlimited => settings != null && settings.clipSize <= 0;

        // ---- Aim info, updated every frame (the HUD reticle reads these) ----
        /// <summary>The rival aim assist is currently locked onto (null if none).</summary>
        public PlaneController LockTarget { get; private set; }
        /// <summary>Where a staple fired now would go: the lead point on the locked target, or straight ahead.</summary>
        public Vector3 AimPoint { get; private set; }
        /// <summary>Distance at which the unlocked reticle is drawn ahead of the nose.</summary>
        public const float ReticleDistance = 30f;

        float nextFireTime;
        float lastShotTime = -999f;
        float refillProgress;   // fractional staples accumulated by the trickle refill

        void Awake()
        {
            if (plane == null) plane = GetComponent<PlaneController>();
            Refill();
        }

        void Update()
        {
            if (settings == null || Unlimited) return;

            if (plane.State == PlaneState.Held)
            {
                if (Ammo < settings.clipSize) Refill();
                return;
            }

            // Trickle refill in flight, once you've stopped shooting for a moment.
            if (plane.State == PlaneState.Flying && Ammo < settings.clipSize && settings.refillPerSecond > 0f
                && Time.time - lastShotTime > settings.refillDelay)
            {
                refillProgress += settings.refillPerSecond * Time.deltaTime;
                if (refillProgress >= 1f)
                {
                    int add = Mathf.FloorToInt(refillProgress);
                    Ammo = Mathf.Min(settings.clipSize, Ammo + add);
                    refillProgress -= add;
                }
            }
        }

        // LateUpdate: after everything has moved this frame, so the reticle matches what you see.
        void LateUpdate()
        {
            if (settings == null) return;
            Vector3 origin = MuzzlePosition;
            LockTarget = FindAssistTarget(origin);
            AimPoint = LockTarget != null ? LeadPoint(origin, LockTarget) : origin + plane.transform.forward * ReticleDistance;
        }

        Vector3 MuzzlePosition => muzzle != null ? muzzle.position : plane.transform.position + plane.transform.forward * 1.6f;

        public void Refill()
        {
            Ammo = settings != null ? settings.clipSize : 0;
            refillProgress = 0f;
        }

        /// <summary>Fires one staple if the gun is ready. Returns true if it fired.</summary>
        public bool TryFire()
        {
            if (settings == null || projectilePrefab == null) return false;
            if (plane.State != PlaneState.Flying || Time.time < nextFireTime) return false;
            if (!Unlimited && Ammo <= 0) return false;

            nextFireTime = Time.time + settings.fireInterval;
            lastShotTime = Time.time;
            if (!Unlimited) Ammo--;

            // Straight ahead, or at the lead point if aim assist has a target.
            Vector3 origin = MuzzlePosition;
            PlaneController target = FindAssistTarget(origin);
            Vector3 dir = target != null ? (LeadPoint(origin, target) - origin).normalized : plane.transform.forward;
            dir = Quaternion.Euler(Random.Range(-settings.spread, settings.spread), Random.Range(-settings.spread, settings.spread), 0f) * dir;

            var staple = Instantiate(projectilePrefab, origin, Quaternion.LookRotation(dir));
            staple.Launch(plane, plane.Velocity + dir * settings.projectileSpeed, settings.damage, settings.projectileLifetime);
            return true;
        }

        /// <summary>The closest-to-the-nose opponent inside the assist cone and range, or null.</summary>
        PlaneController FindAssistTarget(Vector3 origin)
        {
            if (settings.aimAssistAngle <= 0f || plane.State != PlaneState.Flying) return null;
            Vector3 forward = plane.transform.forward;

            PlaneController best = null;
            float bestAngle = settings.aimAssistAngle;
            foreach (var other in PlaneController.All)
            {
                if (other == plane || other.IsPlayer == plane.IsPlayer || other.State != PlaneState.Flying) continue;
                Vector3 to = other.transform.position - origin;
                if (to.magnitude > settings.aimAssistRange) continue;
                float angle = Vector3.Angle(forward, to);
                if (angle < bestAngle) { bestAngle = angle; best = other; }
            }
            return best;
        }

        /// <summary>Where the target WILL be when a staple gets there (its position + velocity x travel time).</summary>
        Vector3 LeadPoint(Vector3 origin, PlaneController target)
        {
            float travelTime = Vector3.Distance(origin, target.transform.position) / settings.projectileSpeed;
            return target.transform.position + target.Velocity * travelTime;
        }
    }
}
