using UnityEditor;
using UnityEngine;

namespace PaperWings.EditorTools
{
    /// <summary>
    /// One-click shortcuts to every tuning asset: PaperWings > Settings > ...
    /// Selecting one shows it in the Inspector and highlights it in the Project window.
    /// Works in Play mode too, so you can tune while flying (ScriptableObject edits are kept).
    /// </summary>
    public static class SettingsMenu
    {
        const string Dir = "Assets/_Project/Settings/";

        [MenuItem("PaperWings/Settings/Controls  - invert pitch", priority = 0)]
        static void Controls() => Open("Controls.asset");

        [MenuItem("PaperWings/Settings/Camera Feel", priority = 1)]
        static void Camera() => Open("CameraFeel_Chase.asset");

        [MenuItem("PaperWings/Settings/Landing (Mug)", priority = 2)]
        static void Landing() => Open("Landing_Mug.asset");

        // Folds: each has a flight feel, a paper, and a fold asset (name, description, mesh, colour).
        [MenuItem("PaperWings/Settings/Dart - Flight", priority = 10)]
        static void Flight() => Open("FlightSettings_Dart.asset");
        [MenuItem("PaperWings/Settings/Dart - Paper", priority = 11)]
        static void Paper() => Open("Paper_Dart.asset");
        [MenuItem("PaperWings/Settings/Dart - Fold", priority = 12)]
        static void FoldDart() => Open("Fold_Dart.asset");

        [MenuItem("PaperWings/Settings/Glider - Flight", priority = 13)]
        static void GliderFlight() => Open("FlightSettings_Glider.asset");
        [MenuItem("PaperWings/Settings/Glider - Paper", priority = 14)]
        static void GliderPaper() => Open("Paper_Glider.asset");
        [MenuItem("PaperWings/Settings/Glider - Fold", priority = 15)]
        static void FoldGlider() => Open("Fold_Glider.asset");

        [MenuItem("PaperWings/Settings/Stunt - Flight", priority = 16)]
        static void StuntFlight() => Open("FlightSettings_Stunt.asset");
        [MenuItem("PaperWings/Settings/Stunt - Paper", priority = 17)]
        static void StuntPaper() => Open("Paper_Stunt.asset");
        [MenuItem("PaperWings/Settings/Stunt - Fold", priority = 18)]
        static void FoldStunt() => Open("Fold_Stunt.asset");

        [MenuItem("PaperWings/Settings/Air - Laptop Thermal", priority = 20)]
        static void Thermal() => Open("Thermal_Laptop.asset");

        [MenuItem("PaperWings/Settings/Air - Tea Steam", priority = 21)]
        static void Steam() => Open("Steam_Tea.asset");

        [MenuItem("PaperWings/Settings/Air - Desk Fan", priority = 22)]
        static void Fan() => Open("Fan_Desk.asset");

        [MenuItem("PaperWings/Settings/Air - Ceiling Vent", priority = 23)]
        static void Vent() => Open("Vent_Ceiling.asset");

        static void Open(string file)
        {
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(Dir + file);
            if (asset == null)
            {
                EditorUtility.DisplayDialog("PaperWings", $"{Dir}{file} not found. Run the stage builder that creates it.", "OK");
                return;
            }
            Selection.activeObject = asset;                       // shows it in the Inspector
            EditorGUIUtility.PingObject(asset);                   // flashes it in the Project window
            EditorApplication.ExecuteMenuItem("Window/General/Inspector");   // make sure the Inspector is open and focused
        }
    }
}
