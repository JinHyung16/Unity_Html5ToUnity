using System;
using System.Collections.Generic;
using System.Linq;

namespace JinHyung.FrontWarsIo
{
    /// <summary>철도 칸 모양 — 그리기용 `[소스] Cq3gr8XO.js:J=function(e){…VERTICAL=0…BOTTOM_RIGHT=5`.</summary>
    public enum EFrontWarsRailType
    {
        Vertical,
        Horizontal,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    public readonly struct FrontWarsRailTile
    {
        public readonly int Tile;
        public readonly EFrontWarsRailType Type;

        public FrontWarsRailTile(int tile, EFrontWarsRailType type)
        {
            Tile = tile;
            Type = type;
        }
    }

    /// <summary>철도 칸 모양 계산 `[소스] Cq3gr8XO.js:function Lm(e,t){ · function Fm(e,t,n,r){ · function Im(e,t,n){`.</summary>
    public static class FrontWarsRailTypes
    {
        public static List<FrontWarsRailTile> Compute(FrontWarsTerrainMap map, IReadOnlyList<int> tiles)
        {
            var result = new List<FrontWarsRailTile>(tiles.Count);

            if (tiles.Count == 0)
                return result;

            if (tiles.Count == 1)
            {
                result.Add(new FrontWarsRailTile(tiles[0], EFrontWarsRailType.Vertical));
                return result;
            }

            result.Add(new FrontWarsRailTile(tiles[0], End(map, tiles[0], tiles[1])));

            for (int i = 1; i < tiles.Count - 1; i++)
                result.Add(new FrontWarsRailTile(tiles[i], Middle(map, tiles[i - 1], tiles[i], tiles[i + 1])));

            int last = tiles.Count - 1;
            result.Add(new FrontWarsRailTile(tiles[last], End(map, tiles[last], tiles[last - 1])));
            return result;
        }

        private static EFrontWarsRailType End(FrontWarsTerrainMap map, int tile, int next)
        {
            int dx = map.X(next) - map.X(tile);
            int dy = map.Y(next) - map.Y(tile);

            if (dx == 0)
                return EFrontWarsRailType.Vertical;

            return dy == 0 ? EFrontWarsRailType.Horizontal : EFrontWarsRailType.Vertical;
        }

        private static EFrontWarsRailType Middle(FrontWarsTerrainMap map, int prev, int tile, int next)
        {
            int m = Math.Sign(map.X(tile) - map.X(prev));
            int h = Math.Sign(map.Y(tile) - map.Y(prev));
            int g = Math.Sign(map.X(next) - map.X(tile));
            int v = Math.Sign(map.Y(next) - map.Y(tile));

            if (m == g && h == v)
                return m == 0 ? EFrontWarsRailType.Vertical : EFrontWarsRailType.Horizontal;

            if (m == 0 && g != 0)
            {
                if (g == 1 && h == -1) return EFrontWarsRailType.BottomRight;
                if (g == -1 && h == -1) return EFrontWarsRailType.BottomLeft;
                if (g == 1 && h == 1) return EFrontWarsRailType.TopRight;
                if (g == -1 && h == 1) return EFrontWarsRailType.TopLeft;
                if (h == 0) return EFrontWarsRailType.Horizontal;
            }

            if (m != 0 && g == 0)
            {
                if (m == 1 && v == -1) return EFrontWarsRailType.TopLeft;
                if (m == -1 && v == -1) return EFrontWarsRailType.TopRight;
                if (m == 1 && v == 1) return EFrontWarsRailType.BottomLeft;
                if (m == -1 && v == 1) return EFrontWarsRailType.BottomRight;
                if (v == 0) return EFrontWarsRailType.Vertical;
            }

            if (m != 0 && h != 0 && g != 0 && v != 0)
            {
                int sx = m + g, sy = h + v;
                if (sx > 0 && sy < 0) return EFrontWarsRailType.TopRight;
                if (sx < 0 && sy < 0) return EFrontWarsRailType.TopLeft;
                if (sx > 0 && sy > 0) return EFrontWarsRailType.BottomRight;
                if (sx < 0 && sy > 0) return EFrontWarsRailType.BottomLeft;
            }

            if (m != 0 && h != 0 && g == 0 && v != 0)
            {
                if (m > 0) return v > 0 ? EFrontWarsRailType.BottomRight : EFrontWarsRailType.TopRight;
                if (m < 0) return v > 0 ? EFrontWarsRailType.BottomLeft : EFrontWarsRailType.TopLeft;
            }

            if (m == 0 && h != 0 && g != 0 && v != 0)
            {
                if (g > 0) return h > 0 ? EFrontWarsRailType.BottomRight : EFrontWarsRailType.TopRight;
                if (g < 0) return h > 0 ? EFrontWarsRailType.BottomLeft : EFrontWarsRailType.TopLeft;
            }

            if (m != 0 && h == 0 && g != 0 && v != 0)
            {
                if (v > 0) return m > 0 ? EFrontWarsRailType.BottomRight : EFrontWarsRailType.BottomLeft;
                if (v < 0) return m > 0 ? EFrontWarsRailType.TopRight : EFrontWarsRailType.TopLeft;
            }

            if (m != 0 && h != 0 && g != 0 && v == 0)
            {
                if (h > 0) return g > 0 ? EFrontWarsRailType.BottomRight : EFrontWarsRailType.BottomLeft;
                if (h < 0) return g > 0 ? EFrontWarsRailType.TopRight : EFrontWarsRailType.TopLeft;
            }

            return Math.Abs(m) + Math.Abs(g) > Math.Abs(h) + Math.Abs(v) ? EFrontWarsRailType.Horizontal : EFrontWarsRailType.Vertical;
        }
    }

