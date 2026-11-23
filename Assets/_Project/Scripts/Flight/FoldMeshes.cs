using System.Collections.Generic;
using UnityEngine;

namespace PaperWings.Flight
{
    /// <summary>
    /// Low-poly procedural meshes for each paper fold. Nose points to +Z, origin near the middle.
    /// Every face is double-sided and flat-shaded, like folded paper (see DartMesh).
    ///
    /// Stage 29 made them a little fantastical, while keeping each fold's size and flying shape:
    ///   Dart   "Arrowhead"  swallow-cut wings, twin tail fins, a folded canopy, drooped wingtips
    ///   Glider "Crane"      an origami crane: long feathered wings, a neck and beak, a tail fan
    ///   Stunt  "Manta"      a manta ray: curved wings, head fins, flipped-up elevons, a whip tail
    ///   Canard "Dragon"     bat wings with finger bones, horns, canards, dorsal spines, a forked tail
    ///   Rival  "Raven"      Accounting: forward-swept, ragged black-feather wings and a hooked beak
    ///   Wingman "Swallow"   Marketing: slim swept wings and long forked tail streamers
    /// </summary>
    public static class FoldMeshes
    {
        /// <summary>Glider "Crane": broad feathered wings with a long chord, ~1.7 long, ~2.7 wide.</summary>
        public static Mesh BuildGlider()
        {
            var b = new Builder();
            b.Mirror((s, m) =>
            {
                // Feathered wing: leading edge out to the tip, then a zig-zag of feathers back to the root.
                var loop = new List<Vector3>
                {
                    V(0.08f, 0.02f, 0.30f, s), V(0.70f, 0.12f, 0.12f, s), V(1.35f, 0.22f, -0.28f, s)
                };
                for (int k = 0; k < 9; k++)
                {
                    float t = k / 8f;
                    float x = Mathf.Lerp(1.30f, 0.16f, t);
                    float z = (k % 2 == 0) ? Mathf.Lerp(-0.72f, -0.62f, t) : Mathf.Lerp(-0.48f, -0.42f, t);
                    loop.Add(V(x, 0.02f + 0.15f * (x / 1.35f), z, s));
                }
                loop.Add(V(0.08f, 0.02f, -0.40f, s));
                m.Fan(V(0.62f, 0.11f, -0.22f, s), loop, true);

                // Tail fan, notched.
                m.Tri(new Vector3(0f, 0.02f, -0.40f), V(0.26f, 0.05f, -0.82f, s), V(0.12f, 0.03f, -0.74f, s));
                m.Tri(new Vector3(0f, 0.02f, -0.40f), V(0.12f, 0.03f, -0.74f, s), V(0.07f, 0.03f, -0.92f, s));
                m.Tri(new Vector3(0f, 0.02f, -0.40f), V(0.07f, 0.03f, -0.92f, s), new Vector3(0f, 0.02f, -0.80f));
            });

            // Body: a thin diamond down the middle.
            b.Tri(new Vector3(0f, 0f, 0.40f), new Vector3(0.09f, 0.02f, 0.05f), new Vector3(0f, 0.02f, -0.45f));
            b.Tri(new Vector3(0f, 0f, 0.40f), new Vector3(0f, 0.02f, -0.45f), new Vector3(-0.09f, 0.02f, 0.05f));
            // Neck rising forward, head and beak (a thin folded fin).
            b.Tri(new Vector3(0f, 0f, 0.30f), new Vector3(0f, 0.24f, 0.88f), new Vector3(0f, 0.02f, 0.62f));
            b.Tri(new Vector3(0f, 0.24f, 0.88f), new Vector3(0f, 0.19f, 1.08f), new Vector3(0f, 0.17f, 0.86f));
            // Shallow keel.
            b.Tri(new Vector3(0f, 0f, 0.35f), new Vector3(0f, 0f, -0.55f), new Vector3(0f, -0.13f, -0.35f));
            return b.Build("PaperGlider");
        }

