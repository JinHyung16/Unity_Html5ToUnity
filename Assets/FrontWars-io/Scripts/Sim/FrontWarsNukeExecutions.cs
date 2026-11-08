using System;
using System.Collections.Generic;
using System.Linq;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 원자탄 · 수소탄 · MIRV 탄두 `[소스] Cq3gr8XO.js:lf=class{`.
    ///
    /// <list type="number">
    /// <item>첫 틱: 사일로 칸(탄두는 넘겨받은 칸)에서 포물선을 계산하고 핵 유닛을 만든다. 파괴 칸을 미리 뽑아 동맹 파기를 판정하고, 대상에게 알림 · 사일로 발사.</item>
    /// <item>대기 틱이 남았으면 기다린다. 아니면 속도만큼 날아가고, 끝에 닿으면 터진다.</item>
    /// <item>폭발: 파괴 칸마다 주인이 내놓고 병력 · 공격 병력 · 수송선 병력을 죽인다. 땅은 낙진(물 핵이면 물). 반경 안 유닛 파괴.</item>
    /// </list>
    /// </summary>
    public sealed class FrontWarsNukeExecution : IFrontWarsExecution
    {
        /// <summary>내부 반경 밖 · 외부 반경 안 칸은 1/2 확률로 파괴.</summary>
        private const int OuterRingChance = 2;

        /// <summary>건물 다시 그리기 반경 여유 `[소스] this.redrawBuildings(e.outer+16)`.</summary>
        private const int RedrawMargin = 16;

        private const int AllianceBreakRelationPenalty = -100;

        /// <summary>폭발로 파괴되는 유닛 — 핵 계열을 뺀 전부 `[소스] cf=Object.values(S).filter(…)`.</summary>
        private static readonly EFrontWarsUnit[] DestroyableTypes = Enum.GetValues(typeof(EFrontWarsUnit)).Cast<EFrontWarsUnit>()
            .Where(t => t != EFrontWarsUnit.AtomBomb && t != EFrontWarsUnit.HydrogenBomb && t != EFrontWarsUnit.MIRVWarhead && t != EFrontWarsUnit.MIRV)
            .ToArray();

        /// <summary>다시 그릴 건물 `[소스] te=new Set([`City`,`Construction`,`Defense Post`,`SAM Launcher`,`Missile Silo`,`Port`])`.</summary>
        private static readonly EFrontWarsUnit[] RedrawTypes =
        {
            EFrontWarsUnit.City, EFrontWarsUnit.Construction, EFrontWarsUnit.DefensePost, EFrontWarsUnit.SAMLauncher, EFrontWarsUnit.MissileSilo, EFrontWarsUnit.Port
        };

        private readonly EFrontWarsUnit _type;
        private readonly FrontWarsPlayer _player;
        private readonly int _dst;
        private int _src;
        private int _speed;
        private int _waitTicks;
        private FrontWarsGame _game;
        private IFrontWarsParabolaPath _path;
        private FrontWarsUnit _nuke;
        private List<int> _tilesToDestroy;
        private HashSet<int> _tilesToDestroySet;

        public bool IsActive { get; private set; } = true;
        public bool ActiveDuringSpawnPhase => false;

        public FrontWarsUnit Nuke => _nuke;

        public FrontWarsNukeExecution(EFrontWarsUnit type, FrontWarsPlayer player, int dstTile, int srcTile = -1, int speed = -1, int waitTicks = 0)
        {
            _type = type;
            _player = player;
            _dst = dstTile;
            _src = srcTile;
            _speed = speed;
            _waitTicks = waitTicks;
        }

        public void Init(FrontWarsGame game, int tick)
        {
            _game = game;

            if (_speed == -1)
                _speed = game.Settings.Config.NukeSpeed;

            _path = game.Paths.Parabola();
        }

        private FrontWarsPlayer Target
        {
            get { return _game.Owner(_dst); }
        }

        /// <summary>
        /// 파괴 칸 — 목표에서 넓이 탐색. 물 핵이면 외부 반경 전부, 아니면 내부 반경 + 외부 고리 1/2 (틱 시드 난수) `[소스] tilesToDestroy(){`.
        /// <para>한 번 계산하면 고정한다.</para>
        /// </summary>
        private List<int> TilesToDestroy()
        {
            if (_tilesToDestroy != null)
                return _tilesToDestroy;

            JinHyung.Data.FrontWarsNukeData magnitude = _game.Settings.Nuke(_nuke.Type);
            int inner = magnitude.InnerRadius * magnitude.InnerRadius;
            int outer = magnitude.OuterRadius * magnitude.OuterRadius;

            if (_game.Settings.WaterNukes)
            {
                _tilesToDestroy = _game.Bfs(_dst, t => _game.Map.EuclideanDistanceSquared(_dst, t) <= outer);
            }
            else
            {
                var random = new FrontWarsRandom(_game.Ticks);
                _tilesToDestroy = _game.Bfs(_dst, t =>
                {
                    int d = _game.Map.EuclideanDistanceSquared(_dst, t);
                    return d <= outer && (d <= inner || random.Chance(OuterRingChance));
                });
            }

            _tilesToDestroySet = new HashSet<int>(_tilesToDestroy);
            return _tilesToDestroy;
        }

        /// <summary>
        /// 파괴 칸이 100칸 넘는 플레이어와 — 그 플레이어가 받은 내 동맹 요청 거절 · 동맹 파기 · 관계 −100 (탄두 제외) `[소스] maybeBreakAlliances(e){`.
        /// </summary>
        private void MaybeBreakAlliances(List<int> tiles)
        {
            var counts = new List<(FrontWarsPlayer Player, int Count)>();

            foreach (int t in tiles)
            {
                FrontWarsPlayer owner = _game.Owner(t);
                if (owner == null)
                    continue;

                int i = counts.FindIndex(c => c.Player == owner);
                if (i < 0)
                    counts.Add((owner, 1));
                else
                    counts[i] = (owner, counts[i].Count + 1);
            }

            int threshold = _game.Settings.Config.NukeAllianceBreakTiles;

            foreach (var (other, count) in counts)
            {
                if (count <= threshold || _nuke.Type == EFrontWarsUnit.MIRVWarhead)
                    continue;

                other.IncomingAllianceRequests().FirstOrDefault(r => r.Requestor == _player)?.Reject();

                FrontWarsAlliance alliance = _player.AllianceWith(other);
                if (alliance != null)
                    _player.BreakAlliance(alliance);

                if (other != _player)
                    other.UpdateRelation(_player, AllianceBreakRelationPenalty);
            }
        }

        public void Tick(int tick)
        {
            if (_nuke == null)
            {
                int src = _src >= 0 ? _src : _player.CanBuild(_type, _dst);

                if (src == FrontWarsPlayer.NoTile)
                {
                    IsActive = false;
                    return;
                }

                _src = src;
                _path.ComputeControlPoints(src, _dst, _speed, _type != EFrontWarsUnit.MIRVWarhead);
                _nuke = _player.BuildUnit(_type, src, new FrontWarsUnitParams { TargetTile = _dst, Trajectory = BuildTrajectory() });
                MaybeBreakAlliances(TilesToDestroy());

                if (_game.HasOwner(_dst))
                {
                    FrontWarsPlayer target = _game.Owner(_dst);

                    if (_type == EFrontWarsUnit.AtomBomb)
                        _game.DisplayIncomingUnit(_nuke.Id, EFrontWarsMessage.NukeInbound, target);
                    else if (_type == EFrontWarsUnit.HydrogenBomb)
                        _game.DisplayIncomingUnit(_nuke.Id, EFrontWarsMessage.HydrogenBombInbound, target);
                }

                _player.Units(EFrontWarsUnit.MissileSilo).Find(u => u.Tile == src)?.Launch();
                return;
            }

            if (_nuke.IsActive == false)
            {
                IsActive = false;
                return;
            }

            if (_waitTicks > 0)
            {
                _waitTicks--;
                return;
            }

            int next = _path.NextTile(_speed, out bool arrived);

            if (arrived)
            {
                Detonate();
                return;
            }

            UpdateNukeTargetable();
            _nuke.Move(next);
            _nuke.SetTrajectoryIndex(_path.CurrentIndex);
        }

        private int TargetableRangeSquared
        {
            get { return _game.Settings.Config.NukeTargetableRange * _game.Settings.Config.NukeTargetableRange; }
        }

        private List<FrontWarsTrajectoryPoint> BuildTrajectory()
        {
            int range = TargetableRangeSquared;
            return _path.AllTiles().Select(t => new FrontWarsTrajectoryPoint(t, IsTargetable(_dst, t, range))).ToList();
        }

        /// <summary>목표나 출발에서 150칸 안이면 SAM 이 잡을 수 있다.</summary>
        private bool IsTargetable(int target, int tile, int rangeSquared)
        {
            return _game.Map.EuclideanDistanceSquared(tile, target) < rangeSquared
                   || (_src >= 0 && _game.Map.EuclideanDistanceSquared(_src, tile) < rangeSquared);
        }

        private void UpdateNukeTargetable()
        {
            if (_nuke.TargetTile < 0)
                return;

            _nuke.SetTargetable(IsTargetable(_nuke.TargetTile, _nuke.Tile, TargetableRangeSquared));
        }

        private void Detonate()
        {
            JinHyung.Data.FrontWarsNukeData magnitude = _game.Settings.Nuke(_nuke.Type);
            List<int> tiles = TilesToDestroy();
            MaybeBreakAlliances(tiles);
            _game.RaiseNukeDetonated(_dst, _nuke.Type, magnitude.OuterRadius);

            FrontWarsPlayer target = Target;
            double maxTroops = target != null ? _game.Rules.MaxTroops(target) : 1;
            bool waterNukes = _game.Settings.WaterNukes;

            foreach (int t in tiles)
            {
                FrontWarsPlayer owner = _game.Owner(t);

                if (owner != null)
                {
                    owner.Relinquish(t);
                    owner.RemoveTroops(_game.Rules.NukeDeathFactor(_type, owner.Troops, owner.NumTilesOwned, maxTroops));

                    foreach (FrontWarsAttack attack in owner.OutgoingAttacks.ToList())
                        attack.Troops -= _game.Rules.NukeDeathFactor(_type, attack.Troops, owner.NumTilesOwned, maxTroops);

                    foreach (FrontWarsUnit boat in owner.Units(EFrontWarsUnit.TransportShip))
                        boat.Troops -= _game.Rules.NukeDeathFactor(_type, boat.Troops, owner.NumTilesOwned, maxTroops);
                }

                if (_game.Map.IsLand(t))
                {
                    if (waterNukes)
                        _game.ConvertToWater(t);
                    else
                        _game.SetFallout(t, true);
                }
            }

            int outer = magnitude.OuterRadius * magnitude.OuterRadius;

            foreach (var (unit, dist) in _game.Units.NearbyUnits(_dst, magnitude.OuterRadius, DestroyableTypes))
            {
                if (dist < outer || (waterNukes && _tilesToDestroySet.Contains(unit.Tile)))
                    unit.Delete(true, _player);
            }

            int redraw = magnitude.OuterRadius + RedrawMargin;
            int redrawSquared = redraw * redraw;

            foreach (var (unit, dist) in _game.Units.NearbyUnits(_dst, redraw, RedrawTypes))
            {
                if (dist < redrawSquared)
                    _game.RaiseUnitUpdate(unit);
            }

            IsActive = false;
            _nuke.SetReachedTarget();
            _nuke.Delete(false);
        }
    }

    /// <summary>
    /// MIRV `[소스] Cq3gr8XO.js:uf=class e{`. 목표 위 하늘까지 날아가 탄두 최대 350개로 갈라진다.
    /// <para>탄두 자리: 목표 주위 1500칸 안, 목표 플레이어 땅, 서로 55칸 이상 떨어진 곳을 1000번까지 뽑는다. 먼 탄두부터 더 빠르게 · 난수 대기로 쏜다.</para>
    /// </summary>
    public sealed class FrontWarsMirvExecution : IFrontWarsExecution
    {
        private const int MirvRange = 1500;
        private const int WarheadCount = 350;
        private const int SeparationAttempts = 1000;
        private const int RandomLandAttempts = 100;
        private const int MinSpacing = 55;
        private const int BucketFactor = 100000;
        private const int SeparateHeightOffset = 500;
        private const int SeparateHeightMargin = 50;
        private const int WarheadBaseSpeed = 15;
        private const int WarheadSpeedRange = 5;
        private const int WarheadWaitMax = 15;
        private const int RelationPenalty = -100;

        private readonly FrontWarsPlayer _player;
        private readonly int _dst;
        private FrontWarsGame _game;
        private FrontWarsRandom _random;
        private IFrontWarsParabolaPath _path;
        private FrontWarsPlayer _targetPlayer;
        private FrontWarsUnit _nuke;
        private int _speed;

        public bool IsActive { get; private set; } = true;
        public bool ActiveDuringSpawnPhase => false;

        public FrontWarsMirvExecution(FrontWarsPlayer player, int dstTile)
        {
            _player = player;
            _dst = dstTile;
        }

        public void Init(FrontWarsGame game, int tick)
        {
            _random = new FrontWarsRandom(game.Ticks + FrontWarsHash.Of(_player.Id));
            _game = game;
            _path = game.Paths.Parabola();
            _targetPlayer = game.Owner(_dst);
            _speed = game.Settings.Config.NukeSpeed;

            if (_targetPlayer != null)
            {
                FrontWarsAlliance alliance = _player.AllianceWith(_targetPlayer);
                if (alliance != null)
                    _player.BreakAlliance(alliance);

                if (_targetPlayer != _player)
                    _targetPlayer.UpdateRelation(_player, RelationPenalty);
            }
        }

        public void Tick(int tick)
        {
            if (_nuke == null)
            {
                int spawn = _player.CanBuild(EFrontWarsUnit.MIRV, _dst);

                if (spawn == FrontWarsPlayer.NoTile)
                {
                    IsActive = false;
                    return;
                }

                _nuke = _player.BuildUnit(EFrontWarsUnit.MIRV, spawn);

                // ⚠ 원본은 x 좌표를 칸 번호로 한 번 더 x() 에 넣는다 — 폭보다 작으면 같은 값이다
                FrontWarsTerrainMap map = _game.Map;
                int x = (int)Math.Floor((map.X(_dst) + map.X(map.X(_nuke.Tile))) / 2.0);
                int y = Math.Max(0, map.Y(_dst) - SeparateHeightOffset) + SeparateHeightMargin;
                int separateDst = map.Ref(x, y);
                _path.ComputeControlPoints(spawn, separateDst);
                _game.DisplayIncomingUnit(_nuke.Id, EFrontWarsMessage.MirvInbound, _targetPlayer);
            }

            int next = _path.NextTile(_speed, out bool arrived);

            if (arrived)
            {
                Separate();
                IsActive = false;
                return;
            }

            _nuke.Move(next);
        }

        private void Separate()
        {
            var targets = new List<int> { _dst };
            var buckets = new Dictionary<int, List<int>>();
            AddToBuckets(buckets, _dst);
            int attempts = SeparationAttempts;

            while (attempts > 0 && targets.Count < WarheadCount)
            {
                attempts--;
                int tile = RandomLand(buckets);

                if (tile >= 0)
                {
                    targets.Add(tile);
                    AddToBuckets(buckets, tile);
                }
            }

            FrontWarsTerrainMap map = _game.Map;
            List<int> ordered = targets.OrderByDescending(t => map.ManhattanDistance(t, _dst)).ToList();

            for (int i = 0; i < ordered.Count; i++)
            {
                int speed = WarheadBaseSpeed + (int)Math.Floor(i / (double)WarheadCount * WarheadSpeedRange);
                int wait = _random.NextInt(0, WarheadWaitMax);
                _game.AddExecution(new FrontWarsNukeExecution(EFrontWarsUnit.MIRVWarhead, _player, ordered[i], _nuke.Tile, speed, wait));
            }

            _nuke.Delete(false);
        }

        private int BucketKey(int tile)
        {
            return (int)Math.Floor(_game.Map.X(tile) / (double)MinSpacing) * BucketFactor + (int)Math.Floor(_game.Map.Y(tile) / (double)MinSpacing);
        }

        private void AddToBuckets(Dictionary<int, List<int>> buckets, int tile)
        {
            int key = BucketKey(tile);

            if (buckets.TryGetValue(key, out List<int> list))
                list.Add(tile);
            else
                buckets[key] = new List<int> { tile };
        }

        private int RandomLand(Dictionary<int, List<int>> buckets)
        {
            FrontWarsTerrainMap map = _game.Map;
            int rangeSquared = MirvRange * MirvRange;

            for (int n = 0; n < RandomLandAttempts; n++)
            {
                int x = _random.NextInt(map.X(_dst) - MirvRange, map.X(_dst) + MirvRange);
                int y = _random.NextInt(map.Y(_dst) - MirvRange, map.Y(_dst) + MirvRange);

                if (map.IsValidCoord(x, y) == false)
                    continue;

                int tile = map.Ref(x, y);

                if (map.IsLand(tile) && map.EuclideanDistanceSquared(tile, _dst) <= rangeSquared && _game.Owner(tile) == _targetPlayer && TooClose(tile, buckets) == false)
                    return tile;
            }

            return -1;
        }

        private bool TooClose(int tile, Dictionary<int, List<int>> buckets)
        {
            int bx = (int)Math.Floor(_game.Map.X(tile) / (double)MinSpacing);
            int by = (int)Math.Floor(_game.Map.Y(tile) / (double)MinSpacing);

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (buckets.TryGetValue((bx + dx) * BucketFactor + (by + dy), out List<int> list) == false)
                        continue;

                    foreach (int t in list)
                    {
                        if (_game.Map.ManhattanDistance(tile, t) < MinSpacing)
                            return true;
                    }
                }
            }

            return false;
        }
    }
}
