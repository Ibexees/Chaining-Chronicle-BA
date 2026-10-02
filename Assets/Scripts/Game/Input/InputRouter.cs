using System;
using CC.Core.Flow;
using CC.Core.Util;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CC.Game.Input
{
    /// <summary>
    /// Owns all input actions and exposes them as plain C# events.
    /// Only the action map of the current mode is enabled. Actions are defined in code
    /// (keyboard + gamepad) and can be rebound through the Input System rebinding API.
    /// </summary>
    public sealed class InputRouter : MonoBehaviour
    {
        private InputActionMap _exploration;
        private InputActionMap _combat;
        private InputActionMap _ui;
        private InputActionMap _active;
        private InputAction _moveAction;

        public InputMode Mode { get; private set; } = InputMode.None;

        /// <summary>Cardinal direction currently held in Exploration mode, or zero. Used for hold-to-walk.</summary>
        public Vector2Int HeldMove =>
            Mode == InputMode.Exploration && TryToCardinal(_moveAction.ReadValue<Vector2>(), out var direction)
                ? direction
                : Vector2Int.zero;

        /// <summary>Exploration: one grid step in a cardinal direction (x or y is +-1, never both).</summary>
        public event Action<Vector2Int> MoveRequested;
        /// <summary>Exploration: context-sensitive interact key.</summary>
        public event Action InteractPressed;
        /// <summary>Exploration: open the pause menu.</summary>
        public event Action MenuPressed;
        /// <summary>Combat and UI: menu navigation (cardinal direction).</summary>
        public event Action<Vector2Int> NavigateRequested;
        public event Action ConfirmPressed;
        public event Action CancelPressed;

        private void Awake()
        {
            BuildActions();
            GameEvents.FlowStateChanged += OnFlowStateChanged;
        }

        private void OnDestroy()
        {
            GameEvents.FlowStateChanged -= OnFlowStateChanged;
            _exploration?.Dispose();
            _combat?.Dispose();
            _ui?.Dispose();
        }

        public void SetMode(InputMode mode)
        {
            _active?.Disable();
            Mode = mode;
            _active = mode switch
            {
                InputMode.Exploration => _exploration,
                InputMode.Combat => _combat,
                InputMode.Ui => _ui,
                _ => null
            };
            _active?.Enable();
        }

        /// <summary>Maps a game flow state to the input mode that belongs to it.</summary>
        public static InputMode ModeFor(GameFlowState state) => state switch
        {
            GameFlowState.Exploring => InputMode.Exploration,
            GameFlowState.Combat => InputMode.Combat,
            GameFlowState.Hub => InputMode.Ui,
            GameFlowState.GameOver => InputMode.Ui,
            _ => InputMode.None
        };

        private void OnFlowStateChanged(GameFlowState previous, GameFlowState next) => SetMode(ModeFor(next));

        private void BuildActions()
        {
            _exploration = new InputActionMap("Exploration");
            var move = _exploration.AddAction("Move", InputActionType.Value);
            _moveAction = move;
            AddDirectionBindings(move);
            move.performed += ctx => EmitCardinal(ctx.ReadValue<Vector2>(), v => MoveRequested?.Invoke(v));

            var interact = _exploration.AddAction("Interact", InputActionType.Button);
            interact.AddBinding("<Keyboard>/e");
            interact.AddBinding("<Keyboard>/enter");
            interact.AddBinding("<Gamepad>/buttonSouth");
            interact.performed += _ => InteractPressed?.Invoke();

            var menu = _exploration.AddAction("Menu", InputActionType.Button);
            menu.AddBinding("<Keyboard>/escape");
            menu.AddBinding("<Gamepad>/start");
            menu.performed += _ => MenuPressed?.Invoke();

            _combat = BuildMenuMap("Combat");
            _ui = BuildMenuMap("Ui");
        }

        private InputActionMap BuildMenuMap(string name)
        {
            var map = new InputActionMap(name);

            var navigate = map.AddAction("Navigate", InputActionType.Value);
            AddDirectionBindings(navigate);
            navigate.performed += ctx => EmitCardinal(ctx.ReadValue<Vector2>(), v => NavigateRequested?.Invoke(v));

            var confirm = map.AddAction("Confirm", InputActionType.Button);
            confirm.AddBinding("<Keyboard>/enter");
            confirm.AddBinding("<Keyboard>/e");
            confirm.AddBinding("<Keyboard>/space");
            confirm.AddBinding("<Gamepad>/buttonSouth");
            confirm.performed += _ => ConfirmPressed?.Invoke();

            var cancel = map.AddAction("Cancel", InputActionType.Button);
            cancel.AddBinding("<Keyboard>/escape");
            cancel.AddBinding("<Keyboard>/q");
            cancel.AddBinding("<Gamepad>/buttonEast");
            cancel.performed += _ => CancelPressed?.Invoke();

            return map;
        }

        private static void AddDirectionBindings(InputAction action)
        {
            action.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            action.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            action.AddBinding("<Gamepad>/dpad");
            action.AddBinding("<Gamepad>/leftStick");
        }

        /// <summary>Reduces an analog or diagonal input to one cardinal step. The dominant axis wins.</summary>
        public static bool TryToCardinal(Vector2 value, out Vector2Int direction)
        {
            const float deadzone = 0.5f;
            direction = Vector2Int.zero;
            float ax = Mathf.Abs(value.x), ay = Mathf.Abs(value.y);
            if (ax < deadzone && ay < deadzone) return false;

            direction = ax >= ay
                ? new Vector2Int(value.x > 0 ? 1 : -1, 0)
                : new Vector2Int(0, value.y > 0 ? 1 : -1);
            return true;
        }

        private static void EmitCardinal(Vector2 value, Action<Vector2Int> emit)
        {
            if (TryToCardinal(value, out var direction)) emit(direction);
        }
    }
}
