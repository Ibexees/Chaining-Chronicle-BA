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

    /// <summary>The player sprite. Walking animations exist for three directions, right mirrors left.</summary>
    public sealed class PlayerView : EntityView
    {
        public const string IdleState = "idle";
        public const string WalkDownState = "walkDown";
        public const string WalkLeftState = "walkLeft";
        public const string WalkUpState = "walkUp";

        public void PlayWalk(Direction direction)
        {
            Renderer.flipX = direction == Direction.Right;
            switch (direction)
            {
                case Direction.Up: Animator.Play(WalkUpState); break;
                case Direction.Down: Animator.Play(WalkDownState); break;
                default: Animator.Play(WalkLeftState); break;
            }
        }

        public void PlayIdle() => Animator.Play(IdleState);
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
