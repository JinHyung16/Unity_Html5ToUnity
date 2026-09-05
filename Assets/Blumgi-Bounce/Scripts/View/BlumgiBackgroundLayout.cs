using JinHyung.Core;
using UnityEngine;

namespace JinHyung.BlumgiBounce
{
    /// <summary>
    /// 인게임 배경 <b>11개</b>(야자수 5 · 줄기 5 · 격자 1)를 «원본 world 좌표 그대로» 앉힌다.
    ///
    /// <para>
    /// ★★ <b>배경은 시차(parallax)가 아니라 «원근»이었다</b> [학습 5회차 실측].
    /// <c>BG</c> 레이어만 <c>zElevation = −200</c> 이고 <c>cameraZ = 100</c> 이라
    /// <c>k = 100 / (100 + 200) = 1/3</c> 이다 — <c>parallaxX/Y</c> 도 <c>scaleRate</c> 도 전부 1 이었다.
    /// 1회차가 「야자수 y ≈ 1707 인데 캡처엔 화면 하단」이라는 모순으로 미측정에 남긴 것의 정체가 이것이다.
    /// </para>
    ///
    /// <para>
    /// ★ <b>원근 카메라를 쓰지 않는다.</b> BG 콘텐츠가 <b>전부 한 z 평면</b>(−200)에 있으므로
    /// 원근이 <b>상수 배율 1/3</b> 로 완전히 축약된다 —
    /// <b>소실 중심(레이아웃 스크롤 <c>(640, 639.5)</c>)에 피벗을 둔 부모에 <c>localScale = 1/3</c></b>.
    /// 카메라가 줌 펀치·셰이크로 움직여도 «원근이 만드는 자연 시차»가 자동으로 따라온다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>표의 «등가» 열은 검산용이라 여기 넣지 않는다.</b> 부모가 1/3 을 이미 들고 있으므로
    /// 자식에는 <b>원본 world 값을 그대로</b> 넣는다 (5회차 §1-c 배치 절차 2번).
    /// </para>
    ///
    /// <para>
    /// ★ <b>5레벨이 좌표·크기·각도·zIndex 까지 완전히 같다</b> (서명 해시 5개 일치 [실측]) ⇒
    /// 레벨마다 새로 만들지 않고 <b>배경 하나를 공유</b>한다. 색만 테마를 따라간다.
    /// </para>
    /// </summary>
    public static class BlumgiBackgroundLayout
    {
        /// <summary>레이아웃 스크롤 = 원근 소실 중심 x [실측 <c>layout._scrollX</c>].</summary>
        public const double VanishingCenterX = 640.0;

        /// <summary>소실 중심 y [실측 <c>layout._scrollY</c>].</summary>
        public const double VanishingCenterY = 639.5;

        /// <summary><c>k(z) = cameraZ / (cameraZ − z)</c> = 100 / (100 + 200) [실측].</summary>
        public const float PerspectiveScale = 1f / 3f;

        /// <summary>격자 소스 타일 50 px × <c>imageScale 4.65</c> = world 피치 <b>232.5</b> [실측].</summary>
        private const float GridImageScale = 4.65f;

        /// <summary>구운 <c>grid_tile.png</c> 는 100 px = <b>2 칸</b>짜리다 (한 칸 50 px = 1 유닛 @PPU 50).</summary>
        private const float GridSpriteUnits = 2f;

        /// <summary>격자 중심 [실측 — 타일배경 (−24360, −24360) + 50000/2].</summary>
        private const double GridCenterX = 640.0;

        private const double GridCenterY = 640.0;

        /// <summary>줄기 두께 [실측 <c>Tlb_PalmTrunk</c> world 크기의 짧은 변 · 5그루 전부 75.246].</summary>
        private const double TrunkThickness = 75.25;

