using System;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 게임 하나의 길찾기 공유 상태 — 원본이 지도 · 게임을 키로 WeakMap 에 걸어 두던 것들을 한 곳에 모았다.
    ///
    /// <list type="bullet">
    /// <item>물길 그래프 `[소스] Cq3gr8XO.js:const Od=new WeakMap` · `function kd(e){` · `function jd(e,t){`</item>
    /// <item>큰 지도 물길 다듬기 `[소스] const Fd=new WeakMap`</item>
    /// <item>무역선 동기 물길 `[소스] const Xd=new WeakMap` · `function Qd(e,t){`</item>
    /// <item>배 길 캐시 `[소스] const tf=new WeakMap` · `function nf(e){`</item>
    /// </list>
    ///
    /// <para>
    /// 게임을 만들 때 하나 만든다. 원본은 게임 생성 직후 물길 그래프를 한 번 만든다(`rh(e,t,n,r,i){ … kd(a)`) — 생성자가 그대로 한다.
    /// 땅이 물로 바뀌면 <see cref="ApplyWaterConversion"/> 를 부른다(원본 `convertToWater` 안의 `Ad(this._map,e)`).
    /// </para>
    ///
    /// <para>
    /// 참고: 원본 상륙 출발 해안 고르기는 게임 객체를 지도 자리에 넘겨 물길 그래프를 따로 하나 더 만든다(키가 달라서).
    /// 두 그래프는 같은 칸 데이터에서 같은 규칙으로 만들어 내용이 같으므로 여기서는 하나를 같이 쓴다.
    /// </para>
    /// </summary>
    public sealed class FrontWarsPathCache
    {
        private readonly FrontWarsWaterGraph _waterGraph;
        private FrontWarsWaterPathRefiner _refiner;
        private FrontWarsWaterSyncPathFinder _waterSync;
        private FrontWarsRouteCache _routeCache;

        /// <param name="map">큰 지도(게임 지도).</param>
        /// <param name="miniMap">작은 지도. 게임 지도가 이미 작은 지도(Compact)면 같은 객체를 넘긴다.</param>
        public FrontWarsPathCache(FrontWarsPathMap map, FrontWarsPathMap miniMap)
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));
            MiniMap = miniMap ?? throw new ArgumentNullException(nameof(miniMap));

            _waterGraph = new FrontWarsWaterGraph(map, miniMap);
            _waterGraph.EnsureFresh();
        }

        public FrontWarsPathMap Map { get; }

        public FrontWarsPathMap MiniMap { get; }

        /// <summary>물길 그래프 — 부를 때마다 물 변환 버전을 맞춘다 `[소스] n.ensureFresh(),n`.</summary>
        public FrontWarsWaterGraph WaterGraph()
        {
            _waterGraph.EnsureFresh();
            return _waterGraph;
        }

        /// <summary>큰 지도 한 칸이 물로 바뀐 직후 부른다 `[소스] function Ad(e,t){Od.get(e)?.applyConversion(t)}`.</summary>
        public void ApplyWaterConversion(int tile)
        {
            _waterGraph.ApplyConversion(tile);
        }

        public FrontWarsWaterPathRefiner Refiner
        {
            get { return _refiner ??= new FrontWarsWaterPathRefiner(Map); }
        }

        /// <summary>무역선 동기 물길 — 처음 부를 때 만든다.</summary>
        public FrontWarsWaterSyncPathFinder WaterSync
        {
            get { return _waterSync ??= new FrontWarsWaterSyncPathFinder(Map, MiniMap, _waterGraph, Refiner); }
        }

        public FrontWarsRouteCache RouteCache
        {
            get { return _routeCache ??= new FrontWarsRouteCache(); }
        }
    }
}
