using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using PaperWings.Air;
using PaperWings.Combat;
using PaperWings.Flight;
using PaperWings.Gameplay;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// Shared building blocks for Stages 23-28: props, air sources (with their particles), rings,
    /// spawners and room wiring. Every helper works in WORLD positions, like the earlier builders.
    /// </summary>
    internal static class StageKit
    {
        public const string Root = "Assets/_Project/";
        public const string DeskScene = Root + "Scenes/DeskPrototype.unity";
        public const float CeilingY = 125f;

        // Room indices (campaign nights refer to rooms by number).
        // Slot 3 was the stairwell: removed in Stage 42, the slot stays empty so later rooms keep their numbers.
        public const int Office = 0, BreakRoom = 1, ServerRoom = 2, Stairwell = 3, Rooftop = 4, Kitchen = 5;

        // ------------------------------------------------------------------ scene

        /// <summary>Opens DeskPrototype (asking to save first). False if it's missing or the user cancelled.</summary>
        public static bool OpenDesk(out Scene scene, bool ask = true)
        {
            scene = default;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(DeskScene) == null)
            {
                EditorUtility.DisplayDialog("PaperWings", "DeskPrototype scene not found. Run the earlier stages first.", "OK");
                return false;
            }
            if (ask && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            scene = SceneManager.GetActiveScene().path == DeskScene ? SceneManager.GetActiveScene() : EditorSceneManager.OpenScene(DeskScene);
            return true;
        }

        public static void Save(Scene scene)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        public static Transform FindRoot(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects()) if (root.name == name) return root.transform;
            return null;
        }

        /// <summary>Deletes a root object (if any) and makes a fresh empty one at a position.</summary>
        public static Transform FreshRoot(Scene scene, string name, Vector3 position)
        {
            var old = FindRoot(scene, name);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var t = new GameObject(name).transform;
            t.position = position;
            return t;
        }

        public static Transform Group(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        public static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(Root + path);

        public static PlaneController Player()
        {
            foreach (var p in Object.FindObjectsByType<PlaneController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (p.GetComponent<PlaneInput>() != null) return p;
            return null;
        }

        // ------------------------------------------------------------------ materials

        public static Material Mat(string name, Color color, float smoothness = 0.15f, float metallic = 0f)
        {
            var m = BuilderUtil.CreateMaterial(name, color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(m);
            return m;
        }

        public static Material Textured(string name, Color tint, Texture tex, Vector2 tiling) => BuilderUtil.CreateMaterial(name, tint, tex, tiling);

        public static Material Emissive(string name, Color baseColor, Color emission)
        {
            var m = BuilderUtil.CreateMaterial(name, baseColor);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Texture2D Dot => Stage2Builder.SoftDotTexture(Root + "Art/T_SoftDot.asset");
        public static Material StreakMat => Stage2Builder.ParticleMaterial("M_WindStreak", Dot, additive: true);
        public static Material ShimmerMat => Stage2Builder.ParticleMaterial("M_HeatShimmer", Dot, additive: false);

        // ------------------------------------------------------------------ geometry

        /// <summary>A solid box (keeps its collider unless told otherwise).</summary>
        public static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material mat, bool collider = true, Vector3 euler = default)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(euler);
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            else CylinderCollider(go);
            return go;
        }

        /// <summary>
        /// Unity's cylinder primitive comes with a CAPSULE collider. On a wide, flat cylinder (a
        /// table top, a tank roof) the capsule becomes a big invisible sphere that planes crash into
        /// (the Squall throw point sat inside one). Swap it for a convex mesh collider of the cylinder.
        /// </summary>
        public static void CylinderCollider(GameObject go)
        {
            var capsule = go.GetComponent<CapsuleCollider>();
            if (capsule == null) return;
            bool trigger = capsule.isTrigger;
            Object.DestroyImmediate(capsule);
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            mc.convex = true;
            mc.isTrigger = trigger;
        }

        /// <summary>Fixes every cylinder already in the open scene(s) that still has a capsule collider. Returns how many.</summary>
        public static int FixCylinderColliders()
        {
            int n = 0;
            foreach (var capsule in Object.FindObjectsByType<CapsuleCollider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var mf = capsule.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null || mf.sharedMesh.name != "Cylinder") continue;
                if (capsule.GetComponentInParent<Flight.PlaneController>() != null) continue;
                CylinderCollider(capsule.gameObject);
                n++;
            }
            return n;
        }

        /// <summary>A cylinder: size.x/z = diameter, size.y = HEIGHT (Unity's cylinder is 2 tall at scale 1, so this halves it).</summary>
        public static GameObject Cyl(string name, Transform parent, Vector3 pos, Vector3 size, Material mat, bool collider = true, Vector3 euler = default)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(euler);
            go.transform.localScale = new Vector3(size.x, size.y * 0.5f, size.z);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        /// <summary>A flat decoration facing <paramref name="facing"/> (no collider, no shadow).</summary>
        public static GameObject Quad(string name, Transform parent, Vector3 pos, Vector3 facing, float w, float h, Material mat)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name;
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(parent, false);
            q.transform.position = pos;
            q.transform.rotation = Quaternion.LookRotation(-facing);   // a Quad's visible face points along -forward
            q.transform.localScale = new Vector3(w, h, 1f);
            var r = q.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return q;
        }

        /// <summary>A wall seen from inside (one-sided face + solid collider behind it).</summary>
        public static void Wall(string name, Transform parent, Vector3 centre, Vector3 inward, float width, float height, Material mat)
            => Stage21Builder.Wall(name, parent, centre, inward, width, height, mat);

        /// <summary>A horizontal slab seen from below or above (floor/ceiling) with a collider.</summary>
        public static GameObject Slab(string name, Transform parent, Vector3 centre, float width, float depth, Material mat, bool facesUp)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = centre;
            go.transform.rotation = facesUp ? Quaternion.identity : Quaternion.Euler(180f, 0f, 0f);
            go.transform.localScale = new Vector3(width / 10f, 1f, depth / 10f);   // a Plane is 10 x 10
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!facesUp) go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        public static Light PointLight(string name, Transform parent, Vector3 pos, Color color, float intensity, float range, bool shadows = false)
        {
            var l = new GameObject(name).AddComponent<Light>();
            l.transform.SetParent(parent, false);
            l.transform.position = pos;
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            return l;
        }

        public static Transform Spawn(string name, Transform parent, Vector3 pos, float yaw)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            return t;
        }

        // ------------------------------------------------------------------ particles

        public static ParticleSystem Emitter(Transform parent, string name, Vector3 localPos, Vector3 localEuler, Material mat, bool stretched, float sizeMin, float sizeMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            return Stage2Builder.AddParticles(go, mat, stretched, sizeMin, sizeMax);
        }

        // ------------------------------------------------------------------ air sources

        /// <summary>A thermal column without the laptop: the laptop prefab's air and shimmer only.</summary>
        public static ThermalSource Thermal(string name, Transform parent, Vector3 basePos, ThermalSettings settings)
        {
            var prefab = Load<GameObject>("Prefabs/LaptopThermal.prefab");
            if (prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = name;
            go.transform.SetPositionAndRotation(basePos, Quaternion.identity);
            go.transform.localScale = Vector3.one;
            foreach (var child in new[] { "Deck", "Screen", "HeatStrip", "ScreenHinge" })
            {
                var c = go.transform.Find(child);
                if (c != null) Object.DestroyImmediate(c.gameObject);
            }
            var source = go.GetComponent<ThermalSource>();
            if (source != null && settings != null) BuilderUtil.SetRef(source, "settings", settings);
            return source;
        }

        public static ThermalSettings ThermalAsset(string file, float lift, float radius, float height, float shimmerRate = 14f)
        {
            var s = Stage2Builder.LoadOrCreate<ThermalSettings>(Root + "Settings/" + file + ".asset");
            s.liftSpeed = lift;
            s.radius = radius;
            s.height = height;
            s.shimmerRate = shimmerRate;
            s.shimmerColor = AirPalette.Of(AirLook.Heat, 0.12f);
            EditorUtility.SetDirty(s);
            return s;
        }

        /// <summary>A radiator against a wall: a finned body plus its curtain of rising warm air.</summary>
        public static RadiatorSource Radiator(string name, Transform parent, Vector3 floorPos, float yaw, float length, RadiatorSettings settings)
        {
            var body = Mat("M_Radiator", new Color(0.86f, 0.85f, 0.8f), 0.35f, 0.2f);
            var warm = Emissive("M_RadiatorWarm", new Color(0.9f, 0.6f, 0.4f), new Color(0.5f, 0.18f, 0.04f));
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.SetPositionAndRotation(floorPos, Quaternion.Euler(0f, yaw, 0f));

            // Model in the radiator's own frame: X along the wall, +Z into the room.
            Vector3 L(float x, float y, float z) => root.TransformPoint(new Vector3(x, y, z));
            var main = Box("Body", root, L(0f, 12f, 1.5f), Vector3.one, body);
            main.transform.localRotation = Quaternion.identity;
            main.transform.localScale = new Vector3(length, 18f, 3f);
            for (float x = -length * 0.5f + 1.5f; x < length * 0.5f; x += 3f)
            {
                var fin = Box("Fin", root, L(x, 12f, 3.2f), Vector3.one, body, collider: false);
                fin.transform.localRotation = Quaternion.identity;
                fin.transform.localScale = new Vector3(0.8f, 17f, 0.6f);
            }
            var strip = Box("WarmTop", root, L(0f, 21.1f, 1.5f), Vector3.one, warm, collider: false);
            strip.transform.localRotation = Quaternion.identity;
            strip.transform.localScale = new Vector3(length - 1f, 0.2f, 2.6f);
            foreach (float x in new[] { -length * 0.5f + 2f, length * 0.5f - 2f })
            {
                var foot = Box("Foot", root, L(x, 1.5f, 1.5f), Vector3.one, body);
                foot.transform.localRotation = Quaternion.identity;
                foot.transform.localScale = new Vector3(1.5f, 3f, 2.5f);
            }

            var source = root.gameObject.AddComponent<RadiatorSource>();
            source.Length = length;
            BuilderUtil.SetRef(source, "settings", settings);
            var shimmer = Emitter(root, "Shimmer", new Vector3(0f, 22f, settings != null ? settings.depth * 0.35f : 4f), new Vector3(-90f, 0f, 0f), ShimmerMat, false, 0.5f, 1.1f);
            var noise = shimmer.noise;
            noise.enabled = true;
            noise.strength = 0.5f;
            noise.frequency = 0.3f;
            BuilderUtil.SetRef(source, "shimmer", shimmer);
            return source;
        }

        public static RadiatorSettings RadiatorAsset()
        {
            var s = Stage2Builder.LoadOrCreate<RadiatorSettings>(Root + "Settings/Radiator_Wall.asset");
            s.liftSpeed = 6.5f;
            s.depth = 11f;
            s.height = 95f;
            s.outwardLean = 0.18f;
            s.shimmerColor = AirPalette.Of(AirLook.Heat, 0.1f);
            EditorUtility.SetDirty(s);
            return s;
        }

        /// <summary>A ceiling fan: downrod, motor, four spinning blades, and its two-part air.</summary>
        public static CeilingFanSource CeilingFan(string name, Transform parent, Vector3 hubPos, float ceilingY, CeilingFanSettings settings, float cycleOffset = 0f)
        {
            var metal = Mat("M_FanMetal", new Color(0.75f, 0.72f, 0.65f), 0.5f, 0.6f);
            var wood = Mat("M_FanBladeWood", new Color(0.5f, 0.34f, 0.22f), 0.3f);
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.position = hubPos;

            float rod = ceilingY - hubPos.y;
            Cyl("Downrod", root, hubPos + Vector3.up * (rod * 0.5f), new Vector3(0.8f, rod, 0.8f), metal, collider: false);
            Cyl("CeilingCap", root, new Vector3(hubPos.x, ceilingY - 0.6f, hubPos.z), new Vector3(4f, 1.2f, 4f), metal, collider: false);
            Cyl("Motor", root, hubPos, new Vector3(6f, 2.6f, 6f), metal);
            var blades = Group(root, "Blades");
            blades.position = hubPos + Vector3.down * 0.6f;
            float R = settings != null ? settings.bladeRadius : 14f;
            for (int i = 0; i < 4; i++)
            {
                var dir = Quaternion.Euler(0f, i * 90f, 0f) * Vector3.forward;
                var b = Box("Blade", blades, blades.position + dir * (R * 0.5f + 1.5f), new Vector3(3.2f, 0.25f, R - 2f), wood,
                            euler: new Vector3(0f, i * 90f, 8f));
                b.transform.SetParent(blades, true);
            }

            var source = root.gameObject.AddComponent<CeilingFanSource>();
            BuilderUtil.SetRef(source, "settings", settings);
            BuilderUtil.SetRef(source, "blades", blades);
            var so = new SerializedObject(source);
            so.FindProperty("cycleOffset").floatValue = cycleOffset;
            so.ApplyModifiedPropertiesWithoutUndo();
            var core = Emitter(root, "CoreStreaks", new Vector3(0f, -2f, 0f), new Vector3(90f, 0f, 0f), StreakMat, true, 0.06f, 0.14f);
            var ring = Emitter(root, "RingStreaks", new Vector3(0f, -(settings != null ? settings.ringReach : 60f), 0f), new Vector3(-90f, 0f, 0f), StreakMat, true, 0.06f, 0.14f);
            BuilderUtil.SetRef(source, "coreStreaks", core);
            BuilderUtil.SetRef(source, "ringStreaks", ring);
            return source;
        }

        public static CeilingFanSettings CeilingFanAsset(string file, float bladeRadius, float reach, float onTime, float offTime)
        {
            var s = Stage2Builder.LoadOrCreate<CeilingFanSettings>(Root + "Settings/" + file + ".asset");
            s.bladeRadius = bladeRadius;
            s.ringWidth = 12f;
            s.coreReach = reach;
            s.ringReach = reach - 5f;
            s.coreDownSpeed = 8f;
            s.ringUpSpeed = 7f;
            s.swirlSpeed = 3.5f;
            s.onTime = onTime;
            s.offTime = offTime;
            EditorUtility.SetDirty(s);
            return s;
        }

        /// <summary>A straight beam of wind, optionally gusting on a cycle.</summary>
        public static BeamAir Beam(string name, Transform parent, Vector3 pos, Quaternion rot, Vector2 size, float range, float speed,
                                   float onTime = 0f, float offTime = 3f, float offset = 0f, bool heat = false, float strength = 1f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            var beam = go.AddComponent<BeamAir>();
            beam.size = size;
            beam.range = range;
            beam.windSpeed = speed;
            beam.onTime = onTime;
            beam.offTime = offTime;
            beam.cycleOffset = offset;
            beam.heat = heat;
            beam.targetStrength = strength;
            var ps = Emitter(go.transform, "Streaks", Vector3.zero, Vector3.zero, StreakMat, true, 0.06f, 0.14f);
            BuilderUtil.SetRef(beam, "streaks", ps);
            return beam;
        }

        /// <summary>A programmable column of air (lift/sink, push/pull, swirl).</summary>
        public static ColumnAir Column(string name, Transform parent, Vector3 basePos, float radius, float height, float vertical,
                                       float radial = 0f, float swirl = 0f, bool heat = false, float strength = 1f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = basePos;
            var c = go.AddComponent<ColumnAir>();
            c.radius = radius;
            c.height = height;
            c.verticalSpeed = vertical;
            c.radialSpeed = radial;
            c.swirlSpeed = swirl;
            c.heat = heat;
            c.targetStrength = strength;
            var ps = Emitter(go.transform, "Streaks", Vector3.zero, new Vector3(-90f, 0f, 0f), heat ? ShimmerMat : StreakMat, !heat, 0.08f, heat ? 0.9f : 0.16f);
            BuilderUtil.SetRef(c, "streaks", ps);
            return c;
        }

        /// <summary>An AC vent (the Stage 5 prefab) at a position, blowing along rot's forward.</summary>
        public static GameObject Vent(string name, Transform parent, Vector3 pos, Quaternion rot)
        {
            var prefab = Load<GameObject>("Prefabs/CeilingVent.prefab");
            if (prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.SetPositionAndRotation(pos, rot);
            return go;
        }

        public static GameObject DeskFan(string name, Transform parent, Vector3 pos, float yaw, float headTilt = 0f)
        {
            var prefab = Load<GameObject>("Prefabs/DeskFan.prefab");
            if (prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var head = go.transform.Find("Head");
            if (head != null) head.localRotation = Quaternion.Euler(headTilt, 0f, 0f);
            return go;
        }

        public static WindFieldSource Wind(string name, Transform parent, Vector3 centre, Vector3 size, Vector3 wind)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = centre;
            var w = go.AddComponent<WindFieldSource>();
            w.size = size;
            w.wind = wind;
            var ps = Emitter(go.transform, "Streaks", Vector3.zero, Vector3.zero, StreakMat, true, 0.08f, 0.2f);
            var main = ps.main;
            main.maxParticles = 3000;
            BuilderUtil.SetRef(w, "streaks", ps);
            return w;
        }

        // ------------------------------------------------------------------ routes and combat

        /// <summary>A ring-route group (inactive until a mode switches it on).</summary>
        public static RaceRoute Route(string name, Transform parent, string routeKey)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var route = go.AddComponent<RaceRoute>();
            BuilderUtil.SetRef(route, "plane", Player());
            var so = new SerializedObject(route);
            so.FindProperty("routeKey").stringValue = routeKey;
            so.ApplyModifiedPropertiesWithoutUndo();
            return route;
        }

        public static void Ring(Transform route, string name, Vector3 pos, float yaw)
        {
            var prefab = Load<GameObject>("Prefabs/StickyRing.prefab");
            if (prefab == null) return;
            var ring = (GameObject)PrefabUtility.InstantiatePrefab(prefab, route);
            ring.name = name;
            ring.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        }

        public static EnemySpawner RivalSpawner(string name, Transform parent, Vector3 pos, float yaw, float firstDelay)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var spawner = go.AddComponent<EnemySpawner>();
            BuilderUtil.SetRef(spawner, "rivalPrefab", Load<GameObject>("Prefabs/RivalDart.prefab"));
            BuilderUtil.SetRef(spawner, "pilotSettings", Load<RivalPilotSettings>("Settings/Pilot_Rival.asset"));
            var so = new SerializedObject(spawner);
            so.FindProperty("firstSpawnDelay").floatValue = firstDelay;
            so.ApplyModifiedPropertiesWithoutUndo();
            return spawner;
        }

        // ------------------------------------------------------------------ rooms

        public static ModeDirector Director() => Object.FindAnyObjectByType<ModeDirector>(FindObjectsInactive.Include);

        /// <summary>Puts a room at a fixed index in the director's list (growing the list if needed).</summary>
        public static void SetRoom(int index, Room room)
        {
            var director = Director();
            if (director == null) { Debug.LogError("No ModeDirector in the scene (run Stage 12)."); return; }
            var so = new SerializedObject(director);
            var rooms = so.FindProperty("rooms");
            if (rooms.arraySize <= index) rooms.arraySize = index + 1;
            rooms.GetArrayElementAtIndex(index).objectReferenceValue = room;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static Room RoomAt(int index)
        {
            var director = Director();
            if (director == null) return null;
            var rooms = new SerializedObject(director).FindProperty("rooms");
            return index < rooms.arraySize ? rooms.GetArrayElementAtIndex(index).objectReferenceValue as Room : null;
        }
    }
}
