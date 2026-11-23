using UnityEngine;
using PaperWings.Flight;

namespace PaperWings.Gameplay
{
    /// <summary>
    /// Hand-launch and reset loop:
    ///   Held:  steer to aim, hold Throw to wind up (the plane pulls back), release to throw.
    ///   Flying: press Reset any time to go back to the hand.
    ///   Down:  automatically back in hand after a short delay.
    ///   Landed: celebrate, then back in hand (press Throw to skip the wait).
    /// </summary>
    public class LaunchAndReset : MonoBehaviour
    {
        [SerializeField] PlaneController plane;
        [SerializeField] PlaneInput input;
        [Tooltip("Where the plane is held before throwing. Its forward (blue arrow) is the throw direction.")]
        [SerializeField] Transform spawnPoint;

        [Header("Aiming while held")]
        [SerializeField] float aimYawRate = 60f;
        [SerializeField] float aimMaxYaw = 45f;
        [SerializeField] float aimMaxPitch = 20f;

        [Header("Wind-up feel")]
        [Tooltip("How far the plane pulls back at full charge.")]
        [SerializeField] float windUpPullback = 0.8f;
        [Tooltip("Nose-up tilt at full charge (deg).")]
        [SerializeField] float windUpTilt = 10f;

        [Header("Auto reset")]
        [SerializeField] float resetDelayAfterCrash = 1.2f;
        [SerializeField] float resetDelayAfterTouchdown = 2.0f;
        [Tooltip("After a successful landing, auto-reset after this long (or press Throw to go again sooner).")]
        [SerializeField] float resetDelayAfterLanding = 6.0f;

        // Set by menus. Stays blocked for one extra frame after unblocking, so the key that
        // closed the menu can't also start a throw.
        static bool inputBlocked;
        static int unblockFrame = -1;
        public static bool InputBlocked
        {
            get => inputBlocked || Time.frameCount <= unblockFrame;
            set { if (inputBlocked && !value) unblockFrame = Time.frameCount + 1; inputBlocked = value; }
        }

        public float Charge01 { get; private set; }
        public bool IsCharging { get; private set; }

        float aimYaw, aimPitch;
        float downTimer;

        void Start()
        {
            if (spawnPoint == null)
            {
                // Fall back to wherever the plane starts in the scene.
                spawnPoint = new GameObject("SpawnPoint (auto)").transform;
                spawnPoint.SetPositionAndRotation(plane.transform.position, plane.transform.rotation);
            }
            ResetToStart();
        }

        /// <summary>Moves the hand to another throw point (rooms). Takes effect on the next reset.</summary>
        public void SetSpawnPoint(Transform point) { if (point != null) spawnPoint = point; }

        public void ResetToStart()
        {
            aimYaw = aimPitch = 0f;
            Charge01 = 0f;
            IsCharging = false;
            downTimer = 0f;
            ClearSpawn();
            plane.Hold(spawnPoint.position, spawnPoint.rotation);
            var trails = plane.GetComponent<PlaneTrails>();
            if (trails) trails.ClearTrails();
        }

        void Update()
        {
            if (InputBlocked) return;   // a menu is open

            if (input != null && input.ResetPressed)
            {
                ResetToStart();
                return;
            }

            switch (plane.State)
            {
                case PlaneState.Held:   UpdateHeld();   break;
                case PlaneState.Down:   UpdateDown();   break;
                case PlaneState.Landed: UpdateLanded(); break;
            }
        }

        /// <summary>
        /// Lifts the hand clear if the throw point sits inside or right against furniture (a keyboard,
        /// a monitor, the water tower's roof), so the plane can't clip it the moment it's thrown.
        /// </summary>
        void ClearSpawn()
        {
            var box = plane.GetComponent<BoxCollider>();
            Vector3 half = box != null ? Vector3.Scale(box.size, plane.transform.lossyScale) * 0.5f : new Vector3(0.7f, 0.15f, 1f);
            half += new Vector3(0.6f, 0.35f, 0.3f);   // a little room for the wobble and the wind-up tilt
            Quaternion rot = spawnPoint.rotation;
            Vector3 start = spawnPoint.position;
            for (int i = 0; i <= 10; i++)
            {
                Vector3 p = start + Vector3.up * (i * 0.6f);
                if (!Blocked(p, half, rot) && !SweepBlocked(p, half, rot))
                {
                    if (i > 0) spawnPoint.position = p;
                    return;
                }
            }
        }

