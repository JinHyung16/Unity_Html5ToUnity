using System;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 길찾기가 보는 지도 — 지형 바이트 + (있으면) 물 변환 상태.
    ///
    /// <para>
    /// 원본 지도 클래스 `[소스] Cq3gr8XO.js:rd=class e{` 의 땅/물 · 해안 · 칸 비용 판정을 물 변환까지 반영해 옮겼다.
    /// 지형만 보는 <see cref="FrontWarsTerrainMap"/> 은 물 변환을 모르므로, 길찾기는 이 클래스를 거친다.
    /// </para>
    /// </summary>
    public sealed class FrontWarsPathMap
    {
        /// <summary>높이 10 미만(평지)은 비용 2, 그 이상은 1 `[소스] cost(e){return this.magnitude(e)<10?2:1}`.</summary>
        public const int LowMagnitudeCost = 2;
        public const int HighMagnitudeCost = 1;
        public const int CostMagnitudeThreshold = 10;

        private const int LandMask = 1 << FrontWarsTerrainMap.LandBit;
        private const int ShorelineMask = 1 << FrontWarsTerrainMap.ShorelineBit;

        private readonly FrontWarsTerrainMap _terrain;
        private readonly IFrontWarsWaterState _water;

        public FrontWarsPathMap(FrontWarsTerrainMap terrain, IFrontWarsWaterState water = null)
        {
            _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
            _water = water;
        }

        public FrontWarsTerrainMap Terrain
        {
            get { return _terrain; }
        }

        public int Width
        {
            get { return _terrain.Width; }
        }

        public int Height
        {
            get { return _terrain.Height; }
        }

        public int TileCount
        {
            get { return _terrain.TileCount; }
        }

        /// <summary>물 변환 버전. 상태가 없으면 늘 0 (원본 초기값).</summary>
        public int WaterVersion
        {
            get { return _water == null ? 0 : _water.WaterVersion; }
        }

        public int X(int tile)
        {
            return tile % _terrain.Width;
        }

        public int Y(int tile)
        {
            return tile / _terrain.Width;
        }

        public bool IsValidCoord(int x, int y)
        {
            return x >= 0 && x < _terrain.Width && y >= 0 && y < _terrain.Height;
        }

        /// <summary>좌표 → 칸. 지도 밖이면 멈춘다 `[소스] Invalid coordinates:`.</summary>
        public int Ref(int x, int y)
        {
            if (IsValidCoord(x, y) == false)
                throw new ArgumentException($"Invalid coordinates: {x},{y}");

            return y * _terrain.Width + x;
        }

        public int ManhattanDistance(int a, int b)
        {
            return _terrain.ManhattanDistance(a, b);
        }

        /// <summary>땅 비트가 켜져 있고 물로 바뀌지 않았으면 땅 `[소스] isLand(t){`.</summary>
        public bool IsLand(int tile)
        {
            if ((_terrain.RawTile(tile) & LandMask) == 0)
                return false;

            return _water == null || _water.IsConvertedToWater(tile) == false;
        }

        public bool IsWater(int tile)
        {
            return IsLand(tile) == false;
        }

        /// <summary>
        /// 해안 칸 `[소스] isShoreline(t){` — 물로 바뀐 칸은 아니다 → 해안 덮어쓰기면 맞다 → 해안 비트가 없으면 아니다
        /// → 땅이면 맞다 → 물이면 4방향(위 · 아래 · 왼쪽 · 오른쪽)에 땅이 하나라도 있어야 맞다.
        /// </summary>
        public bool IsShoreline(int tile)
        {
            if (_water != null)
            {
                if (_water.IsConvertedToWater(tile))
                    return false;

                if (_water.IsShorelineOverride(tile))
                    return true;
            }

            if ((_terrain.RawTile(tile) & ShorelineMask) == 0)
                return false;

            if (IsLand(tile))
                return true;

            int w = _terrain.Width, x = tile % w;
            return (tile >= w && IsLand(tile - w))
                   || (tile < (_terrain.Height - 1) * w && IsLand(tile + w))
                   || (x != 0 && IsLand(tile - 1))
                   || (x != w - 1 && IsLand(tile + 1));
        }

        /// <summary>칸 비용 — 물 변환과 무관하게 높이만 본다 `[소스] cost(e){return this.magnitude(e)<10?2:1}`.</summary>
        public int Cost(int tile)
        {
            return _terrain.Magnitude(tile) < CostMagnitudeThreshold ? LowMagnitudeCost : HighMagnitudeCost;
        }

        /// <summary>4방향 이웃 — 위 · 아래 · 왼쪽 · 오른쪽 순서.</summary>
        public int NeighborsInto(int tile, int[] into)
        {
            return _terrain.NeighborsInto(tile, into);
        }
    }
}
