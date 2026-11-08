using System;

namespace JinHyung.FrontWarsIo
{
    /// <summary>지형 종류 — 원본 `[소스] Cq3gr8XO.js:terrainType(e){`.</summary>
    public enum EFrontWarsTerrain
    {
        Plains,
        Highland,
        Mountain,
        Ocean,
        Lake
    }

    /// <summary>
    /// 지형 칸 배열 — 칸 1개 = 1바이트. 순수 C# 이라 재생 없이 검사에서 읽는다.
    ///
    /// <para>
    /// 칸 바이트 `[소스] Cq3gr8XO.js:IS_LAND_BIT=7`: 비트 7 땅 · 비트 6 해안 · 비트 5 바다 · 비트 0~4 높이(0~31).
    /// 해안 비트는 원본이 파일에 두지 않고 불러올 때 계산한다 — 그래서 파일 값을 지우고 원본 식으로 다시 계산한다
    /// (OpenFront 파일에는 미리 켜져 있다 · 작업 기록 「지형 출처 대조」).
    /// </para>
    /// </summary>
    public sealed class FrontWarsTerrainMap : IFrontWarsWaterState
    {
        public const int LandBit = 7;
        public const int ShorelineBit = 6;
        public const int OceanBit = 5;
        public const int MagnitudeMask = 31;

        /// <summary>높이 < 10 평지 · < 20 고원 · 그 이상 산 `[소스] terrainType(e){`.</summary>
        public const int HighlandMagnitude = 10;
        public const int MountainMagnitude = 20;

        private readonly byte[] _tiles;

        public int Width { get; }
        public int Height { get; }

        public int TileCount
        {
            get { return _tiles.Length; }
        }

        public int LandTileCount { get; private set; }

        /// <summary>물로 바뀐 칸 · 해안으로 표시한 칸 — 핵(물 핵 설정)이 쓴다 `[소스] WATER_OVERRIDE_BIT=15 · SHORELINE_OVERRIDE_BIT=12`.</summary>
        private byte[] _overrides;

        private const byte WaterOverrideFlag = 1;
        private const byte ShorelineOverrideFlag = 2;

        /// <summary>물로 바뀔 때마다 오른다 — 길 찾기 캐시가 본다.</summary>
        public int WaterVersion { get; private set; }

        private byte Override(int tile)
        {
            return _overrides == null ? (byte)0 : _overrides[tile];
        }

        public bool IsConvertedToWater(int tile)
        {
            return (Override(tile) & WaterOverrideFlag) != 0;
        }

        public bool IsShorelineOverride(int tile)
        {
            return (Override(tile) & ShorelineOverrideFlag) != 0;
        }

        public bool IsLake(int tile)
        {
            return IsLand(tile) == false && IsOcean(tile) == false;
        }

        /// <summary>땅을 물로 바꾼다 (해안 표시는 지운다) `[소스] convertToWater(t){`.</summary>
        public void ConvertToWater(int tile)
        {
            if (IsLand(tile) == false)
                return;

            _overrides ??= new byte[_tiles.Length];
            _overrides[tile] = (byte)((_overrides[tile] & ~ShorelineOverrideFlag) | WaterOverrideFlag);
            LandTileCount--;
            WaterVersion++;
        }

        /// <summary>땅 칸을 해안으로 표시한다. 새로 표시했으면 참 `[소스] markShoreline(t){`.</summary>
        public bool MarkShoreline(int tile)
        {
            if (IsLand(tile) == false || (Override(tile) & ShorelineOverrideFlag) != 0)
                return false;

            _overrides ??= new byte[_tiles.Length];
            _overrides[tile] |= ShorelineOverrideFlag;
            return true;
        }

        private FrontWarsTerrainMap(int width, int height, byte[] tiles)
        {
            Width = width;
            Height = height;
            _tiles = tiles;

            int land = 0;
            for (int i = 0; i < tiles.Length; i++)
            {
                if ((tiles[i] & (1 << LandBit)) != 0)
                    land++;
            }

            LandTileCount = land;
        }