        /// <summary>
        /// 줄기 타일 배율 [실측 <c>imageScaleX/Y = 3</c>].
        /// ★ 소스 타일 <b>194 × 26</b> 이 <b>각도 270°</b> 로 서 있어 [5회차 실측]
        /// <b>가로 = 26 × 3 = 78</b>(≈ 인스턴스 폭 75.246 · 타일이 살짝 잘린다) ·
        /// <b>세로 582 마다 무늬 반복</b>(마디 8개 ⇒ 72.75 마다 한 마디)이다 [17회차 실측].
        /// 우리 텍스처는 <b>이미 세워서</b>(28 × 196) 구웠으므로 배율만 걸면 된다.
        /// ⚠ 배율을 안 걸면 마디가 3배 촘촘해진다 (예전 76 × 76 타일이 그 상태였다).
        /// </summary>
        private const float TrunkImageScale = 3f;

        /// <summary>야자수 잎 표시 크기 [실측] — 소스 180×96 을 이 크기로 늘려 그린다.</summary>
        private const float LeafDisplayWidth = 434.11f;

        private const float LeafDisplayHeight = 232.82f;

        /// <summary>구운 <c>palm_leaf.png</c> 소스 크기.</summary>
        private const float LeafSourceWidth = 180f;

        private const float LeafSourceHeight = 96f;

        /// <summary>
        /// <c>spr_PalmTree</c> 원점 [실측 <c>(0.5028, 0.7604)</c>] — <b>중심이 아니다</b>.
        /// 표의 world (x, y) 는 «원점»이라 중심으로 옮기려면 이만큼 뺀다.
        /// </summary>
        private const float LeafOriginX = 0.5028f;

        private const float LeafOriginY = 0.7604f;

        /// <summary>
        /// 야자수 렌더 순서의 바닥. 격자(−90)보다 위 · 골 화살표(−10)보다 아래.
        /// ★ <b>빌더가 이 값을 읽어 프리팹에 굽는다</b> (<c>BlumgiPrefabBuilder</c>) — 두 곳에 숫자를 적지 않는다.
        /// 그루 <c>i</c> 의 잎 = <c>Base + i×2</c> · 줄기 = <c>Base + i×2 + 1</c> [5회차 z 쌍 실측].
        /// </summary>
        public const int PalmSortingBase = -85;

        /// <summary>
        /// 배경 11개 [학습 5회차 전수 실측 · zIndex 오름차순].
        ///
        /// <para>
        /// 잎(<c>spr_PalmTree</c>)과 줄기(<c>Tlb_PalmTrunk</c>)는 <b>z 로 짝</b>을 이루고
        /// <b>잎이 먼저, 줄기가 그 위</b>다 [실측].
        /// </para>
        /// </summary>
        private struct PalmRow
        {
            /// <summary>잎 «원점» world x (표 그대로).</summary>
            public double LeafOriginWorldX;

            public double LeafOriginWorldY;

            /// <summary>줄기 «원점(좌상단)» world x — 잎과 다르다 [실측].</summary>
            public double TrunkOriginWorldX;

            public double TrunkOriginWorldY;

            /// <summary>줄기 길이 (회전 270° 이므로 화면에서는 «세로» 길이다) [실측].</summary>
            public double TrunkLength;
        }

        private static readonly PalmRow[] Palms =
        {
            // zi 1·2
            new PalmRow { LeafOriginWorldX = 1136.0, LeafOriginWorldY = 1768.0, TrunkOriginWorldX = 1095.0, TrunkOriginWorldY = 5099.0, TrunkLength = 3334.12 },
            // zi 3·4
            new PalmRow { LeafOriginWorldX =  833.0, LeafOriginWorldY = 2071.0, TrunkOriginWorldX =  792.0, TrunkOriginWorldY = 5099.0, TrunkLength = 3030.97 },
            // zi 5·6
            new PalmRow { LeafOriginWorldX = -596.0, LeafOriginWorldY = 1768.0, TrunkOriginWorldX = -644.0, TrunkOriginWorldY = 5099.0, TrunkLength = 3334.12 },
            // zi 7·8
            new PalmRow { LeafOriginWorldX = -259.0, LeafOriginWorldY = 2313.0, TrunkOriginWorldX = -292.0, TrunkOriginWorldY = 5123.0, TrunkLength = 2849.08 },
            // zi 9·10
            new PalmRow { LeafOriginWorldX = 1318.0, LeafOriginWorldY = 1222.0, TrunkOriginWorldX = 1272.0, TrunkOriginWorldY = 5143.0, TrunkLength = 3940.42 },
        };

