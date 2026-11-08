using System;
using System.Collections.Generic;
using System.Linq;

namespace JinHyung.FrontWarsIo
{
    /// <summary>건설 화면이 한 칸에 대해 보여 줄 한 줄 `[소스] buildableUnits(e){`.</summary>
    public readonly struct FrontWarsBuildable
    {
        public readonly EFrontWarsUnit Type;

        /// <summary>지을 칸. 못 지으면 −1.</summary>
        public readonly int BuildTile;

        /// <summary>올릴 수 있는 근처 유닛 id. 없으면 −1.</summary>
        public readonly int UpgradeUnitId;

        public readonly long Cost;

        public FrontWarsBuildable(EFrontWarsUnit type, int buildTile, int upgradeUnitId, long cost)
        {
            Type = type;
            BuildTile = buildTile;
            UpgradeUnitId = upgradeUnitId;
            Cost = cost;
        }
    }

    /// <summary>플레이어의 건설 · 자리 · 업그레이드 부분 `[소스] Cq3gr8XO.js:Nm — buildUnit · canBuild · validStructureSpawnTiles · upgradeUnit`.</summary>
    public sealed partial class FrontWarsPlayer
    {
        /// <summary>건물 자리 반경 제곱 (15²) `[소스] euclideanDistSquared(e,n)<225`.</summary>
        private const int StructureAreaRadiusSquared = 225;

        /// <summary>근처 건물을 찾는 반경 `[소스] nearbyUnits(e,30,t)`.</summary>
        private const int StructureNeighborSearch = 30;

        public const int NoTile = -1;

        /// <summary>
        /// 그 종류를 그 칸 근처에 지을 수 있으면 실제로 쓸 칸, 아니면 −1 `[소스] canBuild(e,t,n=null){`.
        /// </summary>
        public int CanBuild(EFrontWarsUnit type, int tile, IReadOnlyList<int> validTiles = null)
        {
            if (_game.Rules.IsUnitDisabled(type))
                return NoTile;

            long cost = _game.Rules.UnitCost(type, this);

            if (IsAlive == false || Gold < cost)
                return NoTile;

            switch (type)
            {
                case EFrontWarsUnit.MIRV:
                    return _game.HasOwner(tile) ? NukeSpawn(tile) : NoTile;
                case EFrontWarsUnit.AtomBomb:
                case EFrontWarsUnit.HydrogenBomb:
                    return NukeSpawn(tile);
                case EFrontWarsUnit.MIRVWarhead:
                case EFrontWarsUnit.Shell:
                case EFrontWarsUnit.SAMMissile:
                    return tile;
                case EFrontWarsUnit.Port:
                    return PortSpawn(tile, validTiles);
                case EFrontWarsUnit.Warship:
                    return WarshipSpawn(tile);
                case EFrontWarsUnit.TransportShip:
                    return FrontWarsTransportShipRules.CanBuildTransportShip(_game, this, tile);
                case EFrontWarsUnit.TradeShip:
                    return TradeShipSpawn(tile);
                case EFrontWarsUnit.Train:
                    return _game.Map.IsLand(tile) ? tile : NoTile;
                default:
                    return LandBasedStructureSpawn(tile, validTiles);
            }
        }

        /// <summary>같은 팀 땅이 아니면, 쿨다운 아닌 사일로 중 목표에 가장 가까운 사일로 칸 `[소스] nukeSpawn(e){`.</summary>
        private int NukeSpawn(int tile)
        {
            FrontWarsPlayer owner = _game.Owner(tile);

            if (owner != null && IsOnSameTeam(owner))
                return NoTile;

            FrontWarsUnit silo = Units(EFrontWarsUnit.MissileSilo)
                .Where(u => u.IsInCooldown == false)
                .OrderBy(u => _game.Map.ManhattanDistance(u.Tile, tile))
                .FirstOrDefault();

            return silo?.Tile ?? NoTile;
        }

        /// <summary>
        /// 반경 20(맨해튼) 안 내 바다 해안 칸 중 가까운 순으로, 건물 자리 규칙을 통과한 첫 칸 `[소스] portSpawn(e,t){`.
        /// </summary>
        private int PortSpawn(int tile, IReadOnlyList<int> validTiles)
        {
            int radius = _game.Settings.Config.PortSpawnRadius;
            List<int> shores = _game.Bfs(tile, t => _game.Map.ManhattanDistance(tile, t) <= radius)
                .Where(t => _game.OwnerSmallId(t) == SmallId && _game.Map.IsOceanShore(t))
                .OrderBy(t => _game.Map.ManhattanDistance(t, tile))
                .ToList();

            var valid = new HashSet<int>(validTiles ?? ValidStructureSpawnTiles(tile));

            foreach (int t in shores)
            {
                if (valid.Contains(t))
                    return t;
            }

            return NoTile;
        }

        /// <summary>바다 칸이면 가장 가까운 항구 칸 `[소스] warshipSpawn(e){`.</summary>
        private int WarshipSpawn(int tile)
        {
            if (_game.Map.IsOcean(tile) == false)
                return NoTile;

            FrontWarsUnit port = Units(EFrontWarsUnit.Port).OrderBy(u => _game.Map.ManhattanDistance(u.Tile, tile)).FirstOrDefault();
            return port?.Tile ?? NoTile;
        }

        private int TradeShipSpawn(int tile)
        {
            FrontWarsUnit port = Units(EFrontWarsUnit.Port).FirstOrDefault(u => u.Tile == tile);
            return port?.Tile ?? NoTile;
        }

