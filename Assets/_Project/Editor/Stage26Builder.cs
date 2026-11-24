using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using PaperWings.Air;
using PaperWings.Combat;
using PaperWings.Gameplay;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// Stage 26: bosses built on air.
    /// Menu: PaperWings > Stage Builders > Stage 26 (Air Bosses)
    ///
    /// Each boss is built around one kind of air, so beating it is about reading and using the air:
    ///   THE STAPLER    (office desk, Stage 10): downdraft slams.
    ///   THE MAINFRAME  (server room): a fan sweep that shoves you sideways (draft), then it overheats
    ///                  and its hot exhaust (heat) is your lift up to the weak spot on top.
    ///   THE CONDENSER  (rooftop): its fan blows a huge updraft, stalls (weak spot), then reverses
    ///                  into suction that drags you onto the grille.
    /// Builds both prefabs and settings, and puts them in their rooms' boss groups (Boss Fight mode).
    /// Run Stage 25 first. Safe to re-run (prefabs rebuilt, settings kept).
    /// </summary>
    public static class Stage26Builder
    {
        const string P = StageKit.Root + "Prefabs/";
        const string S = StageKit.Root + "Settings/";

        [MenuItem("PaperWings/Stage Builders/Stage 26 (Air Bosses)", priority = 126)]
        public static void Build()
        {
            if (!StageKit.OpenDesk(out Scene scene)) return;
            if (!Run(scene)) return;
            StageKit.Save(scene);
            EditorUtility.DisplayDialog("PaperWings",
                "Stage 26 built.\n\n- THE MAINFRAME guards the server room's east end.\n- THE CONDENSER sits in the middle of the roof.\n\n" +
                "Pick the room on the main menu and choose Boss Fight.", "Bring it");
        }

        internal static bool Run(Scene scene)
        {
            var server = StageKit.RoomAt(StageKit.ServerRoom);
            var roof = StageKit.RoomAt(StageKit.Rooftop);
            if (server == null || roof == null)
            {
                EditorUtility.DisplayDialog("PaperWings", "Run Build Stage 25 first (server room and rooftop).", "OK");
                return false;
            }

            var mainframe = BuildMainframe(MainframeSettings());
            var condenser = BuildCondenser(CondenserSettings());

            Place(server, "ServerBoss", mainframe, Stage25Builder.ServerOrigin + new Vector3(112f, 0f, 0f), -90f);
            Place(roof, "RooftopBoss", condenser, Stage25Builder.RoofOrigin, 0f);
            return true;
        }

        static void Place(Room room, string groupName, GameObject prefab, Vector3 pos, float yaw)
        {
            var group = room.transform.Find(groupName);
            if (group == null)
            {
                group = StageKit.Group(room.transform, groupName);
                group.gameObject.SetActive(false);
            }
            for (int i = group.childCount - 1; i >= 0; i--) Object.DestroyImmediate(group.GetChild(i).gameObject);
            var boss = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
            boss.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            room.bossGroup = group.gameObject;
            EditorUtility.SetDirty(room);
        }

        // ------------------------------------------------------------------ settings

        internal static AirBossSettings MainframeSettings()
        {
            string path = S + "Boss_Mainframe.asset";
            bool isNew = AssetDatabase.LoadAssetAtPath<AirBossSettings>(path) == null;
            var s = Stage2Builder.LoadOrCreate<AirBossSettings>(path);
            if (isNew)
            {
                s.displayName = "THE MAINFRAME";
                s.title = "Head of IT";
                s.maxHealth = 700f;
                s.aggroRange = 115f;
                s.telegraph = 1.1f;
                s.attackDuration = 4f;
                s.vulnerableTime = 4f;
                s.restTime = 1.5f;
                s.airSpeed = 16f;          // fan sweep
                s.secondaryAirSpeed = 12f; // exhaust lift / purge
                s.shotsPerVolley = 6;
                s.spreadAngle = 40f;
                EditorUtility.SetDirty(s);
            }
            return s;
        }

        static AirBossSettings CondenserSettings()
        {
            string path = S + "Boss_Condenser.asset";
            bool isNew = AssetDatabase.LoadAssetAtPath<AirBossSettings>(path) == null;
            var s = Stage2Builder.LoadOrCreate<AirBossSettings>(path);
            if (isNew)
            {
                s.displayName = "THE CONDENSER";
                s.title = "Building Services";
                s.maxHealth = 900f;
                s.aggroRange = 145f;
                s.telegraph = 1.2f;
                s.attackDuration = 5f;
                s.vulnerableTime = 3.5f;
                s.restTime = 1.2f;
                s.airSpeed = 15f;          // updraft
                s.secondaryAirSpeed = 11f; // suction
                s.contactDamagePerSecond = 45f;
                s.shotsPerVolley = 5;
                s.spreadAngle = 50f;
                s.projectileSpeed = 34f;
                s.projectileScale = 2.5f;
                EditorUtility.SetDirty(s);
            }
            return s;
        }

        // ------------------------------------------------------------------ shared parts

        internal static Material WeakSpotMat()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(StageKit.Root + "Art/M_BossWeakSpot.mat");
            return m != null ? m : StageKit.Emissive("M_BossWeakSpot", new Color(1f, 0.8f, 0.2f), new Color(0.2f, 0.15f, 0.05f));
        }

        static void SetCommon(AirBoss boss, AirBossSettings settings, Renderer[] glow, Transform muzzle, GameObject reinforcements,
                              ParticleSystem burst, Light[] mood)
        {
            BuilderUtil.SetRef(boss, "settings", settings);
            BuilderUtil.SetRef(boss, "muzzle", muzzle);
            BuilderUtil.SetRef(boss, "projectilePrefab", AssetDatabase.LoadAssetAtPath<StapleProjectile>(P + "Staple.prefab"));
            BuilderUtil.SetRef(boss, "reinforcements", reinforcements);
            BuilderUtil.SetRef(boss, "defeatBurst", burst);
            var so = new SerializedObject(boss);
            var g = so.FindProperty("weakSpotGlow");
            g.arraySize = glow.Length;
            for (int i = 0; i < glow.Length; i++) g.GetArrayElementAtIndex(i).objectReferenceValue = glow[i];
            var m = so.FindProperty("moodLights");
            m.arraySize = mood.Length;
            for (int i = 0; i < mood.Length; i++) m.GetArrayElementAtIndex(i).objectReferenceValue = mood[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AddTarget(GameObject root, string name, Transform aim)
        {
            var t = root.AddComponent<CombatTarget>();
            var so = new SerializedObject(t);
            so.FindProperty("displayName").stringValue = name;
            so.FindProperty("faction").enumValueIndex = (int)Faction.Accounting;
            so.FindProperty("isBoss").boolValue = true;
            so.FindProperty("aimPoint").objectReferenceValue = aim;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static GameObject Reinforcements(Transform parent, Vector3[] localSpots)
        {
            var go = new GameObject("Reinforcements");
            go.transform.SetParent(parent, false);
            for (int i = 0; i < localSpots.Length; i++)
            {
                var sp = StageKit.RivalSpawner($"ReinforcementSpawner_{i + 1}", go.transform, Vector3.zero, i * 180f, 1f + i * 2f);
                sp.transform.localPosition = localSpots[i];
            }
            go.SetActive(false);
            return go;
        }

        static ParticleSystem Burst(Transform parent, Vector3 localPos)
        {
            var go = new GameObject("DefeatBurst");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var ps = Stage2Builder.AddParticles(go, Stage2Builder.ParticleMaterial("M_PaperBits", null, additive: false), false, 0.4f, 1f);
            var main = ps.main;
            main.loop = false; main.prewarm = false; main.playOnAwake = false;
            main.duration = 1f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(10f, 26f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 4f);
            main.gravityModifier = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 300) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 55f;
            shape.radius = 6f;
            return ps;
        }

        static GameObject LocalBox(string name, Transform parent, Vector3 local, Vector3 size, Material mat, bool collider = true)
        {
            var go = StageKit.Box(name, parent, Vector3.zero, size, mat, collider);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.identity;
            return go;
        }

        static GameObject LocalCyl(string name, Transform parent, Vector3 local, Vector3 size, Material mat, Vector3 euler, bool collider = true)
        {
            var go = StageKit.Cyl(name, parent, Vector3.zero, size, mat, collider);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(euler);
            return go;
        }

        static Light LocalLight(Transform parent, Vector3 local, Color c, float intensity, float range)
        {
            var l = StageKit.PointLight("MoodLight", parent, Vector3.zero, c, intensity, range);
            l.transform.localPosition = local;
            return l;
        }

        // ------------------------------------------------------------------ Stage 29: parts, drones, shield

        /// <summary>A shootable boss part at 'local': BossPart on a new object with a box collider. Fill its look as children.</summary>
        internal static BossPart Part(Transform parent, string name, string partName, Vector3 local, Vector3 colliderSize, float health, float bossDamage)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            var part = go.AddComponent<BossPart>();
            var so = new SerializedObject(part);
            so.FindProperty("partName").stringValue = partName;
            so.FindProperty("maxHealth").floatValue = health;
            so.FindProperty("bossDamageOnDestroy").floatValue = bossDamage;
            so.ApplyModifiedPropertiesWithoutUndo();
            var look = new GameObject("Look");
            look.transform.SetParent(go.transform, false);
            var col = look.AddComponent<BoxCollider>();
            col.size = colliderSize;
            var t = go.AddComponent<CombatTarget>();
            var tso = new SerializedObject(t);
            tso.FindProperty("displayName").stringValue = partName;
            tso.FindProperty("faction").enumValueIndex = (int)Faction.Accounting;
            tso.FindProperty("aiTarget").boolValue = true;
            tso.ApplyModifiedPropertiesWithoutUndo();
            return part;
        }

        /// <summary>Finishes a part: what flashes, what disappears, its burst and smoke.</summary>
        internal static void FinishPart(BossPart part, Renderer[] flash, GameObject[] hide)
        {
            var burst = StageKit29.Burst(part.transform, "PartBurst", Vector3.zero, 120, 18f, 0.8f);
            var smoke = StageKit29.Smoke(part.transform, Vector3.zero);
            var list = new System.Collections.Generic.List<GameObject>(hide) { part.transform.Find("Look").gameObject };
            StageKit29.SetArray(part, "flashRenderers", flash);
            StageKit29.SetArray(part, "hideWhenDestroyed", list.ToArray());
            BuilderUtil.SetRef(part, "destroyBurst", burst);
            BuilderUtil.SetRef(part, "smoke", smoke);
        }

        internal static Material PartMat() => StageKit.Emissive("M_BossPart", new Color(0.45f, 0.12f, 0.08f), new Color(0.4f, 0.1f, 0.05f));

        /// <summary>The drones a boss launches to shield itself (rival spawners, off until it calls them).</summary>
        internal static GameObject Drones(Transform parent, Vector3[] localSpots)
        {
            var go = new GameObject("Drones");
            go.transform.SetParent(parent, false);
            for (int i = 0; i < localSpots.Length; i++)
            {
                var sp = StageKit.RivalSpawner($"DroneSpawner_{i + 1}", go.transform, Vector3.zero, i * 180f, 0.6f + i * 0.4f);
                sp.transform.localPosition = localSpots[i];
            }
            go.SetActive(false);
            return go;
        }

        internal static GameObject Shield(Transform parent, Vector3 local, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Shield";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localScale = size;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = StageKit29.Glass("M_BossShield", new Color(0.35f, 0.75f, 1f, 0.16f));
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.SetActive(false);
            return go;
        }

        // ------------------------------------------------------------------ THE MAINFRAME

        static GameObject BuildMainframe(AirBossSettings settings)
        {
            var rack = AssetDatabase.LoadAssetAtPath<Material>(StageKit.Root + "Art/M_Rack.mat") ?? StageKit.Mat("M_Rack", new Color(0.1f, 0.1f, 0.12f));
            var shell = StageKit.Mat("M_MainframeShell", new Color(0.16f, 0.18f, 0.22f), 0.45f, 0.5f);
            var fanMat = StageKit.Mat("M_MainframeFan", new Color(0.08f, 0.08f, 0.09f), 0.3f, 0.4f);
            var weak = WeakSpotMat();

            var root = new GameObject("Mainframe");
            var t = root.transform;
            LocalBox("Cabinet", t, new Vector3(0f, 35f, 0f), new Vector3(40f, 70f, 24f), rack);
            LocalBox("TopFrame", t, new Vector3(0f, 70.5f, 0f), new Vector3(42f, 1f, 26f), shell, collider: false);
            LocalBox("SideL", t, new Vector3(-20.5f, 35f, 0f), new Vector3(1f, 70f, 25f), shell, collider: false);
            LocalBox("SideR", t, new Vector3(20.5f, 35f, 0f), new Vector3(1f, 70f, 25f), shell, collider: false);

            // Two big exhaust fans on the front: each is a part you can shoot off (Stage 29).
            var rotors = new Transform[2];
            var fanParts = new BossPart[2];
            var partMat = PartMat();
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -10f : 10f;
                var fanPart = Part(t, i == 0 ? "FanPart_L" : "FanPart_R", "FAN", new Vector3(x, 45f, 13f), new Vector3(17f, 17f, 3f), 160f, 0.1f);
                var ring = LocalCyl("FanRing", fanPart.transform, new Vector3(0f, 0f, -0.4f), new Vector3(18.5f, 0.6f, 18.5f), partMat, new Vector3(90f, 0f, 0f), collider: false);
                fanParts[i] = fanPart;
                LocalCyl("FanHousing", t, new Vector3(x, 45f, 12.4f), new Vector3(17f, 1f, 17f), fanMat, new Vector3(90f, 0f, 0f), collider: false);
                var rotor = new GameObject("FanRotor").transform;
                rotor.SetParent(t, false);
                rotor.localPosition = new Vector3(x, 45f, 13.2f);
                for (int b = 0; b < 5; b++)
                {
                    var blade = LocalBox("Blade", rotor, Vector3.zero, new Vector3(2.4f, 7f, 0.3f), shell, collider: false);
                    blade.transform.localRotation = Quaternion.Euler(0f, 0f, b * 72f) * Quaternion.Euler(0f, 25f, 0f);
                    blade.transform.localPosition = blade.transform.localRotation * new Vector3(0f, 3.8f, 0f);
                }
                rotors[i] = rotor;
                FinishPart(fanPart, new[] { ring.GetComponent<Renderer>() }, new[] { rotor.gameObject });
            }

            // The heat-sink hatch on top (hinged at the back) with the glowing CPU under it: the weak spot.
            var cpu = LocalBox("CPU_WeakSpot", t, new Vector3(0f, 71.1f, 0f), new Vector3(16f, 0.8f, 12f), weak, collider: false);
            var hatch = new GameObject("HatchHinge").transform;
            hatch.SetParent(t, false);
            hatch.localPosition = new Vector3(0f, 71.6f, -7f);
            var lid = LocalBox("HatchLid", hatch, new Vector3(0f, 0f, 7f), new Vector3(20f, 0.8f, 14f), shell, collider: false);
            for (int i = 0; i < 6; i++)
                LocalBox("HeatFin", hatch, new Vector3(-7.5f + i * 3f, 1f, 7f), new Vector3(0.6f, 2f, 13f), fanMat, collider: false);

            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(t, false);
            muzzle.localPosition = new Vector3(0f, 58f, 14f);

            // Air: the sweeping fan beam, the hot exhaust from the hatch, a cold purge in front.
            var sweep = StageKit.Beam("SweepFan", t, Vector3.zero, Quaternion.identity, new Vector2(40f, 36f), 120f, settings.airSpeed, strength: 0f);
            sweep.transform.localPosition = new Vector3(0f, 45f, 13f);
            sweep.transform.localRotation = Quaternion.identity;
            var exhaust = StageKit.Column("Exhaust", t, Vector3.zero, 12f, 70f, settings.secondaryAirSpeed, heat: true, strength: 0f);
            exhaust.transform.localPosition = new Vector3(0f, 71.5f, 0f);
            var purge = StageKit.Column("Purge", t, Vector3.zero, 18f, 105f, -settings.secondaryAirSpeed, strength: 0f);
            purge.transform.localPosition = new Vector3(0f, 0f, 38f);

            var mood = new[] { LocalLight(t, new Vector3(-15f, 60f, 30f), new Color(0.5f, 0.75f, 1f), 3f, 70f),
                               LocalLight(t, new Vector3(15f, 60f, 30f), new Color(0.5f, 0.75f, 1f), 3f, 70f) };
            var reinforce = Reinforcements(t, new[] { new Vector3(-50f, 80f, 50f), new Vector3(50f, 80f, 50f) });
            var burst = Burst(t, new Vector3(0f, 72f, 0f));

            var drones = Drones(t, new[] { new Vector3(-40f, 85f, 45f), new Vector3(40f, 85f, 45f) });
            var shield = Shield(t, new Vector3(0f, 38f, 4f), new Vector3(62f, 86f, 48f));

            var boss = root.AddComponent<MainframeBoss>();
            SetCommon(boss, settings, new[] { cpu.GetComponent<Renderer>() }, muzzle, reinforce, burst, mood);
            BuilderUtil.SetRef(boss, "drones", drones);
            BuilderUtil.SetRef(boss, "shieldVisual", shield);
            StageKit29.SetArray(boss, "fanParts", fanParts);
            BuilderUtil.SetRef(boss, "sweepBeam", sweep);
            BuilderUtil.SetRef(boss, "exhaust", exhaust);
            BuilderUtil.SetRef(boss, "purge", purge);
            BuilderUtil.SetRef(boss, "hatch", hatch);
            var so = new SerializedObject(boss);
            var r = so.FindProperty("fanRotors");
            r.arraySize = 2;
            r.GetArrayElementAtIndex(0).objectReferenceValue = rotors[0];
            r.GetArrayElementAtIndex(1).objectReferenceValue = rotors[1];
            so.ApplyModifiedPropertiesWithoutUndo();
            AddTarget(root, "THE MAINFRAME", cpu.transform);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, P + "Mainframe.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ------------------------------------------------------------------ THE CONDENSER

        static GameObject BuildCondenser(AirBossSettings settings)
        {
            var body = StageKit.Mat("M_CondenserBody", new Color(0.72f, 0.74f, 0.72f), 0.35f, 0.4f);
            var fins = StageKit.Mat("M_CondenserFins", new Color(0.3f, 0.32f, 0.34f), 0.3f, 0.6f);
            var dark = StageKit.Mat("M_DarkMetal", new Color(0.12f, 0.12f, 0.14f));
            var bladeMat = StageKit.Mat("M_CondenserBlade", new Color(0.2f, 0.22f, 0.25f), 0.4f, 0.5f);
            var weak = WeakSpotMat();

            var root = new GameObject("Condenser");
            var t = root.transform;
            LocalBox("Unit", t, new Vector3(0f, 13f, 0f), new Vector3(56f, 26f, 56f), body);
            // Fin grilles on all four sides.
            foreach (var (pos, size) in new[] { (new Vector3(0f, 13f, 28.1f), new Vector3(48f, 20f, 0.3f)), (new Vector3(0f, 13f, -28.1f), new Vector3(48f, 20f, 0.3f)),
                                                (new Vector3(28.1f, 13f, 0f), new Vector3(0.3f, 20f, 48f)), (new Vector3(-28.1f, 13f, 0f), new Vector3(0.3f, 20f, 48f)) })
                LocalBox("FinGrille", t, pos, size, fins, collider: false);
            for (int i = 0; i < 4; i++)
                LocalBox("Foot", t, new Vector3(i < 2 ? -24f : 24f, 0.75f, i % 2 == 0 ? -24f : 24f), new Vector3(6f, 1.5f, 6f), dark);

            // The fan shroud, blades and the hub on top.
            LocalCyl("Shroud", t, new Vector3(0f, 28f, 0f), new Vector3(42f, 4f, 42f), dark, Vector3.zero);
            var rotor = new GameObject("FanRotor").transform;
            rotor.SetParent(t, false);
            rotor.localPosition = new Vector3(0f, 28.5f, 0f);
            for (int b = 0; b < 5; b++)
            {
                var blade = LocalBox("Blade", rotor, Vector3.zero, new Vector3(4.5f, 0.4f, 16f), bladeMat, collider: false);
                blade.transform.localRotation = Quaternion.Euler(0f, b * 72f, 0f) * Quaternion.Euler(0f, 0f, 18f);
                blade.transform.localPosition = Quaternion.Euler(0f, b * 72f, 0f) * new Vector3(0f, 0f, 10f);
            }
            var hub = LocalCyl("Hub_WeakSpot", t, new Vector3(0f, 29.5f, 0f), new Vector3(8f, 2f, 8f), weak, Vector3.zero, collider: false);
            for (int i = -3; i <= 3; i++)   // guard bars across the top (you can see the blades through them)
                LocalBox("GuardBar", t, new Vector3(i * 6f, 30.4f, 0f), new Vector3(0.5f, 0.5f, 40f), dark, collider: false);

            var grilleTop = new GameObject("GrilleTop").transform;
            grilleTop.SetParent(t, false);
            grilleTop.localPosition = new Vector3(0f, 30.5f, 0f);
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(t, false);
            muzzle.localPosition = new Vector3(0f, 34f, 0f);

            var column = StageKit.Column("FanColumn", t, Vector3.zero, 22f, 130f, settings.airSpeed, radial: 2f, strength: 0f);
            column.transform.localPosition = new Vector3(0f, 30.5f, 0f);
            column.ramp = 1.5f;
            var shock = StageKit.Column("Shockwave", t, Vector3.zero, 80f, 55f, 0f, radial: settings.airSpeed, strength: 0f);
            shock.transform.localPosition = Vector3.zero;
            shock.ramp = 1.2f;
            shock.coreFraction = 0.2f;

            // Particle streaks for the two fan directions (the boss turns their emission up and down).
            var blowPs = StageKit.Emitter(t, "BlowStreaks", new Vector3(0f, 31f, 0f), new Vector3(-90f, 0f, 0f), StageKit.StreakMat, true, 0.08f, 0.18f);
            var bshape = blowPs.shape; bshape.shapeType = ParticleSystemShapeType.Cone; bshape.angle = 0f; bshape.radius = 17f;
            var bmain = blowPs.main; bmain.startSpeed = 14f; bmain.startLifetime = 6f; bmain.simulationSpace = ParticleSystemSimulationSpace.World;
            var suckPs = StageKit.Emitter(t, "SuckStreaks", new Vector3(0f, 120f, 0f), new Vector3(90f, 0f, 0f), StageKit.StreakMat, true, 0.08f, 0.18f);
            var sshape = suckPs.shape; sshape.shapeType = ParticleSystemShapeType.Cone; sshape.angle = 0f; sshape.radius = 20f;
            var smain = suckPs.main; smain.startSpeed = 12f; smain.startLifetime = 7.5f; smain.simulationSpace = ParticleSystemSimulationSpace.World;
            foreach (var ps in new[] { blowPs, suckPs }) { var em = ps.emission; em.rateOverTime = 0f; }

            var mood = new[] { LocalLight(t, new Vector3(0f, 45f, -40f), new Color(1f, 0.85f, 0.6f), 3f, 90f),
                               LocalLight(t, new Vector3(0f, 45f, 40f), new Color(1f, 0.85f, 0.6f), 3f, 90f) };
            var reinforce = Reinforcements(t, new[] { new Vector3(-70f, 100f, 0f), new Vector3(70f, 100f, 0f) });
            var burst = Burst(t, new Vector3(0f, 31f, 0f));

            // Four coolant tanks on its corners: parts (Stage 29). Each one gone = a longer stall, thinner rings.
            var tankMat = StageKit.Emissive("M_CoolantTank", new Color(0.3f, 0.75f, 0.85f), new Color(0.05f, 0.3f, 0.4f));
            int ti = 0;
            foreach (var (tx, tz) in new[] { (-23f, -23f), (23f, -23f), (-23f, 23f), (23f, 23f) })
            {
                var tank = Part(t, $"CoolantTank_{++ti}", "COOLANT TANK", new Vector3(tx, 32f, tz), new Vector3(7f, 12f, 7f), 110f, 0.06f);
                var tankBody = LocalCyl("Tank", tank.transform, Vector3.zero, new Vector3(6f, 11f, 6f), tankMat, Vector3.zero, collider: false);
                var cap = LocalCyl("TankCap", tank.transform, new Vector3(0f, 6f, 0f), new Vector3(4f, 1.2f, 4f), dark, Vector3.zero, collider: false);
                FinishPart(tank, new[] { tankBody.GetComponent<Renderer>() }, new[] { tankBody, cap });
            }
            var drones = Drones(t, new[] { new Vector3(-60f, 70f, 0f), new Vector3(60f, 70f, 0f) });
            var shield = Shield(t, new Vector3(0f, 18f, 0f), new Vector3(90f, 60f, 90f));

            var boss = root.AddComponent<CondenserBoss>();
            SetCommon(boss, settings, new[] { hub.GetComponent<Renderer>() }, muzzle, reinforce, burst, mood);
            BuilderUtil.SetRef(boss, "drones", drones);
            BuilderUtil.SetRef(boss, "shieldVisual", shield);
            BuilderUtil.SetRef(boss, "column", column);
            BuilderUtil.SetRef(boss, "shockwave", shock);
            BuilderUtil.SetRef(boss, "fanRotor", rotor);
            BuilderUtil.SetRef(boss, "grilleTop", grilleTop);
            BuilderUtil.SetRef(boss, "blowStreaks", blowPs);
            BuilderUtil.SetRef(boss, "suckStreaks", suckPs);
            AddTarget(root, "THE CONDENSER", hub.transform);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, P + "Condenser.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }
    }
}
