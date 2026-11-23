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
        [Tooltip("Which department this belongs to. Weapons and AI only hurt/target other factions.")]
        [SerializeField] Faction faction = Faction.Accounting;
        [SerializeField] bool isBoss;
        [Tooltip("Where to aim (e.g. a boss's weak spot). Defaults to the object's position.")]
        [SerializeField] Transform aimPoint;
        [Tooltip("AI pilots attack this even though it isn't a plane (couriers, bombers, boss turrets).")]
        [SerializeField] bool aiTarget;

        public string DisplayName { get => displayName; set => displayName = value; }
        public Faction Faction { get => faction; set => faction = value; }
        public bool AiTarget { get => aiTarget; set => aiTarget = value; }
        public bool IsBoss => isBoss;
        public PlaneController Plane { get; private set; }
        /// <summary>True for friendly AI planes on the player's faction (wingmen).</summary>
        public bool IsAlly => faction == PlayerFaction && Plane != null && !Plane.IsPlayer;

        /// <summary>The faction the player flies for.</summary>
        public const Faction PlayerFaction = Faction.Marketing;

        IDamageable health;
        IMovingTarget mover;

        void Awake()
        {
            Plane = GetComponent<PlaneController>();
            health = GetComponent<IDamageable>();
            mover = GetComponent<IMovingTarget>();
            if (Plane != null && Plane.IsPlayer) faction = PlayerFaction;   // the player is always Marketing
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();

        public Vector3 Position => aimPoint != null ? aimPoint.position : transform.position;
        public Vector3 Velocity => Plane != null ? Plane.Velocity : (mover != null ? mover.Velocity : Vector3.zero);
        /// <summary>True for anything on the player's faction (the player and wingmen).</summary>
        public bool IsPlayerSide => faction == PlayerFaction;
        public bool IsAlive => Plane != null ? Plane.State == PlaneState.Flying : (health == null || health.IsAlive);
        public float Health01 => health != null ? health.Health01 : 1f;

        /// <summary>
        /// The living ENEMY of 'shooter' (any other faction) closest to 'forward' within the cone and range.
        /// </summary>
        public static CombatTarget FindBestInCone(Vector3 origin, Vector3 forward, float maxAngle, float range,
                                                  Faction shooter, bool planesOnly = false)
        {
            CombatTarget best = null;
            float bestAngle = maxAngle;
            foreach (var t in All)
            {
                if (t.faction == shooter || t.faction == Faction.Neutral || !t.IsAlive || (planesOnly && t.Plane == null)) continue;
                Vector3 to = t.Position - origin;
                if (to.magnitude > range) continue;
                float angle = Vector3.Angle(forward, to);
                if (angle < bestAngle) { bestAngle = angle; best = t; }
            }
            return best;
        }

        /// <summary>The nearest living enemy plane (or AI target: courier, bomber) of 'shooter' within range (used by AI pilots).</summary>
        public static CombatTarget FindNearestEnemyPlane(Vector3 origin, Faction shooter, float range)
        {
            CombatTarget best = null;
            float bestDist = range;
            foreach (var t in All)
            {
                if (t.faction == shooter || t.faction == Faction.Neutral || (t.Plane == null && !t.aiTarget) || !t.IsAlive) continue;
                float d = Vector3.Distance(origin, t.Position);
                if (d < bestDist) { bestDist = d; best = t; }
            }
            return best;
        }

        /// <summary>Which faction a hit object belongs to, or null for neutral scenery.</summary>
        public static Faction? FactionOf(Component c)
        {
            var t = c.GetComponentInParent<CombatTarget>();
            return t != null ? t.faction : (Faction?)null;
        }

        /// <summary>The faction of whatever owns this component (planes without a CombatTarget fall back by IsPlayer).</summary>
        public static Faction FactionOfPlane(PlaneController plane)
        {
            var t = plane.GetComponent<CombatTarget>();
            if (t != null) return t.faction;
            return plane.IsPlayer ? PlayerFaction : Faction.Accounting;
        }
    }
}
