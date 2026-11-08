using System;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 포탄 · SAM 미사일의 격자 직선 이동 — 한 번에 x 또는 y 로 한 칸 `[소스] Cq3gr8XO.js:ef=class{constructor(e,t){this.mg=e,this.random=t}`.
    ///
    /// <para>
    /// 확률 1/k (k = floor(1 + |dy| / (|dx| + 1))) 가 맞고 x 가 다르면 x 로, 아니면 y 로 한 칸.
    /// 난수는 매번 한 번 뽑는다. 움직일 곳이 없으면(같은 칸 · 같은 줄인데 x 이동 실패) 도착으로 친다.
    /// </para>
    /// </summary>
    public sealed class FrontWarsStraightPathFinder
    {
        private readonly FrontWarsTerrainMap _map;
        private readonly FrontWarsRandom _random;

        public FrontWarsStraightPathFinder(FrontWarsTerrainMap map, FrontWarsRandom random)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        /// <summary>다음 칸. 도착했으면 거짓(원본은 true 를 돌려준다) `[소스] nextTile(e,t){let n=this.mg.x(e)`.</summary>
        public bool TryNextTile(int from, int to, out int tile)
        {
            tile = FrontWarsPathStep.NoTile;

            int x = _map.X(from), y = _map.Y(from);
            int tx = _map.X(to), ty = _map.Y(to);

            if (x == tx && y == ty)
                return false;

            int nx = x, ny = y;
            double k = Math.Floor(1 + (double)Math.Abs(ty - y) / (Math.Abs(tx - x) + 1));

            if (_random.Chance(k) && x != tx)
            {
                if (x < tx)
                    nx++;
                else
                    nx--;
            }
            else
            {
                if (y < ty)
                    ny++;
                else if (y > ty)
                    ny--;
            }

            if (nx == x && ny == y)
                return false;

            tile = _map.Ref(nx, ny);
            return true;
        }
    }
}
