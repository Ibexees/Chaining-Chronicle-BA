using System;
using System.Linq;
using CC.Core.Grid;
using CC.Game.Stage;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CC.Tests.Content
{
    /// <summary>
    /// Guards the visual content that Chaining Chronicle > Build Content produces: one shared sprite pivot,
    /// an idle pose and a walk cycle for every facing, and walk cycles that can be driven by step time.
    /// </summary>
    public class PlayerVisualsTests
    {
        private const string PlayerControllerPath = "Assets/Content/Animators/Player.controller";
        private static readonly Direction[] Directions = (Direction[])Enum.GetValues(typeof(Direction));

        private AnimatorController _controller;

        [SetUp]
        public void SetUp()
        {
            _controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerControllerPath);
            Assert.IsNotNull(_controller, "Player.controller missing, run Chaining Chronicle > Build Content");
        }

        private AnimatorState State(string name)
        {
            var state = _controller.layers[0].stateMachine.states.Select(s => s.state).FirstOrDefault(s => s.name == name);
            Assert.IsNotNull(state, $"State '{name}' missing in Player.controller");
            return state;
        }

        private static ObjectReferenceKeyframe[] Keys(AnimationClip clip) =>
            AnimationUtility.GetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"));

        [Test]
        public void AllExportedSpritesShareTheCentrePivot()
        {
            var offenders = new System.Collections.Generic.List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Export" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var sprite in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>())
                {
                    var pivot = new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height);
                    if (Vector2.Distance(pivot, new Vector2(0.5f, 0.5f)) > 0.001f) offenders.Add($"{sprite.name} {pivot}");
                }
            }
            Assert.IsEmpty(offenders, "Sprites with a pivot that is not the centre: " + string.Join(", ", offenders));
        }

        [Test]
        public void EveryFacingHasAnIdleAndAWalkState()
        {
            foreach (var direction in Directions)
            {
                Assert.IsNotNull(State(PlayerView.IdleStateName(direction)).motion, $"idle {direction}");
                Assert.IsNotNull(State(PlayerView.WalkStateName(direction)).motion, $"walk {direction}");
            }
        }

        [Test]
        public void IdlePoseDiffersForEveryFacing()
        {
            var sprites = Directions
                .Select(d => Keys((AnimationClip)State(PlayerView.IdleStateName(d)).motion)[0].value)
                .ToArray();

            Assert.AreEqual(Directions.Length, sprites.Distinct().Count(),
                "Two facings share the same idle sprite, so one of them is not visible");
        }

        [Test]
        public void IdleDoesNotUseTheWalkSpeed()
        {
            foreach (var direction in Directions)
                Assert.IsFalse(State(PlayerView.IdleStateName(direction)).speedParameterActive, direction.ToString());
        }

        [Test]
        public void WalkCyclesAreEvenOneSecondLoops()
        {
            foreach (var direction in Directions)
            {
                var state = State(PlayerView.WalkStateName(direction));
                var clip = (AnimationClip)state.motion;
                var keys = Keys(clip);

                Assert.GreaterOrEqual(keys.Length, 4, $"{direction}: a cycle needs at least three phases and the closing key");
                Assert.AreEqual(1f, clip.length, 0.001f, $"{direction}: cycle length");
                Assert.IsTrue(clip.isLooping, $"{direction}: loops");
                Assert.AreSame(keys[0].value, keys[keys.Length - 1].value, $"{direction}: closing key repeats the first frame");

                // Unity adds one frame to a sprite clip, so the closing key sits one frame before 1 s.
                float span = 1f - 1f / clip.frameRate;
                float phase = span / (keys.Length - 1);
                for (int i = 0; i < keys.Length; i++)
                    Assert.AreEqual(i * phase, keys[i].time, 0.001f, $"{direction}: key {i} is not evenly spaced");

                Assert.IsTrue(state.speedParameterActive, $"{direction}: speed is driven by a parameter");
                Assert.AreEqual("walkSpeed", state.speedParameter);
            }
        }

        [Test]
        public void WalkCycleShowsMoreThanOneFrame()
        {
            foreach (var direction in Directions)
            {
                var keys = Keys((AnimationClip)State(PlayerView.WalkStateName(direction)).motion);
                Assert.GreaterOrEqual(keys.Select(k => k.value).Distinct().Count(), 3, direction.ToString());
            }
        }

        [Test]
        public void ControllerDeclaresTheWalkSpeedParameter()
        {
            var parameter = _controller.parameters.FirstOrDefault(p => p.name == "walkSpeed");
            Assert.IsNotNull(parameter);
            Assert.AreEqual(AnimatorControllerParameterType.Float, parameter.type);
        }

        [Test]
        public void StateNamesAreDistinctPerFacing()
        {
            var names = Directions.SelectMany(d => new[] { PlayerView.IdleStateName(d), PlayerView.WalkStateName(d) }).ToArray();
            Assert.AreEqual(names.Length, names.Distinct().Count());
        }
    }
}
