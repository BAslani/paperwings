using UnityEngine;
using PaperWings.Combat;
using PaperWings.Flight;

namespace PaperWings.Gameplay
{
    /// <summary>
    /// Turns saved unlocks into gameplay on the player's plane. Upgrades belong to one fold, so the
    /// loadout is re-applied whenever you switch folds, as well as whenever progress changes:
    ///   Pencil Missiles / Pencil Rack -> how many wing slots hold pencils (0 / 2 / 4)
    ///   Staple Magazine               -> extra staples
    ///   Heavy Paper                   -> tougher paper
    ///   Pusher Propeller              -> the prop on the tail, and its boost lift
    ///   Glider / Stunt / Canard folds -> which folds the picker offers (global)
    ///   Staple Gun                    -> only if the settings make staples an unlock (global)
    /// A brand-new save starts on the raw Dart with just the staple gun.
    /// </summary>
    public class ProgressionApplier : MonoBehaviour
    {
        [SerializeField] PlaneController player;
        [SerializeField] FoldSelector folds;
        [SerializeField] ProgressionSettings settings;

        [Header("What the upgrades give")]
        [SerializeField] int pencilsWithMissiles = 2;
        [SerializeField] int pencilsWithRack = 4;
        [SerializeField] int magazineBonusStaples = 15;
        [SerializeField] float heavyPaperScale = 1.25f;

        bool pencilsBanned, fullLoadout;
        /// <summary>
        /// Flight School: every fold, every upgrade, without touching the save. Off again as soon as
        /// any other mode starts.
        /// </summary>
        public bool FullLoadout
        {
            get => fullLoadout;
            set { if (fullLoadout == value) return; fullLoadout = value; Apply(); }
        }

        bool Owned(string id) => fullLoadout || Progression.IsUnlocked(id);

        /// <summary>Set by campaign nights that take your pencils away. Takes effect next time the plane is in hand.</summary>
        public bool PencilsBanned
        {
            get => pencilsBanned;
            set { if (pencilsBanned == value) return; pencilsBanned = value; ApplyLoadout(); }
        }

        // Awake (not Start) so the fold picker and pencil launcher see the limits before they set up.
        void Awake()
        {
            if (folds != null) folds.FoldChanged += OnFoldChanged;
            Apply();
            Progression.Changed += Apply;
        }

        void OnDestroy()
        {
            Progression.Changed -= Apply;
            if (folds != null) folds.FoldChanged -= OnFoldChanged;
        }

        bool started;
        void Start() => started = true;

        void OnFoldChanged(PlaneFold fold) => ApplyLoadout();

        /// <summary>Re-checks everything: which folds are open, which have propellers, and the current loadout.</summary>
        public void Apply()
        {
            if (player == null) return;
            if (folds != null)
            {
                folds.FoldAvailable = f => Owned(f.unlockId);
                folds.PropellerAvailable = f => Owned(UnlockIds.ForFold(f, UnlockIds.Propeller));
                if (player.State != PlaneState.Flying) folds.Refresh();   // re-fold (and re-fit the prop) only while in hand
            }
            ApplyLoadout();
        }

        /// <summary>Upgrades of the fold you're holding.</summary>
        void ApplyLoadout()
        {
            if (player == null) return;
            PlaneFold fold = folds != null ? folds.Current : null;
            bool Has(string upgrade) => Owned(UnlockIds.ForFold(fold, upgrade));
            bool inHand = player.State != PlaneState.Flying;

            var pencils = player.GetComponent<PencilLauncher>();
            if (pencils != null)
                pencils.SetSlotLimit(pencilsBanned || !Has(UnlockIds.Pencils) ? 0
                                     : Has(UnlockIds.PencilRack) ? pencilsWithRack : pencilsWithMissiles);

            var gun = player.GetComponent<StapleGun>();
            if (gun != null)
            {
                gun.ClipBonus = Has(UnlockIds.StapleMagazine) ? magazineBonusStaples : 0;
                bool hasGun = settings == null || !settings.staplesAreAnUnlock || Owned(UnlockIds.Staples);
                gun.enabled = hasGun;
                var shooter = player.GetComponent<PlayerShooter>();
                if (shooter != null) shooter.enabled = hasGun;
            }

            var paper = player.GetComponent<PaperDamage>();
            if (paper != null)
            {
                paper.IntegrityScale = Has(UnlockIds.HeavyPaper) ? heavyPaperScale : 1f;
                if (inHand && started) paper.Repair();   // fresh sheet at the new toughness (not before everything has woken up)
            }
        }
    }
}