        private int LandBasedStructureSpawn(int tile, IReadOnlyList<int> validTiles)
        {
            IReadOnlyList<int> tiles = validTiles ?? ValidStructureSpawnTiles(tile);
            return tiles.Count != 0 ? tiles[0] : NoTile;
        }

        /// <summary>
        /// 건물을 놓을 수 있는 칸 — 가까운 순 `[소스] validStructureSpawnTiles(e){`.
        /// <list type="number">
        /// <item>그 칸이 내 땅이 아니면 없음.</item>
        /// <item>그 칸에서 반경 15(유클리드) 안 내 칸을 넓이 탐색으로 모은다.</item>
        /// <item>땅에 묶인 건물(반경 30 안)과 15칸 미만인 칸을 뺀다.</item>
        /// <item>그 칸까지 거리 제곱 오름차순 (같으면 찾은 순서).</item>
        /// </list>
        /// </summary>
        public List<int> ValidStructureSpawnTiles(int tile)
        {
            if (_game.OwnerSmallId(tile) != SmallId)
                return new List<int>();

            var boundTypes = new List<EFrontWarsUnit>();

            foreach (EFrontWarsUnit t in Enum.GetValues(typeof(EFrontWarsUnit)))
            {
                if (_game.Settings.Unit(t).TerritoryBound)
                    boundTypes.Add(t);
            }

            var nearby = _game.Units.NearbyUnits(tile, StructureNeighborSearch, boundTypes);
            List<int> area = _game.Bfs(tile, t => _game.Map.EuclideanDistanceSquared(tile, t) < StructureAreaRadiusSquared && _game.OwnerSmallId(t) == SmallId);
            int minDistance = _game.Settings.Config.StructureMinDistance;
            int minSquared = minDistance * minDistance;
            var kept = new List<int>(area.Count);

            foreach (int t in area)
            {
                bool tooClose = false;

                foreach (var (unit, _) in nearby)
                {
                    if (_game.Map.EuclideanDistanceSquared(unit.Tile, t) < minSquared)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (tooClose == false)
                    kept.Add(t);
            }

            return kept.OrderBy(t => _game.Map.EuclideanDistanceSquared(t, tile)).ToList();
        }

        /// <summary>
        /// 유닛을 만든다 — 비용 골드 · 실은 병력을 빼고 격자에 넣는다 `[소스] buildUnit(e,t,n){`.
        /// </summary>
        public FrontWarsUnit BuildUnit(EFrontWarsUnit type, int tile, FrontWarsUnitParams p = null)
        {
            if (_game.Rules.IsUnitDisabled(type))
                throw new InvalidOperationException($"Attempted to build disabled unit {type} at tile {tile} by player {Name}");

            p ??= new FrontWarsUnitParams();
            long cost = _game.Rules.UnitCost(type, this);
            var unit = new FrontWarsUnit(type, _game, tile, _game.NextUnitId(), this, p);
            AddUnitRef(unit);
            RecordUnitConstructed(type);
            RemoveGold(cost);
            RemoveTroops(p.Troops);
            _game.RegisterUnit(unit);
            return unit;
        }

        /// <summary>반경 15 안 그 종류 중 가장 가까운 유닛이 올릴 수 있으면 그것 `[소스] findUnitToUpgrade(e,t){`.</summary>
        public FrontWarsUnit FindUnitToUpgrade(EFrontWarsUnit type, int tile)
        {
            var nearby = _game.Units.NearbyUnits(tile, _game.Settings.Config.StructureMinDistance, new[] { type })
                .OrderBy(x => x.DistSquared)
                .ToList();

            if (nearby.Count == 0)
                return null;

            FrontWarsUnit unit = nearby[0].Unit;
            return CanUpgradeUnit(unit.Type) ? unit : null;
        }

        public bool CanUpgradeUnit(EFrontWarsUnit type)
        {
            return _game.Settings.Unit(type).Upgradable && _game.Rules.IsUnitDisabled(type) == false && Gold >= _game.Rules.UnitCost(type, this);
        }

        public bool CanUpgradeNow()
        {
            return Ticks - LastUpgradeTick >= _game.Settings.Config.UpgradeCooldownTicks;
        }

        /// <summary>비용을 내고 레벨 +1 `[소스] upgradeUnit(e){`.</summary>
        public void UpgradeUnit(FrontWarsUnit unit)
        {
            long cost = _game.Rules.UnitCost(unit.Type, this);
            RemoveGold(cost);
            unit.IncreaseLevel();
            RecordUnitConstructed(unit.Type);
            LastUpgradeTick = Ticks;
        }

        /// <summary>건설 메뉴 목록 — 스폰 단계 · 칸 없음이면 전부 못 짓는다 `[소스] buildableUnits(e){`.</summary>
        public List<FrontWarsBuildable> BuildableUnits(int tile)
        {
            List<int> valid = tile < 0 ? new List<int>() : ValidStructureSpawnTiles(tile);
            var result = new List<FrontWarsBuildable>();

            foreach (EFrontWarsUnit type in Enum.GetValues(typeof(EFrontWarsUnit)))
            {
                int upgrade = -1;

                if (_game.InSpawnPhase == false && tile >= 0)
                {
                    FrontWarsUnit u = FindUnitToUpgrade(type, tile);
                    if (u != null)
                        upgrade = u.Id;
                }

                int build = _game.InSpawnPhase || tile < 0 ? NoTile : CanBuild(type, tile, valid);
                result.Add(new FrontWarsBuildable(type, build, upgrade, _game.Rules.UnitCost(type, this)));
            }

            return result;
        }
    }
}
