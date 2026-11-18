using UnityEngine;
using PaperWings.Flight;

namespace PaperWings.Combat
{
    /// <summary>
    /// Spawns one rival plane here, already flying, and respawns it a few seconds after it goes
    /// down. If the player's staples caused it, the player scores a kill.
    /// Place the spawner where you want the rival to patrol (it circles this point).
    /// To turn rivals off, disable the spawners (or their parent "Combat" object): the rival
    /// is removed with it, and a fresh one appears when the spawner is enabled again.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [SerializeField] GameObject rivalPrefab;
        [SerializeField] RivalPilotSettings pilotSettings;
        [Tooltip("Airspeed the rival starts with.")]
        [SerializeField] float spawnSpeed = 14f;
        [Tooltip("Stagger the first appearance (seconds), so rivals don't all arrive at once.")]
        [SerializeField] float firstSpawnDelay = 1f;

        public PlaneController Current { get; private set; }

        PaperDamage currentPaper;
        float respawnAt;

        // UNITY NOTE: OnEnable/OnDisable run every time the object is switched on/off (Start only
        // runs once), so spawning here makes game modes able to toggle rivals cleanly.
        void OnEnable()
        {
            if (rivalPrefab == null) { enabled = false; return; }
            var go = Instantiate(rivalPrefab, transform.position, transform.rotation);
            go.name = $"Rival ({name})";
            Current = go.GetComponent<PlaneController>();
            currentPaper = go.GetComponent<PaperDamage>();
            var pilot = go.GetComponent<RivalPilot>();
            if (pilot != null) pilot.PatrolCenter = transform.position;
            Current.Downed += OnRivalDowned;

            Current.Hold(transform.position, transform.rotation);   // parked until its first spawn
            respawnAt = Time.time + firstSpawnDelay;
        }

        void OnDisable()
        {
            // Take our rival with us (it's a separate object in the scene, not a child).
            if (Current != null)
            {
                Current.Downed -= OnRivalDowned;
                Destroy(Current.gameObject);
            }
            Current = null;
            currentPaper = null;
        }

        void OnRivalDowned(bool crash)
        {
            // Credit the player if their staples did it recently.
            if (currentPaper != null && currentPaper.LastAttacker != null && currentPaper.LastAttacker.IsPlayer
                && Time.time - currentPaper.LastAttackTime < 3f)
                CombatScore.AddKill();

            respawnAt = Time.time + (pilotSettings != null ? pilotSettings.respawnDelay : 4f);
        }

        void Update()
        {
            if (Current == null || respawnAt <= 0f || Time.time < respawnAt) return;
            respawnAt = 0f;

            // Fresh sheet of paper, back at the spawn point, launched straight into the air.
            Current.Hold(transform.position, transform.rotation);
            if (currentPaper != null) currentPaper.Repair();
            Current.Launch(spawnSpeed, 0f);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, 1.5f);
            if (pilotSettings != null) Air.AirGizmos.DrawCircle(transform.position, Vector3.up, pilotSettings.patrolRadius);
        }
    }
}
