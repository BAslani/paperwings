using UnityEditor;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// Runs every room's "animated film" look pass (Stages 38-41 and 43) in one go: one scene save, one
    /// report. Use it after rebuilding rooms with the old stage builders, which put the plain
    /// versions back. Menu: PaperWings > Room Looks > Rebuild All Room Looks
    /// </summary>
    public static class RoomLooks
    {
        [MenuItem("PaperWings/Room Looks/Rebuild All Room Looks", priority = 50)]
        public static void RebuildAll()
        {
            if (!StageKit.OpenDesk(out var scene)) return;
            var report = new System.Text.StringBuilder();
            try
            {
                void Pass(string room, System.Func<string> run)
                {
                    string r = run();
                    report.Append(r != null ? $"{room}: done.\n" : $"{room}: skipped (the room isn't built yet).\n");
                }
                Pass("Office", () => Stage38Builder.Run(scene));
                Pass("Break Room", () => Stage39Builder.Run(scene));
                Pass("Server Room", () => Stage40Builder.Run(scene));
                Pass("Rooftop", () => Stage41Builder.Run(scene));
                Pass("Kitchen", () => Stage43Builder.Run(scene));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            StageKit.Save(scene);
            EditorUtility.DisplayDialog("PaperWings", "Room looks rebuilt.\n\n" + report, "Lovely");
        }
    }
}
