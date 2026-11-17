using System.Collections.Generic;
using UnityEngine;

namespace PaperWings.Flight
{
    /// <summary>
    /// Builds a low-poly paper dart mesh in code: two flat delta wings with a slight
    /// upward dihedral, and a folded keel underneath. About 2 units long, nose at +Z.
    /// Every face is double-sided (paper has two sides!) and flat-shaded.
    /// </summary>
    public static class DartMesh
    {
        public static Mesh Build()
        {
            // Key points (local space, nose points toward +Z).
            Vector3 nose      = new Vector3(0f,     0f,    1.0f);
            Vector3 tailSpine = new Vector3(0f,     0f,   -1.0f);
            Vector3 leftTip   = new Vector3(-0.7f,  0.1f, -1.0f);   // y > 0 = dihedral
            Vector3 rightTip  = new Vector3( 0.7f,  0.1f, -1.0f);
            Vector3 keelLow   = new Vector3(0f,    -0.22f, -0.85f);

            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();

            AddDoubleSided(verts, normals, tris, nose, leftTip, tailSpine);   // left wing
            AddDoubleSided(verts, normals, tris, nose, tailSpine, rightTip);  // right wing
            AddDoubleSided(verts, normals, tris, nose, tailSpine, keelLow);   // keel (single fold)

            var mesh = new Mesh { name = "PaperDart" };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // Adds a triangle visible from both sides, with its own vertices for flat shading.
        static void AddDoubleSided(List<Vector3> v, List<Vector3> n, List<int> t, Vector3 a, Vector3 b, Vector3 c)
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