    /// <summary>철도 한 줄 `[소스] Cq3gr8XO.js:yd=class{`.</summary>
    public sealed class FrontWarsRailroad
    {
        public FrontWarsTrainStation From { get; }
        public FrontWarsTrainStation To { get; }
        public List<int> Tiles { get; }
        public int Id { get; }
        public bool FullyConstructed { get; set; }

        public FrontWarsRailroad(FrontWarsTrainStation from, FrontWarsTrainStation to, List<int> tiles, int id)
        {
            From = from;
            To = to;
            Tiles = tiles;
            Id = id;
        }

        public void Delete(FrontWarsGame game)
        {
            game.RaiseRailRemoved(Id, Tiles);
            From.Railroads.Remove(this);
            To.Railroads.Remove(this);
        }

        /// <summary>그 칸에 가장 가까운 철도 칸 번호 (같으면 앞쪽).</summary>
        public int NearestTileIndex(FrontWarsTerrainMap map, int tile)
        {
            int x = map.X(tile), y = map.Y(tile), best = -1;
            double bestDist = double.PositiveInfinity;

            for (int i = 0; i < Tiles.Count; i++)
            {
                int dx = map.X(Tiles[i]) - x, dy = map.Y(Tiles[i]) - y;
                int d = dx * dx + dy * dy;

                if (d < bestDist)
                {
                    bestDist = d;
                    best = i;
                }
            }

            return best;
        }
    }

    /// <summary>역에서 다음 역으로 가는 방향 붙은 철도 `[소스] Cq3gr8XO.js:xd=class{ · function bd(e,t){`.</summary>
    public sealed class FrontWarsRailSegment
    {
        public FrontWarsRailroad Railroad { get; }
        public bool Forward { get; }

        public FrontWarsRailSegment(FrontWarsRailroad railroad, bool forward)
        {
            Railroad = railroad;
            Forward = forward;
        }

        /// <summary>그 번호 칸. 범위 밖이면 −1 (원본 undefined).</summary>
        public int GetTile(int index)
        {
            List<int> t = Railroad.Tiles;
            int i = Forward ? index : t.Count - 1 - index;
            return i >= 0 && i < t.Count ? t[i] : -1;
        }

        public int Length
        {
            get { return Railroad.Tiles.Count; }
        }

        /// <summary>from 역에서 to 역으로 이어진 철도 — from 역의 철도 중 to 가 끝인 첫 줄.</summary>
        public static FrontWarsRailSegment Between(FrontWarsTrainStation from, FrontWarsTrainStation to)
        {
            foreach (FrontWarsRailroad r in from.Railroads)
            {
                if (r.From == to)
                    return new FrontWarsRailSegment(r, false);

                if (r.To == to)
                    return new FrontWarsRailSegment(r, true);
            }

            return null;
        }
    }

    /// <summary>
    /// 기차역 — 도시 · 항구 · 공장에 붙는다. 기차가 서면 종류별로 골드를 준다 `[소스] Cq3gr8XO.js:hd=class{ · dd · fd · pd`.
    /// </summary>
    public sealed class FrontWarsTrainStation
    {
        private readonly FrontWarsGame _game;

        public FrontWarsUnit Unit { get; }
        public int Id { get; set; }

        /// <summary>넣은 순서의 철도 집합 (원본 Set).</summary>
        public List<FrontWarsRailroad> Railroads { get; } = new List<FrontWarsRailroad>();

        public FrontWarsTrainCluster Cluster { get; private set; }

        /// <summary>한 번도 묶음이 정해지지 않았다 (원본 undefined — null 과 다르게 판정된다).</summary>
        public bool ClusterUndefined { get; private set; } = true;

        public FrontWarsTrainStation(FrontWarsGame game, FrontWarsUnit unit)
        {
            _game = game;
            Unit = unit;
        }

        public int Tile
        {
            get { return Unit.Tile; }
        }

        public bool IsActive
        {
            get { return Unit.IsActive; }
        }

        public bool TradeAvailable(FrontWarsPlayer player)
        {
            FrontWarsPlayer owner = Unit.Owner;
            return player == owner || owner.CanTrade(player);
        }

        public void AddRailroad(FrontWarsRailroad railroad)
        {
            if (Railroads.Contains(railroad) == false)
                Railroads.Add(railroad);
        }

        public void ClearRailroads()
        {
            Railroads.Clear();
        }

        public List<FrontWarsTrainStation> Neighbors()
        {
            var result = new List<FrontWarsTrainStation>(Railroads.Count);

            foreach (FrontWarsRailroad r in Railroads)
                result.Add(r.From == this ? r.To : r.From);

            return result;
        }

        public void SetCluster(FrontWarsTrainCluster cluster)
        {
            Cluster = cluster;
            ClusterUndefined = false;
        }

