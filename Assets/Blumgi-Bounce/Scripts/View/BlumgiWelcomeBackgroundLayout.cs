using UnityEngine;

namespace JinHyung.BlumgiBounce
{
    /// <summary>
    /// ★★★ WELCOME(<c>Start</c> 레이아웃) 의 <c>BG</c> 레이어 장식 <b>5개</b>.
    ///
    /// <para>
    /// ⚠⚠ <b><see cref="BlumgiBackgroundLayout"/>(인게임)와 «전혀 다른 표»다.</b>
    /// 인게임은 <b>11개</b>(야자수 5 · 기둥 5 · 격자 1)이고 웰컴은 <b>5개</b>(야자수 2 · 기둥 2 · 격자 1)다 —
    /// <b>좌표 · 개수 · 틴트 · 격자 크기 · 격자 불투명도가 전부 다르다</b> [실측 24회차 · `04 §2-a`].
    /// 인게임 표를 갖다 쓰면 그림이 통째로 틀린다. <b>섞어 읽지 않는다.</b>
    /// </para>
    ///
    /// <para>
    /// ★★ <b>「<c>BG</c> 는 화면에서 ×1/3」은 «인게임 레이아웃 전용» 사실이다</b> [실측 24회차].
    /// <c>Start</c> 의 <c>BG</c> 는 <c>zElevation = 0</c> 이라 <b>원근 축소가 «없고»</b>
    /// 뷰포트가 다른 레이어와 같은 <c>[−498.383, 0, 1778.383, 1280]</c> 다
    /// (캔버스 836×470 기준 배율 0.3671875 = 470/1280).
    /// 여기에 ×1/3 을 걸면 <b>야자수가 1/3 로 쪼그라든다.</b>
    /// </para>
    ///
    /// <para>
    /// ★ <b>환산은 이 클래스 «한 곳»만 지난다</b> (`04 §6-a` 의 식 그대로, 뷰포트만 웰컴 것이다):
    /// <code>
    /// canvasX = (worldX − vpLeft) × canvasW / (vpRight − vpLeft)
    /// canvasY = (worldY − vpTop ) × canvasH / (vpBottom − vpTop )
    /// </code>
    /// 뷰포트의 <b>가로 중심이 정확히 world 640</b> 이고 <b>세로가 0~1280 으로 «고정»</b>이라
    /// (원본이 세로 고정 · 가로 확장 = scale outer 라서),
    /// 위 식은 <b>「가로는 캔버스 중심에서 잰 오프셋 · 세로는 캔버스 위에서 잰 거리」</b>와 같다 —
    /// 그래서 <b>화면 비가 16:9 가 아니어도 그대로 맞는다</b>. 정규화(0~1) 로 굽지 않는 이유가 이것이다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>순백 마스크 + 런타임 틴트</b>다 (재발방지 #94) — <c>palm_leaf</c> · <c>palm_trunk</c> ·
    /// <c>grid_tile</c> 는 전부 <b>흰색 알파 마스크</b>이고 색은 원본이 런타임에 입힌다.
    /// <b>결과 색을 텍스처에 굽지 않는다.</b> 여기 적힌 틴트를 <c>Image.color</c> 로 건다.
    /// </para>
    /// </summary>
    public static class BlumgiWelcomeBackgroundLayout
    {
        // ─────────────────────────────────────────────── 뷰포트 [실측 24회차 · gap24/dump_welcome.json]

        /// <summary><c>Start</c> 레이아웃 <c>BG</c> 레이어 <c>GetViewport()</c> 왼쪽 [실측].</summary>
        public const double BgViewportLeft = -498.383;

        /// <summary>위 [실측] — 인게임(−1280.5)과 다르다.</summary>
        public const double BgViewportTop = 0.0;

        public const double BgViewportRight = 1778.383;

        public const double BgViewportBottom = 1280.0;

        /// <summary>뷰포트 가로 중심 = <c>layout._scrollX</c> [실측 640].</summary>
        public static double CenterWorldX
        {
            get { return (BgViewportLeft + BgViewportRight) * 0.5; }
        }

