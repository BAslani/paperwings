using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using PaperWings.CameraRig;
using PaperWings.Flight;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// Stage 4: Cinemachine chase camera + feel polish.
    /// Menu: PaperWings > Stage Builders > Stage 4 (Cinemachine + Polish, All Scenes)
    ///
    /// Upgrades the existing FlightTest and DeskPrototype scenes IN PLACE (only the camera
    /// objects are replaced, so your other scene edits are kept), and adds wing trails to
    /// the PaperDart prefab. The Stage 1 and 3 builders also use this camera rig now.
    /// </summary>
    public static class Stage4Builder
    {
        const string Root = "Assets/_Project";
        const string CameraSettingsPath = Root + "/Settings/CameraFeel_Chase.asset";
        const string PlanePrefabPath = Root + "/Prefabs/PaperDart.prefab";
        static readonly string[] Scenes = { Root + "/Scenes/FlightTest.unity", Root + "/Scenes/DeskPrototype.unity" };

        [MenuItem("PaperWings/Stage Builders/Stage 4 (Cinemachine + Polish, All Scenes)", priority = 104)]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            AddTrailsToPlanePrefab();

            int upgraded = 0;
            foreach (var path in Scenes)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) continue;
                var scene = EditorSceneManager.OpenScene(path);
                if (CameraRigBuilder.ReplaceCameraInOpenScene()) upgraded++;
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<CameraFeelSettings>(CameraSettingsPath);
            EditorUtility.DisplayDialog("PaperWings",
                $"Stage 4 built. Upgraded {upgraded} scene(s) with the Cinemachine chase camera, " +
                "speed lines and wing trails.\n\nCamera tuning: Settings/CameraFeel_Chase.", "Fly!");
        }

        static void AddTrailsToPlanePrefab()
        {
            // UNITY NOTE: LoadPrefabContents opens a prefab in an isolated, invisible scene so
            // editor code can modify it, then SaveAsPrefabAsset writes it back.
            var root = PrefabUtility.LoadPrefabContents(PlanePrefabPath);
            PlaneFxBuilder.AddTrails(root);
            PrefabUtility.SaveAsPrefabAsset(root, PlanePrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>Creates the Cinemachine chase rig. Shared by the Stage 1, 3 and 4 builders.</summary>
    public static class CameraRigBuilder
    {
        const string CameraSettingsPath = "Assets/_Project/Settings/CameraFeel_Chase.asset";
        const string NoisePath = "Packages/com.unity.cinemachine/Presets/Noise/Handheld_normal_mild.asset";

        /// <summary>Removes any old camera objects in the open scene and builds the new rig.</summary>
        public static bool ReplaceCameraInOpenScene()
        {
            var plane = Object.FindAnyObjectByType<PlaneController>();
            if (plane == null) return false;

            foreach (var name in new[] { "Main Camera", "ChaseCam" })
            {
                var old = GameObject.Find(name);
                if (old != null) Object.DestroyImmediate(old);
            }

            // Start the camera behind wherever the plane is placed.
            var spawn = GameObject.Find("SpawnPoint");
            CreateChaseCamera(plane, spawn != null ? spawn.transform : plane.transform,
                              farClip: RenderSettings.fogEndDistance + 200f);
            return true;
        }

        public static void CreateChaseCamera(PlaneController plane, Transform spawn, float farClip)
        {
            var settings = Stage2Builder.LoadOrCreate<CameraFeelSettings>(CameraSettingsPath);
            Vector3 startPos = spawn.position + spawn.rotation * settings.followOffset;

            // ---- The real camera: renders, and its Brain copies the active virtual camera ----
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.position = startPos;
            camGo.transform.LookAt(spawn.position);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;   // no skybox: plain background
            cam.backgroundColor = RenderSettings.fogColor;
            camGo.AddComponent<AudioListener>();
            var brain = camGo.AddComponent<CinemachineBrain>();
            // The plane's Rigidbody interpolates, so it's in its final spot by LateUpdate.
            brain.UpdateMethod = CinemachineBrain.UpdateMethods.LateUpdate;

            var speedLines = CreateSpeedLines(camGo.transform);

            // ---- The virtual camera: decides where to sit and where to look ----
            var vGo = new GameObject("ChaseCam");
            vGo.transform.SetPositionAndRotation(camGo.transform.position, camGo.transform.rotation);
            var vcam = vGo.AddComponent<CinemachineCamera>();
            vcam.Follow = plane.transform;
            vcam.LookAt = plane.transform;
            var lens = LensSettings.Default;
            lens.FieldOfView = settings.baseFov;
            lens.NearClipPlane = 0.1f;
            lens.FarClipPlane = farClip;
            vcam.Lens = lens;

            // Body: sit behind the plane's heading, ignoring its bank and pitch (steady horizon).
            var follow = vGo.AddComponent<CinemachineFollow>();
            follow.TrackerSettings.BindingMode = BindingMode.LockToTargetWithWorldUp;
            follow.FollowOffset = settings.followOffset;
            follow.TrackerSettings.PositionDamping = settings.positionDamping;

            // Aim: keep the plane framed, leading slightly in the direction of travel.
            var composer = vGo.AddComponent<CinemachineRotationComposer>();
            composer.TargetOffset = new Vector3(0f, 0.4f, 0f);
            composer.Damping = settings.aimDamping;
            composer.Lookahead.Enabled = true;
            composer.Lookahead.Time = settings.lookaheadTime;
            composer.Lookahead.Smoothing = 5f;

            // Noise: amplitude is driven at runtime (speed rumble, stall, boost, crash).
            var noise = vGo.AddComponent<CinemachineBasicMultiChannelPerlin>();
            noise.NoiseProfile = AssetDatabase.LoadAssetAtPath<NoiseSettings>(NoisePath);
            noise.AmplitudeGain = 0f;

            var fx = vGo.AddComponent<SpeedCameraEffects>();
            BuilderUtil.SetRef(fx, "plane", plane);
            BuilderUtil.SetRef(fx, "settings", settings);
            BuilderUtil.SetRef(fx, "speedLines", speedLines);

            // Mouse-aim scheme: camera follows the aim direction instead of the nose (Stage 11).
            var mouseCam = vGo.AddComponent<MouseAimCamera>();
            BuilderUtil.SetRef(mouseCam, "plane", plane);
            BuilderUtil.SetRef(mouseCam, "input", plane.GetComponent<PlaneInput>());
        }

        static ParticleSystem CreateSpeedLines(Transform camera)
        {
            // A ring of streaks ahead of the camera flying toward it. Emission is driven by speed.
            var go = new GameObject("SpeedLines");
            go.transform.SetParent(camera, false);
            go.transform.localPosition = new Vector3(0f, 0f, 22f);
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // emit back toward the camera

            var mat = Stage2Builder.ParticleMaterial("M_SpeedLine", null, additive: true);
            var ps = Stage2Builder.AddParticles(go, mat, stretched: true, sizeMin: 0.03f, sizeMax: 0.07f);

            var main = ps.main;
            main.prewarm = false;
            main.startLifetime = 0.7f;
            main.startSpeed = 30f;
            main.startColor = new Color(1f, 1f, 1f, 0.35f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;   // stick to the camera
            main.maxParticles = 400;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 0f;
            shape.radius = 7f;
            shape.radiusThickness = 0.3f;   // outer ring only: keeps the center of the screen clear

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.velocityScale = 0.04f;
            r.lengthScale = 2f;
            return ps;
        }
    }

    /// <summary>Visual extras on the plane prefab (shared by the Stage 1 and Stage 4 builders).</summary>
    public static class PlaneFxBuilder
    {
        public const string PaperSettingsPath = "Assets/_Project/Settings/Paper_Dart.asset";

        /// <summary>Adds (or rebuilds) PaperDamage and its tear-bits particles on the plane root.</summary>
        public static void AddPaperDamage(GameObject planeRoot, PaperSettings paperOverride = null)
        {
            var settings = paperOverride != null ? paperOverride : Stage2Builder.LoadOrCreate<PaperSettings>(PaperSettingsPath);

            var old = planeRoot.transform.Find("TearBits");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            // Little white paper flakes, emitted in bursts by PaperDamage when you take a hit.
            var bitsGo = new GameObject("TearBits");
            bitsGo.transform.SetParent(planeRoot.transform, false);
            var mat = Stage2Builder.ParticleMaterial("M_PaperBits", null, additive: false);
            var bits = Stage2Builder.AddParticles(bitsGo, mat, stretched: false, sizeMin: 0.08f, sizeMax: 0.18f);
            var main = bits.main;
            main.loop = false;
            main.prewarm = false;
            main.playOnAwake = false;
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            main.gravityModifier = 0.6f;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.95f, 0.94f, 0.9f, 1f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;   // flakes stay behind as you fly on
            var emission = bits.emission;
            emission.rateOverTime = 0f;
            var shape = bits.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.5f;
            var rot = bits.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-8f, 8f);

            var damage = planeRoot.GetComponent<PaperDamage>();
            if (damage == null) damage = planeRoot.AddComponent<PaperDamage>();
            var visual = planeRoot.transform.Find("Visual");
            BuilderUtil.SetRef(damage, "settings", settings);
            BuilderUtil.SetRef(damage, "visualMesh", visual != null ? visual.GetComponent<MeshFilter>() : null);
            BuilderUtil.SetRef(damage, "tearBits", bits);
        }

        /// <summary>Adds (or rebuilds) wingtip trails under the plane's Visual child.</summary>
        public static void AddTrails(GameObject planeRoot)
        {
            var visual = planeRoot.transform.Find("Visual");
            if (visual == null) visual = planeRoot.transform;

            foreach (var name in new[] { "TrailLeft", "TrailRight" })
            {
                var old = visual.Find(name);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }

            var mat = Stage2Builder.ParticleMaterial("M_Trail", null, additive: true);
            var left = MakeTrail("TrailLeft", visual, new Vector3(-0.68f, 0.1f, -0.95f), mat);
            var right = MakeTrail("TrailRight", visual, new Vector3(0.68f, 0.1f, -0.95f), mat);

            var fx = planeRoot.GetComponent<PlaneTrails>();
            if (fx == null) fx = planeRoot.AddComponent<PlaneTrails>();
            BuilderUtil.SetRef(fx, "plane", planeRoot.GetComponent<PlaneController>());

            var so = new SerializedObject(fx);
            var arr = so.FindProperty("trails");
            arr.arraySize = 2;
            arr.GetArrayElementAtIndex(0).objectReferenceValue = left;
            arr.GetArrayElementAtIndex(1).objectReferenceValue = right;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static TrailRenderer MakeTrail(string name, Transform parent, Vector3 localPos, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var t = go.AddComponent<TrailRenderer>();
            t.sharedMaterial = mat;
            t.time = 0.8f;
            t.minVertexDistance = 0.15f;
            t.widthMultiplier = 0f;
            t.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.7f, 0.85f, 1f), 1f) },
                new[] { new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(0f, 1f) });
            t.colorGradient = g;
            t.shadowCastingMode = ShadowCastingMode.Off;
            t.receiveShadows = false;
            t.emitting = false;
            return t;
        }
    }
}