        /// <summary>
        /// 기차가 섰다 — 역 주인과 기차 주인이 우호면 역 주인도, 기차 주인은 늘 골드를 받는다.
        /// 빠른 경제에서 도시 · 항구는 (레벨+1) 배.
        /// </summary>
        public void OnTrainStop(FrontWarsTrainExecution train)
        {
            EFrontWarsUnit type = Unit.Type;

            if (type != EFrontWarsUnit.City && type != EFrontWarsUnit.Port && type != EFrontWarsUnit.Factory)
                return;

            FrontWarsPlayer stationOwner = Unit.Owner;
            FrontWarsPlayer trainOwner = train.Owner;
            bool friendly = stationOwner.IsFriendly(trainOwner);
            EFrontWarsRelationKind kind = FrontWarsRelationKinds.Of(trainOwner, stationOwner);
            long gold;

            if (_game.Settings.EconomyMode == EFrontWarsEconomyMode.Fast)
            {
                gold = _game.Rules.TrainGoldFast(kind);
                if (type != EFrontWarsUnit.Factory)
                    gold *= Unit.Level + 1;
            }
            else
            {
                gold = _game.Rules.TrainGold(kind);
            }

            if (friendly)
                stationOwner.AddGold(gold, Tile);

            trainOwner.AddGold(gold, Tile);
        }
    }

    /// <summary>
    /// 이어진 역 묶음 — 기차 목적지를 고른다 `[소스] Cq3gr8XO.js:_d=class{`.
    /// <para>목적지 후보는 도시 · 항구만, 역 주인별로 모아 (자기거나 교역 가능한 주인) 순서대로 센다. 틱마다 다시 모은다.</para>
    /// </summary>
    public sealed class FrontWarsTrainCluster
    {
        private int _groupsTick = -1;
        private readonly List<(FrontWarsPlayer Owner, List<FrontWarsTrainStation> Stations)> _ownerGroups = new List<(FrontWarsPlayer, List<FrontWarsTrainStation>)>();
        private readonly Dictionary<FrontWarsPlayer, (int Total, List<List<FrontWarsTrainStation>> Groups)> _allowedCache = new Dictionary<FrontWarsPlayer, (int, List<List<FrontWarsTrainStation>>)>();

        /// <summary>넣은 순서의 역 집합.</summary>
        public List<FrontWarsTrainStation> Stations { get; } = new List<FrontWarsTrainStation>();

        public bool Has(FrontWarsTrainStation s)
        {
            return Stations.Contains(s);
        }

        public void AddStation(FrontWarsTrainStation s)
        {
            if (Stations.Contains(s) == false)
                Stations.Add(s);

            s.SetCluster(this);
        }

        public void RemoveStation(FrontWarsTrainStation s)
        {
            Stations.Remove(s);
        }

        public void AddStations(IEnumerable<FrontWarsTrainStation> stations)
        {
            foreach (FrontWarsTrainStation s in stations)
                AddStation(s);
        }

        public void Merge(FrontWarsTrainCluster other)
        {
            foreach (FrontWarsTrainStation s in other.Stations.ToList())
                AddStation(s);
        }

        private void EnsureGroups(int tick)
        {
            if (_groupsTick == tick)
                return;

            _groupsTick = tick;
            _ownerGroups.Clear();
            _allowedCache.Clear();

            foreach (FrontWarsTrainStation s in Stations)
            {
                EFrontWarsUnit type = s.Unit.Type;
                if (type != EFrontWarsUnit.City && type != EFrontWarsUnit.Port)
                    continue;

                FrontWarsPlayer owner = s.Unit.Owner;
                int index = _ownerGroups.FindIndex(g => g.Owner == owner);

                if (index < 0)
                    _ownerGroups.Add((owner, new List<FrontWarsTrainStation> { s }));
                else
                    _ownerGroups[index].Stations.Add(s);
            }
        }

        private (int Total, List<List<FrontWarsTrainStation>> Groups) AllowedFor(FrontWarsPlayer player, int tick)
        {
            EnsureGroups(tick);

            if (_allowedCache.TryGetValue(player, out var cached))
                return cached;

            var groups = new List<List<FrontWarsTrainStation>>();
            int total = 0;

            foreach (var (owner, stations) in _ownerGroups)
            {
                if (owner == player || owner.CanTrade(player))
                {
                    groups.Add(stations);
                    total += stations.Count;
                }
            }

            cached = (total, groups);
            _allowedCache[player] = cached;
            return cached;
        }

        public int TradeStationCount(FrontWarsPlayer player, int tick)
        {
            return AllowedFor(player, tick).Total;
        }

        public FrontWarsTrainStation TradeStationAt(FrontWarsPlayer player, int tick, int index)
        {
            int i = index;

            foreach (List<FrontWarsTrainStation> group in AllowedFor(player, tick).Groups)
            {
                if (i < group.Count)
                    return group[i];

                i -= group.Count;
            }

            return null;
        }

        public int Size
        {
            get { return Stations.Count; }
        }

        public void Clear()
        {
            Stations.Clear();
        }
    }

    /// <summary>철도 칸 격자 — 4칸 단위 칸에 지나가는 철도를 둔다 `[소스] Cq3gr8XO.js:zm=class{`.</summary>
    public sealed class FrontWarsRailGrid
    {
        private readonly FrontWarsTerrainMap _map;
        private readonly int _cellSize;
        private readonly Dictionary<int, List<FrontWarsRailroad>> _cells = new Dictionary<int, List<FrontWarsRailroad>>();
        private readonly Dictionary<FrontWarsRailroad, List<int>> _cellsOfRail = new Dictionary<FrontWarsRailroad, List<int>>();

        public FrontWarsRailGrid(FrontWarsTerrainMap map, int cellSize)
        {
            _map = map;
            _cellSize = cellSize;
        }

