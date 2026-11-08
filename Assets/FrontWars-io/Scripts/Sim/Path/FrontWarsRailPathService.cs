using System;
using System.Collections.Generic;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 철도 길찾기 창구 — 새 철도의 칸 길과 기차가 지날 역 순서 `[소스] Cq3gr8XO.js:Vm=class{constructor(e){this.game=e}findTilePath(e,t){`.
    /// </summary>
    public sealed class FrontWarsRailPathService
    {
        /// <summary>반복 5000 · 시도 20 · 방향 전환 벌점 3 `[소스] new Hd(this.game.map(),this.game.miniMap(),e,t,5e3,20,!1,3)`.</summary>
        public const int Iterations = 5000;
        public const int MaxTries = 20;
        public const int TileDirectionChangePenalty = 3;

        private readonly FrontWarsPathCache _cache;

        public FrontWarsRailPathService(FrontWarsPathCache cache)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        /// <summary>두 칸 사이 철도 칸 길(땅길). 한 번에 못 끝내면 빈 배열.</summary>
        public int[] FindTilePath(int from, int to)
        {
            var search = new FrontWarsMiniPathSearch(_cache, from, to, Iterations, MaxTries, false, TileDirectionChangePenalty);
            return search.Compute() == EFrontWarsPathStatus.Completed ? search.ReconstructPath() : Array.Empty<int>();
        }

        /// <summary>두 역 사이 역 순서(양 끝 포함). 한 번에 못 끝내면 빈 목록 `[소스] findStationsPath(e,t){let n=new zd(e,t,5e3,20,new gd(this.game))`.</summary>
        public List<TStation> FindStationsPath<TStation>(TStation from, TStation to, FrontWarsStationGraph<TStation> graph)
            where TStation : class
        {
            var search = new FrontWarsBidirectionalAStar<TStation>(from, to, Iterations, MaxTries, graph);
            return search.Compute() == EFrontWarsPathStatus.Completed ? search.ReconstructPath() : new List<TStation>();
        }
    }
}