        bool Blocked(Vector3 p, Vector3 half, Quaternion rot)
        {
            foreach (var c in Physics.OverlapBox(p, half, rot, ~0, QueryTriggerInteraction.Ignore))
                if (!IsPlane(c)) return true;
            return false;
        }

        // The first couple of metres of the throw, nose slightly down (a weak throw sinks).
        bool SweepBlocked(Vector3 p, Vector3 half, Quaternion rot)
        {
            Vector3 dir = Quaternion.AngleAxis(6f, rot * Vector3.right) * (rot * Vector3.forward);
            foreach (var hit in Physics.BoxCastAll(p, half, dir, rot, 4f, ~0, QueryTriggerInteraction.Ignore))
                if (!IsPlane(hit.collider)) return true;
            return false;
        }

        bool IsPlane(Collider c) => c.attachedRigidbody != null && c.attachedRigidbody.gameObject == plane.gameObject;

        void UpdateLanded()
        {
            // Let the success moment breathe, then go again (Throw skips the wait).
            downTimer += Time.deltaTime;
            bool skip = input != null && input.ThrowPressed && downTimer > 0.5f;
            if (skip || downTimer >= resetDelayAfterLanding) ResetToStart();
        }

        void UpdateHeld()
        {
            FlightSettings s = plane.Settings;
            // Aim: left/right turns, up/down tilts (same pitch convention as flight).
            float yawDelta, pitchDelta;
            if (input != null && input.IsMouseAim)
            {
                // Mouse aim: the mouse moves the throw direction directly; WASD still nudges it.
                float manualPitch = input.InvertPitch ? input.Steer.y : -input.Steer.y;
                yawDelta = input.AimDelta.x + input.Steer.x * aimYawRate * Time.deltaTime;
                pitchDelta = input.AimDelta.y + manualPitch * aimYawRate * Time.deltaTime;
            }
            else
            {
                float pitchIn = input != null ? input.PitchInput : 0f;
                float rollIn = input != null ? input.RollInput : 0f;
                yawDelta = rollIn * aimYawRate * Time.deltaTime;
                pitchDelta = pitchIn * aimYawRate * Time.deltaTime;
            }
            aimYaw = Mathf.Clamp(aimYaw + yawDelta, -aimMaxYaw, aimMaxYaw);
            aimPitch = Mathf.Clamp(aimPitch + pitchDelta, -aimMaxPitch, aimMaxPitch);

            bool held = input != null && input.ThrowHeld;
            // Only a FRESH press starts a wind-up (a key still held from a menu shouldn't throw).
            if (held && (IsCharging || input.ThrowPressed))
            {
                IsCharging = true;
                Charge01 = Mathf.MoveTowards(Charge01, 1f, Time.deltaTime / Mathf.Max(0.01f, s.throwChargeTime));
            }
            else if (IsCharging)
            {
                Throw();
                return;
            }

            // Ease-out curve makes the wind-up feel like a pulled-back arm.
            float pull = 1f - (1f - Charge01) * (1f - Charge01);
            Quaternion aim = spawnPoint.rotation * Quaternion.Euler(-aimPitch - windUpTilt * pull, aimYaw, 0f);
            // Tiny idle bob so the plane feels alive in hand.
            Vector3 bob = Vector3.up * Mathf.Sin(Time.time * 2f) * 0.03f;
            Vector3 pos = spawnPoint.position - aim * Vector3.forward * (windUpPullback * pull) + bob;
            plane.Hold(pos, aim);
        }

        void Throw()
        {
            FlightSettings s = plane.Settings;
            float throwSpeed = Mathf.Lerp(s.throwSpeedMin, s.throwSpeedMax, Charge01);
            // Undo the wind-up tilt so the throw goes where you aimed, then add the kick.
            plane.transform.rotation = spawnPoint.rotation * Quaternion.Euler(-aimPitch, aimYaw, 0f);
            plane.Launch(throwSpeed, s.throwPitchKick);

            var wobble = plane.GetComponent<PaperWobble>();
            if (wobble) wobble.Kick(4f + 4f * Charge01);

            IsCharging = false;
            Charge01 = 0f;
        }

        void UpdateDown()
        {
            downTimer += Time.deltaTime;
            float delay = plane.LastDownWasCrash ? resetDelayAfterCrash : resetDelayAfterTouchdown;
            if (downTimer >= delay) ResetToStart();
        }
    }
}