        /// <summary>뷰포트 세로 크기 [실측 1280] — <b>고정되는 축</b>이다 (scale outer).</summary>
        public static double ViewportWorldHeight
        {
            get { return BgViewportBottom - BgViewportTop; }
        }

        // ─────────────────────────────────────────────── 환산 — «여기만» 지난다

        /// <summary>
        /// world → 캔버스 px 배율. <b>세로가 심판이다</b> — 캔버스 836×470 이면 0.3671875,
        /// 우리 참조 해상도 1920×1080 이면 0.84375 다. <b>같은 식</b>이다.
        /// </summary>
        public static float ScaleFor(float canvasHeightPixels)
        {
            return (float)(canvasHeightPixels / ViewportWorldHeight);
        }

        /// <summary>world 길이 → 캔버스 px.</summary>
        public static float ToCanvasLength(double worldLength, float canvasHeightPixels)
        {
            return (float)(worldLength * ScaleFor(canvasHeightPixels));
        }

        /// <summary>world x → <b>캔버스 가로 중심에서 잰</b> 오프셋 px.</summary>
        public static float ToCanvasOffsetX(double worldX, float canvasHeightPixels)
        {
            return (float)((worldX - CenterWorldX) * ScaleFor(canvasHeightPixels));
        }

        /// <summary>world y → <b>캔버스 위에서 잰</b> 거리 px.</summary>
        public static float ToCanvasTopDistance(double worldY, float canvasHeightPixels)
        {
            return (float)((worldY - BgViewportTop) * ScaleFor(canvasHeightPixels));
        }

        // ─────────────────────────────────────────────── 타일링 환산

        /// <summary>
        /// <c>Image.Type.Tiled</c> 의 타일 한 장은 <b>「스프라이트 px ÷ 스프라이트 PPU × 캔버스 refPPU」</b>
        /// 만큼의 «로컬 px» 를 차지한다 (레벨 도트 바에서 실측으로 확인한 그 규칙).
        /// 우리 아트는 PPU <c>50</c> · 캔버스 refPPU <c>100</c> 이라 <b>로컬 px 1 = world px 0.5</b> 다.
        ///
        /// <para>
        /// 그래서 <b>원본 <c>imageScale</c> 을 트랜스폼이 든다</b> — 인게임 배경이 쓰는 것과 같은 수법이다.
        /// </para>
        /// </summary>
        public static float WorldPerLocalPixel
        {
            get { return BlumgiUnits.WorldPixelsPerUnit / BlumgiUnits.UiPixelsPerUnit; }
        }

        /// <summary>타일드 이미지에 걸 <c>localScale</c>.</summary>
        public static float TileLocalScale(float imageScale, float canvasHeightPixels)
        {
            return imageScale * WorldPerLocalPixel * ScaleFor(canvasHeightPixels);
        }

        /// <summary>타일드 이미지의 <c>sizeDelta</c> (배율 «안»의 로컬 px).</summary>
        public static float TileLocalLength(double worldLength, float imageScale)
        {
            return (float)(worldLength / (imageScale * WorldPerLocalPixel));
        }

        // ─────────────────────────────────────────────── 색 [실측 24회차]

        /// <summary>
        /// 야자수(잎 · 기둥) 틴트 <c>(58, 177, 228)</c> [실측 · 인스턴스 4개 전부 동일].
        /// ⚠ 인게임 야자수는 <c>#80E5FF</c> 라 <b>다르다</b>.
        /// </summary>
        public static readonly Color PalmTint = new Color32(58, 177, 228, 255);

        /// <summary>
        /// 격자 틴트 <b>흰색 · 불투명도 0.5</b> [실측 <c>tint (255,255,255)</c> · <c>op 0.5</c>].
        /// ⚠ 인게임 격자는 <c>op 1</c> 에 레벨 테마색이라 <b>다르다</b>.
        ///
        /// <para>
        /// ⚠ <b>원본 알파를 역산하지 않는다</b> (CLAUDE.md 렌더 규칙) — 원본 캔버스는 감마 합성이라
        /// 브라우저 실측 격자선이 <c>(163, 227, 255)</c>(= 0.5 백색 + 배경 <c>#47C8FF</c> 의 «감마» 평균)인데
        /// Linear 합성인 우리 쪽은 같은 알파에서 더 밝게 나온다. <b>값은 원본 그대로 두고</b>
        /// 차이는 렌더 대조에서 «읽는 숫자»로 남긴다.
        /// </para>
        /// </summary>
        public static readonly Color GridTint = new Color(1f, 1f, 1f, 0.5f);

