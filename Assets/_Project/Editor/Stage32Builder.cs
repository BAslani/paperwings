using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using PaperWings.Combat;
using PaperWings.Gameplay;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// Stage 32: the missions' flying parts, and the bosses rebuilt with parts and drones.
    /// Menu: PaperWings > Stage Builders > Stage 32 (Missions, Boss Parts)
    ///
    ///  - OFFICE: practice drones circling the lounge (Target Practice), Accounting couriers flying
    ///    the window row to the door (Paper Trail).
    ///  - BOSSES: The Mainframe (shootable fans, firewall drones) and The Condenser (coolant tanks,
    ///    maintenance drones) rebuilt (Stage 26).
    ///  - Checks every campaign night's room, group and throw point.
    ///
    /// Also: PaperWings > Build Stages 29-33 (Air Combat Update) runs 29 to 33 in order.
    /// </summary>
    public static class Stage32Builder
    {
        [MenuItem("PaperWings/Stage Builders/Stage 32 (Missions, Boss Parts)", priority = 143)]
        public static void Build()
        {
            if (!StageKit.OpenDesk(out Scene scene)) return;
            string report = Run(scene);
            StageKit.Save(scene);
            EditorUtility.DisplayDialog("PaperWings", "Stage 32 built.\n\n" + report, "Fly");
        }

        [MenuItem("PaperWings/Stage Builders/Stages 29-33 (Air Combat Update)", priority = 144)]
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
    }
}
