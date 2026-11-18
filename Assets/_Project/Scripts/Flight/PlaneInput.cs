using UnityEngine;
using UnityEngine.InputSystem;

namespace PaperWings.Flight
{
    /// <summary>
    /// Reads the "Flight" action map and exposes plain values to the other scripts.
    /// Keeping input in one place means the flight controller never touches devices directly,
    /// so AI or replays could drive it later. Control preferences are applied here.
    ///
    /// Two control schemes (ControlSettings.scheme, press C to switch in play):
    ///  - Classic:  stick / WASD drive pitch and bank directly.
    ///  - MouseAim: the mouse moves an AIM DIRECTION in the world. An "instructor" turns the
    ///              difference between where the nose points and where you aim into stick input,
    ///              so the plane flies itself toward the mouse. WASD overrides an axis while held.
    ///    This works neatly with our flight model because pitch is measured against the horizon
    ///    and heading changes come from bank: we just compare angles.
    ///
    /// UNITY NOTE: The new Input System uses an .inputactions asset where named actions
    /// (Steer, Boost...) are bound to keys/buttons. Double-click
    /// Assets/_Project/Input/PaperWings.inputactions to edit bindings visually.
    /// </summary>
    public class PlaneInput : MonoBehaviour, IFlightInput
    {
        [SerializeField] InputActionAsset actions;
        [Tooltip("Global control preferences (scheme, invert, mouse). PaperWings > Settings > Controls.")]
        [SerializeField] ControlSettings controls;

        InputActionMap map;
        InputAction steer, boost, throwAction, reset, foldPrev, foldNext, fire, missile, look, toggleControls;
        bool boostQueued;
        PlaneController plane;

        // Mouse aim direction, as angles: heading (yaw) and climb (pitch, + = up).
        float aimYaw, aimPitch;
        bool wasFlying;

        /// <summary>Raw stick: x = right (+), y = forward (+). Prefer PitchInput / RollInput.</summary>
        public Vector2 Steer { get; private set; }

        public ControlSettings Controls => controls;
        public bool InvertPitch => controls != null && controls.invertPitch;
        public bool IsMouseAim => controls != null && controls.scheme == ControlScheme.MouseAim;

        /// <summary>World direction the mouse is aiming (mouse-aim scheme).</summary>
        public Vector3 AimDirection => Quaternion.Euler(-aimPitch, aimYaw, 0f) * Vector3.forward;
        public float AimYaw => aimYaw;
        /// <summary>Mouse movement this frame in degrees (x = right, y = up). Used to aim the throw.</summary>
        public Vector2 AimDelta { get; private set; }

        /// <summary>Pitch command, + = nose UP (invert / mouse aim already applied).</summary>
        public float PitchInput
        {
            get
            {
                float manual = InvertPitch ? Steer.y : -Steer.y;
                if (!IsMouseAim || plane == null || (KeyboardOverride && Mathf.Abs(Steer.y) > 0.1f)) return manual;
                // Instructor: pitch toward the aim's climb angle.
                float error = aimPitch - plane.PitchAngle;
                return Mathf.Clamp(error / 15f * controls.aimResponsiveness, -1f, 1f);
            }
        }

        /// <summary>Roll command, + = bank right (mouse aim already applied).</summary>
        public float RollInput
        {
            get
            {
                if (!IsMouseAim || plane == null || (KeyboardOverride && Mathf.Abs(Steer.x) > 0.1f)) return Steer.x;
                // Instructor: bank toward the aim heading (bank is what turns the plane).
                float error = Mathf.DeltaAngle(plane.transform.eulerAngles.y, aimYaw);
                return Mathf.Clamp(error / 30f * controls.aimResponsiveness, -1f, 1f);
            }
        }

        bool KeyboardOverride => controls == null || controls.keyboardOverride;

        /// <summary>True while the throw button is held (for charging the launch).</summary>
        public bool ThrowHeld => throwAction != null && throwAction.IsPressed();

