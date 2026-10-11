using System.Collections;
using CC.Core.Grid;
using UnityEngine;

namespace CC.Game.Stage
{
    /// <summary>A sprite with an animator that stands on a grid cell. Pure presentation.</summary>
    public class EntityView : MonoBehaviour
    {
        public SpriteRenderer Renderer { get; private set; }
        public Animator Animator { get; private set; }

        public static T Create<T>(string objectName, Transform parent, RuntimeAnimatorController controller, int sortingOrder)
            where T : EntityView
        {
            var go = new GameObject(objectName, typeof(SpriteRenderer), typeof(Animator), typeof(T));
            go.transform.SetParent(parent, false);

            var view = go.GetComponent<T>();
            view.Renderer = go.GetComponent<SpriteRenderer>();
            view.Renderer.sortingOrder = sortingOrder;
            view.Animator = go.GetComponent<Animator>();
            view.Animator.runtimeAnimatorController = controller;
            return view;
        }

        public void SetCell(GridPos pos) => transform.position = StageView.CellToWorld(pos);

        public Vector3 WorldPosition => transform.position;
    }

    public sealed class PickupView : EntityView { }

    public sealed class EnemyView : EntityView
    {
        public int EnemyId { get; set; }
        public string TypeId { get; set; }
    }

    /// <summary>
    /// The player sprite. Every facing has an idle pose and a walk cycle, so the facing that the Core
    /// stores (it decides which pickup is collected) is always visible. Walking is one continuous loop:
    /// it is not restarted per step and only changes to idle when the player really stops.
    /// </summary>
    public sealed class PlayerView : EntityView
    {
        private static readonly int WalkSpeedId = Animator.StringToHash("walkSpeed");
        private int _currentState;

        public Direction Facing { get; private set; } = Direction.Down;
        public bool IsWalking { get; private set; }

        public static string IdleStateName(Direction direction) => "idle" + Suffix(direction);
        public static string WalkStateName(Direction direction) => "walk" + Suffix(direction);

        private static string Suffix(Direction direction)
        {
            switch (direction)
            {
                case Direction.Up: return "Up";
                case Direction.Left: return "Left";
                case Direction.Right: return "Right";
                default: return "Down";
            }
        }

        /// <summary>
        /// Sets the walk speed so one walk cycle spans the given number of steps. The walk clips are exactly
        /// one second long, so the speed is the number of cycles per second.
        /// </summary>
        public void SetWalkCycle(float stepSeconds, float stepsPerCycle)
        {
            float cycleSeconds = stepSeconds * stepsPerCycle;
            Animator.SetFloat(WalkSpeedId, cycleSeconds > 0.0001f ? 1f / cycleSeconds : 1f);
        }

        /// <summary>Stands still and looks in the given direction.</summary>
        public void Face(Direction direction)
        {
            Facing = direction;
            IsWalking = false;
            Enter(IdleStateName(direction), 0f);
        }

        /// <summary>
        /// Walks in the given direction. Keeps the running cycle: the same direction does nothing, a
        /// different direction continues at the same phase so the feet do not jump.
        /// </summary>
        public void Walk(Direction direction)
        {
            float phase = IsWalking ? Mathf.Repeat(Animator.GetCurrentAnimatorStateInfo(0).normalizedTime, 1f) : 0f;
            Facing = direction;
            IsWalking = true;
            Enter(WalkStateName(direction), phase);
        }

        /// <summary>Ends walking and shows the idle pose of the current facing. Does nothing when standing.</summary>
        public void StopWalking()
        {
            if (IsWalking) Face(Facing);
        }

        private void Enter(string stateName, float normalizedTime)
        {
            int hash = Animator.StringToHash(stateName);
            if (hash == _currentState) return;

            _currentState = hash;
            Animator.Play(hash, 0, normalizedTime);
        }
    }

    public static class Motion
    {
        /// <summary>Moves a set of transforms linearly to their targets within the given time.</summary>
        public static IEnumerator Slide(Transform[] transforms, Vector3[] from, Vector3[] to, float seconds)
        {
            if (seconds <= 0f || transforms.Length == 0)
            {
                for (int i = 0; i < transforms.Length; i++) if (transforms[i] != null) transforms[i].position = to[i];
                yield break;
            }

            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float f = t / seconds;
                for (int i = 0; i < transforms.Length; i++)
                    if (transforms[i] != null) transforms[i].position = Vector3.Lerp(from[i], to[i], f);
                yield return null;
            }
            for (int i = 0; i < transforms.Length; i++) if (transforms[i] != null) transforms[i].position = to[i];
        }
    }
}
