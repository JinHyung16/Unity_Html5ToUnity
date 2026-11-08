using System;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 무역선용 한 번에 끝까지 찾는 물길 `[소스] Cq3gr8XO.js:Yd=class{`.
    ///
    /// <para>
    /// ① 출발 · 목표를 작은 칸으로 옮긴다 ② 두 칸(과 각 4방향 이웃)의 물 덩어리 번호가 하나도 겹치지 않으면 null
    /// ③ <see cref="FrontWarsWaterAStar"/> 로 작은 칸 길 ④ <see cref="FrontWarsWaterPathRefiner"/> 로 큰 지도 칸 길.
    /// </para>
    /// </summary>
    public sealed class FrontWarsWaterSyncPathFinder
    {
        /// <summary>자기 칸 + 4방향 이웃 = 덩어리 후보 최대 5개.</summary>
        private const int MaxComponentCandidates = 5;

        private readonly FrontWarsPathMap _map;
        private readonly FrontWarsPathMap _miniMap;
        private readonly int _scale;
        private readonly FrontWarsWaterGraph _graph;
        private readonly FrontWarsWaterComponents _components;
        private readonly FrontWarsWaterAStar _aStar;
        private readonly FrontWarsWaterPathRefiner _refiner;
        private readonly int[] _srcComponents = new int[MaxComponentCandidates];
        private readonly int[] _dstComponents = new int[MaxComponentCandidates];

        public FrontWarsWaterSyncPathFinder(FrontWarsPathMap map, FrontWarsPathMap miniMap, FrontWarsWaterGraph graph, FrontWarsWaterPathRefiner refiner)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _miniMap = miniMap ?? throw new ArgumentNullException(nameof(miniMap));
            _graph = graph ?? throw new ArgumentNullException(nameof(graph));
            _refiner = refiner ?? throw new ArgumentNullException(nameof(refiner));
            _scale = map.Width == miniMap.Width ? 1 : 2;
            _components = new FrontWarsWaterComponents(miniMap, graph);
            _aStar = new FrontWarsWaterAStar(miniMap);
        }

        /// <summary>큰 지도 칸 길(출발 · 목표 포함). 못 찾으면 null `[소스] findPath(e,t,n,r){let i=this.miniMap`.</summary>
        public int[] FindPath(int src, int dst, double heuristicWeight, int maxIterations)
        {
            int miniSrc = _miniMap.Ref(_map.X(src) / _scale, _map.Y(src) / _scale);
            int miniDst = _miniMap.Ref(_map.X(dst) / _scale, _map.Y(dst) / _scale);

            _components.EnsureFresh();
            if (SameWaterComponent(miniSrc, miniDst) == false)
                return null;

            int[] miniPath = _aStar.FindPath(miniSrc, miniDst, heuristicWeight, maxIterations, _graph.Costs, _graph.Edges);
            if (miniPath == null)
                return null;

            return _refiner.Refine(miniPath, _miniMap, _scale, src, dst);
        }

        private bool SameWaterComponent(int a, int b)
        {
            int srcCount = CollectComponents(a, _srcComponents);
            if (srcCount == 0)
                return false;

            int dstCount = CollectComponents(b, _dstComponents);
            if (dstCount == 0)
                return false;

            for (int i = 0; i < srcCount; i++)
            {
                for (int j = 0; j < dstCount; j++)
                {
                    if (_srcComponents[i] == _dstComponents[j])
                        return true;
                }
            }

            return false;
        }

        /// <summary>자기 칸 번호, 이어서 위 · 아래 · 왼쪽 · 오른쪽 이웃 번호(겹치면 뺀다) `[소스] collectComponents(e,t){`.</summary>
        private int CollectComponents(int miniTile, int[] into)
        {
            int count = 0;
            int own = _components.ComponentAt(miniTile);
            if (own != FrontWarsWaterComponents.NotWater)
                into[count++] = own;

            int w = _miniMap.Width;
            int total = w * _miniMap.Height;
            int x = miniTile % w;

            if (miniTile >= w)
                count = PushComponent(miniTile - w, into, count);
            if (miniTile < total - w)
                count = PushComponent(miniTile + w, into, count);
            if (x != 0)
                count = PushComponent(miniTile - 1, into, count);
            if (x != w - 1)
                count = PushComponent(miniTile + 1, into, count);

            return count;
        }

        private int PushComponent(int miniTile, int[] into, int count)
        {
            int id = _components.ComponentAt(miniTile);
            if (id == FrontWarsWaterComponents.NotWater)
                return count;

            for (int i = 0; i < count; i++)
            {
                if (into[i] == id)
                    return count;
            }

            into[count] = id;
            return count + 1;
        }
    }
}
