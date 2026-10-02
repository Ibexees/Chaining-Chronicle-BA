using CC.Core.Grid;
using CC.Game.Boot;
using CC.Game.Input;
using UnityEngine;

namespace CC.Game.Stage
{
    public readonly struct PlayerIntent
    {
        public bool IsInteract { get; }
        public Direction Direction { get; }

        private PlayerIntent(bool isInteract, Direction direction)
        {
            IsInteract = isInteract;
            Direction = direction;
        }

        public static PlayerIntent Move(Direction direction) => new PlayerIntent(false, direction);
        public static PlayerIntent Interact() => new PlayerIntent(true, Direction.Down);
    }

    /// <summary>
    /// Turns input events into player intents. Remembers one pending key press so a short tap during
    /// an animation is not lost, and repeats the held direction for hold-to-walk.
    /// </summary>
    public sealed class PlayerController : MonoBehaviour
    {
        private InputRouter _input;
        private PlayerIntent? _buffered;

        private void OnEnable()
        {
            _input = Services.Get<InputRouter>();
            _input.MoveRequested += OnMove;
            _input.InteractPressed += OnInteract;
        }

        private void OnDisable()
        {
            if (_input == null) return;
            _input.MoveRequested -= OnMove;
            _input.InteractPressed -= OnInteract;
        }

        /// <summary>Returns the next action the player wants to take, if any. Consumes it.</summary>
        public bool TryGetIntent(out PlayerIntent intent)
        {
            if (_buffered.HasValue)
            {
                intent = _buffered.Value;
                _buffered = null;
                return true;
            }

            var held = _input.HeldMove;
            if (held != Vector2Int.zero)
            {
                intent = PlayerIntent.Move(ToDirection(held));
                return true;
            }

            intent = default;
            return false;
        }

        /// <summary>Drops a pending key press, e.g. when combat starts.</summary>
        public void ClearBuffer() => _buffered = null;

        public static Direction ToDirection(Vector2Int v)
        {
            if (v.x > 0) return Direction.Right;
            if (v.x < 0) return Direction.Left;
            return v.y > 0 ? Direction.Up : Direction.Down;
        }

        private void OnMove(Vector2Int v) => _buffered = PlayerIntent.Move(ToDirection(v));

        private void OnInteract() => _buffered = PlayerIntent.Interact();
    }
}
