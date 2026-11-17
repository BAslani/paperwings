using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using PaperWings.Air;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// Builds the DeskFan and LaptopThermal prefabs (with particle cues) and their settings assets,
    /// then places them in the FlightTest scene under an "AirTest" object.
    /// Menu: PaperWings > Build Stage 2 (Air Sources in Flight Test)
    ///
    /// Safe to re-run: only the "AirTest" object in the scene is replaced, and existing
    /// Fan/Thermal settings assets are never overwritten.
    /// </summary>
    public static class Stage2Builder
    {
        const string Root = "Assets/_Project";
        const string FanSettingsPath = Root + "/Settings/Fan_Desk.asset";
        const string ThermalSettingsPath = Root + "/Settings/Thermal_Laptop.asset";
        const string FanPrefabPath = Root + "/Prefabs/DeskFan.prefab";
        const string ThermalPrefabPath = Root + "/Prefabs/LaptopThermal.prefab";
        const string FlightTestScene = Root + "/Scenes/FlightTest.unity";

        [MenuItem("PaperWings/Build Stage 2 (Air Sources in Flight Test)")]
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(FlightTestScene) == null)
            {
                EditorUtility.DisplayDialog("PaperWings", "Run 'Build Stage 1' first (FlightTest scene not found).", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var fanSettings = LoadOrCreate<FanSettings>(FanSettingsPath);
            var thermalSettings = LoadOrCreate<ThermalSettings>(ThermalSettingsPath);

            var dot = SoftDotTexture(Root + "/Art/T_SoftDot.asset");
            var streakMat = ParticleMaterial("M_WindStreak", dot, additive: true);
            var shimmerMat = ParticleMaterial("M_HeatShimmer", dot, additive: false);

            var fanPrefab = BuildFanPrefab(fanSettings, streakMat);
            var thermalPrefab = BuildThermalPrefab(thermalSettings, shimmerMat);

            PlaceInFlightTest(fanPrefab, thermalPrefab);
            AssetDatabase.SaveAssets();   // write material/texture changes to disk

            EditorUtility.DisplayDialog("PaperWings",
                "Stage 2 built.\n\nFlightTest now has a desk fan (on the right, blowing across your path) " +
                "and a laptop thermal straight ahead. Press Play!", "Fly!");
        }

        // ------------------------------------------------------------------ prefabs

        static GameObject BuildFanPrefab(FanSettings settings, Material streakMat)
        {
            var bodyMat = BuilderUtil.CreateMaterial("M_FanBody", new Color(0.25f, 0.27f, 0.3f));
            var bladeMat = BuilderUtil.CreateMaterial("M_FanBlade", new Color(0.55f, 0.75f, 0.9f));

            var root = new GameObject("DeskFan");

            // Sizes are in game units (1 unit = 10 cm): a ~40 cm desk fan.
            Prim(PrimitiveType.Cylinder, "Base", root.transform, new Vector3(0f, 0.1f, 0f), Vector3.zero, new Vector3(1.8f, 0.1f, 1.8f), bodyMat);
            Prim(PrimitiveType.Cylinder, "Pole", root.transform, new Vector3(0f, 1.3f, 0f), Vector3.zero, new Vector3(0.25f, 1.2f, 0.25f), bodyMat);

            var head = new GameObject("Head").transform;
            head.SetParent(root.transform, false);
            head.localPosition = new Vector3(0f, 2.7f, 0f);

            // Cylinders point along Y; rotating 90 on X points them along the fan's forward axis.
            Prim(PrimitiveType.Cylinder, "Motor", head, new Vector3(0f, 0f, -0.5f), new Vector3(90f, 0f, 0f), new Vector3(0.7f, 0.35f, 0.7f), bodyMat);
            Prim(PrimitiveType.Cylinder, "BackPlate", head, new Vector3(0f, 0f, -0.15f), new Vector3(90f, 0f, 0f), new Vector3(2.6f, 0.03f, 2.6f), bodyMat);

            var blades = new GameObject("Blades").transform;
            blades.SetParent(head, false);
            blades.localPosition = new Vector3(0f, 0f, 0.05f);
            Prim(PrimitiveType.Cylinder, "Hub", blades, Vector3.zero, new Vector3(90f, 0f, 0f), new Vector3(0.3f, 0.08f, 0.3f), bodyMat);
            for (int i = 0; i < 3; i++)
            {
                var pivot = new GameObject("BladePivot").transform;
                pivot.SetParent(blades, false);
                pivot.localRotation = Quaternion.Euler(0f, 0f, i * 120f);
                Prim(PrimitiveType.Cube, "Blade", pivot, new Vector3(0f, 0.6f, 0f), new Vector3(0f, 20f, 0f), new Vector3(0.45f, 1.0f, 0.03f), bladeMat);
            }

            var streakGo = new GameObject("WindStreaks");
            streakGo.transform.SetParent(head, false);
            streakGo.transform.localPosition = new Vector3(0f, 0f, 0.3f);
            var streaks = AddParticles(streakGo, streakMat, stretched: true, sizeMin: 0.06f, sizeMax: 0.14f);

            // One simple collider for the whole fan, instead of one per decorative part.
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 2.0f, 0f);
            col.size = new Vector3(2.6f, 4.0f, 1.6f);

            var fan = root.AddComponent<FanSource>();
            BuilderUtil.SetRef(fan, "settings", settings);
            BuilderUtil.SetRef(fan, "head", head);
            BuilderUtil.SetRef(fan, "blades", blades);
            BuilderUtil.SetRef(fan, "streaks", streaks);
            fan.SyncVisuals();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, FanPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static GameObject BuildThermalPrefab(ThermalSettings settings, Material shimmerMat)
        {
            var deckMat = BuilderUtil.CreateMaterial("M_LaptopDeck", new Color(0.3f, 0.3f, 0.33f));
            var screenMat = BuilderUtil.CreateMaterial("M_LaptopScreen", new Color(0.55f, 0.75f, 1f));
            screenMat.EnableKeyword("_EMISSION");
            screenMat.SetColor("_EmissionColor", new Color(0.25f, 0.4f, 0.7f));
            var heatMat = BuilderUtil.CreateMaterial("M_HeatVent", new Color(1f, 0.45f, 0.15f));

            var root = new GameObject("LaptopThermal");

            // A ~34 x 24 cm laptop. Colliders from the primitives are kept (they're simple boxes).
            Prim(PrimitiveType.Cube, "Deck", root.transform, new Vector3(0f, 0.075f, 0f), Vector3.zero, new Vector3(3.4f, 0.15f, 2.4f), deckMat, keepCollider: true);
            // A glowing strip on the deck hints "this is hot".
            Prim(PrimitiveType.Cube, "HeatStrip", root.transform, new Vector3(0f, 0.155f, 0.6f), Vector3.zero, new Vector3(2.6f, 0.01f, 0.4f), heatMat);

            var hinge = new GameObject("ScreenHinge").transform;
            hinge.SetParent(root.transform, false);
            hinge.localPosition = new Vector3(0f, 0.15f, 1.15f);
            hinge.localRotation = Quaternion.Euler(15f, 0f, 0f);   // tilted back
            Prim(PrimitiveType.Cube, "Screen", hinge, new Vector3(0f, 1.1f, 0f), Vector3.zero, new Vector3(3.4f, 2.2f, 0.08f), screenMat, keepCollider: true);

            var shimmerGo = new GameObject("HeatShimmer");
            shimmerGo.transform.SetParent(root.transform, false);
            shimmerGo.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            shimmerGo.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);   // particle systems emit along +Z; point it up
            var shimmer = AddParticles(shimmerGo, shimmerMat, stretched: false, sizeMin: 0.6f, sizeMax: 1.4f);

            // Wobbly drift = cheap "shimmer".
            var noise = shimmer.noise;
            noise.enabled = true;
            noise.strength = 0.8f;
            noise.frequency = 0.3f;
            noise.scrollSpeed = 0.4f;

            var thermal = root.AddComponent<ThermalSource>();
            BuilderUtil.SetRef(thermal, "settings", settings);
            BuilderUtil.SetRef(thermal, "shimmer", shimmer);
            thermal.SyncVisuals();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, ThermalPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ------------------------------------------------------------------ scene

        static void PlaceInFlightTest(GameObject fanPrefab, GameObject thermalPrefab)
        {
            var scene = EditorSceneManager.OpenScene(FlightTestScene);
            var old = GameObject.Find("AirTest");
            if (old != null) Object.DestroyImmediate(old);

            var root = new GameObject("AirTest").transform;
            var pedestalMat = BuilderUtil.CreateMaterial("M_Pedestal", new Color(0.45f, 0.42f, 0.5f));

            // Fan: on a 30-unit pedestal to the right of the launch line, blowing left (-X) across it,
            // tilted up 10 degrees so its stream crosses your glide path.
            Prim(PrimitiveType.Cube, "FanPedestal", root, new Vector3(25f, 15f, 40f), Vector3.zero, new Vector3(4f, 30f, 4f), pedestalMat, keepCollider: true);
            var fan = (GameObject)PrefabUtility.InstantiatePrefab(fanPrefab, root);
            fan.transform.SetPositionAndRotation(new Vector3(25f, 30f, 40f), Quaternion.Euler(-10f, -90f, 0f));

            // Thermal: on a 12-unit pedestal straight ahead. You'll glide into it ~7 seconds after launch.
            Prim(PrimitiveType.Cube, "ThermalPedestal", root, new Vector3(0f, 6f, 100f), Vector3.zero, new Vector3(8f, 12f, 8f), pedestalMat, keepCollider: true);
            var thermal = (GameObject)PrefabUtility.InstantiatePrefab(thermalPrefab, root);
            thermal.transform.SetPositionAndRotation(new Vector3(0f, 12f, 100f), Quaternion.Euler(0f, 180f, 0f));

            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = fan;
        }

        // ------------------------------------------------------------------ helpers

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return asset;
        }

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localEuler,
                               Vector3 scale, Material mat, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static ParticleSystem AddParticles(GameObject go, Material mat, bool stretched, float sizeMin, float sizeMax)
        {
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.loop = true;
            main.prewarm = true;        // already flowing when the scene starts
            main.playOnAwake = true;
            main.maxParticles = 1500;
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);

            // Fade in and out over each particle's life so streams have soft ends.
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (stretched)
            {
                // Stretched billboards turn dots into speed lines.
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.velocityScale = 0.12f;
                r.lengthScale = 1.5f;
            }
            else
            {
                r.renderMode = ParticleSystemRenderMode.Billboard;
            }
            return ps;
        }

        static Texture2D SoftDotTexture(string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "SoftDot", wrapMode = TextureWrapMode.Clamp };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                px[y * size + x] = new Color(1f, 1f, 1f, a * a);
            }
            tex.SetPixels(px);
            tex.Apply(true);
            AssetDatabase.CreateAsset(tex, path);
            return tex;
        }

        static Material ParticleMaterial(string name, Texture2D tex, bool additive)
        {
            string path = $"{Root}/Art/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);

            // URP transparent setup (what the material Inspector does when you pick Surface Type = Transparent).
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", additive ? 2f : 0f);   // 0 = Alpha, 2 = Additive
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