        /// <summary>Stunt "Manta": a compact curved delta with elevons folded up. ~1.7 long, ~1.4 wide.</summary>
        public static Mesh BuildStunt()
        {
            var b = new Builder();
            Vector3 nose = new Vector3(0f, 0f, 0.85f), spine = new Vector3(0f, 0.01f, -0.50f);
            b.Mirror((s, m) =>
            {
                // Curved leading edge in three segments, back to the spine.
                var loop = new List<Vector3>
                {
                    nose, V(0.24f, 0.02f, 0.55f, s), V(0.50f, 0.06f, 0.12f, s), V(0.70f, 0.10f, -0.36f, s),
                    V(0.46f, 0.06f, -0.46f, s), V(0.14f, 0.02f, -0.50f, s), spine
                };
                m.Fan(V(0.26f, 0.04f, 0.0f, s), loop, false);
                // Elevon flaps along the trailing edge, bent up.
                Vector3 up = new Vector3(0f, 0.15f, -0.20f);
                m.Quad(V(0.14f, 0.02f, -0.50f, s), V(0.46f, 0.06f, -0.46f, s), V(0.46f, 0.06f, -0.46f, s) + up, V(0.14f, 0.02f, -0.50f, s) + up);
                // Cephalic head fins curling forward.
                m.Tri(V(0.08f, 0.01f, 0.72f, s), V(0.19f, 0.04f, 0.98f, s), V(0.17f, 0.03f, 0.68f, s));
                // Little dorsal fold over the back.
                m.Tri(new Vector3(0f, 0.02f, 0.30f), V(0.06f, 0.11f, -0.05f, s), new Vector3(0f, 0.02f, -0.35f));
            });
            // Whip tail.
            b.Tri(new Vector3(-0.03f, 0.01f, -0.48f), new Vector3(0.03f, 0.01f, -0.48f), new Vector3(0f, 0.04f, -1.05f));
            // Deep keel: a strong grip for snappy turns.
            b.Tri(nose, spine, new Vector3(0f, -0.30f, -0.38f));
            return b.Build("PaperStunt");
        }

        /// <summary>
        /// Canard "Dragon": bat wings set back, a small extra wing (the canard) near the nose, horns,
        /// spines and a forked tail. The canard is what gives it extra lift and a crisper nose. ~2.1 long, ~2 wide.
        /// </summary>
        public static Mesh BuildCanard()
        {
            var b = new Builder();
            Vector3 nose = new Vector3(0f, 0f, 1.0f), tail = new Vector3(0f, 0f, -0.88f);
            b.Mirror((s, m) =>
            {
                // Narrow body strip from snout to tail.
                Vector3 rootFront = V(0.10f, 0f, 0.15f, s), rootBack = V(0.14f, 0f, -0.85f, s);
                m.Tri(nose, rootFront, tail);
                m.Tri(rootFront, rootBack, tail);

                // Bat wing: leading edge to the wrist and tip, then scalloped membrane between finger bones.
                var loop = new List<Vector3>
                {
                    rootFront, V(0.58f, 0.17f, -0.02f, s), V(0.98f, 0.15f, -0.36f, s),
                    V(0.80f, 0.10f, -0.50f, s), V(0.66f, 0.10f, -0.78f, s), V(0.50f, 0.07f, -0.58f, s),
                    V(0.34f, 0.05f, -0.86f, s), V(0.22f, 0.03f, -0.66f, s), rootBack
                };
                m.Fan(V(0.42f, 0.09f, -0.30f, s), loop, false);
                // Finger bones (thin raised folds over the membrane).
                m.Tri(V(0.58f, 0.17f, -0.02f, s), V(0.66f, 0.13f, -0.78f, s), V(0.60f, 0.18f, -0.10f, s));
                m.Tri(V(0.58f, 0.17f, -0.02f, s), V(0.34f, 0.08f, -0.86f, s), V(0.54f, 0.18f, -0.08f, s));

                // The canard: a small delta near the snout.
                m.Tri(V(0.06f, 0.02f, 0.76f, s), V(0.38f, 0.07f, 0.50f, s), V(0.08f, 0.02f, 0.45f, s));
                // Horns sweeping back from the head.
                m.Tri(V(0.05f, 0.03f, 0.88f, s), V(0.13f, 0.22f, 0.66f, s), V(0.04f, 0.05f, 0.76f, s));
                // Forked tail.
                m.Tri(new Vector3(0f, 0.01f, -0.80f), V(0.21f, 0.04f, -1.16f, s), new Vector3(0f, 0.01f, -1.00f));
            });
            // Dorsal spines down the back.
            foreach (float z in new[] { 0.35f, 0.0f, -0.35f, -0.65f })
                b.Tri(new Vector3(0f, 0.01f, z + 0.12f), new Vector3(0f, 0.16f, z - 0.04f), new Vector3(0f, 0.01f, z - 0.12f));
            // Keel underneath for grip.
            b.Tri(nose, tail, new Vector3(0f, -0.22f, -0.6f));
            return b.Build("PaperCanard");
        }

