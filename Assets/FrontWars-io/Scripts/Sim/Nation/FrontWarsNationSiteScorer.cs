using System;
using System.Collections.Generic;
using System.Linq;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 건물 자리 점수 `[소스] Cq3gr8XO.js:function ip(e,t,n,r={}){`. 만들 때 목록을 모아 두고 <see cref="Score"/> 로 칸마다 매긴다.
    ///
    /// <para>s = 원자폭탄 outer(30) · c = 2s(60). 「가장 가까운 X」 는 맨해튼 거리, 같으면 x 오름차순으로 먼저 본 칸 `[소스] function np( · function rp(`.</para>
    /// <list type="table">
    /// <item><term>도시 · 공장 · 사일로</term><description>높이 + min(국경, s) + min(같은 종류(자기 칸 제외), 한도) + min(짝 종류, c) + 철도 점수 × c. 한도 = 공장이면 역 최대 거리, 아니면 c</description></item>
    /// <item><term>항구</term><description>min(같은 종류, c)</description></item>
    /// <item><term>방어초소</term><description>높이 + max(0, s − |s − 국경 거리|) + 그 국경 칸 옆 플레이어마다 s × (우호 − 관계) + min(같은 종류, c)</description></item>
    /// <item><term>SAM</term><description>높이 + min(국경, s) + min(같은 종류, c) + 기본 SAM 사거리 안 건물마다 c × 가중치 (덮개 반영이면 ÷ (1 + 덮개))</description></item>
    /// </list>
    /// </summary>
    public sealed class FrontWarsNationSiteScorer
    {
        private readonly FrontWarsGame _game;
        private readonly FrontWarsPlayer _player;
        private readonly EFrontWarsUnit _type;
        private readonly List<int> _border;
        private readonly List<int> _sameType;
        private readonly List<int> _crossType;
        private readonly Func<int, double> _connectivity;
        private readonly int _spacing;
        private readonly int _spread;
        private readonly int _sameTypeLimit;
        private readonly List<(int Tile, double Weight, int Cover)> _samCovered;
        private readonly bool _samCoverageWeighting;
        private readonly int _defaultSamRangeSquared;

        /// <param name="crossTypeTiles">짝 종류 건물 칸 (도시 ↔ 공장). 없으면 null.</param>
        /// <param name="connectivity">철도 점수. 없으면 null.</param>
        public FrontWarsNationSiteScorer(FrontWarsGame game, FrontWarsPlayer player, EFrontWarsUnit type,
            IReadOnlyList<int> crossTypeTiles, Func<int, double> connectivity, bool levelWeightedSam, bool samCoverageWeighting)
        {
            _game = game;
            _player = player;
            _type = type;
            _border = SortByX(player.BorderTiles.Items);
            _sameType = SortByX(UniqueTiles(player.Units(type)));
            _spacing = FrontWarsNationTiles.NukeOuterRadius(EFrontWarsUnit.AtomBomb);
            _spread = _spacing * 2;

            switch (type)
            {
                case EFrontWarsUnit.City:
                case EFrontWarsUnit.Factory:
                case EFrontWarsUnit.MissileSilo:
                    _sameTypeLimit = type == EFrontWarsUnit.Factory ? game.Settings.Config.TrainStationMaxRange : _spread;
                    _crossType = crossTypeTiles == null ? null : SortByX(UniqueTiles(crossTypeTiles));
                    _connectivity = connectivity;
                    break;
                case EFrontWarsUnit.Port:
                case EFrontWarsUnit.DefensePost:
                    break;
                case EFrontWarsUnit.SAMLauncher:
                    _samCovered = CollectSamCoverage(levelWeightedSam, samCoverageWeighting);
                    _samCoverageWeighting = samCoverageWeighting;
                    int defaultRange = game.Rules.SamRange(1);
                    _defaultSamRangeSquared = defaultRange * defaultRange;
                    break;
                default:
                    throw new InvalidOperationException($"Value function not implemented for {type}");
            }
        }

        /// <summary>SAM 이 덮을 건물 — 도시 · 공장 · 사일로 · 항구, 가중치 = (레벨 가중이면 레벨, 아니면 1). 덮개 반영이면 내 SAM 사거리 안 레벨 합.</summary>
        private List<(int Tile, double Weight, int Cover)> CollectSamCoverage(bool levelWeighted, bool coverageWeighting)
        {
            var covered = new List<(int Tile, double Weight, int Cover)>();

            foreach (FrontWarsUnit unit in _player.AllUnits)
            {
                switch (unit.Type)
                {
                    case EFrontWarsUnit.City:
                    case EFrontWarsUnit.Factory:
                    case EFrontWarsUnit.MissileSilo:
                    case EFrontWarsUnit.Port:
                        covered.Add((unit.Tile, levelWeighted ? unit.Level : 1, 0));
                        break;
                }
            }

            if (coverageWeighting)
            {
                foreach (FrontWarsUnit sam in _player.Units(EFrontWarsUnit.SAMLauncher))
                {
                    int range = _game.Rules.SamRange(sam.Level);
                    int rangeSquared = range * range;

                    for (int i = 0; i < covered.Count; i++)
                    {
                        if (_game.Map.EuclideanDistanceSquared(covered[i].Tile, sam.Tile) <= rangeSquared)
                            covered[i] = (covered[i].Tile, covered[i].Weight, covered[i].Cover + sam.Level);
                    }
                }
            }

            return covered;
        }

        public double Score(int tile)
        {
            switch (_type)
            {
                case EFrontWarsUnit.Port:
                    return PortScore(tile);
                case EFrontWarsUnit.DefensePost:
                    return DefensePostScore(tile);
                case EFrontWarsUnit.SAMLauncher:
                    return SamScore(tile);
                default:
                    return EconomyScore(tile);
            }
        }

        private double EconomyScore(int tile)
        {
            FrontWarsTerrainMap map = _game.Map;
            double score = 0;
            score += map.Magnitude(tile);

            int border = Nearest(_border, tile, FrontWarsNationTiles.NoTile);
            if (border >= 0)
                score += Math.Min(map.ManhattanDistance(border, tile), _spacing);

            int same = Nearest(_sameType, tile, tile);
            if (same >= 0)
                score += Math.Min(map.ManhattanDistance(same, tile), _sameTypeLimit);

            if (_crossType != null)
            {
                int cross = Nearest(_crossType, tile, FrontWarsNationTiles.NoTile);
                if (cross >= 0)
                    score += Math.Min(map.ManhattanDistance(cross, tile), _spread);
            }

            if (_connectivity != null)
                score += _connectivity(tile) * _spread;

            return score;
        }

        private double PortScore(int tile)
        {
            double score = 0;

            int same = Nearest(_sameType, tile, tile);
            if (same >= 0)
                score += Math.Min(_game.Map.ManhattanDistance(same, tile), _spread);

            return score;
        }

        private double DefensePostScore(int tile)
        {
            FrontWarsTerrainMap map = _game.Map;
            double score = 0;
            score += map.Magnitude(tile);

            int border = Nearest(_border, tile, FrontWarsNationTiles.NoTile);

            if (border >= 0)
            {
                int d = map.ManhattanDistance(border, tile);
                score += Math.Max(0, _spacing - Math.Abs(_spacing - d));

                var seen = new HashSet<FrontWarsPlayer>();
                var others = new List<FrontWarsPlayer>();
                int[] scratch = new int[4];
                int n = map.NeighborsInto(border, scratch);

                for (int k = 0; k < n; k++)
                {
                    if (map.IsLand(scratch[k]) == false)
                        continue;

                    int ownerId = _game.OwnerSmallId(scratch[k]);
                    if (ownerId == _player.SmallId)
                        continue;

                    FrontWarsPlayer owner = _game.PlayerBySmallId(ownerId);
                    if (owner != null && seen.Add(owner))
                        others.Add(owner);
                }

                foreach (FrontWarsPlayer other in others)
                    score += _spacing * ((int)EFrontWarsRelation.Friendly - (int)_player.Relation(other));
            }

            int same = Nearest(_sameType, tile, tile);
            if (same >= 0)
                score += Math.Min(map.ManhattanDistance(same, tile), _spread);

            return score;
        }

        private double SamScore(int tile)
        {
            FrontWarsTerrainMap map = _game.Map;
            double score = map.Magnitude(tile);

            int border = Nearest(_border, tile, FrontWarsNationTiles.NoTile);
            if (border >= 0)
                score += Math.Min(map.ManhattanDistance(border, tile), _spacing);

            int same = Nearest(_sameType, tile, tile);
            if (same >= 0)
                score += Math.Min(map.ManhattanDistance(same, tile), _spread);

            foreach (var covered in _samCovered)
            {
                if (map.EuclideanDistanceSquared(tile, covered.Tile) > _defaultSamRangeSquared)
                    continue;

                double weight = _spread * covered.Weight;
                score += _samCoverageWeighting ? weight / (1 + covered.Cover) : weight;
            }

            return score;
        }

        /// <summary>맨해튼 거리가 가장 짧은 첫 칸 (exclude 칸은 건너뜀). 없으면 −1 `[소스] function np(e,t,n,r){`.</summary>
        private int Nearest(List<int> tiles, int tile, int exclude)
        {
            FrontWarsTerrainMap map = _game.Map;
            int x = map.X(tile), y = map.Y(tile);
            int best = FrontWarsNationTiles.NoTile;
            double bestDistance = double.PositiveInfinity;

            foreach (int t in tiles)
            {
                if (t == exclude)
                    continue;

                int d = Math.Abs(map.X(t) - x) + Math.Abs(map.Y(t) - y);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = t;
                }
            }

            return best;
        }

        /// <summary>x 오름차순 안정 정렬 `[소스] function rp(e,t){return Array.from(t).sort((t,n)=>e.x(t)-e.x(n))}`.</summary>
        private List<int> SortByX(IEnumerable<int> tiles)
        {
            FrontWarsTerrainMap map = _game.Map;
            return tiles.OrderBy(map.X).ToList();
        }

        /// <summary>원본 <c>new Set(…)</c> — 처음 나온 순서로 중복 제거.</summary>
        private static List<int> UniqueTiles(IEnumerable<int> tiles)
        {
            var seen = new HashSet<int>();
            var result = new List<int>();

            foreach (int t in tiles)
            {
                if (seen.Add(t))
                    result.Add(t);
            }

            return result;
        }

        private static List<int> UniqueTiles(IEnumerable<FrontWarsUnit> units)
        {
            return UniqueTiles(units.Select(u => u.Tile));
        }
    }
}
