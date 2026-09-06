using System;
using System.Collections.Generic;
using JinHyung.Core;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace JinHyung.UndeadSlayer
{
    /// <summary>
    /// 지형. <b>원본은 24×24 world px 타일 격자</b>다 [실측 · Mesh 지오메트리 직독] —
    /// 정점이 24 간격 격자에 놓이고, 쿼드 수(1683)가 창 크기(51×33)와 정확히 같다.
    ///
    /// <para>
    /// ★ <b>그래서 Unity Tilemap 이 맞는 선택이다.</b> 회차 1 에 「Mesh 한 장이니 타일맵이 아니다」로
    /// 적었던 것은 <b>그리는 «방식»(배칭)을 «내용»으로 오인</b>한 것이다 (원장 오염 #2).
    /// </para>
    ///
    /// <para>
    /// ★ <b>셀 한 칸 = 1 유닛</b>이다 — <see cref="UndeadUnits.WorldPixelsPerUnit"/> 을 타일 크기(24)에
    /// 맞춰 뒀기 때문이다. 셀 크기를 만질 일이 없다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>맵 «전체» 데이터는 미측정</b>이다 — 원본이 절차 생성인지 저작된 맵인지 확인하지 않았다.
    /// 지금은 <b>카메라 둘레만</b> 채우고, 어느 칸에 어느 타일이 오는지는 <b>결정적 해시</b>로 정한다.
    /// 그 사실을 여기 적어 둔다 — 「원본과 같다」로 판정할 수 있는 부분이 아니다.
    /// </para>
    /// </summary>
    public sealed class UndeadTerrainView : MonoBehaviour
    {
        // ── 시드·주파수는 시뮬과 같은 곳(UndeadTerrainSeeds)에서 읽는다 — 그림과 판정이 같은 노이즈를 봐야 한다.

        /// <summary>길 능선 문턱 [소스 <c>au</c> — <c>(road+1)/2 &gt; .9</c>].</summary>
        private const double RoadThreshold = 0.9;

        /// <summary>풀밭 문턱 [소스 <c>au</c> — <c>(grass+1)/2 &lt; .32</c>]. ⚠ 회차 9 는 이 영역을 «길»로 오독했다.</summary>
        private const double GrassThreshold = 0.32;

        /// <summary>
        /// 타일셋 한 줄의 칸 순서. <b>빌더(굽기)·로더(자르기)·여기(고르기)가 같은 열거를 읽는다.</b>
        ///
        /// <para>
        /// ★★ <b>원본 <c>tileId</c> 를 «역할»로 옮긴 것이다</b> [소스 <c>au</c>·<c>ou</c>·<c>hu</c> 직독].
        /// 바이옴 1 이 실제로 쓰는 것은 <b>39종</b>이다 — 바닥 1 + 장식 10 · 풀 4 + 풀 가장자리 12 ·
        /// 길 4 + 길 가장자리 8. 주석의 숫자는 원본 <c>tileId</c> 다.
        /// </para>
        ///
        /// <para>
        /// ⚠ 예전 판은 25종만 두어 <b>길 가장자리 8종·풀 안쪽 모서리 4종·풀 변종 1·길 변종 1</b>을 잃었다.
        /// 잃은 종류는 «다른 타일로 대체»되므로 화면이 정상으로 보이고, 어떤 검사에도 안 걸린다.
        /// </para>
        /// </summary>
        public enum ETile
        {
            Ground,                                                                      // 3
            GroundDetail0, GroundDetail1, GroundDetail2, GroundDetail3, GroundDetail4,   // 4 5 22 23 24
            GroundDetail5, GroundDetail6, GroundDetail7, GroundDetail8, GroundDetail9,   // 41 42 43 60 61

            Grass, GrassA, GrassB, GrassC,                                               // 99 1 2 40
            GrassNW, GrassN, GrassNE,                                                    // 79 80 81
            GrassW, GrassE,                                                              // 98 100
            GrassSW, GrassS, GrassSE,                                                    // 117 118 119
            GrassInNW, GrassInNE, GrassInSW, GrassInSE,                                  // 6 7 25 26

            Road, RoadA, RoadB, RoadC,                                                   // 102 95 96 97
            RoadNW, RoadN, RoadNE,                                                       // 82 83 84
            RoadW, RoadE,                                                                // 101 103
            RoadSW, RoadS, RoadSE,                                                       // 120 121 122
        }

        public const int TilesetColumns = 39;

        /// <summary>칸의 «갈래» — 원본 <c>tileType</c> (길 102 · 풀 99 · 그 밖 3).</summary>
        public enum ETileKind
        {
            Ground,
            Grass,
            Road,
        }

        private readonly UndeadTerrainNoise _noiseDetail = new UndeadTerrainNoise(UndeadTerrainSeeds.Detail);
        private readonly UndeadTerrainNoise _noiseGrass = new UndeadTerrainNoise(UndeadTerrainSeeds.Grass);
        private readonly UndeadTerrainNoise _noiseRoad = new UndeadTerrainNoise(UndeadTerrainSeeds.Road);

        /// <summary>원본이 한 번에 그리는 창 [실측 51×33]. 화면(1031×580 = 43×24칸)보다 넉넉하다.</summary>
        public const int WindowCols = 51;

        public const int WindowRows = 33;

        [SerializeField] private Tilemap _tilemap;

        private TileBase[] _tiles;
        private Vector3Int _lastCenter = new Vector3Int(int.MinValue, int.MinValue, 0);

        /// <summary>지금 칠해져 있는 칸 수 — <b>검사가 이 값을 센다</b>.</summary>
        public int PaintedCellCount { get; private set; }

        public void Bind(Tilemap tilemap, TileBase[] tiles)
        {
            _tilemap = tilemap;
            _tiles = tiles;
        }

        /// <summary>
        /// 카메라가 보는 자리를 채운다. <b>중심 칸이 바뀔 때만</b> 다시 칠한다 —
        /// 매 프레임 전부 칠하면 개체 렌더보다 비싸진다.
        /// </summary>
        public void Follow(UndeadVec2 centerWorld)
        {
            if (_tilemap == null || _tiles == null || _tiles.Length == 0)
                return;

            Vector3 unity = UndeadUnits.ToPosition(centerWorld.X, centerWorld.Y);
            var center = new Vector3Int(Mathf.FloorToInt(unity.x), Mathf.FloorToInt(unity.y), 0);

            if (center == _lastCenter)
                return;

            _lastCenter = center;
            Paint(center);
        }

        private void Paint(Vector3Int center)
        {
            int halfCols = WindowCols / 2;
            int halfRows = WindowRows / 2;

            var positions = new List<Vector3Int>(WindowCols * WindowRows);
            var tiles = new List<TileBase>(WindowCols * WindowRows);

            for (int y = -halfRows; y <= halfRows; y++)
            {
                for (int x = -halfCols; x <= halfCols; x++)
                {
                    var cell = new Vector3Int(center.x + x, center.y + y, 0);
                    positions.Add(cell);
                    tiles.Add(_tiles[TileIndex(cell)]);
                }
            }

            _tilemap.ClearAllTiles();
            _tilemap.SetTiles(positions.ToArray(), tiles.ToArray());
            PaintedCellCount = positions.Count;
        }

        /// <summary>
        /// 칸 → 타일. <b>원본과 «같은 3패스»다</b> [소스 <c>updateOwnTileId</c> → <c>ou</c> → <c>hu</c>].
        ///
        /// <para>
        /// ① <c>au</c> — 길 능선(<c>(road+1)/2 &gt; .9</c>) · 풀밭(<c>(grass+1)/2 &lt; .32</c>) · 바닥(디테일 띠마다 장식)<br/>
        /// ② <c>ou</c> — <b>얇은 목</b>인 풀밭 칸은 바닥으로 되돌린다 (좌우 둘 다 풀이 아니거나, 상하 둘 다 아닐 때)<br/>
        /// ③ <c>hu</c> — 이웃 갈래로 <b>가장자리 타일</b>을 고른다. <b>길에도 가장자리가 있다</b>
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>순서가 곧 규칙이다</b> — <c>hu</c> 의 조건은 «먼저 맞는 것»이 이긴다. 원본 순서를 바꾸면
        /// 같은 이웃 배치에서 다른 타일이 나온다.
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>②가 ③이 보는 갈래를 바꾼다</b> — 그래서 갈래를 두 단계(<see cref="BaseKind"/> → <see cref="Kind"/>)로 나눠 둔다.
        /// </para>
        ///
        /// <para>⚠ 원본 y 는 «아래»로 증가한다. 여기 <c>cell.y + 1</c> 이 원본의 <c>i − 1</c>(위)이다.</para>
        /// </summary>
        private int TileIndex(Vector3Int cell)
        {
            if (_tiles.Length < TilesetColumns)
                return 0;

            int x = cell.x;
            int y = cell.y;

            ETileKind kind = Kind(x, y);

            // ③ hu — 가장자리
            if (kind == ETileKind.Road)
            {
                bool up = Kind(x, y + 1) == ETileKind.Road;
                bool down = Kind(x, y - 1) == ETileKind.Road;
                bool left = Kind(x - 1, y) == ETileKind.Road;
                bool right = Kind(x + 1, y) == ETileKind.Road;

                if (up == false && left == false) return (int)ETile.RoadNW;      // 82
                if (up == false && right == false) return (int)ETile.RoadNE;     // 84
                if (down == false && left == false) return (int)ETile.RoadSW;    // 120
                if (down == false && right == false) return (int)ETile.RoadSE;   // 122
                if (up == false) return (int)ETile.RoadN;                        // 83
                if (down == false) return (int)ETile.RoadS;                      // 121
                if (left == false) return (int)ETile.RoadW;                      // 101
                if (right == false) return (int)ETile.RoadE;                     // 103
            }
            else if (kind == ETileKind.Grass)
            {
                bool up = Kind(x, y + 1) == ETileKind.Grass;
                bool down = Kind(x, y - 1) == ETileKind.Grass;
                bool left = Kind(x - 1, y) == ETileKind.Grass;
                bool right = Kind(x + 1, y) == ETileKind.Grass;
                bool upLeft = Kind(x - 1, y + 1) == ETileKind.Grass;
                bool upRight = Kind(x + 1, y + 1) == ETileKind.Grass;
                bool downLeft = Kind(x - 1, y - 1) == ETileKind.Grass;
                bool downRight = Kind(x + 1, y - 1) == ETileKind.Grass;

                if (left == false && up == false && right && down) return (int)ETile.GrassNW;   // 79
                if (left && up == false && right == false) return (int)ETile.GrassNE;           // 81
                if (left == false && up && right && down == false) return (int)ETile.GrassSW;   // 117
                if (left && up && right == false && down == false) return (int)ETile.GrassSE;   // 119
                if (left && up && upLeft == false) return (int)ETile.GrassInNW;                 // 6
                if (right && up && upRight == false) return (int)ETile.GrassInNE;               // 7
                if (left && down && downLeft == false) return (int)ETile.GrassInSW;             // 25
                if (right && down && downRight == false) return (int)ETile.GrassInSE;           // 26
                if (left && up == false && right) return (int)ETile.GrassN;                     // 80
                if (up && left == false && down) return (int)ETile.GrassW;                      // 98
                if (up && right == false && down) return (int)ETile.GrassE;                     // 100
                if (left && down == false && right) return (int)ETile.GrassS;                   // 118
            }

            return (int)BaseTile(x, y);
        }

        /// <summary>① <c>au</c> — 이웃을 보지 않는 «자기 타일».</summary>
        private ETile BaseTile(int x, int y)
        {
            double e = (Detail(x, y) + 1.0) * 0.5;

            if (IsRoad(x, y))
            {
                int r = (int)Math.Round(100.0 * e % 10.0);

                if (r == 1) return ETile.RoadA;   // 95
                if (r == 3) return ETile.RoadB;   // 96
                if (r == 6) return ETile.RoadC;   // 97

                return ETile.Road;                // 102
            }

            if (IsGrassRaw(x, y))
            {
                double s = (GrassValue(x, y) + 1.0) * 0.5 / 0.3;
                int g = (int)Math.Round(100.0 * s % 10.0);

                if (g == 0 || g == 3 || g == 6 || g == 9) return ETile.GrassB;    // 2
                if (g == 1 || g == 4 || g == 7 || g == 10) return ETile.GrassA;   // 1
                if (g == 2) return ETile.GrassC;                                  // 40

                return ETile.Grass;                                               // 99
            }

            // 바닥 — 10% 띠마다 4% 폭의 장식 [소스]
            if (e < 0.04) return ETile.GroundDetail0;
            if (e < 0.1) return ETile.Ground;
            if (e < 0.14) return ETile.GroundDetail1;
            if (e < 0.2) return ETile.Ground;
            if (e < 0.24) return ETile.GroundDetail2;
            if (e < 0.3) return ETile.Ground;
            if (e < 0.34) return ETile.GroundDetail3;
            if (e < 0.4) return ETile.Ground;
            if (e < 0.44) return ETile.GroundDetail4;
            if (e < 0.5) return ETile.Ground;
            if (e < 0.54) return ETile.GroundDetail5;
            if (e < 0.6) return ETile.Ground;
            if (e < 0.64) return ETile.GroundDetail6;
            if (e < 0.7) return ETile.Ground;
            if (e < 0.74) return ETile.GroundDetail7;
            if (e < 0.8) return ETile.Ground;
            if (e < 0.84) return ETile.GroundDetail8;
            if (e < 0.9) return ETile.Ground;
            if (e < 0.94) return ETile.GroundDetail9;
            return ETile.Ground;
        }

        /// <summary>① 의 갈래 — 아직 «목 제거» 전이다.</summary>
        private ETileKind BaseKind(int x, int y)
        {
            if (IsRoad(x, y))
                return ETileKind.Road;

            return IsGrassRaw(x, y) ? ETileKind.Grass : ETileKind.Ground;
        }

        /// <summary>
        /// ② <c>ou</c> 를 거친 갈래 — <b>얇은 목</b>인 풀밭은 바닥이 된다.
        /// <para>[소스] 좌우 둘 다 풀이 아니거나, 상하 둘 다 풀이 아니면 바닥으로 되돌린다.</para>
        /// </summary>
        private ETileKind Kind(int x, int y)
        {
            ETileKind self = BaseKind(x, y);

            if (self != ETileKind.Grass)
                return self;

            bool up = BaseKind(x, y + 1) == ETileKind.Grass;
            bool down = BaseKind(x, y - 1) == ETileKind.Grass;
            bool left = BaseKind(x - 1, y) == ETileKind.Grass;
            bool right = BaseKind(x + 1, y) == ETileKind.Grass;

            if ((left == false && right == false) || (up == false && down == false))
                return ETileKind.Ground;

            return ETileKind.Grass;
        }

        private bool IsRoad(int x, int y)
        {
            double road = _noiseRoad.Sample(x * UndeadTerrainSeeds.RoadFrequency, y * UndeadTerrainSeeds.RoadFrequency);
            return (Math.Pow(1.0 - Math.Abs(road), 3.0) + 1.0) * 0.5 > RoadThreshold;
        }

        /// <summary>풀밭 값 — 원본은 부호를 뒤집는다 [소스 <c>noiseGrass = −simplex</c>].</summary>
        private double GrassValue(int x, int y)
        {
            return -_noiseGrass.Sample(x * UndeadTerrainSeeds.GrassFrequency, y * UndeadTerrainSeeds.GrassFrequency);
        }

        /// <summary>⚠ <c>ou</c> 전의 판정이다 — 갈래는 <see cref="Kind"/> 를 쓴다.</summary>
        private bool IsGrassRaw(int x, int y)
        {
            if (IsRoad(x, y))
                return false;

            return (GrassValue(x, y) + 1.0) * 0.5 < GrassThreshold;
        }

        private double Detail(int x, int y)
        {
            return _noiseDetail.Sample(x * UndeadTerrainSeeds.DetailFrequency, y * UndeadTerrainSeeds.DetailFrequency);
        }
    }
}
