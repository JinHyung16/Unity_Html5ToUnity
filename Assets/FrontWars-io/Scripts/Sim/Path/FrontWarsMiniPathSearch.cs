using System;
using System.Collections.Generic;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 작은 지도에서 먼저 찾고 큰 지도 칸 길로 펼치는 탐색 `[소스] Cq3gr8XO.js:var Hd=class{`.
    ///
    /// <para>
    /// 출발 · 목표를 작은 지도 칸(x/scale, y/scale)으로 옮겨 <see cref="FrontWarsBidirectionalAStar{TNode}"/> 로 찾는다.
    /// 물길이면 <see cref="FrontWarsWaterPathRefiner"/> 로 큰 지도 물 칸 길을 다시 찾고,
    /// 땅길(철도)이면 작은 칸 길을 scale 배로 늘려 사이를 반올림 직선으로 메운 뒤 출발 · 목표를 붙인다.
    /// </para>
    ///
    /// <para>
    /// 쓰는 곳: 배 길찾기 <see cref="FrontWarsUnitPathFinder.Mini"/> · 상륙 출발 해안 고르기(반복 5만 · 시도 1 → <see cref="TryGetRouteStartTile"/>)
    /// · 철도 칸 길(<see cref="FrontWarsRailPathService.FindTilePath"/>, 반복 5천 · 시도 20 · 땅길 · 방향 전환 벌점 3).
    /// </para>
    /// </summary>
    public sealed class FrontWarsMiniPathSearch
    {
        private readonly FrontWarsPathCache _cache;
        private readonly FrontWarsPathMap _map;
        private readonly FrontWarsPathMap _miniMap;
        private readonly int _src;
        private readonly bool _srcIsList;
        private readonly int _dst;
        private readonly bool _waterPath;
        private readonly int _scale;
        private readonly FrontWarsBidirectionalAStar<int> _aStar;

        /// <summary>출발 한 칸.</summary>
        public FrontWarsMiniPathSearch(FrontWarsPathCache cache, int src, int dst, int iterations, int maxTries, bool waterPath = true, int directionChangePenalty = 0)
            : this(cache, new[] { src }, false, dst, iterations, maxTries, waterPath, directionChangePenalty)
        {
        }

        /// <summary>출발 후보 여럿 — 어느 후보에서 떠날지는 탐색이 고른다(<see cref="TryGetRouteStartTile"/>).</summary>
        public FrontWarsMiniPathSearch(FrontWarsPathCache cache, IReadOnlyList<int> sources, int dst, int iterations, int maxTries, bool waterPath = true, int directionChangePenalty = 0)
            : this(cache, sources, true, dst, iterations, maxTries, waterPath, directionChangePenalty)
        {
        }

        private FrontWarsMiniPathSearch(FrontWarsPathCache cache, IReadOnlyList<int> sources, bool srcIsList, int dst, int iterations, int maxTries, bool waterPath, int directionChangePenalty)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _map = cache.Map;
            _miniMap = cache.MiniMap;
            _srcIsList = srcIsList;
            _src = srcIsList ? FrontWarsPathStep.NoTile : sources[0];
            _dst = dst;
            _waterPath = waterPath;
            _scale = _map.Width == _miniMap.Width ? 1 : 2;

            var miniSources = new int[sources.Count];
            for (int i = 0; i < sources.Count; i++)
                miniSources[i] = ToMini(sources[i]);

            int miniDst = ToMini(dst);

            FrontWarsWaterGraph waterGraph = null;
            int[] endpoints = null;

            if (waterPath)
            {
                waterGraph = cache.WaterGraph();
                endpoints = new int[miniSources.Length + 1];
                Array.Copy(miniSources, endpoints, miniSources.Length);
                endpoints[miniSources.Length] = miniDst;
            }

            var graph = new FrontWarsMiniMapGraph(_miniMap, waterPath, waterGraph, endpoints);
            _aStar = new FrontWarsBidirectionalAStar<int>(miniSources, miniDst, iterations, maxTries, graph, directionChangePenalty);
        }

        private int ToMini(int tile)
        {
            return _miniMap.Ref(_map.X(tile) / _scale, _map.Y(tile) / _scale);
        }

        public EFrontWarsPathStatus Compute()
        {
            return _aStar.Compute();
        }

        /// <summary>작은 칸 길의 첫 노드를 큰 지도 칸(블록 왼쪽 위)으로 `[소스] routeStartTile(){`. 아직 못 찾았으면 거짓.</summary>
        public bool TryGetRouteStartTile(out int tile)
        {
            tile = FrontWarsPathStep.NoTile;
            if (_aStar.TryGetFirstNode(out int node) == false)
                return false;

            tile = CoarseTile(node);
            return true;
        }

        private int CoarseTile(int miniTile)
        {
            return _map.Ref(_miniMap.X(miniTile) * _scale, _miniMap.Y(miniTile) * _scale);
        }

        /// <summary>큰 지도 칸 길(출발 · 목표 포함). 못 찾았으면 빈 배열 `[소스] reconstructPath(){let e=this.aStar.reconstructPath()`.</summary>
        public int[] ReconstructPath()
        {
            List<int> miniPath = _aStar.ReconstructPath();

            if (_waterPath)
            {
                if (miniPath.Count == 0)
                    return Array.Empty<int>();

                int start = _srcIsList ? CoarseTile(miniPath[0]) : _src;
                return _cache.Refiner.Refine(miniPath, _miniMap, _scale, start, _dst) ?? Array.Empty<int>();
            }

            var cells = new List<(int X, int Y)>(miniPath.Count);
            foreach (int node in miniPath)
                cells.Add((_miniMap.X(node), _miniMap.Y(node)));

            List<(int X, int Y)> upscaled = Upscale(cells, _scale);
            (int X, int Y)? srcCell = _srcIsList ? ((int X, int Y)?)null : (_map.X(_src), _map.Y(_src));
            List<(int X, int Y)> joined = AttachEndpoints(upscaled, (_map.X(_dst), _map.Y(_dst)), srcCell);

            var tiles = new int[joined.Count];
            for (int i = 0; i < joined.Count; i++)
                tiles[i] = _map.Ref(joined[i].X, joined[i].Y);

            return tiles;
        }

        /// <summary>
        /// 작은 칸 좌표를 scale 배로 늘리고 이웃 점 사이를 반올림 직선으로 메운다 `[소스] function Wd(e,t=2){`.
        /// </summary>
        private static List<(int X, int Y)> Upscale(List<(int X, int Y)> cells, int scale)
        {
            var result = new List<(int X, int Y)>(cells.Count * scale);

            for (int i = 0; i < cells.Count - 1; i++)
            {
                int ax = cells[i].X * scale, ay = cells[i].Y * scale;
                int bx = cells[i + 1].X * scale, by = cells[i + 1].Y * scale;
                result.Add((ax, ay));

                int dx = bx - ax, dy = by - ay;
                int steps = Math.Max(Math.Abs(dx), Math.Abs(dy));

                for (int k = 1; k < steps; k++)
                    result.Add((JsRound(ax + (double)(dx * k) / steps), JsRound(ay + (double)(dy * k) / steps)));
            }

            if (cells.Count > 0)
                result.Add((cells[cells.Count - 1].X * scale, cells[cells.Count - 1].Y * scale));

            return result;
        }

        /// <summary>
        /// 출발 칸이 길에 있으면 거기부터 자르고 없으면 앞에 붙인다. 목표 칸이 있으면 거기까지 자르고 없으면 뒤에 붙인다 `[소스] function Ud(e,t,n){`.
        /// </summary>
        private static List<(int X, int Y)> AttachEndpoints(List<(int X, int Y)> path, (int X, int Y) dst, (int X, int Y)? src)
        {
            if (src.HasValue)
            {
                int at = path.IndexOf(src.Value);
                if (at == -1)
                    path.Insert(0, src.Value);
                else if (at != 0)
                    path = path.GetRange(at, path.Count - at);
            }

            int end = path.IndexOf(dst);
            if (end == -1)
                path.Add(dst);
            else if (end != path.Count - 1)
                path = path.GetRange(0, end + 1);

            return path;
        }

        /// <summary>JS Math.round — 가장 가까운 정수, 딱 절반이면 +무한대 쪽.</summary>
        private static int JsRound(double value)
        {
            double floor = Math.Floor(value);
            return (int)(value - floor >= 0.5 ? floor + 1 : floor);
        }
    }
}
