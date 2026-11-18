using UnityEngine;
using UnityEngine.InputSystem;

namespace PaperWings.Flight
{
    /// <summary>
    /// Reads the "Flight" action map and exposes plain values to the other scripts.
    /// Keeping input in one place means the flight controller never touches devices directly,
    /// so AI or replays could drive it later. Control preferences (invert pitch) are applied here.
    ///
    /// UNITY NOTE: The new Input System uses an .inputactions asset where named actions
    /// (Steer, Boost...) are bound to keys/buttons. Double-click
    /// Assets/_Project/Input/PaperWings.inputactions to edit bindings visually.
    /// </summary>
    public class PlaneInput : MonoBehaviour
    {
        [SerializeField] InputActionAsset actions;
        [Tooltip("Global control preferences (invert pitch). PaperWings > Settings > Controls.")]
        [SerializeField] ControlSettings controls;

        InputActionMap map;
        InputAction steer, boost, throwAction, reset, foldPrev, foldNext;
        bool boostQueued;

        /// <summary>Raw stick: x = right (+), y = forward (+). Prefer PitchInput / RollInput.</summary>
        public Vector2 Steer { get; private set; }

        public bool InvertPitch => controls != null && controls.invertPitch;

        /// <summary>Pitch command with the invert preference applied: + = nose UP.</summary>
        public float PitchInput => InvertPitch ? Steer.y : -Steer.y;

        /// <summary>Roll command: + = bank right.</summary>
        public float RollInput => Steer.x;

        /// <summary>True while the throw button is held (for charging the launch).</summary>
        public bool ThrowHeld => throwAction != null && throwAction.IsPressed();

        /// <summary>True on the frame the throw button was pressed.</summary>
        public bool ThrowPressed => throwAction != null && throwAction.WasPressedThisFrame();

        /// <summary>True on the frame the reset button was pressed.</summary>
        public bool ResetPressed => reset != null && reset.WasPressedThisFrame();

        /// <summary>True on the frame "previous fold" / "next fold" was pressed.</summary>
        public bool FoldPrevPressed => foldPrev != null && foldPrev.WasPressedThisFrame();
        public bool FoldNextPressed => foldNext != null && foldNext.WasPressedThisFrame();

        void Awake()
        {
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
        }

        void OnEnable() => map?.Enable();
        void OnDisable() => map?.Disable();

        void Update()
        {
            Steer = Vector2.ClampMagnitude(steer.ReadValue<Vector2>(), 1f);

            // Button presses happen in Update (per frame) but the flight model runs in
            // FixedUpdate (per physics tick). Queue the press so it's never missed.
            if (boost.WasPressedThisFrame()) boostQueued = true;
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
