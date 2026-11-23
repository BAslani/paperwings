using UnityEngine;
using PaperWings.Combat;
using PaperWings.Flight;

namespace PaperWings.Gameplay
{
    /// <summary>
    /// Turns saved unlocks into gameplay on the player's plane, now and whenever progress changes:
    ///   Pencil Missiles / Pencil Rack -> how many wing slots hold pencils (0 / 2 / 4)
    ///   Staple Magazine               -> extra staples
    ///   Heavy Paper                   -> tougher paper
    ///   Glider / Stunt / Canard folds -> which folds the picker offers
    ///   Pusher Propeller              -> the Canard's prop (and its boost lift)
    ///   Staple Gun                    -> only if the settings make staples an unlock
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

        bool pencilsBanned;
        /// <summary>Set by campaign nights that take your pencils away. Takes effect next time the plane is in hand.</summary>
        public bool PencilsBanned
        {
            get => pencilsBanned;
            set { if (pencilsBanned == value) return; pencilsBanned = value; Apply(); }
        }

        // Awake (not Start) so the fold picker and pencil launcher see the limits before they set up.
        void Awake()
        {
            Apply();
            Progression.Changed += Apply;
        }

        void OnDestroy() => Progression.Changed -= Apply;

        public void Apply()
        {
            if (player == null) return;

            var pencils = player.GetComponent<PencilLauncher>();
            if (pencils != null)
            {
                int n = pencilsBanned || !Progression.IsUnlocked(UnlockIds.Pencils) ? 0
                      : Progression.IsUnlocked(UnlockIds.PencilRack) ? pencilsWithRack : pencilsWithMissiles;
                pencils.SetSlotLimit(n);
            }

            var gun = player.GetComponent<StapleGun>();
            if (gun != null)
            {
                gun.ClipBonus = Progression.IsUnlocked(UnlockIds.StapleMagazine) ? magazineBonusStaples : 0;
                bool hasGun = settings == null || !settings.staplesAreAnUnlock || Progression.IsUnlocked(UnlockIds.Staples);
                gun.enabled = hasGun;
                var shooter = player.GetComponent<PlayerShooter>();
                if (shooter != null) shooter.enabled = hasGun;
            }

            var paper = player.GetComponent<PaperDamage>();
            if (paper != null) paper.IntegrityScale = Progression.IsUnlocked(UnlockIds.HeavyPaper) ? heavyPaperScale : 1f;

            if (folds != null)
            {
                folds.FoldAvailable = f => Progression.IsUnlocked(f.unlockId);
                folds.AttachmentAvailable = f => Progression.IsUnlocked(f.attachmentUnlockId);
                if (player.State != PlaneState.Flying) folds.Refresh();   // only re-fold while in hand
            }
        }
    }
}
