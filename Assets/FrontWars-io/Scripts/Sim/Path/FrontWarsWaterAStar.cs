using System;
using System.Collections.Generic;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 작은 지도 물길 단방향 A* — 무역선이 한 번에 끝까지 찾는다 `[소스] Cq3gr8XO.js:Jd=class{`.
    ///
    /// <para>
    /// 표시 배열을 도장 번호로 재사용한다. 우선순위 = 누적 비용 + 가중치 × 목표까지 맨해튼 거리(float).
    /// 이웃 순서 위 · 아래 · 왼쪽 · 오른쪽. 출발 칸에서는 건넘 비트 없이 네 방향 모두, 목표 칸으로는 비트 없이 들어간다.
    /// 물 비용 0 칸은 목표일 때만 들어가며 그때 비용은 칸 높이 비용이다. 반복이 한도에 닿으면 null.
    /// </para>
    /// </summary>
    public sealed class FrontWarsWaterAStar
    {
        private const int InitialHeapCapacity = 1 << 18;
        private const int NoPrevious = -1;

        private readonly FrontWarsPathMap _miniMap;
        private readonly uint[] _settledAt;
        private readonly uint[] _gSeenAt;
        private readonly uint[] _gBest;
        private readonly int[] _prev;
        private readonly int[] _candidates = new int[4];
        private readonly FrontWarsQuaternaryHeap _heap = new FrontWarsQuaternaryHeap(InitialHeapCapacity);
        private uint _stamp;

        public FrontWarsWaterAStar(FrontWarsPathMap miniMap)
        {
            _miniMap = miniMap ?? throw new ArgumentNullException(nameof(miniMap));
            int count = miniMap.Width * miniMap.Height;
            _settledAt = new uint[count];
            _gSeenAt = new uint[count];
            _gBest = new uint[count];
            _prev = new int[count];
        }

        public int[] FindPath(int src, int dst, double heuristicWeight, int maxIterations, byte[] cost, byte[] edges)
        {
            _stamp++;
            if (_stamp >= uint.MaxValue)
            {
                Array.Clear(_settledAt, 0, _settledAt.Length);
                Array.Clear(_gSeenAt, 0, _gSeenAt.Length);
                _stamp = 1;
            }

            uint stamp = _stamp;
            int w = _miniMap.Width;
            int lastRowStart = w * _miniMap.Height - w;
            int dstX = dst % w, dstY = dst / w;
            FrontWarsQuaternaryHeap heap = _heap;

            heap.Clear();
            _gBest[src] = 0;
            _gSeenAt[src] = stamp;
            _prev[src] = NoPrevious;
            heap.Push(src, 0);

            int budget = maxIterations;

            while (heap.IsEmpty == false)
            {
                if (--budget <= 0)
                    return null;

                int node = heap.Pop();
                if (_settledAt[node] == stamp)
                    continue;

                _settledAt[node] = stamp;
                if (node == dst)
                    return BuildPath(dst);

                uint g = _gBest[node];
                int x = node % w;
                int bits = edges[node];
                bool isSource = node == src;
                int count = 0;

                if (node >= w && (isSource || (bits & FrontWarsWaterGraph.EdgeUp) != 0 || node - w == dst))
                    _candidates[count++] = node - w;
                if (node < lastRowStart && (isSource || (bits & FrontWarsWaterGraph.EdgeDown) != 0 || node + w == dst))
                    _candidates[count++] = node + w;
                if (x != 0 && (isSource || (bits & FrontWarsWaterGraph.EdgeLeft) != 0 || node - 1 == dst))
                    _candidates[count++] = node - 1;
                if (x != w - 1 && (isSource || (bits & FrontWarsWaterGraph.EdgeRight) != 0 || node + 1 == dst))
                    _candidates[count++] = node + 1;

                for (int i = 0; i < count; i++)
                {
                    int next = _candidates[i];
                    if (_settledAt[next] == stamp)
                        continue;

                    int waterCost = cost[next];
                    if (waterCost == 0 && next != dst)
                        continue;

                    uint tentative = g + (uint)(waterCost == 0 ? _miniMap.Cost(next) : waterCost);
                    if (_gSeenAt[next] == stamp && tentative >= _gBest[next])
                        continue;

                    _prev[next] = node;
                    _gBest[next] = tentative;
                    _gSeenAt[next] = stamp;

                    int manhattan = Math.Abs(next % w - dstX) + Math.Abs(next / w - dstY);
                    heap.Push(next, tentative + heuristicWeight * manhattan);
                }
            }

            return null;
        }

        private int[] BuildPath(int dst)
        {
            var path = new List<int>();
            for (int node = dst; node != NoPrevious; node = _prev[node])
                path.Add(node);

            path.Reverse();
            return path.ToArray();
        }
    }
}
