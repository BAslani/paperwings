using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using PaperWings.Combat;
using PaperWings.Flight;
using PaperWings.Gameplay;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// Stage 29: fixes, a harder economy, the air-combat campaign, and new plane models.
    /// Menu: PaperWings > Build Stage 29 (Economy, Campaign, Plane Models)
    ///
    ///  - FIX: every cylinder with a capsule collider gets a real cylinder collider. A wide flat
    ///    cylinder's capsule is a huge invisible sphere (the Squall throw point was inside one).
    ///  - ECONOMY: upgrades cost three to five times more, and each needs a BLUEPRINT from the
    ///    campaign before the hangar will sell it. Nights no longer hand out upgrades for free.
    ///  - CAMPAIGN v3: thirteen nights of air combat (practice, intercepts, escorts, an ace, a
    ///    squadron battle, four bosses). Saved stars start over once; clips and unlocks stay.
    ///  - MODELS: Arrowhead (Dart), Crane (Glider), Manta (Stunt), Dragon (Canard), with their own
    ///    paper art; Accounting flies Ravens, Marketing's wingmen fly Swallows.
    ///  - Mission pieces used by Stages 30-32: courier and drone prefabs, the soda can, the ace pilot.
    /// Safe to re-run.
    /// </summary>
    public static class Stage29Builder
    {
        const string Art = StageKit.Root + "Art/";

        [MenuItem("PaperWings/Build Stage 29 (Economy, Campaign, Plane Models)", priority = 140)]
        public static void Build()
        {
            if (!StageKit.OpenDesk(out Scene scene)) return;
            string report = Run(scene);
            StageKit.Save(scene);
            EditorUtility.DisplayDialog("PaperWings", "Stage 29 built.\n\n" + report, "OK");
        }

        internal static string Run(Scene scene)
        {
            int fixedCyl = StageKit.FixCylinderColliders();
            Economy();
            Campaign();
            Models();
            MissionPrefabs();
            var ace = AcePilot();
            var director = StageKit.Director();
            if (director != null)
            {
                BuilderUtil.SetRef(director, "acePilot", ace);
                EditorUtility.SetDirty(director);
            }
            AssetDatabase.SaveAssets();
            return $"- Fixed {fixedCyl} cylinder collider(s).\n- Upgrades now cost more and need campaign blueprints.\n" +
                   "- Campaign v3: thirteen combat nights.\n- New plane models and paper.";
        }

        // ------------------------------------------------------------------ economy and campaign

        static void Economy()
        {
            var p = StageKit.Load<ProgressionSettings>("Settings/Progression.asset");
            if (p == null) return;
            var d = ScriptableObject.CreateInstance<ProgressionSettings>();
            p.foldUnlocks = d.foldUnlocks;
            p.foldUpgrades = d.foldUpgrades;
            p.clipsPerKill = d.clipsPerKill;
            p.clipsPerCompletedRun = d.clipsPerCompletedRun;
            p.medalBonus = d.medalBonus;
            Object.DestroyImmediate(d);
            EditorUtility.SetDirty(p);
        }

        static void Campaign()
        {
            var c = StageKit.Load<CampaignSettings>("Settings/Campaign.asset");
            if (c == null) return;
            c.nights = CampaignSettings.NightShift();
            c.version = CampaignSettings.CurrentVersion;
            c.starBonus = new[] { 10, 25 };
            EditorUtility.SetDirty(c);
        }

        // ------------------------------------------------------------------ models

        static void Models()
        {
            var dart = BuilderUtil.SaveMesh(DartMesh.Build(), Art + "PaperDart_Mesh.asset");
            var glider = BuilderUtil.SaveMesh(FoldMeshes.BuildGlider(), Art + "PaperGlider_Mesh.asset");
            var stunt = BuilderUtil.SaveMesh(FoldMeshes.BuildStunt(), Art + "PaperStunt_Mesh.asset");
            var canard = BuilderUtil.SaveMesh(FoldMeshes.BuildCanard(), Art + "PaperCanard_Mesh.asset");
            var raven = BuilderUtil.SaveMesh(FoldMeshes.BuildRaven(), Art + "PaperRaven_Mesh.asset");
            var swallow = BuilderUtil.SaveMesh(FoldMeshes.BuildSwallow(), Art + "PaperSwallow_Mesh.asset");

            Fold("Fold_Dart", "Arrowhead", dart, TextureFactory.ArrowPaper(), -1.02f,
                 "Swallow-cut wings and twin fins: fast and steady. A good all-rounder.");
            Fold("Fold_Glider", "Crane", glider, TextureFactory.CranePaper(), -0.85f,
                 "An origami crane: long feathered wings that ride thermals and glide forever.");
            Fold("Fold_Stunt", "Manta", stunt, TextureFactory.MantaPaper(), -0.55f,
                 "A manta ray: curved wings and a deep keel for tight turns and big dives.");
            Fold("Fold_Canard", "Dragon", canard, TextureFactory.DragonPaper(), -0.95f,
                 "A paper dragon: nose canards and bat wings. Glides far and climbs easily.");

            AiVisual("Prefabs/RivalDart.prefab", raven, TextureFactory.RavenPaper(), "M_PaperRival");
            AiVisual("Prefabs/WingmanDart.prefab", swallow, null, "M_PaperWingman");
        }

        static void Fold(string asset, string displayName, Mesh mesh, Texture2D paper, float propZ, string description)
        {
            var f = StageKit.Load<PlaneFold>($"Settings/{asset}.asset");
            if (f == null) return;
            f.displayName = displayName;   // safe: upgrade ids use foldId, not the name
            f.mesh = mesh;
            f.paperTexture = paper;
            f.paperColor = Color.white;
            f.description = description;
            f.propellerMount = new Vector3(0f, 0f, propZ);
            EditorUtility.SetDirty(f);
        }

        /// <summary>Gives an AI plane prefab its own model (and paper), and moves its trails to the new wingtips.</summary>
        static void AiVisual(string prefabPath, Mesh mesh, Texture2D paper, string materialName)
        {
            string path = StageKit.Root + prefabPath;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return;
            var root = PrefabUtility.LoadPrefabContents(path);
            var damage = root.GetComponent<PaperDamage>();
            MeshFilter visual = null;
            if (damage != null) visual = new SerializedObject(damage).FindProperty("visualMesh").objectReferenceValue as MeshFilter;
            if (visual == null) visual = root.GetComponentInChildren<MeshFilter>();
            if (visual != null)
            {
                visual.sharedMesh = mesh;
                StageKit29.Wingtips(mesh, out Vector3 left, out Vector3 right);
                var trails = root.GetComponent<PlaneTrails>();
                if (trails != null)
                {
                    var tso = new SerializedObject(trails).FindProperty("trails");
                    if (tso != null && tso.arraySize >= 2)
                    {
                        var l = tso.GetArrayElementAtIndex(0).objectReferenceValue as TrailRenderer;
                        var r = tso.GetArrayElementAtIndex(1).objectReferenceValue as TrailRenderer;
                        if (l != null) l.transform.localPosition = left;
                        if (r != null) r.transform.localPosition = right;
                    }
                }
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);

            if (paper != null)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(Art + materialName + ".mat");
                if (mat != null)
                {
                    mat.SetTexture("_BaseMap", paper);
                    mat.SetColor("_BaseColor", Color.white);
                    EditorUtility.SetDirty(mat);
                }
            }
        }

        // ------------------------------------------------------------------ mission prefabs

        static void MissionPrefabs()
        {
            var ledger = StageKit.Textured("M_CourierRed", Color.white, TextureFactory.RavenPaper(), Vector2.one);
            var flyer = StageKit.Textured("M_CourierBlue", Color.white, TextureFactory.FlyerPaper(), Vector2.one);
            var sticky = StageKit.Textured("M_PracticeDrone", new Color(1f, 0.92f, 0.45f), TextureFactory.StickyPaper(), Vector2.one);
            var raven = AssetDatabase.LoadAssetAtPath<Mesh>(Art + "PaperRaven_Mesh.asset");
            var swallow = AssetDatabase.LoadAssetAtPath<Mesh>(Art + "PaperSwallow_Mesh.asset");
            var glider = AssetDatabase.LoadAssetAtPath<Mesh>(Art + "PaperGlider_Mesh.asset");

            Flyer("CourierRed", raven, ledger, 3.2f, Faction.Accounting, new Color(1f, 0.3f, 0.25f));
            Flyer("CourierBlue", swallow, flyer, 3.4f, Faction.Marketing, new Color(0.35f, 0.65f, 1f));
            Flyer("PracticeDrone", glider, sticky, 1.8f, Faction.Accounting, new Color(1f, 0.85f, 0.3f));
            SodaCan();
        }

        static void Flyer(string name, Mesh mesh, Material mat, float scale, Faction faction, Color trail)
        {
            var root = new GameObject(name);
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = Vector3.one * scale;
            visual.AddComponent<MeshFilter>().sharedMesh = mesh;
            visual.AddComponent<MeshRenderer>().sharedMaterial = mat;
            var col = root.AddComponent<BoxCollider>();
            col.size = new Vector3(mesh.bounds.size.x * scale, Mathf.Max(0.8f, mesh.bounds.size.y * scale), mesh.bounds.size.z * scale);
            col.center = mesh.bounds.center * scale;

            // A faction-coloured trail from the tail, so it reads at a distance.
            var trailGo = new GameObject("Trail");
            trailGo.transform.SetParent(root.transform, false);
            trailGo.transform.localPosition = new Vector3(0f, 0f, mesh.bounds.min.z * scale);
            var tr = trailGo.AddComponent<TrailRenderer>();
            tr.time = 1.4f;
            tr.widthMultiplier = 0.5f * scale;
            tr.sharedMaterial = StageKit.StreakMat;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(trail, 0f), new GradientColorKey(trail, 1f) },
                      new[] { new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(0f, 1f) });
            tr.colorGradient = g;

            var flyer = root.AddComponent<PathFlyer>();
            var target = root.AddComponent<CombatTarget>();
            var tso = new SerializedObject(target);
            tso.FindProperty("faction").enumValueIndex = (int)faction;
            tso.FindProperty("aiTarget").boolValue = true;
            tso.FindProperty("displayName").stringValue = name;
            tso.ApplyModifiedPropertiesWithoutUndo();
            var burst = StageKit29.Burst(root.transform, "DownBurst", Vector3.zero, 90, 14f, 0.7f);
            BuilderUtil.SetRef(flyer, "visual", visual.transform);
            BuilderUtil.SetRef(flyer, "downBurst", burst);

            PrefabUtility.SaveAsPrefabAsset(root, StageKit29.P + name + ".prefab");
            Object.DestroyImmediate(root);
        }

        /// <summary>The vending machine's ammo: a red soda can (a neutral projectile).</summary>
        static void SodaCan()
        {
            var red = StageKit.Mat("M_SodaCan", new Color(0.85f, 0.1f, 0.1f), 0.7f, 0.6f);
            var root = new GameObject("SodaCan");
            root.AddComponent<StapleProjectile>();
            var body = StageKit.Cyl("Can", root.transform, Vector3.zero, new Vector3(1.5f, 2.6f, 1.5f), red, collider: false, euler: new Vector3(90f, 0f, 0f));
            body.transform.localPosition = Vector3.zero;
            PrefabUtility.SaveAsPrefabAsset(root, StageKit29.P + "SodaCan.prefab");
            Object.DestroyImmediate(root);
        }

        static RivalPilotSettings AcePilot()
        {
            string path = StageKit29.S + "Pilot_Ace.asset";
            var s = Stage2Builder.LoadOrCreate<RivalPilotSettings>(path);
            s.engageRange = 90f;
            s.disengageRange = 140f;
            s.fireRange = 42f;
            s.fireAngle = 14f;
            s.burstLength = 5;
            s.burstPause = 0.9f;
            s.patrolRadius = 30f;
            s.preferredHeight = 30f;
            s.lowHeight = 12f;
            s.aggressiveness = 1f;
            s.avoidDistance = 18f;
            s.respawnDelay = 6f;
            EditorUtility.SetDirty(s);
            return s;
        }
    }
}
