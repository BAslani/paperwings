using System;
using UnityEngine;

namespace PaperWings.Combat
{
    /// <summary>
    /// Tiny score keeper for the session: rivals shot down by the player.
    /// Static so any system can read it without wiring; resets each Play session.
    /// </summary>
    public static class CombatScore
    {
        /// <summary>Rivals shot down by the player personally.</summary>
        public static int Kills { get; private set; }
        /// <summary>Rivals shot down by the player's whole faction (player + wingmen).</summary>
        public static int TeamKills { get; private set; }
        /// <summary>Fired when the player shoots a rival down.</summary>
        public static event Action<int> KillScored;
        /// <summary>Fired when a wingman shoots a rival down.</summary>
        public static event Action<int> AllyKillScored;

        /// <summary>Fired whenever any projectile (staple, pencil...) hits a plane. Args: shooter, victim.</summary>
        public static event Action<Flight.PlaneController, GameObject> AnyHit;

        /// <summary>Credits a kill to the player's faction; byPlayer = the player did it personally.</summary>
        public static void AddKill(bool byPlayer = true)
        {
            TeamKills++;
            if (byPlayer)
            {
                Kills++;
                KillScored?.Invoke(Kills);
            }
            else AllyKillScored?.Invoke(TeamKills);
        }

        public static void ReportHit(Flight.PlaneController shooter, GameObject victim) => AnyHit?.Invoke(shooter, victim);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Kills = 0;
            TeamKills = 0;
            AllyKillScored = null;
            KillScored = null;
            AnyHit = null;
        }
    }
}