        public void Register(FrontWarsRailroad rail)
        {
            Unregister(rail);
            var keys = new List<int>();
            int last = -1;

            foreach (int tile in rail.Tiles)
            {
                int key = (_map.Y(tile) / _cellSize) << 16 | (_map.X(tile) / _cellSize);

                if (key == last || keys.Contains(key))
                    continue;

                last = key;
                keys.Add(key);

                if (_cells.TryGetValue(key, out List<FrontWarsRailroad> list))
                    list.Add(rail);
                else
                    _cells[key] = new List<FrontWarsRailroad> { rail };
            }

            if (keys.Count > 0)
                _cellsOfRail[rail] = keys;
        }

        public void Unregister(FrontWarsRailroad rail)
        {
            if (_cellsOfRail.TryGetValue(rail, out List<int> keys) == false)
                return;

            foreach (int key in keys)
            {
                if (_cells.TryGetValue(key, out List<FrontWarsRailroad> list) == false)
                    continue;

                int i = list.IndexOf(rail);
                if (i >= 0)
                {
                    list[i] = list[list.Count - 1];
                    list.RemoveAt(list.Count - 1);
                }

                if (list.Count == 0)
                    _cells.Remove(key);
            }

            _cellsOfRail.Remove(rail);
        }

        /// <summary>반경 안 칸을 지나는 철도 — 처음 만난 순서 (x 칸 바깥 · y 칸 안쪽).</summary>
        public List<FrontWarsRailroad> Query(int tile, int radius)
        {
            int x = _map.X(tile), y = _map.Y(tile);
            int x0 = (int)Math.Floor((x - radius) / (double)_cellSize);
            int x1 = (int)Math.Floor((x + radius) / (double)_cellSize);
            int y0 = (int)Math.Floor((y - radius) / (double)_cellSize);
            int y1 = (int)Math.Floor((y + radius) / (double)_cellSize);
            var result = new List<FrontWarsRailroad>();
            var seen = new HashSet<FrontWarsRailroad>();

            for (int cx = x0; cx <= x1; cx++)
            {
                for (int cy = y0; cy <= y1; cy++)
                {
                    if (_cells.TryGetValue(cy << 16 | cx, out List<FrontWarsRailroad> list) == false)
                        continue;

                    foreach (FrontWarsRailroad r in list)
                    {
                        if (seen.Add(r))
                            result.Add(r);
                    }
                }
            }

            return result;
        }
    }

    /// <summary>
    /// 철도망 — 역 연결 · 끊기 · 묶음 다시 계산 `[소스] Cq3gr8XO.js:Um=class{ · Bm=class{`.
    /// </summary>
    public sealed class FrontWarsRailNetwork
    {
        private const int MaxConnectionDistance = 4;
        private const int MaxNewRailsPerStation = 3;
        private const int GridCellSize = 4;
        private const int StationRadius = 3;

        private static readonly EFrontWarsUnit[] StationTypes = { EFrontWarsUnit.City, EFrontWarsUnit.Factory, EFrontWarsUnit.Port };

        private readonly FrontWarsGame _game;
        private readonly FrontWarsRailGrid _railGrid;
        private readonly List<FrontWarsTrainStation> _stations = new List<FrontWarsTrainStation>();
        private readonly Dictionary<FrontWarsUnit, FrontWarsTrainStation> _byUnit = new Dictionary<FrontWarsUnit, FrontWarsTrainStation>();
        private readonly List<FrontWarsTrainCluster> _staleClusters = new List<FrontWarsTrainCluster>();
        private int _nextStationId = 1;
        private int _nextRailId = 1;

        public FrontWarsRailNetwork(FrontWarsGame game)
        {
            _game = game;
            _railGrid = new FrontWarsRailGrid(game.Map, GridCellSize);
        }

        public IReadOnlyList<FrontWarsTrainStation> Stations
        {
            get { return _stations; }
        }

        public void ConnectStation(FrontWarsTrainStation station)
        {
            station.Id = _nextStationId++;
            _byUnit[station.Unit] = station;
            _stations.Add(station);

            if (TrySnapToRails(station) == false)
                ConnectToNearbyStations(station);
        }

        public FrontWarsTrainStation FindStation(FrontWarsUnit unit)
        {
            return _byUnit.TryGetValue(unit, out FrontWarsTrainStation s) ? s : null;
        }

        public void RemoveStation(FrontWarsUnit unit)
        {
            FrontWarsTrainStation station = FindStation(unit);
            if (station == null)
                return;

            DisconnectFromNetwork(station);
            _byUnit.Remove(unit);
            _stations.Remove(station);
            station.Unit.SetTrainStation(false);

            FrontWarsTrainCluster cluster = station.Cluster;
            if (cluster == null)
                return;

            cluster.RemoveStation(station);

            if (cluster.Size == 0)
            {
                DeleteCluster(cluster);
                _staleClusters.Remove(cluster);
                return;
            }

            if (_staleClusters.Contains(cluster) == false)
                _staleClusters.Add(cluster);
        }

        /// <summary>끊긴 묶음을 이어진 조각마다 새 묶음으로 나눈다 (첫 조각은 옛 묶음에 남는다) `[소스] recomputeClusters(){`.</summary>
        public void RecomputeClusters()
        {
            if (_staleClusters.Count == 0)
                return;

            foreach (FrontWarsTrainCluster stale in _staleClusters)
            {
                var remaining = new List<FrontWarsTrainStation>(stale.Stations);

                while (remaining.Count > 0)
                {
                    List<FrontWarsTrainStation> connected = ComputeCluster(remaining[0]);

                    foreach (FrontWarsTrainStation s in connected)
                        remaining.Remove(s);

                    if (remaining.Count > 0)
                        new FrontWarsTrainCluster().AddStations(connected);
                }
            }

            _staleClusters.Clear();
        }

