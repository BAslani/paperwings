using System;
using System.Collections.Generic;
using UnityEngine;
using PaperWings.Flight;

namespace PaperWings.Combat
{
    /// <summary>
    /// Sends PathFlyers along a route (its child points, in order) for mission nights (Stage 29):
    ///   Intercept - Accounting couriers fly the route; shoot them down before they get away.
    ///   Escort    - one Marketing courier at a time flies the route; rivals hunt it, you protect it.
    ///   Practice  - drones loop the route until you've shot enough (the first night).
    /// The ModeDirector reads the counts to decide when you've won or lost.
    /// Starts sending once the player is in the air; switching the object off clears the sky.
    /// </summary>
    public class ConvoySpawner : MonoBehaviour
    {
        public enum Kind { Intercept, Escort, Practice }

        [SerializeField] Kind kind = Kind.Intercept;
        [SerializeField] PathFlyer flyerPrefab;
        [Tooltip("How many fly in total (Intercept), or how many couriers in a row (Escort). Practice keeps going.")]
        [SerializeField] int count = 8;
        [Tooltip("At most this many in the air at once.")]
        [SerializeField] int maxAlive = 2;
        [SerializeField] float firstDelay = 3f;
        [Tooltip("Seconds between launches.")]
        [SerializeField] float interval = 7f;
        [SerializeField] float speed = 9f;
        [SerializeField] float health = 40f;
        [SerializeField] string flyerName = "Courier";

        /// <summary>The convoy running right now (one per night), or null.</summary>
        public static ConvoySpawner Active { get; private set; }
        /// <summary>A hostile flyer went down. Args: the flyer, who shot it (may be null).</summary>
        public static event Action<PathFlyer, PlaneController> HostileDowned;

        public Kind Mode => kind;
        public int Downed { get; private set; }
        /// <summary>Hostile flyers that got away / friendly couriers that arrived.</summary>
        public int Arrived { get; private set; }
        /// <summary>Friendly couriers shot down.</summary>
        public int Lost { get; private set; }
        public int Launched { get; private set; }
        public IReadOnlyList<PathFlyer> Alive => alive;
        public bool Started => started;

        readonly List<PathFlyer> alive = new List<PathFlyer>();
        Vector3[] points;
        float nextLaunch;
        bool started;

        void OnEnable()
        {
            Active = this;
            Downed = Arrived = Lost = Launched = 0;
            started = false;
            points = new Vector3[transform.childCount];
            for (int i = 0; i < points.Length; i++) points[i] = transform.GetChild(i).position;
        }

        void OnDisable()
        {
            if (Active == this) Active = null;
            foreach (var f in alive) if (f != null) { f.Finished -= OnFinished; Destroy(f.gameObject); }
            alive.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Active = null; HostileDowned = null; }

        void Update()
        {
            if (flyerPrefab == null || points == null || points.Length < 2) return;
            if (!started)
            {
                if (!PlayerFlying()) return;
                started = true;
                nextLaunch = Time.time + firstDelay;
            }

            alive.RemoveAll(f => f == null);
            bool more = kind == Kind.Practice || Launched < count;
            if (kind == Kind.Escort) more &= alive.Count == 0;   // one courier at a time
            if (more && alive.Count < maxAlive && Time.time >= nextLaunch)
            {
                Launch();
                nextLaunch = Time.time + interval;
            }
        }

        void Launch()
        {
            var f = Instantiate(flyerPrefab, points[0], Quaternion.identity);
            Faction side = kind == Kind.Escort ? CombatTarget.PlayerFaction : Faction.Accounting;
            f.name = $"{flyerName} {Launched + 1}";
            f.Launch(points, kind == Kind.Practice, speed, health, side, flyerName.ToUpperInvariant());
            f.Finished += OnFinished;
            alive.Add(f);
            Launched++;
        }

        void OnFinished(PathFlyer f, bool arrived)
        {
            f.Finished -= OnFinished;
            alive.Remove(f);
            if (kind == Kind.Escort)
            {
                if (arrived) Arrived++; else Lost++;
                return;
            }
            if (arrived) Arrived++;
            else
            {
                Downed++;
                HostileDowned?.Invoke(f, f.LastAttacker);
            }
        }

        static bool PlayerFlying()
        {
            foreach (var p in PlaneController.All) if (p.IsPlayer && p.State == PlaneState.Flying) return true;
            return false;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = kind == Kind.Escort ? new Color(0.4f, 0.7f, 1f) : new Color(1f, 0.5f, 0.3f);
            for (int i = 1; i < transform.childCount; i++)
                Gizmos.DrawLine(transform.GetChild(i - 1).position, transform.GetChild(i).position);
            if (kind == Kind.Practice && transform.childCount > 2)
                Gizmos.DrawLine(transform.GetChild(transform.childCount - 1).position, transform.GetChild(0).position);
        }
    }
}
