using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using PaperWings.Air;
using PaperWings.Gameplay;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// Stage 25: new places to fly (the stairwell it once built was removed in Stage 42).
    /// Menu: PaperWings > Stage Builders > Stage 25 (Server Room, Rooftop)
    ///
    ///  SERVER ROOM (room 2, 750 west): four rows of racks with alternating aisles. Cold aisles blow
    ///    straight up from the floor (green), the hot aisle rises warm (orange), and cooling units on
    ///    the walls blow along the aisles (cyan), so the air circulates in a loop you can ride.
    ///    The open east end is the Mainframe's arena (Stage 26).
    ///  (Room 3 was the stairwell: removed in Stage 42. Its slot stays empty so room numbers don't move.)
    ///  ROOFTOP (room 4, 900 south): outdoors at last. Gusting wind across the whole roof, exhaust
    ///    stacks to climb, an updraft along the windward edge, a water tower to fly under, a billboard
    ///    with a gap in it, and the city around you. The Condenser's spot is in the middle (Stage 26).
    ///
    /// Each room has its own throw point, ring route, and (server room, rooftop) rival spawners.
    /// Rooms sit far apart in the same scene, past the fog. Safe to re-run.
    /// </summary>
    public static class Stage25Builder
    {
        public static readonly Vector3 ServerOrigin = new Vector3(-750f, 0f, 0f);
        public static readonly Vector3 RoofOrigin = new Vector3(0f, 0f, -900f);

        [MenuItem("PaperWings/Stage Builders/Stage 25 (Server Room, Rooftop)", priority = 125)]
        public static void Build()
        {
            if (!StageKit.OpenDesk(out Scene scene)) return;
            if (!Run(scene)) return;
            StageKit.Save(scene);
            EditorUtility.DisplayDialog("PaperWings",
                "Stage 25 built: two new rooms.\n\n" +
                "- Server Room: hot and cold aisles, cooling units. Free Flight, Ring Run, Dogfight.\n" +
                "- Rooftop: gusting wind, exhaust stacks, a ridge updraft, water tower, billboard. Free Flight, Ring Run, Dogfight.\n\n" +
                "Pick them with Left/Right on the main menu. Run Stage 26 for their bosses.", "Explore");
        }

        internal static bool Run(Scene scene)
        {
            if (StageKit.Director() == null)
            {
                EditorUtility.DisplayDialog("PaperWings", "No ModeDirector in DeskPrototype. Run the earlier stages first.", "OK");
                return false;
            }
            // Make sure the office and break room keep their slots before adding rooms 2-4.
            if (StageKit.RoomAt(StageKit.Office) == null || StageKit.RoomAt(StageKit.BreakRoom) == null)
            {
                EditorUtility.DisplayDialog("PaperWings", "Run Build Stage 21/22 first (the office and break room rooms).", "OK");
                return false;
            }
            StageKit.SetRoom(StageKit.ServerRoom, BuildServerRoom(scene));
            StageKit.SetRoom(StageKit.Rooftop, BuildRooftop(scene));
            return true;
        }

        // ================================================================== SERVER ROOM

        static Room BuildServerRoom(Scene scene)
        {
            Vector3 o = ServerOrigin;
            Vector3 A(float x, float y, float z) => o + new Vector3(x, y, z);
            const float W = 130f, D = 100f, H = 110f;          // half width (x), half depth (z), height
            const float RackX0 = -95f, RackX1 = 40f, RackH = 60f, RackDepth = 12f;
            float rackLen = RackX1 - RackX0, rackMid = (RackX0 + RackX1) * 0.5f;
            float[] rows = { -66f, -22f, 22f, 66f };

            var root = StageKit.FreshRoot(scene, "ServerRoom", o);

            // ---- Materials ----
            var floorTex = BuilderUtil.CheckerTexture(StageKit.Root + "Art/T_ServerTiles.asset", new Color(0.2f, 0.21f, 0.24f), new Color(0.16f, 0.17f, 0.2f));
            var floorMat = StageKit.Textured("M_ServerFloor", Color.white, floorTex, new Vector2(26f, 20f));
            var wallMat = StageKit.Mat("M_ServerWall", new Color(0.22f, 0.25f, 0.3f));
            var ceilMat = StageKit.Mat("M_ServerCeiling", new Color(0.3f, 0.32f, 0.36f));
            var rack = StageKit.Textured("M_Rack", Color.white, TextureFactory.RackFront(), new Vector2(6f, 2f));
            rack.EnableKeyword("_EMISSION");
            rack.SetTexture("_EmissionMap", TextureFactory.RackFront());
            rack.SetColor("_EmissionColor", new Color(0.9f, 0.9f, 0.9f));
            rack.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(rack);
            var rackSide = StageKit.Mat("M_RackSide", new Color(0.07f, 0.075f, 0.09f), 0.4f, 0.4f);
            var tray = StageKit.Mat("M_CableTray", new Color(0.55f, 0.57f, 0.6f), 0.4f, 0.6f);
            var cold = StageKit.Emissive("M_ColdTile", new Color(0.3f, 0.5f, 0.7f), new Color(0.05f, 0.2f, 0.35f));
            var warm = StageKit.Emissive("M_HotTile", new Color(0.5f, 0.35f, 0.25f), new Color(0.3f, 0.1f, 0.02f));
            var crac = StageKit.Mat("M_CoolingUnit", new Color(0.82f, 0.84f, 0.86f), 0.3f, 0.2f);
            var grille = StageKit.Mat("M_Grille", new Color(0.15f, 0.16f, 0.18f), 0.3f, 0.5f);
            var strip = StageKit.Emissive("M_ServerStrip", new Color(0.8f, 0.9f, 1f), new Color(1.6f, 1.9f, 2.4f));

            // ---- Shell ----
            var shell = StageKit.Group(root, "Shell");
            StageKit.Slab("Floor", shell, A(0f, 0f, 0f), W * 2f, D * 2f, floorMat, true);
            StageKit.Slab("Ceiling", shell, A(0f, H, 0f), W * 2f, D * 2f, ceilMat, false);
            StageKit.Wall("WestWall", shell, A(-W, H * 0.5f, 0f), Vector3.right, D * 2f, H, wallMat);
            StageKit.Wall("EastWall", shell, A(W, H * 0.5f, 0f), Vector3.left, D * 2f, H, wallMat);
            StageKit.Wall("NorthWall", shell, A(0f, H * 0.5f, D), Vector3.back, W * 2f, H, wallMat);
            StageKit.Wall("SouthWall", shell, A(0f, H * 0.5f, -D), Vector3.forward, W * 2f, H, wallMat);

            // ---- Rack rows (fronts face the cold aisles, backs the hot aisle) ----
            var racks = StageKit.Group(root, "Racks");
            foreach (float z in rows)
            {
                var row = StageKit.Group(racks, "RackRow");
                StageKit.Box("Rack", row, A(rackMid, RackH * 0.5f, z), new Vector3(rackLen, RackH, RackDepth), rack);
                StageKit.Box("RackTop", row, A(rackMid, RackH + 0.3f, z), new Vector3(rackLen + 1f, 0.6f, RackDepth + 1f), rackSide, collider: false);
                for (float x = RackX0; x <= RackX1 + 0.1f; x += 15f)
                    StageKit.Box("Divider", row, A(x, RackH * 0.5f, z), new Vector3(0.8f, RackH, RackDepth + 0.4f), rackSide, collider: false);
                // Overhead cable tray on rods above each row.
                StageKit.Box("Tray", row, A(rackMid, 74f, z), new Vector3(rackLen, 1f, 8f), tray);
                for (float x = RackX0 + 10f; x < RackX1; x += 40f)
                    StageKit.Cyl("TrayRod", row, A(x, (H + 74f) * 0.5f, z), new Vector3(0.4f, H - 74f, 0.4f), tray, collider: false);
            }

            // ---- Air: cold aisles blow up, the hot aisle rises warm, cooling units blow along the aisles ----
            var air = StageKit.Group(root, "Air");
            float aisleW = (rows[1] - rows[0]) - RackDepth - 4f;
            foreach (float z in new[] { -44f, 44f })
            {
                StageKit.Box("ColdTiles", air, A(rackMid, 0.05f, z), new Vector3(rackLen, 0.1f, aisleW), cold, collider: false);
                StageKit.Beam("ColdAisleUp", air, A(rackMid, 0.5f, z), Quaternion.Euler(-90f, 0f, 0f), new Vector2(rackLen, aisleW), 78f, 7.5f);
            }
            StageKit.Box("HotTiles", air, A(rackMid, 0.05f, 0f), new Vector3(rackLen, 0.1f, aisleW), warm, collider: false);
            StageKit.Beam("HotAisleUp", air, A(rackMid, 0.5f, 0f), Quaternion.Euler(-90f, 0f, 0f), new Vector2(rackLen, aisleW), 100f, 5.5f, heat: true);

            // Cooling units on the west wall push air east along the cold aisles; one on the
            // east side of the hot aisle pulls it back west (a loop you can ride).
            foreach (float z in new[] { -44f, 44f })
            {
                StageKit.Box("CoolingUnit", air, A(-W + 6f, 27.5f, z), new Vector3(12f, 55f, 24f), crac);
                StageKit.Box("CoolingGrille", air, A(-W + 12.05f, 32f, z), new Vector3(0.1f, 26f, 20f), grille, collider: false);
                StageKit.Beam("CoolingBlow", air, A(-W + 12.5f, 32f, z), Quaternion.Euler(0f, 90f, 0f), new Vector2(22f, 26f), 170f, 9f);
            }
            StageKit.Beam("HotAisleReturn", air, A(RackX1 + 4f, 40f, 0f), Quaternion.Euler(0f, -90f, 0f), new Vector2(22f, 30f), 150f, 7f);

            // ---- Lights: cold strips over the aisles ----
            var lights = StageKit.Group(root, "Lights");
            foreach (float z in new[] { -88f, -44f, 0f, 44f, 88f })
                StageKit.Box("LightStrip", lights, A(rackMid, H - 0.5f, z), new Vector3(rackLen, 0.6f, 2.5f), strip, collider: false);
            foreach (var (x, z) in new[] { (-60f, -44f), (20f, -44f), (-60f, 44f), (20f, 44f), (85f, 0f) })
                StageKit.PointLight("ServerLight", lights, A(x, H - 12f, z), new Color(0.75f, 0.88f, 1f), 4f, 120f, shadows: x > 80f);

            // ---- Throw point: on top of the south-west cooling unit, facing down the cold aisle ----
            var spawn = StageKit.Spawn("ServerSpawn", root, A(-W + 6f, 58f, -44f), 90f);

            // ---- Ring route: down a cold aisle, U-turn, back up the hot aisle, U-turn, down the other cold aisle ----
            var route = StageKit.Route("ServerRoute", root, "ServerRoom");
            StageKit.Ring(route.transform, "Ring1_ColdAisle", A(-40f, 52f, -44f), 90f);
            StageKit.Ring(route.transform, "Ring2_EastTurn", A(64f, 50f, -22f), 0f);
            StageKit.Ring(route.transform, "Ring3_HotAisle", A(-20f, 60f, 0f), -90f);
            StageKit.Ring(route.transform, "Ring4_WestTurn", A(-112f, 66f, 22f), 0f);
            StageKit.Ring(route.transform, "Ring5_ColdAisleNorth", A(-30f, 60f, 44f), 90f);
            StageKit.Ring(route.transform, "Ring6_IntoTheArena", A(62f, 72f, 44f), 90f);
            route.gameObject.SetActive(false);

            // ---- Rivals (Dogfight) ----
            var combat = StageKit.Group(root, "ServerCombat");
            StageKit.RivalSpawner("RivalSpawner_1", combat, A(0f, 85f, -44f), 90f, 2f);
            StageKit.RivalSpawner("RivalSpawner_2", combat, A(60f, 88f, 44f), -90f, 6f);
            combat.gameObject.SetActive(false);

            // ---- Boss slot (filled by Stage 26) ----
            var boss = StageKit.Group(root, "ServerBoss");
            boss.gameObject.SetActive(false);

            var room = root.gameObject.AddComponent<Room>();
            room.displayName = "Server Room";
            room.description = "Cold aisles blow up, the hot aisle rises, the cooling units push you round.";
            room.spawnPoint = spawn;
            room.routeGroup = route.gameObject;
            room.combatGroup = combat.gameObject;
            room.ringRunMedals = new Vector3(70f, 52f, 40f);
            return room;
        }

        // ================================================================== ROOFTOP

        static Room BuildRooftop(Scene scene)
        {
            Vector3 o = RoofOrigin;
            Vector3 A(float x, float y, float z) => o + new Vector3(x, y, z);
            const float Half = 150f;

            var root = StageKit.FreshRoot(scene, "Rooftop", o);

            var concrete = StageKit.Textured("M_RoofConcrete", Color.white, TextureFactory.Concrete(), new Vector2(12f, 12f));
            var parapet = StageKit.Mat("M_Parapet", new Color(0.45f, 0.43f, 0.42f));
            var metal = StageKit.Mat("M_RoofMetal", new Color(0.5f, 0.52f, 0.55f), 0.45f, 0.6f);
            var darkMetal = StageKit.Mat("M_DarkMetal", new Color(0.12f, 0.12f, 0.14f));
            var hutMat = StageKit.Mat("M_StairHut", new Color(0.55f, 0.5f, 0.45f));
            var tankMat = StageKit.Mat("M_WaterTank", new Color(0.45f, 0.32f, 0.22f), 0.2f);
            var adA = StageKit.Emissive("M_BillboardA", new Color(1f, 0.6f, 0.2f), new Color(1.6f, 0.8f, 0.2f));
            var adB = StageKit.Emissive("M_BillboardB", new Color(0.3f, 0.6f, 1f), new Color(0.3f, 0.8f, 1.8f));
            var red = StageKit.Emissive("M_BeaconRed", new Color(1f, 0.1f, 0.1f), new Color(3f, 0.2f, 0.2f));
            var city = StageKit.Emissive("M_CityLights", new Color(1f, 0.85f, 0.5f), new Color(1.6f, 1.2f, 0.6f));
            var building = StageKit.Mat("M_Building", new Color(0.12f, 0.13f, 0.17f));

            // ---- Roof deck and parapet ----
            var deck = StageKit.Group(root, "Deck");
            StageKit.Slab("Roof", deck, A(0f, 0f, 0f), Half * 2f, Half * 2f, concrete, true);
            foreach (var (pos, size) in new[] {
                         (A(0f, 6f, Half), new Vector3(Half * 2f + 4f, 12f, 4f)), (A(0f, 6f, -Half), new Vector3(Half * 2f + 4f, 12f, 4f)),
                         (A(Half, 6f, 0f), new Vector3(4f, 12f, Half * 2f)), (A(-Half, 6f, 0f), new Vector3(4f, 12f, Half * 2f)) })
                StageKit.Box("Parapet", deck, pos, size, parapet);
            // Invisible walls and lid a little beyond the parapet, so you can't fly away into the city.
            var bounds = StageKit.Group(root, "Bounds");
            const float B = 168f, Top = 240f;
            foreach (var (pos, size) in new[] {
                         (A(0f, Top * 0.5f, B), new Vector3(B * 2f, Top, 2f)), (A(0f, Top * 0.5f, -B), new Vector3(B * 2f, Top, 2f)),
                         (A(B, Top * 0.5f, 0f), new Vector3(2f, Top, B * 2f)), (A(-B, Top * 0.5f, 0f), new Vector3(2f, Top, B * 2f)),
                         (A(0f, Top, 0f), new Vector3(B * 2f, 2f, B * 2f)) })
            {
                var go = new GameObject("InvisibleWall");
                go.transform.SetParent(bounds, false);
                go.transform.position = pos;
                go.AddComponent<BoxCollider>().size = size;
            }

            // ---- Props ----
            var props = StageKit.Group(root, "Props");
            // Stair hut (the way you came up), with a door and a lamp.
            StageKit.Box("StairHut", props, A(-110f, 25f, 110f), new Vector3(40f, 50f, 40f), hutMat);
            StageKit.Box("HutDoor", props, A(-110f, 22f, 89.9f), new Vector3(20f, 44f, 0.4f), darkMetal, collider: false);
            StageKit.PointLight("HutLamp", props, A(-110f, 46f, 86f), new Color(1f, 0.85f, 0.6f), 3f, 60f);
            // Water tower: four legs, a tank, a roof cap.
            foreach (var (dx, dz) in new[] { (-13f, -13f), (13f, -13f), (-13f, 13f), (13f, 13f) })
                StageKit.Cyl("TowerLeg", props, A(110f + dx, 25f, -110f + dz), new Vector3(2.4f, 50f, 2.4f), darkMetal);
            StageKit.Cyl("Tank", props, A(110f, 66f, -110f), new Vector3(40f, 32f, 40f), tankMat);
            StageKit.Cyl("TankRoof", props, A(110f, 84f, -110f), new Vector3(30f, 4f, 30f), darkMetal);
            // Antenna mast with a blinking-red beacon (emissive).
            StageKit.Cyl("Mast", props, A(-130f, 70f, -130f), new Vector3(2f, 140f, 2f), metal);
            StageKit.Cyl("Beacon", props, A(-130f, 141f, -130f), new Vector3(3f, 2f, 3f), red, collider: false);
            StageKit.PointLight("BeaconLight", props, A(-130f, 141f, -130f), new Color(1f, 0.15f, 0.1f), 2f, 40f);
            // Billboard on stilts with a gap between its two panels (fly through!).
            StageKit.Box("BillboardLeft", props, A(-34f, 45f, 100f), new Vector3(52f, 36f, 2f), adA);
            StageKit.Box("BillboardRight", props, A(34f, 45f, 100f), new Vector3(52f, 36f, 2f), adB);
            StageKit.Box("BillboardTop", props, A(0f, 64f, 100f), new Vector3(120f, 2f, 3f), darkMetal);
            foreach (float x in new[] { -50f, -18f, 18f, 50f })
                StageKit.Box("BillboardPost", props, A(x, 13.5f, 101.5f), new Vector3(2f, 27f, 2f), darkMetal);
            // Small AC boxes and skylights for clutter.
            foreach (var (x, z) in new[] { (-40f, -110f), (-10f, -120f), (130f, 40f) })
                StageKit.Box("ACBox", props, A(x, 5f, z), new Vector3(14f, 10f, 10f), metal);
            var sky = StageKit.Emissive("M_Skylight", new Color(0.5f, 0.6f, 0.8f), new Color(0.4f, 0.55f, 0.9f));
            foreach (var (x, z) in new[] { (-60f, -90f), (20f, 60f) })
                StageKit.Box("Skylight", props, A(x, 1.5f, z), new Vector3(22f, 3f, 14f), sky);

            // ---- The city around (for looks, past the bounds) ----
            var cityRoot = StageKit.Group(root, "City");
            for (int i = 0; i < 28; i++)
            {
                float ang = i * (360f / 28f) + (i * 37 % 11);
                float dist = 240f + (i * 53 % 90);
                float h = 60f + (i * 71 % 170);
                Vector3 p = Quaternion.Euler(0f, ang, 0f) * Vector3.forward * dist;
                float bw = 40f + (i * 29 % 40);
                StageKit.Box("Building", cityRoot, A(p.x, h * 0.5f - 160f, p.z), new Vector3(bw, h + 160f, bw), building, collider: false);
                for (int k = 0; k < 6; k++)   // a few lit windows on the side facing the roof
                {
                    Vector3 face = -p.normalized * (bw * 0.5f + 0.2f);
                    float wx = ((k * 17 + i * 5) % 30) - 15f, wy = h - 10f - (k * 23 % Mathf.Max(20f, h - 20f));
                    Vector3 side = Vector3.Cross(Vector3.up, p.normalized) * wx;
                    var win = StageKit.Box("Window", cityRoot, A(p.x, 0f, p.z) + face + side + Vector3.up * wy, new Vector3(3f, 2f, 0.2f), city, collider: false);
                    win.transform.rotation = Quaternion.LookRotation(-p.normalized);
                }
            }

            // ---- Air: wind, the windward ridge updraft, exhaust stacks ----
            var air = StageKit.Group(root, "Air");
            StageKit.Wind("RoofWind", air, A(0f, 110f, 0f), new Vector3(Half * 2f, 220f, Half * 2f), new Vector3(-6.5f, 0f, 1.5f));
            // Wind from the east hits the east parapet and rises: ridge lift along that edge.
            StageKit.Beam("EastEdgeUpdraft", air, A(Half - 10f, 0.5f, 0f), Quaternion.Euler(-90f, 0f, 0f), new Vector2(20f, Half * 2f - 20f), 90f, 6.5f);
            foreach (var (x, z, name) in new[] { (-60f, 40f, "Stack1"), (60f, -40f, "Stack2"), (100f, 70f, "Stack3") })
            {
                StageKit.Cyl(name, props, A(x, 8f, z), new Vector3(6f, 16f, 6f), darkMetal);
                StageKit.Cyl(name + "Cap", props, A(x, 16.5f, z), new Vector3(7.5f, 1f, 7.5f), metal, collider: false);
                var col = StageKit.Column(name + "Heat", air, A(x, 17f, z), 11f, 140f, 9f, heat: true);
                col.coreFraction = 0.45f;
            }

            // ---- Throw points ----
            var spawn = StageKit.Spawn("RooftopSpawn", root, A(-110f, 52f, 110f), 135f);   // on the stair hut
            var tower = StageKit.Spawn("WaterTowerSpawn", root, A(110f, 92f, -110f), -45f);  // above the water tower roof (Stage 32: raised from 88)

            // ---- Ring route ----
            var route = StageKit.Route("RooftopRoute", root, "Rooftop");
            StageKit.Ring(route.transform, "Ring1_Stack1", A(-60f, 80f, 40f), 90f);
            StageKit.Ring(route.transform, "Ring2_BillboardGap", A(0f, 45f, 100f), 0f);
            StageKit.Ring(route.transform, "Ring3_Stack2", A(60f, 95f, -40f), 0f);
            StageKit.Ring(route.transform, "Ring4_UnderTheTank", A(110f, 30f, -110f), 135f);
            StageKit.Ring(route.transform, "Ring5_EastRidge", A(140f, 55f, 0f), 0f);
            StageKit.Ring(route.transform, "Ring6_Stack3", A(100f, 110f, 70f), -90f);
            route.gameObject.SetActive(false);

            // ---- Rivals (Dogfight "Squall") ----
            var combat = StageKit.Group(root, "RooftopCombat");
            StageKit.RivalSpawner("RivalSpawner_1", combat, A(0f, 90f, -60f), 0f, 2f);
            StageKit.RivalSpawner("RivalSpawner_2", combat, A(-60f, 100f, 60f), 120f, 5f);
            StageKit.RivalSpawner("RivalSpawner_3", combat, A(80f, 95f, 60f), 240f, 9f);
            combat.gameObject.SetActive(false);

            var boss = StageKit.Group(root, "RooftopBoss");
            boss.gameObject.SetActive(false);

            var room = root.gameObject.AddComponent<Room>();
            room.displayName = "Rooftop";
            room.description = "Real wind, exhaust stacks, an updraft on the windward edge.";
            room.spawnPoint = spawn;
            room.routeGroup = route.gameObject;
            room.combatGroup = combat.gameObject;
            room.extraSpawns = new[] { tower };
            room.ringRunMedals = new Vector3(75f, 55f, 42f);
            return room;
        }
    }
}
