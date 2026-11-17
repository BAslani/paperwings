using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using PaperWings.CameraRig;
using PaperWings.Flight;
using PaperWings.Gameplay;
using PaperWings.UI;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// Builds the DeskPrototype scene: a graybox night-shift desk with a laptop thermal,
    /// desk fan, obstacles, and the coffee mug landing target on a book stack.
    /// Menu: PaperWings > Build Stage 3 (Desk Prototype Scene)
    ///
    /// The desk is deliberately oversized (70 x 34 units, 30 units above the floor) so a
    /// full-bank turn (~21 units across) fits over it. Props stay roughly at 10x scale,
    /// except the mug, which is enlarged so it's a fair landing target.
    ///
    /// Re-running regenerates the scene and the CoffeeMug prefab. Settings assets are kept.
    /// </summary>
    public static class Stage3Builder
    {
        const string Root = "Assets/_Project";
        const string ScenePath = Root + "/Scenes/DeskPrototype.unity";
        const string MugPrefabPath = Root + "/Prefabs/CoffeeMug.prefab";
        const string LandingSettingsPath = Root + "/Settings/Landing_Mug.asset";
        const string PlanePrefabPath = Root + "/Prefabs/PaperDart.prefab";
        const string FanPrefabPath = Root + "/Prefabs/DeskFan.prefab";
        const string ThermalPrefabPath = Root + "/Prefabs/LaptopThermal.prefab";

        const float DeskTop = 30f;

        [MenuItem("PaperWings/Build Stage 3 (Desk Prototype Scene)")]
        public static void Build()
        {
            var planePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlanePrefabPath);
            var fanPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FanPrefabPath);
            var thermalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ThermalPrefabPath);
            if (planePrefab == null || fanPrefab == null || thermalPrefab == null)
            {
                EditorUtility.DisplayDialog("PaperWings", "Run 'Build Stage 1' and 'Build Stage 2' first (missing prefabs).", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var landingSettings = Stage2Builder.LoadOrCreate<LandingSettings>(LandingSettingsPath);
            var mugPrefab = BuildMugPrefab(landingSettings);

            BuildScene(planePrefab, fanPrefab, thermalPrefab, mugPrefab);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("PaperWings",
                "Stage 3 built.\n\nScene: " + ScenePath + "\n\nGoal: land on the glowing coffee mug on top of the book stack (far right). " +
                "Ride the laptop thermal or surf the fan to get high enough.", "Fly!");
        }

        // ------------------------------------------------------------------ mug

        static GameObject BuildMugPrefab(LandingSettings settings)
        {
            var ceramic = BuilderUtil.CreateMaterial("M_MugCeramic", new Color(0.92f, 0.9f, 0.86f));
            var coffee = BuilderUtil.CreateMaterial("M_Coffee", new Color(0.22f, 0.13f, 0.07f));
            var marker = Emissive("M_TargetMarker", new Color(0.3f, 1f, 0.45f), new Color(0.2f, 1.2f, 0.35f));
            var beamMat = Stage2Builder.ParticleMaterial("M_TargetBeam", null, additive: true);
            beamMat.SetColor("_BaseColor", new Color(0.3f, 1f, 0.45f, 0.07f));
            var confettiMat = Stage2Builder.ParticleMaterial("M_Confetti", null, additive: false);

            var root = new GameObject("CoffeeMug");

            // Body: 6 wide, 5 tall. A convex MeshCollider gives a flat top to land on
            // (the cylinder primitive's default capsule collider would have a rounded top).
            var body = Stage2Builder.Prim(PrimitiveType.Cylinder, "Body", root.transform, new Vector3(0f, 2.5f, 0f), Vector3.zero, new Vector3(6f, 2.5f, 6f), ceramic);
            var mc = body.AddComponent<MeshCollider>();
            mc.sharedMesh = body.GetComponent<MeshFilter>().sharedMesh;
            mc.convex = true;

            Stage2Builder.Prim(PrimitiveType.Cylinder, "Coffee", root.transform, new Vector3(0f, 5.01f, 0f), Vector3.zero, new Vector3(5.3f, 0.02f, 5.3f), coffee);

            // Handle (three blocks on +X).
            Stage2Builder.Prim(PrimitiveType.Cube, "HandleTop", root.transform, new Vector3(3.5f, 3.9f, 0f), Vector3.zero, new Vector3(1.4f, 0.5f, 0.6f), ceramic);
            Stage2Builder.Prim(PrimitiveType.Cube, "HandleBottom", root.transform, new Vector3(3.5f, 1.3f, 0f), Vector3.zero, new Vector3(1.4f, 0.5f, 0.6f), ceramic);
            Stage2Builder.Prim(PrimitiveType.Cube, "HandleSide", root.transform, new Vector3(4.0f, 2.6f, 0f), Vector3.zero, new Vector3(0.5f, 3.1f, 0.6f), ceramic);

            // Glowing rim markers + a faint beam so the target reads from across the desk.
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI * 2f / 12f;
                Stage2Builder.Prim(PrimitiveType.Cube, "RimMarker", root.transform,
                    new Vector3(Mathf.Cos(a) * 3f, 5.1f, Mathf.Sin(a) * 3f), new Vector3(0f, -a * Mathf.Rad2Deg, 0f),
                    new Vector3(0.35f, 0.2f, 0.35f), marker);
            }
            Stage2Builder.Prim(PrimitiveType.Cylinder, "Beam", root.transform, new Vector3(0f, 20f, 0f), Vector3.zero, new Vector3(6.4f, 15f, 6.4f), beamMat);

            // Confetti burst for successful landings.
            var confettiGo = new GameObject("Confetti");
            confettiGo.transform.SetParent(root.transform, false);
            confettiGo.transform.localPosition = new Vector3(0f, 5.5f, 0f);
            confettiGo.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var confetti = Stage2Builder.AddParticles(confettiGo, confettiMat, stretched: false, sizeMin: 0.25f, sizeMax: 0.5f);
            ConfigureConfetti(confetti);

            var zone = root.AddComponent<LandingZone>();
            BuilderUtil.SetRef(zone, "settings", settings);
            BuilderUtil.SetRef(zone, "celebration", confetti);
            SetFloat(zone, "surfaceHeight", 5f);
            SetFloat(zone, "surfaceRadius", 3f);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, MugPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static void ConfigureConfetti(ParticleSystem ps)
        {
            var main = ps.main;
            main.loop = false;
            main.prewarm = false;
            main.playOnAwake = false;
            main.duration = 1f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(8f, 15f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 3.5f);
            main.gravityModifier = 1.2f;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var colors = new Gradient();
            colors.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.35f, 0.5f), 0f),
                    new GradientColorKey(new Color(1f, 0.85f, 0.2f), 0.33f),
                    new GradientColorKey(new Color(0.3f, 0.8f, 1f), 0.66f),
                    new GradientColorKey(new Color(0.5f, 1f, 0.4f), 1f)
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            main.startColor = new ParticleSystem.MinMaxGradient(colors) { mode = ParticleSystemGradientMode.RandomColor };

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 120) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 1f;

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);   // radians/sec: tumbling paper bits

            // Solid until the end, then fade.
            var col = ps.colorOverLifetime;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            col.color = fade;
        }

        // ------------------------------------------------------------------ scene

        static void BuildScene(GameObject planePrefab, GameObject fanPrefab, GameObject thermalPrefab, GameObject mugPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupNightLighting();

            // ---- Materials ----
            var wood = BuilderUtil.CreateMaterial("M_DeskWood", new Color(0.42f, 0.28f, 0.18f));
            var metal = BuilderUtil.CreateMaterial("M_DarkMetal", new Color(0.12f, 0.12f, 0.14f));
            var plastic = BuilderUtil.CreateMaterial("M_Plastic", new Color(0.2f, 0.21f, 0.24f));
            var paper = BuilderUtil.CreateMaterial("M_PaperStack", new Color(0.9f, 0.9f, 0.85f));
            var screen = Emissive("M_MonitorScreen", new Color(0.3f, 0.45f, 0.7f), new Color(0.15f, 0.3f, 0.6f));
            var bookA = BuilderUtil.CreateMaterial("M_BookRed", new Color(0.6f, 0.15f, 0.15f));
            var bookB = BuilderUtil.CreateMaterial("M_BookBlue", new Color(0.15f, 0.25f, 0.55f));
            var bookC = BuilderUtil.CreateMaterial("M_BookGreen", new Color(0.2f, 0.45f, 0.25f));
            var cupMat = BuilderUtil.CreateMaterial("M_PenCup", new Color(0.25f, 0.5f, 0.55f));
            var penMat = BuilderUtil.CreateMaterial("M_Pen", new Color(0.9f, 0.75f, 0.2f));
            var lampMat = BuilderUtil.CreateMaterial("M_Lamp", new Color(0.15f, 0.15f, 0.15f));

            var carpetTex = BuilderUtil.CheckerTexture(Root + "/Art/T_Carpet.asset",
                new Color(0.16f, 0.17f, 0.22f), new Color(0.13f, 0.14f, 0.18f));
            var carpet = BuilderUtil.CreateMaterial("M_Carpet", Color.white, carpetTex, new Vector2(40, 40));

            // ---- Floor (far below) ----
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(40f, 1f, 40f);   // 400 x 400
            floor.GetComponent<Renderer>().sharedMaterial = carpet;

            // ---- Desk ----
            var desk = new GameObject("Desk").transform;
            Box("DeskTop", desk, new Vector3(0f, DeskTop - 1f, 0f), new Vector3(70f, 2f, 34f), wood);
            foreach (var (x, z) in new[] { (-33f, -15f), (33f, -15f), (-33f, 15f), (33f, 15f) })
                Box("Leg", desk, new Vector3(x, (DeskTop - 2f) * 0.5f, z), new Vector3(2.5f, DeskTop - 2f, 2.5f), metal);

            // ---- Props / obstacles (all sitting on the desk top) ----
            var props = new GameObject("Props").transform;

            // Monitor at the back: a big wall to fly around (or over).
            Box("MonitorBase", props, P(0f, 0.3f, 13f), new Vector3(7f, 0.6f, 4f), plastic);
            Box("MonitorNeck", props, P(0f, 3.3f, 13.8f), new Vector3(1.2f, 6f, 1f), plastic);
            Box("MonitorScreen", props, P(0f, 10f, 13f), new Vector3(24f, 14f, 0.8f), screen);

            Box("Keyboard", props, P(-1f, 0.3f, -6f), new Vector3(16f, 0.6f, 5f), plastic);
            Box("Mouse", props, P(10f, 0.4f, -6f), new Vector3(1.4f, 0.8f, 2.2f), plastic);
            Box("PaperStack", props, P(-5f, 0.5f, 6f), new Vector3(9f, 1f, 6f), paper).transform.rotation = Quaternion.Euler(0f, 8f, 0f);

            // Pen cup with pens.
            Stage2Builder.Prim(PrimitiveType.Cylinder, "PenCup", props, P(-26f, 2.75f, 11f), Vector3.zero, new Vector3(2.6f, 2.75f, 2.6f), cupMat, keepCollider: true);
            for (int i = 0; i < 3; i++)
                Stage2Builder.Prim(PrimitiveType.Cylinder, "Pen", props, P(-26f + (i - 1) * 0.6f, 6.5f, 11f + (i - 1) * 0.3f),
                    new Vector3((i - 1) * 12f, 0f, (i - 1) * -10f), new Vector3(0.3f, 2.2f, 0.3f), penMat);

            // Book stack, top at DeskTop + 11. The mug sits on top.
            Box("Book1", props, P(26f, 1.75f, -8f), new Vector3(10f, 3.5f, 13f), bookA).transform.rotation = Quaternion.Euler(0f, 4f, 0f);
            Box("Book2", props, P(26f, 5.25f, -8f), new Vector3(9f, 3.5f, 12f), bookB).transform.rotation = Quaternion.Euler(0f, -6f, 0f);
            Box("Book3", props, P(26f, 9f, -8f), new Vector3(9.5f, 4f, 12.5f), bookC).transform.rotation = Quaternion.Euler(0f, 2f, 0f);

            // Desk lamp at the back-right corner (also the main light).
            Stage2Builder.Prim(PrimitiveType.Cylinder, "LampBase", props, P(32f, 0.3f, 15f), Vector3.zero, new Vector3(4f, 0.3f, 4f), lampMat, keepCollider: true);
            Stage2Builder.Prim(PrimitiveType.Cylinder, "LampPole", props, P(32f, 12.5f, 15f), Vector3.zero, new Vector3(0.8f, 12.5f, 0.8f), lampMat, keepCollider: true);
            Box("LampArm", props, P(25f, 25f, 8.5f), new Vector3(0.7f, 0.7f, 19f), lampMat).transform.rotation = Quaternion.Euler(0f, -133f, 0f);
            var shade = Stage2Builder.Prim(PrimitiveType.Cylinder, "LampShade", props, P(18f, 24f, 2f), Vector3.zero, new Vector3(5f, 1.2f, 5f), lampMat, keepCollider: true);
            shade.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;   // don't block our own lamp

            // ---- Air sources & target ----
            var gameplay = new GameObject("Gameplay").transform;

            var laptop = (GameObject)PrefabUtility.InstantiatePrefab(thermalPrefab, gameplay);
            laptop.transform.SetPositionAndRotation(P(-14f, 0f, -6f), Quaternion.Euler(0f, 0f, 0f));

            // Fan behind/right of the monitor, aimed toward the mug and tilted up 15 degrees,
            // so you can surf its stream across the desk.
            var fan = (GameObject)PrefabUtility.InstantiatePrefab(fanPrefab, gameplay);
            fan.transform.SetPositionAndRotation(P(14f, 0f, 13f), Quaternion.Euler(0f, 150f, 0f));
            var head = fan.transform.Find("Head");
            if (head != null) head.localRotation = Quaternion.Euler(-15f, 0f, 0f);

            var mug = (GameObject)PrefabUtility.InstantiatePrefab(mugPrefab, gameplay);
            mug.transform.SetPositionAndRotation(P(26f, 11f, -8f), Quaternion.Euler(0f, -30f, 0f));

            // ---- Lights for the props ----
            PointLight("MonitorGlow", P(0f, 10f, 9f), new Color(0.45f, 0.6f, 1f), 2f, 28f);
            PointLight("LaptopHeatGlow", P(-14f, 3f, -6f), new Color(1f, 0.55f, 0.25f), 2f, 16f);
            PointLight("MugGlow", P(26f, 18f, -8f), new Color(0.4f, 1f, 0.5f), 1.5f, 14f);
            var lamp = new GameObject("DeskLampLight").AddComponent<Light>();
            lamp.type = LightType.Spot;
            lamp.color = new Color(1f, 0.85f, 0.6f);
            lamp.intensity = 6f;
            lamp.range = 100f;
            lamp.spotAngle = 120f;
            lamp.shadows = LightShadows.Soft;
            lamp.transform.position = P(18f, 22.5f, 2f);   // just under the shade
            lamp.transform.LookAt(P(-4f, 0f, -2f));

            // ---- Player ----
            var spawn = new GameObject("SpawnPoint").transform;
            spawn.position = P(-30f, 3.5f, -10f);
            spawn.rotation = Quaternion.Euler(0f, 76f, 0f);   // facing the laptop thermal
            CreatePlayerRig(planePrefab, spawn);

            EditorSceneManager.SaveScene(scene, ScenePath);
            BuilderUtil.AddSceneToBuildSettings(ScenePath);
            Selection.activeObject = mug;
        }

        static void CreatePlayerRig(GameObject planePrefab, Transform spawn)
        {
            var plane = (GameObject)PrefabUtility.InstantiatePrefab(planePrefab);
            plane.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
            var controller = plane.GetComponent<PlaneController>();
            var input = plane.GetComponent<PlaneInput>();

            var flow = new GameObject("GameFlow");
            var launcher = flow.AddComponent<LaunchAndReset>();
            BuilderUtil.SetRef(launcher, "plane", controller);
            BuilderUtil.SetRef(launcher, "input", input);
            BuilderUtil.SetRef(launcher, "spawnPoint", spawn);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1000f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fogColor;
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = spawn.position + spawn.rotation * new Vector3(0f, 2.2f, -7f);
            camGo.transform.LookAt(spawn.position);
            var chase = camGo.AddComponent<SimpleChaseCamera>();
            BuilderUtil.SetRef(chase, "target", plane.GetComponent<Rigidbody>());

            var hud = new GameObject("DebugHUD").AddComponent<DebugHUD>();
            BuilderUtil.SetRef(hud, "plane", controller);
            BuilderUtil.SetRef(hud, "launcher", launcher);
        }

        static void SetupNightLighting()
        {
            // Dim blue "after hours" ambient, a weak moon, and warm/cool practical lights on the desk.
            var moon = new GameObject("Moonlight").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.6f, 0.7f, 1f);
            moon.intensity = 0.45f;
            moon.shadows = LightShadows.Soft;
            moon.transform.rotation = Quaternion.Euler(40f, 200f, 0f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.18f, 0.2f, 0.3f);
            RenderSettings.ambientEquatorColor = new Color(0.11f, 0.11f, 0.16f);
            RenderSettings.ambientGroundColor = new Color(0.05f, 0.05f, 0.07f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.04f, 0.05f, 0.09f);
            RenderSettings.fogStartDistance = 120f;
            RenderSettings.fogEndDistance = 450f;
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Position relative to the desk top (y = height above the desk surface).</summary>
        static Vector3 P(float x, float yAboveDesk, float z) => new Vector3(x, DeskTop + yAboveDesk, z);

        static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static void PointLight(string name, Vector3 pos, Color color, float intensity, float range)
        {
            var l = new GameObject(name).AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.transform.position = pos;
        }

        static Material Emissive(string name, Color baseColor, Color emission)
        {
            var m = BuilderUtil.CreateMaterial(name, baseColor);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(m);
            return m;
        }

        static void SetFloat(Object component, string field, float value)
        {
            var so = new SerializedObject(component);
            so.FindProperty(field).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
