using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using PaperWings.Combat;
using PaperWings.Gameplay;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// Stage 32: the missions' flying parts, and the bosses rebuilt with parts and drones.
    /// Menu: PaperWings > Build Stage 32 (Missions, Boss Parts)
    ///
    ///  - OFFICE: practice drones circling the lounge (Target Practice), Accounting couriers flying
    ///    the window row to the door (Paper Trail).
    ///  - STAIRWELL: couriers spiralling up the chimney (Fire Escape), plus a rival spawner so the
    ///    stairwell can host dogfight-based nights.
    ///  - BOSSES: The Mainframe (shootable fans, firewall drones) and The Condenser (coolant tanks,
    ///    maintenance drones) rebuilt (Stage 26).
    ///  - Checks every campaign night's room, group and throw point.
    ///
    /// Also: PaperWings > Build Stages 29-33 (Air Combat Update) runs 29 to 33 in order.
    /// </summary>
    public static class Stage32Builder
    {
        [MenuItem("PaperWings/Build Stage 32 (Missions, Boss Parts)", priority = 143)]
        public static void Build()
        {
            if (!StageKit.OpenDesk(out Scene scene)) return;
            string report = Run(scene);
            StageKit.Save(scene);
            EditorUtility.DisplayDialog("PaperWings", "Stage 32 built.\n\n" + report, "Fly");
        }

        [MenuItem("PaperWings/Build Stages 29-33 (Air Combat Update)", priority = 144)]
        public static void BuildAll()
        {
            if (!StageKit.OpenDesk(out Scene scene)) return;
            string r29 = Stage29Builder.Run(scene);
            if (!Stage30Builder.Run(scene)) return;
            if (!Stage31Builder.Run(scene)) return;
            Run(scene);
            // Stage 33 last: it eases the Auditor that Stage 30 just rebuilt, and runs the final check.
            var office = StageKit.RoomAt(StageKit.Office);
            string r33 = office != null ? Stage33Builder.Run(scene, office) : "(no office room: Stage 33 skipped)";
            StageKit.Save(scene);
            EditorUtility.DisplayDialog("PaperWings",
                "Stages 29-33 built.\n\n" + r29 + "\n- Break room party, appliance events, THE AUDITOR.\n- Server room machinery, halon and blackouts, a squadron.\n" + r33, "Fly");
        }

        internal static string Run(Scene scene)
        {
            Office();
            Stairwell();
            RaiseTowerSpawn();
            Stage26Builder.Run(scene);   // bosses with parts and drones
            return Stage27Builder.Run(scene);
        }

        /// <summary>The Squall throw point sat 2 units over the tank roof; give a nose-down throw some room.</summary>
        static void RaiseTowerSpawn()
        {
            var roof = StageKit.RoomAt(StageKit.Rooftop);
            var spawn = roof != null ? roof.SpawnNamed("WaterTowerSpawn") : null;
            if (spawn == null) return;
            var p = spawn.position;
            spawn.position = new Vector3(p.x, Mathf.Max(p.y, roof.transform.position.y + 92f), p.z);
        }

        static PathFlyer Prefab(string name) => AssetDatabase.LoadAssetAtPath<PathFlyer>(StageKit29.P + name + ".prefab");

        static void Office()
        {
            var room = StageKit.RoomAt(StageKit.Office);
            if (room == null) return;

            // Target Practice: sticky-note drones looping round the lounge side of the office.
            var practice = StageKit29.FreshGroup(room.transform, "PracticeDrones");
            var loop = new Vector3[10];
            for (int i = 0; i < loop.Length; i++)
            {
                float a = i * Mathf.PI * 2f / loop.Length;
                loop[i] = new Vector3(-50f + Mathf.Cos(a) * 62f, 62f + Mathf.Sin(a * 2f) * 12f, -35f + Mathf.Sin(a) * 45f);
            }
            StageKit29.Convoy(practice, "DroneLoop", ConvoySpawner.Kind.Practice, Prefab("PracticeDrone"), loop, 99, 3, 2.5f, 6.5f, 12f, "Drone", 2f);
            StageKit29.AddExtraGroup(room, practice.gameObject);

            // Paper Trail: couriers along the window row and out of the door on the left wall.
            var couriers = StageKit29.FreshGroup(room.transform, "WindowCouriers");
            var trail = new[]
            {
                new Vector3(165f, 70f, 30f), new Vector3(125f, 76f, 70f), new Vector3(45f, 72f, 74f), new Vector3(-35f, 74f, 74f),
                new Vector3(-115f, 66f, 68f), new Vector3(-152f, 56f, 10f), new Vector3(-156f, 48f, -80f), new Vector3(-168f, 43f, -120f)
            };
            StageKit29.Convoy(couriers, "WindowConvoy", ConvoySpawner.Kind.Intercept, Prefab("CourierRed"), trail, 12, 2, 7f, 9f, 45f, "Courier", 4f);
            StageKit29.AddExtraGroup(room, couriers.gameObject);
            EditorUtility.SetDirty(room);
        }

        static void Stairwell()
        {
            var room = StageKit.RoomAt(StageKit.Stairwell);
            if (room == null) return;
            Vector3 o = room.transform.position;

            // Fire Escape: couriers spiral up the warm chimney to the roof door.
            var g = StageKit29.FreshGroup(room.transform, "StairCouriers");
            const int perTurn = 8;
            const float turns = 3.5f, r = 19f, y0 = 20f, y1 = 465f;
            int n = Mathf.RoundToInt(perTurn * turns);
            var pts = new Vector3[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                float a = t * turns * Mathf.PI * 2f;
                pts[i] = o + new Vector3(Mathf.Cos(a) * r, Mathf.Lerp(y0, y1, t), Mathf.Sin(a) * r);
            }
            StageKit29.Convoy(g, "ChimneyConvoy", ConvoySpawner.Kind.Intercept, Prefab("CourierRed"), pts, 12, 3, 8f, 10f, 45f, "Courier", 4f);
            StageKit29.AddExtraGroup(room, g.gameObject);

            // A rival spawner, so dogfight-based nights can run here.
            var combat = StageKit29.FreshGroup(room.transform, "StairCombat");
            StageKit.RivalSpawner("RivalSpawner_1", combat, o + new Vector3(0f, 160f, 0f), 0f, 5f);
            combat.gameObject.SetActive(false);
            room.combatGroup = combat.gameObject;
            EditorUtility.SetDirty(room);
        }
    }
}
