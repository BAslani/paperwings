using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using PaperWings.Flight;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// Stage 15: visual art pass.
    /// Menu: PaperWings > Build Stage 15 (Art Pass)
    ///
    /// Everything is procedural and applied IN PLACE (earlier stages stay intact):
    ///  - Paper stocks for every plane (lined, graph, cardstock, construction, sticky) + UVs on the meshes.
    ///  - Textured desk props: wood grain, woven carpet, code on the monitor, a spreadsheet on the laptop,
    ///    keyboard deck, notebook paper stack, page edges on the books, a glowing lamp bulb.
    ///  - Night-office mood: warmer lamp, cooler monitor glow, deeper ambient, floating dust motes.
    ///  - URP post-processing: bloom, filmic tone mapping, colour grade, vignette.
    /// Safe to re-run.
    /// </summary>
    public static class Stage15Builder
    {
        const string Art = "Assets/_Project/Art/";
        const string S = "Assets/_Project/Settings/";
        const string DeskScene = "Assets/_Project/Scenes/DeskPrototype.unity";
        const string TestScene = "Assets/_Project/Scenes/FlightTest.unity";

        [MenuItem("PaperWings/Build Stage 15 (Art Pass)", priority = 115)]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EditorUtility.DisplayProgressBar("PaperWings", "Painting textures...", 0.1f);
            try
            {
                var lined = TextureFactory.LinedPaper();
                var graph = TextureFactory.GraphPaper();
                var card = TextureFactory.Cardstock();
                var construction = TextureFactory.ConstructionPaper();
                var sticky = TextureFactory.StickyPaper();
                var wood = TextureFactory.WoodGrain();
                var carpet = TextureFactory.Carpet();
                var code = TextureFactory.CodeScreen();
                var sheet = TextureFactory.SpreadsheetScreen();
                var keys = TextureFactory.Keyboard();

                EditorUtility.DisplayProgressBar("PaperWings", "Refolding planes...", 0.4f);
                // Rebuild plane meshes with UVs (copied into the existing assets, so prefabs keep their links).
                BuilderUtil.SaveMesh(DartMesh.Build(), Art + "PaperDart_Mesh.asset");
                BuilderUtil.SaveMesh(FoldMeshes.BuildGlider(), Art + "PaperGlider_Mesh.asset");
                BuilderUtil.SaveMesh(FoldMeshes.BuildStunt(), Art + "PaperStunt_Mesh.asset");

                // Plane papers.
                Tex("M_Paper", lined, Vector2.one, Color.white);
                Tex("M_PaperRival", construction, Vector2.one, null);
                Tex("M_PaperWingman", sticky, Vector2.one, null);
                SetFoldPaper("Fold_Dart", lined);
                SetFoldPaper("Fold_Glider", graph);
                SetFoldPaper("Fold_Stunt", card);

                // Office.
                Tex("M_DeskWood", wood, new Vector2(3f, 1.5f), new Color(0.62f, 0.42f, 0.28f));
                Tex("M_Carpet", carpet, new Vector2(60f, 60f), Color.white);
                Tex("M_LaptopDeck", keys, Vector2.one, Color.white);
                Tex("M_PaperStack", lined, Vector2.one, Color.white);
                Screen("M_MonitorScreen", code, 1.4f);
                Screen("M_LaptopScreen", sheet, 0.9f);
                AssetDatabase.SaveAssets();

                EditorUtility.DisplayProgressBar("PaperWings", "Setting the night-office mood...", 0.6f);
                DressDesk();
                EditorUtility.DisplayProgressBar("PaperWings", "Grading the flight test...", 0.9f);
                DressFlightTest();
                AssetDatabase.SaveAssets();
            }
            finally { EditorUtility.ClearProgressBar(); }

            EditorUtility.DisplayDialog("PaperWings",
                "Stage 15 built: paper stocks, textured props, night-office lighting, dust motes and post-processing.\n\n" +
                "Tip: post-processing (bloom, colour grade) shows in the Game view. In the Scene view, turn it on with the " +
                "'Toggle effects' (layers) button in the Scene view toolbar.", "Admire!");
        }

        // ------------------------------------------------------------------ materials

        static void Tex(string matName, Texture2D tex, Vector2 tiling, Color? color)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(Art + matName + ".mat");
            if (mat == null) return;
            mat.SetTexture("_BaseMap", tex);
            mat.SetTextureScale("_BaseMap", tiling);
            if (color.HasValue) mat.SetColor("_BaseColor", color.Value);
            mat.SetFloat("_Smoothness", 0.12f);
            EditorUtility.SetDirty(mat);
        }

        /// <summary>A glowing screen: the image is both the colour and the emission, so it lights up and blooms.</summary>
        static void Screen(string matName, Texture2D tex, float brightness)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(Art + matName + ".mat");
            if (mat == null) return;
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
            mat.EnableKeyword("_EMISSION");
            mat.SetTexture("_EmissionMap", tex);
            mat.SetColor("_EmissionColor", Color.white * brightness);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            mat.SetFloat("_Smoothness", 0.6f);
            EditorUtility.SetDirty(mat);
        }

        static void SetFoldPaper(string foldName, Texture2D tex)
        {
            var fold = AssetDatabase.LoadAssetAtPath<PlaneFold>(S + foldName + ".asset");
            if (fold == null) return;
            fold.paperTexture = tex;
            EditorUtility.SetDirty(fold);
        }

        static Material Emissive(string name, Color baseColor, Color emission)
        {
            var m = BuilderUtil.CreateMaterial(name, baseColor);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------ desk scene

        static void DressDesk()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(DeskScene) == null) return;
            var scene = EditorSceneManager.OpenScene(DeskScene);

            // ---- Lighting: warm lamp pool, cool monitor, deep blue night ----
            SetLight(scene, "Moonlight", new Color(0.55f, 0.65f, 1f), 0.3f, null);
            SetLight(scene, "DeskLampLight", new Color(1f, 0.78f, 0.52f), 9f, 115f);
            SetLight(scene, "MonitorGlow", new Color(0.45f, 0.6f, 1f), 3.5f, 34f);
            SetLight(scene, "LaptopHeatGlow", new Color(1f, 0.55f, 0.3f), 1.6f, null);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.12f, 0.14f, 0.24f);
            RenderSettings.ambientEquatorColor = new Color(0.08f, 0.08f, 0.13f);
            RenderSettings.ambientGroundColor = new Color(0.03f, 0.03f, 0.05f);
            RenderSettings.fogColor = new Color(0.035f, 0.04f, 0.075f);

            var props = Find(scene, "Props");
            if (props != null) DecorateProps(props.transform);

            Replace(scene, "Ambience");
            var ambience = new GameObject("Ambience");
            MakeDustMotes(ambience.transform, new Vector3(0f, 52f, 0f), new Vector3(80f, 40f, 40f));

            AddPostFX(scene, "PostFX_Night", night: true);
            EditorSceneManager.SaveScene(scene);
        }

        static void DecorateProps(Transform props)
        {
            // Lamp bulb: a small glowing sphere just under the shade (bloom makes it glow).
            var shade = props.Find("LampShade");
            Remove(props, "LampBulb");
            if (shade != null)
            {
                var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bulb.name = "LampBulb";
                bulb.transform.SetParent(props, false);
                bulb.transform.position = shade.position + Vector3.down * 1.3f;
                bulb.transform.localScale = Vector3.one * 2.2f;
                Object.DestroyImmediate(bulb.GetComponent<Collider>());
                bulb.GetComponent<Renderer>().sharedMaterial = Emissive("M_LampBulb", new Color(1f, 0.9f, 0.7f), new Color(4f, 3f, 1.8f));
                bulb.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }

            // Page edges on the books: a thin white slab along one long side of each.
            var pages = BuilderUtil.CreateMaterial("M_PageEdges", new Color(0.93f, 0.91f, 0.85f));
            foreach (var bookName in new[] { "Book1", "Book2", "Book3" })
            {
                var book = props.Find(bookName);
                if (book == null) continue;
                Remove(book, "Pages");
                var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
                p.name = "Pages";
                p.transform.SetParent(book, false);
                p.transform.localPosition = new Vector3(0.502f, 0f, 0f);    // just outside the +X face
                p.transform.localScale = new Vector3(0.01f, 0.82f, 0.95f);  // fractions of the book's size
                Object.DestroyImmediate(p.GetComponent<Collider>());
                p.GetComponent<Renderer>().sharedMaterial = pages;
            }
        }

        static void MakeDustMotes(Transform parent, Vector3 centre, Vector3 size)
        {
            // Tiny specks drifting slowly through the air: the "it's 2 AM in the office" feeling.
            var go = new GameObject("DustMotes");
            go.transform.SetParent(parent, false);
            go.transform.position = centre;
            var mat = Stage2Builder.ParticleMaterial("M_Dust", Stage2Builder.SoftDotTexture(Art + "T_SoftDot.asset"), additive: true);
            var ps = Stage2Builder.AddParticles(go, mat, stretched: false, sizeMin: 0.05f, sizeMax: 0.13f);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(10f, 16f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.3f);
            main.startColor = new Color(1f, 0.92f, 0.8f, 0.35f);
            main.maxParticles = 500;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 30f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = size;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.15f;
            noise.scrollSpeed = 0.1f;
        }

        // ------------------------------------------------------------------ flight test

        static void DressFlightTest()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScene) == null) return;
            var scene = EditorSceneManager.OpenScene(TestScene);
            AddPostFX(scene, "PostFX_Day", night: false);
            EditorSceneManager.SaveScene(scene);
        }

        // ------------------------------------------------------------------ post-processing

        /// <summary>
        /// A global post-processing Volume plus a profile asset.
        /// UNITY NOTE: URP post-processing works through Volumes: a Volume holds a profile of effects
        /// (Bloom, Tonemapping...). "Global" means it applies everywhere. The camera must also have
        /// Post Processing ticked, which we do below.
        /// </summary>
        static void AddPostFX(Scene scene, string profileName, bool night)
        {
            string path = Art + profileName + ".asset";
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(path) != null) AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(night ? 0.85f : 1.1f);
            bloom.intensity.Override(night ? 0.9f : 0.35f);
            bloom.scatter.Override(0.7f);

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);

            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(night ? 0.45f : 0.15f);
            color.contrast.Override(night ? 14f : 8f);
            color.saturation.Override(night ? 10f : 6f);

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(night ? 0.3f : 0.18f);
            vignette.smoothness.Override(0.45f);

            if (night)
            {
                // Warm highlights, cool shadows: lamp-lit desk in a dark office.
                var split = profile.Add<SplitToning>(true);
                split.shadows.Override(new Color(0.35f, 0.45f, 0.75f));
                split.highlights.Override(new Color(1f, 0.85f, 0.65f));
                split.balance.Override(-15f);
            }

            foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            Replace(scene, "PostFX");
            var go = new GameObject("PostFX");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;

            // Make sure the camera actually renders post-processing.
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }

        // ------------------------------------------------------------------ helpers

        static void SetLight(Scene scene, string name, Color color, float intensity, float? range)
        {
            var go = Find(scene, name);
            if (go == null) return;
            var l = go.GetComponent<Light>();
            if (l == null) return;
            l.color = color;
            l.intensity = intensity;
            if (range.HasValue) l.range = range.Value;
            l.shadows = l.type == LightType.Point ? LightShadows.None : LightShadows.Soft;
        }

        static GameObject Find(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t.gameObject;
            }
            return null;
        }

        static void Replace(Scene scene, string rootName)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == rootName) Object.DestroyImmediate(root);
        }

        static void Remove(Transform parent, string childName)
        {
            var c = parent.Find(childName);
            if (c != null) Object.DestroyImmediate(c.gameObject);
        }
    }
}
