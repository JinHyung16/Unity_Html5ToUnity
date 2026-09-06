using UnityEngine;

namespace JinHyung.UndeadSlayer
{
    /// <summary>
    /// ★★ <b>원본 world px ↔ 유니티 유닛 환산은 여기 한 곳만 통과한다.</b>
    ///
    /// <para>
    /// 원본은 <b>PixiJS 8.15.0</b> 이고 «세로 580 world px 를 고정»한 뒤 가로를 화면비만큼 늘린다
    /// (루트 컨테이너 <c>scale = 렌더러높이 / 580</c> · 검산 2건 · <c>04_UIUX규칙.md</c>).
    /// 물리 엔진을 쓰지 않으므로(확정표 F) <b>절대 스케일에 걸린 상수가 없고</b>,
    /// 그래서 스케일은 «파라미터»가 아니라 «표현 편의»로 고를 수 있다.
    /// </para>
    ///
    /// <para>
    /// ★ 그래서 <b>지형 타일에 맞춘다</b> — 원본 지형은 <b>24 × 24 world px 격자</b>다
    /// (Mesh 지오메트리 직독 · 정점이 24 간격 · 쿼드 1683개 · <c>05_연출.md</c>).
    /// <b>1 유닛 = 24 원본 world px</b> 로 두면 <b>Tilemap 셀 하나가 정확히 1 유닛</b>이 되어
    /// 셀 크기를 만질 일이 없어진다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>PPU 를 코드 여기저기에 상수로 박지 않는다.</b> 임포터도 · 프리팹 빌더도 · 배치도
    /// <see cref="WorldPixelsPerUnit"/> 하나를 읽는다 — 확정이 뒤집혀도 <b>이 한 줄</b>만 고치면 된다.
    /// </para>
    /// </summary>
    public static class UndeadUnits
    {
        /// <summary>
        /// ★ <b>단 하나의 환산 상수</b> — 원본 world px 몇 개가 유니티 1 유닛인가.
        /// <para>24 = 원본 지형 타일 한 칸 [실측]. 스프라이트 임포터 PPU 도 같은 값을 쓴다.</para>
        /// </summary>
        public const float WorldPixelsPerUnit = 24f;

        /// <summary>
        /// UI 캔버스의 <c>referencePixelsPerUnit</c>.
        /// ⚠ <b>UI 스프라이트 PPU 와 «같은 값»이어야 한다</b> — 어긋나면 <c>Image</c> 가 9-slice 경계를
        /// 그 비로 나눠 써 모서리가 통째로 늘어난다. UI 는 월드와 분리돼 있어 위 상수와 무관하다.
        /// </summary>
        public const float UiPixelsPerUnit = 100f;

        /// <summary>
        /// 설계 뷰포트 <b>세로</b> [실측 · 확정표 3-d]. <b>이 축이 고정된다</b> —
        /// 가로는 화면비가 정하므로 상수로 두지 않는다.
        /// </summary>
        public const float DesignViewportHeight = 580f;

        /// <summary>
        /// 직교 카메라 크기 (유닛). 원본이 세로 580 world 고정이므로 반이 290 world 다.
        /// <para>580 ÷ 2 ÷ 24 = <b>12.0833…</b></para>
        /// </summary>
        public static float CameraOrthographicSize
        {
            get { return DesignViewportHeight * 0.5f / WorldPixelsPerUnit; }
        }

        /// <summary>원본 world 길이 → 유닛.</summary>
        public static float ToUnits(double worldLength)
        {
            return (float)(worldLength / WorldPixelsPerUnit);
        }

        /// <summary>
        /// 원본 world 좌표 → 유니티 위치.
        /// ⚠ 원본(Pixi)은 <b>아래가 +y</b> 이고 유니티는 위가 +y 다 — <b>여기서만</b> 뒤집는다.
        /// </summary>
        public static Vector3 ToPosition(double worldX, double worldY)
        {
            return new Vector3((float)(worldX / WorldPixelsPerUnit),
                               (float)(-worldY / WorldPixelsPerUnit),
                               0f);
        }

        /// <summary>
        /// 원본 world 벡터(속도 등) → 유니티 벡터. 위치와 <b>같은 y 뒤집기</b>를 탄다.
        /// 속도를 위치와 다른 규칙으로 넘기면 궤적이 «위아래만» 뒤집힌 채로 그럴듯하게 돌아간다.
        /// </summary>
        public static Vector2 ToVector(double worldX, double worldY)
        {
            return new Vector2((float)(worldX / WorldPixelsPerUnit),
                               (float)(-worldY / WorldPixelsPerUnit));
        }

        /// <summary>유니티 길이 → 원본 world 길이. 엔진에서 «되읽을» 때 쓴다 (골든 벡터 대조).</summary>
        public static double ToWorldLength(float units)
        {
            return units * (double)WorldPixelsPerUnit;
        }

        /// <summary><see cref="ToVector"/> 의 역 — <b>y 를 되뒤집는다</b>.</summary>
        public static void ToWorld(Vector2 value, out double worldX, out double worldY)
        {
            worldX = value.x * (double)WorldPixelsPerUnit;
            worldY = -value.y * (double)WorldPixelsPerUnit;
        }
    }
}
