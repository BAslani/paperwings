using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// A small importer for binary glTF (.glb) skinned characters (Stage 44: Biscuit the cat).
    /// Unity doesn't read .glb on its own; rather than add a package, this turns one file into
    /// ordinary Unity assets: a mesh per primitive group (with bone weights and bind poses), PNG
    /// textures, URP Lit materials, and generic AnimationClips (one per glTF animation) that an
    /// Animator can play. The node hierarchy is rebuilt as GameObjects under a "Model" child of the
    /// given root, and the clips' paths are relative to that root.
    ///
    /// UNITY NOTE: glTF is right-handed and Unity left-handed. Converting means mirroring X: positions
    /// and normals get x = -x, rotations (x, y, z, w) become (x, -y, -z, w), matrices become S*M*S
    /// with S = diag(-1, 1, 1, 1), and every triangle's winding flips. glTF puts UV (0, 0) at the
    /// top-left of an image, Unity at the bottom-left, so v = 1 - v.
    /// </summary>
    internal static class GlbImport
    {
        public sealed class Result
        {
            public Transform model;
            public readonly List<AnimationClip> clips = new List<AnimationClip>();
            public readonly List<SkinnedMeshRenderer> renderers = new List<SkinnedMeshRenderer>();
            public string author, license, title, source;
        }

        /// <summary>Imports the file under <paramref name="animRoot"/> and saves its assets in <paramref name="folder"/> as "{baseName}_...".</summary>
        public static Result Import(string glbPath, string folder, string baseName, Transform animRoot)
        {
            var bytes = File.ReadAllBytes(glbPath);
            if (bytes.Length < 20 || Encoding.ASCII.GetString(bytes, 0, 4) != "glTF") throw new Exception("Not a binary glTF file: " + glbPath);
            int jsonLen = BitConverter.ToInt32(bytes, 12);
            var json = (Dictionary<string, object>)MiniJson.Parse(Encoding.UTF8.GetString(bytes, 20, jsonLen));
            int binStart = 20 + jsonLen + 8;
            var gl = new Gltf(json, bytes, binStart);
            Directory.CreateDirectory(folder);

            var result = new Result();
            if (json.TryGetValue("asset", out var assetObj) && assetObj is Dictionary<string, object> asset && asset.TryGetValue("extras", out var ex) && ex is Dictionary<string, object> extras)
            {
                result.author = extras.TryGetValue("author", out var a) ? a as string : null;
                result.license = extras.TryGetValue("license", out var l) ? l as string : null;
                result.title = extras.TryGetValue("title", out var t) ? t as string : null;
                result.source = extras.TryGetValue("source", out var s) ? s as string : null;
            }

            // ---- Textures and materials ----
            var textures = new List<Texture2D>();
            var images = gl.List("images");
            var materialsJson = gl.List("materials");
            var blendImages = new HashSet<int>();
            var texList = gl.List("textures");
            foreach (Dictionary<string, object> m in materialsJson)
                if (Str(m, "alphaMode") == "BLEND" || Str(m, "alphaMode") == "MASK")
                {
                    int ti = BaseTexture(m);
                    if (ti >= 0) blendImages.Add(Int(texList[ti], "source"));
                }
            for (int i = 0; i < images.Count; i++)
            {
                var img = (Dictionary<string, object>)images[i];
                var data = gl.BufferView(Int(img, "bufferView"));
                string path = $"{folder}/{baseName}_Tex{i}.png";
                File.WriteAllBytes(path, data);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                if (AssetImporter.GetAtPath(path) is TextureImporter ti)
                {
                    ti.maxTextureSize = 2048;
                    ti.alphaIsTransparency = blendImages.Contains(i);
                    ti.SaveAndReimport();
                }
                textures.Add(AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            }

            var materials = new List<Material>();
            for (int i = 0; i < materialsJson.Count; i++)
            {
                var m = (Dictionary<string, object>)materialsJson[i];
                string path = $"{folder}/{baseName}_Mat{i}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    AssetDatabase.CreateAsset(mat, path);
                }
                int texIndex = BaseTexture(m);
                if (texIndex >= 0)
                {
                    var tex = textures[Int(texList[texIndex], "source")];
                    mat.SetTexture("_BaseMap", tex);
                    mat.SetTexture("_MainTex", tex);
                }
                mat.SetColor("_BaseColor", Color.white);
                mat.SetFloat("_Metallic", 0f);
                mat.SetFloat("_Smoothness", 0.2f);
                bool doubleSided = m.TryGetValue("doubleSided", out var ds) && ds is bool b && b;
                mat.SetFloat("_Cull", doubleSided ? (float)CullMode.Off : (float)CullMode.Back);
                string alpha = Str(m, "alphaMode");
                if (alpha == "BLEND" || alpha == "MASK")
                {
                    // Fur cards and whiskers: alpha-tested, not blended, so they sort correctly against the body.
                    mat.SetFloat("_AlphaClip", 1f);
                    mat.SetFloat("_Cutoff", 0.35f);
                    mat.EnableKeyword("_ALPHATEST_ON");
                    mat.renderQueue = (int)RenderQueue.AlphaTest;
                }
                EditorUtility.SetDirty(mat);
                materials.Add(mat);
            }

            // ---- Nodes ----
            var nodes = gl.List("nodes");
            var transforms = new Transform[nodes.Count];
            var model = new GameObject("Model").transform;
            model.SetParent(animRoot, false);
            result.model = model;
            void Make(int index, Transform parent)
            {
                var n = (Dictionary<string, object>)nodes[index];
                var go = new GameObject(Str(n, "name") ?? $"Node{index}");
                var t = go.transform;
                t.SetParent(parent, false);
                SetLocal(t, n);
                transforms[index] = t;
                if (n.TryGetValue("children", out var ch))
                    foreach (var c in (List<object>)ch) Make(Convert.ToInt32(c, CultureInfo.InvariantCulture), t);
            }
            var scenes = gl.List("scenes");
            int sceneIndex = json.TryGetValue("scene", out var sc) ? Convert.ToInt32(sc, CultureInfo.InvariantCulture) : 0;
            foreach (var r in (List<object>)((Dictionary<string, object>)scenes[sceneIndex])["nodes"])
                Make(Convert.ToInt32(r, CultureInfo.InvariantCulture), model);

            // ---- Skinned meshes ----
            var meshesJson = gl.List("meshes");
            var skins = gl.List("skins");
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = (Dictionary<string, object>)nodes[i];
                if (!n.ContainsKey("mesh") || transforms[i] == null) continue;
                int meshIndex = Int(n, "mesh");
                var mesh = BuildMesh(gl, (Dictionary<string, object>)meshesJson[meshIndex], out var primMats);
                mesh.name = $"{baseName}_Mesh{meshIndex}";

                if (n.ContainsKey("skin"))
                {
                    var skin = (Dictionary<string, object>)skins[Int(n, "skin")];
                    var joints = (List<object>)skin["joints"];
                    var ibm = gl.Floats(Int(skin, "inverseBindMatrices"));
                    var binds = new Matrix4x4[joints.Count];
                    var bones = new Transform[joints.Count];
                    for (int j = 0; j < joints.Count; j++)
                    {
                        var m = new Matrix4x4();
                        for (int c = 0; c < 4; c++) for (int rr = 0; rr < 4; rr++) m[rr, c] = ibm[j * 16 + c * 4 + rr];   // glTF matrices are column-major
                        binds[j] = Mirror(m);
                        bones[j] = transforms[Convert.ToInt32(joints[j], CultureInfo.InvariantCulture)];
                    }
                    mesh.bindposes = binds;
                    mesh = SaveAsset(mesh, $"{folder}/{mesh.name}.asset");

                    var smr = transforms[i].gameObject.AddComponent<SkinnedMeshRenderer>();
                    smr.sharedMesh = mesh;
                    smr.bones = bones;
                    smr.rootBone = skin.ContainsKey("skeleton") ? transforms[Int(skin, "skeleton")] : bones[0];
                    smr.sharedMaterials = primMats.ConvertAll(mi => mi >= 0 && mi < materials.Count ? materials[mi] : null).ToArray();
                    smr.updateWhenOffscreen = true;   // one character: always-correct bounds beat a culling saving
                    result.renderers.Add(smr);
                }
                else
                {
                    mesh = SaveAsset(mesh, $"{folder}/{mesh.name}.asset");
                    transforms[i].gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                    transforms[i].gameObject.AddComponent<MeshRenderer>().sharedMaterials = primMats.ConvertAll(mi => mi >= 0 && mi < materials.Count ? materials[mi] : null).ToArray();
                }
            }

            // ---- Animations ----
            var anims = gl.List("animations");
            for (int a = 0; a < anims.Count; a++)
            {
                var an = (Dictionary<string, object>)anims[a];
                var clip = new AnimationClip { name = $"{baseName}_{Clean(Str(an, "name") ?? "Anim" + a)}", frameRate = 30f, legacy = false };
                var samplers = (List<object>)an["samplers"];
                foreach (Dictionary<string, object> ch in (List<object>)an["channels"])
                {
                    var target = (Dictionary<string, object>)ch["target"];
                    if (!target.ContainsKey("node")) continue;
                    var t = transforms[Int(target, "node")];
                    if (t == null) continue;
                    string path = AnimationUtility.CalculateTransformPath(t, animRoot);
                    var sampler = (Dictionary<string, object>)samplers[Int(ch, "sampler")];
                    var times = gl.Floats(Int(sampler, "input"));
                    var values = gl.Floats(Int(sampler, "output"));
                    switch (Str(target, "path"))
                    {
                        case "translation":
                            AddCurves(clip, path, "m_LocalPosition", times, values, 3, (v, k) => k == 0 ? -v : v);
                            break;
                        case "scale":
                            AddCurves(clip, path, "m_LocalScale", times, values, 3, (v, k) => v);
                            break;
                        case "rotation":
                            // Keep each key on the same side as the one before (q and -q are the same rotation).
                            for (int k = 1; k < times.Length; k++)
                            {
                                float dot = 0f;
                                for (int c = 0; c < 4; c++) dot += values[k * 4 + c] * values[(k - 1) * 4 + c];
                                if (dot < 0f) for (int c = 0; c < 4; c++) values[k * 4 + c] = -values[k * 4 + c];
                            }
                            AddCurves(clip, path, "m_LocalRotation", times, values, 4, (v, k) => k == 1 || k == 2 ? -v : v);
                            break;
                    }
                }
                clip.EnsureQuaternionContinuity();
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                result.clips.Add(SaveAsset(clip, $"{folder}/{clip.name}.anim"));
            }

            AssetDatabase.SaveAssets();
            return result;
        }

        // ------------------------------------------------------------------ pieces

        static Mesh BuildMesh(Gltf gl, Dictionary<string, object> meshJson, out List<int> primMaterials)
        {
            var pos = new List<Vector3>(); var nrm = new List<Vector3>(); var uv = new List<Vector2>();
            var weights = new List<BoneWeight>();
            var subs = new List<int[]>();
            primMaterials = new List<int>();
            foreach (Dictionary<string, object> prim in (List<object>)meshJson["primitives"])
            {
                var attrs = (Dictionary<string, object>)prim["attributes"];
                int start = pos.Count;
                var p = gl.Floats(Int(attrs, "POSITION"));
                int count = p.Length / 3;
                for (int i = 0; i < count; i++) pos.Add(new Vector3(-p[i * 3], p[i * 3 + 1], p[i * 3 + 2]));
                if (attrs.ContainsKey("NORMAL"))
                {
                    var n = gl.Floats(Int(attrs, "NORMAL"));
                    for (int i = 0; i < count; i++) nrm.Add(new Vector3(-n[i * 3], n[i * 3 + 1], n[i * 3 + 2]));
                }
                else for (int i = 0; i < count; i++) nrm.Add(Vector3.up);
                if (attrs.ContainsKey("TEXCOORD_0"))
                {
                    var t = gl.Floats(Int(attrs, "TEXCOORD_0"));
                    for (int i = 0; i < count; i++) uv.Add(new Vector2(t[i * 2], 1f - t[i * 2 + 1]));
                }
                else for (int i = 0; i < count; i++) uv.Add(Vector2.zero);
                if (attrs.ContainsKey("JOINTS_0") && attrs.ContainsKey("WEIGHTS_0"))
                {
                    var j = gl.Ints(Int(attrs, "JOINTS_0"));
                    var w = gl.Floats(Int(attrs, "WEIGHTS_0"));
                    for (int i = 0; i < count; i++)
                    {
                        float sum = w[i * 4] + w[i * 4 + 1] + w[i * 4 + 2] + w[i * 4 + 3];
                        if (sum <= 0f) sum = 1f;
                        weights.Add(new BoneWeight
                        {
                            boneIndex0 = j[i * 4], weight0 = w[i * 4] / sum,
                            boneIndex1 = j[i * 4 + 1], weight1 = w[i * 4 + 1] / sum,
                            boneIndex2 = j[i * 4 + 2], weight2 = w[i * 4 + 2] / sum,
                            boneIndex3 = j[i * 4 + 3], weight3 = w[i * 4 + 3] / sum,
                        });
                    }
                }
                int[] idx;
                if (prim.ContainsKey("indices"))
                {
                    var raw = gl.Ints(Int(prim, "indices"));
                    idx = new int[raw.Length];
                    for (int i = 0; i < raw.Length; i += 3) { idx[i] = raw[i] + start; idx[i + 1] = raw[i + 2] + start; idx[i + 2] = raw[i + 1] + start; }   // flip winding
                }
                else
                {
                    idx = new int[count];
                    for (int i = 0; i < count; i += 3) { idx[i] = start + i; idx[i + 1] = start + i + 2; idx[i + 2] = start + i + 1; }
                }
                subs.Add(idx);
                primMaterials.Add(prim.ContainsKey("material") ? Int(prim, "material") : -1);
            }

            var mesh = new Mesh();
            if (pos.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(pos);
            mesh.SetNormals(nrm);
            mesh.SetUVs(0, uv);
            if (weights.Count == pos.Count) mesh.boneWeights = weights.ToArray();
            mesh.subMeshCount = subs.Count;
            for (int s = 0; s < subs.Count; s++) mesh.SetTriangles(subs[s], s);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        static void SetLocal(Transform t, Dictionary<string, object> n)
        {
            if (n.TryGetValue("matrix", out var mo))
            {
                var f = (List<object>)mo;
                var m = new Matrix4x4();
                for (int c = 0; c < 4; c++) for (int r = 0; r < 4; r++) m[r, c] = Convert.ToSingle(f[c * 4 + r], CultureInfo.InvariantCulture);
                m = Mirror(m);
                t.localPosition = m.GetColumn(3);
                t.localRotation = m.rotation;
                t.localScale = m.lossyScale;
                return;
            }
            if (n.TryGetValue("translation", out var tr))
            {
                var v = (List<object>)tr;
                t.localPosition = new Vector3(-F(v[0]), F(v[1]), F(v[2]));
            }
            if (n.TryGetValue("rotation", out var ro))
            {
                var q = (List<object>)ro;
                t.localRotation = new Quaternion(F(q[0]), -F(q[1]), -F(q[2]), F(q[3]));
            }
            if (n.TryGetValue("scale", out var so))
            {
                var s = (List<object>)so;
                t.localScale = new Vector3(F(s[0]), F(s[1]), F(s[2]));
            }
        }

        /// <summary>A right-handed glTF matrix in Unity's left-handed space: S * M * S with S = diag(-1, 1, 1, 1).</summary>
        static Matrix4x4 Mirror(Matrix4x4 m)
        {
            var s = Matrix4x4.Scale(new Vector3(-1f, 1f, 1f));
            return s * m * s;
        }

        static void AddCurves(AnimationClip clip, string path, string property, float[] times, float[] values, int comps, Func<float, int, float> convert)
        {
            string[] suffix = { ".x", ".y", ".z", ".w" };
            for (int c = 0; c < comps; c++)
            {
                var keys = new Keyframe[times.Length];
                for (int k = 0; k < times.Length; k++) keys[k] = new Keyframe(times[k], convert(values[k * comps + c], c));
                // Linear interpolation, as glTF's LINEAR samplers ask.
                for (int k = 0; k < keys.Length; k++)
                {
                    float inT = k > 0 ? (keys[k].value - keys[k - 1].value) / Mathf.Max(1e-5f, keys[k].time - keys[k - 1].time) : 0f;
                    float outT = k < keys.Length - 1 ? (keys[k + 1].value - keys[k].value) / Mathf.Max(1e-5f, keys[k + 1].time - keys[k].time) : 0f;
                    keys[k].inTangent = k > 0 ? inT : outT;
                    keys[k].outTangent = k < keys.Length - 1 ? outT : inT;
                }
                clip.SetCurve(path, typeof(Transform), property + suffix[c], new AnimationCurve(keys));
            }
        }

        /// <summary>Saves an asset, or updates the existing one in place so references to it survive a re-import.</summary>
        static T SaveAsset<T>(T obj, string path) where T : UnityEngine.Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(obj, path);
                return obj;
            }
            EditorUtility.CopySerialized(obj, existing);
            existing.name = Path.GetFileNameWithoutExtension(path);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        static int BaseTexture(Dictionary<string, object> material)
        {
            if (material.TryGetValue("pbrMetallicRoughness", out var p) && p is Dictionary<string, object> pbr
                && pbr.TryGetValue("baseColorTexture", out var b) && b is Dictionary<string, object> bt)
                return Int(bt, "index");
            return -1;
        }

        static string Clean(string s)
        {
            var sb = new StringBuilder();
            foreach (char ch in s) sb.Append(char.IsLetterOrDigit(ch) ? ch : '_');
            return sb.ToString();
        }

        static float F(object o) => Convert.ToSingle(o, CultureInfo.InvariantCulture);
        static int Int(object dict, string key) => Convert.ToInt32(((Dictionary<string, object>)dict)[key], CultureInfo.InvariantCulture);
        static string Str(Dictionary<string, object> d, string key) => d.TryGetValue(key, out var v) ? v as string : null;

        // ------------------------------------------------------------------ buffers

        sealed class Gltf
        {
            readonly Dictionary<string, object> json;
            readonly byte[] bytes;
            readonly int binStart;

            public Gltf(Dictionary<string, object> json, byte[] bytes, int binStart) { this.json = json; this.bytes = bytes; this.binStart = binStart; }

            public List<object> List(string key) => json.TryGetValue(key, out var v) ? (List<object>)v : new List<object>();

            public byte[] BufferView(int index)
            {
                var bv = (Dictionary<string, object>)List("bufferViews")[index];
                int off = binStart + (bv.ContainsKey("byteOffset") ? Int(bv, "byteOffset") : 0);
                var data = new byte[Int(bv, "byteLength")];
                Buffer.BlockCopy(bytes, off, data, 0, data.Length);
                return data;
            }

            static int Comps(string type) => type switch { "SCALAR" => 1, "VEC2" => 2, "VEC3" => 3, "VEC4" => 4, "MAT4" => 16, _ => 1 };
            static int Size(int componentType) => componentType switch { 5120 => 1, 5121 => 1, 5122 => 2, 5123 => 2, 5125 => 4, 5126 => 4, _ => 4 };

            void Locate(int accessor, out int start, out int count, out int comps, out int ctype, out int stride, out bool normalized)
            {
                var a = (Dictionary<string, object>)List("accessors")[accessor];
                var bv = (Dictionary<string, object>)List("bufferViews")[Int(a, "bufferView")];
                comps = Comps((string)a["type"]);
                ctype = Int(a, "componentType");
                count = Int(a, "count");
                start = binStart + (bv.ContainsKey("byteOffset") ? Int(bv, "byteOffset") : 0) + (a.ContainsKey("byteOffset") ? Int(a, "byteOffset") : 0);
                stride = bv.ContainsKey("byteStride") ? Int(bv, "byteStride") : comps * Size(ctype);
                normalized = a.TryGetValue("normalized", out var n) && n is bool b && b;
            }

            public float[] Floats(int accessor)
            {
                Locate(accessor, out int start, out int count, out int comps, out int ctype, out int stride, out bool normalized);
                var result = new float[count * comps];
                int size = Size(ctype);
                for (int i = 0; i < count; i++)
                    for (int c = 0; c < comps; c++)
                    {
                        int o = start + i * stride + c * size;
                        float v = ctype switch
                        {
                            5126 => BitConverter.ToSingle(bytes, o),
                            5123 => BitConverter.ToUInt16(bytes, o) / (normalized ? 65535f : 1f),
                            5121 => bytes[o] / (normalized ? 255f : 1f),
                            5122 => BitConverter.ToInt16(bytes, o) / (normalized ? 32767f : 1f),
                            5120 => (sbyte)bytes[o] / (normalized ? 127f : 1f),
                            _ => BitConverter.ToUInt32(bytes, o),
                        };
                        result[i * comps + c] = v;
                    }
                return result;
            }

            public int[] Ints(int accessor)
            {
                Locate(accessor, out int start, out int count, out int comps, out int ctype, out int stride, out _);
                var result = new int[count * comps];
                int size = Size(ctype);
                for (int i = 0; i < count; i++)
                    for (int c = 0; c < comps; c++)
                    {
                        int o = start + i * stride + c * size;
                        result[i * comps + c] = ctype switch
                        {
                            5121 => bytes[o],
                            5123 => BitConverter.ToUInt16(bytes, o),
                            5125 => (int)BitConverter.ToUInt32(bytes, o),
                            _ => (int)BitConverter.ToSingle(bytes, o),
                        };
                    }
                return result;
            }
        }

        // ------------------------------------------------------------------ JSON

        /// <summary>Just enough JSON for glTF: objects, arrays, strings, numbers (as double), true/false/null.</summary>
        static class MiniJson
        {
            public static object Parse(string s) { int i = 0; return Value(s, ref i); }

            static void Skip(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

            static object Value(string s, ref int i)
            {
                Skip(s, ref i);
                char c = s[i];
                if (c == '{')
                {
                    var d = new Dictionary<string, object>();
                    i++; Skip(s, ref i);
                    if (s[i] == '}') { i++; return d; }
                    while (true)
                    {
                        Skip(s, ref i);
                        string key = Text(s, ref i);
                        Skip(s, ref i); i++;   // ':'
                        d[key] = Value(s, ref i);
                        Skip(s, ref i);
                        if (s[i] == ',') { i++; continue; }
                        i++; return d;         // '}'
                    }
                }
                if (c == '[')
                {
                    var l = new List<object>();
                    i++; Skip(s, ref i);
                    if (s[i] == ']') { i++; return l; }
                    while (true)
                    {
                        l.Add(Value(s, ref i));
                        Skip(s, ref i);
                        if (s[i] == ',') { i++; continue; }
                        i++; return l;         // ']'
                    }
                }
                if (c == '"') return Text(s, ref i);
                if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
                if (s.Length - i >= 5 && string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
                if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
                int start = i;
                while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
                return double.Parse(s.Substring(start, i - start), CultureInfo.InvariantCulture);
            }

            static string Text(string s, ref int i)
            {
                var sb = new StringBuilder();
                i++;   // opening quote
                while (s[i] != '"')
                {
                    if (s[i] == '\\')
                    {
                        i++;
                        char e = s[i];
                        switch (e)
                        {
                            case 'n': sb.Append('\n'); break;
                            case 't': sb.Append('\t'); break;
                            case 'r': sb.Append('\r'); break;
                            case 'b': sb.Append('\b'); break;
                            case 'f': sb.Append('\f'); break;
                            case 'u': sb.Append((char)int.Parse(s.Substring(i + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture)); i += 4; break;
                            default: sb.Append(e); break;
                        }
                        i++;
                    }
                    else sb.Append(s[i++]);
                }
                i++;   // closing quote
                return sb.ToString();
            }
        }
    }
}