        /// <summary>배경 부모를 만든다 — <b>소실 중심에 피벗 · <c>localScale = 1/3</c></b>.</summary>
        public static Transform CreateRoot(Transform parent)
        {
            var go = new GameObject("BackgroundRoot");
            go.transform.SetParent(parent, false);
            go.transform.position = BlumgiUnits.ToPosition(VanishingCenterX, VanishingCenterY);
            go.transform.localScale = new Vector3(PerspectiveScale, PerspectiveScale, 1f);
            return go.transform;
        }

        /// <summary>
        /// 부모 안에서의 로컬 위치. <b>부모가 소실 중심에 있고 1/3 을 들고 있으므로</b>
        /// 자식에는 원본 world 를 «그대로» 넣으면 된다 — 그 차이를 여기서 한 번만 계산한다.
        /// </summary>
        public static Vector3 LocalOf(double worldX, double worldY)
        {
            return BlumgiUnits.ToPosition(worldX, worldY)
                   - BlumgiUnits.ToPosition(VanishingCenterX, VanishingCenterY);
        }

        /// <summary>
        /// 배경 프리팹 인스턴스의 자식들을 실측 좌표에 앉힌다.
        /// ⚠ 패스 ② 는 야자수 좌표가 없어 <b>5그루를 원점에 두고</b> 넘겼다 — 그 자리를 여기서 채운다.
        /// </summary>
        public static void Apply(BlumgiLevelBackground background)
        {
            if (background == null)
            {
                Log.Error("배경 프리팹이 없다 — 배치를 건너뛴다");
                return;
            }

            Transform root = background.transform;

            ApplySolid(Find(root, "Solid"));
            ApplyGrid(Find(root, "Grid"));

            Transform palmRoot = root.Find("Palms");

            if (palmRoot == null)
            {
                Log.Error("배경 프리팹에 Palms 가 없다 — 빌더를 다시 돌린다");
                return;
            }

            if (palmRoot.childCount != Palms.Length)
                Log.Error($"야자수 수가 실측과 다르다: {palmRoot.childCount} — 실측 {Palms.Length}");

            int count = Mathf.Min(palmRoot.childCount, Palms.Length);

            for (int i = 0; i < count; i++)
                ApplyPalm(palmRoot.GetChild(i), Palms[i]);
        }

        /// <summary>
        /// 단색 판. <b>부모가 1/3 이라 화면을 덮으려면 3배 이상</b>이어야 한다 —
        /// 여기서 안 키우면 화면 가장자리에 «판이 끝난 자리»가 보인다.
        /// </summary>
        private static void ApplySolid(Transform solid)
        {
            var renderer = solid == null ? null : solid.GetComponent<SpriteRenderer>();

            if (renderer == null)
                return;

            solid.localPosition = LocalOf(VanishingCenterX, VanishingCenterY);
            solid.localScale = Vector3.one;

            // 16:9 에서 보이는 world 는 대략 2276 × 1280 이다 — 그 3배 위로 넉넉히 잡는다.
            renderer.size = new Vector2(BlumgiUnits.ToUnits(10000.0), BlumgiUnits.ToUnits(6000.0));
        }

        /// <summary>
        /// 격자. <b>타일 피치가 232.5 world 다</b> (소스 50 × <c>imageScale 4.65</c>) —
        /// 구운 타일은 «한 칸 = 1 유닛» 이므로 <b>배율 4.65 를 트랜스폼이 든다</b>.
        /// </summary>
        private static void ApplyGrid(Transform grid)
        {
            var renderer = grid == null ? null : grid.GetComponent<SpriteRenderer>();

            if (renderer == null)
                return;

            grid.localPosition = LocalOf(GridCenterX, GridCenterY);
            grid.localScale = new Vector3(GridImageScale, GridImageScale, 1f);

            // 배율 4.65 «안»의 단위로 준다 — 화면 세로 25.6 유닛을 덮으려면 (25.6 × 3) / 4.65 ≈ 16.5 유닛.
            renderer.size = new Vector2(GridSpriteUnits * 22f, GridSpriteUnits * 14f);
        }