        // ─────────────────────────────────────────────── 격자 [실측]

        /// <summary>
        /// 격자 한 칸 = <b>150 world</b> [실측 24회차 캡처 · <c>gap24/shot_welcome.png</c>].
        /// 세로줄 14개 회귀 <c>(766 − 50) / 13 = 55.0769 css</c> ÷ 0.3671875 = <b>150.00 world</b> ·
        /// 가로줄 5개 예측 51.04 / 106.12 / 161.20 / 216.27 / 271.35 ↔ 관측 51 / 106 / 161 / 216 / 271
        /// (<b>≤ 0.35 px</b>). <b>위상은 격자 원점 기준</b>이다.
        /// ⚠ 인게임은 <b>232.5</b>(<c>imageScale 4.65</c>) 라 <b>다르다</b>.
        /// </summary>
        public const double GridCellWorld = 150.0;

        /// <summary>소스 격자 타일 한 칸 = <b>50 px</b> (구운 <c>grid_tile.png</c> 100 px = 2칸).</summary>
        public const double GridSourceCellPixels = 50.0;

        /// <summary>격자의 원본 <c>imageScale</c> — <see cref="GridCellWorld"/> ÷ 소스 50 px = <b>3.0</b>.</summary>
        public static float GridImageScale
        {
            get { return (float)(GridCellWorld / GridSourceCellPixels); }
        }

        /// <summary>
        /// 기둥 타일 배율 [17회차 인게임 실측 <c>imageScale = 3</c> · 같은 클래스 <c>Tlb_PalmTrunk</c>].
        /// ⚠ <b>웰컴에서는 재확인하지 못했다</b> — 보이는 구간이 하단 그라디언트에 묻혀 마디가 안 잡힌다
        /// (04 §6 재측정 대기). 보이는 기둥이 <b>한 타일(588 world)보다 짧아</b> 화면상 차이가 없다.
        /// </summary>
        public const float TrunkImageScale = 3f;

        // ─────────────────────────────────────────────── 인스턴스 전수 5개 [실측 24회차]

        /// <summary>장식 한 개. <b>bbox 는 원본 <c>GetBoundingBox()</c> 그대로</b>다.</summary>
        public struct Decor
        {
            /// <summary>계층에서 쓸 이름. 검사기가 이 이름으로 찾는다.</summary>
            public string Name;

            /// <summary>어드레서블 주소 (<see cref="BlumgiArtAddress"/>).</summary>
            public string Address;

            public double WorldLeft;

            public double WorldTop;

            public double WorldRight;

            public double WorldBottom;

            /// <summary>반복(타일드)인가. 아니면 <c>Simple</c>(한 장 늘림)이다.</summary>
            public bool Tiled;

            /// <summary>타일드일 때의 원본 <c>imageScale</c>.</summary>
            public float ImageScale;

            /// <summary>
            /// 타일 «시작점»이 원본에서 <b>위쪽</b>인가.
            /// ⚠ 유니티 <c>Image.Type.Tiled</c> 는 <b>rect 아래에서 위로</b> 채우는데
            /// 원본 <c>Tbg_Grid</c> 는 <b>원점(좌상단)에서 아래로</b> 채운다 —
            /// 높이가 칸의 정수배가 아니면 <b>줄 위상이 통째로 어긋난다</b>.
            /// 그래서 이 표시가 붙은 것만 <b>아래쪽을 칸 경계까지 늘려</b> 굽는다 (늘어난 부분은 화면 밖이다).
            /// 기둥은 원본 원점이 <b>화면 아래쪽</b>(회전 270°) 이라 그대로 맞아 표시가 없다.
            /// </summary>
            public bool SnapTileFromTop;

