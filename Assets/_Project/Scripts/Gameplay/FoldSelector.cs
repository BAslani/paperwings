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

        const string PrefKey = "PaperWings.SelectedFold";
        int index;

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
            if (Count == 0) return;
            index = Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, 0), 0, Count - 1);
            Apply();
        }

        void Update()
        {
            if (Count < 2 || input == null || plane.State != PlaneState.Held) return;
            if (input.FoldNextPressed) Cycle(+1);
            else if (input.FoldPrevPressed) Cycle(-1);
        }

        public void Cycle(int direction)
        {
            index = (index + direction + Count) % Count;
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
            if (damage != null) damage.Configure(fold.paper, fold.mesh, fold.paperColor);
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
