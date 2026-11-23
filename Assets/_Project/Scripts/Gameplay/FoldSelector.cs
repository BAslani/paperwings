using System;
using UnityEngine;
using PaperWings.Flight;

namespace PaperWings.Gameplay
{
    /// <summary>
    /// Pick a paper fold before throwing. While the plane is in hand, FoldPrev / FoldNext
    /// (Q / E, LB / RB) cycle through the list; the plane instantly takes on that fold's
    /// handling, paper, mesh and colour. The choice is remembered between sessions.
    ///
    /// To add a fold: create a PlaneFold asset and drag it into the Folds list here.
    /// </summary>
    public class FoldSelector : MonoBehaviour
    {
        [SerializeField] PlaneController plane;
        [SerializeField] PlaneInput input;
        [SerializeField] PlaneFold[] folds;

        public event Action<PlaneFold> FoldChanged;

        public PlaneFold Current => folds != null && folds.Length > 0 ? folds[index] : null;
        public int Index => index;
        public int Count => folds != null ? folds.Length : 0;

        /// <summary>Which folds may be picked (set by progression). Null = all of them.</summary>
        public Func<PlaneFold, bool> FoldAvailable { get; set; }
        /// <summary>Whether a fold's attachment is unlocked (set by progression). Null = always.</summary>
        public Func<PlaneFold, bool> AttachmentAvailable { get; set; }
        /// <summary>How many folds can currently be picked.</summary>
        public int AvailableCount
        {
            get { int n = 0; for (int i = 0; i < Count; i++) if (IsAvailable(i)) n++; return n; }
        }

        bool IsAvailable(int i) => folds[i] != null && (FoldAvailable == null || FoldAvailable(folds[i]));

        const string PrefKey = "PaperWings.SelectedFold";
        int index;
        GameObject currentAttachment;   // spawned part for the current fold (if any)
        bool started;

        void Start()
        {
            // Ignore empty slots in the list (and say so, instead of failing silently).
            if (folds != null)
            {
                int before = folds.Length;
                folds = System.Array.FindAll(folds, f => f != null);
                if (folds.Length < before)
                    Debug.LogWarning($"FoldSelector: {before - folds.Length} empty fold slot(s) ignored. " +
                                     "Re-run PaperWings > Build Stage 7, or drag fold assets into the list.", this);
            }
            started = true;
            if (Count == 0) return;
            index = Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, 0), 0, Count - 1);
            if (!IsAvailable(index)) index = FirstAvailable();
            Apply();
        }

        int FirstAvailable()
        {
            for (int i = 0; i < Count; i++) if (IsAvailable(i)) return i;
            return 0;
        }

        /// <summary>
        /// Re-applies the current fold (e.g. after an unlock or a progress reset): falls back to the
        /// first available fold if the current one is locked, and refreshes its attachment.
        /// </summary>
        public void Refresh()
        {
            if (!started || Count == 0) return;   // not set up yet: Start will apply the right fold
            if (!IsAvailable(index)) index = FirstAvailable();
            Apply();
        }

        void Update()
        {
            if (AvailableCount < 2 || input == null || plane.State != PlaneState.Held) return;
            if (input.FoldNextPressed) Cycle(+1);
            else if (input.FoldPrevPressed) Cycle(-1);
        }

        public void Cycle(int direction)
        {
            // Step to the next fold that's unlocked, skipping locked ones.
            for (int step = 0; step < Count; step++)
            {
                index = (index + direction + Count) % Count;
                if (IsAvailable(index)) break;
            }
            PlayerPrefs.SetInt(PrefKey, index);
            Apply();
            // A little flutter so the swap feels physical.
            var wobble = plane.GetComponent<PaperWobble>();
            if (wobble != null) wobble.Kick(5f);
        }

        void Apply()
        {
            PlaneFold fold = Current;
            if (fold == null || !plane.SetSettings(fold.flight)) return;

            Transform visual = plane.transform.Find("Visual");
            var damage = plane.GetComponent<PaperDamage>();
            if (damage != null) damage.Configure(fold.paper, fold.mesh, fold.paperColor, fold.paperTexture);
            else if (visual != null && fold.mesh != null) visual.GetComponent<MeshFilter>().sharedMesh = fold.mesh;

            if (fold.mesh != null)
            {
                Bounds b = fold.mesh.bounds;

                // Fit the collider to the new shape (flat box, like the original).
                var box = plane.GetComponent<BoxCollider>();
                if (box != null)
                {
                    box.center = new Vector3(b.center.x, -0.05f, b.center.z);
                    box.size = new Vector3(b.size.x, 0.3f, b.size.z);
                }

                // Move the wing trails to the new wingtips.
                var trails = plane.GetComponent<PlaneTrails>();
                if (trails != null)
                {
                    FindWingtips(fold.mesh, out Vector3 left, out Vector3 right);
                    trails.SetTipPositions(left, right);
                }
            }

            // Swap the fold's optional attachment (e.g. the Canard's propeller).
            // Locked attachments don't appear, and don't give their bonus (the propeller's boost lift).
            if (currentAttachment != null) Destroy(currentAttachment);
            currentAttachment = null;
            bool attachmentUnlocked = AttachmentAvailable == null || AttachmentAvailable(fold);
            if (fold.attachment != null && attachmentUnlocked)
                currentAttachment = Instantiate(fold.attachment, visual != null ? visual : plane.transform, false);
            plane.BoostLiftScale = fold.attachment == null || attachmentUnlocked ? 1f : 0f;

            FoldChanged?.Invoke(fold);
        }

        /// <summary>Wingtips = the outermost vertices on each side (rearmost if there's a tie).</summary>
        static void FindWingtips(Mesh mesh, out Vector3 left, out Vector3 right)
        {
            left = right = Vector3.zero;
            foreach (var v in mesh.vertices)
            {
                if (v.x < left.x - 0.01f || (Mathf.Abs(v.x - left.x) <= 0.01f && v.z < left.z)) left = v;
                if (v.x > right.x + 0.01f || (Mathf.Abs(v.x - right.x) <= 0.01f && v.z < right.z)) right = v;
            }
        }
    }
}