            public Color Tint;

            public double WorldWidth
            {
                get { return WorldRight - WorldLeft; }
            }

            public double WorldHeight
            {
                get { return WorldBottom - WorldTop; }
            }

            public double WorldCenterX
            {
                get { return (WorldLeft + WorldRight) * 0.5; }
            }

            public double WorldCenterY
            {
                get { return (WorldTop + WorldBottom) * 0.5; }
            }
        }

        /// <summary>
        /// <b>zIndex 오름차순 = 그리는 순서</b> [실측 <c>zi</c> 0·1·2·3·4].
        /// 격자가 맨 아래 · 그루마다 <b>잎 → 기둥</b> 순이다 (인게임과 같은 짝 규칙).
        /// UI 라 <b>형제 순서가 곧 그리는 순서</b>이므로 이 배열 순서대로 자식을 만든다.
        /// </summary>
        public static readonly Decor[] Decors =
        {
            // zi 0 — Tbg_Grid #551970 · tint (255,255,255) · op 0.5
            new Decor
            {
                Name = "Grid", Address = BlumgiArtAddress.GridTileAddress,
                WorldLeft = -2161.0, WorldTop = -2111.0, WorldRight = 3113.816, WorldBottom = 4314.0,
                Tiled = true, ImageScale = 3f, SnapTileFromTop = true,
                Tint = GridTint,
            },

            // zi 1 — spr_PalmTree #627947 (오른쪽 그루의 잎)
            new Decor
            {
                Name = "PalmLeafA", Address = BlumgiArtAddress.PalmLeafAddress,
                WorldLeft = 905.732, WorldTop = 590.96, WorldRight = 1339.843, WorldBottom = 823.78,
                Tiled = false, ImageScale = 1f, SnapTileFromTop = false,
                Tint = PalmTint,
            },

            // zi 2 — Tlb_PalmTrunk #350017 (a = 4.712 rad = 270°)
            new Decor
            {
                Name = "PalmTrunkA", Address = BlumgiArtAddress.PalmTrunkAddress,
                WorldLeft = 1078.0, WorldTop = 748.58, WorldRight = 1153.246, WorldBottom = 4689.0,
                Tiled = true, ImageScale = TrunkImageScale, SnapTileFromTop = false,
                Tint = PalmTint,
            },

            // zi 3 — spr_PalmTree #403060 (왼쪽 그루의 잎)
            new Decor
            {
                Name = "PalmLeafB", Address = BlumgiArtAddress.PalmLeafAddress,
                WorldLeft = 9.732, WorldTop = 925.96, WorldRight = 443.843, WorldBottom = 1158.78,
                Tiled = false, ImageScale = 1f, SnapTileFromTop = false,
                Tint = PalmTint,
            },

            // zi 4 — Tlb_PalmTrunk #880475
            new Decor
            {
                Name = "PalmTrunkB", Address = BlumgiArtAddress.PalmTrunkAddress,
                WorldLeft = 182.0, WorldTop = 1083.58, WorldRight = 257.246, WorldBottom = 5024.0,
                Tiled = true, ImageScale = TrunkImageScale, SnapTileFromTop = false,
                Tint = PalmTint,
            },
        };

        /// <summary>
        /// 굽는 세로 크기 (world). <see cref="Decor.SnapTileFromTop"/> 인 것만
        /// <b>아래쪽을 칸 경계까지</b> 늘린다 — 늘어난 만큼은 뷰포트(0~1280) 밖이다.
        /// </summary>
        public static double BakedWorldHeight(Decor decor)
        {
            if (decor.SnapTileFromTop == false)
                return decor.WorldHeight;

            double cell = decor.ImageScale * GridSourceCellPixels;
            int cells = Mathf.CeilToInt((float)(decor.WorldHeight / cell));
            return cells * cell;
        }

        /// <summary>굽는 세로 중심 (world). 위 변은 실측 그대로 두고 아래만 늘어난다.</summary>
        public static double BakedWorldCenterY(Decor decor)
        {
            return decor.WorldTop + BakedWorldHeight(decor) * 0.5;
        }
    }
}
