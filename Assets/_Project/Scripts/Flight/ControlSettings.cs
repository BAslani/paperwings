using UnityEngine;

namespace PaperWings.Flight
{
    public enum ControlScheme
    {
        /// <summary>Stick / WASD drive pitch and bank directly.</summary>
        Classic,
        /// <summary>The mouse moves an aim point; the plane steers itself toward it (War Thunder / Ace Combat PC style).</summary>
        MouseAim
    }

    /// <summary>
    /// Player control preferences that apply to every plane/fold (unlike FlightSettings,
    /// which describes one plane's handling). Open via PaperWings > Settings > Controls.
    /// </summary>
    [CreateAssetMenu(fileName = "Controls", menuName = "PaperWings/Control Settings")]
    public class ControlSettings : ScriptableObject
    {
        [Tooltip("Classic = stick/WASD steer directly. Mouse Aim = mouse sets where to go, the plane follows. Press C in play to switch.")]
        public ControlScheme scheme = ControlScheme.Classic;

        [Tooltip("OFF = flight-style (W / stick forward dives). ON = W / stick forward climbs.")]
        public bool invertPitch = false;

        [Header("Mouse Aim")]
        [Tooltip("Degrees the aim point moves per pixel of mouse movement.")]
        [Range(0.01f, 0.5f)] public float mouseSensitivity = 0.08f;

        [Tooltip("Flip mouse up/down.")]
        public bool invertMouseY = false;

        [Tooltip("How hard the plane chases the aim point. Higher = snappier, lower = smoother and floatier.")]
        [Range(0.2f, 3f)] public float aimResponsiveness = 1f;

        [Tooltip("Holding W/S or A/D takes manual control of that axis; release to go back to following the mouse.")]
        public bool keyboardOverride = true;

        [Tooltip("Hide and lock the cursor to the game view while flying with the mouse (Esc releases it in the Editor).")]
        public bool lockCursor = true;

        [Tooltip("How far ahead the camera looks along the aim direction (units).")]
        public float cameraLookDistance = 40f;
    }
}
