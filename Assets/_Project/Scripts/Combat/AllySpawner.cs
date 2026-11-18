using UnityEngine;
using PaperWings.Flight;

namespace PaperWings.Combat
{
    /// <summary>
    /// Owns one wingman: a friendly AI plane that flies with the player.
    ///   - While the player's plane is in hand, the wingman waits in hand beside it.
    ///   - When the player throws, the wingman launches alongside at the same speed.
    ///   - If it's shot down, a fresh one rejoins from behind the player after a few seconds.
    /// Disable the spawner (or its group) and the wingman disappears with it.
    /// </summary>
    public class AllySpawner : MonoBehaviour
    {
        [SerializeField] GameObject wingmanPrefab;
        [SerializeField] PlaneController leader;
        [Tooltip("Formation slot relative to the leader in flight (x right, y up, z forward).")]
        [SerializeField] Vector3 formationOffset = new Vector3(7f, 1f, -6f);
        [Tooltip("Where it waits beside the player's hand before the throw.")]
        [SerializeField] Vector3 handOffset = new Vector3(3f, 0.6f, -1.5f);
        [SerializeField] float respawnDelay = 5f;

        public PlaneController Current { get; private set; }

        PaperDamage paper;
        float respawnAt = -1f;

        void OnEnable()
        {
            if (wingmanPrefab == null || leader == null) { enabled = false; return; }
            var go = Instantiate(wingmanPrefab, transform.position, transform.rotation);
            go.name = $"Wingman ({name})";
            Current = go.GetComponent<PlaneController>();
            paper = go.GetComponent<PaperDamage>();
            var pilot = go.GetComponent<RivalPilot>();
            if (pilot != null)
            {
                pilot.Leader = leader;
                pilot.FormationOffset = formationOffset;
            }
            Current.Downed += OnDowned;
            WaitInHand();
        }

        void OnDisable()
        {
            if (Current != null)
            {
                Current.Downed -= OnDowned;
                Destroy(Current.gameObject);
            }
            Current = null;
        }

        void OnDowned(bool crash) => respawnAt = Time.time + respawnDelay;

        void Update()
        {
            if (Current == null) return;

            switch (leader.State)
            {
                case PlaneState.Held:
                    // Player is about to throw: wait in hand beside them (and heal up).
                    WaitInHand();
                    break;

                case PlaneState.Flying:
                    if (Current.State == PlaneState.Held)
                        LaunchNear(formationOffset * 0.5f, leader.Speed);   // thrown together with the player
                    else if (Current.State == PlaneState.Down && respawnAt > 0f && Time.time >= respawnAt)
                        LaunchNear(new Vector3(formationOffset.x, 4f, -18f), leader.Speed);   // rejoin from behind
                    break;
            }
        }

        void WaitInHand()
        {
            respawnAt = -1f;
            Transform l = leader.transform;
            Current.Hold(l.position + l.rotation * handOffset, l.rotation);
            if (paper != null) paper.Repair();
        }

        void LaunchNear(Vector3 offset, float speed)
        {
            respawnAt = -1f;
            Transform l = leader.transform;
            Quaternion heading = Quaternion.Euler(0f, l.eulerAngles.y, 0f);
            Current.Hold(l.position + heading * offset, heading);
            if (paper != null) paper.Repair();
            Current.Launch(Mathf.Max(speed, 12f), 0f);
        }
    }
}
