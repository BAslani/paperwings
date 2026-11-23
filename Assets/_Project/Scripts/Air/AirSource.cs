using UnityEngine;

namespace PaperWings.Air
{
    /// <summary>
    /// Base class for anything that moves air. Subclasses answer one question:
    /// "What is the wind velocity at this point, and how strongly am I affecting it (0..1)?"
    ///
    /// The base class handles registering with the AirField and draws wind arrows
    /// in the Scene view when the object is selected.
    /// </summary>
    public abstract class AirSource : MonoBehaviour
    {
        public abstract AirKind Kind { get; }

        /// <summary>How wet this air is, 0 (dry) to 1 (steam). Wet air makes paper soggy.</summary>
        public virtual float Moisture => 0f;

        /// <summary>
        /// The kind of air at one point. Most sources are one kind everywhere; a ceiling fan pushes
        /// down under its blades and lifts at their edge, so it answers per point (Stage 24).
        /// </summary>
        public virtual AirKind KindAt(Vector3 worldPosition) => Kind;

        /// <summary>True if this air is warm and dry (dries soggy paper). Default: dry thermals.</summary>
        public virtual bool IsHeat => Kind == AirKind.Thermal && Moisture < 0.5f;

        /// <summary>
        /// True if an AI pilot can circle around this source's position to climb (a round column of lift).
        /// Radiators (a strip along a wall) and fans don't qualify, so rivals don't circle into walls.
        /// </summary>
        public virtual bool OffersLiftCenter => Kind == AirKind.Thermal;

        /// <summary>How this air should look (its colour in the air palette).</summary>
        public virtual AirLook Look => Kind switch
        {
            AirKind.Thermal => Moisture >= 0.5f ? AirLook.Steam : (IsHeat ? AirLook.Heat : AirLook.Updraft),
            AirKind.Downdraft => AirLook.Downdraft,
            _ => AirLook.Draft
        };

        /// <summary>Wind velocity at a world position. influence = 0 means "outside my zone".</summary>
        public abstract Vector3 GetWind(Vector3 worldPosition, out float influence);

        /// <summary>World-space box that contains the whole zone (used for gizmo sampling).</summary>
        public abstract Bounds GetBounds();

        protected virtual void OnEnable() => AirField.Register(this);
        protected virtual void OnDisable() => AirField.Unregister(this);

        protected Color GizmoColor => AirPalette.Of(Look);

        /// <summary>
        /// When selected: sample a 3D grid inside the bounds and draw an arrow for the wind at each point.
        /// Works automatically for any new AirSource subclass.
        /// </summary>
        protected virtual void OnDrawGizmosSelected()
        {
            Bounds b = GetBounds();
            const int steps = 7;
            Vector3 step = b.size / (steps - 1);
            Color c = GizmoColor;

            for (int x = 0; x < steps; x++)
            for (int y = 0; y < steps; y++)
            for (int z = 0; z < steps; z++)
            {
                Vector3 p = b.min + Vector3.Scale(step, new Vector3(x, y, z));
                Vector3 wind = GetWind(p, out float influence);
                if (influence <= 0.01f) continue;
                Gizmos.color = new Color(c.r, c.g, c.b, Mathf.Lerp(0.25f, 1f, influence));
                AirGizmos.DrawArrow(p, wind * 0.25f);
            }
        }
    }

    /// <summary>Gizmo drawing helpers (Unity's Gizmos API has no circles or arrows built in).</summary>
    public static class AirGizmos
    {
        public static void DrawCircle(Vector3 center, Vector3 normal, float radius, int segments = 32)
        {
            Quaternion rot = Quaternion.LookRotation(normal);
            Vector3 prev = center + rot * new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                Vector3 next = center + rot * new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }

        public static void DrawArrow(Vector3 from, Vector3 vector)
        {
            if (vector.sqrMagnitude < 0.0001f) return;
            Vector3 to = from + vector;
            Gizmos.DrawLine(from, to);
            Vector3 dir = vector.normalized;
            Vector3 side = Vector3.Cross(dir, Mathf.Abs(dir.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
            float head = Mathf.Min(0.6f, vector.magnitude * 0.3f);
            Gizmos.DrawLine(to, to - dir * head + side * head * 0.5f);
            Gizmos.DrawLine(to, to - dir * head - side * head * 0.5f);
        }
    }
}
