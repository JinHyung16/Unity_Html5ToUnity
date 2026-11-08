using System.Collections.Generic;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 길 찾기 묶음 구현 — <c>Sim/Path/</c> 이식본(원본 대조 15/15)을 실행이 쓰는 모양으로 잇는다.
    /// <para>게임당 하나. 만들 때 물길 그래프를 바로 만든다 (원본 kd(e) 와 같은 시점). 축소 맵 판이면 두 지도가 같다.</para>
    /// </summary>
    public sealed class FrontWarsPathFactory : IFrontWarsPathFactory
    {
        private readonly FrontWarsGame _game;
        private readonly FrontWarsPathCache _cache;
        private readonly FrontWarsRailPathService _rail;
        private readonly FrontWarsStationGraph<FrontWarsTrainStation> _stationGraph;

        public FrontWarsPathFactory(FrontWarsGame game)
        {
            _game = game;

            var map = new FrontWarsPathMap(game.Map, game.Settings.WaterNukes ? game.Map : null);
            FrontWarsPathMap mini = game.MiniMap == null || game.MiniMap == game.Map
                ? map
                : new FrontWarsPathMap(game.MiniMap, game.Settings.WaterNukes ? game.MiniMap : null);

            _cache = new FrontWarsPathCache(map, mini);
            _rail = new FrontWarsRailPathService(_cache);
            _stationGraph = new FrontWarsStationGraph<FrontWarsTrainStation>(game.Map, s => s.Tile, (s, into) => into.AddRange(s.Neighbors()));
        }

        public IFrontWarsUnitPathFinder Mini(int maxTries, bool waterPath = true, int x = 20)
        {
            return new UnitPathAdapter(FrontWarsUnitPathFinder.Mini(_cache, maxTries, waterPath, x));
        }

        public IFrontWarsUnitPathFinder MiniWaterSync(bool useCache = true, int x = 4, int maxNodes = 250000)
        {
            return new UnitPathAdapter(FrontWarsUnitPathFinder.MiniWaterSync(_cache, useCache, x, maxNodes));
        }

        public IFrontWarsParabolaPath Parabola()
        {
            return new ParabolaAdapter(new FrontWarsParabolaPathFinder(_game.Map));
        }

        public bool ComputeRoute(IReadOnlyList<int> sources, int destination, int maxTries, int x, out int routeStartTile)
        {
            var search = new FrontWarsMiniPathSearch(_cache, sources, destination, maxTries, x);

            if (search.Compute() != EFrontWarsPathStatus.Completed)
            {
                routeStartTile = -1;
                return false;
            }

            if (search.TryGetRouteStartTile(out int tile) == false)
                tile = -1;

            routeStartTile = tile;
            return true;
        }

        public List<int> FindRailTilePath(int from, int to)
        {
            return new List<int>(_rail.FindTilePath(from, to));
        }

        public List<FrontWarsTrainStation> FindStationsPath(FrontWarsTrainStation from, FrontWarsTrainStation to)
        {
            return _rail.FindStationsPath(from, to, _stationGraph);
        }

        public void ApplyWaterConversion(int tile)
        {
            _cache.ApplyWaterConversion(tile);
        }

        private sealed class UnitPathAdapter : IFrontWarsUnitPathFinder
        {
            private readonly FrontWarsUnitPathFinder _inner;

            public UnitPathAdapter(FrontWarsUnitPathFinder inner)
            {
                _inner = inner;
            }

            public FrontWarsPathStep NextTile(int from, int to, int arriveDistance = 1)
            {
                return _inner.NextTile(from, to, arriveDistance);
            }

            public void Reset()
            {
                _inner.Reset();
            }
        }

        private sealed class ParabolaAdapter : IFrontWarsParabolaPath
        {
            private readonly FrontWarsParabolaPathFinder _inner;

            public ParabolaAdapter(FrontWarsParabolaPathFinder inner)
            {
                _inner = inner;
            }

            public void ComputeControlPoints(int src, int dst, int speed = 3, bool distanceBasedHeight = true)
            {
                _inner.ComputeControlPoints(src, dst, speed, distanceBasedHeight);
            }

            public int NextTile(int speed, out bool arrived)
            {
                arrived = _inner.TryNextTile(speed, out int tile) == false;
                return tile;
            }

            public int CurrentIndex
            {
                get { return _inner.CurrentIndex(); }
            }

            public IReadOnlyList<int> AllTiles()
            {
                return _inner.AllTiles();
            }
        }
    }
}
