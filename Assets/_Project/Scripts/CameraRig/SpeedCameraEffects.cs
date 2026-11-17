using UnityEngine;
using Unity.Cinemachine;
using PaperWings.Flight;

namespace PaperWings.CameraRig
{
    /// <summary>
    /// Drives the Cinemachine chase camera from the plane's state:
    ///   - FOV widens with speed (plus a punch on boost, a zoom-in on landing)
    ///   - camera leans (Dutch) into banked turns
    ///   - noise shake for top-speed rumble, stalls, boosts and crashes
    ///   - speed-line particles fade in at high speed
    /// Also pushes CameraFeelSettings into the Cinemachine components every frame for live tuning.
    ///
    /// UNITY/CINEMACHINE NOTE: The real Camera has a CinemachineBrain. The brain copies the
    /// position/rotation/lens of the active CinemachineCamera ("virtual camera") every frame.
    /// The virtual camera's behaviour comes from components on the same GameObject:
    /// CinemachineFollow (where to sit), CinemachineRotationComposer (where to aim),
    /// CinemachineBasicMultiChannelPerlin (procedural shake).
    /// </summary>
    [RequireComponent(typeof(CinemachineCamera))]
    public class SpeedCameraEffects : MonoBehaviour
    {
        [SerializeField] PlaneController plane;
        [SerializeField] CameraFeelSettings settings;
        [Tooltip("Optional particle system for speed lines (usually a child of the Main Camera).")]
        [SerializeField] ParticleSystem speedLines;

        CinemachineCamera vcam;
        CinemachineFollow follow;
        CinemachineRotationComposer composer;
        CinemachineBasicMultiChannelPerlin noise;

        float fov, dutch, kickShake, boostFov;

        void Awake()
        {
            vcam = GetComponent<CinemachineCamera>();
            follow = GetComponent<CinemachineFollow>();
            composer = GetComponent<CinemachineRotationComposer>();
            noise = GetComponent<CinemachineBasicMultiChannelPerlin>();
            if (settings != null) fov = settings.baseFov;
        }

        void OnEnable()
        {
            if (plane == null) return;
            plane.Boosted += OnBoost;
            plane.Downed += OnDowned;
        }

        void OnDisable()
        {
            if (plane == null) return;
            plane.Boosted -= OnBoost;
            plane.Downed -= OnDowned;
        }

        void OnBoost()
        {
            boostFov = settings.boostFovKick;
            kickShake = Mathf.Max(kickShake, settings.boostShake);
        }

        void OnDowned(bool crash)
        {
            if (crash) kickShake = Mathf.Max(kickShake, settings.crashShake);
        }

        // Update runs before the brain's LateUpdate, so our lens changes apply this same frame.
        void Update()
        {
            if (plane == null || settings == null) return;
            float dt = Time.deltaTime;
            ApplyFramingSettings();

            bool flying = plane.State == PlaneState.Flying;
            float speed = flying ? plane.Speed : 0f;
            float speed01 = Mathf.InverseLerp(settings.fovSpeedMin, settings.fovSpeedMax, speed);

            // ---- FOV ----
            boostFov = Mathf.MoveTowards(boostFov, 0f, settings.boostFovKick * 1.5f * dt);
            float targetFov = Mathf.Lerp(settings.baseFov, settings.maxFov, speed01) + boostFov;
            if (plane.State == PlaneState.Landed) targetFov = settings.baseFov - settings.landedZoom;
            fov = Mathf.Lerp(fov, targetFov, 1f - Mathf.Exp(-settings.fovResponse * dt));
            vcam.Lens.FieldOfView = fov;

            // ---- Lean into turns ----
            // Plane bank is "right wing down = positive"; camera Dutch uses the opposite sign.
            float targetDutch = flying ? -plane.BankAngle * settings.leanAmount : 0f;
            dutch = Mathf.Lerp(dutch, targetDutch, 1f - Mathf.Exp(-settings.leanResponse * dt));
            vcam.Lens.Dutch = dutch;

            // ---- Shake ----
            kickShake = Mathf.MoveTowards(kickShake, 0f, settings.shakeDecay * dt);
            if (noise != null)
            {
                float rumble = settings.rumbleAtMaxSpeed * speed01 * speed01;
                float stall = flying ? settings.stallShake * plane.StallAmount : 0f;
                noise.AmplitudeGain = rumble + stall + kickShake;
                noise.FrequencyGain = 1f + speed01;
            }

            // ---- Speed lines ----
            if (speedLines != null)
            {
                float lines01 = Mathf.InverseLerp(settings.speedLinesStartSpeed, plane.Settings.maxSpeed, speed);
                var emission = speedLines.emission;
                emission.rateOverTime = settings.speedLinesMaxRate * lines01 * lines01;
                var main = speedLines.main;
                main.startSpeed = Mathf.Max(20f, speed * 1.5f);
            }
        }

        void ApplyFramingSettings()
        {
            if (follow != null)
            {
                follow.FollowOffset = settings.followOffset;
                follow.TrackerSettings.PositionDamping = settings.positionDamping;
                follow.TrackerSettings.RotationDamping = new Vector3(0f, settings.yawDamping, 0f);
            }
            if (composer != null)
            {
                composer.Damping = settings.aimDamping;
                composer.Lookahead.Enabled = settings.lookaheadTime > 0f;
                composer.Lookahead.Time = settings.lookaheadTime;
            }
        }
    }
}
