using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using PaperWings.Air;
using PaperWings.Combat;
using PaperWings.Flight;
using PaperWings.Gameplay;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// Stage 30: the break room is throwing an office party, and the appliances have opinions.
    /// Menu: PaperWings > Build Stage 30 (Break Room Party, The Auditor)
    ///
    ///  - PARTY: balloon bunches tied to the table, the couch and the counter (shoot them to pop
    ///    them: confetti and a puff of warm air), bunting across the ceiling, a birthday cake whose
    ///    candles each send up a thin thermal.
    ///  - ROOM EVENTS (mid-fight set pieces, announced on the HUD):
    ///      MICROWAVE DING   the door pops open and a big column of heat roars up off the counter
    ///      FRIDGE DOOR      the door swings out across the room and cold air spills down
    ///      VENDING HAYWIRE  the vending machine sprays soda cans across the room (they hit everyone)
    ///  - COFFEE RUN: the escort route (Marketing couriers fly the coffee orders to the door).
    ///  - THE AUDITOR: a flying bomber boss folded from a ledger, circling under the ceiling.
    /// Run Stages 21-29 first. Safe to re-run.
    /// </summary>
    public static class Stage30Builder
    {
        static Vector3 origin;
        static Vector3 A(float x, float y, float z) => origin + new Vector3(x, y, z);

        [MenuItem("PaperWings/Build Stage 30 (Break Room Party, The Auditor)", priority = 141)]
        public static void Build()
        {
            if (!StageKit.OpenDesk(out Scene scene)) return;
            if (!Run(scene)) return;
            StageKit.Save(scene);
            EditorUtility.DisplayDialog("PaperWings", "Stage 30 built: the break room party, its appliance events, the Coffee Run escort and THE AUDITOR.", "Party");
        }

        internal static bool Run(Scene scene)
        {
            var room = StageKit.RoomAt(StageKit.BreakRoom);
            if (room == null) { EditorUtility.DisplayDialog("PaperWings", "No break room (run Stage 21/22).", "OK"); return false; }
            origin = room.transform.position;
            Party(room);
            Events(room);
            CoffeeRun(room);
            Auditor(room);
            EditorUtility.SetDirty(room);
            return true;
        }

        static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        // ------------------------------------------------------------------ the party

        static void Party(Room room)
        {
            var party = StageKit29.FreshGroup(room.transform, "Party");
            var colours = new[]
            {
                StageKit.Mat("M_BalloonRed", new Color(0.95f, 0.2f, 0.25f), 0.8f),
                StageKit.Mat("M_BalloonYellow", new Color(1f, 0.85f, 0.2f), 0.8f),
                StageKit.Mat("M_BalloonBlue", new Color(0.25f, 0.55f, 1f), 0.8f),
                StageKit.Mat("M_BalloonGreen", new Color(0.3f, 0.85f, 0.4f), 0.8f),
                StageKit.Mat("M_BalloonPink", new Color(1f, 0.45f, 0.75f), 0.8f),
            };
            var stringMat = StageKit.Mat("M_BalloonString", new Color(0.9f, 0.9f, 0.9f));

            // Bunches: anchor (where the strings are tied) and how many.
            var bunches = new (Vector3 anchor, int n)[]
            {
                (A(-5f, 25f, -22f), 5),     // the table
                (A(40f, 22f, -105f), 4),    // the couch back
                (A(-62f, 30f, 22f), 3),     // the counter end by the sink
                (A(105f, 30f, -45f), 3),    // the water cooler
                (A(-95f, 0f, -85f), 4),     // the corner by the door
            };
            int k = 0;
            foreach (var (anchor, n) in bunches)
            {
                for (int i = 0; i < n; i++, k++)
                {
                    float ang = i * (360f / n) + k * 17f;
                    Vector3 off = Quaternion.Euler(0f, ang, 0f) * Vector3.forward * (2.5f + (i % 2) * 2f);
                    float h = 22f + (k * 7 % 13);
                    Balloon(party, anchor, anchor + off + Vector3.up * h, colours[k % colours.Length], stringMat);
                }
            }

            // Bunting across the ceiling (no colliders: it's flimsy).
            var flagMats = new[] { colours[0], colours[1], colours[2], colours[3] };
            var cord = StageKit.Mat("M_BuntingCord", new Color(0.8f, 0.8f, 0.75f));
            Bunting(party, A(-128f, 108f, -100f), A(128f, 104f, 30f), flagMats, cord);
            Bunting(party, A(-128f, 104f, 30f), A(128f, 108f, -100f), flagMats, cord);
            Bunting(party, A(-128f, 100f, -35f), A(128f, 100f, -35f), flagMats, cord);

            // The cake on the table, candles sending up thin thermals.
            var cakeMat = StageKit.Mat("M_Cake", new Color(1f, 0.95f, 0.9f), 0.3f);
            var icing = StageKit.Mat("M_CakeIcing", new Color(1f, 0.55f, 0.7f), 0.5f);
            var flame = StageKit.Emissive("M_CandleFlame", new Color(1f, 0.7f, 0.2f), new Color(3f, 1.8f, 0.4f));
            StageKit.Cyl("Cake", party, A(-5f, 28f, -22f), new Vector3(12f, 6f, 12f), cakeMat);
            StageKit.Cyl("CakeIcing", party, A(-5f, 31.2f, -22f), new Vector3(12.4f, 0.6f, 12.4f), icing, collider: false);
            var air = StageKit.Group(party, "CandleAir");
            for (int i = 0; i < 5; i++)
            {
                Vector3 p = A(-5f, 31.5f, -22f) + Quaternion.Euler(0f, i * 72f, 0f) * Vector3.forward * 3.5f;
                StageKit.Cyl("Candle", party, p + Vector3.up * 1.5f, new Vector3(0.6f, 3f, 0.6f), colours[i % colours.Length], collider: false);
                StageKit.Box("Flame", party, p + Vector3.up * 3.4f, new Vector3(0.5f, 0.9f, 0.5f), flame, collider: false);
            }
            StageKit.Column("CandleHeat", air, A(-5f, 34f, -22f), 6f, 70f, 6.5f, heat: true);
            StageKit.PointLight("CandleGlow", party, A(-5f, 38f, -22f), new Color(1f, 0.7f, 0.35f), 1.5f, 20f);
        }

        static void Balloon(Transform parent, Vector3 anchor, Vector3 pos, Material mat, Material stringMat)
        {
            var root = new GameObject("Balloon");
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(5f, 6f, 5f);
            body.GetComponent<Renderer>().sharedMaterial = mat;
            var knot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            knot.name = "Knot";
            Object.DestroyImmediate(knot.GetComponent<Collider>());
            knot.transform.SetParent(body.transform, false);
            knot.transform.localPosition = new Vector3(0f, -0.52f, 0f);
            knot.transform.localScale = new Vector3(0.12f, 0.08f, 0.12f);
            knot.GetComponent<Renderer>().sharedMaterial = mat;

            // String down to the anchor (static: the balloon only bobs a little).
            Vector3 top = pos + Vector3.down * 3f;
            var str = StageKit.Box("String", root.transform, (top + anchor) * 0.5f, new Vector3(0.12f, Vector3.Distance(top, anchor), 0.12f), stringMat, collider: false);
            str.transform.rotation = Quaternion.FromToRotation(Vector3.up, top - anchor);

            var confetti = StageKit29.Confetti(root.transform, Vector3.zero, 70);
            var puff = StageKit.Column("Puff", root.transform, pos + Vector3.down * 2f, 6f, 26f, 7f, heat: true, strength: 0f);
            puff.ramp = 4f;

            var b = root.AddComponent<Balloon>();
            BuilderUtil.SetRef(b, "body", body.transform);
            BuilderUtil.SetRef(b, "popBurst", confetti);
            BuilderUtil.SetRef(b, "puff", puff);
        }

        static void Bunting(Transform parent, Vector3 from, Vector3 to, Material[] flags, Material cordMat)
        {
            var line = new GameObject("Bunting").transform;
            line.SetParent(parent, false);
            int n = Mathf.RoundToInt(Vector3.Distance(from, to) / 6f);
            Vector3 prev = from;
            for (int i = 1; i <= n; i++)
            {
                float t = i / (float)n;
                Vector3 p = Vector3.Lerp(from, to, t) + Vector3.down * Mathf.Sin(t * Mathf.PI) * 9f;   // it sags
                var seg = StageKit.Box("Cord", line, (prev + p) * 0.5f, new Vector3(0.15f, 0.15f, Vector3.Distance(prev, p)), cordMat, collider: false);
                seg.transform.rotation = Quaternion.LookRotation(p - prev);
                // A pennant hanging from the cord: a small diamond (rotated square), half above the cord.
                var flag = StageKit.Box("Flag", line, p + Vector3.down * 1.4f, new Vector3(2.4f, 2.4f, 0.08f), flags[i % flags.Length], collider: false);
                flag.transform.rotation = Quaternion.LookRotation(Vector3.Cross(Vector3.up, (p - prev).normalized)) * Quaternion.Euler(0f, 0f, 45f);
                prev = p;
            }
        }

        // ------------------------------------------------------------------ the appliances

        static void Events(Room room)
        {
            var events = StageKit29.FreshGroup(room.transform, "Events");
            var white = StageKit.Mat("M_ApplianceWhite", new Color(0.93f, 0.93f, 0.92f), 0.4f);
            var glass = StageKit.Emissive("M_MicrowaveDoorGlass", new Color(0.25f, 0.18f, 0.1f), new Color(1.4f, 0.8f, 0.3f));

            // ---- MICROWAVE DING: the door pops open, a column of heat roars up ----
            var micro = new GameObject("MicrowaveDoorHinge").transform;
            micro.SetParent(events, false);
            micro.position = A(-23f, 35f, 23.2f);
            var door = StageKit.Box("MicrowaveDoor", micro, A(-17f, 35f, 22.9f), new Vector3(12f, 9f, 0.6f), white);
            StageKit.Box("DoorGlass", micro, A(-18f, 35f, 22.55f), new Vector3(8f, 5.5f, 0.1f), glass, collider: false);
            StageKit29.Kinematic(door);
            var heat = StageKit.Column("MicrowaveBloom", events, A(-15f, 30f, 16f), 16f, 92f, 10.5f, heat: true, strength: 0f);
            heat.ramp = 1.6f;
            heat.coreFraction = 0.55f;
            var popcorn = StageKit.Emitter(events, "Popcorn", Vector3.zero, new Vector3(-90f, 0f, 0f), StageKit.ShimmerMat, false, 0.5f, 0.9f);
            popcorn.transform.position = A(-15f, 36f, 18f);
            { var m = popcorn.main; m.startColor = new Color(1f, 0.97f, 0.85f, 1f); m.startSpeed = new ParticleSystem.MinMaxCurve(10f, 22f); m.gravityModifier = 1f; m.startLifetime = 3f; m.simulationSpace = ParticleSystemSimulationSpace.World;
              var sh = popcorn.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 25f; sh.radius = 3f;
              var em = popcorn.emission; em.rateOverTime = 40f; em.enabled = false; }
            var ding = StageKit29.Event(events, "MicrowaveDing", "MICROWAVE: DING!", "A column of heat is roaring up off the counter: climb in it!",
                                        new Color(1f, 0.6f, 0.2f), 12f, 3f, 7f, 22f);
            ding.air = new AirSource[] { heat };
            ding.movers = new[] { micro };
            ding.openEuler = new[] { new Vector3(0f, 105f, 0f) };
            ding.openOffset = new[] { Vector3.zero };
            ding.loops = new[] { popcorn };
            ding.warnSound = Audio.Sfx.LockBeep;
            ding.activeSound = Audio.Sfx.Ding;
            ding.openTime = 0.25f;

            // ---- FRIDGE DOOR: it swings out across the room, cold air spills down ----
            var hinge = new GameObject("FridgeDoorHinge").transform;
            hinge.SetParent(events, false);
            hinge.position = A(66f, 36f, 18.4f);
            var fdoor = StageKit.Box("FridgeDoor", hinge, A(55f, 36f, 17.6f), new Vector3(22f, 70f, 1.6f), white);
            StageKit.Box("FridgeDoorHandle", hinge, A(45.5f, 40f, 16.5f), new Vector3(1f, 16f, 1f), StageKit.Mat("M_Steel", new Color(0.7f, 0.72f, 0.75f), 0.7f, 0.9f), collider: false);
            StageKit29.Kinematic(fdoor);
            var spill = StageKit.Beam("FridgeSpill", events, A(55f, 50f, 17f), Quaternion.Euler(55f, 180f, 0f), new Vector2(20f, 40f), 70f, 9f, strength: 0f);
            var fridgeLight = StageKit.PointLight("FridgeLight", events, A(55f, 45f, 12f), new Color(0.75f, 0.9f, 1f), 4f, 45f);
            var mist = StageKit.Emitter(events, "ColdMist", Vector3.zero, new Vector3(40f, 180f, 0f), StageKit.ShimmerMat, false, 2f, 4f);
            mist.transform.position = A(55f, 30f, 15f);
            { var m = mist.main; m.startColor = new Color(0.85f, 0.95f, 1f, 0.35f); m.startSpeed = 5f; m.startLifetime = 4f; m.simulationSpace = ParticleSystemSimulationSpace.World;
              var em = mist.emission; em.rateOverTime = 25f; em.enabled = false;
              var sh = mist.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(18f, 50f, 1f); }
            var fridge = StageKit29.Event(events, "FridgeDoor", "FRIDGE DOOR!", "It swings out across the room and cold air spills down: keep clear.",
                                          new Color(0.55f, 0.85f, 1f), 22f, 2f, 6f, 24f);
            fridge.air = new AirSource[] { spill };
            fridge.movers = new[] { hinge };
            fridge.openEuler = new[] { new Vector3(0f, -100f, 0f) };
            fridge.openOffset = new[] { Vector3.zero };
            fridge.eventLights = new[] { fridgeLight };
            fridge.loops = new[] { mist };
            fridge.warnSound = Audio.Sfx.BossClank;
            fridge.activeSound = Audio.Sfx.BossClack;

            // ---- VENDING HAYWIRE: soda cans spray across the room ----
            var vending = FindDeep(room.transform, "VendingMachine");
            var launcherGo = new GameObject("CanLauncher");
            launcherGo.transform.SetParent(vending != null ? vending : events, false);
            launcherGo.transform.position = A(-110.5f, 30f, -30f);
            launcherGo.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            var launcher = launcherGo.AddComponent<HazardLauncher>();
            BuilderUtil.SetRef(launcher, "projectilePrefab", AssetDatabase.LoadAssetAtPath<StapleProjectile>(StageKit29.P + "SodaCan.prefab"));
            launcherGo.SetActive(false);
            var vend = StageKit29.Event(events, "VendingHaywire", "VENDING MACHINE HAYWIRE!", "Cans are flying across the room. They hit everyone: drag a rival through them.",
                                        new Color(1f, 0.35f, 0.3f), 32f, 2.5f, 6f, 26f);
            vend.enableWhileActive = new[] { launcherGo };
            var vendLight = FindDeep(room.transform, "VendingGlow");
            if (vendLight != null && vendLight.GetComponent<Light>() != null)
            {
                vend.lights = new[] { vendLight.GetComponent<Light>() };
                vend.lightScale = 2.5f;
                vend.strobe = true;
                vend.lightColor = new Color(1f, 0.2f, 0.15f, 1f);
            }
        }

        // ------------------------------------------------------------------ escort: Coffee Run

        static void CoffeeRun(Room room)
        {
            var g = StageKit29.FreshGroup(room.transform, "CoffeeCourier");
            var prefab = AssetDatabase.LoadAssetAtPath<PathFlyer>(StageKit29.P + "CourierBlue.prefab");
            var pts = new[]
            {
                A(22f, 45f, 14f), A(55f, 55f, -12f), A(100f, 62f, -45f), A(85f, 70f, -92f), A(30f, 66f, -95f),
                A(-25f, 60f, -70f), A(-55f, 52f, -98f), A(-70f, 44f, -107f), A(-70f, 43f, -110f)
            };
            StageKit29.Convoy(g, "CoffeeConvoy", ConvoySpawner.Kind.Escort, prefab, pts, 2, 1, 4f, 7.5f, 140f, "Courier", 4f);
            StageKit29.AddExtraGroup(room, g.gameObject);
        }

        // ------------------------------------------------------------------ THE AUDITOR

        static AirBossSettings AuditorSettings()
        {
            string path = StageKit29.S + "Boss_Auditor.asset";
            bool isNew = AssetDatabase.LoadAssetAtPath<AirBossSettings>(path) == null;
            var s = Stage2Builder.LoadOrCreate<AirBossSettings>(path);
            if (isNew)
            {
                s.displayName = "THE AUDITOR";
                s.title = "Chief of Accounting";
                s.maxHealth = 650f;
                s.armoredMultiplier = 0.25f;
                s.weakSpotMultiplier = 2.5f;
                s.aggroRange = 220f;
                s.engageDelay = 3f;
                s.wakeUpTime = 1.5f;
                s.telegraph = 1.2f;
                s.attackDuration = 4f;
                s.vulnerableTime = 4.5f;
                s.restTime = 1.5f;
                s.secondaryAirSpeed = 9f;
                s.shotsPerVolley = 5;
                s.spreadAngle = 30f;
                s.projectileSpeed = 36f;
                s.projectileDamage = 8f;
                s.projectileLifetime = 2.5f;
                s.projectileScale = 2f;
                s.respawnDelay = 25f;
                EditorUtility.SetDirty(s);
            }
            return s;
        }

        static void Auditor(Room room)
        {
            var settings = AuditorSettings();
            var hullMesh = AssetDatabase.LoadAssetAtPath<Mesh>(StageKit.Root + "Art/PaperRaven_Mesh.asset");
            var hullMat = AssetDatabase.LoadAssetAtPath<Material>(StageKit.Root + "Art/M_PaperRival.mat") ?? StageKit.Mat("M_AuditorHull", new Color(0.2f, 0.2f, 0.23f));
            var dark = StageKit.Mat("M_DarkMetal", new Color(0.12f, 0.12f, 0.14f));
            var weak = AssetDatabase.LoadAssetAtPath<Material>(StageKit.Root + "Art/M_BossWeakSpot.mat") ?? StageKit.Emissive("M_BossWeakSpot", new Color(1f, 0.8f, 0.2f), new Color(0.2f, 0.15f, 0.05f));
            var partMat = Stage26Builder.PartMat();

            var root = new GameObject("Auditor");
            var t = root.transform;
            var body = new GameObject("Body").transform;
            body.SetParent(t, false);
            body.localPosition = new Vector3(85f, 75f, 0f);
            body.localRotation = Quaternion.Euler(0f, 0f, 0f);

            // The hull: a Raven folded from a whole ledger, scaled up twenty times.
            var hull = new GameObject("Hull");
            hull.transform.SetParent(body, false);
            hull.transform.localScale = Vector3.one * 20f;
            hull.AddComponent<MeshFilter>().sharedMesh = hullMesh;
            hull.AddComponent<MeshRenderer>().sharedMaterial = hullMat;
            // Simple solid colliders: fuselage and wing.
            var fus = new GameObject("FuselageCollider"); fus.transform.SetParent(body, false);
            fus.AddComponent<BoxCollider>().size = new Vector3(5f, 4f, 36f);
            var wing = new GameObject("WingCollider"); wing.transform.SetParent(body, false);
            wing.transform.localPosition = new Vector3(0f, 1.5f, -1f);
            wing.AddComponent<BoxCollider>().size = new Vector3(32f, 1.5f, 10f);
            StageKit29.Kinematic(body.gameObject);

            // Engine at the tail: the weak spot. Two pusher props beside it.
            var engine = StageKit.Cyl("Engine_WeakSpot", body, Vector3.zero, new Vector3(4f, 5f, 4f), weak, collider: false, euler: new Vector3(90f, 0f, 0f));
            engine.transform.localPosition = new Vector3(0f, 0.8f, -14f);
            engine.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var props = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                var prop = new GameObject("Propeller").transform;
                prop.SetParent(body, false);
                prop.localPosition = new Vector3(i == 0 ? -5f : 5f, 1f, -15f);
                for (int bl = 0; bl < 3; bl++)
                {
                    var blade = StageKit.Box("Blade", prop, Vector3.zero, new Vector3(0.8f, 4.5f, 0.2f), dark, collider: false);
                    blade.transform.localPosition = Quaternion.Euler(0f, 0f, bl * 120f) * new Vector3(0f, 2.2f, 0f);
                    blade.transform.localRotation = Quaternion.Euler(0f, 0f, bl * 120f);
                }
                props[i] = prop;
            }

            // Wing turrets: parts.
            var turrets = new BossPart[2];
            var muzzles = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -10f : 10f;
                var part = Stage26Builder.Part(body, i == 0 ? "Turret_L" : "Turret_R", "TURRET", new Vector3(x, 2.4f, 0f), new Vector3(4.5f, 3f, 4.5f), 130f, 0.08f);
                var look = part.transform.Find("Look");
                var baseCyl = StageKit.Cyl("TurretBase", look, Vector3.zero, new Vector3(4f, 2f, 4f), partMat, collider: false);
                baseCyl.transform.localPosition = Vector3.zero;
                var barrel = StageKit.Box("Barrel", look, Vector3.zero, new Vector3(0.8f, 0.8f, 5f), dark, collider: false);
                barrel.transform.localPosition = new Vector3(0f, 0.8f, 2.6f);
                var muzzle = new GameObject("TurretMuzzle").transform;
                muzzle.SetParent(part.transform, false);
                muzzle.localPosition = new Vector3(0f, 0.8f, 5.4f);
                Stage26Builder.FinishPart(part, new[] { baseCyl.GetComponent<Renderer>() }, new GameObject[0]);
                turrets[i] = part;
                muzzles[i] = muzzle;
            }

            var bay = new GameObject("BombBay").transform; bay.SetParent(body, false); bay.localPosition = new Vector3(0f, -2.5f, 0f);
            var gun = new GameObject("Muzzle").transform; gun.SetParent(body, false); gun.localPosition = new Vector3(0f, 3f, 8f);
            var wash = StageKit.Column("BellyWash", t, Vector3.zero, 13f, 70f, -settings.secondaryAirSpeed, strength: 0f);
            wash.transform.localPosition = body.localPosition + new Vector3(0f, -72f, 0f);
            var drones = Stage26Builder.Drones(t, new[] { new Vector3(-50f, 60f, 20f), new Vector3(50f, 60f, -20f) });
            var shield = Stage26Builder.Shield(body, Vector3.zero, new Vector3(44f, 18f, 44f));
            var burst = StageKit29.Burst(body, "DefeatBurst", Vector3.zero, 300, 26f, 1f);

            var boss = root.AddComponent<AuditorBoss>();
            BuilderUtil.SetRef(boss, "settings", settings);
            BuilderUtil.SetRef(boss, "muzzle", gun);
            BuilderUtil.SetRef(boss, "projectilePrefab", AssetDatabase.LoadAssetAtPath<StapleProjectile>(StageKit29.P + "Staple.prefab"));
            BuilderUtil.SetRef(boss, "defeatBurst", burst);
            BuilderUtil.SetRef(boss, "drones", drones);
            BuilderUtil.SetRef(boss, "shieldVisual", shield);
            BuilderUtil.SetRef(boss, "body", body);
            BuilderUtil.SetRef(boss, "bombBay", bay);
            BuilderUtil.SetRef(boss, "wash", wash);
            StageKit29.SetArray(boss, "weakSpotGlow", new[] { engine.GetComponent<Renderer>() });
            StageKit29.SetArray(boss, "turrets", turrets);
            StageKit29.SetArray(boss, "turretMuzzles", muzzles);
            StageKit29.SetArray(boss, "propellers", props);
            var so = new SerializedObject(boss);
            so.FindProperty("orbitRadius").vector2Value = new Vector2(85f, 40f);
            so.FindProperty("altitude").floatValue = 75f;
            so.FindProperty("boundsMin").vector3Value = new Vector3(-108f, 28f, -58f);
            so.FindProperty("boundsMax").vector3Value = new Vector3(108f, 105f, 52f);
            so.ApplyModifiedPropertiesWithoutUndo();

            var target = root.AddComponent<CombatTarget>();
            var tso = new SerializedObject(target);
            tso.FindProperty("displayName").stringValue = "THE AUDITOR";
            tso.FindProperty("faction").enumValueIndex = (int)Faction.Accounting;
            tso.FindProperty("isBoss").boolValue = true;
            tso.FindProperty("aimPoint").objectReferenceValue = engine.transform;
            tso.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, StageKit29.P + "Auditor.prefab");
            Object.DestroyImmediate(root);

            // Into the break room's boss group, at the middle of the room.
            var group = StageKit29.FreshGroup(room.transform, "BreakRoomBoss");
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
            inst.transform.SetPositionAndRotation(A(0f, 0f, -34f), Quaternion.identity);
            group.gameObject.SetActive(false);
            room.bossGroup = group.gameObject;
        }
    }
}