        /// <summary>역 경로 `[소스] Vm.findStationsPath`.</summary>
        public List<FrontWarsTrainStation> FindStationsPath(FrontWarsTrainStation from, FrontWarsTrainStation to)
        {
            return _game.Paths.FindStationsPath(from, to);
        }

        /// <summary>철도 칸 경로 `[소스] Vm.findTilePath`.</summary>
        private List<int> FindTilePath(int from, int to)
        {
            return _game.Paths.FindRailTilePath(from, to);
        }

        public List<int> OverlappingRailroads(int tile)
        {
            var tiles = new HashSet<int>();

            foreach (FrontWarsRailroad r in _railGrid.Query(tile, StationRadius))
            {
                foreach (int t in r.Tiles)
                    tiles.Add(t);
            }

            return tiles.OrderBy(t => t).ToList();
        }

        public bool HasRailNear(int tile)
        {
            return _railGrid.Query(tile, StationRadius).Count > 0;
        }

        /// <summary>건물을 놓기 전에 깔릴 철도 미리보기 `[소스] previewRailPaths(e,t){`.</summary>
        public List<List<int>> PreviewRailPaths(EFrontWarsUnit type, int tile)
        {
            var paths = new List<List<int>>();

            if (type != EFrontWarsUnit.City && type != EFrontWarsUnit.Port && type != EFrontWarsUnit.Factory)
                return paths;

            if (HasRailNear(tile))
                return paths;

            int maxRange = _game.Settings.Config.TrainStationMaxRange;
            int minRangeSquared = _game.Settings.Config.TrainStationMinRange * _game.Settings.Config.TrainStationMinRange;
            int maxSize = _game.Settings.Config.RailroadMaxSize;

            if (type != EFrontWarsUnit.Factory && _game.HasUnitNearby(tile, maxRange, EFrontWarsUnit.Factory, null) == false)
                return paths;

            var nearby = _game.Units.NearbyUnits(tile, maxRange, StationTypes).OrderBy(n => n.DistSquared).ToList();
            var chosen = new List<FrontWarsTrainStation>();

            foreach (var (unit, distSquared) in nearby)
            {
                if (paths.Count >= MaxNewRailsPerStation)
                    break;

                if (distSquared <= minRangeSquared)
                    continue;

                FrontWarsTrainStation station = FindStation(unit);
                if (station == null)
                    continue;

                if (chosen.Any(c => DistanceFrom(station, c, MaxConnectionDistance - 1) != -1))
                    continue;

                List<int> path = FindTilePath(tile, station.Tile);

                if (path.Count > 0 && path.Count < maxSize)
                {
                    paths.Add(path);
                    chosen.Add(station);
                }
            }

            return paths;
        }

        private bool TrySnapToRails(FrontWarsTrainStation station)
        {
            var clusters = new List<FrontWarsTrainCluster>();

            foreach (FrontWarsRailroad rail in _railGrid.Query(station.Tile, StationRadius))
            {
                if (rail.FullyConstructed == false)
                    continue;

                int index = rail.NearestTileIndex(_game.Map, station.Tile);
                if (index <= 0 || index >= rail.Tiles.Count)
                    continue;

                FrontWarsTrainCluster cluster = SpliceRail(rail, station, index);
                if (cluster != null && clusters.Contains(cluster) == false)
                    clusters.Add(cluster);
            }

            if (clusters.Count > 1)
                MergeClusters(clusters);

            return clusters.Count != 0;
        }

        private FrontWarsTrainCluster SpliceRail(FrontWarsRailroad rail, FrontWarsTrainStation station, int index)
        {
            rail.From.Railroads.Remove(rail);
            rail.To.Railroads.Remove(rail);
            _railGrid.Unregister(rail);

            var head = new FrontWarsRailroad(rail.From, station, rail.Tiles.GetRange(0, index), _nextRailId++) { FullyConstructed = true };
            var tail = new FrontWarsRailroad(station, rail.To, rail.Tiles.GetRange(index, rail.Tiles.Count - index), _nextRailId++) { FullyConstructed = true };

            station.AddRailroad(head);
            station.AddRailroad(tail);
            rail.From.AddRailroad(head);
            rail.To.AddRailroad(tail);
            _railGrid.Register(head);
            _railGrid.Register(tail);
            _game.RaiseRailSplit(rail.Id, head, tail);

            FrontWarsTrainCluster cluster = rail.From.Cluster;
            cluster?.AddStation(station);
            return cluster;
        }

        private void ConnectToNearbyStations(FrontWarsTrainStation station)
        {
            var nearby = _game.Units.NearbyUnits(station.Tile, _game.Settings.Config.TrainStationMaxRange, StationTypes).OrderBy(n => n.DistSquared).ToList();
            var clusters = new List<FrontWarsTrainCluster>();
            int minRangeSquared = _game.Settings.Config.TrainStationMinRange * _game.Settings.Config.TrainStationMinRange;
            int made = 0;

            foreach (var (unit, distSquared) in nearby)
            {
                if (made >= MaxNewRailsPerStation)
                    break;

                if (unit == station.Unit)
                    continue;

                FrontWarsTrainStation other = FindStation(unit);
                if (other == null)
                    continue;

                int distance = DistanceFrom(other, station, MaxConnectionDistance);

                // 원본 null(묶음 지워짐) 과 undefined(한 번도 안 정해짐) 가 다르다
                bool otherNull = other.ClusterUndefined == false && other.Cluster == null;
                bool stationNull = station.ClusterUndefined == false && station.Cluster == null;
                bool either = otherNull || stationNull;

                if (otherNull && stationNull == false)
                    continue;

                bool farInGraph = distance > MaxConnectionDistance || distance == -1;
                bool farEnough = either || distSquared >= minRangeSquared;

                if (farInGraph && farEnough && Connect(station, other))
                {
                    made++;

                    if (otherNull)
                    {
                        var created = new FrontWarsTrainCluster();
                        created.AddStation(other);
                        created.AddStation(station);
                        if (clusters.Contains(created) == false)
                            clusters.Add(created);
                    }
                    else
                    {
                        other.Cluster?.AddStation(station);
                        if (other.Cluster != null && clusters.Contains(other.Cluster) == false)
                            clusters.Add(other.Cluster);
                    }
                }
            }

            if (clusters.Count > 1)
                MergeClusters(clusters);
            else if (clusters.Count == 0)
                new FrontWarsTrainCluster().AddStation(station);
        }