        /// <summary>Accounting's "Raven": forward-swept ragged wings, a hooked beak, a wedge tail. ~2 long, ~1.7 wide.</summary>
        public static Mesh BuildRaven()
        {
            var b = new Builder();
            Vector3 nose = new Vector3(0f, 0f, 0.95f), spine = new Vector3(0f, 0.01f, -0.70f);
            b.Mirror((s, m) =>
            {
                // Body diamond.
                m.Tri(nose, V(0.11f, 0.02f, 0.18f, s), spine);
                // Ragged wing: forward-swept leading edge, primary feathers along the back.
                var loop = new List<Vector3>
                {
                    V(0.11f, 0.02f, 0.18f, s), V(0.84f, 0.15f, 0.16f, s), V(0.72f, 0.12f, -0.26f, s),
                    V(0.62f, 0.11f, -0.04f, s), V(0.52f, 0.09f, -0.38f, s), V(0.40f, 0.08f, -0.14f, s),
                    V(0.29f, 0.05f, -0.48f, s), V(0.11f, 0.02f, -0.40f, s)
                };
                m.Fan(V(0.42f, 0.09f, -0.04f, s), loop, false);
                m.Tri(V(0.11f, 0.02f, 0.18f, s), V(0.11f, 0.02f, -0.40f, s), spine);
                // Wedge tail.
                m.Tri(new Vector3(0f, 0.01f, -0.55f), V(0.26f, 0.04f, -1.00f, s), new Vector3(0f, 0.02f, -0.93f));
                // Brow ridges over the head.
                m.Tri(V(0.03f, 0.02f, 0.80f, s), V(0.10f, 0.10f, 0.55f, s), V(0.05f, 0.03f, 0.50f, s));
            });
            // Hooked beak.
            b.Tri(new Vector3(0f, 0.02f, 0.75f), new Vector3(0f, -0.10f, 1.05f), new Vector3(0f, 0.05f, 0.92f));
            // Keel.
            b.Tri(nose, spine, new Vector3(0f, -0.22f, -0.55f));
            return b.Build("PaperRaven");
        }

        /// <summary>Marketing's "Swallow": slim swept wings and long forked tail streamers. ~2.2 long, ~1.7 wide.</summary>
        public static Mesh BuildSwallow()
        {
            var b = new Builder();
            Vector3 nose = new Vector3(0f, 0f, 1.0f), spine = new Vector3(0f, 0.01f, -0.45f);
            b.Mirror((s, m) =>
            {
                m.Tri(nose, V(0.09f, 0.02f, 0.25f, s), spine);
                // Swept crescent wing.
                var loop = new List<Vector3>
                {
                    V(0.09f, 0.02f, 0.25f, s), V(0.45f, 0.08f, 0.02f, s), V(0.86f, 0.12f, -0.58f, s),
                    V(0.60f, 0.08f, -0.44f, s), V(0.30f, 0.04f, -0.30f, s), V(0.11f, 0.02f, -0.22f, s)
                };
                m.Fan(V(0.36f, 0.06f, -0.14f, s), loop, false);
                m.Tri(V(0.09f, 0.02f, 0.25f, s), V(0.11f, 0.02f, -0.22f, s), spine);
                // Tail streamer.
                m.Tri(V(0.05f, 0.01f, -0.30f, s), V(0.33f, 0.04f, -1.20f, s), V(0.10f, 0.01f, -0.55f, s));
            });
            b.Tri(nose, spine, new Vector3(0f, -0.20f, -0.30f));
            return b.Build("PaperSwallow");
        }

        /// <summary>A point on one side: s = +1 right, -1 left (x mirrored).</summary>
        static Vector3 V(float x, float y, float z, int s) => new Vector3(x * s, y, z);

        /// <summary>Collects triangles; Mirror runs a recipe for the right side and the left side.</summary>
        internal class Builder
        {
            readonly List<Vector3> verts = new List<Vector3>();
            readonly List<Vector3> normals = new List<Vector3>();
            readonly List<int> tris = new List<int>();

            public void Tri(Vector3 a, Vector3 b, Vector3 c) => DartMesh.AddDoubleSided(verts, normals, tris, a, b, c);

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d) { Tri(a, b, c); Tri(a, c, d); }

            /// <summary>Triangles from 'centre' to each edge of the outline, closing it (last point back to the first).</summary>
            public void Fan(Vector3 centre, List<Vector3> outline, bool unusedClosedFlag = true)
            {
                for (int i = 0; i + 1 < outline.Count; i++) Tri(centre, outline[i], outline[i + 1]);
                if (outline.Count > 2) Tri(centre, outline[outline.Count - 1], outline[0]);
            }

            public void Mirror(System.Action<int, Builder> side)
            {
                side(1, this);
                side(-1, this);
            }

            public Mesh Build(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(verts);
                mesh.SetNormals(normals);
                mesh.SetTriangles(tris, 0);
                DartMesh.ApplyPaperUVs(mesh);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
