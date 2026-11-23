using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using PaperWings.Gameplay;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// Stage 24: the office's furniture becomes air and geometry.
    /// Menu: PaperWings > Build Stage 24 (Office Air)
    ///
    /// Until now the room around the desk was scenery. Now it is a level, in three height bands:
    ///   FLOOR (0-30):    a space heater by the lounge (rescue lift from near the floor) and a PC
    ///                    tower whose exhaust rises beside the coworker's desk.
    ///   WALLS:           radiators under both windows and along the left wall: long curtains of warm
    ///                    air you fly ALONG, like soaring a ridge (new RadiatorSource).
    ///   CEILING (85-125): a ceiling fan over the lounge (new CeilingFanSource: down in the middle,
    ///                    up around the edge), three pendant lamps with little warm columns, a cable
    ///                    tray to fly under, and a hanging sign.
    /// Plus a second office route, "WindowRoute" (radiator to radiator via the fan), and two named
    /// throw points (LoungeSpawn, CoworkerSpawn) that campaign nights use.
    ///
    /// Everything new lives under the "OfficeAir" root. Safe to re-run.
    /// </summary>
    public static class Stage24Builder
    {
        const float Left = -170f, Right = 170f, Back = 90f, Front = -150f, Ceiling = StageKit.CeilingY;

        [MenuItem("PaperWings/Build Stage 24 (Office Air)", priority = 124)]
        public static void Build()
        {
            if (!StageKit.OpenDesk(out Scene scene)) return;
            if (!Run(scene)) return;
            StageKit.Save(scene);
            EditorUtility.DisplayDialog("PaperWings",
                "Stage 24 built: the office is a level now.\n\n" +
                "- Radiators under both windows and along the left wall: fly along them to climb.\n" +
                "- A ceiling fan over the lounge: its edge lifts (green), its middle drops (violet). It rests every few seconds.\n" +
                "- A space heater, a PC tower's exhaust, pendant lamps, a cable tray and a hanging sign.\n" +
                "- New route 'WindowRoute' and throw points LoungeSpawn / CoworkerSpawn (used by the campaign).", "Fly it");
        }

        internal static bool Run(Scene scene)
        {
            var office = StageKit.RoomAt(StageKit.Office);
            if (office == null)
            {
                EditorUtility.DisplayDialog("PaperWings", "No office Room found. Run Build Stage 21 and 22 first.", "OK");
                return false;
            }

            MoveTallDrawers(scene);
            var root = StageKit.FreshRoot(scene, "OfficeAir", Vector3.zero);
            var radiator = StageKit.RadiatorAsset();
            var fanSettings = StageKit.CeilingFanAsset("CeilingFan_Office", 14f, 80f, 9f, 4f);
            var heater = StageKit.ThermalAsset("Thermal_Heater", 8.5f, 12f, 90f, 16f);
            var pcExhaust = StageKit.ThermalAsset("Thermal_PCExhaust", 6f, 7f, 50f, 8f);
            var pendant = StageKit.ThermalAsset("Thermal_Pendant", 5f, 7f, 20f, 6f);

            // ---- Walls: radiators ----
            var walls = StageKit.Group(root, "Radiators");
            StageKit.Radiator("Radiator_LeftWindow", walls, new Vector3(-110f, 0f, Back - 1.5f), 180f, 60f, radiator);
            StageKit.Radiator("Radiator_RightWindow", walls, new Vector3(110f, 0f, Back - 1.5f), 180f, 60f, radiator);
            StageKit.Radiator("Radiator_LeftWall", walls, new Vector3(Left + 1.5f, 0f, 10f), 90f, 100f, radiator);

            // ---- Ceiling: fan over the lounge, pendant lamps, cable tray, hanging sign ----
            var ceiling = StageKit.Group(root, "Ceiling");
            StageKit.CeilingFan("CeilingFan_Lounge", ceiling, new Vector3(-120f, 114f, -80f), Ceiling, fanSettings);

            var cord = StageKit.Mat("M_PendantCord", new Color(0.1f, 0.1f, 0.1f));
            var shade = StageKit.Emissive("M_PendantShade", new Color(1f, 0.85f, 0.6f), new Color(1.4f, 0.95f, 0.5f));
            foreach (var p in new[] { new Vector3(-55f, 100f, -40f), new Vector3(40f, 100f, -110f), new Vector3(95f, 100f, 20f) })
            {
                var lamp = StageKit.Group(ceiling, "PendantLamp");
                lamp.position = p;
                StageKit.Cyl("Cord", lamp, new Vector3(p.x, (Ceiling + p.y) * 0.5f + 1f, p.z), new Vector3(0.3f, Ceiling - p.y - 2f, 0.3f), cord, collider: false);
                StageKit.Cyl("Shade", lamp, p, new Vector3(8f, 4f, 8f), shade);
                StageKit.PointLight("PendantLight", lamp, p + Vector3.down * 4f, new Color(1f, 0.82f, 0.55f), 2.2f, 55f);
                StageKit.Thermal("PendantHeat", lamp, p + Vector3.up * 2.2f, pendant);
            }

            var steel = StageKit.Mat("M_CableTray", new Color(0.55f, 0.57f, 0.6f), 0.4f, 0.6f);
            var tray = StageKit.Group(ceiling, "CableTray");
            StageKit.Box("Tray", tray, new Vector3(0f, 104f, 40f), new Vector3(330f, 1.2f, 7f), steel);
            StageKit.Box("TrayLipA", tray, new Vector3(0f, 105.2f, 36.7f), new Vector3(330f, 1.6f, 0.4f), steel, collider: false);
            StageKit.Box("TrayLipB", tray, new Vector3(0f, 105.2f, 43.3f), new Vector3(330f, 1.6f, 0.4f), steel, collider: false);
            for (float x = -150f; x <= 150f; x += 60f)
                StageKit.Cyl("Hanger", tray, new Vector3(x, (Ceiling + 104f) * 0.5f, 40f), new Vector3(0.5f, Ceiling - 104f, 0.5f), steel, collider: false);

            var signBoard = StageKit.Mat("M_SignBoard", new Color(0.12f, 0.16f, 0.3f));
            var signText = StageKit.Emissive("M_SignText", new Color(0.9f, 0.95f, 1f), new Color(1.2f, 1.3f, 1.6f));
            var sign = StageKit.Group(ceiling, "HangingSign_Marketing");
            StageKit.Box("Board", sign, new Vector3(-10f, 105f, -40f), new Vector3(26f, 7f, 0.6f), signBoard, euler: new Vector3(0f, 70f, 0f));
            StageKit.Box("Lettering", sign, new Vector3(-10f, 105f, -40f), new Vector3(20f, 2.2f, 0.8f), signText, collider: false, euler: new Vector3(0f, 70f, 0f));
            foreach (float side in new[] { -10f, 10f })
            {
                Vector3 off = Quaternion.Euler(0f, 70f, 0f) * new Vector3(side, 0f, 0f);
                StageKit.Cyl("Wire", sign, new Vector3(-10f, 116.75f, -40f) + off, new Vector3(0.2f, 16.5f, 0.2f), cord, collider: false);
            }

            var exitGlow = StageKit.Emissive("M_ExitSign", new Color(0.2f, 0.8f, 0.3f), new Color(0.3f, 2f, 0.5f));
            StageKit.Box("ExitSign", ceiling, new Vector3(Left + 1f, 95f, Front + 30f), new Vector3(1f, 5f, 14f), exitGlow, collider: false);

            // ---- Floor: space heater by the lounge, PC tower by the coworker's desk ----
            var floor = StageKit.Group(root, "Floor");
            var heaterBody = StageKit.Mat("M_HeaterBody", new Color(0.82f, 0.8f, 0.75f), 0.3f, 0.3f);
            var heaterGlow = StageKit.Emissive("M_HeaterGlow", new Color(1f, 0.5f, 0.2f), new Color(2.2f, 0.8f, 0.2f));
            var heaterPos = new Vector3(-110f, 0f, -125f);
            StageKit.Box("SpaceHeater", floor, heaterPos + Vector3.up * 7f, new Vector3(9f, 14f, 6f), heaterBody);
            StageKit.Box("HeaterGrille", floor, heaterPos + new Vector3(0f, 8f, 3.05f), new Vector3(7f, 9f, 0.1f), heaterGlow, collider: false);
            StageKit.PointLight("HeaterGlow", floor, heaterPos + new Vector3(0f, 8f, 6f), new Color(1f, 0.5f, 0.2f), 1.5f, 22f);
            StageKit.Thermal("HeaterHeat", floor, heaterPos + Vector3.up * 2f, heater);

            var towerMat = StageKit.Mat("M_PCTower", new Color(0.08f, 0.08f, 0.09f), 0.4f);
            var led = StageKit.Emissive("M_PCLed", new Color(0.2f, 0.5f, 1f), new Color(0.3f, 0.8f, 2f));
            var towerPos = new Vector3(148f, 0f, -100f);
            StageKit.Box("PCTower", floor, towerPos + Vector3.up * 11f, new Vector3(9f, 22f, 20f), towerMat);
            StageKit.Box("PCLed", floor, towerPos + new Vector3(-4.55f, 19f, 6f), new Vector3(0.1f, 0.6f, 4f), led, collider: false);
            StageKit.Thermal("PCExhaust", floor, towerPos + new Vector3(0f, 22f, -12f), pcExhaust);

            // ---- Throw points for campaign nights ----
            var spawns = StageKit.Group(root, "Spawns");
            var lounge = StageKit.Spawn("LoungeSpawn", spawns, new Vector3(-122f, 12.5f, -92f), 165f);           // coffee table, facing the heater
            var coworker = StageKit.Spawn("CoworkerSpawn", spawns, new Vector3(88f, 33.5f, -91f), -90f);         // front edge of the coworker's desk (clear of the keyboard), facing the room

            // ---- WindowRoute: radiator to radiator, via the ceiling fan ----
            var route = StageKit.Route("WindowRoute", root, "OfficeWindows");
            StageKit.Spawn("Spawn", route.transform, new Vector3(-32f, 29f, 80f), -90f);                         // top of the chest of drawers
            StageKit.Ring(route.transform, "Ring1_LeftWindowRadiator", new Vector3(-112f, 36f, 83f), -90f);
            StageKit.Ring(route.transform, "Ring2_LeftWallRadiator", new Vector3(-162f, 60f, 0f), 180f);
            StageKit.Ring(route.transform, "Ring3_FanEdge", new Vector3(-120f, 100f, -101f), 90f);
            StageKit.Ring(route.transform, "Ring4_UnderTheSign", new Vector3(-10f, 90f, -40f), 70f);
            StageKit.Ring(route.transform, "Ring5_RightWindowRadiator", new Vector3(105f, 74f, 83f), 90f);
            StageKit.Ring(route.transform, "Ring6_PastTheBooks", new Vector3(140f, 70f, 30f), 180f);
            route.gameObject.SetActive(false);

            // ---- Wire the office room ----
            office.description = "Your desk, the lounge fan, radiators under the windows.";
            office.extraSpawns = new[] { lounge, coworker };
            office.extraRoutes = new[] { route.gameObject };
            EditorUtility.SetDirty(office);
            return true;
        }

        /// <summary>The tall drawer cabinet leaves the back-left corner for the front wall (drawers mirrored to face the room).</summary>
        static void MoveTallDrawers(Scene scene)
        {
            var officeRoom = StageKit.FindRoot(scene, "OfficeRoom");
            var drawers = officeRoom != null ? officeRoom.Find("Furniture/Drawers") : null;
            if (drawers == null) return;
            foreach (Transform t in drawers)
            {
                if (t.position.x > -140f) continue;   // only the corner cabinet (it stood at x = -155)
                float dz = t.position.z - 78f;
                t.position = new Vector3(133f + (t.position.x + 155f), t.position.y, -141f - dz);
            }
        }
    }
}
