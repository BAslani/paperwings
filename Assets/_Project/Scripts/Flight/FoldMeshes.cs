using System.Collections.Generic;
using UnityEngine;

namespace PaperWings.Flight
{
    /// <summary>
    /// Low-poly procedural meshes for each paper fold. Nose points to +Z, origin near the middle.
    /// Every face is double-sided and flat-shaded, like the dart (see DartMesh).
    /// </summary>
    public static class FoldMeshes
    {
        /// <summary>
        /// Glider: broad wings with a long chord and little upturned winglets. ~1.6 long, ~2.6 wide.
        /// </summary>
        public static Mesh BuildGlider()
        {
            var b = new Builder();
            Vector3 nose = new Vector3(0f, 0f, 0.8f);
            Vector3 tail = new Vector3(0f, 0f, -0.8f);

            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 tipFront = new Vector3(1.3f * side, 0.12f, -0.15f);
                Vector3 tipBack  = new Vector3(1.3f * side, 0.12f, -0.65f);
                Vector3 rootBack = new Vector3(0.25f * side, 0.02f, -0.8f);
                b.Tri(nose, tipFront, tipBack);
                b.Tri(nose, tipBack, rootBack);
                b.Tri(nose, rootBack, tail);

                // Winglet: a small fin folded up at the tip.
                Vector3 wingletTop = new Vector3(1.32f * side, 0.38f, -0.6f);
                b.Tri(tipFront, wingletTop, tipBack);
            }

            // Shallow keel to hold.
            b.Tri(nose, tail, new Vector3(0f, -0.14f, -0.6f));
            return b.Build("PaperGlider");
        }

        /// <summary>
        /// Stunt: a compact delta with elevon flaps folded up at the back. ~1.6 long, ~1.3 wide.
        /// </summary>
        public static Mesh BuildStunt()
        {
            var b = new Builder();
            Vector3 nose = new Vector3(0f, 0f, 0.8f);
            Vector3 tail = new Vector3(0f, 0f, -0.55f);

            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 tip = new Vector3(0.62f * side, 0.04f, -0.55f);
                b.Tri(nose, tip, tail);

                // Elevon flap: a strip along the trailing edge, bent upward.
                Vector3 inner = new Vector3(0.12f * side, 0.01f, -0.55f);
                Vector3 innerUp = new Vector3(0.12f * side, 0.14f, -0.78f);
                Vector3 tipUp = new Vector3(0.62f * side, 0.17f, -0.78f);
                b.Tri(inner, tip, tipUp);
                b.Tri(inner, tipUp, innerUp);
            }

            // Deep keel: a strong grip for snappy turns.
            b.Tri(nose, tail, new Vector3(0f, -0.3f, -0.45f));
            return b.Build("PaperStunt");
        }

        class Builder
        {
            readonly List<Vector3> verts = new List<Vector3>();
            readonly List<Vector3> normals = new List<Vector3>();
            readonly List<int> tris = new List<int>();

            public void Tri(Vector3 a, Vector3 b, Vector3 c) => DartMesh.AddDoubleSided(verts, normals, tris, a, b, c);

            public Mesh Build(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(verts);
                mesh.SetNormals(normals);
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