        private static void ApplyPalm(Transform palm, PalmRow row)
        {
            // ── 잎: 표의 (x, y) 는 «원점»이고 원점이 (0.5028, 0.7604) 라 중심이 아니다 [실측].
            double leafCenterX = row.LeafOriginWorldX - (LeafOriginX - 0.5f) * LeafDisplayWidth;
            double leafCenterY = row.LeafOriginWorldY - (LeafOriginY - 0.5f) * LeafDisplayHeight;

            palm.localPosition = LocalOf(leafCenterX, leafCenterY);
            palm.localRotation = Quaternion.identity;
            palm.localScale = Vector3.one;

            Transform leaf = Find(palm, "Leaf");
            Transform trunk = Find(palm, "Trunk");

            // ★ 그리기 순서(잎 아래 · 줄기 위 · 그루마다 +2)는 «프리팹이 든다» —
            //   빌더가 `PalmSortingBase` 를 읽어 그루별로 구워 둔다. 여기서 뒤집지 않는다.
            //   ⚠ 예전에는 이 자리가 프리팹의 반대 순서를 런타임에 덮어쓰고 있었다 (확정표 10-b 위반).

            if (leaf != null)
            {
                leaf.localPosition = Vector3.zero;

                // 소스 180×96 → 표시 434.11×232.82 [실측]. 소스만 보고 쓰면 원본보다 작다.
                leaf.localScale = BlumgiUnits.DisplayScale(LeafSourceWidth, LeafSourceHeight,
                                                           LeafDisplayWidth, LeafDisplayHeight);
            }

            if (trunk == null)
                return;

            // ── 줄기: 원점 (0,0)(좌상단) · 각도 270° ⇒ 화면에서는 «세로 띠»다.
            //    중심 = (원점x + 두께/2, 원점y − 길이/2). 아래 끝(y 5099~5143)이 화면 밖까지 내려간다 [실측].
            double trunkCenterX = row.TrunkOriginWorldX + TrunkThickness * 0.5;
            double trunkCenterY = row.TrunkOriginWorldY - row.TrunkLength * 0.5;

            // 잎이 부모라 잎 배율(2.41×2.43)이 자식에 곱해진다 — 그걸 되돌려야 world 크기가 맞는다.
            Vector3 leafScale = leaf == null ? Vector3.one : leaf.localScale;

            trunk.localPosition = LocalOf(trunkCenterX, trunkCenterY) - LocalOf(leafCenterX, leafCenterY);
            trunk.localRotation = Quaternion.identity;

            var trunkRenderer = trunk.GetComponent<SpriteRenderer>();

            if (trunkRenderer == null)
                return;

            // ★ 타일 배율 3 을 «트랜스폼»이 든다 — 그래서 size 는 3 으로 나눠서 준다.
            //   (size 는 지역 단위라 배율이 곱해진다. 안 나누면 줄기가 3배 굵어지고 3배 길어진다.)
            trunkRenderer.size = new Vector2(BlumgiUnits.ToUnits(TrunkThickness) / TrunkImageScale,
                                             BlumgiUnits.ToUnits(row.TrunkLength) / TrunkImageScale);

            // 줄기가 잎의 «형제»가 아니라 «자식»으로 구워졌으면 잎 배율을 되돌린다.
            if (trunk.parent == leaf)
            {
                trunk.localScale = new Vector3(
                    (leafScale.x == 0f ? 1f : 1f / leafScale.x) * TrunkImageScale,
                    (leafScale.y == 0f ? 1f : 1f / leafScale.y) * TrunkImageScale,
                    1f);
            }
            else
            {
                trunk.localScale = new Vector3(TrunkImageScale, TrunkImageScale, 1f);
            }
        }

        private static Transform Find(Transform root, string childName)
        {
            Transform child = root.Find(childName);

            if (child == null)
                Log.Error($"배경 프리팹에 {childName} 이 없다 — BlumgiPrefabBuilder.BuildAll 을 다시 돌린다");

            return child;
        }
    }
}
