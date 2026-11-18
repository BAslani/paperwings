using System.Collections.Generic;
using UnityEngine;
using PaperWings.Flight;

namespace PaperWings.Combat
{
    /// <summary>
    /// Marks something as a valid target for aim assist, pencil lock-on and HUD markers:
    /// planes (player and rivals) and bosses alike. It works out position, velocity, side and
    /// health from whatever else is on the object (PlaneController, any IDamageable).
    /// </summary>
    [DisallowMultipleComponent]
    public class CombatTarget : MonoBehaviour
    {
        public static readonly List<CombatTarget> All = new List<CombatTarget>();

        [SerializeField] string displayName = "Rival";
        [SerializeField] bool isBoss;
        [Tooltip("Where to aim (e.g. a boss's weak spot). Defaults to the object's position.")]
        [SerializeField] Transform aimPoint;

        public string DisplayName => displayName;
        public bool IsBoss => isBoss;
        public PlaneController Plane { get; private set; }

        IDamageable health;

        void Awake()
        {
            Plane = GetComponent<PlaneController>();
            health = GetComponent<IDamageable>();
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();

        public Vector3 Position => aimPoint != null ? aimPoint.position : transform.position;
        public Vector3 Velocity => Plane != null ? Plane.Velocity : Vector3.zero;
        /// <summary>True for the player's side (currently just the player's plane).</summary>
        public bool IsPlayerSide => Plane != null && Plane.IsPlayer;
        public bool IsAlive => Plane != null ? Plane.State == PlaneState.Flying : (health == null || health.IsAlive);
        public float Health01 => health != null ? health.Health01 : 1f;

        /// <summary>
        /// The living target on the requested side that's closest to 'forward' within the cone and range.
        /// </summary>
        public static CombatTarget FindBestInCone(Vector3 origin, Vector3 forward, float maxAngle, float range, bool wantPlayerSide)
        {
            CombatTarget best = null;
            float bestAngle = maxAngle;
            foreach (var t in All)
            {
                if (t.IsPlayerSide != wantPlayerSide || !t.IsAlive) continue;
                Vector3 to = t.Position - origin;
                if (to.magnitude > range) continue;
                float angle = Vector3.Angle(forward, to);
                if (angle < bestAngle) { bestAngle = angle; best = t; }
            }
            return best;
        }

        /// <summary>Which side a hit object belongs to: true = player, false = enemy, null = neutral scenery.</summary>
        public static bool? SideOf(Component c)
        {
            var t = c.GetComponentInParent<CombatTarget>();
            return t != null ? t.IsPlayerSide : (bool?)null;
        }
    }
}
