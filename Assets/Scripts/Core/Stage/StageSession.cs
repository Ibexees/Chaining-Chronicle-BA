using System;
using System.Collections.Generic;
using System.Linq;
using CC.Core.Grid;
using CC.Core.Util;

namespace CC.Core.Stage
{
    /// <summary>
    /// Runtime state and turn logic of one stage. Turn based, Mystery-Dungeon style:
    /// enemies move only after the player used up a turn (move or collect).
    /// Walking into a wall or a pickup only turns the player and costs no turn.
    /// Standing on the same cell as an enemy starts combat; the session then waits
    /// until <see cref="RemoveEnemy"/> resolves the engagement.
    /// </summary>
    public sealed class StageSession : IStageWorld
    {
        private readonly StageLayout _layout;
        private readonly IStageEnemyMover _mover;
        private readonly IRng _rng;
        private readonly List<StageEnemy> _enemies = new List<StageEnemy>();
        private readonly List<StagePickup> _pickups = new List<StagePickup>();
        private bool _finished;

        public int StageNumber => _layout.StageNumber;
        public TileGrid Grid => _layout.Grid;
        public GridPos PlayerPos { get; private set; }
        public Direction Facing { get; private set; } = Direction.Down;
        public IReadOnlyList<StageEnemy> Enemies => _enemies;
        public IReadOnlyList<StagePickup> Pickups => _pickups;

        /// <summary>Id of the enemy the player is currently fighting, or -1.</summary>
        public int EngagedEnemyId { get; private set; } = -1;

        public bool IsOnStairs => Grid.Get(PlayerPos) == TileType.StairsDown;

        public StageSession(StageLayout layout, IStageEnemyMover mover, IRng rng)
        {
            _layout = layout ?? throw new ArgumentNullException(nameof(layout));
            _mover = mover ?? throw new ArgumentNullException(nameof(mover));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));

            PlayerPos = layout.PlayerStart;
            int id = 0;
            foreach (var spawn in layout.Enemies) _enemies.Add(new StageEnemy(id++, spawn.TypeId, spawn.Level, spawn.Pos));
            foreach (var spawn in layout.Pickups) _pickups.Add(new StagePickup(id++, spawn.Kind, spawn.Pos));

            GameEvents.RaiseStageStarted(layout.StageNumber);
        }

        public bool IsOccupied(GridPos pos) => EnemyAt(pos) != null || PickupAt(pos) != null;

        public StageEnemy EnemyAt(GridPos pos) => _enemies.Find(e => e.Pos == pos);

        public StagePickup PickupAt(GridPos pos) => _pickups.Find(p => p.Pos == pos);

        public StageEnemy EnemyById(int id) => _enemies.Find(e => e.Id == id);

        private bool Accepting => !_finished && EngagedEnemyId < 0;

        /// <summary>Tries to walk one cell. Blocked moves only change the facing.</summary>
        public TurnResult TryMove(Direction direction)
        {
            if (!Accepting) return Result(TurnKind.Ignored);

            Facing = direction;
            var from = PlayerPos;
            var to = from.Step(direction);

            if (!Grid.IsWalkable(to) || PickupAt(to) != null) return Result(TurnKind.Turned);

            PlayerPos = to;
            GameEvents.RaisePlayerMoved(to);

            var enemyHere = EnemyAt(to);
            if (enemyHere != null)
            {
                Engage(enemyHere.Id);
                return new TurnResult(TurnKind.Moved, from, to, Facing, engagedEnemyId: enemyHere.Id);
            }

            var moves = RunEnemyTurn();
            return new TurnResult(TurnKind.Moved, from, to, Facing, enemyMoves: moves, engagedEnemyId: EngagedEnemyId);
        }

        /// <summary>
        /// Context sensitive interact key: use the stairs under the player, otherwise collect
        /// the pickup on the cell the player faces.
        /// </summary>
        public TurnResult Interact()
        {
            if (!Accepting) return Result(TurnKind.Ignored);

            if (IsOnStairs)
            {
                _finished = true;
                return Result(TurnKind.Descended);
            }

            var target = PlayerPos.Step(Facing);
            var pickup = PickupAt(target);
            if (pickup == null) return Result(TurnKind.NothingToInteract);

            _pickups.Remove(pickup);
            GameEvents.RaisePowerUpCollected(pickup.Kind);

            var moves = RunEnemyTurn();
            return new TurnResult(
                TurnKind.PickedUp, PlayerPos, PlayerPos, Facing,
                pickup.Kind, pickup.Id, moves, EngagedEnemyId);
        }

        /// <summary>
        /// The enemies that take part in a combat (A36): the engaged enemy first, then every other enemy within
        /// <paramref name="joinRadius"/> walking steps of the player, nearest first (ties by id), at most
        /// <paramref name="maxEnemies"/> in total. Distances follow the corridors, so an enemy behind a wall
        /// does not join. Empty if the engaged enemy does not exist.
        /// </summary>
        public IReadOnlyList<StageEnemy> CombatParticipants(int engagedEnemyId, int joinRadius, int maxEnemies)
        {
            var engaged = EnemyById(engagedEnemyId);
            if (engaged == null || maxEnemies < 1) return new List<StageEnemy>();

            var distances = new DistanceMap(Grid, PlayerPos, maxSteps: Math.Max(joinRadius, 0));
            var joining = _enemies
                .Where(e => e != engaged && distances.IsReachable(e.Pos))
                .OrderBy(e => distances[e.Pos])
                .ThenBy(e => e.Id)
                .Take(maxEnemies - 1);

            return new[] { engaged }.Concat(joining).ToList();
        }

        /// <summary>Removes a defeated or escaped-from enemy and ends the engagement (A5, A6).</summary>
        public void RemoveEnemy(int enemyId)
        {
            var enemy = EnemyById(enemyId);
            if (enemy == null) throw new ArgumentException($"No enemy with id {enemyId}", nameof(enemyId));

            _enemies.Remove(enemy);
            if (EngagedEnemyId == enemyId) EngagedEnemyId = -1;
        }

        private TurnResult Result(TurnKind kind) => new TurnResult(kind, PlayerPos, PlayerPos, Facing);

        private void Engage(int enemyId)
        {
            EngagedEnemyId = enemyId;
            GameEvents.RaiseEnemyEngaged(enemyId);
        }

        private List<EnemyMove> RunEnemyTurn()
        {
            var moves = new List<EnemyMove>();
            // Snapshot: ids are stable and list order is spawn order, so the turn order is deterministic.
            foreach (var enemy in _enemies.ToArray())
            {
                var from = enemy.Pos;
                var target = _mover.ChooseStep(enemy, this, _rng);
                if (!IsLegalEnemyStep(from, target)) continue;

                enemy.Pos = target;
                moves.Add(new EnemyMove(enemy.Id, from, target));

                if (target == PlayerPos)
                {
                    Engage(enemy.Id);
                    break; // combat starts, the remaining enemies wait
                }
            }
            return moves;
        }

        private bool IsLegalEnemyStep(GridPos from, GridPos target)
        {
            if (target == from) return false;
            if (GridPos.Manhattan(from, target) != 1) return false;
            if (!Grid.IsWalkable(target)) return false;
            return target == PlayerPos || !IsOccupied(target);
        }
    }
}
