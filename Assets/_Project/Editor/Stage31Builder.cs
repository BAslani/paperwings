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
    /// Stage 31: the server room comes alive.
    /// Menu: PaperWings > Stage Builders > Stage 31 (Server Room Machinery)
    ///
    ///  - FLOOR WAVES: the cold-aisle floor vents pulse in sequence, a wave of lift travelling down
    ///    each aisle. Surf it.
    ///  - TAPE ROBOT: a gantry spans the room under the ceiling and shuttles along it; at every stop
    ///    its arms plunge into the cold aisles. Solid: it sweeps the space above the racks.
    ///  - CABLE CURTAIN: bundles of cable hang from the ceiling between the racks and the Mainframe.
    ///  - COOLANT PIPES glowing along the ceiling (looks).
    ///  - ROOM EVENTS: HALON PURGE (sirens, red strobes, all the air dumps toward the floor except a
    ///    warm shelter column by the east wall) and BLACKOUT (lights out, cooling stops, the racks glow).
    ///  - SQUADRON: two Marketing wingmen and two extra rivals, so the server room has a Squadron Battle.
    /// Run Stage 25 first. Safe to re-run.
    /// </summary>
    public static class Stage31Builder
    {
        const float W = 130f, D = 100f, H = 110f;
        const float RackX0 = -95f, RackX1 = 40f;
        static Vector3 origin;
        static Vector3 A(float x, float y, float z) => origin + new Vector3(x, y, z);

        [MenuItem("PaperWings/Stage Builders/Stage 31 (Server Room Machinery)", priority = 142)]
        public static void Build()
        {
            if (!StageKit.OpenDesk(out Scene scene)) return;
            if (!Run(scene)) return;
            StageKit.Save(scene);
            EditorUtility.DisplayDialog("PaperWings", "Stage 31 built: floor waves, the tape robot, the cable curtain, halon and blackout events, and a server-room squadron.", "Boot");
        }

        internal static bool Run(Scene scene)
        {
            var room = StageKit.RoomAt(StageKit.ServerRoom);
            if (room == null) { EditorUtility.DisplayDialog("PaperWings", "No server room (run Stage 25).", "OK"); return false; }
            origin = room.transform.position;
            FloorWaves(room);
            var machinery = StageKit29.FreshGroup(room.transform, "Machinery");
            TapeRobot(machinery);
            Cables(machinery);
            Events(room);
            Squadron(room);
            EditorUtility.SetDirty(room);
            return true;
        }

        // ------------------------------------------------------------------ floor waves

        static void FloorWaves(Room room)
        {
            var air = room.transform.Find("Air");
            if (air == null) air = StageKit.Group(room.transform, "Air");
            // Replace the steady cold-aisle beams (Stage 25) with pulsing segments.
            for (int i = air.childCount - 1; i >= 0; i--)
            {
                var c = air.GetChild(i);
                if (c.name == "ColdAisleUp" || c.name.StartsWith("ColdWave")) Object.DestroyImmediate(c.gameObject);
            }
            const int segments = 5;
            float len = (RackX1 - RackX0) / segments, aisleW = 44f - 12f - 4f;
            foreach (float z in new[] { -44f, 44f })
            {
                for (int s = 0; s < segments; s++)
                {
                    float x = RackX0 + len * (s + 0.5f);
                    // West to east: each segment fires 0.8 s after the one before.
                    StageKit.Beam($"ColdWave_{(z < 0 ? "S" : "N")}{s + 1}", air, A(x, 0.5f, z), Quaternion.Euler(-90f, 0f, 0f),
                                  new Vector2(len, aisleW), 85f, 10f, onTime: 2.2f, offTime: 1.8f, offset: -s * 0.8f);
                }
            }
        }

        // ------------------------------------------------------------------ tape robot

        static void TapeRobot(Transform parent)
        {
            var metal = StageKit.Mat("M_GantryMetal", new Color(0.85f, 0.65f, 0.15f), 0.4f, 0.5f);
            var dark = StageKit.Mat("M_DarkMetal", new Color(0.12f, 0.12f, 0.14f));
            var beacon = StageKit.Emissive("M_Beacon", new Color(1f, 0.6f, 0.1f), new Color(2.5f, 1.4f, 0.2f));

            // Rails along both long walls.
            foreach (float z in new[] { -D + 3f, D - 3f })
                StageKit.Box("GantryRail", parent, A((RackX0 + RackX1) * 0.5f, H - 6f, z), new Vector3(RackX1 - RackX0 + 20f, 2f, 3f), dark, collider: false);

            var robot = new GameObject("TapeRobot").transform;
            robot.SetParent(parent, false);
            robot.position = A(RackX0, H - 9f, 0f);
            StageKit.Box("Bridge", robot, A(RackX0, H - 9f, 0f), new Vector3(6f, 3.5f, D * 2f - 6f), metal);
            StageKit.Box("BridgeStripe", robot, A(RackX0, H - 7f, 0f), new Vector3(6.2f, 0.6f, D * 2f - 6f), dark, collider: false);
            StageKit.Box("Beacon", robot, A(RackX0, H - 6.5f, 0f), new Vector3(2f, 1.2f, 2f), beacon, collider: false);
            var lamp = StageKit.PointLight("BeaconLight", robot, A(RackX0, H - 10f, 0f), new Color(1f, 0.6f, 0.15f), 2.5f, 40f);

            // The arm rig: one arm over each cold aisle, dropping together at each stop.
            var arms = new GameObject("Arms").transform;
            arms.SetParent(robot, false);
            arms.position = A(RackX0, H - 11f, 0f);
            foreach (float z in new[] { -44f, 44f })
            {
                StageKit.Box("Carriage", arms, A(RackX0, H - 11.5f, z), new Vector3(8f, 3f, 8f), metal);
                StageKit.Box("Arm", arms, A(RackX0, H - 25f, z), new Vector3(2.4f, 24f, 2.4f), dark);
                StageKit.Box("Claw", arms, A(RackX0, H - 38f, z), new Vector3(6f, 2.5f, 4f), metal);
            }
            StageKit29.Kinematic(robot.gameObject);

            var mover = robot.gameObject.AddComponent<ShuttleMover>();
            float y = H - 9f;
            mover.points = new[] { new Vector3(RackX0, y, 0f), new Vector3(-55f, y, 0f), new Vector3(-15f, y, 0f), new Vector3(25f, y, 0f) };
            for (int i = 0; i < mover.points.Length; i++) mover.points[i] += origin - parent.position;   // local to the parent
            mover.speed = 13f;
            mover.pause = 2.6f;
            mover.arm = arms;
            mover.armDrop = 24f;
            mover.armSpeed = 28f;
        }

        // ------------------------------------------------------------------ cables and pipes

        static void Cables(Transform parent)
        {
            var cable = StageKit.Mat("M_Cable", new Color(0.05f, 0.05f, 0.06f), 0.3f);
            var coolant = StageKit.Emissive("M_CoolantPipe", new Color(0.2f, 0.6f, 0.8f), new Color(0.2f, 0.9f, 1.4f));
            var curtain = StageKit.Group(parent, "CableCurtain");
            int i = 0;
            for (float z = -92f; z <= 92f; z += 7f, i++)
            {
                if (i % 4 == 2) continue;   // gaps to fly through
                float len = 16f + (i * 37 % 26);
                StageKit.Box("CableBundle", curtain, A(52f + (i % 2) * 3f, H - len * 0.5f, z), new Vector3(1f, len, 1f), cable);
            }
            var pipes = StageKit.Group(parent, "CoolantPipes");
            foreach (float z in new[] { -80f, -30f, 30f, 80f })
                StageKit.Cyl("CoolantPipe", pipes, A((RackX0 + RackX1) * 0.5f, H - 3f, z), new Vector3(2.4f, RackX1 - RackX0 + 30f, 2.4f), coolant, collider: false, euler: new Vector3(0f, 0f, 90f));
        }

        // ------------------------------------------------------------------ events

        static void Events(Room room)
        {
            var events = StageKit29.FreshGroup(room.transform, "Events");
            var lightsGroup = room.transform.Find("Lights");
            var lights = lightsGroup != null ? StageKit29.LightsUnder(lightsGroup) : new Light[0];

            // ---- HALON PURGE ----
            var halon = StageKit.Beam("HalonDump", events, A(0f, H - 1f, 0f), Quaternion.Euler(90f, 0f, 0f), new Vector2(W * 2f - 10f, D * 2f - 10f), H - 2f, 6.5f, strength: 0f);
            var shelter = StageKit.Column("HalonShelter", events, A(88f, 0f, -60f), 18f, H - 5f, 11f, heat: true, strength: 0f);
            var fog = StageKit.Emitter(events, "HalonFog", Vector3.zero, new Vector3(90f, 0f, 0f), StageKit.ShimmerMat, false, 4f, 8f);
            fog.transform.position = A(-20f, H - 2f, 0f);
            { var m = fog.main; m.startColor = new Color(0.95f, 0.95f, 1f, 0.3f); m.startSpeed = 6f; m.startLifetime = 9f; m.maxParticles = 1500; m.simulationSpace = ParticleSystemSimulationSpace.World;
              var sh = fog.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(200f, 160f, 1f);
              var em = fog.emission; em.rateOverTime = 90f; em.enabled = false; }
            var purge = StageKit29.Event(events, "HalonPurge", "HALON PURGE!", "All the air is dumping toward the floor: shelter in the warm column by the south-east wall.",
                                         new Color(1f, 0.3f, 0.25f), 25f, 3.5f, 6f, 32f);
            purge.air = new AirSource[] { halon, shelter };
            purge.lights = lights;
            purge.lightScale = 1.2f;
            purge.lightColor = new Color(1f, 0.15f, 0.1f, 1f);
            purge.strobe = true;
            purge.loops = new[] { fog };
            purge.warnSound = Audio.Sfx.Alarm;
            purge.activeSound = Audio.Sfx.BossSlam;

            // ---- BLACKOUT ----
            var emergency = new Light[3];
            int e = 0;
            foreach (var (x, z) in new[] { (-W + 2f, 0f), (0f, D - 2f), (0f, -D + 2f) })
                emergency[e++] = StageKit.PointLight("EmergencyLight", events, A(x, H - 15f, z), new Color(1f, 0.15f, 0.1f), 2.5f, 90f);
            var air = room.transform.Find("Air");
            var cooling = new System.Collections.Generic.List<AirSource>();
            if (air != null)
                foreach (var b in air.GetComponentsInChildren<BeamAir>(true))
                    if (b.name == "CoolingBlow" || b.name == "HotAisleReturn") cooling.Add(b);
            var blackout = StageKit29.Event(events, "Blackout", "BLACKOUT!", "Power cut: the cooling stops and only the racks glow. Watch for trails.",
                                            new Color(0.75f, 0.8f, 1f), 50f, 2f, 9f, 34f);
            blackout.lights = lights;
            blackout.lightScale = 0.04f;
            blackout.eventLights = emergency;
            blackout.airOff = cooling.ToArray();
            blackout.warnSound = Audio.Sfx.BossClank;
            blackout.activeSound = Audio.Sfx.BossSlam;
            blackout.closeTime = 0.3f;
        }

        // ------------------------------------------------------------------ squadron

        static void Squadron(Room room)
        {
            var g = StageKit29.FreshGroup(room.transform, "ServerSquadron");
            var wingman = StageKit.Load<GameObject>("Prefabs/WingmanDart.prefab");
            var player = StageKit.Player();
            foreach (var (name, offset, hand) in new[] { ("Wingman_Left", new Vector3(-7f, 1f, -6f), new Vector3(-3f, 0.6f, -1.5f)),
                                                         ("Wingman_Right", new Vector3(7f, 1f, -6f), new Vector3(3f, 0.6f, -1.5f)) })
            {
                var go = new GameObject(name);
                go.transform.SetParent(g, false);
                var a = go.AddComponent<AllySpawner>();
                BuilderUtil.SetRef(a, "wingmanPrefab", wingman);
                BuilderUtil.SetRef(a, "leader", player);
                var so = new SerializedObject(a);
                so.FindProperty("formationOffset").vector3Value = offset;
                so.FindProperty("handOffset").vector3Value = hand;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            StageKit.RivalSpawner("SquadronRival_1", g, A(-60f, 90f, -66f), 90f, 4f);
            StageKit.RivalSpawner("SquadronRival_2", g, A(-60f, 92f, 66f), 90f, 8f);
            g.gameObject.SetActive(false);
            room.squadronGroup = g.gameObject;
        }
    }
}
