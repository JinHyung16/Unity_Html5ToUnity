using System;
using System.Collections.Generic;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 수송선 공격 `[소스] Cq3gr8XO.js:Pf=class{`.
    ///
    /// <list type="number">
    /// <item>init: 배가 최대면 실패 알림. 병력(기본 1/5, 가진 만큼) · 내릴 칸 · 출발 칸을 정하고 병력을 실은 배를 띄운다.</item>
    /// <item>tick: 후퇴 명령이면 출발 칸으로 돌아간다. 길 찾기 한 걸음씩.</item>
    /// <item>도착: 내 칸이거나 물이면 병력을 돌려받는다. 아니면 내릴 칸을 먹고, 우호 대상이면 병력을 돌려받고 아니면 그 칸에서 공격을 시작한다.</item>
    /// </list>
    /// </summary>
    public sealed class FrontWarsTransportShipExecution : IFrontWarsExecution
    {
        private const int TicksPerMove = 1;
        private const int PendingTouchInterval = 5;
        private const int PathMaxTries = 2000;
        private const int PathX = 500;

        private readonly FrontWarsPlayer _attacker;
        private readonly string _targetId;
        private readonly int _ref;
        private double? _startTroops;
        private int _src;
        private FrontWarsGame _game;
        private IFrontWarsUnitPathFinder _path;
        private FrontWarsPlayer _target;
        private FrontWarsUnit _boat;
        private int _dst = -1;
        private int _lastMove;

        public bool IsActive { get; private set; } = true;
        public bool ActiveDuringSpawnPhase => false;

        /// <param name="targetId">null = 주인 없는 땅.</param>
        /// <param name="sourceTile">−1 = 알아서 고른다.</param>
        public FrontWarsTransportShipExecution(FrontWarsPlayer attacker, string targetId, int destinationTile, double? troops, int sourceTile = -1)
        {
            _attacker = attacker;
            _targetId = targetId;
            _ref = destinationTile;
            _startTroops = troops;
            _src = sourceTile;
        }

        public void Init(FrontWarsGame game, int tick)
        {
            if ((_targetId != null && game.HasPlayer(_targetId) == false) || game.Map.IsValidRef(_ref) == false || (_src >= 0 && game.Map.IsValidRef(_src) == false))
            {
                IsActive = false;
                return;
            }

            _lastMove = tick;
            _game = game;
            _path = game.Paths.Mini(PathMaxTries, true, PathX);

            if (_attacker.UnitCount(EFrontWarsUnit.TransportShip) >= game.Settings.Config.BoatMaxNumber)
            {
                game.DisplayMessage(EFrontWarsMessage.AttackFailed, _attacker, game.Settings.Config.BoatMaxNumber);
                IsActive = false;
                return;
            }

            _target = _targetId == null ? null : game.Player(_targetId);
            _startTroops ??= game.Rules.BoatAttackAmount(_attacker);
            _startTroops = Math.Min(_startTroops.Value, _attacker.Troops);
            _dst = FrontWarsTransportShipRules.TargetTransportTile(game, _ref);

            if (_dst < 0)
            {
                IsActive = false;
                return;
            }

            int spawn = _attacker.CanBuild(EFrontWarsUnit.TransportShip, _dst);

            if (spawn == FrontWarsPlayer.NoTile)
            {
                IsActive = false;
                return;
            }

            if (_src < 0)
                _src = spawn;
            else if (game.OwnerSmallId(_src) != _attacker.SmallId || game.Map.IsShore(_src) == false)
                _src = spawn;

            _boat = _attacker.BuildUnit(EFrontWarsUnit.TransportShip, _src, new FrontWarsUnitParams { Troops = _startTroops.Value });
            _boat.TargetTile = _dst;

            // 목적지를 정한 뒤 화면에 한 번 더 알린다 — 만들 때 알림은 목적지 이전이라 배 목적지 표시가 한 틱 늦는다 (판 계산에는 안 들어간다)
            game.RaiseUnitUpdate(_boat);

            if (_target != null)
                game.DisplayIncomingUnit(_boat.Id, EFrontWarsMessage.NavalInvasionInbound, _target);
        }

        public void Tick(int tick)
        {
            if (_dst < 0)
            {
                IsActive = false;
                return;
            }

            if (IsActive == false)
                return;

            if (_boat.IsActive == false)
            {
                IsActive = false;
                return;
            }

            if (tick - _lastMove < TicksPerMove)
                return;

            _lastMove = tick;

            if (_boat.Retreating)
            {
                _dst = _src;

                if (_boat.TargetTile != _dst)
                {
                    _boat.TargetTile = _dst;
                    _path.Reset();
                }
            }

            FrontWarsPathStep step = _path.NextTile(_boat.Tile, _dst);

            switch (step.Status)
            {
                case EFrontWarsPathStatus.Completed:
                    Arrive();
                    return;
                case EFrontWarsPathStatus.NextTile:
                    _boat.Move(step.Tile);
                    break;
                case EFrontWarsPathStatus.Pending:
                    if (tick % PendingTouchInterval == 0)
                        _game.RaiseUnitUpdate(_boat);
                    break;
                case EFrontWarsPathStatus.PathNotFound:
                    _attacker.AddTroops(_boat.Troops);
                    _boat.Delete(false);
                    IsActive = false;
                    return;
            }
        }

        private void Arrive()
        {
            if (_game.OwnerSmallId(_dst) == _attacker.SmallId || _game.Map.IsWater(_dst))
            {
                _attacker.AddTroops(_boat.Troops);
                _boat.Delete(false);
                IsActive = false;
                return;
            }

            _attacker.Conquer(_dst);

            if (_target != null && _attacker.IsFriendly(_target))
                _attacker.AddTroops(_boat.Troops);
            else
                _game.AddExecution(new FrontWarsAttackExecution(_boat.Troops, _attacker, _targetId, _dst, false));

            _boat.Delete(false);
            IsActive = false;
        }
    }

    /// <summary>수송선 후퇴 명령 `[소스] Cq3gr8XO.js:vu=class{`.</summary>
    public sealed class FrontWarsCancelBoatExecution : IFrontWarsExecution
    {
        private readonly FrontWarsPlayer _player;
        private readonly int _unitId;

        public bool IsActive { get; private set; } = true;
        public bool ActiveDuringSpawnPhase => false;

        public FrontWarsCancelBoatExecution(FrontWarsPlayer player, int unitId)
        {
            _player = player;
            _unitId = unitId;
        }

        public void Init(FrontWarsGame game, int tick)
        {
        }

        public void Tick(int tick)
        {
            FrontWarsUnit boat = _player.Units().Find(u => u.Id == _unitId && u.Type == EFrontWarsUnit.TransportShip);
            boat?.OrderBoatRetreat();
            IsActive = false;
        }
    }

    /// <summary>전함 순찰 칸 옮기기 — init 에서 바로 한다 `[소스] Cq3gr8XO.js:pp=class{`.</summary>
    public sealed class FrontWarsMoveWarshipExecution : IFrontWarsExecution
    {
        private readonly FrontWarsPlayer _owner;
        private readonly int _unitId;
        private readonly int _position;

        public bool IsActive => false;
        public bool ActiveDuringSpawnPhase => false;

        public FrontWarsMoveWarshipExecution(FrontWarsPlayer owner, int unitId, int tile)
        {
            _owner = owner;
            _unitId = unitId;
            _position = tile;
        }

        public void Init(FrontWarsGame game, int tick)
        {
            if (game.Map.IsValidRef(_position) == false)
                return;

            FrontWarsUnit warship = _owner.Units(EFrontWarsUnit.Warship).Find(u => u.Id == _unitId);

            if (warship == null || warship.IsActive == false)
                return;

            warship.PatrolTile = _position;
            warship.TargetTile = -1;
            warship.TargetUnit = null;
        }

        public void Tick(int tick)
        {
        }
    }

    /// <summary>
    /// 64개가 넘으면 끝난 유닛을 걸러내는 유닛 집합 `[소스] Cq3gr8XO.js:_f=class{`.
    /// </summary>
    internal sealed class FrontWarsPruningUnitSet
    {
        private const int MinPruneAt = 64;

        private readonly List<FrontWarsUnit> _units = new List<FrontWarsUnit>();
        private int _pruneAt = MinPruneAt;

        public bool Has(FrontWarsUnit unit)
        {
            return _units.Contains(unit);
        }

        public void Add(FrontWarsUnit unit)
        {
            if (_units.Count >= _pruneAt)
            {
                _units.RemoveAll(u => u.IsActive == false);
                _pruneAt = Math.Max(MinPruneAt, _units.Count * 2);
            }

            if (_units.Contains(unit) == false)
                _units.Add(unit);
        }
    }

    /// <summary>
    /// 전함 `[소스] Cq3gr8XO.js:vf=class{`.
    ///
    /// <list type="number">
    /// <item>항구가 있으면 매 틱 체력 +1.</item>
    /// <item>표적: 반경 130 안 적 수송선 → 전함 → 무역선(항구가 있어야, 해적 보호 끝, 순찰 반경 안) 순, 같은 종류면 가까운 쪽.</item>
    /// <item>무역선이면 쫓아가 붙으면 빼앗는다. 아니면 순찰하며 20틱 넘게 지나면 포탄을 쏜다.</item>
    /// </list>
    /// </summary>
    public sealed class FrontWarsWarshipExecution : IFrontWarsExecution
    {
        private const int PathMaxTries = 2000;
        private const int PathX = 500;
        private const int HuntSteps = 2;
        private const int HuntArriveDistance = 5;
        private const int PatrolRetryTicks = 100;
        private const int PatrolAttemptsPerRound = 500;
        private const int PatrolRounds = 3;
        private const int HealPerTick = 1;

        private static readonly EFrontWarsUnit[] TargetTypes = { EFrontWarsUnit.TransportShip, EFrontWarsUnit.Warship, EFrontWarsUnit.TradeShip };

        private readonly FrontWarsPlayer _owner;
        private readonly int _patrolTile;
        private readonly FrontWarsPruningUnitSet _alreadySentShell = new FrontWarsPruningUnitSet();
        private readonly FrontWarsPruningUnitSet _unreachable = new FrontWarsPruningUnitSet();
        private FrontWarsGame _game;
        private IFrontWarsUnitPathFinder _path;
        private FrontWarsRandom _random;
        private FrontWarsUnit _warship;
        private int _lastShellAttack;
        private int _patrolSearchRetryAt;

        public bool IsActive => _warship != null && _warship.IsActive;
        public bool ActiveDuringSpawnPhase => false;

        public FrontWarsWarshipExecution(FrontWarsPlayer owner, int patrolTile)
        {
            _owner = owner;
            _patrolTile = patrolTile;
        }

        /// <summary>이미 있는 전함을 이어받는다 (원본 input 이 유닛일 때).</summary>
        public FrontWarsWarshipExecution(FrontWarsUnit warship)
        {
            _warship = warship;
            _owner = warship.Owner;
            _patrolTile = warship.PatrolTile;
        }

        public void Init(FrontWarsGame game, int tick)
        {
            _game = game;
            _path = game.Paths.Mini(PathMaxTries, true, PathX);
            _random = new FrontWarsRandom(game.Ticks);

            if (_warship != null)
                return;

            int spawn = _owner.CanBuild(EFrontWarsUnit.Warship, _patrolTile);

            if (spawn == FrontWarsPlayer.NoTile)
                return;

            _warship = _owner.BuildUnit(EFrontWarsUnit.Warship, spawn, new FrontWarsUnitParams { PatrolTile = _patrolTile });
        }

        public void Tick(int tick)
        {
            if (_warship.Health <= 0)
            {
                _warship.Delete();
                return;
            }

            if (_warship.Owner.UnitCount(EFrontWarsUnit.Port) > 0)
                _warship.ModifyHealth(HealPerTick, null);

            _warship.TargetUnit = FindTargetUnit();

            if (_warship.TargetUnit != null && _warship.TargetUnit.Type == EFrontWarsUnit.TradeShip)
            {
                HuntDownTradeShip();
                return;
            }

            Patrol();

            if (_warship.TargetUnit != null)
                ShootTarget();
        }

        private FrontWarsUnit FindTargetUnit()
        {
            FrontWarsPlayer owner = _warship.Owner;
            bool hasPort = owner.UnitCount(EFrontWarsUnit.Port) > 0;
            int patrolRange = _game.Settings.Config.WarshipPatrolRange;
            int patrolRangeSquared = patrolRange * patrolRange;
            FrontWarsUnit best = null;
            int bestPriority = 3;
            double bestDist = double.PositiveInfinity;

            foreach (var (unit, dist) in _game.Units.NearbyUnits(_warship.Tile, _game.Settings.Config.WarshipTargetingRange, TargetTypes))
            {
                if (unit.Owner == owner || unit == _warship || unit.Owner.IsFriendly(owner) || _alreadySentShell.Has(unit) || _unreachable.Has(unit))
                    continue;

                if (unit.Type == EFrontWarsUnit.TradeShip)
                {
                    FrontWarsPlayer destinationOwner = unit.TargetUnit?.Owner;

                    if (hasPort == false || unit.IsSafeFromPirates || destinationOwner == owner || (destinationOwner != null && destinationOwner.IsFriendly(owner)))
                        continue;

                    if (_warship.PatrolTile >= 0 && _game.Map.EuclideanDistanceSquared(_warship.PatrolTile, unit.Tile) > patrolRangeSquared)
                        continue;
                }

                int priority = unit.Type == EFrontWarsUnit.TransportShip ? 0 : unit.Type == EFrontWarsUnit.Warship ? 1 : 2;

                if (priority < bestPriority || (priority == bestPriority && dist < bestDist))
                {
                    best = unit;
                    bestPriority = priority;
                    bestDist = dist;
                }
            }

            return best;
        }

        /// <summary>20틱마다 포탄. 수송선에 쏠 때는 간격을 안 잰다 (원본). 체력 없는 목표면 한 번만 쏜다.</summary>
        private void ShootTarget()
        {
            FrontWarsUnit target = _warship.TargetUnit;

            if (_game.Ticks - _lastShellAttack <= _game.Settings.Config.WarshipShellIntervalTicks)
                return;

            if (target.Type != EFrontWarsUnit.TransportShip)
                _lastShellAttack = _game.Ticks;

            _game.AddExecution(new FrontWarsShellExecution(_warship.Tile, _warship.Owner, _warship, target));

            if (target.HasHealth == false)
            {
                _alreadySentShell.Add(target);
                _warship.TargetUnit = null;
            }
        }

        private void HuntDownTradeShip()
        {
            FrontWarsUnit target = _warship.TargetUnit;

            for (int i = 0; i < HuntSteps; i++)
            {
                FrontWarsPathStep step = _path.NextTile(_warship.Tile, target.Tile, HuntArriveDistance);

                switch (step.Status)
                {
                    case EFrontWarsPathStatus.Completed:
                        _warship.Owner.CaptureUnit(target);
                        _warship.TargetUnit = null;
                        _warship.Move(_warship.Tile);
                        return;
                    case EFrontWarsPathStatus.NextTile:
                        _warship.Move(step.Tile);
                        break;
                    case EFrontWarsPathStatus.Pending:
                        _game.RaiseUnitUpdate(_warship);
                        break;
                    case EFrontWarsPathStatus.PathNotFound:
                        _unreachable.Add(target);
                        _warship.TargetUnit = null;
                        return;
                }
            }
        }

        private void Patrol()
        {
            if (_warship.TargetTile < 0)
            {
                if (_game.Ticks < _patrolSearchRetryAt)
                    return;

                int tile = RandomTile(false);

                if (tile < 0)
                {
                    _patrolSearchRetryAt = _game.Ticks + PatrolRetryTicks;
                    return;
                }

                _warship.TargetTile = tile;
            }

            FrontWarsPathStep step = _path.NextTile(_warship.Tile, _warship.TargetTile);

            switch (step.Status)
            {
                case EFrontWarsPathStatus.Completed:
                    _warship.TargetTile = -1;
                    _warship.Move(step.Tile);
                    break;
                case EFrontWarsPathStatus.NextTile:
                    _warship.Move(step.Tile);
                    break;
                case EFrontWarsPathStatus.Pending:
                    _game.RaiseUnitUpdate(_warship);
                    return;
                case EFrontWarsPathStatus.PathNotFound:
                    _warship.TargetTile = -1;
                    break;
            }
        }

        /// <summary>
        /// 순찰 칸 둘레에서 바다 칸을 뽑는다 — 500번 실패마다 반경을 1.5배, 3바퀴. 해안 제외로 못 찾으면 해안 포함으로 한 번 더 `[소스] randomTile(e=!1){`.
        /// </summary>
        private int RandomTile(bool allowShoreline)
        {
            int patrol = _warship.PatrolTile;
            if (patrol < 0)
                return -1;

            int range = _game.Settings.Config.WarshipPatrolRange;
            int attempts = 0, rounds = 0;
            int px = _game.Map.X(patrol), py = _game.Map.Y(patrol);

            while (rounds < PatrolRounds)
            {
                int half = Math.Max(1, range / 2);
                int x = px + _random.NextInt(-half, half);
                int y = py + _random.NextInt(-half, half);

                if (_game.Map.IsValidCoord(x, y))
                {
                    int tile = _game.Map.Ref(x, y);
                    if (_game.Map.IsOcean(tile) && (allowShoreline || _game.Map.IsShoreline(tile) == false))
                        return tile;
                }

                if (++attempts >= PatrolAttemptsPerRound)
                {
                    rounds++;
                    attempts = 0;
                    range += range / 2;
                }
            }

            return allowShoreline ? -1 : RandomTile(true);
        }
    }

    /// <summary>
    /// 무역선 `[소스] Cq3gr8XO.js:ff=class{`. 출발 항구에서 목적지 항구까지 물길로 가서, 도착하면 두 주인이 골드를 받는다.
    /// <para>빼앗기면 빼앗은 쪽 가장 가까운 항구로 목적지를 바꾸고, 도착 골드는 빼앗은 쪽만 받는다.</para>
    /// </summary>
    public sealed class FrontWarsTradeShipExecution : IFrontWarsExecution
    {
        private const int PendingMoveInterval = 5;
        private const int PathX = 4;

        private readonly FrontWarsPlayer _origOwner;
        private readonly FrontWarsUnit _srcPort;
        private FrontWarsUnit _dstPort;
        private FrontWarsGame _game;
        private IFrontWarsUnitPathFinder _path;
        private FrontWarsUnit _ship;
        private bool _wasCaptured;
        private int _tilesTraveled;

        public bool IsActive { get; private set; } = true;
        public bool ActiveDuringSpawnPhase => false;

        public FrontWarsTradeShipExecution(FrontWarsPlayer origOwner, FrontWarsUnit srcPort, FrontWarsUnit dstPort)
        {
            _origOwner = origOwner;
            _srcPort = srcPort;
            _dstPort = dstPort;
        }

        public void Init(FrontWarsGame game, int tick)
        {
            _game = game;
            _path = game.Paths.MiniWaterSync(true, PathX);
        }

        public void Tick(int tick)
        {
            if (_ship == null)
            {
                int spawn = _origOwner.CanBuild(EFrontWarsUnit.TradeShip, _srcPort.Tile);

                if (spawn == FrontWarsPlayer.NoTile)
                {
                    IsActive = false;
                    return;
                }

                _ship = _origOwner.BuildUnit(EFrontWarsUnit.TradeShip, spawn, new FrontWarsUnitParams { TargetUnit = _dstPort, LastSetSafeFromPirates = tick });
            }

            if (_ship.IsActive == false)
            {
                IsActive = false;
                return;
            }

            FrontWarsPlayer owner = _ship.Owner;
            FrontWarsPlayer dstOwner = _dstPort.Owner;

            if (_wasCaptured == false && _origOwner != owner)
                _wasCaptured = true;

            if (_wasCaptured == false && dstOwner.Id == _srcPort.Owner.Id)
            {
                End();
                return;
            }

            if (_wasCaptured == false && (_dstPort.IsActive == false || owner.CanTrade(dstOwner) == false))
            {
                End();
                return;
            }

            if (_wasCaptured && (owner != dstOwner || _dstPort.IsActive == false) && RetargetToOwnPort(null) == false)
            {
                End();
                return;
            }

            int tile = _ship.Tile;

            if (tile == _dstPort.Tile)
            {
                Complete();
                return;
            }

            FrontWarsPathStep step = _path.NextTile(tile, _dstPort.Tile);

            switch (step.Status)
            {
                case EFrontWarsPathStatus.Pending:
                    if (_game.Ticks % PendingMoveInterval == 0)
                        _ship.Move(tile);
                    break;
                case EFrontWarsPathStatus.NextTile:
                    if (_game.Map.IsWater(step.Tile) && _game.Map.IsShoreline(step.Tile))
                        _ship.SetSafeFromPirates();

                    _ship.Move(step.Tile);
                    _tilesTraveled++;
                    break;
                case EFrontWarsPathStatus.Completed:
                    Complete();
                    break;
                case EFrontWarsPathStatus.PathNotFound:
                    if (_wasCaptured && RetargetToOwnPort(_dstPort))
                        break;

                    if (_ship.IsActive)
                        _ship.Delete(false);

                    IsActive = false;
                    break;
            }
        }

        private void End()
        {
            _ship.Delete(false);
            IsActive = false;
        }

        private bool RetargetToOwnPort(FrontWarsUnit exclude)
        {
            FrontWarsUnit best = null;
            double bestDist = double.PositiveInfinity;

            foreach (FrontWarsUnit port in _ship.Owner.Units(EFrontWarsUnit.Port))
            {
                if (port.IsActive == false || port == exclude)
                    continue;

                int d = _game.Map.ManhattanDistance(_ship.Tile, port.Tile);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = port;
                }
            }

            if (best == null)
                return false;

            _dstPort = best;
            _ship.TargetUnit = best;
            _path.Reset();
            return true;
        }

        private void Complete()
        {
            IsActive = false;
            _ship.Delete(false);

            int ports = _ship.Owner.UnitCount(EFrontWarsUnit.Port);
            long gold = _game.Settings.EconomyMode == EFrontWarsEconomyMode.Fast
                ? _game.Rules.TradeShipGoldFast(_tilesTraveled, ports)
                : _game.Rules.TradeShipGold(_tilesTraveled, ports);

            if (_wasCaptured)
            {
                _ship.Owner.AddGold(gold, _dstPort.Tile);
                _game.DisplayMessage(EFrontWarsMessage.CapturedEnemyUnit, _ship.Owner, gold, _origOwner.Name);
                return;
            }

            _srcPort.Owner.AddGold(gold);
            _dstPort.Owner.AddGold(gold, _dstPort.Tile);
            _game.DisplayMessage(EFrontWarsMessage.ReceivedGoldFromTrade, _dstPort.Owner, gold, _srcPort.Owner.Name);
            _game.DisplayMessage(EFrontWarsMessage.ReceivedGoldFromTrade, _srcPort.Owner, gold, _dstPort.Owner.Name);
        }
    }
}
