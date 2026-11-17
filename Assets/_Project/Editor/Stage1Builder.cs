using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using PaperWings.CameraRig;
using PaperWings.Flight;
using PaperWings.Gameplay;
using PaperWings.UI;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// Builds the Stage 1 flight test scene, the paper dart prefab, and the assets they need.
    /// Menu: PaperWings > Build Stage 1 (Flight Test Scene)
    ///
    /// Safe to run again: meshes, materials, prefab and scene are regenerated, but an existing
    /// FlightSettings asset is NEVER overwritten, so your tuning survives.
    ///
    /// UNITY NOTE: Scripts inside any folder named "Editor" only exist in the Editor and are
    /// stripped from builds. [MenuItem] adds an entry to the top menu bar.
    /// </summary>
    public static class Stage1Builder
    {
        const string Root = "Assets/_Project";
        const string SettingsPath = Root + "/Settings/FlightSettings_Dart.asset";
        const string ActionsPath = Root + "/Input/PaperWings.inputactions";
        const string PrefabPath = Root + "/Prefabs/PaperDart.prefab";
        const string ScenePath = Root + "/Scenes/FlightTest.unity";

        [MenuItem("PaperWings/Build Stage 1 (Flight Test Scene)")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var settings = LoadOrCreateSettings();
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            if (actions == null)
            {
                EditorUtility.DisplayDialog("PaperWings", "Couldn't find " + ActionsPath, "OK");
                return;
            }

            var paperMat = BuilderUtil.CreateMaterial("M_Paper", new Color(0.96f, 0.95f, 0.9f));
            var dartMesh = BuilderUtil.SaveMesh(DartMesh.Build(), Root + "/Art/PaperDart_Mesh.asset");
            var prefab = BuildPlanePrefab(settings, actions, dartMesh, paperMat);

            BuildScene(prefab);

            EditorUtility.DisplayDialog("PaperWings",
                "Stage 1 built.\n\nScene: " + ScenePath + "\nPress Play, hold SPACE, release to throw.", "Fly!");
        }

        static FlightSettings LoadOrCreateSettings()
        {
            var s = AssetDatabase.LoadAssetAtPath<FlightSettings>(SettingsPath);
            if (s != null) return s;
            s = ScriptableObject.CreateInstance<FlightSettings>();
            AssetDatabase.CreateAsset(s, SettingsPath);
            AssetDatabase.SaveAssets();
            return s;
        }

        static GameObject BuildPlanePrefab(FlightSettings settings, InputActionAsset actions, Mesh mesh, Material mat)
        {
            var root = new GameObject("PaperDart");

            // Physics body. Speculative CCD handles fast, thin objects and works when kinematic too.
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 1f;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;   // smooth rendering between physics ticks
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.isKinematic = true;

            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, -0.05f, 0f);
            col.size = new Vector3(1.4f, 0.3f, 2f);

            // Visual child (wobble rotates this, not the physics root).
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            visual.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = visual.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;

            var input = root.AddComponent<PlaneInput>();
            var controller = root.AddComponent<PlaneController>();
            var wobble = root.AddComponent<PaperWobble>();

            BuilderUtil.SetRef(input, "actions", actions);
            BuilderUtil.SetRef(controller, "settings", settings);
            BuilderUtil.SetRef(controller, "input", input);
            BuilderUtil.SetRef(wobble, "plane", controller);
            BuilderUtil.SetRef(wobble, "visual", visual.transform);
            PlaneFxBuilder.AddTrails(root);   // wingtip trails (Stage 4)

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static void BuildScene(GameObject planePrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuilderUtil.SetupDaylight();

            // ---- Ground: huge checkerboard so you can read speed and height ----
            var checker = BuilderUtil.CheckerTexture(Root + "/Art/T_Checker.asset",
                new Color(0.55f, 0.62f, 0.55f), new Color(0.47f, 0.54f, 0.47f));
            var groundMat = BuilderUtil.CreateMaterial("M_Ground", Color.white, checker, new Vector2(100, 100));
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(100f, 1f, 100f);   // Plane primitive is 10x10 -> 1000x1000
            ground.GetComponent<Renderer>().sharedMaterial = groundMat;

            // ---- Launch tower ----
            var towerMat = BuilderUtil.CreateMaterial("M_Tower", new Color(0.75f, 0.55f, 0.35f));
            var tower = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tower.name = "LaunchTower";
            tower.transform.position = new Vector3(0f, 20f, 0f);
            tower.transform.localScale = new Vector3(6f, 40f, 6f);
            tower.GetComponent<Renderer>().sharedMaterial = towerMat;

            // ---- Reference pillars to fly around (seeded so the layout is stable) ----
            var pillarMat = BuilderUtil.CreateMaterial("M_Pillar", new Color(0.6f, 0.62f, 0.7f));
            var pillars = new GameObject("Pillars").transform;
            var rng = new System.Random(7);
            for (int i = 0; i < 40; i++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float dist = 40f + (float)rng.NextDouble() * 220f;
                float height = 8f + (float)rng.NextDouble() * 35f;
                float width = 3f + (float)rng.NextDouble() * 5f;
                var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
                p.name = "Pillar";
                p.transform.SetParent(pillars, false);
                p.transform.position = new Vector3(Mathf.Cos(angle) * dist, height * 0.5f, Mathf.Sin(angle) * dist);
                p.transform.localScale = new Vector3(width, height, width);
                p.GetComponent<Renderer>().sharedMaterial = pillarMat;
            }

            // ---- Spawn point & plane ----
            var spawn = new GameObject("SpawnPoint").transform;
            spawn.position = new Vector3(0f, 41.5f, 2f);
            spawn.rotation = Quaternion.identity;   // throw toward +Z

            var plane = (GameObject)PrefabUtility.InstantiatePrefab(planePrefab);
            plane.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
            var controller = plane.GetComponent<PlaneController>();
            var input = plane.GetComponent<PlaneInput>();

            // ---- Game flow (launch + reset) ----
            var flow = new GameObject("GameFlow");
            var launcher = flow.AddComponent<LaunchAndReset>();
            BuilderUtil.SetRef(launcher, "plane", controller);
            BuilderUtil.SetRef(launcher, "input", input);
            BuilderUtil.SetRef(launcher, "spawnPoint", spawn);

            // ---- Camera (Cinemachine chase rig) ----
            CameraRigBuilder.CreateChaseCamera(controller, spawn, farClip: 2000f);

            // ---- HUD ----
            var hud = new GameObject("DebugHUD").AddComponent<DebugHUD>();
            BuilderUtil.SetRef(hud, "plane", controller);
            BuilderUtil.SetRef(hud, "launcher", launcher);

            EditorSceneManager.SaveScene(scene, ScenePath);
            BuilderUtil.AddSceneToBuildSettings(ScenePath);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<FlightSettings>(SettingsPath);
        }
    }

    /// <summary>Small helpers shared by the stage builders.</summary>
    public static class BuilderUtil
    {
        public static void SetRef(Object component, string field, Object value)
        {
            // SerializedObject edits private [SerializeField] fields the same way the Inspector does.
            var so = new SerializedObject(component);
            var prop = so.FindProperty(field);
            if (prop == null) { Debug.LogError($"Field '{field}' not found on {component.GetType().Name}"); return; }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static Material CreateMaterial(string name, Color color, Texture tex = null, Vector2? tiling = null)
        {
            string path = $"Assets/_Project/Art/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            if (tex != null)
            {
                mat.SetTexture("_BaseMap", tex);
                mat.SetTextureScale("_BaseMap", tiling ?? Vector2.one);
            }
            mat.SetFloat("_Smoothness", 0.15f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static Mesh SaveMesh(Mesh mesh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                // Copy into the existing asset so prefab references stay valid.
                existing.Clear();
                existing.vertices = mesh.vertices;
                existing.normals = mesh.normals;
                existing.triangles = mesh.triangles;
                existing.RecalculateBounds();
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        public static Texture2D CheckerTexture(string path, Color a, Color b)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Checker", anisoLevel = 8 };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = ((x / 32) + (y / 32)) % 2 == 0 ? a : b;
            tex.SetPixels(px);
            tex.Apply(true);
            AssetDatabase.CreateAsset(tex, path);
            return tex;
        }

        public static void SetupDaylight()
        {
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.85f);
            RenderSettings.ambientEquatorColor = new Color(0.5f, 0.52f, 0.5f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.28f, 0.25f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.72f, 0.8f, 0.9f);
            RenderSettings.fogStartDistance = 150f;
            RenderSettings.fogEndDistance = 700f;
        }

        public static void AddSceneToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == path)) return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
