using UnityEngine;

namespace PaperWings.CameraRig
{
    /// <summary>
    /// Camera "feel" knobs: framing, lag, speed FOV, lean and shake.
    /// Applied every frame by SpeedCameraEffects, so edits in Play mode show instantly (and are kept).
    /// </summary>
    [CreateAssetMenu(fileName = "CameraFeelSettings", menuName = "PaperWings/Camera Feel Settings")]
    public class CameraFeelSettings : ScriptableObject
    {
        [Header("Framing & Lag")]
        [Tooltip("Camera position relative to the plane's heading (x right, y up, z forward). Negative z = behind.")]
        public Vector3 followOffset = new Vector3(0f, 2.4f, -7.5f);

        [Tooltip("How lazily the camera follows position (seconds-ish). Higher Z = camera falls back more when you speed up.")]
        public Vector3 positionDamping = new Vector3(0.3f, 0.4f, 0.6f);

        [Tooltip("How lazily the camera swings around behind you in turns. Higher = more 'swing out' in turns.")]
        [Range(0f, 3f)] public float yawDamping = 0.7f;

        [Tooltip("How lazily the camera re-aims at the plane.")]
        public Vector2 aimDamping = new Vector2(0.35f, 0.35f);

        [Tooltip("Aim ahead of where the plane is going (seconds). Makes the camera lead turns and dives.")]
        [Range(0f, 1f)] public float lookaheadTime = 0.25f;

        [Header("Speed FOV")]
        public float baseFov = 60f;
        public float maxFov = 84f;
        [Tooltip("FOV starts widening above this speed...")]
        public float fovSpeedMin = 14f;
        [Tooltip("...and is fully wide at this speed.")]
        public float fovSpeedMax = 42f;
        [Tooltip("How quickly FOV follows speed (per second).")]
        [Range(0.5f, 10f)] public float fovResponse = 3f;
        [Tooltip("Extra FOV punch when you boost (degrees). Fades out quickly.")]
        public float boostFovKick = 10f;
        [Tooltip("Zoom in this many degrees after a successful landing (a little 'hero shot').")]
        public float landedZoom = 12f;

        [Header("Lean")]
        [Tooltip("Camera roll as a fraction of the plane's bank. 0.25 = lean 14 degrees at a 55 degree bank. Negative flips it.")]
        [Range(-1f, 1f)] public float leanAmount = 0.25f;
        [Range(0.5f, 10f)] public float leanResponse = 3f;

        [Header("Shake  (Cinemachine noise amplitude)")]
        [Tooltip("Gentle rumble at top speed.")]
        public float rumbleAtMaxSpeed = 0.6f;
        [Tooltip("Shake while stalling (doubles the stall warning).")]
        public float stallShake = 0.8f;
        public float boostShake = 0.8f;
        public float crashShake = 3f;
        [Tooltip("How fast boost/crash shakes fade (per second).")]
        public float shakeDecay = 3f;

        [Header("Speed Lines")]
        [Tooltip("Speed lines start appearing above this speed.")]
        public float speedLinesStartSpeed = 22f;
        [Tooltip("Speed lines per second at max speed.")]
        public float speedLinesMaxRate = 90f;
    }
}
