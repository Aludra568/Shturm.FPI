using UnityEngine;
using UnityEngine.InputSystem;

namespace RicochetBerlin
{
    /// <summary>
    /// All player input in one place. Actions are declared in code so the bindings are easy to read:
    /// keyboard + mouse first, gamepad as a bonus.
    /// </summary>
    public class InputReader : MonoBehaviour
    {
        [Tooltip("Degrees the aim turns per pixel of horizontal mouse movement.")]
        [SerializeField] float mouseSensitivity = 0.08f;
        [Tooltip("Degrees per second at full right-stick deflection.")]
        [SerializeField] float stickTurnSpeed = 110f;

        InputAction move, aimStick, fire, focus, ability, turnAround, pause, restart;

#if UNITY_EDITOR
        /// <summary>Automated smoke tests drive the tank through these (never set in normal play).</summary>
        public Vector2? TestMove;
        public bool? TestFire;
        public bool? TestFocus;
        /// <summary>World point the aim should swing to (test only).</summary>
        public Vector2? TestAimPoint;
        public Vector2 Move => TestMove ?? move.ReadValue<Vector2>();
        public bool FireHeld => TestFire ?? fire.IsPressed();
        public bool FocusHeld => TestFocus ?? focus.IsPressed();
#else
        public Vector2 Move => move.ReadValue<Vector2>();
        public bool FireHeld => fire.IsPressed();
        public bool FocusHeld => focus.IsPressed();
#endif
        /// <summary>The tank class special: nitro / smoke screen / active armour.</summary>
        public bool AbilityPressed => ability.WasPressedThisFrame();
        public bool TurnAroundPressed => turnAround.WasPressedThisFrame();
        public bool PausePressed => pause.WasPressedThisFrame();
        public bool RestartPressed => restart.WasPressedThisFrame();

        /// <summary>
        /// How far to turn the aim this frame, in degrees (positive = clockwise / to the right).
        /// The camera rotates with the aim, so aiming is relative - mouse left/right, like WoT.
        /// </summary>
        public float AimTurn
        {
            get
            {
                float turn = aimStick.ReadValue<Vector2>().x * stickTurnSpeed * Time.unscaledDeltaTime;
                if (Mouse.current != null) turn += Mouse.current.delta.ReadValue().x * mouseSensitivity;
                return turn;
            }
        }

        void Awake()
        {
            move = new InputAction("Move");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            move.AddBinding("<Gamepad>/leftStick");

            aimStick = new InputAction("AimStick", binding: "<Gamepad>/rightStick");

            fire = new InputAction("Fire", binding: "<Mouse>/leftButton");
            fire.AddBinding("<Gamepad>/rightTrigger");

            // hold to narrow the flashlight into a long beam
            focus = new InputAction("Focus", binding: "<Mouse>/rightButton");
            focus.AddBinding("<Gamepad>/leftShoulder");

            ability = new InputAction("Ability", binding: "<Keyboard>/leftShift");
            ability.AddBinding("<Keyboard>/space");
            ability.AddBinding("<Gamepad>/leftTrigger");

            turnAround = new InputAction("TurnAround", binding: "<Keyboard>/q");
            turnAround.AddBinding("<Gamepad>/rightShoulder");

            pause = new InputAction("Pause", binding: "<Keyboard>/escape");
            pause.AddBinding("<Gamepad>/start");

            restart = new InputAction("Restart", binding: "<Keyboard>/r");
            restart.AddBinding("<Gamepad>/select");
        }

        void OnEnable()
        {
            move.Enable(); aimStick.Enable(); fire.Enable(); focus.Enable(); ability.Enable(); turnAround.Enable(); pause.Enable(); restart.Enable();
        }

        void OnDisable()
        {
            move.Disable(); aimStick.Disable(); fire.Disable(); focus.Disable(); ability.Disable(); turnAround.Disable(); pause.Disable(); restart.Disable();
        }

        void OnDestroy()
        {
            move.Dispose(); aimStick.Dispose(); fire.Dispose(); focus.Dispose(); ability.Dispose(); turnAround.Dispose(); pause.Dispose(); restart.Dispose();
        }
    }
}
