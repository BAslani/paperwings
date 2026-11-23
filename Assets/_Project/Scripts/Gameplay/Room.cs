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
