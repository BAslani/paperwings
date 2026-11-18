using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using PaperWings.Air;
using PaperWings.Flight;
using PaperWings.Gameplay;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// Stage 5: more air variety + a timed ring route in the desk scene.
    /// Menu: PaperWings > Build Stage 5 (Steam, Vent, Ring Route)
    ///
    /// Builds TeaSteam, CeilingVent and StickyRing prefabs and their settings assets, then adds
    /// a "Stage5" group to DeskPrototype: a ceiling with an AC vent (downdraft), a steaming tea mug,
    /// and a 5-ring route that ends with landing on the coffee mug.
    /// Re-running only replaces the "Stage5" group; settings assets are kept.
    /// </summary>
    public static class Stage5Builder
    {
        const string Root = "Assets/_Project";
        const string DeskScene = Root + "/Scenes/DeskPrototype.unity";
        const string SteamSettingsPath = Root + "/Settings/Steam_Tea.asset";
        const string VentSettingsPath = Root + "/Settings/Vent_Ceiling.asset";
        const string TeaPrefabPath = Root + "/Prefabs/TeaSteam.prefab";
        const string VentPrefabPath = Root + "/Prefabs/CeilingVent.prefab";
        const string RingPrefabPath = Root + "/Prefabs/StickyRing.prefab";

        const float DeskTop = 30f;
        const float CeilingY = 95f;

        [MenuItem("PaperWings/Build Stage 5 (Steam, Vent, Ring Route)", priority = 105)]
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(DeskScene) == null)
            {
                EditorUtility.DisplayDialog("PaperWings", "Run 'Build Stage 3' first (DeskPrototype scene not found).", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var steamSettings = Stage2Builder.LoadOrCreate<SteamSettings>(SteamSettingsPath);
            var ventSettings = Stage2Builder.LoadOrCreate<VentSettings>(VentSettingsPath);

            var dot = Stage2Builder.SoftDotTexture(Root + "/Art/T_SoftDot.asset");
            var steamMat = Stage2Builder.ParticleMaterial("M_Steam", dot, additive: false);
            var ventMat = Stage2Builder.ParticleMaterial("M_VentStreak", dot, additive: true);

            var teaPrefab = BuildTeaPrefab(steamSettings, steamMat);
            var ventPrefab = BuildVentPrefab(ventSettings, ventMat);
            var ringPrefab = BuildRingPrefab();

            PlaceInDeskScene(teaPrefab, ventPrefab, ringPrefab);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("PaperWings",
                "Stage 5 built.\n\nDeskPrototype now has a ceiling AC vent (downdraft), a steaming tea mug, " +
                "and a 5-ring route. Fly through the glowing ring, follow the arrows, then land on the coffee mug " +
                "to set a time.", "Fly!");
        }

        // ------------------------------------------------------------------ prefabs

        static GameObject BuildTeaPrefab(SteamSettings settings, Material steamMat)
        {
            var ceramic = BuilderUtil.CreateMaterial("M_TeaMug", new Color(0.35f, 0.55f, 0.75f));
            var tea = BuilderUtil.CreateMaterial("M_Tea", new Color(0.6f, 0.35f, 0.12f));

            var root = new GameObject("TeaSteam");
            // A smaller mug (3 wide, 4 tall). Convex MeshCollider = flat top, like the coffee mug.
            var body = Stage2Builder.Prim(PrimitiveType.Cylinder, "Body", root.transform, new Vector3(0f, 2f, 0f), Vector3.zero, new Vector3(3f, 2f, 3f), ceramic);
            var mc = body.AddComponent<MeshCollider>();
            mc.sharedMesh = body.GetComponent<MeshFilter>().sharedMesh;
            mc.convex = true;
            Stage2Builder.Prim(PrimitiveType.Cylinder, "Tea", root.transform, new Vector3(0f, 4.01f, 0f), Vector3.zero, new Vector3(2.6f, 0.02f, 2.6f), tea);
            Stage2Builder.Prim(PrimitiveType.Cube, "Handle", root.transform, new Vector3(1.9f, 2.2f, 0f), Vector3.zero, new Vector3(0.6f, 2f, 0.4f), ceramic);

            // The steam source sits at the rim, so the column starts there.
            var steamGo = new GameObject("Steam");
            steamGo.transform.SetParent(root.transform, false);
            steamGo.transform.localPosition = new Vector3(0f, 4.1f, 0f);
            var source = steamGo.AddComponent<SteamSource>();

            var particlesGo = new GameObject("SteamParticles");
            particlesGo.transform.SetParent(steamGo.transform, false);
            particlesGo.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);   // emit up
            var ps = Stage2Builder.AddParticles(particlesGo, steamMat, stretched: false, sizeMin: 0.6f, sizeMax: 1.4f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 0.4f;
            noise.scrollSpeed = 0.6f;
            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;   // steam billows out as it rises
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 1.8f));

            BuilderUtil.SetRef(source, "settings", settings);
            BuilderUtil.SetRef(source, "steam", ps);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, TeaPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static GameObject BuildVentPrefab(VentSettings settings, Material streakMat)
        {
            var grilleMat = BuilderUtil.CreateMaterial("M_VentGrille", new Color(0.8f, 0.82f, 0.85f));
            var slatMat = BuilderUtil.CreateMaterial("M_VentSlat", new Color(0.25f, 0.27f, 0.3f));

            // Local space: x/y = across the opening, +z = direction the air blows.
            var root = new GameObject("CeilingVent");
            Vector2 s = settings.size;
            Stage2Builder.Prim(PrimitiveType.Cube, "Frame", root.transform, new Vector3(0f, 0f, -0.3f), Vector3.zero, new Vector3(s.x + 1.2f, s.y + 1.2f, 0.4f), grilleMat);
            int slats = 6;
            for (int i = 0; i < slats; i++)
            {
                float y = Mathf.Lerp(-s.y * 0.4f, s.y * 0.4f, i / (float)(slats - 1));
                Stage2Builder.Prim(PrimitiveType.Cube, "Slat", root.transform, new Vector3(0f, y, -0.05f), new Vector3(30f, 0f, 0f), new Vector3(s.x, 0.25f, 0.6f), slatMat);
            }

            var streakGo = new GameObject("VentStreaks");
            streakGo.transform.SetParent(root.transform, false);
            streakGo.transform.localPosition = new Vector3(0f, 0f, 0.3f);
            var ps = Stage2Builder.AddParticles(streakGo, streakMat, stretched: true, sizeMin: 0.06f, sizeMax: 0.12f);

            var vent = root.AddComponent<VentSource>();
            BuilderUtil.SetRef(vent, "settings", settings);
            BuilderUtil.SetRef(vent, "streaks", ps);
            vent.SyncVisuals();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, VentPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static GameObject BuildRingPrefab()
        {
            // Emission must be enabled on the material so the ring can glow via a property block.
            var mat = BuilderUtil.CreateMaterial("M_StickyRing", new Color(0.95f, 0.85f, 0.35f));
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.black);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(mat);

            var root = new GameObject("StickyRing");
            const float outer = 7f, bar = 0.6f, depth = 0.15f;
            float off = (outer - bar) * 0.5f;
            var renderers = new Renderer[4];
            renderers[0] = Stage2Builder.Prim(PrimitiveType.Cube, "Top", root.transform, new Vector3(0f, off, 0f), Vector3.zero, new Vector3(outer, bar, depth), mat).GetComponent<Renderer>();
            renderers[1] = Stage2Builder.Prim(PrimitiveType.Cube, "Bottom", root.transform, new Vector3(0f, -off, 0f), Vector3.zero, new Vector3(outer, bar, depth), mat).GetComponent<Renderer>();
            renderers[2] = Stage2Builder.Prim(PrimitiveType.Cube, "Left", root.transform, new Vector3(-off, 0f, 0f), Vector3.zero, new Vector3(bar, outer, depth), mat).GetComponent<Renderer>();
            renderers[3] = Stage2Builder.Prim(PrimitiveType.Cube, "Right", root.transform, new Vector3(off, 0f, 0f), Vector3.zero, new Vector3(bar, outer, depth), mat).GetComponent<Renderer>();
            foreach (var r in renderers) r.shadowCastingMode = ShadowCastingMode.Off;

            // Generous, deep trigger so fast planes can't skip it between physics ticks.
            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(outer - bar, outer - bar, 3f);

            var cp = root.AddComponent<Checkpoint>();
            var so = new SerializedObject(cp);
            var arr = so.FindProperty("frameRenderers");
            arr.arraySize = renderers.Length;
            for (int i = 0; i < renderers.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, RingPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ------------------------------------------------------------------ scene

        static void PlaceInDeskScene(GameObject teaPrefab, GameObject ventPrefab, GameObject ringPrefab)
        {
            var scene = EditorSceneManager.OpenScene(DeskScene);
            var old = GameObject.Find("Stage5");
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject("Stage5").transform;

            // Ceiling: one-sided plane facing down. No shadows, so it doesn't black out the moonlight.
            var ceilingMat = BuilderUtil.CreateMaterial("M_Ceiling", new Color(0.18f, 0.18f, 0.2f));
            var ceiling = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ceiling.name = "Ceiling";
            ceiling.transform.SetParent(root, false);
            ceiling.transform.position = new Vector3(0f, CeilingY, 0f);
            ceiling.transform.rotation = Quaternion.Euler(180f, 0f, 0f);
            ceiling.transform.localScale = new Vector3(10f, 1f, 6f);   // 100 x 60
            var cr = ceiling.GetComponent<Renderer>();
            cr.sharedMaterial = ceilingMat;
            cr.shadowCastingMode = ShadowCastingMode.Off;

            // AC vent in the ceiling, blowing straight down over the gap between the
            // laptop thermal and the monitor. The direct line from ring 2 to ring 3 goes through it.
            var vent = (GameObject)PrefabUtility.InstantiatePrefab(ventPrefab, root);
            vent.transform.SetPositionAndRotation(new Vector3(-2f, CeilingY - 0.2f, 8f), Quaternion.Euler(90f, 0f, 0f));

            // Tea mug at the front-middle of the desk: a strong, gusty steam thermal.
            var tea = (GameObject)PrefabUtility.InstantiatePrefab(teaPrefab, root);
            tea.transform.SetPositionAndRotation(P(9f, 0f, -11f), Quaternion.identity);

            // Route: thermal -> over the monitor (dodging the vent) -> dive into the fan stream ->
            // climb the steam -> land on the coffee mug.
            var routeGo = new GameObject("Route");
            routeGo.transform.SetParent(root, false);
            var route = routeGo.AddComponent<RaceRoute>();
            BuilderUtil.SetRef(route, "plane", Object.FindAnyObjectByType<PlaneController>());

            Ring(ringPrefab, routeGo.transform, "Ring1_ThermalEntry", P(-21f, 6f, -8f), 76f);
            Ring(ringPrefab, routeGo.transform, "Ring2_ThermalTop", P(-14f, 33f, 3f), 90f);
            Ring(ringPrefab, routeGo.transform, "Ring3_OverMonitor", P(8f, 22f, 14f), 90f);
            Ring(ringPrefab, routeGo.transform, "Ring4_FanStream", P(18f, 6f, 1f), 150f);   // clear of the boss jaw (Stage 10)
            Ring(ringPrefab, routeGo.transform, "Ring5_SteamTop", P(9f, 28f, -4f), 90f);

            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = routeGo;
        }

        static void Ring(GameObject prefab, Transform parent, string name, Vector3 pos, float yaw)
        {
            var ring = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            ring.name = name;
            ring.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        }

        static Vector3 P(float x, float yAboveDesk, float z) => new Vector3(x, DeskTop + yAboveDesk, z);
    }
}
