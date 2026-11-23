using System;
using UnityEngine;
using PaperWings.Flight;

namespace PaperWings.Combat
{
    /// <summary>
    /// What every boss has in common (Stage 26), so the mode director, HUD and campaign can run any
    /// of them: The Stapler (downdraft slams), The Mainframe (sweeping drafts and hot exhaust) and
    /// The Condenser (a rooftop fan that lifts, then sucks). Each boss is built on one kind of air.
    ///
    /// A boss is armored most of the time and opens a WEAK SPOT on a rhythm you learn; hits there
    /// do <see cref="WeakSpotMultiplier"/> times the damage.
    /// </summary>
    public abstract class BossBase : MonoBehaviour, IDamageable
    {
        /// <summary>The boss that is switched on right now (one room's boss group at a time). Null if none.</summary>
        public static BossBase Active { get; private set; }

        public abstract string DisplayName { get; }
        public abstract string Title { get; }
        public abstract float AggroRange { get; }
        public abstract float Phase2At { get; }
        public abstract float Phase3At { get; }
        public abstract float WeakSpotMultiplier { get; }

        public abstract float Health01 { get; }
        public bool IsAlive => Health01 > 0f;
        public abstract bool IsDefeated { get; }
        public abstract bool IsAwake { get; }
        /// <summary>True while it's rattling awake (before its first attack).</summary>
        public abstract bool Waking { get; }
        public int Phase => Health01 > Phase2At ? 1 : (Health01 > Phase3At ? 2 : 3);
        /// <summary>True while hits do extra damage.</summary>
        public abstract bool WeakSpotOpen { get; }

        /// <summary>A big attack is coming: the HUD pulses <see cref="WarningText"/> in red.</summary>
        public virtual bool Warning => false;
        public virtual string WarningText => "INCOMING!  get clear!";
        /// <summary>What to tell the player while it's armored (teaches the fight).</summary>
        public virtual string ArmoredHint => "ARMORED: dodge, then hit the weak spot";
        /// <summary>What to say while the weak spot is open.</summary>
        public virtual string WeakSpotText => "WEAK SPOT OPEN!";

        /// <summary>Args: damage dealt, whether it hit the weak spot.</summary>
        public event Action<float, bool> Damaged;
        public event Action Defeated;

        protected void RaiseDamaged(float dealt, bool weak) => Damaged?.Invoke(dealt, weak);
        protected void RaiseDefeated() => Defeated?.Invoke();

        /// <summary>Full health, attack loop restarted. Called when a Boss Fight begins.</summary>
        public abstract void ResetFight();
        public abstract void ApplyDamage(float amount, PlaneController source);

        protected virtual void OnEnable() => Active = this;
        protected virtual void OnDisable() { if (Active == this) Active = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Active = null;

        /// <summary>The player's plane, if it's in the air.</summary>
        protected static PlaneController FindFlyingPlayer()
        {
            foreach (var p in PlaneController.All)
                if (p.IsPlayer && p.State == PlaneState.Flying) return p;
            return null;
        }
    }
}