        /// <summary>
        /// 헤더 없는 칸 배열에서 만든다. 크기가 폭×높이와 다르면 멈춘다 `[소스] Terrain data length … doesn't match`.
        /// <para>입력 배열은 건드리지 않는다 — 복사해서 해안 비트를 지운 뒤 다시 계산한다.</para>
        /// </summary>
        public static FrontWarsTerrainMap FromRawTiles(byte[] raw, int width, int height)
        {
            if (raw == null)
                throw new ArgumentNullException(nameof(raw));

            if (raw.Length != width * height)
                throw new ArgumentException($"지형 데이터 {raw.Length} 바이트가 {width}x{height} 와 맞지 않는다");

            var tiles = new byte[raw.Length];
            byte clearShore = unchecked((byte)~(1 << ShorelineBit));

            for (int i = 0; i < raw.Length; i++)
                tiles[i] = (byte)(raw[i] & clearShore);

            ComputeShorelineBits(tiles, width, height);
            return new FrontWarsTerrainMap(width, height, tiles);
        }

        /// <summary>
        /// 해안 비트를 다시 계산한다 `[소스] function ig(e){`.
        /// <para>칸마다 4방향 이웃 중 땅/물이 자기와 다른 칸이 하나라도 있으면 켠다. 단 바이트가 정확히 159(지나갈 수 없는 산)면 끈다.</para>
        /// <para>원본은 앞 칸의 비트를 이미 바꾼 뒤에도 땅 비트(128)만 비교하므로 순서에 영향이 없다.</para>
        /// </summary>
        private static void ComputeShorelineBits(byte[] tiles, int width, int height)
        {
            int count = width * height;
            int lastRowStart = count - width;

            for (int t = 0; t < count; t++)
            {
                int b = tiles[t];
                int land = b & 128;
                bool shore = false;

                if (b != ImpassableLandByte)
                {
                    int x = t % width;
                    shore = (t >= width && (tiles[t - width] & 128) != land)
                            || (t < lastRowStart && (tiles[t + width] & 128) != land)
                            || (x != 0 && (tiles[t - 1] & 128) != land)
                            || (x != width - 1 && (tiles[t + 1] & 128) != land);
                }

                tiles[t] = (byte)(shore ? b | 64 : b & ~64);
            }
        }

        /// <summary>땅 + 높이 31 + 해안 아님 = 159. 원본이 이 바이트를 해안 계산 · 땅 수에서 뺀다.</summary>
        private const int ImpassableLandByte = 159;

        /// <summary>
        /// 승리 판정의 분모가 되는 땅 칸 수 `[소스] function ag(e){ r&128&&r!==159`.
        /// <para>해안 계산 뒤에 센다 — 해안인 산(223)은 들어가고, 안쪽 산(159)은 빠진다.</para>
        /// </summary>
        public int CountLandTilesForWin()
        {
            int n = 0;

            for (int i = 0; i < _tiles.Length; i++)
            {
                int b = _tiles[i];
                if ((b & 128) != 0 && b != ImpassableLandByte)
                    n++;
            }

            return n;
        }

        /// <summary>4방향 이웃 — 위 · 아래 · 왼쪽 · 오른쪽 순서 `[소스] neighborsInto(e,t){`. 채운 수를 돌려준다.</summary>
        public int NeighborsInto(int tile, int[] into)
        {
            int w = Width;
            int x = tile % w;
            int n = 0;

            if (tile >= w)
                into[n++] = tile - w;
            if (tile < (Height - 1) * w)
                into[n++] = tile + w;
            if (x != 0)
                into[n++] = tile - 1;
            if (x != w - 1)
                into[n++] = tile + 1;

            return n;
        }

        /// <summary>8방향 이웃 — dx −1..1 바깥, dy −1..1 안쪽 순서 `[소스] neighborsWithDiagInto(e,t){`.</summary>
        public int NeighborsWithDiagInto(int tile, int[] into)
        {
            int cx = X(tile), cy = Y(tile), n = 0;

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0)
                        continue;

