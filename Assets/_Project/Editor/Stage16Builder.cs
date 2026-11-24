using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PaperWings.Flight;
using PaperWings.Gameplay;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// Stage 16: the Canard fold. A plane with a small extra wing at the nose that glides further,
    /// climbs more cheaply, rides air better, and gets a lifting boost.
    /// Menu: PaperWings > Stage Builders > Stage 16 (Canard Fold)
    ///
    /// Creates the mesh, dot-grid paper, FlightSettings_Canard, Paper_Canard and Fold_Canard,
    /// then adds the fold to the fold picker in both scenes. Existing assets are never overwritten.
    /// </summary>
    public static class Stage16Builder
    {
        const string S = "Assets/_Project/Settings/";
        const string Art = "Assets/_Project/Art/";
        const string FoldPath = S + "Fold_Canard.asset";
        static readonly string[] Scenes = { "Assets/_Project/Scenes/FlightTest.unity", "Assets/_Project/Scenes/DeskPrototype.unity" };

        [MenuItem("PaperWings/Stage Builders/Stage 16 (Canard Fold)", priority = 116)]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var paperTex = TextureFactory.DotGridPaper();
            var mesh = BuilderUtil.SaveMesh(FoldMeshes.BuildCanard(), Art + "PaperCanard_Mesh.asset");

            var flight = AssetDatabase.LoadAssetAtPath<FlightSettings>(S + "FlightSettings_Canard.asset");
            if (flight == null)
            {
                flight = Object.Instantiate(AssetDatabase.LoadAssetAtPath<FlightSettings>(S + "FlightSettings_Dart.asset"));
                // Glides further and climbs cheaper (keep climb cost >= dive gain: no free height).
                flight.cruiseSpeed = 13.5f; flight.maxSpeed = 42f;
                flight.diveSpeedGain = 17f; flight.climbSpeedCost = 17f;
                flight.glideRatio = 12f; flight.slowSinkExtra = 1.8f; flight.turnSinkExtra = 0.9f;
                // The canard gives the nose more authority, even when slow.
                flight.pitchRate = 82f; flight.turnRate = 72f; flight.lowSpeedAuthority = 0.55f;
                flight.stallSpeed = 6f; flight.stallRecoverSpeed = 9f;
                // Catches air better.
                flight.airInfluence = 1.2f; flight.tailwindSpeedGain = 0.6f;
                // Stronger, quicker boost that also lifts.
                flight.boostSpeed = 14f; flight.boostCooldown = 2.5f;
                flight.boostLift = 9f; flight.boostLiftDuration = 1.2f;
                AssetDatabase.CreateAsset(flight, S + "FlightSettings_Canard.asset");
            }

            var paper = AssetDatabase.LoadAssetAtPath<PaperSettings>(S + "Paper_Canard.asset");
            if (paper == null)
            {
                paper = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PaperSettings>(S + "Paper_Dart.asset"));
                paper.maxIntegrity = 95f;
                AssetDatabase.CreateAsset(paper, S + "Paper_Canard.asset");
            }

            var fold = AssetDatabase.LoadAssetAtPath<PlaneFold>(FoldPath);
            if (fold == null)
            {
                fold = ScriptableObject.CreateInstance<PlaneFold>();
                fold.displayName = "Canard";
                fold.description = "A small extra wing at the nose. Glides far, climbs easily, rides the air, and its boost lifts you up.";
                fold.paperColor = new Color(0.98f, 0.96f, 0.92f);
                AssetDatabase.CreateAsset(fold, FoldPath);
            }
            fold.flight = fold.flight != null ? fold.flight : flight;
            fold.paper = fold.paper != null ? fold.paper : paper;
            fold.mesh = mesh;
            fold.paperTexture = paperTex;
            fold.attachment = BuildPropeller(paperTex);
            if (fold.description.StartsWith("A small extra wing at the nose."))
                fold.description = "A little pusher propeller on the tail and small nose wings. Glides far, climbs easily, rides the air, and its boost lifts you up.";
            EditorUtility.SetDirty(fold);
            AssetDatabase.SaveAssets();

            int added = 0;
            foreach (var path in Scenes)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) continue;
                var scene = EditorSceneManager.OpenScene(path);
                var canard = AssetDatabase.LoadAssetAtPath<PlaneFold>(FoldPath);   // reload after opening the scene
                var selector = Object.FindAnyObjectByType<FoldSelector>();
                if (selector != null && AppendFold(selector, canard)) added++;
                EditorSceneManager.SaveScene(scene);
            }

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<FlightSettings>(S + "FlightSettings_Canard.asset");
            EditorUtility.DisplayDialog("PaperWings",
                $"Stage 16 built: the CANARD fold (added to the picker in {added} scene(s)).\n\n" +
                "In hand, press Q / E to select it. Try boosting into a climb: the boost lifts you.\n\n" +
                "Tuning: PaperWings > Settings > Canard - Flight (Boost Lift, Glide Ratio...).", "Fold it!");
        }

        /// <summary>
        /// The little propeller: a pushpin through the nose as the axle, a paper hub and two
        /// twisted paper blades, plus a faint disc that appears when it spins fast.
        /// </summary>
        static GameObject BuildPropeller(Texture2D paperTex)
        {
            var steel = BuilderUtil.CreateMaterial("M_PinSteel", new Color(0.75f, 0.76f, 0.8f));
            steel.SetFloat("_Metallic", 0.8f);
            var pinHead = BuilderUtil.CreateMaterial("M_PinHead", new Color(0.85f, 0.15f, 0.15f));
            var blade = BuilderUtil.CreateMaterial("M_PropBlade", new Color(1f, 0.93f, 0.6f));
            blade.SetTexture("_BaseMap", paperTex);
            EditorUtility.SetDirty(blade);
            var blur = Stage2Builder.ParticleMaterial("M_PropBlur", null, additive: false);

            var root = new GameObject("Propeller");
            // A small pusher prop on the Canard's tail (local to Visual): turned to face backward,
            // so the pushpin head pokes out behind and the blades sit just past the trailing edge.
            root.transform.localPosition = new Vector3(0f, 0f, -0.92f);
            root.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            root.transform.localScale = Vector3.one * 0.6f;

            // Pushpin axle along the nose (cylinders stand on Y, so rotate 90 on X to point along Z).
            Part(PrimitiveType.Cylinder, "Pin", root.transform, Vector3.zero, new Vector3(90f, 0f, 0f), new Vector3(0.04f, 0.12f, 0.04f), steel);
            Part(PrimitiveType.Sphere, "PinHead", root.transform, new Vector3(0f, 0f, 0.14f), Vector3.zero, Vector3.one * 0.12f, pinHead);

            var rotor = new GameObject("Rotor").transform;
            rotor.SetParent(root.transform, false);
            rotor.localPosition = new Vector3(0f, 0f, 0.06f);
            Part(PrimitiveType.Cylinder, "Hub", rotor, Vector3.zero, new Vector3(90f, 0f, 0f), new Vector3(0.12f, 0.03f, 0.12f), blade);
            // Two paper blades, twisted in opposite directions like a real prop.
            Part(PrimitiveType.Cube, "BladeA", rotor, new Vector3(0f, 0.2f, 0f), new Vector3(0f, 20f, 0f), new Vector3(0.1f, 0.36f, 0.012f), blade);
            Part(PrimitiveType.Cube, "BladeB", rotor, new Vector3(0f, -0.2f, 0f), new Vector3(0f, -20f, 0f), new Vector3(0.1f, 0.36f, 0.012f), blade);

            var disc = Part(PrimitiveType.Cylinder, "BlurDisc", root.transform, new Vector3(0f, 0f, 0.06f), new Vector3(90f, 0f, 0f), new Vector3(0.8f, 0.002f, 0.8f), blur);

            var prop = root.AddComponent<Propeller>();
            BuilderUtil.SetRef(prop, "rotor", rotor);
            BuilderUtil.SetRef(prop, "blurDisc", disc.GetComponent<Renderer>());

            const string path = "Assets/_Project/Prefabs/Propeller.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 euler, Vector3 scale, Material mat)
        {
            var go = Stage2Builder.Prim(type, name, parent, pos, euler, scale, mat);   // no collider: decoration only
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>Adds the fold to the selector's list if it isn't there yet (keeps the existing order).</summary>
        static bool AppendFold(FoldSelector selector, PlaneFold fold)
        {
            var so = new SerializedObject(selector);
            var arr = so.FindProperty("folds");
            var current = new List<Object>();
            for (int i = 0; i < arr.arraySize; i++)
            {
                var f = arr.GetArrayElementAtIndex(i).objectReferenceValue;
                if (f == fold) return false;
                current.Add(f);
            }
            arr.arraySize = current.Count + 1;
            arr.GetArrayElementAtIndex(current.Count).objectReferenceValue = fold;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }
    }
}
