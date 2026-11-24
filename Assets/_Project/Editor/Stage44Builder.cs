using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using PaperWings.Gameplay;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// Stage 44: Biscuit the cat, a real cat at last (PROTOTYPE model).
    /// Menu: PaperWings > Stage Builders > Stage 44 (Biscuit the Cat)
    ///
    ///  - Imports Assets/ThirdParty/Cat/an_animated_cat.glb (a realistic black cat with a full face
    ///    and tail rig and one sitting/grooming loop) into ordinary Unity assets in Art/Biscuit/.
    ///  - Builds the Biscuit prefab: the model scaled to sit with her ears at counter height, turned
    ///    to face forward, with colliders on her body and head, a hitbox on her swatting paw, and her
    ///    behaviour (CatBrain): watching planes, blinking, ear and tail life, the crouch-and-wiggle
    ///    hunting: she stalks low planes and strikes when they're in reach (the old timed swat is retired).
    ///  - Replaces the sphere cat in the kitchen with the prefab (same spot) and retires the old box paw.
    ///
    /// LICENSING: this model is a stand-in. Its Sketchfab page credits an earlier upload from the game
    /// "Murdered: Soul Suspect", so the CC BY licence it was posted under may not be the uploader's
    /// to give. Fine for prototyping; swap in a licensed cat before release (only the bone names in
    /// this file need changing). See Assets/ThirdParty/Cat/CREDITS.txt. Safe to re-run.
    /// </summary>
    public static class Stage44Builder
    {
        const string GlbPath = "Assets/ThirdParty/Cat/an_animated_cat.glb";
        const string ArtFolder = StageKit.Root + "Art/Biscuit";
        const string PrefabPath = StageKit.Root + "Prefabs/Biscuit.prefab";
        // Her size: the model's own units times this. 1.6 (the last try) was still too big; this is 30% smaller.
        // (Earlier tries sized her by ear height in her watching pose, but there her head is bowed, so she came out bigger than meant.)
        const float ModelScale = 1.12f;
        const float SwatDesignHeight = 44f;   // the ear height Stage 37's swat zone was laid out for: the zone scales with her
        static float swatScale = 1f;          // her ear height / SwatDesignHeight, measured in BuildPrefab, used by Place

        // The model's bones, by name prefix (the full names carry export numbering, e.g. "j_head_08_19").
        const string Hips = "j_hips_", Chest = "j_chest_", Neck = "j_neck_1_", Head = "j_head_",
                     EyeL = "j_l_eye_", EyeR = "j_r_eye_", LidL = "j_l_upper_eyelid_", LidR = "j_r_upper_eyelid_",
                     EarL = "j_l_ear_", EarR = "j_r_ear_",
                     UpperL = "j_l_humerous_", LowerL = "j_l_elbow_", WristL = "j_l_wrist_", PalmL = "j_l_palm_",
                     UpperR = "j_r_humerous_", LowerR = "j_r_elbow_", WristR = "j_r_wrist_", PalmR = "j_r_palm_";
        static readonly string[] Spine = { "j_spine_1_", "j_spine_2_", "j_spine_3_" };
        static readonly string[] Tail = { "j_tail_1_", "j_tail_2_", "j_tail_3_", "j_tail_4_", "j_tail_5_", "j_tail_6_" };
        static readonly string[] Feet = { "j_l_toe_", "j_r_toe_", "j_l_finger_", "j_r_finger_" };

        [MenuItem("PaperWings/Stage Builders/Stage 44 (Biscuit the Cat)", priority = 155)]
        public static void Build()
        {
            if (!File.Exists(GlbPath))
            {
                EditorUtility.DisplayDialog("PaperWings", $"Cat model not found at {GlbPath}.", "OK");
                return;
            }
            if (!StageKit.OpenDesk(out var scene)) return;
            var room = StageKit.RoomAt(StageKit.Kitchen);
            if (room == null)
            {
                EditorUtility.DisplayDialog("PaperWings", "No kitchen. Run Stage Builders > Stage 37 first.", "OK");
                return;
            }

            string report;
            try
            {
                EditorUtility.DisplayProgressBar("PaperWings", "Importing the cat...", 0.2f);
                var prefab = BuildPrefab(out report);
                if (prefab == null) return;
                EditorUtility.DisplayProgressBar("PaperWings", "Moving Biscuit into the kitchen...", 0.8f);
                Place(room.transform, prefab);
                WriteCredits();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            StageKit.Save(scene);
            EditorUtility.DisplayDialog("PaperWings",
                "Stage 44 built: Biscuit the cat (prototype model).\n\n" + report +
                "\n\nLook at her in the Game view: Free Flight in the Kitchen. Fly low and slow in front of her and watch her stalk, then strike.\n\n" +
                "This model is a stand-in: see Assets/ThirdParty/Cat/CREDITS.txt before any release.", "Purr");
        }

        // ------------------------------------------------------------------ the prefab

        static GameObject BuildPrefab(out string report)
        {
            report = "";
            var root = new GameObject("Biscuit");
            try
            {
                var import = GlbImport.Import(GlbPath, ArtFolder, "Biscuit", root.transform);
                if (import.clips.Count == 0) { EditorUtility.DisplayDialog("PaperWings", "The cat model has no animation.", "OK"); return null; }
                var clip = import.clips[0];
                foreach (var smr in import.renderers) foreach (var m in smr.sharedMaterials) if (m != null) { m.SetFloat("_Smoothness", 0.25f); EditorUtility.SetDirty(m); }

                var bones = new Dictionary<string, Transform>();
                Transform B(string prefix)
                {
                    if (bones.TryGetValue(prefix, out var t)) return t;
                    foreach (var c in root.GetComponentsInChildren<Transform>(true)) if (c.name.StartsWith(prefix)) { bones[prefix] = c; return c; }
                    return null;
                }
                foreach (var p in new[] { Hips, Chest, Neck, Head, EyeL, EyeR, LidL, LidR, EarL, EarR, UpperL, LowerL, WristL, PalmL, UpperR, LowerR, WristR, PalmR })
                    if (B(p) == null) Debug.LogWarning($"Biscuit: no bone starting '{p}'.");

                // Face forward (+Z), at her real-life size times ModelScale, standing on the origin.
                var model = import.model;
                clip.SampleAnimation(root, 0f);
                var facing = Vector3.ProjectOnPlane(B(Head).position - B(Hips).position, Vector3.up);
                float turn = Vector3.SignedAngle(facing, Vector3.forward, Vector3.up);
                model.RotateAround(root.transform.position, Vector3.up, turn);
                model.localScale *= ModelScale;
                model.localPosition *= ModelScale;

                // The clip never has her sitting with her paw down AND her head up: she starts and ends
                // looking down at her paw, and only lifts her head while licking it. So two moments:
                //  - restTime: paw down (the pose she holds while watching; CatBrain lifts her head)
                //  - lookTime: her face most nearly level and forward (to find which way her head faces)
                float restTime = 0f, lookTime = 0f, lowest = float.MaxValue, level = -2f;
                const int samples = 90;
                for (int i = 0; i < samples; i++)
                {
                    float time = clip.length * i / samples;
                    clip.SampleAnimation(root, time);
                    var paw = B(PalmR);
                    if (paw != null && paw.position.y < lowest) { lowest = paw.position.y; restTime = time; }
                    var face = ((B(EyeL).position + B(EyeR).position) * 0.5f - B(Head).position).normalized;
                    float d = Vector3.Dot(face, Vector3.forward);
                    if (d > level) { level = d; lookTime = time; }
                }

                clip.SampleAnimation(root, restTime);
                var middle = (B(Hips).position + B(Chest).position) * 0.5f;
                model.position -= new Vector3(middle.x, FeetY(B), middle.z);

                // Which of the head bone's own axes points out of her face (it differs from rig to rig).
                clip.SampleAnimation(root, lookTime);
                var headFace = NearestAxis(Quaternion.Inverse(B(Head).rotation) * Vector3.forward);
                var headUp = NearestAxis(Vector3.ProjectOnPlane(Quaternion.Inverse(B(Head).rotation) * Vector3.up, headFace));   // never the face axis itself
                float h = Mathf.Max(B(EarL).position.y, B(EarR).position.y);   // ear height with her head up: sizes her colliders and swat zone
                swatScale = h / SwatDesignHeight;
                clip.SampleAnimation(root, restTime);

                // Poses taken from the clip: lids open and closed (it blinks), ears back (it flicks them).
                var brain = root.AddComponent<CatBrain>();
                (brain.lidOpenL, brain.lidClosedL) = OpenClosed(root, clip, B(LidL), B(Head), headUp, import.renderers);
                (brain.lidOpenR, brain.lidClosedR) = OpenClosed(root, clip, B(LidR), B(Head), headUp, import.renderers);
                brain.earBackL = Farthest(root, clip, B(EarL));
                brain.earBackR = Farthest(root, clip, B(EarR));
                clip.SampleAnimation(root, restTime);

                brain.idleClip = clip;
                brain.restTime = restTime;
                brain.headFace = headFace;
                brain.headUp = headUp;
                brain.hips = B(Hips); brain.chest = B(Chest); brain.neck = B(Neck); brain.head = B(Head);
                brain.eyeL = B(EyeL); brain.eyeR = B(EyeR); brain.lidL = B(LidL); brain.lidR = B(LidR);
                brain.earL = B(EarL); brain.earR = B(EarR);
                brain.upperL = B(UpperL); brain.lowerL = B(LowerL); brain.pawL = B(WristL);
                brain.upperR = B(UpperR); brain.lowerR = B(LowerR); brain.pawR = B(WristR);
                var spine = new List<Transform>();
                foreach (var p in Spine) { var t = B(p); if (t != null) spine.Add(t); }
                brain.spine = spine.ToArray();
                var tail = new List<Transform>();
                foreach (var p in Tail) { var t = B(p); if (t != null) tail.Add(t); }
                brain.tail = tail.ToArray();

                // Colliders ride on the bones; a kinematic body on the root makes them one moving compound.
                Sphere(B(Chest), "ChestCollider", 0.2f * h);
                Sphere(B(Hips), "HipsCollider", 0.19f * h);
                Sphere(B(Head), "HeadCollider", 0.12f * h);
                foreach (var pawBone in new[] { B(PalmL), B(PalmR) })
                {
                    if (pawBone == null) continue;
                    var hit = new GameObject("PawHitbox");
                    hit.transform.SetParent(pawBone, false);
                    var box = hit.AddComponent<BoxCollider>();
                    box.size = Vector3.one * (0.13f * h) / Mathf.Max(0.0001f, pawBone.lossyScale.x);
                }
                var body = root.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.interpolation = RigidbodyInterpolation.None;
                root.GetComponent<Animator>().applyRootMotion = false;

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                int tris = 0;
                foreach (var smr in import.renderers) tris += smr.sharedMesh.triangles.Length / 3;
                report = $"- Imported {import.title ?? "the cat"} by {import.author ?? "unknown"}: {tris} triangles, {brain.tail.Length} tail bones, idle clip {clip.length:0.0} s.\n" +
                         $"- Scaled x{ModelScale:0.##}: ears at {h:0.#} with her head up (the counter top is 36). Watching pose from {restTime:0.00} s; her face is the head's {headFace} axis.\n" +
                         "- Hunting: watches planes, stalks low ones (crouch, wide eyes, tail-tip twitch, hip wiggle), then bats with either paw, bats twice, or rears up and grabs. Hitboxes on both paws.";
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static float FeetY(System.Func<string, Transform> bone)
        {
            float y = float.MaxValue;
            foreach (var p in Feet) { var t = bone(p); if (t != null) y = Mathf.Min(y, t.position.y); }
            return y == float.MaxValue ? 0f : y;
        }

        /// <summary>The principal axis (±X, ±Y, ±Z) closest to a direction.</summary>
        static Vector3 NearestAxis(Vector3 v)
        {
            var a = new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
            if (a.x >= a.y && a.x >= a.z) return new Vector3(Mathf.Sign(v.x), 0f, 0f);
            if (a.y >= a.z) return new Vector3(0f, Mathf.Sign(v.y), 0f);
            return new Vector3(0f, 0f, Mathf.Sign(v.z));
        }

        static List<Quaternion> Samples(GameObject root, AnimationClip clip, Transform bone, int n = 90)
        {
            var list = new List<Quaternion>();
            for (int i = 0; i < n; i++)
            {
                clip.SampleAnimation(root, clip.length * i / n);
                list.Add(bone.localRotation);
            }
            return list;
        }

        static float MeanAngle(List<Quaternion> all, Quaternion q)
        {
            float sum = 0f;
            foreach (var o in all) sum += Quaternion.Angle(o, q);
            return sum / Mathf.Max(1, all.Count);
        }

        /// <summary>
        /// A lid's two extremes in the clip, and which is which. The closed one is the one where the
        /// lid's skin sits LOWER on her head (an upper lid comes down over the eye). Measured on the
        /// skinned mesh itself, because "the pose she's in most" fails: she grooms with her eyes half shut.
        /// </summary>
        static (Quaternion open, Quaternion closed) OpenClosed(GameObject root, AnimationClip clip, Transform bone, Transform head,
                                                               Vector3 headUp, List<SkinnedMeshRenderer> renderers, int n = 90)
        {
            if (bone == null || head == null) return (Quaternion.identity, Quaternion.identity);
            var s = Samples(root, clip, bone, n);
            int a = 0, b = 0;
            float best = -1f;
            for (int i = 0; i < s.Count; i++)
                for (int j = i + 1; j < s.Count; j++)
                {
                    float ang = Quaternion.Angle(s[i], s[j]);
                    if (ang > best) { best = ang; a = i; b = j; }
                }
            float ha = LidHeight(root, clip, clip.length * a / n, bone, head, headUp, renderers);
            float hb = LidHeight(root, clip, clip.length * b / n, bone, head, headUp, renderers);
            return ha >= hb ? (s[a], s[b]) : (s[b], s[a]);
        }

        /// <summary>How high (along the head's up axis) the skin moved by this lid bone sits, at a moment in the clip.</summary>
        static float LidHeight(GameObject root, AnimationClip clip, float time, Transform lid, Transform head, Vector3 headUp, List<SkinnedMeshRenderer> renderers)
        {
            clip.SampleAnimation(root, time);
            float sum = 0f;
            int count = 0;
            var inverseHead = Quaternion.Inverse(head.rotation);
            foreach (var smr in renderers)
            {
                int bi = System.Array.IndexOf(smr.bones, lid);
                if (bi < 0) continue;
                var baked = new Mesh();
                smr.BakeMesh(baked, true);   // with the renderer's scale; then place it with position and rotation only
                var place = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                var verts = baked.vertices;
                var weights = smr.sharedMesh.boneWeights;
                for (int i = 0; i < verts.Length && i < weights.Length; i++)
                {
                    var w = weights[i];
                    float on = (w.boneIndex0 == bi ? w.weight0 : 0f) + (w.boneIndex1 == bi ? w.weight1 : 0f) + (w.boneIndex2 == bi ? w.weight2 : 0f) + (w.boneIndex3 == bi ? w.weight3 : 0f);
                    if (on < 0.5f) continue;
                    var p = place.MultiplyPoint3x4(verts[i]);
                    sum += Vector3.Dot(inverseHead * (p - head.position), headUp);
                    count++;
                }
                Object.DestroyImmediate(baked);
            }
            return count > 0 ? sum / count : 0f;
        }

        /// <summary>The pose farthest from the rest of the clip (for an ear: flicked back).</summary>
        static Quaternion Farthest(GameObject root, AnimationClip clip, Transform bone)
        {
            if (bone == null) return Quaternion.identity;
            var s = Samples(root, clip, bone);
            var far = s[0];
            float best = -1f;
            foreach (var q in s) { float m = MeanAngle(s, q); if (m > best) { best = m; far = q; } }
            return far;
        }

        static void Sphere(Transform bone, string name, float worldRadius)
        {
            if (bone == null) return;
            var go = new GameObject(name);
            go.transform.SetParent(bone, false);
            go.AddComponent<SphereCollider>().radius = worldRadius / Mathf.Max(0.0001f, bone.lossyScale.x);
        }

        // ------------------------------------------------------------------ the kitchen

        /// <summary>Swaps the sphere cat (Stage 37) for the prefab, in the same spot, and retires her old box paw.</summary>
        static void Place(Transform room, GameObject prefab)
        {
            var old = room.Find("Biscuit");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            foreach (var t in room.GetComponentsInChildren<Transform>(true))
                if (t.name == "PawHinge") { Object.DestroyImmediate(t.gameObject); break; }

            // The timed swat (Stage 37's CatSwat room event) is retired: she strikes when a plane is in
            // reach now, and her crouch is the warning. Its downdraft stays, driven by her (slam).
            RoomEvent swat = null;
            foreach (var ev in room.GetComponentsInChildren<RoomEvent>(true)) if (ev.name == "CatSwat") swat = ev;
            if (swat != null)
            {
                swat.movers = new Transform[0];
                swat.openEuler = new Vector3[0];
                swat.openOffset = new Vector3[0];
                swat.air = new Air.AirSource[0];
                swat.eventLights = new Light[0];
                swat.loops = new ParticleSystem[0];
                swat.enabled = false;
                EditorUtility.SetDirty(swat);
            }
            Air.BeamAir slam = null;

            // Stage 37 laid out the swat's downdraft, its dust and the eye glow for a bigger cat: fit them to her reach.
            // (Set from Stage 37's numbers every time, so re-running doesn't shrink them twice.)
            var catAt = room.position + new Vector3(-40f, 0f, 70f);
            float k = swatScale;
            foreach (var t in room.GetComponentsInChildren<Transform>(true))
            {
                switch (t.name)
                {
                    case "SwatDowndraft":
                        t.position = catAt + new Vector3(0f, 72f * k, -30f * k);
                        if (t.TryGetComponent(out Air.BeamAir beam))
                        {
                            beam.size = new Vector2(60f, 44f) * k;
                            beam.range = 72f * k;
                            beam.targetStrength = 0f;
                            beam.ramp = 15f;   // slams on and off with her strike
                            EditorUtility.SetDirty(beam);
                            slam = beam;
                        }
                        break;
                    case "SwatDust":   // it belonged to the timed swat
                        t.gameObject.SetActive(false);
                        break;
                    case "EyeGlow":    // her real eyes do the warning now
                        t.gameObject.SetActive(false);
                        break;
                }
            }

            var cat = (GameObject)PrefabUtility.InstantiatePrefab(prefab, room);
            cat.name = "Biscuit";
            cat.transform.SetPositionAndRotation(room.position + new Vector3(-40f, 0f, 70f), Quaternion.Euler(0f, 180f, 0f));   // in front of the counter, facing the room (Stage 37's spot)
            var brain = cat.GetComponent<CatBrain>();
            brain.slam = slam;
            PrefabUtility.RecordPrefabInstancePropertyModifications(brain);
        }

        static void WriteCredits()
        {
            const string path = "Assets/ThirdParty/Cat/CREDITS.txt";
            File.WriteAllText(path,
                "Biscuit the cat (PROTOTYPE MODEL)\n" +
                "\"An Animated Cat\" by Evil_Katz, https://sketchfab.com/3d-models/an-animated-cat-aec25699660043a29595f9572149d1e8\n" +
                "Posted under CC BY 4.0 (http://creativecommons.org/licenses/by/4.0/). Credit required if used.\n\n" +
                "WARNING: the model's page credits an earlier upload taken from the game \"Murdered: Soul Suspect\".\n" +
                "If the model comes from that game, the CC BY licence is not the uploader's to grant.\n" +
                "Use for prototyping only. Replace with a properly licensed cat before any release:\n" +
                "Stage44Builder.cs maps her bones by name, so a new rig only needs those names updated.\n");
            AssetDatabase.ImportAsset(path);
        }
    }
}
