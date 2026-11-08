using System;
using System.Collections.Generic;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 기차역 그래프 — 역끼리 철도로 이어진 이웃, 비용 1, 늘 지나갈 수 있다 `[소스] Cq3gr8XO.js:gd=class{constructor(e){this.game=e}`.
    ///
    /// <para>
    /// 역 타입은 철도 쪽이 가진다. 여기서는 두 가지만 빌린다:
    /// 역의 칸(`tile()`), 이웃 역 목록(`neighbors()` — 역의 철도 집합을 넣은 순서대로 돌며 반대쪽 역).
    /// </para>
    /// </summary>
    public sealed class FrontWarsStationGraph<TStation> : IFrontWarsAStarGraph<TStation>
        where TStation : class
    {
        private readonly FrontWarsTerrainMap _map;
        private readonly Func<TStation, int> _tileOf;
        private readonly Action<TStation, List<TStation>> _neighborsInto;

        /// <param name="tileOf">역 → 칸</param>
        /// <param name="neighborsInto">역 → 이웃 역을 목록 뒤에 넣는다(원본 순서). 목록은 이미 비어 있다.</param>
        public FrontWarsStationGraph(FrontWarsTerrainMap map, Func<TStation, int> tileOf, Action<TStation, List<TStation>> neighborsInto)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _tileOf = tileOf ?? throw new ArgumentNullException(nameof(tileOf));
            _neighborsInto = neighborsInto ?? throw new ArgumentNullException(nameof(neighborsInto));
        }

        public int NeighborsInto(TStation node, List<TStation> into)
        {
            into.Clear();
            _neighborsInto(node, into);
            return into.Count;
        }

        public double Cost(TStation node)
        {
            return 1;
        }

        public bool IsTraversable(TStation from, TStation to)
        {
            return true;
        }

        public int PosX(TStation node)
        {
            return _map.X(_tileOf(node));
        }

        public int PosY(TStation node)
        {
            return _map.Y(_tileOf(node));
        }
    }
}
