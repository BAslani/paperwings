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
        /// <summary>True while this spawner is sending an ace (a campaign night's named rival).</summary>
        public bool IsAce => ace != null;

        PaperDamage currentPaper;
        float respawnAt;
        AceStyle ace;

        /// <summary>How an ace differs from a regular rival.</summary>
        public class AceStyle
        {
            public string name = "THE AUDITOR'S ACE";
            public RivalPilotSettings pilot;
            public float integrityScale = 3f;
            public float scale = 1.35f;
            public Color trail = new Color(1f, 0.8f, 0.2f);
        }

        RivalPilotSettings pilotOverride;
        /// <summary>
        /// Flies this spawner's rival (and every respawn) with other pilot settings, e.g. rookies on an
        /// early campaign night. Null = the spawner's own pilot. Cleared when the spawner is switched off.
        /// </summary>
        public RivalPilotSettings PilotOverride
        {
            get => pilotOverride;
            set { pilotOverride = value; ApplyPilot(); }
        }

        void ApplyPilot()
        {
            if (Current == null || ace != null) return;   // an ace keeps its own pilot
            var pilot = Current.GetComponent<RivalPilot>();
            if (pilot != null && (pilotOverride != null || pilotSettings != null)) pilot.Settings = pilotOverride != null ? pilotOverride : pilotSettings;
        }

        /// <summary>Turns this spawner's rival (and every respawn) into an ace until the spawner is switched off.</summary>
        public void MakeAce(AceStyle style)
        {
            ace = style;
            ApplyAce();
        }

        void ApplyAce()
        {
            if (ace == null || Current == null) return;
            var pilot = Current.GetComponent<RivalPilot>();
            if (pilot != null && ace.pilot != null) pilot.Settings = ace.pilot;
            if (currentPaper != null) { currentPaper.IntegrityScale = ace.integrityScale; currentPaper.Repair(); }
            Current.transform.localScale = Vector3.one * ace.scale;
            var target = Current.GetComponent<CombatTarget>();
            if (target != null) target.DisplayName = ace.name;
            var trails = Current.GetComponent<PlaneTrails>();
            if (trails != null) trails.SetColors(ace.trail, new Color(ace.trail.r, ace.trail.g, ace.trail.b, 0f), 0.6f);
        }

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
            ace = null;
            pilotOverride = null;
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
            // Credit the player's faction if one of its planes (you or a wingman) hit it recently.
            var attacker = currentPaper != null ? currentPaper.LastAttacker : null;
            if (attacker != null && Time.time - currentPaper.LastAttackTime < 3f
                && CombatTarget.FactionOfPlane(attacker) == CombatTarget.PlayerFaction)
                CombatScore.AddKill(byPlayer: attacker.IsPlayer);

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