        /// <summary>True on the frame the throw button was pressed.</summary>
        public bool ThrowPressed => throwAction != null && throwAction.WasPressedThisFrame();

        /// <summary>True on the frame the reset button was pressed.</summary>
        public bool ResetPressed => reset != null && reset.WasPressedThisFrame();

        /// <summary>True on the frame "previous fold" / "next fold" was pressed.</summary>
        public bool FoldPrevPressed => foldPrev != null && foldPrev.WasPressedThisFrame();
        public bool FoldNextPressed => foldNext != null && foldNext.WasPressedThisFrame();

        /// <summary>True while the fire button is held (weapons auto-fire at their own rate).</summary>
        public bool FireHeld => fire != null && fire.IsPressed();

        /// <summary>True on the frame the missile (pencil) button was pressed.</summary>
        public bool MissilePressed => missile != null && missile.WasPressedThisFrame();

        void Awake()
        {
            plane = GetComponent<PlaneController>();
            if (actions == null)
            {
                Debug.LogError("PlaneInput: no InputActionAsset assigned.", this);
                enabled = false;
                return;
            }
            map = actions.FindActionMap("Flight", throwIfNotFound: true);
            steer = map.FindAction("Steer", true);
            boost = map.FindAction("Boost", true);
            throwAction = map.FindAction("Throw", true);
            reset = map.FindAction("Reset", true);
            // Optional actions: missing ones simply never fire.
            foldPrev = map.FindAction("FoldPrev", false);
            foldNext = map.FindAction("FoldNext", false);
            fire = map.FindAction("Fire", false);
            missile = map.FindAction("Missile", false);
            look = map.FindAction("Look", false);
            toggleControls = map.FindAction("ToggleControls", false);
        }

        void OnEnable() => map?.Enable();

        void OnDisable()
        {
            map?.Disable();
            SetCursorCaptured(false);
        }

        void Update()
        {
            Steer = Vector2.ClampMagnitude(steer.ReadValue<Vector2>(), 1f);

            // Button presses happen in Update (per frame) but the flight model runs in
            // FixedUpdate (per physics tick). Queue the press so it's never missed.
            if (boost.WasPressedThisFrame()) boostQueued = true;

            if (toggleControls != null && toggleControls.WasPressedThisFrame() && controls != null)
                controls.scheme = IsMouseAim ? ControlScheme.Classic : ControlScheme.MouseAim;

            UpdateMouseAim();
            SetCursorCaptured(IsMouseAim && controls.lockCursor && Application.isFocused);
        }

        void UpdateMouseAim()
        {
            AimDelta = Vector2.zero;
            if (!IsMouseAim || look == null) return;

            Vector2 mouse = look.ReadValue<Vector2>() * controls.mouseSensitivity;
            if (controls.invertMouseY) mouse.y = -mouse.y;
            AimDelta = mouse;

            bool flying = plane != null && plane.State == PlaneState.Flying;
            if (!flying || !wasFlying)
            {
                // In hand (or down), and on the first frame of flight: match the aim to the nose,
                // so the moment you throw you're aiming where the plane is pointing.
                // (While in hand, LaunchAndReset uses AimDelta to aim the throw itself.)
                Vector3 e = transform.rotation.eulerAngles;
                aimYaw = e.y;
                aimPitch = -Mathf.DeltaAngle(0f, e.x);
                wasFlying = flying;
                if (!flying) return;
            }

            aimYaw = Mathf.Repeat(aimYaw + mouse.x, 360f);
            aimPitch = Mathf.Clamp(aimPitch + mouse.y, -plane.Settings.maxPitchDown, plane.Settings.maxPitchUp);
        }

        static void SetCursorCaptured(bool captured)
        {
            if (captured && Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else if (!captured && Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        /// <summary>Returns true once per boost press.</summary>
        public bool ConsumeBoost()
        {
            bool b = boostQueued;
            boostQueued = false;
            return b;
        }
    }
}