        private void DisconnectFromNetwork(FrontWarsTrainStation station)
        {
            foreach (FrontWarsRailroad r in station.Railroads.ToList())
            {
                _railGrid.Unregister(r);
                r.Delete(_game);
            }

            station.ClearRailroads();

            if (station.ClusterUndefined)
                throw new InvalidOperationException("train station cluster undefined (original crashes here too)");

            FrontWarsTrainCluster cluster = station.Cluster;
            if (cluster != null && cluster.Size == 1)
                DeleteCluster(cluster);
        }

        private static void DeleteCluster(FrontWarsTrainCluster cluster)
        {
            foreach (FrontWarsTrainStation s in cluster.Stations)
                s.SetCluster(null);

            cluster.Clear();
        }

        private bool Connect(FrontWarsTrainStation from, FrontWarsTrainStation to)
        {
            List<int> path = FindTilePath(from.Tile, to.Tile);

            if (path.Count > 0 && path.Count < _game.Settings.Config.RailroadMaxSize)
            {
                var rail = new FrontWarsRailroad(from, to, path, _nextRailId++);
                _game.AddExecution(new FrontWarsRailConstructionExecution(rail));
                from.AddRailroad(rail);
                to.AddRailroad(rail);
                _railGrid.Register(rail);
                return true;
            }

            return false;
        }

        /// <summary>역 그래프 거리 (max 를 넘으면 −1) `[소스] distanceFrom(e,t,n){`.</summary>
        private static int DistanceFrom(FrontWarsTrainStation start, FrontWarsTrainStation target, int max)
        {
            if (start == target)
                return 0;

            var visited = new HashSet<FrontWarsTrainStation>();
            var queue = new Queue<(FrontWarsTrainStation Station, int Distance)>();
            queue.Enqueue((start, 0));

            while (queue.Count > 0)
            {
                var (s, d) = queue.Dequeue();

                if (visited.Add(s) == false || d >= max)
                    continue;

                foreach (FrontWarsTrainStation n in s.Neighbors())
                {
                    if (n == target)
                        return d + 1;

                    if (visited.Contains(n) == false)
                        queue.Enqueue((n, d + 1));
                }
            }

            return -1;
        }

        private static List<FrontWarsTrainStation> ComputeCluster(FrontWarsTrainStation start)
        {
            var visited = new List<FrontWarsTrainStation>();
            var seen = new HashSet<FrontWarsTrainStation>();
            var queue = new Queue<FrontWarsTrainStation>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                FrontWarsTrainStation s = queue.Dequeue();

                if (seen.Add(s) == false)
                    continue;

                visited.Add(s);

                foreach (FrontWarsTrainStation n in s.Neighbors())
                {
                    if (seen.Contains(n) == false)
                        queue.Enqueue(n);
                }
            }

            return visited;
        }

        private static void MergeClusters(IEnumerable<FrontWarsTrainCluster> clusters)
        {
            var merged = new FrontWarsTrainCluster();

            foreach (FrontWarsTrainCluster c in clusters.ToList())
                merged.Merge(c);
        }
    }

    /// <summary>매 틱 철도 묶음 다시 계산 — 게임이 맨 처음 넣는 실행 `[소스] Cq3gr8XO.js:Pm=class{`.</summary>
    public sealed class FrontWarsRailRecomputeExecution : IFrontWarsExecution
    {
        private readonly FrontWarsRailNetwork _network;

        public bool IsActive => true;
        public bool ActiveDuringSpawnPhase => false;

        public FrontWarsRailRecomputeExecution(FrontWarsRailNetwork network)
        {
            _network = network;
        }

        public void Init(FrontWarsGame game, int tick)
        {
        }

        public void Tick(int tick)
        {
            _network.RecomputeClusters();
        }
    }

    /// <summary>
    /// 철도 깔기 연출 — 양 끝에서 3칸씩 틱마다 알린다. 다 깔면 완성 표시 `[소스] Cq3gr8XO.js:Rm=class{`.
    /// </summary>
    public sealed class FrontWarsRailConstructionExecution : IFrontWarsExecution
    {
        private const int Increment = 3;

        private readonly FrontWarsRailroad _rail;
        private FrontWarsGame _game;
        private List<FrontWarsRailTile> _railTiles = new List<FrontWarsRailTile>();
        private int _head;
        private int _tail;

        public bool IsActive { get; private set; } = true;
        public bool ActiveDuringSpawnPhase => false;

        public FrontWarsRailConstructionExecution(FrontWarsRailroad rail)
        {
            _rail = rail;
            _tail = rail.Tiles.Count;
        }

