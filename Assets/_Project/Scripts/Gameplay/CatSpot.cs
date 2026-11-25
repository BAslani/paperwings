using UnityEngine;

namespace PaperWings.Gameplay
{
    /// <summary>
    /// A place Biscuit the cat knows in her room (Stage 45): a spot on the floor, or a perch she jumps
    /// up to (a chair, the counter, the windowsill). Spots are joined by links; a link between two
    /// spots at different heights is a jump. Her brain finds routes over these links, so the builder
    /// only has to place spots with clear straight lines between linked ones.
    ///
    /// The spot's position is where her feet stand (on the surface), and its forward is the way she
    /// faces when she settles there.
    /// </summary>
    public class CatSpot : MonoBehaviour
    {
        public enum Kind { Floor, Perch }

        public Kind kind = Kind.Floor;
        [Tooltip("She naps here when she's tired (the warm windowsill is her favourite).")]
        public bool napSpot;
        [Tooltip("How much she likes it, for naps and for wandering (higher = more often).")]
        [Range(0f, 3f)] public float liking = 1f;
        [Tooltip("Spots she can walk or jump to from here. Links work both ways.")]
        public CatSpot[] links = new CatSpot[0];

        public Vector3 Position => transform.position;
        public Vector3 Facing => Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        public bool IsHigh => kind == Kind.Perch;

        void OnDrawGizmos()
        {
            Gizmos.color = napSpot ? new Color(1f, 0.6f, 0.2f) : kind == Kind.Perch ? new Color(0.4f, 0.8f, 1f) : new Color(0.6f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up, 3f);
            Gizmos.DrawLine(transform.position + Vector3.up, transform.position + Vector3.up + Facing * 6f);
            if (links == null) return;
            Gizmos.color = new Color(1f, 1f, 1f, 0.35f);
            foreach (var l in links) if (l != null) Gizmos.DrawLine(transform.position + Vector3.up, l.transform.position + Vector3.up);
        }
    }
}
