using System.Collections.Generic;
using UnityEngine;

namespace PaperWings.Flight
{
    /// <summary>
    /// Builds the Dart ("Arrowhead", Stage 29) in code: swallow-cut delta wings with a slight upward
    /// dihedral and drooped tips, twin tail fins, a little folded canopy and a keel underneath.
    /// About 2 units long, nose at +Z. Every face is double-sided (paper has two sides!) and flat-shaded.
    /// </summary>
    public static class DartMesh
    {
        public static Mesh Build()
        {
            var b = new FoldMeshes.Builder();
            Vector3 nose = new Vector3(0f, 0f, 1.05f);
            Vector3 spine = new Vector3(0f, 0f, -0.92f);
            b.Mirror((s, m) =>
            {
                Vector3 tip = new Vector3(0.72f * s, 0.12f, -0.80f);
                Vector3 notch = new Vector3(0.44f * s, 0.08f, -0.60f);
                Vector3 trail = new Vector3(0.18f * s, 0.03f, -1.00f);
                // Wing with a swallow-cut trailing edge.
                m.Tri(nose, tip, notch);
                m.Tri(nose, notch, trail);
                m.Tri(nose, trail, spine);
                // Drooped wingtip fold.
                m.Tri(tip, new Vector3(0.74f * s, -0.10f, -0.84f), new Vector3(0.60f * s, 0.10f, -0.68f));
                // Twin tail fin.
                m.Tri(new Vector3(0.16f * s, 0.03f, -0.50f), new Vector3(0.17f * s, 0.03f, -0.95f), new Vector3(0.19f * s, 0.30f, -0.92f));
                // Leading-edge crease: a thin raised strake along the front of the wing.
                m.Tri(new Vector3(0.04f * s, 0.01f, 0.70f), new Vector3(0.40f * s, 0.10f, -0.20f), new Vector3(0.10f * s, 0.06f, 0.30f));
                // Canopy: a little folded pyramid near the nose.
                Vector3 apex = new Vector3(0f, 0.12f, 0.35f);
                m.Tri(apex, new Vector3(0.08f * s, 0.02f, 0.55f), new Vector3(0.10f * s, 0.02f, 0.15f));
                m.Tri(apex, new Vector3(0f, 0.01f, 0.62f), new Vector3(0.08f * s, 0.02f, 0.55f));
            });
            b.Tri(nose, spine, new Vector3(0f, -0.22f, -0.80f));   // keel (single fold)
            return b.Build("PaperDart");
        }

        /// <summary>
        /// Texture coordinates for paper textures: project the plane from above, like laying the
        /// unfolded sheet flat. u runs across the wings (centre crease at u = 0.5), v runs nose-to-tail.
        /// Every fold uses the same mapping, so one paper texture fits them all.
        /// UNITY NOTE: UVs say which part of a texture lands on each vertex (0..1 across the image).
        /// </summary>
        public static void ApplyPaperUVs(Mesh mesh)
        {
            var v = mesh.vertices;
            var uv = new Vector2[v.Length];
            for (int i = 0; i < v.Length; i++)
                uv[i] = new Vector2(v[i].x * 0.38f + 0.5f, v[i].z * 0.5f + 0.5f);
            mesh.uv = uv;
        }

        // Adds a triangle visible from both sides, with its own vertices for flat shading.
        public static void AddDoubleSided(List<Vector3> v, List<Vector3> n, List<int> t, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;

            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c);
            n.Add(normal); n.Add(normal); n.Add(normal);
            t.Add(i); t.Add(i + 1); t.Add(i + 2);

            // Back face: reversed winding, flipped normal.
            i = v.Count;
            v.Add(a); v.Add(c); v.Add(b);
            n.Add(-normal); n.Add(-normal); n.Add(-normal);
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
        }
    }
}