        public void Init(FrontWarsGame game, int tick)
        {
            _game = game;
            _railTiles = FrontWarsRailTypes.Compute(game.Map, _rail.Tiles);
        }

        public void Tick(int tick)
        {
            if (_rail.From.IsActive == false || _rail.To.IsActive == false)
            {
                IsActive = false;
                return;
            }

            if (_head > _tail)
            {
                Complete();
                return;
            }

            List<FrontWarsRailTile> added;

            if (_tail - _head <= 2 * Increment)
            {
                added = _railTiles.GetRange(_head, _tail - _head);
                Complete();
            }
            else
            {
                added = _railTiles.GetRange(_head, Increment);
                added.AddRange(_railTiles.GetRange(_tail - Increment, Increment));
                _head += Increment;
                _tail -= Increment;
            }

            if (added.Count > 0)
                _game.RaiseRailAdded(_rail.Id, added);
        }

        private void Complete()
        {
            if (_rail.From.Unit.IsActive)
                _game.RaiseUnitUpdate(_rail.From.Unit);

            if (_rail.To.Unit.IsActive)
                _game.RaiseUnitUpdate(_rail.To.Unit);

            IsActive = false;
            _rail.FullyConstructed = true;
        }
    }

    /// <summary>
    /// 기차역 실행 — 역을 만들어 철도망에 잇고, 기차를 띄울 수 있는 역이면 10틱 간격으로 굴린다 `[소스] Cq3gr8XO.js:Cd=class{`.
    /// </summary>
    public sealed class FrontWarsTrainStationExecution : IFrontWarsExecution
    {
        private const int NumCars = 5;
        private const int SpawnCooldownTicks = 10;

        private readonly FrontWarsUnit _unit;
        private readonly bool _spawnTrains;
        private FrontWarsGame _game;
        private FrontWarsRandom _random;
        private FrontWarsTrainStation _station;
        private int _lastSpawnTick;

        public bool IsActive { get; private set; } = true;
        public bool ActiveDuringSpawnPhase => false;

        /// <summary>⚠ 만들 때 바로 유닛에 역 표시를 켠다 (원본 생성자).</summary>
        public FrontWarsTrainStationExecution(FrontWarsUnit unit, bool spawnTrains = false)
        {
            _unit = unit;
            _spawnTrains = spawnTrains;
            _unit.SetTrainStation(true);
        }

        /// <summary>도시 · 항구 — 반경 안에 (누구의 것이든) 공장이 있으면 역 실행을 넣는다 `[소스] createStation(){`.</summary>
        public static void CreateStationIfFactoryNearby(FrontWarsGame game, FrontWarsUnit unit)
        {
            if (game.HasUnitNearby(unit.Tile, game.Settings.Config.TrainStationMaxRange, EFrontWarsUnit.Factory, null))
                game.AddExecution(new FrontWarsTrainStationExecution(unit));
        }

        public void Init(FrontWarsGame game, int tick)
        {
            _game = game;

            if (_spawnTrains)
                _random = new FrontWarsRandom(game.Ticks);
        }

        public void Tick(int tick)
        {
            if (IsActive == false || _unit == null)
                return;

            if (_station == null)
            {
                _station = new FrontWarsTrainStation(_game, _unit);
                _game.RailNetwork.ConnectStation(_station);
            }

            if (_station.IsActive == false)
            {
                IsActive = false;
                return;
            }

            SpawnTrain(tick);
        }

        private bool ShouldSpawnTrain(int tradeStations)
        {
            int rate = _game.Settings.EconomyMode == EFrontWarsEconomyMode.Fast
                ? _game.Rules.TrainSpawnRateFast(tradeStations)
                : _game.Rules.TrainSpawnRate(_unit.Owner.UnitCount(EFrontWarsUnit.Factory));

            for (int i = 0; i < _unit.Level; i++)
            {
                if (_random.Chance(rate))
                    return true;
            }

            return false;
        }

        private void SpawnTrain(int tick)
        {
            if (_spawnTrains == false || tick < _lastSpawnTick + SpawnCooldownTicks)
                return;

            FrontWarsTrainCluster cluster = _station.Cluster;
            if (cluster == null)
                return;

            int count = cluster.TradeStationCount(_unit.Owner, tick);
            if (count == 0 || ShouldSpawnTrain(count) == false)
                return;

            FrontWarsTrainStation destination = cluster.TradeStationAt(_unit.Owner, tick, _random.NextInt(0, count));

            if (destination != null && destination != _station)
            {
                _game.AddExecution(new FrontWarsTrainExecution(_game.RailNetwork, _unit.Owner, _station, destination, NumCars));
                _lastSpawnTick = tick;
            }
        }
    }

    /// <summary>기차 칸 종류 `[소스] Cq3gr8XO.js:C=function(e){return e.Engine=`Engine`,e.Carriage=`Carriage`…`.</summary>
    public static class FrontWarsTrainTypes
    {
        public const string Engine = "Engine";
        public const string Carriage = "Carriage";
    }

    /// <summary>
    /// 기차 — 역 경로를 따라 틱마다 2칸, 칸 사이 2칸 간격으로 객차가 따라온다. 역에 설 때마다 골드 `[소스] Cq3gr8XO.js:Sd=class{`.
    /// </summary>
    public sealed class FrontWarsTrainExecution : IFrontWarsExecution
    {
        private const int Spacing = 2;
        private const int Speed = 2;

