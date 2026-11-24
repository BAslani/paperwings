using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// Tools for the stylized "animated film" look pass (Stage 38 onwards): soft rounded boxes instead
    /// of sharp cubes, per-room palette swaps, and generated textures (rug, light cookies).
    /// Written so every room can reuse it: point it at a room's roots and a palette.
    /// </summary>
    internal static class StageLook
    {
        // ------------------------------------------------------------------ rounded boxes

        /// <summary>
        /// A box of the given size with rounded edges and corners of the given radius.
        /// UNITY NOTE: a sharp cube catches no light on its edges, which is what makes primitives read
        /// as "programmer art". A rounded edge gets a thin highlight, like a real (or Pixar) object.
        ///
        /// How it's made: each face is a grid whose lines bunch up near the edges; every grid point is
        /// pushed onto the surface of a rounded box (closest point on the inner box + radius along the
        /// direction to it). Neighbouring faces meet with matching positions and normals, so it's smooth.
        /// </summary>
        public static Mesh RoundedBox(Vector3 size, float radius, int segments = 3)
        {
            Vector3 h = size * 0.5f;
            float r = Mathf.Min(radius, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * 0.98f);
            Vector3 c = h - Vector3.one * r;   // the inner (sharp) box

            var coords = new float[3][];
            for (int a = 0; a < 3; a++) coords[a] = Axis(c[a], r, segments);

            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            for (int n = 0; n < 3; n++)
                for (int s = -1; s <= 1; s += 2)
                {
                    int a = (n + 1) % 3, b = (n + 2) % 3;   // a x b = +n
                    float[] ca = coords[a], cb = coords[b];
                    int start = verts.Count, w = ca.Length;
                    var faceNormal = Vector3.zero;
                    faceNormal[n] = s;

                    for (int j = 0; j < cb.Length; j++)
                        for (int i = 0; i < w; i++)
                        {
                            var p = Vector3.zero;
                            p[n] = s * h[n];
                            p[a] = ca[i];
                            p[b] = cb[j];
                            var inner = new Vector3(Mathf.Clamp(p.x, -c.x, c.x), Mathf.Clamp(p.y, -c.y, c.y), Mathf.Clamp(p.z, -c.z, c.z));
                            var d = p - inner;
                            var nrm = d.sqrMagnitude > 1e-12f ? d.normalized : faceNormal;
                            verts.Add(inner + nrm * r);
                            normals.Add(nrm);
                            uvs.Add(new Vector2(p[a], p[b]) * 0.1f);   // ~one texture tile per 10 units, any box size
                        }

                    for (int j = 0; j < cb.Length - 1; j++)
                        for (int i = 0; i < w - 1; i++)
                        {
                            int v00 = start + j * w + i, v10 = v00 + 1, v01 = v00 + w, v11 = v01 + 1;
                            // Unity draws a triangle's front when cross(v1 - v0, v2 - v0) points at the viewer.
                            if (s > 0) tris.AddRange(new[] { v00, v10, v11, v00, v11, v01 });
                            else tris.AddRange(new[] { v00, v11, v10, v00, v01, v11 });
                        }
                }

            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        /// <summary>Grid lines along one axis: dense around the two rounded edges, one big span between.</summary>
        static float[] Axis(float c, float r, int seg)
        {
            var list = new List<float>();
            for (int k = 0; k <= seg; k++) list.Add(-c - r * Mathf.Cos(k * Mathf.PI * 0.5f / seg));
            for (int k = c > 1e-4f ? 0 : 1; k <= seg; k++) list.Add(c + r * Mathf.Sin(k * Mathf.PI * 0.5f / seg));
            return list.ToArray();
        }

        /// <summary>
        /// The same rounded box squeezed into a 1 x 1 x 1 cube, so it drops in for Unity's Cube mesh
        /// WITHOUT touching the object's scale or collider. Its bevel is pre-stretched so that, once
        /// the object's scale is applied, the edges come out evenly round.
        /// </summary>
        public static Mesh UnitRoundedBox(Vector3 worldSize, float radius)
        {
            var mesh = RoundedBox(worldSize, radius);
            var v = mesh.vertices;
            var n = mesh.normals;
            for (int i = 0; i < v.Length; i++)
            {
                v[i] = new Vector3(v[i].x / worldSize.x, v[i].y / worldSize.y, v[i].z / worldSize.z);
                // Normals go through the inverse-transpose of the scale: undo it here.
                n[i] = Vector3.Scale(n[i], worldSize).normalized;
            }
            mesh.vertices = v;
            mesh.normals = n;
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        /// <summary>One asset file holding many rounded-box meshes, looked up by size and radius.</summary>
        public sealed class MeshStore
        {
            readonly string path;
            readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();

            public MeshStore(string assetPath)
            {
                path = assetPath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (o is Mesh m) meshes[m.name] = m;
            }

            public Mesh Get(Vector3 worldSize, float radius)
            {
                worldSize = new Vector3(R(worldSize.x), R(worldSize.y), R(worldSize.z));
                radius = R(radius);
                string key = string.Format(CultureInfo.InvariantCulture, "RBox_{0}x{1}x{2}_r{3}", worldSize.x, worldSize.y, worldSize.z, radius);
                if (meshes.TryGetValue(key, out var mesh) && mesh != null) return mesh;

                mesh = UnitRoundedBox(worldSize, radius);
                mesh.name = key;
                if (AssetDatabase.LoadMainAssetAtPath(path) == null) AssetDatabase.CreateAsset(mesh, path);
                else AssetDatabase.AddObjectToAsset(mesh, path);
                meshes[key] = mesh;
                return mesh;
            }

            static float R(float x) => Mathf.Round(x * 100f) / 100f;
        }

        /// <summary>
        /// Swaps Unity's sharp Cube mesh for a rounded one on every plain box under a root.
        /// Only objects made of rendering parts (mesh, renderer, collider) are touched; prefab instances
        /// and anything carrying a script are left alone. Scale and colliders don't change, so gameplay
        /// collisions are exactly as before. Safe to re-run (it recognises its own meshes).
        /// </summary>
        /// <param name="palettePrefix">Materials with this prefix tile their texture by size (fine on the
        /// new UVs). Any other textured material (a screen, a keyboard) keeps the Cube and its UVs.</param>
        public static int RoundCubes(Transform root, MeshStore store, string palettePrefix, float maxBevel = 1.6f, float bevelFraction = 0.3f)
        {
            if (root == null) return 0;
            int count = 0;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh;
                if (mesh == null) continue;
                bool builtInCube = mesh.name == "Cube" && AssetDatabase.GetAssetPath(mesh).StartsWith("Library/");
                bool ours = mesh.name.StartsWith("RBox_");
                if (!builtInCube && !ours) continue;
                if (PrefabUtility.IsPartOfPrefabInstance(mf)) continue;
                if (!OnlyRenderingParts(mf.gameObject)) continue;
                if (HasPictureTexture(mf.GetComponent<Renderer>(), palettePrefix)) continue;

                var s = mf.transform.lossyScale;
                var size = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
                float smallest = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
                if (smallest < 0.02f) continue;

                float radius = Mathf.Clamp(smallest * bevelFraction, 0.02f, maxBevel);
                mf.sharedMesh = store.Get(size, radius);
                count++;
            }
            return count;
        }

        static bool HasPictureTexture(Renderer r, string palettePrefix)
        {
            if (r == null) return false;
            foreach (var m in r.sharedMaterials)
                if (m != null && !m.name.StartsWith(palettePrefix) && m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null)
                    return true;
            return false;
        }

        static bool OnlyRenderingParts(GameObject go)
        {
            foreach (var c in go.GetComponents<Component>())
                if (!(c is Transform || c is MeshFilter || c is MeshRenderer || c is Collider)) return false;
            return true;
        }

        // ------------------------------------------------------------------ palette

        /// <summary>
        /// Replaces materials by name under a root (e.g. "M_OfficeCouch" -> "M_O_Teal"). The old shared
        /// materials stay as they are, so other rooms using them don't change.
        /// </summary>
        public static int Remap(Transform root, Dictionary<string, Material> map)
        {
            if (root == null) return 0;
            int count = 0;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (PrefabUtility.IsPartOfPrefabInstance(r)) continue;
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] != null && map.TryGetValue(mats[i].name, out var m) && m != mats[i]) { mats[i] = m; changed = true; }
                if (!changed) continue;
                r.sharedMaterials = mats;
                count++;
            }
            return count;
        }

        /// <summary>A URP Lit material with colour, smoothness, metallic and an optional tiled texture.</summary>
        public static Material Mat(string name, Color color, float smoothness, float metallic = 0f, Texture tex = null, Vector2? tiling = null)
        {
            var m = BuilderUtil.CreateMaterial(name, color, tex, tiling);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------ textures

        public delegate Color Painter(float u, float v, int x, int y);

        /// <summary>Paints a square texture into an asset (re-painting the same asset on re-runs).</summary>
        public static Texture2D Paint(string assetPath, int size, TextureWrapMode wrap, Painter paint)
        {
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = paint((x + 0.5f) / size, (y + 0.5f) / size, x, y);

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (tex == null)
            {
                tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = Path.GetFileNameWithoutExtension(assetPath) };
                AssetDatabase.CreateAsset(tex, assetPath);
            }
            else if (tex.width != size) tex.Reinitialize(size, size, TextureFormat.RGBA32, true);

            tex.wrapMode = wrap;
            tex.filterMode = FilterMode.Trilinear;
            tex.anisoLevel = 8;
            tex.SetPixels(px);
            tex.Apply(true);
            EditorUtility.SetDirty(tex);
            return tex;
        }

        /// <summary>Soft step from 0 to 1 as x goes from a to b (for anti-aliased painted edges).</summary>
        public static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));
    }
}
