using UnityEngine;
using UnityEngine.InputSystem;

namespace PaperWings.Flight
{
    /// <summary>
    /// Reads the "Flight" action map and exposes plain values to the other scripts.
    /// Keeping input in one place means the flight controller never touches devices directly,
    /// so AI or replays could drive it later.
    ///
    /// UNITY NOTE: The new Input System uses an .inputactions asset where named actions
    /// (Steer, Boost...) are bound to keys/buttons. Double-click
    /// Assets/_Project/Input/PaperWings.inputactions to edit bindings visually.
    /// </summary>
    public class PlaneInput : MonoBehaviour
    {
        [SerializeField] InputActionAsset actions;

        InputActionMap map;
        InputAction steer, boost, throwAction, reset;
        bool boostQueued;

        /// <summary>x = roll (right +), y = stick forward (+) / back (-).</summary>
        public Vector2 Steer { get; private set; }

        /// <summary>True while the throw button is held (for charging the launch).</summary>
        public bool ThrowHeld => throwAction != null && throwAction.IsPressed();

        /// <summary>True on the frame the reset button was pressed.</summary>
        public bool ResetPressed => reset != null && reset.WasPressedThisFrame();

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