        private readonly FrontWarsRailNetwork _network;
        private readonly FrontWarsTrainStation _source;
        private readonly FrontWarsTrainStation _destination;
        private readonly int _numCars;
        private readonly List<FrontWarsUnit> _cars = new List<FrontWarsUnit>();
        private readonly List<int> _usedTiles = new List<int>();
        private List<FrontWarsTrainStation> _stations = new List<FrontWarsTrainStation>();
        private FrontWarsUnit _train;
        private FrontWarsRailSegment _currentRailroad;
        private bool _hasCargo;
        private int _currentTile;

        public FrontWarsPlayer Owner { get; }
        public bool IsActive { get; private set; } = true;
        public bool ActiveDuringSpawnPhase => false;

        public FrontWarsTrainExecution(FrontWarsRailNetwork network, FrontWarsPlayer player, FrontWarsTrainStation source, FrontWarsTrainStation destination, int numCars)
        {
            _network = network;
            Owner = player;
            _source = source;
            _destination = destination;
            _numCars = numCars;
        }

        public void Init(FrontWarsGame game, int tick)
        {
            List<FrontWarsTrainStation> path = _network.FindStationsPath(_source, _destination);

            if (path == null || path.Count <= 1)
            {
                IsActive = false;
                return;
            }

            _stations = path;
            _currentRailroad = FrontWarsRailSegment.Between(_stations[0], _stations[1]);

            if (_currentRailroad == null)
            {
                IsActive = false;
                return;
            }

            int spawn = Owner.CanBuild(EFrontWarsUnit.Train, _stations[0].Tile);

            if (spawn == FrontWarsPlayer.NoTile)
            {
                IsActive = false;
                return;
            }

            _train = CreateTrainUnits(spawn);
        }

        public void Tick(int tick)
        {
            if (_train.IsActive == false || ActiveSourceOrDestination() == false)
            {
                DeleteTrain();
                return;
            }

            int next = GetNextTile();

            // 원본 `t?` — 칸 0 도 거짓이다
            if (next > 0)
            {
                UpdateCarsPositions(next);
            }
            else
            {
                TargetReached();
                DeleteTrain();
            }
        }

        private FrontWarsUnit CreateTrainUnits(int tile)
        {
            FrontWarsUnit engine = Owner.BuildUnit(EFrontWarsUnit.Train, tile, new FrontWarsUnitParams { TargetUnit = _destination.Unit, TrainType = FrontWarsTrainTypes.Engine });
            _cars.Add(Owner.BuildUnit(EFrontWarsUnit.Train, tile, new FrontWarsUnitParams { TargetUnit = _destination.Unit, TrainType = FrontWarsTrainTypes.Engine }));

            for (int i = 0; i < _numCars; i++)
                _cars.Add(Owner.BuildUnit(EFrontWarsUnit.Train, tile, new FrontWarsUnitParams { TrainType = FrontWarsTrainTypes.Carriage, Loaded = _hasCargo }));

            return engine;
        }

        public void LoadCargo()
        {
            if (_hasCargo || _train == null)
                return;

            _hasCargo = true;

            for (int i = 1; i < _cars.Count; i++)
                _cars[i].SetLoaded(true);
        }

        private void TargetReached()
        {
            _train.SetReachedTarget();

            foreach (FrontWarsUnit car in _cars)
                car.SetReachedTarget();
        }

        private void DeleteTrain()
        {
            IsActive = false;

            if (_train != null && _train.IsActive)
                _train.Delete(false);

            foreach (FrontWarsUnit car in _cars)
            {
                if (car.IsActive)
                    car.Delete(false);
            }
        }

        private bool ActiveSourceOrDestination()
        {
            return _stations.Count > 1 && _stations[1].IsActive && _stations[0].IsActive;
        }

        private void SaveTraversedTiles(int start, int count)
        {
            if (_currentRailroad == null)
                return;

            int n = start;

            for (int i = 0; i < count && n < _currentRailroad.Length; i++)
            {
                SaveTile(_currentRailroad.GetTile(n));
                n++;
            }
        }

        private void SaveTile(int tile)
        {
            _usedTiles.Add(tile);

            if (_usedTiles.Count > _cars.Count * Spacing + 3)
                _usedTiles.RemoveAt(0);
        }

        private void UpdateCarsPositions(int tile)
        {
            for (int i = _cars.Count - 1; i >= 0; i--)
            {
                int index = (i + 1) * Spacing + 2;

                if (_usedTiles.Count > index)
                    _cars[i].Move(_usedTiles[index]);
            }

            _train.Move(tile);
        }

        private bool NextStation()
        {
            if (_stations.Count > 2)
            {
                _stations.RemoveAt(0);
                FrontWarsRailSegment segment = FrontWarsRailSegment.Between(_stations[0], _stations[1]);

                if (segment != null)
                {
                    _currentRailroad = segment;
                    return true;
                }
            }

            return false;
        }

        private bool CanTradeWithDestination()
        {
            return _stations.Count > 1 && _stations[1].TradeAvailable(Owner);
        }

        private int GetNextTile()
        {
            if (_currentRailroad == null || CanTradeWithDestination() == false)
                return -1;

            SaveTraversedTiles(_currentTile, Speed);
            _currentTile += Speed;
            int overflow = _currentTile - _currentRailroad.Length;

            if (overflow >= 0)
            {
                _stations[1].OnTrainStop(this);

                if (NextStation() == false)
                    return -1;

                _currentTile = overflow;
                SaveTraversedTiles(0, overflow);
            }

            return _currentRailroad.GetTile(_currentTile);
        }
    }
}
