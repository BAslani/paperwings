using UnityEngine;

namespace PaperWings.Gameplay
{
    /// <summary>
    /// One place to fly: the office desk, the break room... Each room has its own throw point and
    /// its own groups that game modes switch on (ring route, rivals, boss, squadron). A mode can only
    /// be played in a room that has the group it needs.
    ///
    /// Rooms live in the same scene, far enough apart that the fog hides one from the other.
    /// The ModeDirector moves your hand (the throw point) to the chosen room.
    /// </summary>
    public class Room : MonoBehaviour
    {
        public string displayName = "Office";
        [TextArea(1, 3)] public string description = "";
        [Tooltip("Where the plane is held before the throw in this room.")]
        public Transform spawnPoint;

        [Header("Groups switched on per mode (leave empty if this room doesn't have that mode)")]
        public GameObject routeGroup;
        public GameObject combatGroup;
        public GameObject bossGroup;
        [Tooltip("Extra rivals plus your wingmen for Squadron Battle (also needs the combat group).")]
        public GameObject squadronGroup;

        [Header("Places inside this room (Stage 27): campaign nights can pick one by name")]
        [Tooltip("Extra throw points, e.g. 'Lounge' or 'Landing3'. A night's Spawn field picks one by name.")]
        public Transform[] extraSpawns;
        [Tooltip("Extra ring routes (each a group holding a RaceRoute). A night's Area field picks one by name. " +
                 "A route may carry its own throw point as a child named 'Spawn'.")]
        public GameObject[] extraRoutes;
        [Tooltip("Mission groups (Stage 29): a convoy route, an ace's spawner... A night's Group field switches one on by name.")]
        public GameObject[] extraGroups;

        /// <summary>The mission group with this name, or null.</summary>
        public GameObject GroupNamed(string groupName)
        {
            if (string.IsNullOrEmpty(groupName) || extraGroups == null) return null;
            foreach (var g in extraGroups) if (g != null && g.name == groupName) return g;
            return null;
        }

        /// <summary>The extra throw point with this name, or null.</summary>
        public Transform SpawnNamed(string spawnName)
        {
            if (string.IsNullOrEmpty(spawnName) || extraSpawns == null) return null;
            foreach (var s in extraSpawns) if (s != null && s.name == spawnName) return s;
            return null;
        }

        /// <summary>The extra route group with this name (or the main route if it has that name), or null.</summary>
        public GameObject RouteNamed(string routeName)
        {
            if (string.IsNullOrEmpty(routeName)) return null;
            if (routeGroup != null && routeGroup.name == routeName) return routeGroup;
            if (extraRoutes == null) return null;
            foreach (var r in extraRoutes) if (r != null && r.name == routeName) return r;
            return null;
        }

        [Header("Medals")]
        [Tooltip("Bronze / silver / gold seconds for this room's Ring Run. Zero = the Progression defaults.")]
        public Vector3 ringRunMedals = Vector3.zero;

        public bool Supports(GameMode mode) => mode switch
        {
            GameMode.FreeFlight => true,
            GameMode.RingRun => routeGroup != null,
            GameMode.Dogfight => combatGroup != null,
            GameMode.Squadron => combatGroup != null && squadronGroup != null,
            GameMode.BossFight => bossGroup != null,
            _ => false
        };

        void OnDrawGizmos()
        {
            if (spawnPoint == null) return;
            Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.8f);
            Gizmos.DrawWireSphere(spawnPoint.position, 1.5f);
        }
    }
}
