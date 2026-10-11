using System.Collections;
using System.Collections.Generic;
using CC.Core.Flow;
using CC.Core.Run;
using CC.Core.Stage;
using CC.Core.Util;
using CC.Data;
using CC.Game.Boot;
using UnityEngine;

namespace CC.Game.Stage
{
    /// <summary>
    /// Brings the Core stage to life: generates the stage of the current run, creates the views,
    /// runs one player action at a time and animates its result. Rules live in StageSession.
    /// </summary>
    public sealed class StageController : MonoBehaviour
    {
        private const int PickupSortingOrder = 5;
        private const int EnemySortingOrder = 10;
        private const int PlayerSortingOrder = 11;
        // Idle only after this long without input, so a held key or quick taps keep the walk cycle running.
        private const float IdleGraceSeconds = 0.06f;

        [SerializeField] private GameContent content;
        [SerializeField] private StageView view;
        [SerializeField] private PlayerController playerInput;
        [SerializeField] private StageCameraFollow cameraFollow;
        [SerializeField] private float stepSeconds = 0.14f;
        [Tooltip("Steps one full walk cycle spans. 2 = one foot per step, 1 = the whole cycle in every step.")]
        [SerializeField] private float stepsPerWalkCycle = 2f;

        private GameFlow _flow;
        private RunState _run;
        private StageSession _session;
        private BiomeDef _biome;
        private PlayerView _player;
        private readonly Dictionary<int, EnemyView> _enemyViews = new Dictionary<int, EnemyView>();
        private readonly Dictionary<int, PickupView> _pickupViews = new Dictionary<int, PickupView>();
        private Transform _entityRoot;
        private bool _busy;
        private float _idleTimer;

        public StageSession Session => _session;
        public int StageNumber => _run?.StageNumber ?? 0;
        public int Seed => _run?.Seed ?? 0;

        private void Start()
        {
            if (!Services.TryGet(out _flow) || _flow.Run == null)
            {
                Debug.LogError("StageController needs a running game. Start from the Boot scene and begin a run in the Hub.");
                enabled = false;
                return;
            }

            _run = _flow.Run;
            _entityRoot = new GameObject("Entities").transform;
            _entityRoot.SetParent(transform, false);
            BuildStage(_run.StageNumber);
        }

        private void Update()
        {
            if (_busy || _session == null || _flow.Current != GameFlowState.Exploring) return;
            if (playerInput.TryGetIntent(out var intent))
            {
                _idleTimer = 0f;
                StartCoroutine(Execute(intent));
                return;
            }

            _idleTimer += Time.deltaTime;
            if (_idleTimer >= IdleGraceSeconds) _player.StopWalking();
        }

        /// <summary>Removes an enemy after combat (won or fled) and lets the player continue (A5, A6).</summary>
        public void ResolveEngagement(int enemyId)
        {
            _session.RemoveEnemy(enemyId);
            if (_enemyViews.TryGetValue(enemyId, out var enemyView))
            {
                Destroy(enemyView.gameObject);
                _enemyViews.Remove(enemyId);
            }
        }

        public EnemyView EnemyViewFor(int enemyId) => _enemyViews.TryGetValue(enemyId, out var v) ? v : null;

        private void BuildStage(int stageNumber)
        {
            ClearEntities();

            _biome = content.BiomeFor(stageNumber);
            var layout = StageGenerator.Generate(_run.Seed, stageNumber, content.ParamsFor(stageNumber));
            var rng = new SeededRng(_run.Seed).Derive(1000 + stageNumber);
            _session = new StageSession(layout, new ChaseOrWanderMover(), rng);

            view.Build(layout, _biome);

            _player = EntityView.Create<PlayerView>("Player", _entityRoot, content.playerAnimator, PlayerSortingOrder);
            _player.SetCell(_session.PlayerPos);
            _player.SetWalkCycle(stepSeconds, stepsPerWalkCycle);
            _player.Face(_session.Facing);

            foreach (var enemy in _session.Enemies)
            {
                var def = _biome.FindEnemy(enemy.TypeId);
                var enemyView = EntityView.Create<EnemyView>($"Enemy_{enemy.TypeId}_{enemy.Id}", _entityRoot,
                    def != null ? def.animator : null, EnemySortingOrder);
                enemyView.EnemyId = enemy.Id;
                enemyView.TypeId = enemy.TypeId;
                enemyView.SetCell(enemy.Pos);
                _enemyViews[enemy.Id] = enemyView;
            }

            foreach (var pickup in _session.Pickups)
            {
                var pickupView = EntityView.Create<PickupView>($"Pickup_{pickup.Kind}_{pickup.Id}", _entityRoot,
                    content.pickupAnimator, PickupSortingOrder);
                pickupView.SetCell(pickup.Pos);
                _pickupViews[pickup.Id] = pickupView;
            }

            if (cameraFollow != null) cameraFollow.SetTarget(_player.transform, snap: true);
        }

        private void ClearEntities()
        {
            foreach (Transform child in _entityRoot) Destroy(child.gameObject);
            _enemyViews.Clear();
            _pickupViews.Clear();
            _player = null;
        }

        private IEnumerator Execute(PlayerIntent intent)
        {
            _busy = true;

            var result = intent.IsInteract ? _session.Interact() : _session.TryMove(intent.Direction);

            switch (result.Kind)
            {
                case TurnKind.Moved:
                    // Stays in the walk loop. Update returns to idle once no further step follows.
                    _player.Walk(result.Facing);
                    yield return Animate(result, playerMoves: true);
                    break;

                case TurnKind.PickedUp:
                    _player.Face(result.Facing);
                    if (_pickupViews.TryGetValue(result.PickupId, out var pickupView))
                    {
                        Destroy(pickupView.gameObject);
                        _pickupViews.Remove(result.PickupId);
                    }
                    yield return Animate(result, playerMoves: false);
                    break;

                case TurnKind.Descended:
                    _run.AdvanceStage();
                    BuildStage(_run.StageNumber);
                    break;

                default:
                    // Turned, nothing to interact with: no time passes. Skip a frame so a held key does not spin.
                    _player.Face(result.Facing);
                    yield return null;
                    break;
            }

            if (result.EnemyEngaged)
            {
                _player.StopWalking();
                playerInput.ClearBuffer();
                _flow.GoTo(GameFlowState.Combat);
            }

            _busy = false;
        }

        private IEnumerator Animate(TurnResult result, bool playerMoves)
        {
            var transforms = new List<Transform>();
            var from = new List<Vector3>();
            var to = new List<Vector3>();

            if (playerMoves)
            {
                transforms.Add(_player.transform);
                from.Add(StageView.CellToWorld(result.PlayerFrom));
                to.Add(StageView.CellToWorld(result.PlayerTo));
            }
            foreach (var move in result.EnemyMoves)
            {
                if (!_enemyViews.TryGetValue(move.EnemyId, out var enemyView)) continue;
                transforms.Add(enemyView.transform);
                from.Add(StageView.CellToWorld(move.From));
                to.Add(StageView.CellToWorld(move.To));
            }

            yield return Motion.Slide(transforms.ToArray(), from.ToArray(), to.ToArray(), stepSeconds);
        }
    }
}