                    int x = cx + dx, y = cy + dy;
                    if (x >= 0 && x < Width && y >= 0 && y < Height)
                        into[n++] = y * Width + x;
                }
            }

            return n;
        }

        public bool IsValidRef(int tile)
        {
            return tile >= 0 && tile < _tiles.Length;
        }

        public bool IsValidCoord(int x, int y)
        {
            return x >= 0 && x < Width && y >= 0 && y < Height;
        }

        public int ManhattanDistance(int a, int b)
        {
            return Math.Abs(X(a) - X(b)) + Math.Abs(Y(a) - Y(b));
        }

        public int EuclideanDistanceSquared(int a, int b)
        {
            int dx = X(a) - X(b), dy = Y(a) - Y(b);
            return dx * dx + dy * dy;
        }

        public bool IsWater(int tile)
        {
            return IsLand(tile) == false;
        }

        /// <summary>해안 칸 `[소스] isShoreline(t){` — 땅이면 비트만 보고, 물이면 4방향에 땅이 있어야 참.</summary>
        public bool IsShoreline(int tile)
        {
            byte o = Override(tile);

            if ((o & WaterOverrideFlag) != 0)
                return false;

            if ((o & ShorelineOverrideFlag) != 0)
                return true;

            if ((_tiles[tile] & (1 << ShorelineBit)) == 0)
                return false;

            if (IsLand(tile))
                return true;

            int w = Width, x = tile % w;
            return (tile >= w && IsLand(tile - w))
                   || (tile < (Height - 1) * w && IsLand(tile + w))
                   || (x != 0 && IsLand(tile - 1))
                   || (x != w - 1 && IsLand(tile + 1));
        }

        /// <summary>땅인 해안 `[소스] isShore(e){`.</summary>
        public bool IsShore(int tile)
        {
            return IsLand(tile) && IsShoreline(tile);
        }

        /// <summary>바다(호수 제외)와 맞닿은 땅 `[소스] isOceanShore(e){`.</summary>
        public bool IsOceanShore(int tile)
        {
            if (IsLand(tile) == false)
                return false;

            int w = Width, x = tile % w;
            return (tile >= w && IsOcean(tile - w))
                   || (tile < (Height - 1) * w && IsOcean(tile + w))
                   || (x != 0 && IsOcean(tile - 1))
                   || (x != w - 1 && IsOcean(tile + 1));
        }

        public int Ref(int x, int y)
        {
            return y * Width + x;
        }

        public int X(int tile)
        {
            return tile % Width;
        }

        public int Y(int tile)
        {
            return tile / Width;
        }

        public byte RawTile(int tile)
        {
            return _tiles[tile];
        }

        /// <summary>땅 비트 · 물로 안 바뀜 `[소스] isLand(t){`.</summary>
        public bool IsLand(int tile)
        {
            return (_tiles[tile] & (1 << LandBit)) != 0 && (Override(tile) & WaterOverrideFlag) == 0;
        }

        /// <summary>물로 바뀐 칸은 바다다 `[소스] isOcean(t){`.</summary>
        public bool IsOcean(int tile)
        {
            if ((Override(tile) & WaterOverrideFlag) != 0)
                return true;

            return (_tiles[tile] & (1 << OceanBit)) != 0;
        }

        public int Magnitude(int tile)
        {
            return _tiles[tile] & MagnitudeMask;
        }

        /// <summary>땅이고 높이가 31 이면 지나갈 수 없다 `[소스] isImpassable(`.</summary>
        public bool IsImpassable(int tile)
        {
            return IsLand(tile) && Magnitude(tile) == MagnitudeMask;
        }

        public EFrontWarsTerrain TerrainType(int tile)
        {
            if (IsLand(tile))
            {
                int magnitude = Magnitude(tile);
                if (magnitude < HighlandMagnitude)
                    return EFrontWarsTerrain.Plains;

                return magnitude < MountainMagnitude ? EFrontWarsTerrain.Highland : EFrontWarsTerrain.Mountain;
            }

            return IsOcean(tile) ? EFrontWarsTerrain.Ocean : EFrontWarsTerrain.Lake;
        }
    }
}
