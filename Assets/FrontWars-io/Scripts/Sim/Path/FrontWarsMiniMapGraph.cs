using System.Collections.Generic;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 작은 지도 칸 그래프 — 배(물길)와 철도(땅길) 두 가지로 쓴다 `[소스] Cq3gr8XO.js:Bd=class{`.
    ///
    /// <para>
    /// 물길: 비용 = 물길 그래프의 작은 칸 물 비용(0 이면 칸 높이 비용). 건넘 = 물길 그래프 경계 비트,
    /// 또는 출발 · 목표 칸에서 물 비용이 있는 칸으로 나가는 경우.
    /// </para>
    /// <para>
    /// 땅길: 비용 = 칸 높이 비용 + 물이면 3. 건넘 = 들어갈 칸이 땅이거나, 두 칸 중 하나가 해안.
    /// </para>
    /// </summary>
    public sealed class FrontWarsMiniMapGraph : IFrontWarsAStarGraph<int>
    {
        /// <summary>땅길에서 물 칸에 더하는 비용 `[소스] this.waterPenalty=3`.</summary>
        public const int WaterPenalty = 3;

        private readonly FrontWarsPathMap _miniMap;
        private readonly bool _waterPath;
        private readonly int[] _endpoints;
        private readonly byte[] _edges;
        private readonly byte[] _waterCost;
        private readonly int[] _neighborBuffer = new int[4];

        public FrontWarsMiniMapGraph(FrontWarsPathMap miniMap, bool waterPath, FrontWarsWaterGraph waterGraph, int[] endpoints)
        {
            _miniMap = miniMap;
            _waterPath = waterPath;
            _endpoints = endpoints ?? new int[0];
            _edges = waterGraph?.Edges;
            _waterCost = waterGraph?.Costs;
        }

        public int NeighborsInto(int node, List<int> into)
        {
            into.Clear();
            int n = _miniMap.NeighborsInto(node, _neighborBuffer);
            for (int i = 0; i < n; i++)
                into.Add(_neighborBuffer[i]);

            return n;
        }

        public double Cost(int node)
        {
            if (_waterCost != null)
            {
                int water = _waterCost[node];
                if (water != 0)
                    return water;
            }

            int cost = _miniMap.Cost(node);
            if (_waterPath == false && _miniMap.IsWater(node))
                cost += WaterPenalty;

            return cost;
        }

        public int PosX(int node)
        {
            return _miniMap.X(node);
        }

        public int PosY(int node)
        {
            return _miniMap.Y(node);
        }

        private bool IsEndpoint(int node)
        {
            for (int i = 0; i < _endpoints.Length; i++)
            {
                if (_endpoints[i] == node)
                    return true;
            }

            return false;
        }

        public bool IsTraversable(int from, int to)
        {
            if (_waterPath)
            {
                if (_edges == null)
                    return _miniMap.IsWater(to);

                return (_edges[from] & EdgeBitToward(from, to)) != 0
                       || (IsEndpoint(from) && _waterCost[to] != 0);
            }

            return _miniMap.IsWater(to) == false || _miniMap.IsShoreline(from) || _miniMap.IsShoreline(to);
        }

        /// <summary>이웃 방향의 경계 비트 `[소스] function Vd(e,t){` — 한 칸 왼쪽 4 · 오른쪽 8 · 위 1 · 아래 2.</summary>
        private static int EdgeBitToward(int from, int to)
        {
            int diff = to - from;
            if (diff == -1)
                return FrontWarsWaterGraph.EdgeLeft;
            if (diff == 1)
                return FrontWarsWaterGraph.EdgeRight;

            return diff < 0 ? FrontWarsWaterGraph.EdgeUp : FrontWarsWaterGraph.EdgeDown;
        }
    }
}
