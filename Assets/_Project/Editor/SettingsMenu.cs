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

        [MenuItem("PaperWings/Settings/Flight (Dart)  - controls, invert pitch, speed, turning", priority = 0)]
        static void Flight() => Open("FlightSettings_Dart.asset");

        [MenuItem("PaperWings/Settings/Paper (Dart)  - damage, wetness", priority = 1)]
        static void Paper() => Open("Paper_Dart.asset");

        [MenuItem("PaperWings/Settings/Camera Feel", priority = 1)]
        static void Camera() => Open("CameraFeel_Chase.asset");

        [MenuItem("PaperWings/Settings/Landing (Mug)", priority = 2)]
        static void Landing() => Open("Landing_Mug.asset");

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
