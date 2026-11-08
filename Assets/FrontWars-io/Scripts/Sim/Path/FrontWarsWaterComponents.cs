using System;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 작은 지도 물 덩어리 번호 — 물길 그래프의 건넘 비트로 이어진 칸끼리 같은 번호 `[소스] Cq3gr8XO.js:qd=class{`.
    /// <para>번호는 1부터, 칸 번호 순서로 처음 만나는 덩어리부터 붙인다. 물 비용 0 인 칸은 -1.</para>
    /// </summary>
    public sealed class FrontWarsWaterComponents
    {
        public const int NotWater = -1;

        private const int Unlabeled = 0;
        private const int NotBuilt = -1;

        private readonly FrontWarsWaterGraph _graph;
        private readonly int _width;
        private readonly int _tileCount;
        private readonly int[] _ids;
        private int[] _queue;
        private int _builtForVersion = NotBuilt;

        public FrontWarsWaterComponents(FrontWarsPathMap miniMap, FrontWarsWaterGraph graph)
        {
            _graph = graph ?? throw new ArgumentNullException(nameof(graph));
            _width = miniMap.Width;
            _tileCount = miniMap.Width * miniMap.Height;
            _ids = new int[_tileCount];
        }

        /// <summary>그래프를 먼저 맞추고, 그래프 버전이 바뀌었으면 번호를 다시 붙인다 `[소스] ensureFresh(){this.graph.ensureFresh()`.</summary>
        public void EnsureFresh()
        {
            _graph.EnsureFresh();
            if (_graph.Version == _builtForVersion)
                return;

            _builtForVersion = _graph.Version;
            Rebuild();
        }

        public int ComponentAt(int miniTile)
        {
            return _ids[miniTile];
        }

        private void Rebuild()
        {
            int w = _width;
            int[] ids = _ids;
            _queue ??= new int[_tileCount];
            int[] queue = _queue;
            byte[] cost = _graph.Costs;
            byte[] edges = _graph.Edges;

            for (int t = 0; t < _tileCount; t++)
                ids[t] = cost[t] == 0 ? NotWater : Unlabeled;

            int label = 0;

            for (int start = 0; start < _tileCount; start++)
            {
                if (ids[start] != Unlabeled)
                    continue;

                label++;
                ids[start] = label;
                int head = 0, tail = 0;
                queue[tail++] = start;

                while (head < tail)
                {
                    int t = queue[head++];
                    int bits = edges[t];

                    if ((bits & FrontWarsWaterGraph.EdgeUp) != 0 && ids[t - w] == Unlabeled)
                    {
                        ids[t - w] = label;
                        queue[tail++] = t - w;
                    }

                    if ((bits & FrontWarsWaterGraph.EdgeDown) != 0 && ids[t + w] == Unlabeled)
                    {
                        ids[t + w] = label;
                        queue[tail++] = t + w;
                    }

                    if ((bits & FrontWarsWaterGraph.EdgeLeft) != 0 && ids[t - 1] == Unlabeled)
                    {
                        ids[t - 1] = label;
                        queue[tail++] = t - 1;
                    }

                    if ((bits & FrontWarsWaterGraph.EdgeRight) != 0 && ids[t + 1] == Unlabeled)
                    {
                        ids[t + 1] = label;
                        queue[tail++] = t + 1;
                    }
                }
            }
        }
    }
}
