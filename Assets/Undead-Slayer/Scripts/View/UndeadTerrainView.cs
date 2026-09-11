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

        /// <summary>길 능선 문턱 [소스 <c>au</c>·<c>pu</c> — <c>(road+1)/2 &gt; .9</c>]. <b>두 바이옴이 같다.</b></summary>
        private const double RoadThreshold = 0.9;

        /// <summary>
        /// 풀밭(눈밭) 문턱 — <b>바이옴마다 다르다</b> [소스 <c>au</c> <c>&lt; .32</c> · <c>pu</c> <c>&lt; .75</c>].
        /// <para>★ 겨울은 문턱이 두 배 넘게 높아 <b>화면 대부분이 눈밭</b>이다 — 이 한 숫자가 두 바이옴의 인상을 가른다.</para>
        /// <para>⚠ 회차 9 는 이 영역을 «길»로 오독했다.</para>
        /// </summary>
        private const double GraveyardGrassThreshold = 0.32;

        private const double WinterGrassThreshold = 0.75;

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
        ///
        /// <para>
        /// ★★ <b>두 바이옴의 «합집합»이다.</b> 묘지는 풀 변종을 <b>셋</b>만 쓰고 겨울은 <b>열</b>을 쓴다 —
        /// 안 쓰는 자리는 그 갈래의 <b>바닥 타일로 채워</b> 굽는다. 시트에 구멍을 두면
        /// 어느 바이옴에서 어느 칸이 비는지가 그림에만 나타난다.
        /// </para>
        /// </summary>
        public enum ETile
        {
            Ground,                                                                      // 3 / 611
            GroundDetail0, GroundDetail1, GroundDetail2, GroundDetail3, GroundDetail4,
            GroundDetail5, GroundDetail6, GroundDetail7, GroundDetail8, GroundDetail9,

            Grass,                                                                       // 99 / 707
            GrassVar0, GrassVar1, GrassVar2, GrassVar3, GrassVar4,
            GrassVar5, GrassVar6, GrassVar7, GrassVar8, GrassVar9,
            GrassNW, GrassN, GrassNE,
            GrassW, GrassE,
            GrassSW, GrassS, GrassSE,
            GrassInNW, GrassInNE, GrassInSW, GrassInSE,

            Road,                                                                        // 102 / 716
            RoadVar0, RoadVar1, RoadVar2,
            RoadNW, RoadN, RoadNE,
            RoadW, RoadE,
            RoadSW, RoadS, RoadSE,
        }

        /// <summary>⚠ 굽는 쪽(<c>measure_biome_tiles.py</c> 의 역할 목록)과 <b>한 쌍</b>이다.</summary>
        public const int TilesetColumns = 46;

        /// <summary>바이옴 — <b>1 묘지 · 2 겨울 황무지</b> [소스 <c>jl = [1, 2]</c>].</summary>
        public int Biome { get; private set; } = 1;

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

        /// <summary>
        /// 창 하나치 <b>타일 캐시</b> [소스 <c>caches.tileId</c> · <c>caches.tileType</c>].
        ///
        /// <para>
        /// ★★ <b>캐시가 있어야 원본과 같아진다.</b> 원본 2패스(<c>ou</c>)는 이 배열을 <b>제자리에서 고치며</b>
        /// 행 우선으로 훑는다 — 그래서 <b>왼쪽·위 이웃은 «이미 고쳐진» 값</b>이고, 얇은 풀 목을 지우는 판정이
        /// <b>오른쪽·아래로 전파</b>된다.
        /// </para>
        ///
        /// <para>
        /// ⚠ [사고] 예전에는 이웃을 <b>순수 함수로 다시 계산</b>했다 — 전파가 없어 <b>얇은 풀 조각이 그대로 남았고</b>,
        /// 원본의 «큰 덩어리»와 달리 화면이 잘게 조각나 보였다. 값은 전부 맞는데 그림이 달랐다.
        /// </para>
        /// </summary>
        private ETile[] _cacheTile;

        private ETileKind[] _cacheKind;

        /// <summary>창의 왼쪽 끝 칸 — <b>원본 타일 좌표</b> [소스 <c>lastCameraTileX</c>].</summary>
        private int _originTileX;

        /// <summary>
        /// 창의 <b>위</b> 끝 칸 — <b>원본 타일 좌표</b> [소스 <c>lastCameraTileY</c>].
        ///
        /// <para>
        /// ⚠⚠ <b>원본 타일 y 는 «아래»로 증가한다</b>. 유니티 칸 y 는 «위»로 증가한다 —
        /// <c>UndeadUnits.ToPosition</c> 이 y 를 뒤집기 때문이다.
        /// </para>
        ///
        /// <para>
        /// ⚠ [사고] 예전에는 <b>유니티 칸 좌표를 그대로 노이즈에 넣었다</b>. 심플렉스 노이즈는
        /// <c>f(x, −y) ≠ f(x, y)</c> 라 <b>지형 무늬가 통째로 «상하 뒤집힌» 다른 무늬</b>가 나왔다.
        /// 나무·모닥불은 시뮬(원본 좌표)이 놓으므로 <b>지형만 어긋나</b> 「무늬가 다르다」로 보인다.
        /// 노이즈 표본 골든은 <b>순수 함수</b>만 재서 이 어긋남을 못 잡았다.
        /// </para>
        /// </summary>
        private int _originTileY;

        /// <summary>지금 칠해져 있는 칸 수 — <b>검사가 이 값을 센다</b>.</summary>
        public int PaintedCellCount { get; private set; }

        private TileBase[] _graveyardTiles;
        private TileBase[] _winterTiles;

        /// <summary>
        /// 타일맵과 <b>바이옴 두 벌</b>을 물린다.
        /// <para>⚠ 겨울 벌이 없으면 묘지 벌로 대신한다 — <b>조용히 «다른 바이옴 그림»이 나온다</b>는 뜻이라 오류를 남긴다.</para>
        /// </summary>
        public void Bind(Tilemap tilemap, TileBase[] graveyardTiles, TileBase[] winterTiles)
        {
            _tilemap = tilemap;
            _graveyardTiles = graveyardTiles;
            _winterTiles = winterTiles;

            if (winterTiles == null || winterTiles.Length < TilesetColumns)
                Log.Error("겨울 타일셋을 못 읽었다 — 바이옴 2 가 묘지 그림으로 나온다");

            _tiles = Biome == 2 && winterTiles != null ? winterTiles : graveyardTiles;
        }

        /// <summary>
        /// 바이옴을 바꾼다 — <b>타일 벌을 갈고 창을 다시 칠한다</b>.
        /// <para>⚠ 다시 칠하지 않으면 카메라가 «칸을 넘을 때»까지 옛 바이옴 그림이 남는다.</para>
        /// </summary>
        public void SetBiome(int biome)
        {
            Biome = biome == 2 ? 2 : 1;
            _tiles = Biome == 2 && _winterTiles != null ? _winterTiles : _graveyardTiles;
            _lastCenter = new Vector3Int(int.MinValue, int.MinValue, 0);
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

        /// <summary>
        /// 창 하나를 <b>원본과 «같은 세 패스»로</b> 칠한다 [소스 <c>updateTileCache</c>].
        ///
        /// <para>
        /// ① <c>updateOwnTileId</c> — 이웃을 안 보고 자기 타일·갈래를 캐시에 넣는다<br/>
        /// ② <c>ou</c> — <b>캐시를 제자리에서 고친다</b> (얇은 풀 목 → 바닥). 행 우선이라 <b>전파된다</b><br/>
        /// ③ <c>hu</c> — ②의 결과를 읽어 가장자리 타일을 고른다
        /// </para>
        ///
        /// <para>⚠ <b>세 패스를 한 패스로 합치면 안 된다</b> — ②의 전파가 사라져 그림이 달라진다.</para>
        /// </summary>
        private void Paint(Vector3Int center)
        {
            int halfCols = WindowCols / 2;
            int halfRows = WindowRows / 2;

            // ★ 창 안에서는 «원본 타일 좌표»로만 센다 — 노이즈도 이웃도 전부 원본 기준이다.
            //   유니티 칸으로는 «그릴 때 한 번»만 바꾼다 (tileY = −unityY).
            _originTileX = center.x - halfCols;
            _originTileY = -(center.y + halfRows);

            int count = WindowCols * WindowRows;

            if (_cacheTile == null || _cacheTile.Length != count)
            {
                _cacheTile = new ETile[count];
                _cacheKind = new ETileKind[count];
            }

            // ① 자기 타일 — 이웃을 안 본다
            for (int row = 0; row < WindowRows; row++)
            {
                for (int col = 0; col < WindowCols; col++)
                {
                    int tx = _originTileX + col;
                    int ty = _originTileY + row;
                    int i = row * WindowCols + col;

                    _cacheTile[i] = BaseTile(tx, ty);
                    _cacheKind[i] = BaseKind(tx, ty);
                }
            }

            // ② ou — 제자리에서 고친다. ⚠ 순서가 곧 규칙이다 (위 → 아래 · 왼 → 오른)
            for (int row = 0; row < WindowRows; row++)
            {
                for (int col = 0; col < WindowCols; col++)
                    RemoveThinNeck(_originTileX + col, _originTileY + row);
            }

            // ③ hu — 가장자리
            var positions = new List<Vector3Int>(count);
            var tiles = new List<TileBase>(count);

            for (int row = 0; row < WindowRows; row++)
            {
                for (int col = 0; col < WindowCols; col++)
                {
                    int tx = _originTileX + col;
                    int ty = _originTileY + row;

                    // 그릴 때만 유니티 칸으로 — y 뒤집기는 «여기 한 곳»뿐이다
                    positions.Add(new Vector3Int(tx, -ty, 0));
                    tiles.Add(_tiles[(int)Autotile(tx, ty)]);
                }
            }

            _tilemap.ClearAllTiles();
            _tilemap.SetTiles(positions.ToArray(), tiles.ToArray());
            PaintedCellCount = positions.Count;
        }

        /// <summary>
        /// 캐시 자리 [소스 <c>getTileIndex</c> — <c>(y − 위끝) × 열수 + (x − 왼끝)</c>].
        /// <para>⚠ <b>원본은 범위를 «자르지 않는다»</b> — 창 왼쪽 밖을 물으면 «윗줄 오른쪽 끝»이 나온다.
        /// 그 버릇까지 원본 그림의 일부라 여기서도 자르지 않는다.</para>
        /// </summary>
        private int CacheIndex(int tileX, int tileY)
        {
            return (tileY - _originTileY) * WindowCols + (tileX - _originTileX);
        }

        /// <summary>캐시가 든 갈래 — <b>창 밖은 «바닥»</b>으로 본다 (원본에서는 <c>undefined</c> 라 어느 갈래와도 다르다).</summary>
        private ETileKind KindAt(int x, int y)
        {
            int i = CacheIndex(x, y);
            return (uint)i < (uint)_cacheKind.Length ? _cacheKind[i] : ETileKind.Ground;
        }

        /// <summary>
        /// ② <c>ou</c> — <b>얇은 목</b>인 풀밭 칸을 바닥으로 되돌린다.
        /// <para>[소스] 좌우 둘 다 풀이 아니거나, 상하 둘 다 풀이 아니면 바닥이다.</para>
        /// <para>⚠ 캐시를 <b>제자리에서</b> 고친다 — 그래야 다음 칸이 «고쳐진» 이웃을 본다.</para>
        /// </summary>
        private void RemoveThinNeck(int x, int y)
        {
            int i = CacheIndex(x, y);

            if ((uint)i >= (uint)_cacheKind.Length || _cacheKind[i] != ETileKind.Grass)
                return;

            // ⚠ 원본 y 는 «아래»로 증가한다 — 위가 −1 이다
            bool up = KindAt(x, y - 1) == ETileKind.Grass;
            bool down = KindAt(x, y + 1) == ETileKind.Grass;
            bool left = KindAt(x - 1, y) == ETileKind.Grass;
            bool right = KindAt(x + 1, y) == ETileKind.Grass;

            if ((left == false && right == false) || (up == false && down == false))
            {
                _cacheTile[i] = ETile.Ground;
                _cacheKind[i] = ETileKind.Ground;
            }
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
        /// <para>⚠ 여기 <c>x</c>·<c>y</c> 는 <b>원본 타일 좌표</b>다 — y 는 «아래»로 증가한다.
        /// 유니티 칸으로 바꾸는 곳은 <see cref="Paint"/> 의 마지막 한 줄뿐이다.</para>
        /// </summary>
        private ETile Autotile(int x, int y)
        {
            int self = CacheIndex(x, y);

            if (_tiles.Length < TilesetColumns || (uint)self >= (uint)_cacheKind.Length)
                return ETile.Ground;

            ETileKind kind = _cacheKind[self];

            // ③ hu — 가장자리
            if (kind == ETileKind.Road)
            {
                bool up = KindAt(x, y - 1) == ETileKind.Road;
                bool down = KindAt(x, y + 1) == ETileKind.Road;
                bool left = KindAt(x - 1, y) == ETileKind.Road;
                bool right = KindAt(x + 1, y) == ETileKind.Road;

                if (up == false && left == false) return ETile.RoadNW;      // 82
                if (up == false && right == false) return ETile.RoadNE;     // 84
                if (down == false && left == false) return ETile.RoadSW;    // 120
                if (down == false && right == false) return ETile.RoadSE;   // 122
                if (up == false) return ETile.RoadN;                        // 83
                if (down == false) return ETile.RoadS;                      // 121
                if (left == false) return ETile.RoadW;                      // 101
                if (right == false) return ETile.RoadE;                     // 103
            }
            else if (kind == ETileKind.Grass)
            {
                bool up = KindAt(x, y - 1) == ETileKind.Grass;
                bool down = KindAt(x, y + 1) == ETileKind.Grass;
                bool left = KindAt(x - 1, y) == ETileKind.Grass;
                bool right = KindAt(x + 1, y) == ETileKind.Grass;
                bool upLeft = KindAt(x - 1, y - 1) == ETileKind.Grass;
                bool upRight = KindAt(x + 1, y - 1) == ETileKind.Grass;
                bool downLeft = KindAt(x - 1, y + 1) == ETileKind.Grass;
                bool downRight = KindAt(x + 1, y + 1) == ETileKind.Grass;

                if (left == false && up == false && right && down) return ETile.GrassNW;   // 79
                if (left && up == false && right == false) return ETile.GrassNE;           // 81
                if (left == false && up && right && down == false) return ETile.GrassSW;   // 117
                if (left && up && right == false && down == false) return ETile.GrassSE;   // 119
                if (left && up && upLeft == false) return ETile.GrassInNW;                 // 6
                if (right && up && upRight == false) return ETile.GrassInNE;               // 7
                if (left && down && downLeft == false) return ETile.GrassInSW;             // 25
                if (right && down && downRight == false) return ETile.GrassInSE;           // 26
                if (left && up == false && right) return ETile.GrassN;                     // 80
                if (up && left == false && down) return ETile.GrassW;                      // 98
                if (up && right == false && down) return ETile.GrassE;                     // 100
                if (left && down == false && right) return ETile.GrassS;                   // 118
            }

            // ②가 고친 «캐시의» 타일이다 — 여기서 다시 계산하면 ②의 전파가 사라진다
            return _cacheTile[self];
        }

        /// <summary>① <c>au</c> — 이웃을 보지 않는 «자기 타일».</summary>
        private ETile BaseTile(int x, int y)
        {
            double e = (Detail(x, y) + 1.0) * 0.5;

            if (IsRoad(x, y))
            {
                // 길 변종은 <b>두 바이옴이 같은 식</b>이다 [소스 au·pu — 100·e % 10]
                int r = JsRound(100.0 * e % 10.0);

                if (r == 1) return ETile.RoadVar0;
                if (r == 3) return ETile.RoadVar1;
                if (r == 6) return ETile.RoadVar2;

                return ETile.Road;
            }

            if (IsGrassRaw(x, y))
                return GrassTile(x, y);

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

        /// <summary>
        /// 풀(눈) 변종 — <b>바이옴마다 «식»이 다르다</b>.
        /// <para>[소스 <c>au</c>] 묘지는 <c>round(100·s) % 10</c> 로 <b>셋</b> 중에 고르고,
        /// [소스 <c>pu</c>] 겨울은 <c>round(1000·s) % 13</c> 로 <b>열</b> 중에 고른다 —
        /// 겨울 바닥이 훨씬 잘게 섞여 보이는 이유가 이것이다.</para>
        /// <para>⚠ 어느 쪽도 맞지 않으면 <b>바닥 타일</b>이다 — 겨울은 13 중 셋(8·11·12)이 그렇다.</para>
        /// </summary>
        private ETile GrassTile(int x, int y)
        {
            double s = (GrassValue(x, y) + 1.0) * 0.5 / 0.3;

            if (Biome == 2)
            {
                switch (JsRound(1000.0 * s % 13.0))
                {
                    case 0: return ETile.GrassVar0;
                    case 1: return ETile.GrassVar1;
                    case 2: return ETile.GrassVar2;
                    case 3: return ETile.GrassVar3;
                    case 4: return ETile.GrassVar4;
                    case 5: return ETile.GrassVar5;
                    case 6: return ETile.GrassVar6;
                    case 7: return ETile.GrassVar7;
                    case 9: return ETile.GrassVar8;
                    case 10: return ETile.GrassVar9;
                    default: return ETile.Grass;
                }
            }

            // ⚠ 여기 «어느 변종»인지는 굽는 쪽 역할 순서와 한 쌍이다 —
            //   묘지 변종은 순서대로 원본 타일 2 · 1 · 40 이다.
            int g = JsRound(100.0 * s % 10.0);

            if (g == 0 || g == 3 || g == 6 || g == 9) return ETile.GrassVar0;    // 2
            if (g == 1 || g == 4 || g == 7 || g == 10) return ETile.GrassVar1;   // 1
            if (g == 2) return ETile.GrassVar2;                                  // 40

            return ETile.Grass;                                                  // 99
        }

        /// <summary>
        /// 자바스크립트 <c>Math.round</c> — <b>언제나 «반올림 위로»</b> 다.
        /// <para>⚠ C# 의 <c>Math.Round</c> 는 «짝수로» 반올림한다(2.5 → 2). 타일 변종을 고르는 나눗셈이라
        /// 딱 .5 가 나오면 <b>다른 칸이 나온다</b>.</para>
        /// </summary>
        private static int JsRound(double value)
        {
            return (int)Math.Floor(value + 0.5);
        }

        /// <summary>① 의 갈래 — 아직 «목 제거» 전이다.</summary>
        private ETileKind BaseKind(int x, int y)
        {
            if (IsRoad(x, y))
                return ETileKind.Road;

            return IsGrassRaw(x, y) ? ETileKind.Grass : ETileKind.Ground;
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

            return (GrassValue(x, y) + 1.0) * 0.5
                   < (Biome == 2 ? WinterGrassThreshold : GraveyardGrassThreshold);
        }

        private double Detail(int x, int y)
        {
            return _noiseDetail.Sample(x * UndeadTerrainSeeds.DetailFrequency, y * UndeadTerrainSeeds.DetailFrequency);
        }
    }
}
