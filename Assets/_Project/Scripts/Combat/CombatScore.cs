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
        public static int Kills { get; private set; }
        /// <summary>Fired when the player shoots a rival down.</summary>
        public static event Action<int> KillScored;

        /// <summary>Fired whenever any projectile (staple, pencil...) hits a plane. Args: shooter, victim.</summary>
        public static event Action<Flight.PlaneController, Flight.PlaneController> AnyHit;

        public static void AddKill()
        {
            Kills++;
            KillScored?.Invoke(Kills);
        }

        public static void ReportHit(Flight.PlaneController shooter, Flight.PlaneController victim) => AnyHit?.Invoke(shooter, victim);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Kills = 0;
            KillScored = null;
            AnyHit = null;
        }
    }
}
